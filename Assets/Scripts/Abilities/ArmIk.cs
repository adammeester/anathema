using UnityEngine;

namespace Anathema.Abilities
{
    // Two-bone IK for a rigged arm (upper arm -> forearm -> hand), working in world space so the rig's bone roll does
    // not matter. Build it while the model is at rest (it measures the bone lengths and directions then), and call
    // Solve() each LateUpdate after the parent bones (spine) have been posed.
    public class ArmIk
    {
        readonly Transform upper;
        readonly Transform fore;
        readonly float upperLength;
        readonly float foreLength;
        readonly Vector3 upperDir; // upper -> fore, in the upper bone's local space
        readonly Vector3 foreDir;  // fore -> hand, in the fore bone's local space

        public float Reach => upperLength + foreLength;
        public Transform Hand { get; }

        public ArmIk(Transform upper, Transform fore, Transform hand)
        {
            this.upper = upper;
            this.fore = fore;
            Hand = hand;

            Vector3 u = fore.position - upper.position;
            Vector3 f = hand.position - fore.position;
            upperLength = u.magnitude;
            foreLength = f.magnitude;
            upperDir = Quaternion.Inverse(upper.rotation) * u.normalized;
            foreDir = Quaternion.Inverse(fore.rotation) * f.normalized;
        }

        // Reaches the wrist toward `target`, with the elbow bending toward `pole`. `weight` blends from the pose the
        // bones are in now (0) to the solved pose (1). Returns where the wrist ends up (clamped to the arm's reach).
        public Vector3 Solve(Vector3 target, Vector3 pole, float weight)
        {
            Quaternion upper0 = upper.localRotation;
            Quaternion fore0 = fore.localRotation;

            Vector3 shoulder = upper.position;
            Vector3 toTarget = target - shoulder;
            float dist = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - foreLength) + 0.01f, Reach * 0.999f);
            Vector3 axis = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector3.forward;
            Vector3 wrist = shoulder + axis * dist;

            // Law of cosines: how far along the shoulder->wrist line the elbow sits, and how far off it.
            float along = (dist * dist + upperLength * upperLength - foreLength * foreLength) / (2f * dist);
            float off = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 bend = pole - Vector3.Dot(pole, axis) * axis;
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.Cross(axis, Vector3.up).normalized;
            Vector3 elbow = shoulder + axis * along + bend * off;

            upper.rotation = Quaternion.FromToRotation(upper.rotation * upperDir, (elbow - shoulder).normalized) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(fore.rotation * foreDir, (wrist - fore.position).normalized) * fore.rotation;

            if (weight < 1f)
            {
                upper.localRotation = Quaternion.Slerp(upper0, upper.localRotation, weight);
                fore.localRotation = Quaternion.Slerp(fore0, fore.localRotation, weight);
            }

            return wrist;
        }
    }
}
