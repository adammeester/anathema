using UnityEngine;

namespace Anathema.Combat
{
    // Test aid: fires a basic projectile at the nearest living enemy-team Health on a fixed interval,
    // so counters can be tested without anyone else attacking.
    [RequireComponent(typeof(Health))]
    public class DummyShooter : MonoBehaviour
    {
        [SerializeField] float interval = 1.5f;
        [SerializeField] float damage = 8f;
        [SerializeField] float range = 30f;
        [SerializeField] float projectileSpeed = 18f;
        [SerializeField] float projectileRadius = 0.25f;
        [SerializeField] float muzzleHeight = 0.6f;
        [SerializeField] Color projectileColor = new Color(1f, 0.2f, 0.2f);

        Health health;
        Material projectileMaterial;
        float nextShot;

        public void Configure(float shotInterval, float shotDamage)
        {
            interval = shotInterval;
            damage = shotDamage;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            nextShot = Time.time + interval;
        }

        void Update()
        {
            if (health.IsDead || Time.time < nextShot || Stunned.IsStunned(gameObject)) return;

            Health target = FindTarget();
            if (target == null) return;

            nextShot = Time.time + interval;
            Fire(target);
        }

        Health FindTarget()
        {
            Health best = null;
            float bestDistance = float.MaxValue;

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.Team == health.Team || h.IsDead || h.Structure) continue;
                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = h;
                }
            }

            return best;
        }

        void Fire(Health target)
        {
            Vector3 origin = transform.position + Vector3.up * muzzleHeight;
            Vector3 aimPoint = target.transform.position + Vector3.up * muzzleHeight;
            Vector3 dir = aimPoint - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            transform.rotation = Quaternion.LookRotation(dir);
            origin += dir * 0.7f;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dummy Projectile";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * projectileRadius * 2f;

            if (projectileMaterial == null) projectileMaterial = VisualUtil.UnlitMaterial(projectileColor);
            go.GetComponent<Renderer>().sharedMaterial = projectileMaterial;

            go.AddComponent<Projectile>().Init(gameObject, health.Team, damage, projectileSpeed, range, projectileRadius, dir);
        }
    }
}
