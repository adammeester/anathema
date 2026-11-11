using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // The Hospitaller's passive, Blessed Suffering. Pain borne for others becomes faith:
    //   - damage she takes for an ally (Sanctified Tether, Martyrdom) gives her energy;
    //   - it also builds Faith stacks (see Faith), each making everything she does to enemies hit harder.
    public static class BlessedSuffering
    {
        // Energy per point of damage she takes for an ally.
        public const float EnergyPerSharedDamage = 0.5f;

        // Multiplier for the damage of her abilities: +6% per Faith stack.
        public static float DamageScale(Health hospitaller)
        {
            if (hospitaller == null || hospitaller.IsDead) return 1f;
            var faith = hospitaller.GetComponent<Faith>();
            return faith != null ? 1f + Faith.BonusPerStack * faith.Stacks : 1f;
        }

        public static float Scale(Health hospitaller, float damage) => damage * DamageScale(hospitaller);

        // Her machine: she just carried `damage` for an ally. Energy and Faith.
        public static void OnShared(GameObject hospitaller, Energy energy, float damage)
        {
            if (damage <= 0f) return;
            if (energy != null) energy.Gain(damage * EnergyPerSharedDamage);
            Faith.For(hospitaller).AddShared(damage);
        }
    }
}
