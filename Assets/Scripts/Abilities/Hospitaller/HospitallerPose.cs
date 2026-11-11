using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Arm poses for the Hospitaller. The model has no animation clips, so this runs after ProceduralWalk:
    //   - the left arm always holds the open book steady in front of her (the walk swing is undone every frame);
    //   - the right arm (the icon) points at a world target while a beam is held, or is raised high to pray.
    // Works on replicas too, because the abilities replay there and call the same methods.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which writes both arms every LateUpdate
    public class HospitallerPose : MonoBehaviour
    {
        const float FadeIn = 0.1f;
        const float FadeOut = 0.2f;

        Transform upperL, foreL;
        Quaternion upperLRest, foreLRest;

        ArmIk right;
        Transform rightShoulder;
        float rightOutward = -1f;

        bool raise;
        float start;
        float until;
        Vector3 pointAt;
        bool pointing;
        MouseAim aim;

        public static HospitallerPose For(GameObject character)
        {
            var pose = character.GetComponent<HospitallerPose>();
            if (pose == null) pose = character.AddComponent<HospitallerPose>();
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

            Transform foreR = null, handR = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "UpperArm_L" && upperL == null) upperL = t;
                else if (t.name == "Forearm_L" && foreL == null) foreL = t;
                else if (t.name == "UpperArm_R" && rightShoulder == null) rightShoulder = t;
                else if (t.name == "Forearm_R" && foreR == null) foreR = t;
                else if (t.name == "Hand_R" && handR == null) handR = t;
            }

            if (upperL != null && foreL != null)
            {
                upperLRest = upperL.localRotation;
                foreLRest = foreL.localRotation;
            }
            if (rightShoulder != null && foreR != null && handR != null)
            {
                right = new ArmIk(rightShoulder, foreR, handR);
                if (upperL != null) rightOutward = Mathf.Sign(Vector3.Dot(rightShoulder.position - upperL.position, transform.right));
            }
        }

        // Where the beam leaves her: the top of the icon in the right hand (or the chest without a rig).
        public Vector3 IconTip
        {
            get
            {
                if (right == null) return transform.position + Vector3.up * 1.5f + transform.forward * 0.4f;
                return right.Hand.position + Vector3.up * 0.45f;
            }
        }

        // Point the icon at `target` for the next `duration` seconds (call every frame while a beam is held).
        public void Point(Vector3 target, float duration)
        {
            Begin(false, duration);
            pointing = true;
            pointAt = target;
        }

        // Raise the icon high overhead for `duration` seconds.
        public void Raise(float duration)
        {
            Begin(true, duration);
            pointing = false;
        }

        public void Release()
        {
            if (until > Time.time + 0.1f) until = Time.time + 0.1f;
        }

        void Begin(bool raised, float duration)
        {
            float now = Time.time;
            if (now >= until || raise != raised) start = now;
            raise = raised;
            until = now + duration;
        }

        void LateUpdate()
        {
            // The book never swings.
            if (upperL != null)
            {
                upperL.localRotation = upperLRest;
                foreL.localRotation = foreLRest;
            }

            if (right == null || Time.time >= until) return;

            float fade = Mathf.Min(Mathf.Clamp01((Time.time - start) / FadeIn), Mathf.Clamp01((until - Time.time) / FadeOut));
            float weight = Mathf.SmoothStep(0f, 1f, fade);
            float reach = right.Reach * 0.95f;
            Vector3 side = transform.right * rightOutward;
            Vector3 shoulder = rightShoulder.position;

            Vector3 target;
            if (raise)
            {
                Vector3 fwd = transform.forward;
                target = shoulder + (Vector3.up * 0.9f + fwd * 0.3f + side * 0.15f).normalized * reach;
            }
            else
            {
                Vector3 dir = pointing ? pointAt - shoulder : (aim != null ? aim.AimDirection : transform.forward);
                dir.y = Mathf.Clamp(dir.y, -0.5f, 0.5f);
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;
                target = shoulder + dir * reach + Vector3.up * (reach * 0.1f);
            }

            right.Solve(target, side + Vector3.down * 0.6f, weight);
        }
    }
}
