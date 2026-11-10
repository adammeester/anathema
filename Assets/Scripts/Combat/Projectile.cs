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
        public void Init(GameObject ownerObject, int team, float damageAmount, float moveSpeed, float range, float hitRadius, Vector3 moveDirection, System.Action<Health> onHitCallback = null, System.Func<Health, float> damageScaleFor = null)
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
        }

        Vector3 spawnPoint;

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
                damageScale = null;
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
                if (target != null && target.TakeDamage(damage * scale, owner)) onHit?.Invoke(target);
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
                if (health != null && (health.Team == ownerTeam || health.IsDead)) continue;
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
