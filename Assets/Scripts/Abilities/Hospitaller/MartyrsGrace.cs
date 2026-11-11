using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Space: Martyr's Grace, the Hospitaller's counter. The caught hit is negated; she and every ally near her are
    // healed, and the attacker is struck by a flash of light that silences them for a moment.
    [DefaultExecutionOrder(100)] // keep in step with Counter
    public class MartyrsGrace : Counter
    {
        [SerializeField] float heal = 15f;
        [SerializeField] float healRadius = 6f;
        [SerializeField] float smiteDamage = 6f;
        [SerializeField] float silenceSeconds = 1.2f;

        public override string DisplayName => "Martyr's Grace";

        protected override void OnCounterSuccess(GameObject attacker)
        {
            HospitallerFx.Burst(transform.position, healRadius, new Color(1f, 0.9f, 0.5f, 0.8f), 0.5f, 0.5f);
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure || h.Team != Health.Team) continue;
                Vector3 d = h.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= healRadius) Healing.Apply(h, heal, gameObject);
            }

            if (attacker == null) return;
            Health victim = attacker.GetComponent<Health>();
            if (victim != null && !victim.IsDead && victim.Team != Health.Team)
                victim.Strike(Hit.Direct(BlessedSuffering.Scale(Health, smiteDamage)).WithSilence(silenceSeconds, true), gameObject);
        }
    }
}
