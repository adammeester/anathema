using Anathema.Upgrades;

namespace Anathema.Abilities.KhorneBerserker
{
    // The Khorne Berserker's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever
    // append new ones; never renumber.
    //
    //   Armament  - weapon transformations. They replace the chop (LMB), the secondary (RMB) and the passive.
    //               Optional: a Berserker with the default dual chainaxes stays fully competitive.
    //   Endurance - defensive upgrades.
    //   Ferocity  - enhancements to the abilities the weapon swap leaves alone (Rush, Maim, Counter, ultimate, Rage).
    public static class BerserkerUpgrade
    {
        public const int HeavyChainaxe = 0;
        public const int Eviscerator = 1;

        public const int AblativePlating = 2;
        public const int GorehoundVigour = 3;
        public const int CrimsonCommunion = 4;
        public const int DefiantRoar = 5;

        public const int CrimsonMomentum = 6;
        public const int GoreRush = 7;
        public const int Hamstring = 8;
        public const int TwinAxes = 9;
        public const int VengefulBlood = 10;
        public const int SkullthroneFavour = 11;

        public const int FeelNoPain = 12;
        public const int Unstoppable = 13;
        public const int BerserkersFury = 14;
        public const int RelentlessPursuit = 15;
        public const int Skulltaker = 16;

        public const string WeaponGroup = "weapon";

        // Tunables shared with the code that applies them.
        public const float AblativeArmour = 0.15f;
        public const float GorehoundHealth = 40f;
        public const float CommunionHealFraction = 0.15f;      // of damage dealt, as recoverable health
        public const float DefiantThreshold = 0.3f;            // of max health
        public const float DefiantHealFraction = 0.15f;        // of max health
        public const float DefiantEnrageSeconds = 4f;
        public const int MomentumExtraStacks = 2;
        public const float MomentumWindow = 5f;
        public const float GoreRushCooldownScale = 0.65f;
        public const float HamstringSlowFraction = 0.45f;
        public const float VengefulCooldownScale = 0.65f;
        public const float VengefulRetaliationScale = 1.5f;
        public const float SkullthroneCostScale = 0.75f;
        public const float FeelNoPainHit = 20f;                // a "large hit" is at least this much damage taken
        public const float FeelNoPainReduction = 0.35f;
        public const float FeelNoPainSeconds = 3f;
        public const float UnstoppableScale = 0.6f;            // crowd control lasts 60% as long
        public const float FuryHealthThreshold = 0.4f;
        public const float FuryAttackSpeed = 0.3f;
        public const float PursuitMovePerStack = 0.03f;
        public const float SkulltakerThreshold = 0.4f;         // target health fraction
        public const float SkulltakerBonus = 0.3f;

        static bool NoWeapon(int mask) => (mask & ((1 << HeavyChainaxe) | (1 << Eviscerator))) == 0;

        // The fixed offers: Sets[pick][slot] is a list of alternatives for one card (the first that is still available is
        // shown). Pick 0 is offered after round 1, pick 1 after round 2, and so on. Everyone sees the same sets, so the pick
        // has no luck in it. Every set is Armament / Endurance / Ferocity, so a Berserker can always skip the weapon slot
        // and never take a weapon at all:
        //
        //   after round 1   Armament: keep the dual chainaxes (Twin Axes)   Endurance: Feel No Pain        Ferocity: Berserker's Fury
        //   after round 2   Armament: Eviscerator                           Endurance: Blood-Fuelled Regen  Ferocity: Relentless Pursuit
        //   after round 3   Armament: Heavy Chainaxe                        Endurance: Unstoppable          Ferocity: Skulltaker
        //   after round 4   Armament slot: no weapon left to offer, so a second Endurance card
        //
        // A weapon card that is blocked (you already hold the other weapon) falls back to the next alternative in its slot.
        // Upgrades in no set (Hamstring, Skullthrone's Favour, Vengeful Blood, Defiant Roar) stay in the catalog for the
        // F1 test panel and for future sets.
        public static readonly int[][][] Sets =
        {
            new[] { new[] { TwinAxes },                             new[] { FeelNoPain },       new[] { BerserkersFury } },
            new[] { new[] { Eviscerator, GoreRush },                new[] { CrimsonCommunion }, new[] { RelentlessPursuit } },
            new[] { new[] { HeavyChainaxe, GorehoundVigour },       new[] { Unstoppable },      new[] { Skulltaker } },
            new[] { new[] { AblativePlating },                      new[] { DefiantRoar },      new[] { CrimsonMomentum } },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(HeavyChainaxe, UpgradeCategory.Armament, "Heavy Chainaxe - The Reaver",
                "Slow, brutal area damage. Chop becomes a wide heavy sweep with greater reach; Axe Toss becomes Gorecleave, a frontal sweep that knocks back and bleeds. Heavy hits inflict Bleed.")
                .InGroup(WeaponGroup),
            new UpgradeDef(Eviscerator, UpgradeCategory.Armament, "Eviscerator - The Executioner",
                "High-risk burst. Chop becomes a slow strike with a pronounced wind-up; Axe Toss becomes Executioner's Descent, an overhead blow that deals bonus damage to the wounded. Attacks partially ignore armour and shields.")
                .InGroup(WeaponGroup),

            new UpgradeDef(AblativePlating, UpgradeCategory.Endurance, "Ablative Plating",
                "Take 15% less damage from every source.").Armour(AblativeArmour),
            new UpgradeDef(GorehoundVigour, UpgradeCategory.Endurance, "Gorehound's Vigour",
                "+40 maximum health.").Health(GorehoundHealth),
            new UpgradeDef(CrimsonCommunion, UpgradeCategory.Endurance, "Blood-Fuelled Regeneration",
                "Heal 15% of the damage you deal with melee attacks (recoverable health only)."),
            new UpgradeDef(FeelNoPain, UpgradeCategory.Endurance, "Feel No Pain",
                "Taking a large hit (20+ damage) grants 35% damage reduction for 3s."),
            new UpgradeDef(Unstoppable, UpgradeCategory.Endurance, "Unstoppable",
                "Stuns, silences, roots, slows and panic last 40% shorter.").CrowdControl(UnstoppableScale),
            new UpgradeDef(DefiantRoar, UpgradeCategory.Endurance, "Defiant Roar",
                "Once per round, dropping below 30% health heals you for 15% and sends you into a 4s Enraged state."),

            new UpgradeDef(CrimsonMomentum, UpgradeCategory.Ferocity, "Crimson Momentum",
                "Rage holds 2 more stacks and lasts 5s between hits."),
            new UpgradeDef(GoreRush, UpgradeCategory.Ferocity, "Gore Rush",
                "Axe Rush recharges 35% faster and its sweep leaves targets bleeding."),
            new UpgradeDef(BerserkersFury, UpgradeCategory.Ferocity, "Berserker's Fury",
                "+30% attack speed while below 40% health."),
            new UpgradeDef(RelentlessPursuit, UpgradeCategory.Ferocity, "Relentless Pursuit",
                "Each Rage stack from consecutive hits also adds 3% movement speed."),
            new UpgradeDef(Skulltaker, UpgradeCategory.Ferocity, "Skulltaker",
                "Your melee attacks deal 30% more damage to enemies below 40% health."),
            new UpgradeDef(Hamstring, UpgradeCategory.Ferocity, "Hamstring",
                "Maim slows by 45% and also makes the target bleed."),
            new UpgradeDef(TwinAxes, UpgradeCategory.Armament, "Keep the Dual Chainaxes",
                "No transformation: your chainaxes stay as they are, and Axe Toss now hurls a second axe in a spread.")
                .Requires(NoWeapon),
            new UpgradeDef(VengefulBlood, UpgradeCategory.Ferocity, "Vengeful Blood",
                "Counter recharges 35% faster and its retaliation hits 50% harder."),
            new UpgradeDef(SkullthroneFavour, UpgradeCategory.Ferocity, "Skullthrone's Favour",
                "Blood for the Blood God costs 25% less energy."),
        };
    }
}
