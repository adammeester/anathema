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

        public void Init(GameObject ownerObject, int team, float damageAmount, float moveSpeed, float range, float hitRadius, Vector3 moveDirection, System.Action<Health> onHitCallback = null)
        {
            onHit = onHitCallback;
            owner = ownerObject;
            ownerTeam = team;
            damage = damageAmount;
            speed = moveSpeed;
            remainingRange = range;
            radius = hitRadius;
            direction = moveDirection.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remainingRange);

            if (TryFindHit(step, out RaycastHit hit))
            {
                Health target = hit.collider.GetComponentInParent<Health>();
                if (target != null && target.TakeDamage(damage, owner)) onHit?.Invoke(target);
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
