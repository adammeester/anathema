using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // The Hospitaller's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever
    // append new ones; never renumber.
    //
    // Each category is one playstyle; the first pick establishes it, the next two build on it, and the last pick is a
    // capstone that bends Martyrdom towards it. Payoffs land during the fight, never on kills.
    //   Endurance - the Martyr: stay on an ally and carry their pain.
    //   Ferocity  - the Inquisitor: mark a target with Penance and burn it down with the team.
    //   Remedy    - the Sanctifier: control the ground with Sanctus Pulse and out-heal burst.
    public static class HospitallerUpgrade
    {
        // Martyr (Endurance)
        public const int SharedBurden = 0;
        public const int SteadfastFaith = 1;
        public const int BearerOfBurdens = 2;
        public const int Sanctuary = 3;
        public const int Absolution = 4;
        public const int LastRites = 5;
        public const int SaintsAegis = 6;

        // Inquisitor (Ferocity)
        public const int ZealousFervour = 7;
        public const int SearingLight = 8;
        public const int WrathfulDoves = 9;
        public const int Judgement = 10;
        public const int Damnation = 11;
        public const int MartyrsDue = 12;
        public const int HolyPyre = 13;

        // Sanctifier (Remedy)
        public const int OverflowingGrace = 14;
        public const int Tithes = 15;
        public const int Communion = 16;
        public const int LingeringBlessing = 17;
        public const int HallowedGround = 18;
        public const int SwiftPrayers = 19;
        public const int Rapture = 20;

        // Tunables shared with the code that applies them.
        public const float SharedBurdenFraction = 0.45f;     // tether share on an ally (base 35%)
        public const int SteadfastMaxStacks = 7;             // Faith cap (base 5)
        public const float BearerLinger = 1.5f;              // her healable health decays this much later and slower
        public const int SanctuaryFaith = 2;
        public const float AbsolutionFraction = 0.3f;        // of the damage carried on an ally link, healed when it ends
        public const float SaintsAegisFraction = 0.25f;      // of everything Martyrdom absorbed, healed to each bound ally

        public const float ZealDamagePerStack = 20f;         // tether damage on enemies per Faith stack
        public const float SearingLightScale = 1.5f;
        public const int SearingLightMinStacks = 3;
        public const float WrathfulDamagePerStack = 1.5f;
        public const float WrathfulRechargeRefund = 1f;      // seconds off a tether charge per dove hit on a Penance target
        public const float JudgementDuration = 6f;
        public const float JudgementExposed = 0.15f;
        public const float DamnationFraction = 0.25f;        // of the damage taken during Penance, dealt when it ends
        public const float DamnationMax = 40f;
        public const float MartyrsDueFraction = 0.1f;        // of an ally's direct damage on a Penance target, healed back
        public const float HolyPyreMaxDamage = 120f;
        public const float HolyPyreHealCut = 0.4f;
        public const float HolyPyreHealCutSeconds = 3f;

        public const float OverflowCap = 15f;
        public const float OverflowSeconds = 3f;
        public const float TithesEnergyPerHeal = 0.25f;      // energy per point of healing that lands on an ally
        public const float CommunionShare = 0.7f;            // each ally inside gets this much of the pulse (never less than the split)
        public const float LingeringDelay = 1f;
        public const float LingeringScale = 0.5f;
        public const float HallowedSeconds = 3f;
        public const float HallowedBonus = 10f;              // extra healable health for allies on the blessed ground
        public const float SwiftPrayersScale = 0.75f;        // Sanctus recharge 6s -> 4.5s
        public const float RaptureHeal = 26f;

        public static bool Has(GameObject hospitaller, int bit) => UpgradeState.Has(hospitaller, bit);

        public static readonly int[][][] Sets =
        {
            new[] { new[] { SharedBurden, SteadfastFaith }, new[] { ZealousFervour, SearingLight }, new[] { OverflowingGrace, Tithes } },
            new[] { new[] { BearerOfBurdens, Sanctuary }, new[] { WrathfulDoves, Judgement }, new[] { Communion, LingeringBlessing } },
            new[] { new[] { Absolution, LastRites }, new[] { Damnation, MartyrsDue }, new[] { HallowedGround, SwiftPrayers } },
            new[] { new[] { SaintsAegis }, new[] { HolyPyre }, new[] { Rapture } },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(SharedBurden, UpgradeCategory.Endurance, "Shared Burden",
                "Your tether on an ally carries 45% of the damage they take, instead of 35%."),
            new UpgradeDef(SteadfastFaith, UpgradeCategory.Endurance, "Steadfast Faith",
                "Faith holds up to 7 stacks instead of 5."),
            new UpgradeDef(BearerOfBurdens, UpgradeCategory.Endurance, "Bearer of Burdens",
                "Your healable health lasts 50% longer before it fades, so the pain you carry can still be mended."),
            new UpgradeDef(Sanctuary, UpgradeCategory.Endurance, "Sanctuary",
                "When Martyr's Grace catches a hit, you gain 2 Faith."),
            new UpgradeDef(Absolution, UpgradeCategory.Endurance, "Absolution",
                "When your tether on an ally ends, they are healed for 30% of the damage you carried for them."),
            new UpgradeDef(LastRites, UpgradeCategory.Endurance, "Last Rites",
                "Once per round, a hit that would kill you leaves you at 1 health and a Sanctus Pulse bursts at your feet."),
            new UpgradeDef(SaintsAegis, UpgradeCategory.Endurance, "Saint's Aegis",
                "When Martyrdom ends, every ally it bound is healed for 25% of everything you absorbed."),

            new UpgradeDef(ZealousFervour, UpgradeCategory.Ferocity, "Zealous Fervour",
                "Damage your tether deals to enemies also builds Faith: 1 stack per 20 damage."),
            new UpgradeDef(SearingLight, UpgradeCategory.Ferocity, "Searing Light",
                "While you have 3 or more Faith, your tether burns enemies 50% harder."),
            new UpgradeDef(WrathfulDoves, UpgradeCategory.Ferocity, "Wrathful Doves",
                "Dove Strike deals +1.5 damage per Faith stack, and each dove that hits an enemy under Penance takes 1s off a tether charge."),
            new UpgradeDef(Judgement, UpgradeCategory.Ferocity, "Judgement",
                "Penance lasts 6s instead of 4, and its target takes 15% more damage instead of 10%."),
            new UpgradeDef(Damnation, UpgradeCategory.Ferocity, "Damnation",
                "When Penance ends, its target is burned for 25% of the damage they took during it (up to 40)."),
            new UpgradeDef(MartyrsDue, UpgradeCategory.Ferocity, "Martyr's Due",
                "While Penance holds, allies who hit its target are healed for 10% of the damage they deal."),
            new UpgradeDef(HolyPyre, UpgradeCategory.Ferocity, "Holy Pyre",
                "Martyrdom's burst can deal up to 120 instead of 80, and cuts the healing of everyone it hits by 40% for 3s."),

            new UpgradeDef(OverflowingGrace, UpgradeCategory.Remedy, "Overflowing Grace",
                "Healing you give beyond what someone can recover becomes a barrier on them (up to 15, for 3s)."),
            new UpgradeDef(Tithes, UpgradeCategory.Remedy, "Tithes",
                "Healing you give to allies builds energy: 1 for every 4 health restored."),
            new UpgradeDef(Communion, UpgradeCategory.Remedy, "Communion",
                "Sanctus Pulse no longer splits its healing: every ally inside receives 70% of it (or their even share, if more)."),
            new UpgradeDef(LingeringBlessing, UpgradeCategory.Remedy, "Lingering Blessing",
                "Each Sanctus Pulse pulses again 1s later at half strength."),
            new UpgradeDef(HallowedGround, UpgradeCategory.Remedy, "Hallowed Ground",
                "Sanctus Pulse leaves blessed ground for 3s: allies standing on it can recover 10 more health than usual."),
            new UpgradeDef(SwiftPrayers, UpgradeCategory.Remedy, "Swift Prayers",
                "Sanctus Pulse charges recharge in 4.5s instead of 6s."),
            new UpgradeDef(Rapture, UpgradeCategory.Remedy, "Rapture",
                "When Martyrdom ends, all your Sanctus Pulse charges return and every ally it bound is healed for 26."),
        };
    }
}
