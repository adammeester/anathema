using Anathema.Upgrades;

namespace Anathema.Abilities.PlagueSurgeon
{
    // The Plague Surgeon's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever
    // append new ones; never renumber.
    //
    // He is always a support whose damage is rot over time, so he has no weapon transformations. Instead:
    //   Endurance - keeping him alive.
    //   Ferocity  - Affliction: more damage-over-time pressure.
    //   Remedy    - support: healing and protecting allies.
    public static class PlagueSurgeonUpgrade
    {
        // Endurance
        public const int BloatedConstitution = 0;
        public const int DisgustinglyResilient = 1;
        public const int RottingHide = 2;
        public const int ChitinousWings = 3;

        // Ferocity (Affliction)
        public const int Virulence = 4;
        public const int Contagion = 5;
        public const int HungryBrood = 6;
        public const int LingeringMiasma = 7;
        public const int RotHarvest = 8;

        // Remedy
        public const int TonicSpit = 9;
        public const int Swarm = 10;
        public const int FecundWard = 11;
        public const int Overdose = 12;
        public const int FieldTriage = 13;

        // Endurance (appended later)
        public const int ParasiticBond = 14;

        // Tunables shared with the code that applies them.
        public const float BloatedHealth = 50f;
        public const float ResilientFraction = 0.3f;          // of area and damage-over-time damage
        public const float RottingHideReach = 3.5f;           // an attacker this close with a direct hit counts as melee
        public const float RottingHideSeconds = 3f;
        public const float RottingHideDamagePerSecond = 2f;
        public const float WingsCooldownScale = 0.75f;
        public const float WingsBarrier = 20f;
        public const float WingsBarrierSeconds = 3f;
        public const int VirulenceMaxStacks = 5;
        public const float VirulenceExtraSeconds = 1f;
        public const float ContagionRadius = 5f;
        public const float ContagionInterval = 2f;
        public const float BroodRange = 6f;
        public const float BroodDurationScale = 0.75f;
        public const float MiasmaLifetimeScale = 1.5f;
        public const float MiasmaGrowth = 0.3f;               // the cloud ends 30% wider than it started
        public const float RotHarvestEnergyPerStack = 0.4f;   // per Festering tick (4 a second)
        public const float TonicHeal = 4f;
        public const float FecundStrengthScale = 1.25f;
        public const float FecundHeal = 20f;
        public const float OverdoseCap = 15f;
        public const float OverdoseSeconds = 3f;
        public const float TriageReduction = 0.5f;
        public const float TriageSeconds = 0.75f;             // refreshed by every cloud tick (twice a second)

        // Endurance / Affliction / Remedy each round, with two Remedy or two Affliction cards in the last two sets so
        // every upgrade is offered once. Past round 4 the leftovers come from the random draw.
        //
        //   after round 1   Bloated Constitution    Virulence                          Tonic Spit
        //   after round 2   Disgustingly Resilient, Parasitic Bond  Hungry Brood       Swarm
        //   after round 3   Rotting Hide            Contagion                          Overdose, Field Triage
        //   after round 4   Chitinous Wings         Lingering Miasma, Rot Harvest      Fecund Ward
        public static readonly int[][][] Sets =
        {
            new[] { new[] { BloatedConstitution }, new[] { Virulence }, new[] { TonicSpit } },
            new[] { new[] { DisgustinglyResilient }, new[] { ParasiticBond }, new[] { HungryBrood }, new[] { Swarm } },
            new[] { new[] { RottingHide }, new[] { Contagion }, new[] { Overdose }, new[] { FieldTriage } },
            new[] { new[] { ChitinousWings }, new[] { LingeringMiasma }, new[] { RotHarvest }, new[] { FecundWard } },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(BloatedConstitution, UpgradeCategory.Endurance, "Bloated Constitution",
                "+50 maximum health.").Health(BloatedHealth),
            new UpgradeDef(DisgustinglyResilient, UpgradeCategory.Endurance, "Disgustingly Resilient",
                "Take 30% less damage from area attacks and damage over time.").AreaResist(ResilientFraction),
            new UpgradeDef(RottingHide, UpgradeCategory.Endurance, "Rotting Hide",
                "Enemies who hit you up close gain a stack of Festering."),
            new UpgradeDef(ChitinousWings, UpgradeCategory.Endurance, "Chitinous Wings",
                "Winged Escape recharges 25% faster, and you land with a barrier that soaks 20 damage for 3s."),
            new UpgradeDef(ParasiticBond, UpgradeCategory.Endurance, "Parasitic Bond",
                "Damage your Pestilent Flies deal also heals you for the same amount."),

            new UpgradeDef(Virulence, UpgradeCategory.Ferocity, "Virulence",
                "Festering holds up to 5 stacks instead of 3, and each stack from Plague Spit lasts 1s longer."),
            new UpgradeDef(Contagion, UpgradeCategory.Ferocity, "Contagion",
                "When a Festering enemy dies, their stacks spread to enemies within 5m. An enemy at maximum Festering also passes 1 stack to the nearest other enemy every 2s."),
            new UpgradeDef(HungryBrood, UpgradeCategory.Ferocity, "Hungry Brood",
                "When a Pestilent Fly's host dies or its time runs out, it jumps once to the nearest enemy (or ally, for a tending fly) within 6m and stays for 75% of its duration."),
            new UpgradeDef(LingeringMiasma, UpgradeCategory.Ferocity, "Lingering Miasma",
                "Plague Clouds last 50% longer and slowly spread, ending 30% wider."),
            new UpgradeDef(RotHarvest, UpgradeCategory.Ferocity, "Rot Harvest",
                "Festering feeds you energy for every stack: about 5 energy a second from a target with 3 stacks, instead of 2."),

            new UpgradeDef(TonicSpit, UpgradeCategory.Remedy, "Tonic Spit",
                "Plague Spit no longer passes through allies: a glob that hits one heals them for 4."),
            new UpgradeDef(Swarm, UpgradeCategory.Remedy, "Swarm",
                "Pestilent Fly holds a second charge, and each charge recharges independently."),
            new UpgradeDef(FecundWard, UpgradeCategory.Remedy, "Fecund Ward",
                "Ward of Plague is 25% stronger, and its rupture also heals allies near the holder for 20."),
            new UpgradeDef(Overdose, UpgradeCategory.Remedy, "Overdose",
                "Healing you give beyond what someone can recover becomes a barrier on them (up to 15, for 3s)."),
            new UpgradeDef(FieldTriage, UpgradeCategory.Remedy, "Field Triage",
                "Your healing cloud washes bleeding and burning off allies inside it, and they take 50% less damage over time while they stay in it."),
        };
    }
}
