using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Player
{
    // Procedural walk cycle for the rigged character models (Art/*.py: Hips, Spine, Thigh_L, Shin_L, Foot_L, ...).
    // Lives on the "Model" object and poses the bones in LateUpdate, so it needs no clips or Animator Controller.
    //
    // The gait follows the real movement direction in world space, so it works with WASD strafing while the body
    // faces the mouse (walk forward, backpedal, side-step and diagonals all fall out of the same maths). The cycle
    // phase advances with distance travelled, which keeps the feet from skating at any speed.
    [DisallowMultipleComponent]
    public class ProceduralWalk : MonoBehaviour
    {
        [Header("Gait")]
        [Tooltip("Distance covered by one full cycle (two steps). Longer = slower leg turnover at the same speed.")]
        [SerializeField] float strideLength = 2.3f;
        [Tooltip("Speed that counts as full intensity. 0 = read PlayerMovement's move speed.")]
        [SerializeField] float referenceSpeed;
        [Tooltip("How quickly the pose reacts to starting, stopping and changing direction.")]
        [SerializeField] float responsiveness = 14f;

        [Header("Legs")]
        [SerializeField, Range(0f, 60f)] float thighSwing = 34f;
        [SerializeField, Range(0f, 90f)] float kneeBend = 62f;
        [Tooltip("How much the feet counter-rotate to stay flat on the ground.")]
        [SerializeField, Range(0f, 1f)] float footLevel = 0.75f;

        [Header("Body")]
        [Tooltip("Vertical hip bounce in metres: lowest when the legs are spread, highest as they pass.")]
        [SerializeField] float bob = 0.045f;
        [SerializeField] float forwardLean = 5f;
        [SerializeField] float pelvisTwist = 5f;
        [SerializeField] float shoulderTwist = 4f;
        [Tooltip("How much the head cancels the torso twist, so the view stays steady.")]
        [SerializeField, Range(0f, 1f)] float headStability = 0.75f;

        [Header("Arms (weapon arm swings less)")]
        [SerializeField] float armSwingLeft = 10f;
        [SerializeField] float armSwingRight = 3f;
        [SerializeField] float coatSway = 14f;

        class Bone
        {
            public Transform t;
            public Quaternion restLocalRotation;
            public Vector3 restLocalPosition;
        }

        Bone hips, spine, head, coatTail;
        Bone thighL, shinL, footL, thighR, shinR, footR;
        Bone armL, armR;
        readonly List<Bone> all = new List<Bone>();

        Vector3 lastPosition;
        Vector3 smoothedVelocity;
        Vector3 direction = Vector3.forward;
        float phase;
        float speedRef = 6.5f;
        bool valid;

        void Awake()
        {
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) byName[t.name] = t;

            hips = Grab(byName, "Hips");
            spine = Grab(byName, "Spine");
            head = Grab(byName, "Head");
            coatTail = Grab(byName, "CoatTail");
            thighL = Grab(byName, "Thigh_L");
            shinL = Grab(byName, "Shin_L");
            footL = Grab(byName, "Foot_L");
            thighR = Grab(byName, "Thigh_R");
            shinR = Grab(byName, "Shin_R");
            footR = Grab(byName, "Foot_R");
            armL = Grab(byName, "UpperArm_L");
            armR = Grab(byName, "UpperArm_R");

            // Primitive fallback model has no skeleton: nothing to animate.
            valid = hips != null && thighL != null && thighR != null;
            enabled = valid;

            var movement = GetComponentInParent<PlayerMovement>();
            if (referenceSpeed > 0f) speedRef = referenceSpeed;
            else if (movement != null) speedRef = movement.MoveSpeed;
        }

        void OnEnable()
        {
            lastPosition = transform.position;
            smoothedVelocity = Vector3.zero;
        }

        void OnDisable()
        {
            if (valid) ResetPose();
        }

        Bone Grab(Dictionary<string, Transform> byName, string name)
        {
            if (!byName.TryGetValue(name, out Transform t)) return null;
            var bone = new Bone { t = t, restLocalRotation = t.localRotation, restLocalPosition = t.localPosition };
            all.Add(bone);
            return bone;
        }

        void ResetPose()
        {
            foreach (Bone b in all)
            {
                b.t.localRotation = b.restLocalRotation;
                b.t.localPosition = b.restLocalPosition;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 step = transform.position - lastPosition;
            lastPosition = transform.position;
            step.y = 0f;
            Vector3 velocity = step / dt;
            if (velocity.magnitude > speedRef * 3f) velocity = Vector3.zero; // teleport, e.g. respawn

            smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-responsiveness * dt));
            float speed = smoothedVelocity.magnitude;
            if (speed > 0.05f) direction = smoothedVelocity / speed; // keep the last heading while fading out

            float intensity = Mathf.Clamp01(speed / speedRef);
            phase = Mathf.Repeat(phase + speed * dt / strideLength * Mathf.PI * 2f, Mathf.PI * 2f);

            Pose(intensity);
        }

        void Pose(float k)
        {
            Vector3 d = direction;
            Vector3 swingAxis = Vector3.Cross(Vector3.down, d); // rotating about this takes "down" toward the move direction
            Vector3 leanAxis = Vector3.Cross(Vector3.up, d);
            float forwardness = Vector3.Dot(d, transform.forward); // +1 walking forward, -1 backpedalling, 0 strafing
            float s = Mathf.Sin(phase);

            // Hips: bounce and a twist that brings the forward-stepping hip along with it.
            if (hips != null)
            {
                float bounce = bob * k * (0.5f * (1f + Mathf.Cos(2f * phase)) - 1f);
                hips.t.position = hips.t.parent.TransformPoint(hips.restLocalPosition) + Vector3.up * bounce;
            }
            Quaternion hipsTwist = Quaternion.AngleAxis(-pelvisTwist * s * forwardness * k, Vector3.up);
            Apply(hips, hipsTwist);

            // Spine: lean into the move and twist against the pelvis.
            Quaternion spineDelta = Quaternion.AngleAxis(forwardLean * k, leanAxis)
                                    * Quaternion.AngleAxis((shoulderTwist + pelvisTwist) * s * forwardness * k, Vector3.up);
            Apply(spine, spineDelta);

            // Head cancels part of the accumulated torso rotation.
            Apply(head, Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(spineDelta * hipsTwist), headStability));

            // Legs: the two sides run half a cycle apart.
            Leg(thighL, shinL, footL, phase, swingAxis, k);
            Leg(thighR, shinR, footR, phase + Mathf.PI, swingAxis, k);

            // Arms swing against the legs. The weapon arm (right) barely moves so the gun stays on target.
            Apply(armL, Quaternion.AngleAxis(-armSwingLeft * s * k, swingAxis));
            Apply(armR, Quaternion.AngleAxis(armSwingRight * s * k, swingAxis));

            // Coat tail trails behind the movement.
            if (coatTail != null)
            {
                float sway = coatSway * k * (1f + 0.35f * Mathf.Sin(2f * phase - 0.8f));
                Apply(coatTail, Quaternion.AngleAxis(-sway, swingAxis));
            }
        }

        void Leg(Bone thigh, Bone shin, Bone foot, float p, Vector3 swingAxis, float k)
        {
            float thighAngle = thighSwing * Mathf.Sin(p) * k;
            // The knee folds the foot back while the leg swings forward, with a slight permanent bend.
            float flex = kneeBend * k * (Mathf.Max(0f, Mathf.Cos(p + 0.6f)) + 0.08f);
            float footAngle = -(thighAngle - flex) * footLevel;

            Apply(thigh, Quaternion.AngleAxis(thighAngle, swingAxis));
            Apply(shin, Quaternion.AngleAxis(-flex, swingAxis));
            Apply(foot, Quaternion.AngleAxis(footAngle, swingAxis));
        }

        // Rotates the bone about its own origin by `worldDelta` on top of whatever it inherits from its parent.
        // Must be called parent-first.
        static void Apply(Bone b, Quaternion worldDelta)
        {
            if (b == null) return;
            b.t.rotation = worldDelta * (b.t.parent.rotation * b.restLocalRotation);
        }
    }
}
