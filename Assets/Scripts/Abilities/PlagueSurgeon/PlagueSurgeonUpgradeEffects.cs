using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Applies the Plague Surgeon's upgrades that are not part of one ability's own code. Added by UpgradeState at runtime.
    //   Owner machine: cooldown changes (Chitinous Wings).
    //   Where damage and healing are resolved (the server online): Rotting Hide and Overdose.
    // Everything else reads the upgrade mask in the ability it changes (PlagueSurgeonUpgrade has the numbers).
    public class PlagueSurgeonUpgradeEffects : MonoBehaviour
    {
        Health health;
        UpgradeState upgrades;
        WingedEscape escape;

        void Awake()
        {
            health = GetComponent<Health>();
            upgrades = GetComponent<UpgradeState>();
            escape = GetComponent<WingedEscape>();
        }

        void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            Health.HealResolved += OnAnyHeal;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            Health.HealResolved -= OnAnyHeal;
        }

        void Update()
        {
            if (health.IsReplica || upgrades == null) return;
            if (escape != null)
                escape.CooldownScale = upgrades.Has(PlagueSurgeonUpgrade.ChitinousWings) ? PlagueSurgeonUpgrade.WingsCooldownScale : 1f;
        }

        // Rotting Hide: a direct hit from an enemy standing next to him leaves them festering.
        void OnDamaged(Health _, float amount, GameObject source)
        {
            if (!health.IsAuthority || upgrades == null || !upgrades.Has(PlagueSurgeonUpgrade.RottingHide)) return;
            if (health.LastHitAoe || health.LastHitDot || source == null || source == gameObject) return;
            if (!source.TryGetComponent(out Health attacker) || attacker.IsDead || attacker.Team == health.Team) return;

            Vector3 offset = source.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > PlagueSurgeonUpgrade.RottingHideReach) return;

            // His own energy only counts on his own machine (offline, or a host playing him).
            Energy energy = health.IsReplica ? null : GetComponent<Energy>();
            Festering.Apply(source, gameObject, energy, PlagueSurgeonUpgrade.RottingHideSeconds, PlagueSurgeonUpgrade.RottingHideDamagePerSecond);
        }

        // Overdose: healing this surgeon gave that the target had no room for becomes a short barrier on them.
        void OnAnyHeal(Health target, float requested, float healed, GameObject healer)
        {
            if (healer != gameObject || target == null || target.IsDead || !target.IsAuthority) return;
            if (upgrades == null || !upgrades.Has(PlagueSurgeonUpgrade.Overdose)) return;
            if (target.Team != health.Team) return;

            float excess = requested - healed;
            if (excess <= 0.01f) return;
            Barrier.Grant(target.gameObject, excess, PlagueSurgeonUpgrade.OverdoseSeconds, PlagueSurgeonUpgrade.OverdoseCap, gameObject);
        }

        // ---- Helpers for the abilities -----------------------------------------------------------------------------

        public static bool Has(GameObject surgeon, int bit) => UpgradeState.Has(surgeon, bit);

        // How many Festering stacks a target can hold from this surgeon's rot.
        public static int MaxFesteringStacks(GameObject surgeon) =>
            Has(surgeon, PlagueSurgeonUpgrade.Virulence) ? PlagueSurgeonUpgrade.VirulenceMaxStacks : Festering.MaxStacks;
    }
}
