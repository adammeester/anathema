using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Khorne Berserker's counter payoff: blood rage. The negated hit is answered with a savage axe burst that
    // damages the attacker, and the Berserker drinks in some of the violence as health. It also resets the axe rush
    // (Q) and empowers the next axe chop.
    [DefaultExecutionOrder(100)] // keep in step with Counter; not every Unity version inherits the attribute
    public class BerserkerCounter : Counter
    {
        [SerializeField] float retaliationDamage = 18f;
        [SerializeField] float healFraction = 0.1f; // of max health
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] float burstRadius = 2.2f;
        [SerializeField] float enrageDuration = 4f;
        [SerializeField] Color burstColor = new Color(1f, 0.15f, 0.1f, 0.9f);

        protected override void OnCounterSuccess(GameObject attacker)
        {
            SlashEffect.Spawn(transform.position, Aim.AimDirection, burstRadius, 360f, 0.4f, burstColor);
            Health.Heal(Health.Max * healFraction);
            Enraged.Apply(gameObject, enrageDuration);

            // Blood rage: the rush is ready again and the next axe chop hits harder.
            var rush = GetComponent<AxeRush>();
            if (rush != null) rush.ResetCooldown();
            var chop = GetComponent<AxeChop>();
            if (chop != null) chop.Empower();

            if (attacker == null) return;
            Health target = attacker.GetComponentInParent<Health>();
            if (target != null && target.Team != Health.Team && target.TakeDamage(retaliationDamage, gameObject))
            {
                Energy energy = GetComponent<Energy>();
                if (energy != null) energy.Gain(energyPerHit);
            }
        }
    }
}
