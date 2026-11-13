using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // LMB: a three-round burst of galvanic fire from the arquebus. Each round deals modest damage over a long reach;
    // every hit feeds the energy bar that powers the EX shotgun blast and the ultimate.
    public class GalvanicShot : Ability
    {
        [SerializeField] float damage = 3.67f;        // per round
        [SerializeField] int burstCount = 3;
        [SerializeField] float burstInterval = 0.08f; // seconds between rounds in a burst
        [SerializeField] float range = 22f;
        [SerializeField] float projectileSpeed = 42f;
        [SerializeField] float projectileRadius = 0.2f;
        [SerializeField] float muzzleHeight = 1.3f;
        [SerializeField] float muzzleForward = 1.5f;
        [SerializeField] float energyPerHit = 3f;     // per round
        [SerializeField, Range(0f, 1f)] float aimLift = 0.65f; // 1 = full sniper shoulder aim
        [SerializeField] Color boltColor = new Color(0.5f, 0.95f, 1f);
        [Header("EX: Arc Round (tick to enable; off = no EX on this ability)")]
        [SerializeField] bool arcRoundEnabled = false;
        [SerializeField] float exEnergyCost = 20f;
        [SerializeField] float arcRange = 6f;          // how far from the struck enemy the arc can jump
        [SerializeField, Range(0f, 1f)] float arcDamageFraction = 0.75f;

        Energy energy;
        Material boltMaterial;
        Material haloMaterial;

        // Rounds of the current burst still to fire, and when the next one goes.
        int roundsLeft;
        float nextRoundTime;
        bool burstArc;

        public override string DisplayName => "Galvanic Shot";
        public override string KeyLabel => "LMB";
        public override float ExCost => ArcOn ? exEnergyCost : 0f;

        // The EX is on when ticked in the editor or unlocked by the Arc Round upgrade.
        bool ArcOn => arcRoundEnabled || RangerUpgrade.Has(gameObject, RangerUpgrade.ArcRound);

        // Galvanic Overload: how many rounds of one burst have struck. Each burst gets its own tally, since a burst's
        // rounds can still be in flight when the next one starts.
        sealed class BurstTally { public int Hits; }
        BurstTally burstTally;
        public override string ExName => "Arc Round";
        public override string ExDescription => "Arc Round";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 0.75f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        void OnDestroy()
        {
            if (boltMaterial != null) Destroy(boltMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButton(0) && !PrimaryFireBlocked;
        }

        protected override void Activate()
        {
            // The EX energy is spent here (owner only); replicas already have ExActivation from the owner's cue.
            if (!IsReplica) ExActivation = ArcOn && TrySpendEx(energy, exEnergyCost);
            bool arc = ExActivation && ArcOn;
            burstTally = new BurstTally();

            // Each round of the burst follows the cursor as it fires; the remaining rounds follow from Update().
            burstArc = arc;
            roundsLeft = Mathf.Max(1, burstCount);
            FireRound();
        }

        protected override void Update()
        {
            base.Update();
            if (roundsLeft <= 0) return;
            if (Health.IsDead) { roundsLeft = 0; return; }
            if (!IsReplica && IsDisabled)
            {
                // Stunned / knocked back mid-burst: the rest of the burst is lost, and replicas are told to stop too.
                roundsLeft = 0;
                InterruptSelf();
                return;
            }
            if (Time.time >= nextRoundTime) FireRound();
        }

        void FireRound()
        {
            roundsLeft--;
            nextRoundTime = Time.time + burstInterval;

            // Read the aim fresh for every round, so sweeping the mouse mid-burst steers the later rounds.
            // (On replicas, MouseAim tracks the owner's synced aim point.)
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();
            bool arc = burstArc;
            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;

            // Raise the rifle part-way toward the aim line (no sniper stance) so firing reads clearly while still moving.
            RangerPose.For(gameObject).Aim(0.4f, false, aimLift, false);

            if (boltMaterial == null) boltMaterial = VisualUtil.UnlitMaterial(boltColor);
            if (haloMaterial == null) haloMaterial = VisualUtil.UnlitMaterial(new Color(boltColor.r, boltColor.g, boltColor.b, 0.3f));

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Galvanic Round";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (projectileRadius * 1.2f);
            go.GetComponent<Renderer>().sharedMaterial = boltMaterial;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Halo";
            Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 2.4f;
            halo.GetComponent<Renderer>().sharedMaterial = haloMaterial;
            halo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = projectileRadius * 1.1f;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Sfx.Play(Sfx.Longrifle, 0.3f, 1.08f, 1.2f, 0.5f);
            if (arc) go.name = "Arc Round";
            BurstTally tally = burstTally;
            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir,
                arc ? (System.Action<Health>)(target => OnArcHit(target, tally)) : target => OnHit(target, tally));
        }

        void OnHit(Health target, BurstTally tally)
        {
            HitMarkerSound.Play();
            if (energy == null) return;
            energy.Gain(energyPerHit);

            // Galvanic Overload: the round that completes a perfect burst pays out the bonus.
            if (tally == null || !RangerUpgrade.Has(gameObject, RangerUpgrade.GalvanicOverload)) return;
            if (++tally.Hits == Mathf.Max(1, burstCount)) energy.Gain(RangerUpgrade.GalvanicOverloadEnergy);
        }

        // EX: the round hits as normal, then arcs to the nearest other enemy near the one it struck.
        void OnArcHit(Health first, BurstTally tally)
        {
            OnHit(first, tally);

            Health best = null;
            float bestDistance = arcRange;
            Vector3 from = first.transform.position;
            foreach (Collider c in Physics.OverlapSphere(from + Vector3.up, arcRange, ~0, QueryTriggerInteraction.Ignore))
            {
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h == first || h.Team == Health.Team || h.IsDead) continue;
                float d = Vector3.Distance(from, h.transform.position);
                if (d < bestDistance) { bestDistance = d; best = h; }
            }
            if (best == null) return;

            RangerFx.Tracer(from + Vector3.up * 1.2f, best.transform.position + Vector3.up * 1.2f, 0.12f, RangerFx.Cyan, 0.2f);
            best.Strike(Hit.Direct(damage * arcDamageFraction), gameObject, landed =>
            {
                if (!landed) return;
                HitMarkerSound.Play();
                if (energy != null) energy.Gain(energyPerHit * arcDamageFraction);
            });
        }

        protected override void Cancel()
        {
            roundsLeft = 0;
        }
    }
}
