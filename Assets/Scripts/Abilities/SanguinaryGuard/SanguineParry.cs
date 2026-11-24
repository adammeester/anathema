using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Space: Sanguine Parry, the guard's counter. The hit is negated; he cuts the attacker if they are close, and the
    // blood he draws heals the most injured teammate near him (himself included), which fills Vitae like any heal, and
    // his next Chalice Offering charge comes back `chaliceRefund` of its recharge sooner, and the chalice fills by `vitaeGain`.
    [DefaultExecutionOrder(100)] // keep in step with Counter
    public class SanguineParry : Counter
    {
        [SerializeField] float riposteDamage = 8f;
        [SerializeField] float riposteRange = 3.5f;
        [SerializeField] float heal = 15f;
        [SerializeField] float healRange = 8f;
        [SerializeField] float energyGain = 10f;
        [SerializeField] float chaliceRefund = 0.5f;   // a successful parry brings the next Chalice charge forward by this much of its recharge
        [SerializeField] float vitaeGain = 20f;        // a successful parry also fills the chalice by this much

        protected override Color GuardColor => new Color(0.85f, 0.1f, 0.1f, 0.45f);
        protected override Color GlowColor => new Color(1f, 0.2f, 0.15f, 0.4f);

        public override string DisplayName => "Sanguine Parry";

        protected override void OnCounterSuccess(GameObject attacker)
        {
            Energy energy = GetComponent<Energy>();
            if (energy != null) energy.Gain(energyGain);
            if (TryGetComponent(out ChaliceOffering chalice)) chalice.RefundCharge(chaliceRefund);
            // The blow turned aside feeds the chalice directly (owner only: Vitae lives on his machine).
            if (!Health.IsReplica && TryGetComponent(out Vitae vitae)) vitae.Add(vitaeGain);
            SanguinaryKit.Mist(transform.position, 1.4f, 0.4f);

            // The riposte is a sword cut: none while Blade Hurl has the sword out of his hand.
            bool armed = !(TryGetComponent(out CrimsonRush rush) && rush.Disarmed);
            if (armed && attacker != null && attacker.TryGetComponent(out Health enemy) && !enemy.IsDead && enemy.Team != Health.Team
                && SanguinaryKit.Flat(attacker.transform.position - transform.position).magnitude <= riposteRange)
            {
                enemy.Strike(Hit.Direct(riposteDamage), gameObject);
                BloodImpact.Play(enemy.transform.position + Vector3.up, SanguinaryKit.Flat(enemy.transform.position - transform.position), 1f);
            }

            Health ally = SanguinaryKit.MostInjuredAlly(Health, transform.position, healRange);
            if (ally == null) return;
            SanguinaryKit.Heal(gameObject, ally, heal);
            SanguinaryKit.BloodStream(transform.position, ally.transform);
        }
    }
}
