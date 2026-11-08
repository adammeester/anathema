using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Arm poses for the Plague Surgeon's abilities. The model has no animation clips, so the arms are solved with IK on
    // top of the walk cycle (which is why this runs after ProceduralWalk): each ability asks for a pose, which fades in
    // quickly, holds while the ability keeps asking, then fades back to the walk. The legs are never touched.
    //   Spit    - the right arm (drill) comes up and points down the aim line, kicking back on every glob.
    //   Throw   - the left arm (cleaver) thrusts out to loose the fly.
    //   Conjure - both arms raised and spread, for the cloud and the ward.
    //   Wings   - both arms swept out and back, for the winged escape.
    // Works on replicas too, because the abilities replay on the remote machine and call the same methods.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes both arms every LateUpdate
    public class PlagueSurgeonPose : MonoBehaviour
    {
        enum Shape
        {
            Point,
            Raise,
            Wing
        }

        class Arm
        {
            public ArmIk Ik;
            public Transform Shoulder;
            public float Outward; // +1 if the arm's outside is along transform.right, else -1
            public Shape Shape;
            public float Start;
            public float Until;
            public float Recoil;
        }

        const float FadeIn = 0.08f;
        const float FadeOut = 0.2f;

        Arm left;
        Arm right;
        MouseAim aim;

        // The pose component for `character`, added on first use so older prefabs get it too.
        public static PlagueSurgeonPose For(GameObject character)
        {
            var pose = character.GetComponent<PlagueSurgeonPose>();
            if (pose == null) pose = character.AddComponent<PlagueSurgeonPose>();
            return pose;
        }

        void Awake()
        {
            aim = GetComponent<MouseAim>();
            Transform model = transform.Find("Model");
            if (model == null)
            {
                enabled = false;
                return;
            }

            left = BuildArm(model, "L");
            right = BuildArm(model, "R");
            if (left != null && right != null)
            {
                float sign = Mathf.Sign(Vector3.Dot(left.Shoulder.position - right.Shoulder.position, transform.right));
                left.Outward = sign;
                right.Outward = -sign;
            }
            else
            {
                left = null;
                right = null;
                enabled = false; // fallback model without the rig
            }
        }

        static Arm BuildArm(Transform model, string side)
        {
            Transform upper = null, fore = null, hand = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "UpperArm_" + side && upper == null) upper = t;
                else if (t.name == "Forearm_" + side && fore == null) fore = t;
                else if (t.name == "Hand_" + side && hand == null) hand = t;
            }

            if (upper == null || fore == null || hand == null) return null;
            return new Arm { Ik = new ArmIk(upper, fore, hand), Shoulder = upper };
        }

        // Where a glob leaves the spitting hand when aiming along `dir`; `fallback` if the model has no rig.
        public Vector3 MuzzlePosition(Vector3 dir, Vector3 fallback)
        {
            if (right == null) return fallback;
            return right.Shoulder.position + dir * (right.Ik.Reach * 0.95f) + Vector3.down * (right.Ik.Reach * 0.3f);
        }

        public void Spit() => Hold(right, Shape.Point, 0.55f, true);

        public void Throw() => Hold(left, Shape.Point, 0.45f, true);

        public void Conjure(float duration)
        {
            Hold(left, Shape.Raise, duration, false);
            Hold(right, Shape.Raise, duration, false);
        }

        public void Wings(float duration)
        {
            Hold(left, Shape.Wing, duration, false);
            Hold(right, Shape.Wing, duration, false);
        }

        static void Hold(Arm arm, Shape shape, float duration, bool kick)
        {
            if (arm == null) return;
            float now = Time.time;
            // Restart the fade-in only when the arm was down (or is changing pose); repeat calls just extend the hold.
            if (now >= arm.Until || arm.Shape != shape) arm.Start = now;
            arm.Shape = shape;
            arm.Until = now + duration;
            if (kick) arm.Recoil = 1f;
        }

        void LateUpdate()
        {
            Pose(left);
            Pose(right);
        }

        void Pose(Arm arm)
        {
            if (arm == null || Time.time >= arm.Until) return;

            float fade = Mathf.Min(Mathf.Clamp01((Time.time - arm.Start) / FadeIn), Mathf.Clamp01((arm.Until - Time.time) / FadeOut));
            float weight = Mathf.SmoothStep(0f, 1f, fade);
            arm.Recoil = Mathf.MoveTowards(arm.Recoil, 0f, 7f * Time.deltaTime);

            Vector3 dir = aim != null ? aim.AimDirection : transform.forward;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;
            Vector3 side = transform.right * arm.Outward;
            float reach = arm.Ik.Reach * 0.95f;

            Vector3 target;
            switch (arm.Shape)
            {
                case Shape.Raise:
                    target = arm.Shoulder.position + (dir * 0.45f + Vector3.up * 0.85f + side * 0.25f).normalized * reach;
                    break;
                case Shape.Wing:
                    target = arm.Shoulder.position + (side * 0.9f + Vector3.up * 0.35f - dir * 0.3f).normalized * reach;
                    break;
                default: // Point: straight down the aim line, a little below the shoulder, kicked back by the recoil
                    target = arm.Shoulder.position + dir * (reach - arm.Recoil * 0.14f) + Vector3.down * (reach * 0.3f);
                    break;
            }

            // Elbow bends out and down.
            Vector3 pole = side + Vector3.down * 0.6f;
            arm.Ik.Solve(target, pole, weight);
        }
    }
}
