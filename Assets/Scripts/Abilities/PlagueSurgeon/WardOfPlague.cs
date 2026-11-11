using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Ultimate (R): Ward of Plague. Costs a full energy bar. Wraps an ally in a shield of plague-light: aim at the ally
    // nearest the cursor (within range) and press R. With no ally near the cursor, the surgeon wards himself.
    // The ward absorbs damage until its strength is spent or its time runs out; either way it ruptures and infects every
    // enemy near the holder with a heavy Festering (see PlagueWard).
    public class WardOfPlague : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float range = 14f;
        [SerializeField] float pickRadius = 3f;   // how close to the cursor an ally must be to be chosen
        [SerializeField] float wardStrength = 60f;
        [SerializeField] float wardDuration = 8f;

        Energy energy;

        public override string DisplayName => "Ward of Plague";
        public override string KeyLabel => "R";

        protected override float WindupDuration => 0.6f;
        protected override Color WindupColor => new Color(0.6f, 0.9f, 0.3f, 1f);

        // Energy is only spent when the ward lands, so cancelling the wind-up costs nothing.
        protected override void OnWindupStart() => PlagueSurgeonPose.For(gameObject).Conjure(WindupDuration + 0.1f);

        protected override void OnWindupCancel() => PlagueSurgeonPose.For(gameObject).Release();

        // The HUD greys the slot out until the bar is full.
        public override bool Available => energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject);

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;
        }

        protected override void Activate()
        {
            PlagueSurgeonPose.For(gameObject).Conjure(0.6f);
            // The owner's machine does the casting; a replica has nothing to show (the ward appears on its holder).
            if (IsReplica) return;

            if (energy == null || !energy.TrySpend(energyCost))
            {
                RefundCooldown();
                return;
            }

            Health target = PickAlly();
            float strength = wardStrength
                             * (PlagueSurgeonUpgradeEffects.Has(gameObject, PlagueSurgeonUpgrade.FecundWard) ? PlagueSurgeonUpgrade.FecundStrengthScale : 1f);
            PlagueWard.Apply(target.gameObject, gameObject, strength, wardDuration);
        }

        // The ally closest to the cursor, within range of the surgeon; the surgeon himself if there is no other.
        Health PickAlly()
        {
            Health best = null;
            float bestDistance = pickRadius;

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team != Health.Team || h.gameObject == gameObject) continue;

                Vector3 fromSurgeon = h.transform.position - transform.position;
                fromSurgeon.y = 0f;
                if (fromSurgeon.magnitude > range) continue;

                Vector3 fromCursor = h.transform.position - Aim.AimPoint;
                fromCursor.y = 0f;
                if (fromCursor.magnitude < bestDistance)
                {
                    bestDistance = fromCursor.magnitude;
                    best = h;
                }
            }

            return best != null ? best : Health;
        }

        protected override void Cancel()
        {
            // Instant cast: nothing to cancel.
        }
    }
}
