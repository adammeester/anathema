using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // The Bullgryn's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever append
    // new ones; never renumber.
    //
    // Only the first pick is designed so far: the Grenadier Gauntlet (turns him into a ranged character) is offered
    // after round 1. The other cards are first-pass placeholders to fill the sets; later picks draw from them at random.
    public static class BullgrynUpgrade
    {
        public const string WeaponGroup = "BullgrynWeapon";

        // Armament
        public const int GrenadierGauntlet = 0;

        // Endurance
        public const int ThickSkull = 1;
        public const int ReinforcedSlab = 2;

        // Ferocity
        public const int Bonebreaker = 3;
        public const int Headlong = 4;

        // Tunables shared with the code that applies them.
        public const float ThickSkullHealth = 25f;
        public const float ReinforcedSlabStaminaScale = 1.4f;   // Brace stamina
        public const float BonebreakerSlow = 0.45f;             // Shield Bash slow, instead of 30%...
        public const float BonebreakerSlowSeconds = 2.5f;       // ...for 2.5s instead of 2s
        public const float HeadlongCooldownScale = 0.75f;       // Bull Charge

        public static bool HasGauntlet(GameObject bullgryn) => UpgradeState.Has(bullgryn, GrenadierGauntlet);

        //   after round 1   Grenadier Gauntlet   Thick Skull        Bonebreaker
        //   later           random draws from what is left
        public static readonly int[][][] Sets =
        {
            new[]
            {
                new[] { GrenadierGauntlet },
                new[] { ThickSkull },
                new[] { Bonebreaker },
            },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(GrenadierGauntlet, UpgradeCategory.Armament, "Grenadier Gauntlet",
                "Trade the maul for a grenade launcher. Your basic attack lobs a grenade to the cursor (4-11m) that bursts for " +
                "area damage. A full Grit bar fires a wider Krak Shell that slows.").InGroup(WeaponGroup),

            new UpgradeDef(ThickSkull, UpgradeCategory.Endurance, "Thick Skull",
                "+25 maximum health.").Health(ThickSkullHealth),
            new UpgradeDef(ReinforcedSlab, UpgradeCategory.Endurance, "Reinforced Slab",
                "Brace's stamina is 40% larger, so you can hold the shield longer and soak more before your guard breaks."),

            new UpgradeDef(Bonebreaker, UpgradeCategory.Ferocity, "Bonebreaker",
                "Shield Bash slows by 45% for 2.5s instead of 30% for 2s."),
            new UpgradeDef(Headlong, UpgradeCategory.Ferocity, "Headlong",
                "Bull Charge recharges 25% faster."),
        };
    }
}
