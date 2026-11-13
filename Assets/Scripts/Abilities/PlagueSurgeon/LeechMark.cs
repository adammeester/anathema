using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Status effect for Leech Draught: an enemy whose Festering lets the surgeon's allies drink from it. Every direct
    // hit an ally of the surgeon lands on them heals that ally for a share of the damage (the surgeon's own hits and
    // damage over time don't count).
    // Held on the copy where damage is resolved (the server online). Festering lives on the surgeon's machine, so it
    // keeps this refreshed from there for as long as the rot lasts; the mark lapses shortly after the rot does.
    public class LeechMark : MonoBehaviour
    {
        public const float RefreshInterval = 0.25f;

        GameObject surgeon;
        Health health;
        float endTime;

        public static void Refresh(GameObject target, GameObject surgeon, float seconds)
        {
            if (target == null || surgeon == null || seconds <= 0f) return;
            if (!target.TryGetComponent(out Health holder) || holder.IsDead || holder.Structure) return;

            if (!holder.IsAuthority)
            {
                if (holder.Net != null)
                    holder.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Leech, seconds, 0f, Vector3.zero, surgeon);
                return;
            }

            var mark = target.GetComponent<LeechMark>();
            if (mark == null) mark = target.AddComponent<LeechMark>();
            mark.surgeon = surgeon;
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
            if (health == null || health.IsDead || surgeon == null || Time.time >= endTime) Destroy(this);
        }

        void OnDamaged(Health _, float amount, GameObject source)
        {
            if (health.LastHitDot || amount <= 0f || source == null || surgeon == null || source == surgeon) return;
            if (!source.TryGetComponent(out Health attacker) || attacker.IsDead) return;
            if (!surgeon.TryGetComponent(out Health surgeonHealth) || attacker.Team != surgeonHealth.Team) return;
            if (attacker.Team == health.Team) return;

            Healing.Apply(attacker, amount * PlagueSurgeonUpgrade.LeechFraction, surgeon);
        }
    }
}
