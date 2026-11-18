using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // The Thousand Sons Sorcerer's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only
    // ever append new ones; never renumber. Payoffs land during the fight, never on kills.
    //
    // The categories are shown as three of the Cults of Prospero (UpgradeDef.Themed):
    //   Pavoni (biomancy)    - Remedy:    healing and restoring allies
    //   Raptora (telekinesis) - Endurance: barriers and kinetic shields
    //   Corvidae (divination) - Ferocity:  the curse and the tricks of time
    //   Pyrae (pyromancy)     - Ferocity:  warpfire
    //
    //   after round 1   Rejuvenate (Pavoni)          Sandshell (Raptora)        Hastened Ruin (Corvidae)
    //   after round 2   Shifting Dunes (Pavoni)      Kinetic Ward (Raptora)     Echoing Hourglass (Corvidae)
    //   after round 3   the Pavoni and Raptora cards not yet taken            Fires of Tzeentch (Pyrae)
    //   later           random draws from what is left
    public static class SorcererUpgrade
    {
        // Remedy (Pavoni)
        public const int Rejuvenate = 0;
        public const int ShiftingDunes = 1;

        // Endurance (Raptora)
        public const int Sandshell = 2;
        public const int KineticWard = 3;

        // Ferocity (Corvidae)
        public const int HastenedRuin = 4;
        public const int EchoingHourglass = 5;

        // Ferocity (Pyrae)
        public const int FiresOfTzeentch = 6;

        // Tunables shared with the code that applies them.
        public const int ShiftingDunesMaxBonus = 2;        // bonus Mending Sands charges on top of the usual 3 (5 in all)
        public const float SandshellAmount = 14f;          // barrier on the Sorcerer and each ally Dust Form passes through
        public const float SandshellSeconds = 3f;
        public static readonly float[] KineticWardSteps = { 16f, 8f, 4f }; // barrier per negated attack, falling off
        public const float KineticWardSeconds = 3.5f;
        public const float HastenedRuinBonus = 4f;         // extra damage when a curse is set off early
        public const float EchoDelay = 0.5f;               // the lesser hourglass is ready this long after the first throw...
        public const float EchoWindow = 2f;                // ...and stays ready this long
        public const float EchoDamage = 10f;
        public const float EchoRadius = 1.6f;
        public const float WarpfireRadius = 2.5f;          // Fires of Tzeentch: the pool where Weaver of Fates lands
        public const float WarpfireSeconds = 4f;
        public const float WarpfireDamagePerSecond = 10f;

        public const string Pavoni = "PAVONI";
        public const string Raptora = "RAPTORA";
        public const string Corvidae = "CORVIDAE";
        public const string Pyrae = "PYRAE";
        static readonly Color PavoniColor = new Color(0.45f, 0.85f, 0.45f);
        static readonly Color RaptoraColor = new Color(0.35f, 0.7f, 1f);
        static readonly Color CorvidaeColor = new Color(0.72f, 0.52f, 1f);
        static readonly Color PyraeColor = new Color(1f, 0.45f, 0.75f);

        public static bool Has(GameObject sorcerer, int bit) => sorcerer != null && UpgradeState.Has(sorcerer, bit);

        public static readonly int[][][] Sets =
        {
            new[]
            {
                new[] { Rejuvenate },
                new[] { Sandshell },
                new[] { HastenedRuin },
            },
            new[]
            {
                new[] { ShiftingDunes },
                new[] { KineticWard },
                new[] { EchoingHourglass },
            },
            new[]
            {
                new[] { Rejuvenate, ShiftingDunes },
                new[] { Sandshell, KineticWard },
                new[] { FiresOfTzeentch },
            },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(Rejuvenate, UpgradeCategory.Remedy, "Rejuvenate",
                "Mending Sands leaves Mending on everyone it heals: they heal another 6 after 3.5s. Dust Form makes it heal at once.").Themed(Pavoni, PavoniColor),
            new UpgradeDef(ShiftingDunes, UpgradeCategory.Remedy, "Shifting Dunes",
                "Every enemy hit by Sinking Sands gives you an extra Mending Sands charge, up to 5 in all. Extra charges don't recharge.").Themed(Pavoni, PavoniColor),

            new UpgradeDef(Sandshell, UpgradeCategory.Endurance, "Sandshell",
                "Pouring through an ally with Dust Form wraps you both in a 14-point barrier for 3s.").Themed(Raptora, RaptoraColor),
            new UpgradeDef(KineticWard, UpgradeCategory.Endurance, "Kinetic Ward",
                "Each attack Kine Shield negates gives you a barrier for 3.5s: 16 for the first, then 8, then 4.").Themed(Raptora, RaptoraColor),

            new UpgradeDef(HastenedRuin, UpgradeCategory.Ferocity, "Hastened Ruin",
                "A Curse of the Rubricae set off early (Bolt of Change, Sinking Sands, Dust Form) deals 4 extra damage.").Themed(Corvidae, CorvidaeColor),
            new UpgradeDef(EchoingHourglass, UpgradeCategory.Ferocity, "Echoing Hourglass",
                "Sinking Sands gains a second throw: shortly after the first, press E again (within 2s) to lob a lesser hourglass. It covers a smaller area " +
                "and deals 10 damage, still stops enemies dead, and lays the Curse of the Rubricae instead of setting it off.").Themed(Corvidae, CorvidaeColor),

            new UpgradeDef(FiresOfTzeentch, UpgradeCategory.Ferocity, "Fires of Tzeentch",
                "Where Weaver of Fates brings someone back, a pool of blue and pink warpfire erupts, burning enemies inside for " +
                "10 damage a second for 4s.").Themed(Pyrae, PyraeColor),
        };
    }
}
