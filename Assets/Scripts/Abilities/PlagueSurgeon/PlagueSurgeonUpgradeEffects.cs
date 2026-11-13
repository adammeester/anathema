using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Applies the Plague Surgeon's upgrades that are not part of one ability's own code. Added by UpgradeState at runtime.
    //   Owner machine: cooldown changes (Chitinous Wings), Hive Shell (his flies live here; the armour is sent to the
    //   server) and Grateful Patients (his energy lives here).
    //   Where damage and healing are resolved (the server online): Rotting Hide, Overdose and Sloughing Flesh.
    // Everything else reads the upgrade mask in the ability it changes (PlagueSurgeonUpgrade has the numbers).
    public class PlagueSurgeonUpgradeEffects : MonoBehaviour
    {
        Health health;
        UpgradeState upgrades;
        WingedEscape escape;
        Energy energy;
        float nextHiveShell;

        void Awake()
        {
            health = GetComponent<Health>();
            upgrades = GetComponent<UpgradeState>();
            escape = GetComponent<WingedEscape>();
            energy = GetComponent<Energy>();
        }

        void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            health.LethalSave = OnLethalHit;
            Health.HealResolved += OnAnyHeal;
            Healing.Applying += OnHealGiven;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            if (health.LethalSave == (System.Func<GameObject, bool>)OnLethalHit) health.LethalSave = null;
            Health.HealResolved -= OnAnyHeal;
            Healing.Applying -= OnHealGiven;
        }

        void Update()
        {
            if (health.IsReplica || upgrades == null) return;
            if (escape != null)
                escape.CooldownScale = upgrades.Has(PlagueSurgeonUpgrade.ChitinousWings) ? PlagueSurgeonUpgrade.WingsCooldownScale : 1f;

            // Hive Shell: a short armour window, topped up while a fly is tending an ally.
            if (!health.IsDead && Time.time >= nextHiveShell && upgrades.Has(PlagueSurgeonUpgrade.HiveShell)
                && Infestation.IsTendingFor(gameObject))
            {
                nextHiveShell = Time.time + LeechMark.RefreshInterval;
                if (health.IsAuthority) health.GrantTemporaryArmor(PlagueSurgeonUpgrade.HiveShellReduction, PlagueSurgeonUpgrade.HiveShellSeconds);
                else if (health.Net != null)
                    health.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Armor, PlagueSurgeonUpgrade.HiveShellReduction,
                        PlagueSurgeonUpgrade.HiveShellSeconds, Vector3.zero, gameObject);
            }
        }

        // Sloughing Flesh: once per round the killing blow is shrugged off at 1 health inside a half-strength ward.
        bool OnLethalHit(GameObject source)
        {
            if (upgrades == null || !upgrades.Has(PlagueSurgeonUpgrade.SloughingFlesh)) return false;
            PlagueWard.Apply(gameObject, gameObject, PlagueSurgeonUpgrade.SloughWardStrength, PlagueSurgeonUpgrade.SloughWardSeconds);
            return true;
        }

        // Grateful Patients: healing this surgeon gives an ally (as much of it as they can take) becomes his energy.
        void OnHealGiven(Health target, float amount, GameObject healer)
        {
            if (healer != gameObject || health.IsReplica || energy == null || upgrades == null) return;
            if (target == null || target == health || target.IsDead || target.Team != health.Team) return;
            if (!upgrades.Has(PlagueSurgeonUpgrade.GratefulPatients)) return;

            float landed = Mathf.Min(amount, target.Recoverable);
            if (landed > 0f) energy.Gain(landed * PlagueSurgeonUpgrade.GratefulEnergyPerHeal);
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
        // Lays one stack of the surgeon's Plague Spit rot (its duration and strength) on `target`.
        public static void ApplySpitRot(GameObject surgeon, Health target, Energy surgeonEnergy)
        {
            if (surgeon == null || target == null || target.IsDead) return;
            if (!surgeon.TryGetComponent(out PlagueSpit spit)) return;
            Festering.Apply(target.gameObject, surgeon, surgeonEnergy, spit.RotDuration, spit.RotDamagePerSecond);
        }

        public static int MaxFesteringStacks(GameObject surgeon) =>
            Has(surgeon, PlagueSurgeonUpgrade.Virulence) ? PlagueSurgeonUpgrade.VirulenceMaxStacks : Festering.MaxStacks;
    }
}
