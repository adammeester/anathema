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
            // Defiant Roar is once per round: it rearms in the prep phase before each round (healing back to full
            // mid-round must not rearm it). It fires where damage is resolved (see OnDamaged), so it rearms there.
            if (health.IsAuthority && Anathema.Net.NetGame.InPrep) defiantUsed = false;

            // Relentless Pursuit's speed per Rage stack: every copy needs it, since the server simulates movement too.
            rage.MovePerStack = upgrades.Has(BerserkerUpgrade.RelentlessPursuit) ? BerserkerUpgrade.PursuitMovePerStack : 0f;

            if (health.IsReplica) return;

            if (rush != null) rush.CooldownScale = upgrades.Has(BerserkerUpgrade.GoreRush) ? BerserkerUpgrade.GoreRushCooldownScale : 1f;
            if (counter != null) counter.CooldownScale = upgrades.Has(BerserkerUpgrade.VengefulBlood) ? BerserkerUpgrade.VengefulCooldownScale : 1f;

            bool furious = upgrades.Has(BerserkerUpgrade.BerserkersFury) && !health.IsDead
                           && health.Normalized < BerserkerUpgrade.FuryHealthThreshold;
            // Rage is the dual chainaxes' passive; the Eviscerator and Heavy Chainaxe replace it with their own.
            rage.Suppressed = BerserkerWeapons.Of(gameObject) != BerserkerWeapon.DualChainaxes;
            rage.ExtraAttackSpeed = furious ? BerserkerUpgrade.FuryAttackSpeed : 0f;
        }

        // Runs where damage is resolved (the server's copy online): Feel No Pain's armour must be there to count, and
        // Defiant Roar's heal and enrage are decided there (the enrage is forwarded to the Berserker's own machine).
        void OnDamaged(Health _, float amount, GameObject source)
        {
            if (!health.IsAuthority || health.IsDead) return;

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

        // The Eviscerator's armour piercing / shield breaking: its base value, or Whetted Edge's stronger one.
        public static float Pierce(GameObject berserker, float basePierce)
        {
            return UpgradeState.Has(berserker, BerserkerUpgrade.WhettedEdge) ? BerserkerUpgrade.WhettedPierce : basePierce;
        }

        public static float ShieldBreak(GameObject berserker, float baseShieldBreak)
        {
            return UpgradeState.Has(berserker, BerserkerUpgrade.WhettedEdge) ? BerserkerUpgrade.WhettedShieldBreak : baseShieldBreak;
        }

        // Hamstring and Gore Rush leave the target bleeding (only if the hit lands).
        public static Hit WithUpgradeBleed(Hit hit)
        {
            return hit.WithBleed(BerserkerUpgrade.UpgradeBleedPerSecond, BerserkerUpgrade.UpgradeBleedSeconds);
        }

        // What every landed Berserker melee hit does for its owner: energy, Rage, Crimson Communion and the blood spray.
        public static System.Action<bool> OnMeleeResult(GameObject berserker, Energy energy, float energyGain, float dealt,
            Health target, Vector3 direction, float bloodScale, bool squelchy = false)
        {
            return landed =>
            {
                if (!landed) return;
                if (energy != null) energy.Gain(energyGain);
                RegisterHit(berserker, dealt);
                MeleeSound.ChainImpact();
                if (target != null) BloodImpact.Play(target.transform.position + Vector3.up, direction, bloodScale, squelchy);
            };
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
        // The returned list and the collider buffer are reused between calls (no allocation per swing), so enumerate
        // the result straight away and do not keep it.
        static readonly Collider[] hitBuffer = new Collider[64];
        static readonly List<Health> foundBuffer = new List<Health>();

        public static List<Health> EnemiesInCone(GameObject attacker, Health self, Vector3 origin, Vector3 aim, float reach, float arc)
        {
            var found = foundBuffer;
            found.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin + Vector3.up, reach, hitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = hitBuffer[i];
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
