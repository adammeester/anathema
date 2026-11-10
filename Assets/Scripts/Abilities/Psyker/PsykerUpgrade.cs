using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // The Psyker's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever append
    // new ones; never renumber.
    //
    //   Armament  - Force Stave (Warp Bolt becomes Stave Sweep, Chain Lightning becomes Kine Slam, and Mind Leech unlocks).
    //   Endurance - Warded Mind (more max health), Warp Ward (a shield when she teleports).
    //   Ferocity  - Arc Conduit (Chain Lightning bounces to every Charged enemy).
    public static class PsykerUpgrade
    {
        public const int ArcConduit = 0;
        public const int WardedMind = 1;
        public const int WarpWard = 2;
        public const int ForceStave = 3;

        public const string WeaponGroup = "weapon";

        // True while this Psyker wields the Force Stave. Reads the synced upgrade mask, so replicas agree with the owner.
        public static bool HoldsStave(GameObject player) => UpgradeState.Has(player, ForceStave);

        // Tunables shared with the code that applies them.
        public const float WardedMindHealth = 50f;
        public const float ConduitDamageScale = 0.6f;     // of Chain Lightning's base damage, per bounce
        public const float WarpWardStrength = 35f;
        public const float WarpWardSeconds = 4f;

        // Armament / Endurance / Ferocity, like the Berserker. The Stave and Arc Conduit exclude each other (the Stave
        // replaces Chain Lightning, which is what Arc Conduit improves), so a pick that was passed over is offered again
        // until one of the two has been taken.
        //
        //   after round 1   Armament: Force Stave   Endurance: Warded Mind   Ferocity: Arc Conduit
        //   after round 2   Armament: Force Stave   Endurance: Warp Ward     Ferocity: Arc Conduit
        public static readonly int[][][] Sets =
        {
            new[] { new[] { ForceStave }, new[] { WardedMind, WarpWard }, new[] { ArcConduit } },
            new[] { new[] { ForceStave }, new[] { WarpWard, WardedMind }, new[] { ArcConduit } },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(ForceStave, UpgradeCategory.Armament, "Force Stave - The Sentinel",
                "Close-range control. Warp Bolt becomes Stave Sweep, a wide melee arc whose third swing hits harder and shoves enemies back; Chain Lightning becomes Kine Slam, a shockwave around you that damages and Charges everything it hits. Unlocks Mind Leech (Shift+RMB) but loses Psychic Scream (Shift+E). Excludes Arc Conduit.")
                .InGroup(WeaponGroup)
                .Requires(m => (m & (1 << ArcConduit)) == 0),
            new UpgradeDef(ArcConduit, UpgradeCategory.Ferocity, "Arc Conduit",
                "Chain Lightning also bounces once to every Charged enemy, the first target included, for 60% damage each. Excludes the Force Stave.")
                .Requires(m => (m & (1 << ForceStave)) == 0),
            new UpgradeDef(WardedMind, UpgradeCategory.Endurance, "Warded Mind",
                "+40 maximum health.").Health(WardedMindHealth),
            new UpgradeDef(WarpWard, UpgradeCategory.Endurance, "Warp Ward",
                "Warp Step wraps you in a shield that soaks 35 damage for 4s."),
        };
    }
}
