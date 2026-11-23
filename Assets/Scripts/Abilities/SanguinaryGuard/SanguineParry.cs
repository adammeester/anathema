using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Space: Sanguine Parry, the guard's counter. The hit is negated; he cuts the attacker if they are close, and the
    // blood he draws heals the most injured teammate near him (himself included), which fills Vitae like any heal.
    [DefaultExecutionOrder(100)] // keep in step with Counter
    public class SanguineParry : Counter
    {
        [SerializeField] float riposteDamage = 8f;
        [SerializeField] float riposteRange = 3.5f;
        [SerializeField] float heal = 15f;
        [SerializeField] float healRange = 8f;
        [SerializeField] float energyGain = 10f;

        protected override Color GuardColor => new Color(0.85f, 0.1f, 0.1f, 0.45f);
        protected override Color GlowColor => new Color(1f, 0.2f, 0.15f, 0.4f);

        public override string DisplayName => "Sanguine Parry";

        protected override void OnCounterSuccess(GameObject attacker)
        {
            Energy energy = GetComponent<Energy>();
            if (energy != null) energy.Gain(energyGain);
            SanguinaryKit.Mist(transform.position, 1.4f, 0.4f);

            if (attacker != null && attacker.TryGetComponent(out Health enemy) && !enemy.IsDead && enemy.Team != Health.Team
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
