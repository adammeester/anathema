using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // Cast poses for the Sorcerer. The model has no animation clips, so the arms are solved with IK on top of the walk
    // cycle (which is why this runs after ProceduralWalk). The staff arm casts his support spells (Mending Sands); the
    // free arm casts his offensive ones (Sands of Prospero, Sinking Sands, the Kine Shield throw). Each arm can:
    //   Raise  - over a wind-up the hand is hoisted gradually overhead while he leans back a little; long casts tremble
    //            as they near the end.
    //   Strike - the hand swings down toward where the spell goes, holds a moment, then eases back to the walk.
    //   Drop   - a cancelled cast: the arm falls back from wherever it got to.
    // Every move starts from wherever the arm is now, so back-to-back casts (held LMB) flow into each other.
    // Works on replicas too, because the abilities replay on the remote machine and call the same methods.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and the upper arms every LateUpdate
    public class SorcererPose : MonoBehaviour
    {
        public enum Hand
        {
            Staff,
            Free
        }

        enum Phase
        {
            Idle,
            Raise,
            Strike,
            Drop
        }

        class Arm
        {
            public ArmIk Ik;
            public Transform Shoulder;
            public Transform Fore;
            public Quaternion ForeRest;
            public float Outward = 1f; // +1 if the arm's outside is along transform.right, else -1
            public float LeanScale;    // how much this arm's cast leans the body back

            public Phase Phase;
            public float Start;
            public float Duration;
            public float Drop;         // Strike: how far below level the hand points
            public Vector3 StrikeDir;
            public float FromWeight;
            public Vector3 FromDir;    // body-relative (x right, y up, z forward) at the start of the move
            public float Weight;       // last frame's
            public Vector3 Dir;        // last frame's, body-relative
        }

        const float StrikeSwing = 0.08f;
        const float StrikeHold = 0.15f;
        const float StrikeFade = 0.25f;
        const float DropFade = 0.18f;
        const float MaxLean = 9f;
        const float MaxHeadTilt = 7f;
        const float Tremble = 2.5f;
        const float TrembleMinCast = 0.5f; // only casts at least this long shake

        Arm staffArm;
        Arm freeArm;
        Transform spine;
        Transform head;
        MouseAim aim;

        // The pose component for `character`, added on first use so older prefabs get it too.
        public static SorcererPose For(GameObject character)
        {
            var pose = character.GetComponent<SorcererPose>();
            if (pose == null) pose = character.AddComponent<SorcererPose>();
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

            Transform staff = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Spine" && spine == null) spine = t;
                else if (t.name == "Head" && head == null) head = t;
                else if (t.name == "Staff" && staff == null) staff = t;
            }

            string staffSide = StaffSide(staff);
            staffArm = BuildArm(model, staffSide, 1f);
            freeArm = BuildArm(model, staffSide == "R" ? "L" : "R", 0.45f);
            if (staffArm != null && freeArm != null)
            {
                float sign = Mathf.Sign(Vector3.Dot(staffArm.Shoulder.position - freeArm.Shoulder.position, transform.right));
                staffArm.Outward = sign;
                freeArm.Outward = -sign;
            }
            else
            {
                staffArm = null;
                freeArm = null;
                enabled = false; // fallback model without the rig
            }
        }

        static Arm BuildArm(Transform model, string side, float leanScale)
        {
            Transform upper = null, fore = null, hand = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "UpperArm_" + side && upper == null) upper = t;
                else if (t.name == "Forearm_" + side && fore == null) fore = t;
                else if (t.name == "Hand_" + side && hand == null) hand = t;
            }
            if (upper == null || fore == null || hand == null) return null;
            return new Arm
            {
                Ik = new ArmIk(upper, fore, hand), Shoulder = upper, Fore = fore, ForeRest = fore.localRotation,
                LeanScale = leanScale, Dir = Overhead
            };
        }

        // "L" or "R": the arm the Staff bone hangs under. Right if the rig doesn't say.
        static string StaffSide(Transform staff)
        {
            for (Transform t = staff; t != null; t = t.parent)
            {
                bool limb = t.name.StartsWith("Hand") || t.name.StartsWith("Forearm") || t.name.StartsWith("UpperArm");
                if (limb && t.name.EndsWith("_L")) return "L";
                if (limb && t.name.EndsWith("_R")) return "R";
            }
            return "R";
        }

        Arm Get(Hand hand) => hand == Hand.Staff ? staffArm : freeArm;

        // Sideways offset (flat, from the body's centre) of `hand`'s shoulder, for spells that should leave from that side.
        // A fallback model without the rig: the staff is taken to be on the right.
        public Vector3 SideOffset(Hand hand, float fallback = 0.35f)
        {
            Arm arm = Get(hand);
            if (arm == null) return transform.right * (hand == Hand.Staff ? fallback : -fallback);
            Vector3 d = arm.Shoulder.position - transform.position;
            return transform.right * Vector3.Dot(d, transform.right);
        }

        // The hand rises over `seconds`, reaching full height as the wind-up ends.
        public void Raise(Hand hand, float seconds)
        {
            Arm arm = Get(hand);
            if (arm == null) return;
            Begin(arm, Phase.Raise);
            arm.Duration = Mathf.Max(0.05f, seconds);
        }

        // The cast goes off: the hand swings toward `point` (pointing `drop` below level: 0 straight out, ~0.35 at the
        // ground a few metres away), then the arm returns to the walk.
        public void Strike(Hand hand, Vector3 point, float drop = 0.35f)
        {
            Arm arm = Get(hand);
            if (arm == null) return;
            Vector3 d = point - transform.position;
            d.y = 0f;
            Begin(arm, Phase.Strike);
            arm.StrikeDir = d.sqrMagnitude > 1e-4f ? d.normalized : Forward();
            arm.Drop = drop;
        }

        // The cast was cancelled: the arm falls back from wherever it had got to.
        public void Drop(Hand hand)
        {
            Arm arm = Get(hand);
            if (arm == null || arm.Phase != Phase.Raise) return;
            Begin(arm, Phase.Drop);
        }

        void Begin(Arm arm, Phase phase)
        {
            arm.FromWeight = arm.Phase == Phase.Idle ? 0f : arm.Weight;
            // From rest, a new raise or strike starts from overhead, so the arm sweeps up as its weight comes in.
            arm.FromDir = arm.FromWeight > 0.01f ? arm.Dir : Overhead;
            arm.Phase = phase;
            arm.Start = Time.time;
        }

        Vector3 Forward()
        {
            Vector3 dir = aim != null ? aim.AimDirection : transform.forward;
            dir.y = 0f;
            return dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;
        }

        // Body-relative directions (x = the arm's outside, y = up, z = forward).
        static readonly Vector3 Overhead = new Vector3(0.15f, 1f, 0.3f).normalized;

        void LateUpdate()
        {
            if (staffArm == null) return;

            float lean = 0f, shake = 0f;
            Step(staffArm, ref lean, ref shake);
            Step(freeArm, ref lean, ref shake);
            if (staffArm.Phase == Phase.Idle && freeArm.Phase == Phase.Idle && lean <= 0f) return;

            // Lean back as a hand goes up (the walk has already written Spine and Head this frame).
            Vector3 right = transform.right;
            float noise = shake * Tremble * (Mathf.PerlinNoise(Time.time * 20f, 0.53f) * 2f - 1f);
            if (spine != null)
                spine.rotation = Quaternion.AngleAxis(-MaxLean * lean, right) * Quaternion.AngleAxis(noise, transform.forward) * spine.rotation;
            if (head != null)
                head.rotation = Quaternion.AngleAxis(-MaxHeadTilt * lean, right) * head.rotation;

            Solve(staffArm);
            Solve(freeArm);
        }

        // Works out this frame's weight and direction for `arm`; adds its lean and tremble to the body's.
        void Step(Arm arm, ref float lean, ref float shake)
        {
            if (arm.Phase == Phase.Idle) return;

            float elapsed = Time.time - arm.Start;
            float weight;
            Vector3 dir;
            float armLean;

            switch (arm.Phase)
            {
                case Phase.Raise:
                {
                    float t = Mathf.Clamp01(elapsed / arm.Duration);
                    float s = Mathf.SmoothStep(0f, 1f, t);
                    weight = Mathf.Lerp(arm.FromWeight, 1f, s);
                    dir = Vector3.Slerp(arm.FromDir, Overhead, s);
                    armLean = weight;
                    if (arm.Duration >= TrembleMinCast) shake = Mathf.Max(shake, t * t);
                    break;
                }
                case Phase.Strike:
                {
                    float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / StrikeSwing));
                    float fade = Mathf.Clamp01((elapsed - StrikeSwing - StrikeHold) / StrikeFade);
                    if (fade >= 1f) { Finish(arm); return; }
                    Vector3 down = BodyRelative(arm, (arm.StrikeDir - Vector3.up * arm.Drop).normalized);
                    weight = Mathf.Lerp(arm.FromWeight, 1f, swing) * (1f - Mathf.SmoothStep(0f, 1f, fade));
                    dir = Vector3.Slerp(arm.FromDir, down, swing);
                    armLean = Mathf.Max(0f, dir.y) * weight;
                    break;
                }
                default: // Drop
                {
                    float fade = Mathf.Clamp01(elapsed / DropFade);
                    if (fade >= 1f) { Finish(arm); return; }
                    weight = arm.FromWeight * (1f - fade);
                    dir = arm.FromDir;
                    armLean = weight;
                    break;
                }
            }

            arm.Weight = weight;
            arm.Dir = dir;
            lean = Mathf.Max(lean, armLean * arm.LeanScale);
        }

        void Solve(Arm arm)
        {
            if (arm.Phase == Phase.Idle) return;

            // The forearm isn't touched by the walk, so start each frame's solve from its rest pose.
            arm.Fore.localRotation = arm.ForeRest;

            Vector3 fwd = Forward();
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * arm.Outward; // the arm's outside, square to the aim
            Vector3 world = (side * arm.Dir.x + Vector3.up * arm.Dir.y + fwd * arm.Dir.z).normalized;
            Vector3 target = arm.Shoulder.position + world * (arm.Ik.Reach * 0.97f);
            Vector3 pole = side + Vector3.down * 0.5f - fwd * 0.3f;
            arm.Ik.Solve(target, pole, arm.Weight);
        }

        // A world direction expressed in `arm`'s body-relative frame (x = its outside, y = up, z = forward).
        Vector3 BodyRelative(Arm arm, Vector3 world)
        {
            Vector3 fwd = Forward();
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * arm.Outward; // matches transform.right when facing the aim
            return new Vector3(Vector3.Dot(world, side), world.y, Vector3.Dot(world, fwd)).normalized;
        }

        void Finish(Arm arm)
        {
            arm.Phase = Phase.Idle;
            arm.Weight = 0f;
            arm.Dir = Overhead;
            if (arm.Fore != null) arm.Fore.localRotation = arm.ForeRest;
        }

        void OnDisable()
        {
            if (staffArm != null) Finish(staffArm);
            if (freeArm != null) Finish(freeArm);
        }
    }
}
