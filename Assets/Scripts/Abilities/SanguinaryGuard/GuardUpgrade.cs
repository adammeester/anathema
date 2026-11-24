using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    public enum GuardWeapon { PowerSword, EncarmineAxe, GlaiveEncarmine }

    // The Sanguinary Guard's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever
    // append new ones; never renumber. Payoffs land during the fight, never on kills.
    //
    //   Armament  - optional weapon swaps. They replace the Power Sword's cuts, the Exsanguinating Arc and Blood
    //               Transfusion; Vitae and Red Thirst stay on every weapon. A weapon is for good: there is no swapping
    //               back, so the weapon's own upgrades are never wasted.
    //   Endurance - surviving the health he spends.
    //   Ferocity  - sharpening the rest of the kit.
    public static class GuardUpgrade
    {
        // Armament
        public const int EncarmineAxe = 0;
        public const int GlaiveEncarmine = 1;
        public const int HeadsmansDue = 2;        // Axe
        public const int Wellspring = 3;          // Axe
        public const int DancingBlade = 4;        // Glaive
        public const int BloodRicochet = 5;       // Glaive

        // Endurance
        public const int SanctifiedBlood = 6;
        public const int DeepVeins = 7;
        public const int IronHalo = 8;
        public const int BloodWard = 9;

        // Ferocity
        public const int BrimmingChalice = 10;
        public const int OverflowingChalice = 11;
        public const int Unquenched = 12;
        public const int WrathOfBaal = 13;
        public const int DeathFromAbove = 14;
        public const int CrimsonMomentum = 15;
        public const int SanguinaryRite = 16;
        public const int DeathMask = 17;
        public const int SeraphicAscension = 18;
        public const int BladeOfSanguinius = 19;

        public const string WeaponGroup = "weapon";

        // ---- Tunables shared with the code that applies them -------------------------------------------------------

        // Encarmine Axe: heavy chops; the Arc becomes Executioner's Cleave; RMB becomes Sanguine Font.
        public const float AxeDamage = 15f;
        public const float AxeRange = 2.8f;
        public const float AxeDegrees = 110f;
        public const float AxeCooldown = 1f;
        public const float CleaveLength = 4.5f;
        public const float CleaveWidth = 1.6f;
        public const float CleaveDamage = 40f;
        public const float HeadsmanSlow = 0.5f;           // Cleave's slow with Headsman's Due (base: the Arc's 30%)
        public const float FontCost = 12f;
        public const float FontRange = 8f;
        public const float FontRadius = 2.5f;
        public const float FontSeconds = 3f;
        public const float FontHealPerSecond = 7f;
        public const float FontCooldown = 7f;
        public const float WellspringSeconds = 5f;
        public const float WellspringLinger = 2f;        // allies who step out keep healing this long

        // Glaive Encarmine: long narrow thrusts; the Arc becomes Twin Crescent; Transfusion bounces once.
        public const float GlaiveDamage = 10f;
        public const float GlaiveRange = 3.4f;
        public const float GlaiveDegrees = 70f;
        public const float CrescentShare = 0.7f;         // each of the two spins, of the Arc's damage
        public const float CrescentGap = 0.2f;           // seconds between the two spins
        public const float DancingBladeEnergy = 20f;
        public const float GlaiveBoltHeal = 18f;
        public const float BounceHeal = 10f;
        public const float BounceRange = 6f;

        // Endurance
        public const float DeepVeinsHealth = 25f;
        public const float DeepVeinsFloor = 0.1f;        // health-cost floor (base 15%)
        public const float IronHaloThreshold = 0.2f;
        public const float IronHaloShield = 40f;
        public const float IronHaloSeconds = 3f;
        public const float BloodWardShare = 0.5f;        // of the health paid, as a shield
        public const float BloodWardSeconds = 2f;

        // Ferocity
        public const float BrimmingCap = 130f;
        public const float BrimmingArcDamage = 1.2f;
        public const float OverflowShare = 0.5f;         // overhealing counts this much toward Vitae
        public const float UnquenchedThreshold = 0.3f;
        public const float WrathReturnTime = 0.3f;
        public const float DeathFromAboveStun = 0.5f;
        public const float MomentumCooldownScale = 0.7f;
        public const float RiteRecharge = 9f;       // Chalice Offering charge recharge (base 12s)
        public const float DeathMaskPanic = 0.8f;
        public const float SeraphicSeconds = 7f;
        public const int RicochetBounces = 2;
        public const float RicochetRange = 6f;
        public const float RicochetDamageShare = 0.5f;
        public const float RicochetStunShare = 0.5f;

        public static bool Has(GameObject guard, int bit) => UpgradeState.Has(guard, bit);

        public static GuardWeapon WeaponOf(GameObject guard)
        {
            if (Has(guard, EncarmineAxe)) return GuardWeapon.EncarmineAxe;
            if (Has(guard, GlaiveEncarmine)) return GuardWeapon.GlaiveEncarmine;
            return GuardWeapon.PowerSword;
        }

        static bool Wields(int mask, int weapon) => (mask & (1 << weapon)) != 0;

        // The fixed offers: Sets[pick][slot] lists alternatives for one card (a random one first; anything owned or
        // blocked is skipped). Pick 0 is offered after round 1. Every set is Armament / Endurance / Ferocity, and the
        // first one offers both weapons side by side.
        //   - Armament: the weapons while he holds the sword; once he holds one, that weapon's own upgrades; a guard
        //     who keeps the sword gets Death from Above or Seraphic Ascension there instead.
        //   - Wrath of Baal and Blade of Sanguinius share a card, so they rarely come together.
        public static readonly int[][][] Sets =
        {
            new[] { new[] { EncarmineAxe }, new[] { GlaiveEncarmine }, new[] { BloodWard, DeepVeins }, new[] { CrimsonMomentum, Unquenched, OverflowingChalice } },
            new[] { new[] { HeadsmansDue, DancingBlade, EncarmineAxe, GlaiveEncarmine }, new[] { SanctifiedBlood, IronHalo }, new[] { WrathOfBaal, BladeOfSanguinius } },
            new[] { new[] { Wellspring, BloodRicochet, DeathFromAbove }, new[] { IronHalo, BloodWard, DeepVeins }, new[] { DeathMask, SanguinaryRite, BrimmingChalice } },
            new[] { new[] { HeadsmansDue, DancingBlade, Wellspring, BloodRicochet, DeathFromAbove, SeraphicAscension }, new[] { SanctifiedBlood, DeepVeins, IronHalo, BloodWard }, new[] { SeraphicAscension, BrimmingChalice, BladeOfSanguinius, WrathOfBaal } },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(EncarmineAxe, UpgradeCategory.Armament, "Encarmine Axe - The Butcher",
                "Your cuts become slow, heavy chops (15 damage, every 1s). The Exsanguinating Arc becomes Executioner's Cleave: a 4.5m line in front of you for 40. Blood Transfusion becomes Sanguine Font: pay 12 health to pour a pool up to 8m away that heals allies in it 7/s for 3s.")
                .InGroup(WeaponGroup),
            new UpgradeDef(GlaiveEncarmine, UpgradeCategory.Armament, "Glaive Encarmine - The Duellist",
                "Your cuts become long, narrow thrusts (10 damage, 3.4m reach). The Exsanguinating Arc becomes Twin Crescent: two spins at 70% damage each, so its lifesteal lands twice. Blood Transfusion heals 18, then bounces to the nearest other ally within 6m for 10.")
                .InGroup(WeaponGroup),
            new UpgradeDef(HeadsmansDue, UpgradeCategory.Armament, "Headsman's Due",
                "Encarmine Axe only. Executioner's Cleave slows by 50% instead of 30%.")
                .Requires(m => Wields(m, EncarmineAxe)),
            new UpgradeDef(Wellspring, UpgradeCategory.Armament, "Wellspring",
                "Encarmine Axe only. Sanguine Font lasts 5s, and allies who leave it keep healing for 2s.")
                .Requires(m => Wields(m, EncarmineAxe)),
            new UpgradeDef(DancingBlade, UpgradeCategory.Armament, "Dancing Blade",
                "Glaive only. Landing both spins of Twin Crescent on the same enemy gives you 20 energy.")
                .Requires(m => Wields(m, GlaiveEncarmine)),
            new UpgradeDef(BloodRicochet, UpgradeCategory.Armament, "Blood Ricochet",
                "Glaive only. Blood Transfusion's bounce heals the full 18.")
                .Requires(m => Wields(m, GlaiveEncarmine)),

            new UpgradeDef(SanctifiedBlood, UpgradeCategory.Endurance, "Sanctified Blood",
                "Vitae no longer drains."),
            new UpgradeDef(DeepVeins, UpgradeCategory.Endurance, "Deep Veins",
                "+25 maximum health, and your health costs can take you down to 10% health instead of 15%.")
                .Health(DeepVeinsHealth),
            new UpgradeDef(IronHalo, UpgradeCategory.Endurance, "Iron Halo",
                "Once per round, the first time you drop to 20% health you gain a 40 shield for 3s."),
            new UpgradeDef(BloodWard, UpgradeCategory.Endurance, "Blood Ward",
                "Whenever you pay health for an ability, you gain a shield worth half of it for 2s."),

            new UpgradeDef(BrimmingChalice, UpgradeCategory.Ferocity, "Brimming Chalice",
                "Vitae holds 130 instead of 100, and the Exsanguinating Arc deals 20% more damage."),
            new UpgradeDef(OverflowingChalice, UpgradeCategory.Ferocity, "Overflowing Chalice",
                "Overhealing you give fills Vitae at half the rate."),
            new UpgradeDef(Unquenched, UpgradeCategory.Ferocity, "Unquenched",
                "Red Thirst takes you at 30% health instead of 20%."),
            new UpgradeDef(WrathOfBaal, UpgradeCategory.Ferocity, "Wrath of Baal",
                "Blade Hurl's weapon flies back to you in 0.3s instead of 0.6s."),
            new UpgradeDef(DeathFromAbove, UpgradeCategory.Ferocity, "Death from Above",
                "Jump Pack Descent's slam also stuns for 0.5s."),
            new UpgradeDef(CrimsonMomentum, UpgradeCategory.Ferocity, "Crimson Momentum",
                "Crimson Rush (and Blade Hurl) recharge 30% faster: 9s instead of 13s."),
            new UpgradeDef(SanguinaryRite, UpgradeCategory.Ferocity, "Sanguinary Rite",
                "Chalice Offering charges recharge in 9s instead of 12s."),
            new UpgradeDef(DeathMask, UpgradeCategory.Ferocity, "Death Mask",
                "Enemies hit by Chalice of Wrath also panic for 0.8s."),
            new UpgradeDef(SeraphicAscension, UpgradeCategory.Ferocity, "Seraphic Ascension",
                "Wings of the Angel's ascension lasts 7s instead of 5s."),
            new UpgradeDef(BladeOfSanguinius, UpgradeCategory.Ferocity, "Blade of Sanguinius",
                "Blade Hurl ricochets: after its first hit it bounces to the nearest other enemy within 6m, up to 2 times, each bounce dealing half damage and stunning for half as long. It flies home from its last hit."),
        };
    }
}
