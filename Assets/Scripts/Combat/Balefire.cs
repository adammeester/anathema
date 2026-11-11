using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: green, witch-lit flames (the Mandrake's Balefire). It burns like Burning, but it is tied to the
    // character who lit it: the next time that character lands any hit on the victim (Health.ApplyHitLocal calls
    // OnStruck), everything the balefire still had left to burn lands at once and the flames go out. Damage-over-time
    // ticks (poison, other burns) don't set it off; only real hits do.
    // Re-applying refreshes the duration and keeps the stronger burn.
    // Like Burning it lives on the server's copy of the victim (it deals the damage there); everyone else draws a
    // visual-only copy from the server's published state (NetPlayer.SyncServerFx).
    public class Balefire : MonoBehaviour
    {
        const float TickInterval = 0.5f;
        const int Flames = 6;
        const float RingRadius = 0.6f;

        // Violet witch-fire, so it never reads as the Mandrake's green poison.
        public static readonly Color Core = new Color(0.55f, 0.15f, 1f, 0.8f);
        public static readonly Color Tip = new Color(1f, 0.65f, 1f, 0.9f);
        static readonly Color RingColor = new Color(0.45f, 0.1f, 0.9f, 0.45f);

        float endTime;
        float damagePerSecond;
        float nextTickTime;
        GameObject source;
        Health health;
        bool visualOnly; // drawn from the server's published state: shows the flames, the server takes the damage

        Transform[] flames;
        float[] phase;
        Material flameMaterial;
        Material tipMaterial;
        Transform ring;
        Material ringMaterial;

        // What the flames would still deal if left to burn out.
        public float RemainingDamage => damagePerSecond * Mathf.Max(0f, endTime - Time.time);

        public static Balefire Apply(GameObject target, GameObject damageSource, float damagePerSecond, float duration)
        {
            if (target == null) return null;
            if (target.TryGetComponent(out Health targetHealth))
            {
                if (targetHealth.Structure || targetHealth.IsDead) return null;
                if (!targetHealth.IsAuthority)
                {
                    targetHealth.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Balefire, damagePerSecond, duration, Vector3.zero, damageSource);
                    return null;
                }
            }

            var balefire = target.GetComponent<Balefire>();
            if (balefire == null)
            {
                balefire = target.AddComponent<Balefire>();
                balefire.nextTickTime = Time.time + TickInterval;
            }
            balefire.visualOnly = false;
            balefire.source = damageSource;
            balefire.endTime = Time.time + duration;
            balefire.damagePerSecond = Mathf.Max(balefire.damagePerSecond, damagePerSecond);
            return balefire;
        }

        // Authority: `attacker` just landed a hit on `victim`. If it is the one who lit the victim's balefire, the
        // flames burst.
        public static void OnStruck(GameObject victim, GameObject attacker)
        {
            if (victim == null || attacker == null) return;
            if (!victim.TryGetComponent(out Balefire b) || b.visualOnly || b.source != attacker) return;
            b.Detonate();
        }

        void Detonate()
        {
            float burst = RemainingDamage;
            endTime = 0f; // spent, whatever happens below
            Destroy(this);
            PlayBurst(transform.position);
            if (health != null && health.Net != null && health.Net.IsSpawned) health.Net.ServerBalefireBurst();
            if (health != null && burst > 0f) health.TakeDamage(burst, source, false, dot: true);
        }

        // The burst: a flash of green fire and a spray of embers. Every machine plays it (NetPlayer.ServerBalefireBurst).
        public static void PlayBurst(Vector3 at)
        {
            var go = new GameObject("Balefire Burst");
            go.transform.position = at + Vector3.up;
            go.AddComponent<BurstFx>();
        }

        sealed class BurstFx : MonoBehaviour
        {
            const float Life = 0.5f;
            const int Embers = 12;
            Transform core;
            Transform[] embers;
            Vector3[] velocity;
            Material coreMaterial;
            Material emberMaterial;
            float start;

            void Awake()
            {
                start = Time.time;
                coreMaterial = VisualUtil.UnlitMaterial(Core, 3003);
                emberMaterial = VisualUtil.UnlitMaterial(Tip, 3004);
                core = Part("Burst Core", coreMaterial);
                embers = new Transform[Embers];
                velocity = new Vector3[Embers];
                for (int i = 0; i < Embers; i++)
                {
                    embers[i] = Part("Burst Ember", emberMaterial);
                    embers[i].localScale = Vector3.one * 0.14f;
                    velocity[i] = (Random.onUnitSphere + Vector3.up * 0.9f).normalized * Random.Range(3.5f, 7f);
                }
                Sfx.Play(Sfx.SlamExplosion, 0.4f, 1.3f, 1.5f, 0.6f);
                Sfx.Play(Sfx.GroundFire, 0.35f, 1.2f, 1.35f, 0.5f);
            }

            Transform Part(string partName, Material material)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.name = partName;
                Destroy(p.GetComponent<Collider>());
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.transform.SetParent(transform, false);
                return p.transform;
            }

            void Update()
            {
                float t = (Time.time - start) / Life;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                core.localScale = new Vector3(1f, 1.5f, 1f) * Mathf.Lerp(0.5f, 3f, Mathf.Sqrt(t));
                Color c = Core;
                c.a = Core.a * (1f - t);
                coreMaterial.color = c;
                for (int i = 0; i < embers.Length; i++)
                {
                    velocity[i] += Physics.gravity * Time.deltaTime * 0.4f;
                    embers[i].localPosition += velocity[i] * Time.deltaTime;
                }
            }

            void OnDestroy()
            {
                if (coreMaterial != null) Destroy(coreMaterial);
                if (emberMaterial != null) Destroy(emberMaterial);
            }
        }

        public static bool IsBurning(GameObject target) => target != null && target.TryGetComponent(out Balefire _);

        // A balefire that is really doing damage here (not a drawn copy).
        public static bool IsBurningForReal(GameObject target) => target.TryGetComponent(out Balefire b) && !b.visualOnly;

        // Online: draws a balefire the server reports; lasts until the server says it has stopped.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Balefire _)) return;
            var balefire = target.AddComponent<Balefire>();
            balefire.visualOnly = true;
            balefire.endTime = float.MaxValue;
            balefire.nextTickTime = float.MaxValue;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            flameMaterial = VisualUtil.UnlitMaterial(Core, 3002);
            tipMaterial = VisualUtil.UnlitMaterial(Tip, 3003);
            ringMaterial = VisualUtil.UnlitMaterial(RingColor, 3001);

            // A witch-fire circle burns on the ground around the victim's feet.
            GameObject ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "Balefire Ring";
            Destroy(ringGo.GetComponent<Collider>());
            var ringRenderer = ringGo.GetComponent<Renderer>();
            ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ring = ringGo.transform;

            flames = new Transform[Flames];
            phase = new float[Flames];
            for (int i = 0; i < Flames; i++)
            {
                GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "Balefire Flame";
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
            if (ring != null) Destroy(ring.gameObject);
            if (flameMaterial != null) Destroy(flameMaterial);
            if (tipMaterial != null) Destroy(tipMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
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
                if (!visualOnly) health.TakeDamage(damagePerSecond * TickInterval, source, false, dot: true);
                if (health.IsDead) return;
            }

            // The ground circle pulses.
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            ring.localScale = new Vector3(RingRadius * 2f * pulse, 0.01f, RingRadius * 2f * pulse);

            // Tall violet tongues of flame stand on the circle and lick upward, each stretching and shrinking on its
            // own beat (they hold their places rather than drifting up, unlike poison's rising droplets).
            for (int i = 0; i < flames.Length; i++)
            {
                float t = Time.time * 9f + phase[i];
                float angle = i * Mathf.PI * 2f / flames.Length + 0.15f * Mathf.Sin(t * 0.5f);
                float height = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(t)) + (i % 2 == 0 ? 0.25f : 0f);
                float width = 0.16f + 0.04f * Mathf.Sin(t * 1.7f);
                flames[i].localPosition = new Vector3(Mathf.Cos(angle) * RingRadius * 0.75f, height * 0.5f + 0.1f, Mathf.Sin(angle) * RingRadius * 0.75f);
                flames[i].localScale = new Vector3(width, height, width);
            }
        }
    }
}
