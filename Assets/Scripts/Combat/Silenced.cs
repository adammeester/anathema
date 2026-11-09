using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: while present, the owner can't start any ability (Ability.Update checks IsSilenced).
    // Added to the victim at runtime; re-applying refreshes the duration.
    public class Silenced : MonoBehaviour
    {
        float endTime;

        public static Silenced Apply(GameObject target, float duration)
        {
            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Silence, duration, 0f, Vector3.zero);
                return null;
            }

            if (Enraged.IsEnraged(target)) return null;

            var silenced = target.GetComponent<Silenced>();
            if (silenced == null) silenced = target.AddComponent<Silenced>();
            silenced.endTime = Mathf.Max(silenced.endTime, Time.time + duration);
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
