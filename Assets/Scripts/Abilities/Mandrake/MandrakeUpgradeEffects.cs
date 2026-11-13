using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // Applies the Mandrake's upgrades that are not part of one ability's own code. Added by UpgradeState at runtime.
    //   Owner machine: Veilwalker (his stealth lives there; the heal is sent to the server).
    //   Where damage is resolved (the server online, which holds a copy of every Mandrake): Crippling Toxin (poison
    //   lives there) and the Balefire burst upgrades (Witchfire Venom, Spreading Flame, Smothering Fire).
    // Everything else reads the upgrade mask in the ability it changes (MandrakeUpgrade has the numbers).
    public class MandrakeUpgradeEffects : MonoBehaviour
    {
        const float VeilwalkerInterval = 0.5f;

        static readonly Collider[] spreadBuffer = new Collider[32];

        Health health;
        UpgradeState upgrades;
        float nextVeilHeal;

        void Awake()
        {
            health = GetComponent<Health>();
            upgrades = GetComponent<UpgradeState>();
        }

        void OnEnable()
        {
            Poisoned.Applied += OnPoison;
            Poisoned.Ticked += OnPoison;
            Balefire.Burst += OnBalefireBurst;
        }

        void OnDisable()
        {
            Poisoned.Applied -= OnPoison;
            Poisoned.Ticked -= OnPoison;
            Balefire.Burst -= OnBalefireBurst;
        }

        public static bool Has(GameObject mandrake, int bit) => UpgradeState.Has(mandrake, bit);

        void Update()
        {
            if (health == null || health.IsReplica || health.IsDead || upgrades == null) return;

            // Veilwalker: a steady trickle of healing while hidden.
            if (!upgrades.Has(MandrakeUpgrade.Veilwalker) || !Stealthed.IsStealthed(gameObject))
            {
                nextVeilHeal = Time.time + VeilwalkerInterval;
                return;
            }
            if (Time.time < nextVeilHeal) return;
            nextVeilHeal += VeilwalkerInterval;
            health.Heal(MandrakeUpgrade.VeilwalkerHealPerSecond * VeilwalkerInterval, gameObject);
        }

        // Authority: Crippling Toxin. Every time this Mandrake's poison is laid or ticks, the victim is slowed by its
        // current stack count. The slow is short and topped up by each tick, so it fades soon after the poison ends
        // or bursts.
        void OnPoison(Health victim, GameObject source)
        {
            if (source != gameObject || victim == null || victim.IsDead || upgrades == null) return;
            if (!upgrades.Has(MandrakeUpgrade.CripplingToxin)) return;
            if (!victim.TryGetComponent(out Poisoned poison) || poison.Stacks <= 0) return;

            int stacks = Mathf.Min(poison.Stacks, MandrakeUpgrade.CripplingMaxStacks);
            Slowed.Apply(victim.gameObject, MandrakeUpgrade.CripplingRefreshSeconds, stacks * MandrakeUpgrade.CripplingSlowPerStack);
        }

        // Authority: a balefire this Mandrake lit has just burst on `victim`.
        void OnBalefireBurst(Health victim, GameObject source, float damagePerSecond, float duration)
        {
            if (source != gameObject || victim == null || upgrades == null) return;

            // Smothering Fire: a deeper healing cut (CutHealing keeps the stronger share).
            if (!victim.IsDead && upgrades.Has(MandrakeUpgrade.SmotheringFire))
                victim.CutHealing(MandrakeUpgrade.SmotheringHealingCut, Balefire.BurstHealingCutSeconds);

            // Witchfire Venom: the burst sets the poison off too.
            if (!victim.IsDead && upgrades.Has(MandrakeUpgrade.WitchfireVenom))
                Poisoned.DetonateNow(victim.gameObject);

            // Spreading Flame: nearby enemies catch a weaker balefire (lit by this Mandrake, so his hits burst it too).
            if (upgrades.Has(MandrakeUpgrade.SpreadingFlame) && health != null)
            {
                float spreadDps = damagePerSecond * MandrakeUpgrade.SpreadingFlameStrength;
                Vector3 at = victim.transform.position + Vector3.up;
                int count = Physics.OverlapSphereNonAlloc(at, MandrakeUpgrade.SpreadingFlameRadius, spreadBuffer, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Health other = spreadBuffer[i].GetComponentInParent<Health>();
                    if (other == null || other == victim || other == health || other.IsDead || other.Structure || other.Team == health.Team) continue;
                    if (Balefire.IsBurningForReal(other.gameObject)) continue; // don't weaken or refresh a burn already going
                    Balefire.Apply(other.gameObject, gameObject, spreadDps, duration);
                }
            }
        }
    }
}
