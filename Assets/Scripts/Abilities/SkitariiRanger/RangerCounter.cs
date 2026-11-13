using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // Skitarii Ranger's counter payoff: the negated hit is answered with a half-charged Transuranic shot fired
    // straight back at whoever triggered it, and Disengage is ready again.
    [DefaultExecutionOrder(100)] // keep in step with Counter; not every Unity version inherits the attribute
    public class RangerCounter : Counter
    {
        [Tooltip("Fraction of a full Transuranic charge the return shot carries (0.5 = half the charge duration).")]
        [SerializeField] float chargeFraction = 0.5f;

        protected override void OnCounterSuccess(GameObject attacker)
        {
            var disengage = GetComponent<Disengage>();
            if (disengage != null) disengage.Refresh();

            if (attacker == null) return;
            var shot = GetComponent<TransuranicShot>();
            if (shot == null) return;

            Vector3 toAttacker = attacker.transform.position - transform.position;
            shot.CounterShot(toAttacker, chargeFraction);
        }
    }
}
