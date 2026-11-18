using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: slows movement for a while. Unlike Maimed it leaves no trail. Re-applying refreshes the duration and
    // keeps the stronger slow. PlayerMovement and DummyMover read SpeedMultiplier (use Multiplier(target) to be safe).
    public class Slowed : MonoBehaviour
    {
        float endTime;
        float slow;
        // A fading snare (`fade`): the slow wears off evenly over the whole duration instead of holding, e.g. a full
        // stop that eases back to normal speed over a second (the Sorcerer's Sinking Sands).
        float fadeStart;
        float fadeDuration;
        float fadeSlow;

        public float SpeedMultiplier
        {
            get
            {
                float fading = 0f;
                if (fadeDuration > 0f && Time.time < fadeStart + fadeDuration)
                    fading = fadeSlow * (1f - (Time.time - fadeStart) / fadeDuration);
                return 1f - Mathf.Max(Time.time < endTime ? slow : 0f, fading);
            }
        }

        public static float Multiplier(GameObject target)
        {
            return target.TryGetComponent(out Slowed s) ? s.SpeedMultiplier : 1f;
        }

        public static Slowed Apply(GameObject target, float duration, float slowFraction, bool fade = false)
        {
            if (Anathema.Net.NetPlayer.RouteControl(target, Anathema.Net.NetPlayer.StatusKind.Slow, duration, slowFraction, new Vector3(fade ? 1f : 0f, 0f, 0f))) return null;

            if (Enraged.IsEnraged(target) || Unstoppable.Is(target)) return null;
            duration *= Health.CrowdControlScaleOf(target);

            var slowed = target.GetComponent<Slowed>();
            if (slowed == null)
            {
                slowed = target.AddComponent<Slowed>();
                slowed.slow = 0f;
            }
            if (fade)
            {
                slowed.fadeStart = Time.time;
                slowed.fadeDuration = Mathf.Max(0.01f, duration);
                slowed.fadeSlow = Mathf.Clamp01(slowFraction);
                slowed.endTime = Mathf.Max(slowed.endTime, Time.time);
                return slowed;
            }
            slowed.slow = Mathf.Max(Time.time < slowed.endTime ? slowed.slow : 0f, Mathf.Clamp01(slowFraction));
            slowed.endTime = Mathf.Max(slowed.endTime, Time.time + duration);
            return slowed;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if ((Time.time >= endTime && Time.time >= fadeStart + fadeDuration) || (health != null && health.IsDead)) Destroy(this);
        }
    }
}
