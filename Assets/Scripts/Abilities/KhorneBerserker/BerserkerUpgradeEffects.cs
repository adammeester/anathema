using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Applies the Berserker's non-weapon upgrades and the Rage passive plumbing. Added by UpgradeState at runtime.
    // Owner machine only: damage, healing and cooldowns are all resolved there.
    public class BerserkerUpgradeEffects : MonoBehaviour
    {
        Health health;
        UpgradeState upgrades;
        Rage rage;
        AxeRush rush;
        BerserkerCounter counter;
        bool defiantUsed;

        void Awake()
        {
            health = GetComponent<Health>();
            upgrades = GetComponent<UpgradeState>();
            rush = GetComponent<AxeRush>();
            counter = GetComponent<BerserkerCounter>();
            rage = Rage.EnsureOn(gameObject); // the Rage passive is part of the base kit
        }

        void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
        }

        void Update()
        {
            if (health.IsReplica) return;

            if (rush != null) rush.CooldownScale = upgrades.Has(BerserkerUpgrade.GoreRush) ? BerserkerUpgrade.GoreRushCooldownScale : 1f;
            if (counter != null) counter.CooldownScale = upgrades.Has(BerserkerUpgrade.VengefulBlood) ? BerserkerUpgrade.VengefulCooldownScale : 1f;

            bool furious = upgrades.Has(BerserkerUpgrade.BerserkersFury) && !health.IsDead
                           && health.Normalized < BerserkerUpgrade.FuryHealthThreshold;
            rage.ExtraAttackSpeed = furious ? BerserkerUpgrade.FuryAttackSpeed : 0f;
            rage.MovePerStack = upgrades.Has(BerserkerUpgrade.RelentlessPursuit) ? BerserkerUpgrade.PursuitMovePerStack : 0f;

            // Defiant Roar rearms when the next round restores full health.
            if (health.Normalized >= 0.99f) defiantUsed = false;
        }

        void OnDamaged(Health _, float amount, GameObject source)
        {
            if (health.IsReplica || health.IsDead) return;

            if (upgrades.Has(BerserkerUpgrade.FeelNoPain) && amount >= BerserkerUpgrade.FeelNoPainHit)
                health.GrantTemporaryArmor(BerserkerUpgrade.FeelNoPainReduction, BerserkerUpgrade.FeelNoPainSeconds);

            if (upgrades.Has(BerserkerUpgrade.DefiantRoar) && !defiantUsed && health.Normalized < BerserkerUpgrade.DefiantThreshold)
            {
                defiantUsed = true;
                health.HealPermanent(health.Max * BerserkerUpgrade.DefiantHealFraction);
                Enraged.Apply(gameObject, BerserkerUpgrade.DefiantEnrageSeconds);
            }
        }

        // ---- Called by the Berserker's attacks ---------------------------------------------------------------------

        // A melee hit landed for `dealt` damage: build Rage and drink the blood.
        public static void RegisterHit(GameObject berserker, float dealt)
        {
            if (berserker == null) return;

            GainRage(berserker);

            if (UpgradeState.Has(berserker, BerserkerUpgrade.CrimsonCommunion) && berserker.TryGetComponent(out Health health))
                health.Heal(dealt * BerserkerUpgrade.CommunionHealFraction);
        }

        // One Rage stack (with Crimson Momentum's bigger cap and longer window).
        public static void GainRage(GameObject berserker)
        {
            bool momentum = UpgradeState.Has(berserker, BerserkerUpgrade.CrimsonMomentum);
            Rage.Gain(berserker,
                Rage.DefaultMaxStacks + (momentum ? BerserkerUpgrade.MomentumExtraStacks : 0),
                momentum ? BerserkerUpgrade.MomentumWindow : Rage.DefaultWindow);
        }

        // Skulltaker: finishing the wounded hurts more.
        public static float DamageScale(GameObject berserker, Health target)
        {
            if (target != null && target.Normalized < BerserkerUpgrade.SkulltakerThreshold
                && UpgradeState.Has(berserker, BerserkerUpgrade.Skulltaker))
                return 1f + BerserkerUpgrade.SkulltakerBonus;
            return 1f;
        }
    }

    // Which weapon the Berserker is wielding (Armament upgrades). Works on replicas too: the owned mask is synced.
    public enum BerserkerWeapon
    {
        DualChainaxes,
        HeavyChainaxe,
        Eviscerator
    }

    public static class BerserkerWeapons
    {
        public static BerserkerWeapon Of(GameObject berserker)
        {
            if (UpgradeState.Has(berserker, BerserkerUpgrade.HeavyChainaxe)) return BerserkerWeapon.HeavyChainaxe;
            if (UpgradeState.Has(berserker, BerserkerUpgrade.Eviscerator)) return BerserkerWeapon.Eviscerator;
            return BerserkerWeapon.DualChainaxes;
        }

        // Every living enemy whose body lies within `reach` of `origin` and inside the `arc` around `aim`.
        public static List<Health> EnemiesInCone(GameObject attacker, Health self, Vector3 origin, Vector3 aim, float reach, float arc)
        {
            var found = new List<Health>();
            Collider[] colliders = Physics.OverlapSphere(origin + Vector3.up, reach, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in colliders)
            {
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target.Team == self.Team || target.IsDead || found.Contains(target)) continue;

                Vector3 to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(aim, to) > arc * 0.5f) continue;
                found.Add(target);
            }
            return found;
        }
    }
}
