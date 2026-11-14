using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Status effect for Martyr's Due: an enemy held by the Hospitaller's Penance. Every direct hit one of her allies lands
    // on them heals that ally for a share of the damage (her own hits and damage over time don't count).
    // Held on the copy where the enemy's damage is resolved (the server online). The tether refreshes it from that same
    // copy for as long as Penance holds, so it needs no network message; it lapses a moment after Penance ends.
    public class MartyrsDueMark : MonoBehaviour
    {
        Health hospitaller;
        Health health;
        float endTime;

        public static void Refresh(Health target, Health hospitaller, float seconds)
        {
            if (target == null || hospitaller == null || seconds <= 0f || !target.IsAuthority) return;
            if (target.IsDead || target.Structure) return;

            var mark = target.GetComponent<MartyrsDueMark>();
            if (mark == null) mark = target.gameObject.AddComponent<MartyrsDueMark>();
            mark.hospitaller = hospitaller;
            mark.endTime = Mathf.Max(mark.endTime, Time.time + seconds);
        }

        void OnEnable()
        {
            health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        void Update()
        {
            if (health == null || health.IsDead || hospitaller == null || Time.time >= endTime) Destroy(this);
        }

        void OnDamaged(Health _, float amount, GameObject source)
        {
            if (health.LastHitDot || amount <= 0f || source == null || hospitaller == null) return;
            if (source == hospitaller.gameObject) return;
            if (!source.TryGetComponent(out Health attacker) || attacker.IsDead) return;
            if (attacker.Team != hospitaller.Team || attacker.Team == health.Team) return;

            Healing.Apply(attacker, amount * HospitallerUpgrade.MartyrsDueFraction, hospitaller.gameObject);
        }
    }
}
