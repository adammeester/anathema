using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // The Skitarii Ranger's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever
    // append new ones; never renumber.
    //
    // She keeps her arquebus, so there are no weapon transformations. Instead:
    //   Endurance - staying alive and at range (Disengage upgrades, holding her ground while planted).
    //   Ferocity  - Transuranic Shot (the patient sniper), and Galvanic Shot / Shrapnel Field (pressure and zoning).
    // Each upgrade is applied by the ability it changes, which reads the mask through Has().
    public static class RangerUpgrade
    {
        // Endurance
        public const int ServoLeap = 0;
        public const int RefractorField = 1;
        public const int CaltropBurst = 2;
        public const int AugmeticBracing = 3;

        // Ferocity: Transuranic Shot
        public const int QuickenedCapacitors = 4;
        public const int ExecutionersRound = 5;
        public const int Overpenetration = 6;
        public const int RadSaturation = 7;

        // Ferocity: Galvanic Shot and Shrapnel Field
        public const int ArcRound = 8;
        public const int GalvanicOverload = 9;
        public const int LingeringShrapnel = 10;
        public const int PointBlankDoctrine = 11;

        // Tunables shared with the code that applies them.
        public const float ServoLeapChargeCooldown = 7f;        // each of the two charges recharges on its own
        public const float RefractorBarrier = 20f;
        public const float RefractorSeconds = 2f;
        public const float QuickenedChargeScale = 0.7f;         // Transuranic charge time x0.7 (1.6s -> 1.12s)
        public const float ExecutionerBonus = 0.25f;            // full-charge shot vs. targets below...
        public const float ExecutionerThreshold = 0.5f;         // ...this share of max health
        public const float OverpenetrationPerTarget = 0.15f;    // each enemy already pierced adds this much damage
        public const float RadSaturationBonus = 0.15f;          // the marked target takes this much more damage...
        public const float RadSaturationSeconds = 4f;           // ...from every source, for this long
        public const float GalvanicOverloadEnergy = 4f;         // bonus energy for a burst that lands every round
        public const float LingeringDurationScale = 1.5f;
        public const float LingeringRadiusScale = 1.25f;
        public const float PointBlankExCost = 25f;              // Scattershot's cost instead of ShrapnelField.exEnergyCost (35)
        public const float PointBlankKnockbackScale = 1.5f;

        // A charge counts as "full" from here (floating point never quite reaches 1 on the frame it is released).
        public const float FullCharge = 0.999f;

        public static bool Has(GameObject ranger, int bit) => UpgradeState.Has(ranger, bit);

        // Three cards a round: Endurance, Transuranic, Galvanic/Shrapnel. A card shows the first of its alternatives not
        // owned yet, so anything passed over comes back later. The three strongest picks (Quickened Capacitors, Arc
        // Round, Servo-Leap) arrive in different rounds and stay on offer afterwards, so taking one never means giving
        // up another. Round 4 brings the Transuranic capstone.
        //
        //   after round 1   Refractor Field    Quickened Capacitors   Lingering Shrapnel
        //   after round 2   Caltrop Burst      Overpenetration        Arc Round
        //   after round 3   Servo-Leap         Executioner's Round    Galvanic Overload
        //   after round 4   Augmetic Bracing   Rad-Saturation         Point-Blank Doctrine
        public static readonly int[][][] Sets =
        {
            new[]
            {
                new[] { RefractorField },
                new[] { QuickenedCapacitors },
                new[] { LingeringShrapnel },
            },
            new[]
            {
                new[] { CaltropBurst, RefractorField },
                new[] { Overpenetration, QuickenedCapacitors },
                new[] { ArcRound, LingeringShrapnel },
            },
            new[]
            {
                new[] { ServoLeap, CaltropBurst, RefractorField },
                new[] { ExecutionersRound, Overpenetration, QuickenedCapacitors },
                new[] { GalvanicOverload, ArcRound, LingeringShrapnel },
            },
            new[]
            {
                new[] { AugmeticBracing, ServoLeap, CaltropBurst, RefractorField },
                new[] { RadSaturation, ExecutionersRound, Overpenetration, QuickenedCapacitors },
                new[] { PointBlankDoctrine, GalvanicOverload, ArcRound, LingeringShrapnel },
            },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(ServoLeap, UpgradeCategory.Endurance, "Servo-Leap",
                "Disengage holds 2 charges, each recharging on its own (7s)."),
            new UpgradeDef(RefractorField, UpgradeCategory.Endurance, "Refractor Field",
                "Landing from Disengage wraps you in a barrier that soaks 20 damage for 2s."),
            new UpgradeDef(CaltropBurst, UpgradeCategory.Endurance, "Caltrop Burst",
                "Disengage gains an EX (Shift, 30 energy): the spot you leap from is strewn with caltrops that cut and slow pursuers."),
            new UpgradeDef(AugmeticBracing, UpgradeCategory.Endurance, "Augmetic Bracing",
                "While charging Transuranic Shot or firing Culling Volley you can't be knocked back, so knockback no longer breaks them."),

            new UpgradeDef(QuickenedCapacitors, UpgradeCategory.Ferocity, "Quickened Capacitors",
                "Transuranic Shot charges 30% faster."),
            new UpgradeDef(ExecutionersRound, UpgradeCategory.Ferocity, "Executioner's Round",
                "A fully charged Transuranic Shot deals 25% more damage to enemies below half health."),
            new UpgradeDef(Overpenetration, UpgradeCategory.Ferocity, "Overpenetration",
                "Transuranic Shot gains 15% damage for every enemy it has already pierced."),
            new UpgradeDef(RadSaturation, UpgradeCategory.Ferocity, "Rad-Saturation",
                "A fully charged Transuranic Shot irradiates what it hits: they take 15% more damage from every source for 4s."),

            new UpgradeDef(ArcRound, UpgradeCategory.Ferocity, "Arc Round",
                "Galvanic Shot gains an EX (Shift, 20 energy): each round that hits arcs to the nearest other enemy for 75% damage."),
            new UpgradeDef(GalvanicOverload, UpgradeCategory.Ferocity, "Galvanic Overload",
                "A Galvanic burst that lands all its rounds gives 4 bonus energy."),
            new UpgradeDef(LingeringShrapnel, UpgradeCategory.Ferocity, "Lingering Shrapnel",
                "Shrapnel Field lasts 50% longer and covers a 25% wider area."),
            new UpgradeDef(PointBlankDoctrine, UpgradeCategory.Ferocity, "Point-Blank Doctrine",
                "Scattershot costs 25 energy instead of 35 and knocks enemies back 50% farther."),
        };
    }
}
