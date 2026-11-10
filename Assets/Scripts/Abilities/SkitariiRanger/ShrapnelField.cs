using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // Q: lobs a shrapnel canister to the cursor (pulled in to max range). It bursts into a field of whirling metal that
    // damages enemies over time and slows anyone standing in it.
    // EX (Shift, costs energy): Scattershot. Instead of the canister the Ranger fires a short-range shotgun blast in a
    // cone that hurts, knocks back and slows everything in it.
    public class ShrapnelField : Ability
    {
        [SerializeField] float fieldRange = 12f;
        [SerializeField] float fieldRadius = 3.5f;
        [SerializeField] float fieldDuration = 4.5f;
        [SerializeField] float fieldDamagePerSecond = 11f;
        [SerializeField] float fieldSlow = 0.35f;
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float blastRange = 7f;
        [SerializeField] float blastHalfAngle = 38f;
        [SerializeField] float blastDamage = 26f;
        [SerializeField] float blastKnockback = 5f;
        [SerializeField] float blastKnockbackTime = 0.25f;
        [SerializeField] float blastSlow = 0.4f;
        [SerializeField] float blastSlowDuration = 2.5f;
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] float muzzleHeight = 1.3f;

        Energy energy;

        protected override float WindupDuration => 0.25f;
        protected override Color WindupColor => RangerFx.Ember;

        // The EX energy is spent when the wind-up starts and given back if it is cancelled.
        protected override void OnWindupStart()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
        }

        protected override void OnWindupCancel()
        {
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        public override string DisplayName => "Shrapnel Field";
        public override string KeyLabel => "Q";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Scattershot";
        public override string ExDescription => "Scattershot";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 8f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.Q);
        }

        protected override void Activate()
        {
            if (ExActivation)
            {
                Scattershot();
                return;
            }

            Vector3 here = transform.position;
            Vector3 to = Aim.AimPoint - here;
            to.y = 0f;
            if (to.magnitude > fieldRange) to = to.normalized * fieldRange;
            Vector3 centre = new Vector3(here.x + to.x, here.y, here.z + to.z);

            // A short streak for the canister's flight, then the burst.
            Vector3 dir = to.sqrMagnitude < 0.0001f ? transform.forward : to.normalized;
            RangerFx.Tracer(here + Vector3.up * muzzleHeight + dir * 1.2f, centre + Vector3.up * 0.3f, 0.12f, RangerFx.Ember, 0.18f);
            PsykerFx.Ring(centre, 0.3f, fieldRadius, 0.4f, RangerFx.Ember, 0.25f);
            ShrapnelPool.Spawn(gameObject, Health.Team, centre, fieldRadius, fieldDuration, fieldDamagePerSecond, fieldSlow, !IsReplica);
        }

        void Scattershot()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * 1.5f;

            // A fan of short pellet streaks, plus a flash at the muzzle.
            const int pellets = 11;
            for (int i = 0; i < pellets; i++)
            {
                float a = Mathf.Lerp(-blastHalfAngle, blastHalfAngle, i / (pellets - 1f)) + Random.Range(-3f, 3f);
                Vector3 d = Quaternion.Euler(0f, a, 0f) * dir;
                RangerFx.Tracer(origin, origin + d * blastRange * Random.Range(0.7f, 1f), 0.09f, RangerFx.Ember, 0.16f);
            }
            PsykerFx.Ring(transform.position + dir * 1.5f, 0.2f, 2f, 0.22f, RangerFx.Ember, 0.3f);

            if (IsReplica) return;

            bool marked = false;
            Vector3 centre = transform.position;
            foreach (Collider col in Physics.OverlapSphere(centre + Vector3.up, blastRange + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead) continue;

                Vector3 to = col.ClosestPoint(centre) - centre;
                to.y = 0f;
                if (to.magnitude > blastRange || to.sqrMagnitude < 0.0001f) continue;
                if (Vector3.Angle(dir, to) > blastHalfAngle) continue;

                Hit blast = Hit.Direct(blastDamage)
                    .WithKnockback(to.normalized, blastKnockback, blastKnockbackTime)
                    .WithSlow(blastSlowDuration, blastSlow);
                target.Strike(blast, gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(energyPerHit);
                    if (marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
        }

        protected override void Cancel()
        {
        }
    }
}
