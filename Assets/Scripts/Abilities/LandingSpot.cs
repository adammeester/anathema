using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities
{
    // For abilities that pass over walls (Warp Step, Winged Escape): finds a landing point along a flight path that
    // isn't inside a wall or pillar, backing off from the intended point if it is. Characters don't count as obstacles.
    public static class LandingSpot
    {
        const float Radius = 0.4f;
        const float Step = 0.5f;

        // 'distance' is the intended flight length from 'from' along 'dir'; returns the usable length (0 if nowhere is clear).
        public static float Clear(Transform self, Vector3 from, Vector3 dir, float distance)
        {
            for (float d = distance; d > 0f; d -= Step)
                if (!Blocked(self, from + dir * d)) return d;
            return 0f;
        }

        static bool Blocked(Transform self, Vector3 pos)
        {
            Vector3 a = pos + Vector3.up * (Radius + 0.1f);
            Vector3 b = pos + Vector3.up * (1.6f - Radius);
            foreach (Collider c in Physics.OverlapCapsule(a, b, Radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(self) || c.GetComponentInParent<Health>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
