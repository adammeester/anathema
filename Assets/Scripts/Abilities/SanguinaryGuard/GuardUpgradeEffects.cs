using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Applies the Sanguinary Guard's upgrades that aren't read straight from the abilities: cooldown scales and Iron
    // Halo. Added by UpgradeState at runtime. Owner machine only (cooldowns and the shield are decided there; the
    // shield is handed to the server by Barrier.Grant).
    public class GuardUpgradeEffects : MonoBehaviour
    {
        Health health;
        UpgradeState upgrades;
        CrimsonRush rush;
        bool haloUsed;

        void Awake()
        {
            health = GetComponent<Health>();
            upgrades = GetComponent<UpgradeState>();
            rush = GetComponent<CrimsonRush>();
        }

        void Update()
        {
            if (health == null || upgrades == null || health.IsReplica) return;

            // Iron Halo is once per round: it rearms in the prep phase before each round.
            if (Anathema.Net.NetGame.InPrep) haloUsed = false;

            if (rush != null) rush.CooldownScale = upgrades.Has(GuardUpgrade.CrimsonMomentum) ? GuardUpgrade.MomentumCooldownScale : 1f;

            if (upgrades.Has(GuardUpgrade.IronHalo) && !haloUsed && !health.IsDead && health.Normalized <= GuardUpgrade.IronHaloThreshold)
            {
                haloUsed = true;
                Barrier.Grant(gameObject, GuardUpgrade.IronHaloShield, GuardUpgrade.IronHaloSeconds, GuardUpgrade.IronHaloShield, gameObject);
                Hospitaller.HospitallerFx.Burst(transform.position, 1.6f, SanguinaryKit.Gold, 0.5f, 1f);
                Sfx.Play(Sfx.MagicImpact, 0.35f, 1.1f, 1.2f, 0.6f);
            }
        }
    }
}
