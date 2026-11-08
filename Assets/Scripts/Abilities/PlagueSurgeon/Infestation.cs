using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Status effect: a plague fly feeding on (or tending) whoever it is attached to. Added to the host at runtime by the
    // surgeon's Pestilent Fly, on the surgeon's machine only; the damage and healing it deals are forwarded to the host's
    // own machine like any other hit. Enemy hosts take damage each tick, ally hosts are healed.
    // Re-applying replaces the previous infestation. The EX fly never attaches: it calls Detonate where it lands.
    public class Infestation : MonoBehaviour
    {
        const float TickInterval = 0.5f;

        GameObject source;
        int sourceTeam;
        Energy sourceEnergy;
        Health host;
        float endTime;
        float totalDuration;
        float nextTick;
        float damagePerTick;
        float healPerTick;
        float energyPerTick;
        Transform fly;

        bool HostIsEnemy => host.Team != sourceTeam;

        public static Infestation Apply(Health target, GameObject source, int sourceTeam, Energy sourceEnergy,
            float duration, float enemyDps, float allyHps, float energyGain)
        {
            var existing = target.GetComponent<Infestation>();
            if (existing != null) Destroy(existing);

            var inf = target.gameObject.AddComponent<Infestation>();
            inf.host = target;
            inf.source = source;
            inf.sourceTeam = sourceTeam;
            inf.sourceEnergy = sourceEnergy;
            inf.totalDuration = Mathf.Max(0.01f, duration);
            inf.endTime = Time.time + duration;
            inf.nextTick = Time.time + TickInterval;
            inf.damagePerTick = enemyDps * TickInterval;
            inf.healPerTick = allyHps * TickInterval;
            inf.energyPerTick = energyGain;
            inf.BuildFly();
            return inf;
        }

        void BuildFly()
        {
            fly = FlyVisual.Create(transform, false, 0.8f).transform;
        }

        void Update()
        {
            if (host == null || host.IsDead || source == null)
            {
                Destroy(this);
                return;
            }

            // Orbit the host's shoulders.
            float t = Time.time * 6f;
            Vector3 previous = fly.localPosition;
            fly.localPosition = new Vector3(Mathf.Cos(t) * 0.6f, 1.6f + Mathf.Sin(t * 1.7f) * 0.15f, Mathf.Sin(t) * 0.6f);
            Vector3 heading = fly.localPosition - previous;
            if (heading.sqrMagnitude > 1e-8f) fly.localRotation = Quaternion.LookRotation(heading);

            if (Time.time >= endTime)
            {
                Destroy(this);
                return;
            }

            if (Anathema.Net.NetGame.Frozen) return;
            while (Time.time >= nextTick)
            {
                nextTick += TickInterval;
                Tick();
                if (host == null || host.IsDead) return;
            }
        }

        void Tick()
        {
            if (HostIsEnemy)
            {
                if (host.TakeDamage(damagePerTick, source, true) && sourceEnergy != null) sourceEnergy.Gain(energyPerTick);
            }
            else
            {
                Healing.Apply(host, healPerTick);
            }
        }

        // The EX fly's burst: damage and panic for every enemy near `center`, healing for every ally, all at once.
        // `visualOnly` (a remote surgeon's replica) just plays the flash; the real hits come from the owner's machine.
        public static void Detonate(Vector3 center, GameObject source, int sourceTeam, float radius,
            float damage, float heal, float panic, bool visualOnly)
        {
            BurstEffect.Spawn(center, radius);
            if (visualOnly) return;

            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.IsDead) continue;

                Vector3 offset = h.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > radius) continue;

                if (h.Team != sourceTeam)
                {
                    h.TakeDamage(damage, source, true);
                    // The stench sends survivors running from the surgeon.
                    if (!h.IsDead && panic > 0f) Panicked.Apply(h.gameObject, source, panic);
                }
                else Healing.Apply(h, heal);
            }
        }

        void OnDestroy()
        {
            if (fly != null) Destroy(fly.gameObject);
        }

        // A quick expanding ring-and-dome flash: sickly green for the damage, pale gold for the healing, both at once.
        public class BurstEffect : MonoBehaviour
        {
            const float Duration = 0.4f;

            Transform dome;
            Transform glow;
            Material domeMaterial;
            Material glowMaterial;
            Vector3 center;
            float radius;
            float startTime;

            public static void Spawn(Vector3 position, float radius)
            {
                var go = new GameObject("Plague Burst");
                var fx = go.AddComponent<BurstEffect>();
                fx.center = position;
                fx.radius = radius;
                fx.startTime = Time.time;
                fx.Build();
            }

            void Build()
            {
                domeMaterial = VisualUtil.UnlitMaterial(new Color(0.4f, 0.8f, 0.1f, 0.4f));
                glowMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.95f, 0.5f, 0.35f));
                dome = MakeSphere("Poison Dome", domeMaterial);
                glow = MakeSphere("Healing Glow", glowMaterial);
            }

            Transform MakeSphere(string name, Material material)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = name;
                Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.GetComponent<Renderer>().sharedMaterial = material;
                s.transform.SetParent(transform, false);
                return s.transform;
            }

            void Update()
            {
                float t = (Time.time - startTime) / Duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                float eased = 1f - (1f - t) * (1f - t);
                dome.position = center + Vector3.up * 0.5f;
                glow.position = center + Vector3.up * 0.5f;
                dome.localScale = Vector3.one * (radius * 2f * eased);
                glow.localScale = Vector3.one * (radius * 1.4f * eased);

                SetAlpha(domeMaterial, 0.4f * (1f - t));
                SetAlpha(glowMaterial, 0.35f * (1f - t));
            }

            static void SetAlpha(Material material, float alpha)
            {
                Color c = material.color;
                c.a = alpha;
                material.color = c;
            }

            void OnDestroy()
            {
                if (domeMaterial != null) Destroy(domeMaterial);
                if (glowMaterial != null) Destroy(glowMaterial);
            }
        }
    }
}
