using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    public enum GreyKnightWeapon
    {
        Halberd,       // the starting Nemesis halberd and storm bolter
        Daemonhammer,
        Psycannon
    }

    // The Grey Knight's upgrade list. The bit numbers are the saved/synced identity of each upgrade, so only ever append
    // new ones; never renumber.
    //
    //   Armament - weapon transformations (one at a time).
    //       Daemonhammer: a two-handed hammer. LMB becomes a heavy sweep, Q a ground-shaking slam; Smite is kept.
    //       Psycannon:    a two-handed cannon. LMB fires an energy shell that explodes on contact or at the end of its
    //                     range; Q becomes a cannon bash. Smite is kept.
    //   Ferocity - Purifier Order, a passive: the helmet turns white and the Grey Knight's attacks set enemies alight.
    public static class GreyKnightUpgrade
    {
        public const int Daemonhammer = 0;
        public const int Psycannon = 1;
        public const int PurifierOrder = 2;

        public const int PaladinOrder = 3;

        public const string WeaponGroup = "weapon";
        public const string OrderGroup = "order";   // the Purifier and Paladin Orders are mutually exclusive

        public static GreyKnightWeapon WeaponOf(GameObject player)
        {
            if (UpgradeState.Has(player, Daemonhammer)) return GreyKnightWeapon.Daemonhammer;
            if (UpgradeState.Has(player, Psycannon)) return GreyKnightWeapon.Psycannon;
            return GreyKnightWeapon.Halberd;
        }

        public static bool HasPaladin(GameObject player) => UpgradeState.Has(player, PaladinOrder);

        public static bool HasPurifier(GameObject player) => UpgradeState.Has(player, PurifierOrder);

        // Purifier Order tunables.
        public const float BurnPerSecond = 6f;
        public const float BurnSeconds = 4f;
        public const float GroundFireRadius = 1.8f;
        public const float GroundFireSeconds = 4f;
        public const float GroundFirePerSecond = 8f;

        // Owner machine only: under the Purifier Order, set `target` alight.
        public static void Ignite(GameObject attacker, Health target)
        {
            if (attacker == null || target == null || target.IsDead || !HasPurifier(attacker)) return;
            if (attacker.TryGetComponent(out Health mine) && mine.IsReplica) return;
            Burning.Apply(target.gameObject, attacker, BurnPerSecond, BurnSeconds);
        }

        // Pick 0 is offered after round 1, pick 1 after round 2, and so on. A weapon already taken (or the Purifier Order)
        // drops out of later sets; an exhausted set simply offers nothing.
        public static readonly int[][][] Sets =
        {
            new[] { new[] { Daemonhammer }, new[] { Psycannon }, new[] { PurifierOrder } },
            new[] { new[] { Daemonhammer }, new[] { Psycannon }, new[] { PaladinOrder, PurifierOrder } },
            new[] { new[] { PurifierOrder }, new[] { PaladinOrder } },
        };

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(Daemonhammer, UpgradeCategory.Armament, "Daemonhammer - The Purge",
                "Two-handed melee. Attack becomes a heavy hammer sweep and Sweep (Q) becomes a ground-shattering slam that stuns. Smite is kept. Replaces the halberd and storm bolter.")
                .InGroup(WeaponGroup),
            new UpgradeDef(Psycannon, UpgradeCategory.Armament, "Psycannon - The Cleanser",
                "Two-handed heavy weapon. Attack fires a ball of psychic energy that explodes on contact or at the end of its range, damaging everything around it; Sweep (Q) becomes the Overcharged Round, a slow-charging shell with a huge blast that panics everything it hits. Smite is kept. Replaces the halberd and storm bolter.")
                .InGroup(WeaponGroup),
            new UpgradeDef(PurifierOrder, UpgradeCategory.Ferocity, "Purifier Order",
                "Your helmet and shoulders turn white. Smite becomes Purifying Flame, a 2s flame-thrower channel with about the same damage as Smite plus burning. Consecrate, Purgatus, Sweep, the Psycannon and the Daemonhammer also set enemies on fire (5 damage per second for 4s); the halberd sweep leaves burning ground, and so does the hammer slam (but not its swings).")
                .InGroup(OrderGroup),
            new UpgradeDef(PaladinOrder, UpgradeCategory.Endurance, "Paladin Order",
                "Your helmet and shoulders turn gold. Sanctified armour: +30 maximum health and 25% less damage from every hit. Martyr's Resolve: once per round, a hit that would kill you leaves you on 1 health and immune for 2s. Cannot be combined with the Purifier Order.")
                .Health(30f).Armour(0.25f).LastStand().InGroup(OrderGroup),
        };
    }
}
