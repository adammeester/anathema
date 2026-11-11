using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: while present, the owner can't start any ability (Ability.Update checks IsSilenced).
    // Apply(..., interrupt: true) also cuts off whatever the victim is casting or channelling.
    // Added to the victim at runtime; re-applying refreshes the duration.
    public class Silenced : MonoBehaviour
    {
        float endTime;

        public static Silenced Apply(GameObject target, float duration, bool interrupt = false)
        {
            if (Anathema.Net.NetPlayer.RouteControl(target, Anathema.Net.NetPlayer.StatusKind.Silence, duration, interrupt ? 1f : 0f, Vector3.zero)) return null;

            if (Enraged.IsEnraged(target)) return null;
            duration *= Health.CrowdControlScaleOf(target);

            var silenced = target.GetComponent<Silenced>();
            if (silenced == null) silenced = target.AddComponent<Silenced>();
            silenced.endTime = Mathf.Max(silenced.endTime, Time.time + duration);
            if (interrupt) Anathema.Abilities.Ability.InterruptAll(target);
            return silenced;
        }

        public static bool IsSilenced(GameObject target)
        {
            return target.TryGetComponent(out Silenced _);
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead)) Destroy(this);
        }
    }
}
