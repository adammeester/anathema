using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: slows movement for a while. Unlike Maimed it leaves no trail. Re-applying refreshes the duration and
    // keeps the stronger slow. PlayerMovement and DummyMover read SpeedMultiplier (use Multiplier(target) to be safe).
    public class Slowed : MonoBehaviour
    {
        float endTime;
        float slow;

        public float SpeedMultiplier => 1f - slow;

        public static float Multiplier(GameObject target)
        {
            return target.TryGetComponent(out Slowed s) ? s.SpeedMultiplier : 1f;
        }

        public static Slowed Apply(GameObject target, float duration, float slowFraction)
        {
            // Online: a remote player's status is applied on their own machine.
            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Slow, duration, slowFraction, Vector3.zero);
                return null;
            }

            if (Enraged.IsEnraged(target)) return null;
            duration *= Health.CrowdControlScaleOf(target);

            var slowed = target.GetComponent<Slowed>();
            if (slowed == null)
            {
                slowed = target.AddComponent<Slowed>();
                slowed.slow = 0f;
            }
            slowed.slow = Mathf.Max(Time.time < slowed.endTime ? slowed.slow : 0f, Mathf.Clamp01(slowFraction));
            slowed.endTime = Mathf.Max(slowed.endTime, Time.time + duration);
            return slowed;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead)) Destroy(this);
        }
    }
}
