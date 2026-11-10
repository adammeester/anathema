using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: damage over time that leaves blood on the ground. Added to the victim at runtime by whatever
    // applies it (Bleeding.Apply); re-applying refreshes the duration and keeps the stronger bleed.
    public class Bleeding : MonoBehaviour
    {
        const float TickInterval = 1f;
        const float DripInterval = 0.3f;
        const float DripMinDistance = 0.4f;
        const float DripMaxDistance = 1.1f;

        float endTime;
        float damagePerSecond;
        float nextTickTime;
        float nextDripTime;
        GameObject source;
        Health health;
        bool visualOnly; // a copy drawn from the server's published state: shows the blood, the server takes the damage

        // The bleed lives on the server's copy of the victim (it deals the damage there); everyone else draws it from
        // the server's published state (NetPlayer.SyncServerFx).
        public static Bleeding Apply(GameObject target, GameObject damageSource, float damagePerSecond, float duration)
        {
            if (target.TryGetComponent(out Health targetHealth) && !targetHealth.IsAuthority)
            {
                targetHealth.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Bleed, damagePerSecond, duration, Vector3.zero, damageSource);
                return null;
            }

            var bleeding = target.GetComponent<Bleeding>();
            if (bleeding == null)
            {
                bleeding = target.AddComponent<Bleeding>();
                bleeding.nextTickTime = Time.time + TickInterval;
            }
            bleeding.visualOnly = false;
            bleeding.source = damageSource;
            bleeding.endTime = Time.time + duration;
            bleeding.damagePerSecond = Mathf.Max(bleeding.damagePerSecond, damagePerSecond);
            return bleeding;
        }

        // A bleed that is really doing damage here (not a drawn copy).
        public static bool IsBleedingForReal(GameObject target) => target.TryGetComponent(out Bleeding b) && !b.visualOnly;

        // Online: draws a bleed the server reports; lasts until the server says it has stopped.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Bleeding _)) return;
            var bleeding = target.AddComponent<Bleeding>();
            bleeding.visualOnly = true;
            bleeding.endTime = float.MaxValue;
            bleeding.nextTickTime = float.MaxValue;
        }

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void Update()
        {
            if (health == null || health.IsDead || Time.time >= endTime)
            {
                Destroy(this);
                return;
            }

            if (Time.time >= nextTickTime)
            {
                nextTickTime += TickInterval;
                if (!visualOnly) health.TakeDamage(damagePerSecond * TickInterval, source, true, dot: true);
                if (health.IsDead) return;
            }

            if (Time.time >= nextDripTime)
            {
                nextDripTime = Time.time + DripInterval;
                // Splatter out to a random side so the blood shows even when the victim stands still.
                Vector2 side = Random.insideUnitCircle.normalized * Random.Range(DripMinDistance, DripMaxDistance);
                BloodDrop.Spawn(new Vector3(transform.position.x + side.x, 0.14f, transform.position.z + side.y));
            }
        }
    }
}
