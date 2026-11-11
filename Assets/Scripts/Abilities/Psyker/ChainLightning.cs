using System.Collections.Generic;
using Anathema.Abilities.SkitariiRanger;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // RMB: a bolt of white lightning that strikes the first enemy along the aim line, then leaps to the nearest other
    // enemies, each jump weaker than the last. Instant. Hits feed the energy bar.
    // Passive: everyone it strikes is left Charged (a rune above their head that other abilities can react to).
    //
    // Force Stave (Armament upgrade): this becomes Kine Slam, a shockwave around the Psyker that damages, shoves and
    // Charges every enemy in its radius. The stave also unlocks the EX version of this key, Mind Leech (Shift+RMB).
    public class ChainLightning : Ability
    {
        [SerializeField] float damage = 16f;
        [SerializeField] float range = 12f;
        [SerializeField] float aimRadius = 0.6f;
        [SerializeField] int maxJumps = 2;
        [SerializeField] float jumpRange = 5f;
        [SerializeField] float falloff = 0.7f;
        [SerializeField] float energyPerHit = 6f;
        [SerializeField] float originHeight = 1.25f;
        [SerializeField] float chargedDuration = Charged.DefaultDuration;
        [SerializeField] float chargedBonus = 0.4f;   // extra damage against enemies that are already Charged

        [Header("Kine Slam (Force Stave)")]
        [SerializeField] float slamDamage = 18f;
        [SerializeField] float slamRadius = 4f;
        [SerializeField] float slamWindup = 0.5f;
        [SerializeField] float slamCooldown = 6f;
        [SerializeField] float slamKnockback = 2.5f;
        [SerializeField] float slamKnockbackDuration = 0.25f;
        [SerializeField] float slamEnergyPerHit = 6f;

        [Header("EX: Mind Leech (Force Stave only)")]
        [SerializeField] float exEnergyCost = 40f;
        [SerializeField] float leechRange = 11f;
        [SerializeField] float leechDuration = 2f;
        [SerializeField] float silenceDuration = 2f;
        [SerializeField] float leechDamagePerSecond = 16f;
        [SerializeField] float leechHealPerSecond = 16f;
        [SerializeField] float leechEnergyPerSecond = 12f;

        Energy energy;
        TelegraphBeam telegraph;

        bool Stave => PsykerUpgrade.HoldsStave(gameObject);

        protected override float WindupDuration => Stave ? slamWindup : 0.8f;
        protected override Color WindupColor => new Color(0.7f, 0.85f, 1f, 1f);

        public override string DisplayName => Stave ? "Kine Slam" : "Chain Lightning";
        public override string KeyLabel => "RMB";

        // Mind Leech exists only for a Psyker holding the Force Stave; without it this key has no EX version.
        public override float ExCost => Stave ? exEnergyCost : 0f;
        public override string ExDescription => "Mind Leech";
        public override string ExName => "Mind Leech";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 5f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        // The stave's slam has its own recharge; `cooldown` itself stays Chain Lightning's.
        protected override void Update()
        {
            CooldownScale = Stave && cooldown > 0f ? slamCooldown / cooldown : 1f;
            base.Update();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1);
        }

        // Chain Lightning draws a beam through the wind-up, swelling and brightening until the bolt fires along it.
        // With the stave there is no beam; the EX energy for Mind Leech is spent here and given back if cancelled.
        protected override void OnWindupStart()
        {
            if (Stave)
            {
                if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
                return;
            }

            ClearTelegraph();
            telegraph = new GameObject("Chain Lightning Telegraph").AddComponent<TelegraphBeam>();
            telegraph.Init(this, WindupDuration);
        }

        protected override void OnWindupCancel()
        {
            ClearTelegraph();
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        void ClearTelegraph()
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
            telegraph = null;
        }

        void AimLine(out Vector3 start, out Vector3 dir)
        {
            dir = Aim.AimDirection;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            start = transform.position + Vector3.up * originHeight + dir * 0.6f;
        }

        protected override void Activate()
        {
            ClearTelegraph();

            if (Stave)
            {
                if (ExActivation)
                {
                    // EX: Mind Leech on the enemy nearest the cursor. If there is nobody in range the energy is given back.
                    bool started = MindLeech.Begin(gameObject, Health, energy, Aim.AimPoint, !IsReplica, leechRange, leechDuration,
                        silenceDuration, leechDamagePerSecond, leechHealPerSecond, leechEnergyPerSecond);
                    if (!started && !IsReplica && energy != null) energy.Gain(exEnergyCost);
                }
                else
                {
                    Slam();
                }
                return;
            }

            AimLine(out Vector3 start, out Vector3 dir);

            // First strike: nearest enemy along the line, unless a wall is nearer.
            Health first = null;
            float firstDistance = range;
            float wallDistance = range;
            foreach (RaycastHit hit in Physics.SphereCastAll(start, aimRadius, dir, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h == null)
                {
                    if (hit.distance > 0f) wallDistance = Mathf.Min(wallDistance, hit.distance);
                    continue;
                }
                if (h.Team == Health.Team || h.IsDead || hit.distance >= firstDistance) continue;
                first = h;
                firstDistance = hit.distance;
            }

            if (first == null || firstDistance > wallDistance)
            {
                // Nothing struck: the bolt fizzles out at the end of its range or at the wall.
                PsykerFx.Bolt(new List<Vector3> { start, start + dir * Mathf.Min(wallDistance, range * 0.75f) }, 0.2f);
                return;
            }

            Strike(start, first);
        }

        // The bolt struck `first` at `from`: the lightning leaps through up to maxJumps more enemies.
        void Strike(Vector3 from, Health first)
        {
            var points = new List<Vector3> { from };
            var struck = new List<Health>();
            Health current = first;
            for (int jump = 0; jump <= maxJumps && current != null; jump++)
            {
                struck.Add(current);
                points.Add(ChestOf(current));

                // Passive: every victim is left charged. Applied on replicas too, so everyone sees the rune.
                // The bonus is read first, so only a victim that was already Charged takes it (a follow-up hit).
                float bonus = ChargedScale(current);
                Charged.Apply(current.gameObject, chargedDuration);

                if (!IsReplica)
                {
                    float dealt = damage * Mathf.Pow(falloff, jump) * bonus;
                    current.Strike(Hit.Direct(dealt).AsAoe(), gameObject, OnLanded(energyPerHit));
                }

                current = NextTarget(current, struck);
            }

            // Arc Conduit upgrade: the bolt then runs on to every other Charged enemy on the map (anyone already struck
            // by the bolt is skipped, so nobody is hit twice) and finally returns to the original target.
            if (Anathema.Upgrades.UpgradeState.Has(gameObject, PsykerUpgrade.ArcConduit))
            {
                var remaining = new List<Health>();
                foreach (Health h in FindObjectsByType<Health>())
                    if (!struck.Contains(h) && !h.IsDead && h.Team != Health.Team && Charged.IsCharged(h.gameObject)) remaining.Add(h);

                Vector3 at = points[points.Count - 1];
                while (remaining.Count > 0)
                {
                    // Nearest charged enemy to where the bolt has got to.
                    Health next = remaining[0];
                    float best = float.MaxValue;
                    foreach (Health h in remaining)
                    {
                        float d = Vector3.SqrMagnitude(ChestOf(h) - at);
                        if (d < best) { best = d; next = h; }
                    }
                    remaining.Remove(next);

                    at = ChestOf(next);
                    points.Add(at);
                    ConduitHit(next);
                }

                // The bolt returns to where it started (visual only: the first target was already struck).
                if (first != null && !first.IsDead && points.Count > struck.Count + 1) points.Add(ChestOf(first));
            }

            PsykerFx.Bolt(points, 0.3f);
        }

        void ConduitHit(Health target)
        {
            float bonus = ChargedScale(target);
            Charged.Apply(target.gameObject, chargedDuration); // the bolt keeps them charged
            if (IsReplica) return;
            target.Strike(Hit.Direct(damage * PsykerUpgrade.ConduitDamageScale * bonus).AsAoe(), gameObject, OnLanded(energyPerHit));
        }

        // Hit marker and energy for a hit that landed.
        System.Action<bool> OnLanded(float energyGain)
        {
            return landed =>
            {
                if (!landed) return;
                HitMarkerSound.Play();
                if (energy != null) energy.Gain(energyGain);
            };
        }

        Health NextTarget(Health from, List<Health> struck)
        {
            Health best = null;
            float bestDistance = jumpRange;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team || struck.Contains(h)) continue;
                float d = Vector3.Distance(h.transform.position, from.transform.position);
                if (d >= bestDistance) continue;
                best = h;
                bestDistance = d;
            }
            return best;
        }

        // Passive: Chain Lightning (and Kine Slam) hit already-Charged enemies harder, like Warp Bolt.
        float ChargedScale(Health target)
        {
            return Charged.IsCharged(target.gameObject) ? 1f + chargedBonus : 1f;
        }

        static Vector3 ChestOf(Health h)
        {
            return h.transform.position + Vector3.up * 1.1f;
        }

        // Kine Slam: a telekinetic shockwave rolls out from the Psyker. Every machine shows it and the Charged rune;
        // only the owner's copy deals the damage and the shove.
        void Slam()
        {
            Vector3 centre = transform.position;
            PsykerFx.Ring(centre, 0.4f, slamRadius, 0.35f, PsykerFx.Cyan, 0.3f);
            PsykerFx.Ring(centre, 0.2f, slamRadius * 0.7f, 0.3f, new Color(1f, 1f, 1f, 0.8f), 0.16f);

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team) continue;

                Vector3 offset = h.transform.position - centre;
                offset.y = 0f;
                if (offset.magnitude > slamRadius) continue;

                float bonus = ChargedScale(h);
                Charged.Apply(h.gameObject, chargedDuration);
                if (IsReplica) continue;

                h.Strike(Hit.Direct(slamDamage * bonus).AsAoe().WithKnockback(offset, slamKnockback, slamKnockbackDuration),
                    gameObject, OnLanded(slamEnergyPerHit));
            }
        }

        protected override void Cancel()
        {
            ClearTelegraph();
            var leech = GetComponent<MindLeech>();
            if (leech != null) leech.Stop();
        }

        // A thin beam that tracks the aim line during the wind-up and builds in width and brightness.
        sealed class TelegraphBeam : MonoBehaviour
        {
            ChainLightning owner;
            float duration, begin;
            LineRenderer glow, core;
            Material glowMaterial, coreMaterial;

            public void Init(ChainLightning o, float d)
            {
                owner = o;
                duration = Mathf.Max(0.05f, d);
                begin = Time.time;
                glowMaterial = VisualUtil.UnlitMaterial(new Color(0.6f, 0.8f, 1f, 0.1f), 3000);
                coreMaterial = VisualUtil.UnlitMaterial(new Color(1f, 1f, 1f, 0.1f), 3001);
                glow = Layer("Glow", glowMaterial);
                core = Layer("Core", coreMaterial);
                Update();
            }

            LineRenderer Layer(string name, Material m)
            {
                var child = new GameObject(name);
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.numCapVertices = 3;
                line.sharedMaterial = m;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return line;
            }

            void Update()
            {
                if (owner == null) { Destroy(gameObject); return; }

                owner.AimLine(out Vector3 start, out Vector3 dir);
                Vector3 end = RangerFx.Trace(owner.transform, owner.Health.Team, start, dir, owner.range, owner.aimRadius, null);
                float t = Mathf.Clamp01((Time.time - begin) / duration);
                float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 60f);

                foreach (LineRenderer l in new[] { glow, core })
                {
                    l.SetPosition(0, start);
                    l.SetPosition(1, end);
                }
                glow.startWidth = glow.endWidth = Mathf.Lerp(0.05f, 0.5f, t * t);
                core.startWidth = core.endWidth = Mathf.Lerp(0.02f, 0.15f, t * t);
                glowMaterial.color = new Color(0.6f, 0.8f, 1f, Mathf.Lerp(0.1f, 0.55f, t) * flicker);
                coreMaterial.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, t) * flicker);
            }

            void OnDestroy()
            {
                if (glowMaterial != null) Destroy(glowMaterial);
                if (coreMaterial != null) Destroy(coreMaterial);
            }
        }
    }
}
