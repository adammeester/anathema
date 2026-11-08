using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: slows movement and leaves a trail of blood while it lasts. Added to the victim at runtime by
    // whatever applies it (Maimed.Apply); re-applying refreshes the duration. PlayerMovement reads SpeedMultiplier,
    // and DummyMover does the same, so anything without a mover just bleeds.
    public class Maimed : MonoBehaviour
    {
        const float DripInterval = 0.2f;
        const float TrailStep = 0.4f;
        const float FloorTop = 0.14f;

        float endTime;
        float slow;
        float nextDripTime;
        Vector3 lastDropPosition;

        public float SpeedMultiplier => 1f - slow;

        public static Maimed Apply(GameObject target, float duration, float slowFraction)
        {
            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Maim, duration, slowFraction, Vector3.zero);
                return null;
            }

            var maimed = target.GetComponent<Maimed>();
            if (maimed == null) maimed = target.AddComponent<Maimed>();
            maimed.endTime = Time.time + duration;
            maimed.slow = Mathf.Clamp01(slowFraction);
            return maimed;
        }

        void OnEnable()
        {
            lastDropPosition = transform.position;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            Vector3 pos = transform.position;
            Vector3 flat = pos - lastDropPosition;
            flat.y = 0f;

            // A drop every step of travel, plus a slower drip while standing still.
            if (flat.magnitude >= TrailStep || Time.time >= nextDripTime)
            {
                BloodDrop.Spawn(GroundPoint());
                lastDropPosition = pos;
                nextDripTime = Time.time + DripInterval;
            }
        }

        // Feet position on the floor, ignoring the victim's own colliders.
        Vector3 GroundPoint()
        {
            // The arena's visible floor discs are collider-less and stack up to ~0.12 above the floor collider, so a
            // raycast lands underneath them; keep the blood just above the highest disc.
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            float y = FloorTop;

            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance;
                y = Mathf.Max(FloorTop, hit.point.y + 0.01f);
            }
            return new Vector3(transform.position.x, y, transform.position.z);
        }
    }
}
