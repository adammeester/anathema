using Anathema.Upgrades;

namespace Anathema.Abilities.Mandrake
{
    // The Mandrake's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever append
    // new ones; never renumber.
    //
    // He keeps his glimmersteel blade, so there are no weapon transformations. Instead:
    //   Endurance - the shadows keep him alive (healing in stealth, a barrier on a riposte, faster escapes).
    //   Ferocity  - venom (poison that slows and hits harder) and balefire (more bolts, bigger bursts).
    public static class MandrakeUpgrade
    {
        // Endurance
        public const int Veilwalker = 0;
        public const int ShadesEmbrace = 1;
        public const int ReapingShadows = 2;

        // Ferocity: venom
        public const int CripplingToxin = 3;
        public const int EnvenomedEdge = 4;
        public const int HuntersPatience = 5;
        public const int WitchfireVenom = 6;

        // Ferocity: balefire
        public const int TwinFlames = 7;
        public const int SpreadingFlame = 8;
        public const int SmotheringFire = 9;

        // Tunables shared with the code that applies them.
        public const float VeilwalkerHealPerSecond = 5f;        // while stealthed (recoverable health only, like any heal)
        public const float ShadesEmbraceBarrier = 20f;
        public const float ShadesEmbraceSeconds = 3f;
        public const float CripplingSlowPerStack = 0.05f;       // movement slow per poison stack...
        public const int CripplingMaxStacks = 3;                // ...counting at most this many (15%)
        public const float CripplingRefreshSeconds = 0.75f;     // re-applied on every poison tick (0.5s), so it ends with the poison
        public const int EnvenomedEdgeExtraStacks = 1;          // the cleave lays 2 stacks instead of 1
        public const float HuntersPatienceVenomBonus = 0.2f;    // instead of MandrakeKit.VenomBonus (10%)
        public const float TwinFlamesChargeCooldown = 10f;      // each of the two charges recharges on its own
        public const float SpreadingFlameRadius = 3f;
        public const float SpreadingFlameStrength = 0.5f;       // of the burst balefire's burn
        public const float SmotheringHealingCut = 0.4f;         // instead of Balefire.BurstHealingCut (30%), same 4s

        // Three cards a round: Endurance, venom, balefire. A card shows the first of its alternatives not owned yet, so
        // anything passed over comes back later. The three strongest picks (Veilwalker, Crippling Toxin, Twin Flames)
        // are spread one per round, so taking one never means giving up another: Veilwalker is always on offer after
        // round 1, Crippling Toxin after round 2, Twin Flames after round 3. Round 4 brings the venom capstone.
        //
        //   after round 1   Veilwalker         Envenomed Edge      Smothering Fire
        //   after round 2   Shade's Embrace    Crippling Toxin     Spreading Flame
        //   after round 3   Reaping Shadows    Hunter's Patience   Twin Flames
        //   after round 4   (any Endurance left)  Witchfire Venom  (any balefire left)
        public static readonly int[][][] Sets =
        {
            new[]
            {
                new[] { Veilwalker },
                new[] { EnvenomedEdge },
                new[] { SmotheringFire },
            },
            new[]
            {
                new[] { ShadesEmbrace, ReapingShadows, Veilwalker },
                new[] { CripplingToxin, EnvenomedEdge, HuntersPatience },
                new[] { SpreadingFlame, SmotheringFire, TwinFlames },
            },
            new[]
            {
                new[] { ReapingShadows, ShadesEmbrace, Veilwalker },
                new[] { HuntersPatience, CripplingToxin, EnvenomedEdge },
                new[] { TwinFlames, SpreadingFlame, SmotheringFire },
            },
            new[]
            {
                new[] { ShadesEmbrace, ReapingShadows, Veilwalker },
                new[] { WitchfireVenom, HuntersPatience, CripplingToxin, EnvenomedEdge },
                new[] { TwinFlames, SpreadingFlame, SmotheringFire },
            },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(Veilwalker, UpgradeCategory.Endurance, "Veilwalker",
                "While you are hidden in the shadows, you recover 5 health a second."),
            new UpgradeDef(ShadesEmbrace, UpgradeCategory.Endurance, "Shade's Embrace",
                "A successful Umbral Riposte wraps you in a barrier that soaks 20 damage for 3s."),
            new UpgradeDef(ReapingShadows, UpgradeCategory.Endurance, "Reaping Shadows",
                "Soul Harvest also takes time off Shadow Jump's cooldown, not just Shadow Veil's."),

            new UpgradeDef(CripplingToxin, UpgradeCategory.Ferocity, "Crippling Toxin",
                "Each stack of your poison slows the enemy by 5%, up to 15% at 3 stacks."),
            new UpgradeDef(EnvenomedEdge, UpgradeCategory.Ferocity, "Envenomed Edge",
                "Glimmersteel Blade's cleave lays 2 stacks of poison instead of 1."),
            new UpgradeDef(HuntersPatience, UpgradeCategory.Ferocity, "Hunter's Patience",
                "Your attacks deal 20% more damage to poisoned enemies, instead of 10%."),
            new UpgradeDef(WitchfireVenom, UpgradeCategory.Ferocity, "Witchfire Venom",
                "When your Balefire bursts, it also sets off the target's poison."),

            new UpgradeDef(TwinFlames, UpgradeCategory.Ferocity, "Twin Flames",
                "Balefire holds 2 charges, each recharging on its own (10s). A second bolt bursts the first one's flames."),
            new UpgradeDef(SpreadingFlame, UpgradeCategory.Ferocity, "Spreading Flame",
                "When your Balefire bursts, enemies within 3m catch a half-strength Balefire."),
            new UpgradeDef(SmotheringFire, UpgradeCategory.Ferocity, "Smothering Fire",
                "Balefire's burst cuts the target's healing by 40% instead of 30%."),
        };
    }
}
