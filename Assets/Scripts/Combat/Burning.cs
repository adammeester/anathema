using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: damage over time that wreathes the victim in flames. Added to the victim at runtime by whatever
    // applies it (Burning.Apply); re-applying refreshes the duration and keeps the stronger burn.
    public class Burning : MonoBehaviour
    {
        const float TickInterval = 1f;
        const int Flames = 5;

        float endTime;
        float damagePerSecond;
        float nextTickTime;
        GameObject source;
        Health health;
        bool visualOnly; // a copy drawn from the server's published state: shows the flames, the server takes the damage

        Transform[] flames;
        float[] phase;
        Material flameMaterial;
        Material tipMaterial;

        // The burn lives on the server's copy of the victim (it deals the damage there); everyone else draws it from
        // the server's published state (NetPlayer.SyncServerFx).
        public static Burning Apply(GameObject target, GameObject damageSource, float damagePerSecond, float duration)
        {
            if (target.TryGetComponent(out Health targetHealth) && !targetHealth.IsAuthority)
            {
                targetHealth.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Burn, damagePerSecond, duration, Vector3.zero, damageSource);
                return null;
            }

            var burning = target.GetComponent<Burning>();
            if (burning == null)
            {
                burning = target.AddComponent<Burning>();
                burning.nextTickTime = Time.time + TickInterval;
            }
            burning.visualOnly = false;
            burning.source = damageSource;
            burning.endTime = Time.time + duration;
            burning.damagePerSecond = Mathf.Max(burning.damagePerSecond, damagePerSecond);
            return burning;
        }

        public static bool IsBurning(GameObject target) => target.TryGetComponent(out Burning _);

        // A burn that is really doing damage here (not a drawn copy).
        public static bool IsBurningForReal(GameObject target) => target.TryGetComponent(out Burning b) && !b.visualOnly;

        // Online: draws a burn the server reports; lasts until the server says it has stopped.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Burning _)) return;
            var burning = target.AddComponent<Burning>();
            burning.visualOnly = true;
            burning.endTime = float.MaxValue;
            burning.nextTickTime = float.MaxValue;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            flameMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.45f, 0.08f, 0.75f), 3002);
            tipMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.9f, 0.4f, 0.85f), 3003);

            flames = new Transform[Flames];
            phase = new float[Flames];
            for (int i = 0; i < Flames; i++)
            {
                GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "Burn Flame";
                Destroy(f.GetComponent<Collider>());
                var r = f.GetComponent<Renderer>();
                r.sharedMaterial = i % 2 == 0 ? flameMaterial : tipMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.transform.SetParent(transform, false);
                flames[i] = f.transform;
                phase[i] = Random.value * 10f;
            }
        }

        void OnDestroy()
        {
            if (flames != null) foreach (Transform f in flames) if (f != null) Destroy(f.gameObject);
            if (flameMaterial != null) Destroy(flameMaterial);
            if (tipMaterial != null) Destroy(tipMaterial);
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

            // Flames lick up around the body, flickering.
            for (int i = 0; i < flames.Length; i++)
            {
                float t = Time.time * 6f + phase[i];
                float angle = phase[i] + Time.time * 1.5f;
                float lift = 0.2f + 0.45f * i / (flames.Length - 1) + 0.12f * Mathf.Sin(t);
                float s = 0.28f + 0.1f * Mathf.Sin(t * 1.7f);
                flames[i].localPosition = new Vector3(Mathf.Cos(angle) * 0.32f, lift * 2f, Mathf.Sin(angle) * 0.32f);
                flames[i].localScale = new Vector3(s, s * 1.6f, s);
            }
        }
    }
}
