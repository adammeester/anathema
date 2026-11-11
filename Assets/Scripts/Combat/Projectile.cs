using UnityEngine;

namespace Anathema.Combat
{
    public class Projectile : MonoBehaviour
    {
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        int ownerTeam;
        float damage;
        float speed;
        float radius;
        float remainingRange;
        Vector3 direction;
        GameObject owner;
        System.Action<Health> onHit;
        System.Func<Health, float> damageScale;

        // `damageScaleFor` (optional) is asked for a multiplier on the damage when the projectile reaches a target.
        public Projectile Init(GameObject ownerObject, int team, float damageAmount, float moveSpeed, float range, float hitRadius, Vector3 moveDirection, System.Action<Health> onHitCallback = null, System.Func<Health, float> damageScaleFor = null)
        {
            onHit = onHitCallback;
            damageScale = damageScaleFor;
            owner = ownerObject;
            ownerTeam = team;
            damage = damageAmount;
            speed = moveSpeed;
            remainingRange = range;
            radius = hitRadius;
            direction = moveDirection.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
            spawnPoint = transform.position;
            return this;
        }

        Vector3 spawnPoint;

        // Status effects the projectile carries (stun, bleed...). They travel with its damage as one Hit, so they only
        // land if the damage does. Build with Hit.Direct(0).With...(); the damage is filled in on impact.
        Hit riders;
        bool hasRiders;

        public Projectile WithRiders(Hit carried)
        {
            riders = carried;
            hasRiders = true;
            return this;
        }

        // Optional: allies (never the shooter) stop the projectile too, and are handed to this instead of being hit
        // (e.g. Tonic Spit heals them). Without it the projectile passes through allies.
        System.Action<Health> onAllyHit;

        public Projectile WithAllyHit(System.Action<Health> onAlly)
        {
            onAllyHit = onAlly;
            return this;
        }

        // An enemy dome turns the projectile around, back to where it was fired from. It now belongs to the dome's
        // owner, so it can hit the original shooter (and no longer reports hits to the original shooter's ability).
        void TryReflect()
        {
            foreach (ReflectDome dome in ReflectDome.All)
            {
                if (dome.Team == ownerTeam || !dome.Contains(transform.position)) continue;

                Vector3 back = spawnPoint - transform.position;
                direction = back.sqrMagnitude > 0.01f ? back.normalized : -direction;
                transform.rotation = Quaternion.LookRotation(direction);
                remainingRange = Mathf.Max(remainingRange, back.magnitude + 2f);
                owner = dome.Owner;
                ownerTeam = dome.Team;
                onHit = null;
                onAllyHit = null;
                damageScale = null;
                hasRiders = false; // the shooter's effects don't come back at them
                return;
            }
        }

        void Update()
        {
            TryReflect();
            float step = Mathf.Min(speed * Time.deltaTime, remainingRange);

            if (TryFindHit(step, out RaycastHit hit))
            {
                Health target = hit.collider.GetComponentInParent<Health>();
                float scale = target != null && damageScale != null ? damageScale(target) : 1f;
                if (target != null && target.Team == ownerTeam)
                {
                    onAllyHit?.Invoke(target);
                }
                else if (target != null)
                {
                    System.Action<Health> hitCallback = onHit; // the reflect branch above may clear it later
                    Hit strike = hasRiders ? riders : Hit.Direct(0f);
                    strike.Damage = damage * scale;
                    target.Strike(strike, owner, landed => { if (landed) hitCallback?.Invoke(target); });
                }
                else
                {
                    Sfx.Play(Sfx.Impact, 0.25f, 0.9f, 1.15f, 0.6f); // spent against a wall or pillar
                }
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
            remainingRange -= step;
            if (remainingRange <= 0f) Destroy(gameObject);
        }

        // Nearest solid hit along this frame's step. Allies (including the shooter) and the dead are skipped;
        // anything else without Health (walls, pillars) stops the projectile.
        bool TryFindHit(float distance, out RaycastHit best)
        {
            best = default;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                Health health = h.collider.GetComponentInParent<Health>();
                if (health != null && (health.IsDead || (health.Team == ownerTeam && (onAllyHit == null || health.gameObject == owner)))) continue;
                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    best = h;
                    found = true;
                }
            }

            return found;
        }
    }
}
