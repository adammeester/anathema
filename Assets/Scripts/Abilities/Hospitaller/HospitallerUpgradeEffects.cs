using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Applies the Hospitaller's upgrades that are not part of one ability's own code. Added by UpgradeState at runtime.
    //   Every copy: Bearer of Burdens (how long her healable health lingers; only the authority's decay matters).
    //   Owner machine: Tithes (her energy lives here).
    //   Where damage and healing are resolved (the server online): Overflowing Grace and Last Rites.
    // Everything else reads the upgrade mask in the ability it changes (HospitallerUpgrade has the numbers).
    public class HospitallerUpgradeEffects : MonoBehaviour
    {
        Health health;
        UpgradeState upgrades;
        Energy energy;
        SanctusPulse sanctus;

        void Awake()
        {
            health = GetComponent<Health>();
            upgrades = GetComponent<UpgradeState>();
            energy = GetComponent<Energy>();
            sanctus = GetComponent<SanctusPulse>();
        }

        void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            health.LethalSave = OnLethalHit;
            Health.HealResolved += OnAnyHeal;
            Healing.Applying += OnHealGiven;
        }

        void OnDisable()
        {
            if (health.LethalSave == (System.Func<GameObject, bool>)OnLethalHit) health.LethalSave = null;
            Health.HealResolved -= OnAnyHeal;
            Healing.Applying -= OnHealGiven;
        }

        bool Has(int bit) => upgrades != null && upgrades.Has(bit);

        void Update()
        {
            health.RecoverableLinger = Has(HospitallerUpgrade.BearerOfBurdens) ? HospitallerUpgrade.BearerLinger : 1f;
        }

        // Last Rites: once per round (Health counts that) the killing blow leaves her at 1 health, and a free Sanctus
        // Pulse lands at her feet. It goes off after the usual short delay, by which time the blow has landed and its
        // damage is there to be healed back.
        bool OnLethalHit(GameObject source)
        {
            if (!Has(HospitallerUpgrade.LastRites)) return false;
            HospitallerFx.Burst(transform.position, 1.8f, new Color(1f, 0.95f, 0.7f, 0.9f), 0.5f, 1.4f);
            if (sanctus != null) sanctus.FreePulse(transform.position);
            return true;
        }

        // Tithes: healing she gives an ally (as much of it as they can take) becomes her energy.
        void OnHealGiven(Health target, float amount, GameObject healer)
        {
            if (healer != gameObject || health.IsReplica || energy == null) return;
            if (target == null || target == health || target.IsDead || target.Team != health.Team) return;
            if (!Has(HospitallerUpgrade.Tithes)) return;

            float landed = Mathf.Min(amount, target.Recoverable);
            if (landed > 0f) energy.Gain(landed * HospitallerUpgrade.TithesEnergyPerHeal);
        }

        // Overflowing Grace: healing she gave that the target had no room for becomes a short barrier on them.
        void OnAnyHeal(Health target, float requested, float healed, GameObject healer)
        {
            if (healer != gameObject || target == null || target.IsDead || !target.IsAuthority) return;
            if (!Has(HospitallerUpgrade.OverflowingGrace) || target.Team != health.Team) return;

            float excess = requested - healed; // `requested` already has any healing cut applied
            if (excess <= 0.01f) return;
            Barrier.Grant(target.gameObject, excess, HospitallerUpgrade.OverflowSeconds, HospitallerUpgrade.OverflowCap, gameObject);
        }
    }
}
