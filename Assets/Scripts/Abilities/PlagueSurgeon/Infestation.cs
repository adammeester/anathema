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
        bool jumped;   // Hungry Brood: a fly only ever jumps once
        float nextCarrier; // Carrier Fly: when a feeding fly next lays a Festering stack

        // Every fly currently attached, on this machine (the surgeon's).
        static readonly System.Collections.Generic.List<Infestation> Active = new System.Collections.Generic.List<Infestation>();

        bool HostIsEnemy => host.Team != sourceTeam;

        // Hive Shell: is one of this surgeon's flies tending a living ally right now?
        public static bool IsTendingFor(GameObject surgeon)
        {
            foreach (Infestation inf in Active)
                if (inf != null && inf.source == surgeon && inf.host != null && !inf.host.IsDead && !inf.HostIsEnemy) return true;
            return false;
        }

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

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
            inf.nextCarrier = Time.time + PlagueSurgeonUpgrade.CarrierInterval;
            inf.damagePerTick = enemyDps * TickInterval;
            inf.healPerTick = allyHps * TickInterval;
            inf.energyPerTick = energyGain;
            inf.BuildFly();
            // The swarm buzzing on them for as long as the fly stays on (the loop goes with the fly).
            Sfx.Loop(Sfx.Flies, inf.fly, 0.35f, Random.Range(0.95f, 1.05f), 0.15f);
            return inf;
        }

        void BuildFly()
        {
            fly = FlyVisual.Create(transform, false, 0.8f).transform;
        }

        void Update()
        {
            if (source == null || host == null)
            {
                Destroy(this);
                return;
            }

            if (host.IsDead)
            {
                TryJump();
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
                TryJump();
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

        // Hungry Brood: a fly whose host died or whose time ran out hops once to the nearest character on the same side
        // as its old host (a feeding fly finds another enemy, a tending fly another ally; never the surgeon himself).
        void TryJump()
        {
            if (jumped || source == null || !PlagueSurgeonUpgradeEffects.Has(source, PlagueSurgeonUpgrade.HungryBrood)) return;

            bool feeding = HostIsEnemy;
            Health best = null;
            float bestDistance = PlagueSurgeonUpgrade.BroodRange;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h == host || h.IsDead || h.gameObject == source) continue;
                if ((h.Team != sourceTeam) != feeding) continue;

                Vector3 offset = h.transform.position - host.transform.position;
                offset.y = 0f;
                if (offset.magnitude <= bestDistance)
                {
                    bestDistance = offset.magnitude;
                    best = h;
                }
            }
            if (best == null) return;

            Vector3 from = fly != null ? fly.position : host.transform.position + Vector3.up * 1.6f;
            BroodHop.Launch(from, best, source, sourceTeam, sourceEnergy, totalDuration * PlagueSurgeonUpgrade.BroodDurationScale,
                damagePerTick / TickInterval, healPerTick / TickInterval, energyPerTick);
        }

        // The fly crossing to its new host. It latches on when it arrives (if the new host is still standing).
        class BroodHop : MonoBehaviour
        {
            const float Speed = 16f;

            Health target;
            GameObject source;
            int sourceTeam;
            Energy sourceEnergy;
            float duration, enemyDps, allyHps, energyGain;

            public static void Launch(Vector3 from, Health target, GameObject source, int sourceTeam, Energy sourceEnergy,
                float duration, float enemyDps, float allyHps, float energyGain)
            {
                var go = new GameObject("Brood Hop");
                Sfx.Loop(Sfx.Flies2, go.transform, 0.3f, Random.Range(0.95f, 1.05f), 0f, true); // in flight to the next host
                go.transform.position = from;
                var hop = go.AddComponent<BroodHop>();
                hop.target = target;
                hop.source = source;
                hop.sourceTeam = sourceTeam;
                hop.sourceEnergy = sourceEnergy;
                hop.duration = duration;
                hop.enemyDps = enemyDps;
                hop.allyHps = allyHps;
                hop.energyGain = energyGain;
                FlyVisual.Create(go.transform, false, 0.9f);
            }

            void Update()
            {
                if (target == null || target.IsDead || source == null)
                {
                    Destroy(gameObject);
                    return;
                }

                Vector3 goal = target.transform.position + Vector3.up * 1.6f;
                Vector3 to = goal - transform.position;
                float step = Speed * Time.deltaTime;
                if (to.magnitude <= step)
                {
                    Infestation inf = Apply(target, source, sourceTeam, sourceEnergy, duration, enemyDps, allyHps, energyGain);
                    inf.jumped = true;
                    Destroy(gameObject);
                    return;
                }

                transform.position += to.normalized * step;
                transform.rotation = Quaternion.LookRotation(to);
            }
        }

        void Tick()
        {
            if (HostIsEnemy)
            {
                Energy energy = sourceEnergy;
                float gain = energyPerTick;
                Health leech = ParasiteFor(source);
                float amount = damagePerTick;
                host.Strike(Hit.Direct(damagePerTick).AsAoe().AsDot(), source, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(gain);
                    if (leech != null && !leech.IsDead) Healing.Apply(leech, amount, leech.gameObject);
                });

                // Carrier Fly: the feeding fly seeds the host with the surgeon's rot.
                if (!host.IsDead && Time.time >= nextCarrier && PlagueSurgeonUpgradeEffects.Has(source, PlagueSurgeonUpgrade.CarrierFly))
                {
                    nextCarrier = Time.time + PlagueSurgeonUpgrade.CarrierInterval;
                    PlagueSurgeonUpgradeEffects.ApplySpitRot(source, host, sourceEnergy);
                }
            }
            else
            {
                Healing.Apply(host, healPerTick, source);
            }
        }

        // The EX fly's burst: damage and panic for every enemy near `center`, healing for every ally, all at once.
        // `visualOnly` (a remote surgeon's replica) just plays the flash; the real hits come from the owner's machine.
        public static void Detonate(Vector3 center, GameObject source, int sourceTeam, float radius,
            float damage, float heal, float panic, bool visualOnly)
        {
            BurstEffect.Spawn(center, radius);
            Sfx.Play(Sfx.DiseaseBurst, 0.5f, 0.95f, 1.05f, 1.6f);
            if (visualOnly) return;

            // Parasitic Bond: the burst heals the surgeon once, on the first enemy it actually hurts.
            Health leech = ParasiteFor(source);
            bool leeched = false;
            System.Action<bool> onBurstResult = leech == null ? null : landed =>
            {
                if (!landed || leeched || leech == null || leech.IsDead) return;
                leeched = true;
                Healing.Apply(leech, damage, leech.gameObject);
            };

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead) continue;

                Vector3 offset = h.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > radius) continue;

                if (h.Team != sourceTeam)
                {
                    // The stench sends survivors running from the surgeon.
                    Hit burst = Hit.Direct(damage).AsAoe();
                    if (panic > 0f) burst = burst.WithPanic(panic);
                    h.Strike(burst, source, onBurstResult);
                }
                else Healing.Apply(h, heal, source);
            }
        }

        // Parasitic Bond: the surgeon to heal for the fly's damage, or null without the upgrade.
        static Health ParasiteFor(GameObject surgeon)
        {
            if (surgeon == null || !PlagueSurgeonUpgradeEffects.Has(surgeon, PlagueSurgeonUpgrade.ParasiticBond)) return null;
            return surgeon.GetComponent<Health>();
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
