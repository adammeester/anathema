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
        [Header("Scattershot wall slam: an enemy blasted into a wall is stunned")]
        [SerializeField] float wallSlamStun = 1.5f;
        [SerializeField] float wallSlamMinHeight = 0.6f; // scenery lower than this (kerbs, rubble) doesn't count as a wall

        Energy energy;
        float spentEx; // what this cast's EX cost, to give back exactly that on a cancel

        protected override float WindupDuration => 0.25f;
        protected override Color WindupColor => RangerFx.Ember;

        // Point-Blank Doctrine makes Scattershot cheaper and shove harder.
        bool PointBlank => RangerUpgrade.Has(gameObject, RangerUpgrade.PointBlankDoctrine);
        float ScatterCost => PointBlank ? Mathf.Min(exEnergyCost, RangerUpgrade.PointBlankExCost) : exEnergyCost;
        float ScatterKnockback => blastKnockback * (PointBlank ? RangerUpgrade.PointBlankKnockbackScale : 1f);

        // The EX energy is spent when the wind-up starts and given back if it is cancelled.
        protected override void OnWindupStart()
        {
            if (IsReplica) return;
            spentEx = ScatterCost;
            ExActivation = TrySpendEx(energy, spentEx);
        }

        protected override void OnWindupCancel()
        {
            if (ExActivation && !IsReplica && energy != null) energy.Gain(spentEx);
        }

        public override string DisplayName => "Shrapnel Field";
        public override string KeyLabel => "Q";
        public override float ExCost => ScatterCost;
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

            // Lingering Shrapnel: a longer-lasting, wider field (the mask is synced, so replicas draw the same field).
            bool lingering = RangerUpgrade.Has(gameObject, RangerUpgrade.LingeringShrapnel);
            float radius = fieldRadius * (lingering ? RangerUpgrade.LingeringRadiusScale : 1f);
            float duration = fieldDuration * (lingering ? RangerUpgrade.LingeringDurationScale : 1f);

            // A short streak for the canister's flight, then the burst.
            Vector3 dir = to.sqrMagnitude < 0.0001f ? transform.forward : to.normalized;
            RangerFx.Tracer(here + Vector3.up * muzzleHeight + dir * 1.2f, centre + Vector3.up * 0.3f, 0.12f, RangerFx.Ember, 0.18f);
            PsykerFx.Ring(centre, 0.3f, radius, 0.4f, RangerFx.Ember, 0.25f);
            Sfx.Play(Sfx.Longrifle, 0.4f, 0.85f, 0.92f, 0.6f);
            Sfx.Play(Sfx.Impact, 0.45f, 0.9f, 1.05f);
            ShrapnelPool.Spawn(gameObject, Health.Team, centre, radius, duration, fieldDamagePerSecond, fieldSlow, !IsReplica);
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
            Sfx.Play(Sfx.HeavyBolterShot, 0.55f, 0.7f, 0.78f); // the shotgun blast

            if (IsReplica) return;

            bool marked = false;
            float knockback = ScatterKnockback;
            float firedAt = Time.time;
            Vector3 centre = transform.position;
            foreach (Collider col in Physics.OverlapSphere(centre + Vector3.up, blastRange + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead) continue;

                Vector3 to = col.ClosestPoint(centre) - centre;
                to.y = 0f;
                if (to.magnitude > blastRange || to.sqrMagnitude < 0.0001f) continue;
                if (Vector3.Angle(dir, to) > blastHalfAngle) continue;

                Vector3 push = to.normalized;
                // Wall slam: find now (before the shove starts) whether a wall lies inside the knockback's path.
                bool slams = FindWall(target, push, knockback, out float wallDistance, out Vector3 impact);

                Hit blast = Hit.Direct(blastDamage)
                    .WithKnockback(push, knockback, blastKnockbackTime)
                    .WithSlow(blastSlowDuration, blastSlow);
                target.Strike(blast, gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(energyPerHit);
                    if (slams)
                    {
                        // Stun on impact: when the shove (at constant speed) reaches the wall, less the time already gone.
                        float reach = blastKnockbackTime * Mathf.Clamp01(wallDistance / knockback);
                        StartCoroutine(WallSlam(target, Mathf.Max(0f, reach - (Time.time - firedAt)), impact));
                    }
                    if (marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
        }

        // True if solid scenery (not a character, and taller than a kerb) stands within `distance` of `target` along
        // `push`. Targets that can't be shoved right now (Augmetic Bracing, Enraged) don't slam.
        bool FindWall(Health target, Vector3 push, float distance, out float wallDistance, out Vector3 impact)
        {
            wallDistance = 0f;
            impact = Vector3.zero;
            if (wallSlamStun <= 0f || target.Structure || Knockback.IsImmune(target.gameObject) || Enraged.IsEnraged(target.gameObject))
                return false;

            Vector3 from = target.transform.position;
            float best = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.SphereCastAll(from + Vector3.up, 0.35f, push, distance + 0.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.transform.IsChildOf(target.transform)) continue;
                if (hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.collider.bounds.max.y - from.y < wallSlamMinHeight) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                impact = hit.point;
            }
            if (float.IsPositiveInfinity(best)) return false;
            wallDistance = best;
            return true;
        }

        System.Collections.IEnumerator WallSlam(Health target, float delay, Vector3 impact)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (target == null || target.IsDead) yield break;

            // Stunned.Apply hands the stun to the victim's machine online and shows the stars here too.
            Stunned.Apply(target.gameObject, wallSlamStun);
            PsykerFx.Ring(new Vector3(impact.x, target.transform.position.y, impact.z), 0.2f, 1.6f, 0.3f, RangerFx.Ember, 0.3f);
            Sfx.Play(Sfx.Impact, 0.6f, 0.7f, 0.8f);
        }

        protected override void Cancel()
        {
        }
    }
}
