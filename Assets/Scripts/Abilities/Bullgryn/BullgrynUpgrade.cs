using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // The Bullgryn's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever append
    // new ones; never renumber or reuse a retired bit.
    //
    // The round-1 Armament pick is the weapon fork: trade the maul for the Grenadier Gauntlet (a ranged character), or
    // keep it with Haymaker, which also opens the maul-only upgrades. Owning any maul upgrade takes the gauntlet off
    // the table, so those cards are never wasted.
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

        // Maul only (appended). Bits 5 (Bodyguard) and 8 (Bone Rattler) are retired: too strong.
        public const int BigSwings = 6;         // Armament
        public const int GroundPounder = 7;     // Ferocity
        public const int Haymaker = 9;          // Armament: the round-1 keep-the-maul card

        // Tunables shared with the code that applies them.
        public const float ThickSkullHealth = 25f;
        public const float ReinforcedSlabStaminaScale = 1.4f;   // Brace stamina
        public const float BonebreakerSlow = 0.45f;             // Shield Bash slow, instead of 30%...
        public const float BonebreakerSlowSeconds = 2.5f;       // ...for 2.5s instead of 2s
        public const float HeadlongCooldownScale = 0.75f;       // Bull Charge

        public const float BigSwingsArc = 150f;                 // instead of 110
        public const float BigSwingsRangeBonus = 0.5f;          // 2.6m -> 3.1m

        public const float GroundPounderRadius = 3.6f;          // Slab Slam hits all around him
        public const float GroundPounderStun = 1f;              // instead of 0.75s

        public const float HaymakerCooldown = 10f;              // the passive charge; the maul glows while it is ready
        public const float HaymakerDamageScale = 1.5f;
        public const float HaymakerShove = 3f;                  // instead of 1.5m
        public const float HaymakerPinStun = 0.75f;             // knocked into terrain

        const int MaulMask = (1 << BigSwings) | (1 << GroundPounder) | (1 << Haymaker);

        static bool NoGauntlet(int mask) => (mask & (1 << GrenadierGauntlet)) == 0;
        static bool NoMaulUpgrades(int mask) => (mask & MaulMask) == 0;

        public static bool HasGauntlet(GameObject bullgryn) => UpgradeState.Has(bullgryn, GrenadierGauntlet);
        public static bool Has(GameObject bullgryn, int bit) => UpgradeState.Has(bullgryn, bit);

        // Sets[pick][slot] lists alternatives for one card; the first still available is shown, so a gauntlet
        // Bullgryn falls through the maul cards to a general one.
        //
        //   after round 1   Grenadier Gauntlet | Haymaker (keep the maul) | Thick Skull | Bonebreaker
        //   after round 2   Big Swings / Haymaker / Reinforced Slab / Headlong   Reinforced Slab / Thick Skull / Bonebreaker   Ground Pounder / Headlong / Bonebreaker
        //   after round 3   the same alternatives, whatever is still left
        //   later           random draws from what is left
        public static readonly int[][][] Sets =
        {
            new[]
            {
                new[] { GrenadierGauntlet },
                new[] { Haymaker },
                new[] { ThickSkull },
                new[] { Bonebreaker },
            },
            new[]
            {
                new[] { BigSwings, Haymaker, ReinforcedSlab, Headlong },
                new[] { ReinforcedSlab, ThickSkull, Bonebreaker },
                new[] { GroundPounder, Headlong, Bonebreaker },
            },
            new[]
            {
                new[] { BigSwings, Haymaker, Headlong, Bonebreaker },
                new[] { ThickSkull, ReinforcedSlab, Bonebreaker },
                new[] { GroundPounder, Bonebreaker, Headlong },
            },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(GrenadierGauntlet, UpgradeCategory.Armament, "Grenadier Gauntlet",
                "Trade the maul for a grenade launcher. Your basic attack lobs a grenade to the cursor (4-11m) that bursts for " +
                "area damage. A full Grit bar fires a wider Krak Shell that slows.").InGroup(WeaponGroup).Requires(NoMaulUpgrades),

            new UpgradeDef(ThickSkull, UpgradeCategory.Endurance, "Thick Skull",
                "+25 maximum health.").Health(ThickSkullHealth),
            new UpgradeDef(ReinforcedSlab, UpgradeCategory.Endurance, "Reinforced Slab",
                "Brace's stamina is 40% larger, so you can hold the shield longer and soak more before your guard breaks."),

            new UpgradeDef(Bonebreaker, UpgradeCategory.Ferocity, "Bonebreaker",
                "Shield Bash slows by 45% for 2.5s instead of 30% for 2s."),
            new UpgradeDef(Headlong, UpgradeCategory.Ferocity, "Headlong",
                "Bull Charge recharges 25% faster."),

            new UpgradeDef(BigSwings, UpgradeCategory.Armament, "Big Swings",
                "Slab Maul only. The maul sweeps 150 degrees out to 3.1m, and every second connecting swing in a row shoves " +
                "instead of every third.").Requires(NoGauntlet),
            new UpgradeDef(GroundPounder, UpgradeCategory.Ferocity, "Ground Pounder",
                "Slab Maul only. Slab Slam pounds the ground all around you: every enemy within 3.6m is hit, shoved and " +
                "stunned for 1s.").Requires(NoGauntlet),
            new UpgradeDef(Haymaker, UpgradeCategory.Armament, "Haymaker",
                "Keep the Slab Maul and unlock its upgrades. Every 10s your maul charges up and glows: your next maul hit is a " +
                "Haymaker that deals 50% more damage and knocks enemies 3m. Anyone knocked into a wall is stunned for 0.75s.")
                .Requires(NoGauntlet),
        };
    }
}
