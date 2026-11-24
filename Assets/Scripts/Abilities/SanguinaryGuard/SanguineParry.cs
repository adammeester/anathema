using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Space: Sanguine Parry, the guard's counter. The hit is negated and there is no automatic riposte: instead the
    // chalice fills to the brim, so his very next swing is the Exsanguinating Arc and he can retaliate himself (the Arc's
    // lifesteal and ally share are where the healing comes from; the parry itself heals no one). His next Chalice Offering
    // charge also comes back `chaliceRefund` of its recharge sooner.
    [DefaultExecutionOrder(100)] // keep in step with Counter
    public class SanguineParry : Counter
    {
        [SerializeField] float energyGain = 10f;
        [SerializeField] float chaliceRefund = 0.5f;   // a successful parry brings the next Chalice charge forward by this much of its recharge

        protected override Color GuardColor => new Color(0.85f, 0.1f, 0.1f, 0.45f);
        protected override Color GlowColor => new Color(1f, 0.2f, 0.15f, 0.4f);

        public override string DisplayName => "Sanguine Parry";

        protected override void OnCounterSuccess(GameObject attacker)
        {
            Energy energy = GetComponent<Energy>();
            if (energy != null) energy.Gain(energyGain);
            if (TryGetComponent(out ChaliceOffering chalice)) chalice.RefundCharge(chaliceRefund);
            // The blow turned aside fills the chalice to the brim: the next swing is the Arc (owner only: Vitae lives on
            // his machine; other players see the blade blaze through the published "empowered" flag).
            if (!Health.IsReplica && TryGetComponent(out Vitae vitae)) vitae.Add(vitae.Max);
            SanguinaryKit.Mist(transform.position, 1.4f, 0.4f);
            Hospitaller.HospitallerFx.Burst(transform.position, 1.6f, SanguinaryKit.BrightBlood, 0.4f, 0.8f);
        }
    }
}
