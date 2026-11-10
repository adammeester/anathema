using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Makes the Heavy Chainaxe and the Eviscerator two-handed. While one is wielded the weapon is held centred in front
    // of the chest, and both arms are solved (two-bone IK) onto its two grip markers, so both hands always stay on it.
    //
    // The weapon swings about the shoulder axis: AxeChop and AxeToss feed their arm-raise angle in through ChopPitch /
    // TossPitch instead of rotating a single arm. Rotating about the shoulder axis keeps both grips the same distance
    // from both shoulders, so the arms can always reach. Added by UpgradeState at runtime; runs after every ability pose.
    [DefaultExecutionOrder(200)]
    public class TwoHandedGrip : MonoBehaviour
    {
        // Degrees about the character's right axis (negative lifts the weapon up and back), per ability.
        public float ChopPitch { get; set; }
        public float TossPitch { get; set; }

        // Where the hands hold the weapon, in design units relative to the shoulder centre: forward and up for the rear
        // hand, then how far the front hand sits along the shaft and how far the shaft leans forward.
        const float RearForward = 0.22f, RearUp = -0.2f, RearSide = 0.14f, GripSpacing = 0.42f;
        // Direction of the shaft: held low and diagonal, forward and out to the character's left. The game camera looks
        // down steeply, so a near-vertical blade reads as sticking out behind him.
        const float ShaftForward = 0.6f, ShaftUp = 0.3f, ShaftSide = 0.75f;
        // The most the hands may sit from the shoulder axis (design units) with the arms nearly straight.
        const float DesignRadius = 0.47f;

        struct Arm
        {
            public Transform up, fore, hand;
            public Quaternion upRest, foreRest, handRest;
            public float a, b; // upper arm and forearm length
        }

        class Weapon
        {
            public Transform bone;
            public Vector3 bonePos;   // root space, at rest
            public Quaternion boneRot;
            public Vector3 gripA;     // root space, at rest
            public Vector3 shaftDir;  // root space, at rest (rear grip toward front grip)
        }

        Arm armL, armR;
        Transform spine;
        Quaternion spineRestRel;
        Weapon heavy, evisc;
        float unit = 1f;
        Vector3 forward0, left0; // the model's facing and its L side, in root space
        bool held;

        // True while a two-handed weapon is wielded (the abilities then pose the weapon instead of single arms).
        public bool Engaged => enabled && Current() != null;

        void Awake()
        {
            Transform model = transform.Find("Model");
            if (model == null) { enabled = false; return; }

            Transform upL = null, foreL = null, handL = null, upR = null, foreR = null, handR = null;
            Transform heavyBone = null, heavyA = null, heavyB = null, eviscBone = null, eviscA = null, eviscB = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Spine": if (spine == null) spine = t; break;
                    case "UpperArm_L": if (upL == null) upL = t; break;
                    case "Forearm_L": if (foreL == null) foreL = t; break;
                    case "Hand_L": if (handL == null) handL = t; break;
                    case "UpperArm_R": if (upR == null) upR = t; break;
                    case "Forearm_R": if (foreR == null) foreR = t; break;
                    case "Hand_R": if (handR == null) handR = t; break;
                    case "Heavy_Chainaxe": if (heavyBone == null) heavyBone = t; break;
                    case "Heavy_GripA": if (heavyA == null) heavyA = t; break;
                    case "Heavy_GripB": if (heavyB == null) heavyB = t; break;
                    case "Eviscerator": if (eviscBone == null) eviscBone = t; break;
                    case "Evisc_GripA": if (eviscA == null) eviscA = t; break;
                    case "Evisc_GripB": if (eviscB == null) eviscB = t; break;
                }
            }

            if (spine == null || upL == null || foreL == null || handL == null || upR == null || foreR == null || handR == null)
            {
                enabled = false;
                return;
            }

            armL = MakeArm(upL, foreL, handL);
            armR = MakeArm(upR, foreR, handR);
            spineRestRel = Quaternion.Inverse(transform.rotation) * spine.rotation;
            heavy = MakeWeapon(heavyBone, heavyA, heavyB);
            evisc = MakeWeapon(eviscBone, eviscA, eviscB);

            // The model's own facing and side, read from the rest pose rather than assumed: the wrists hang in front of the
            // shoulders, and "L" is the side of the L bones. (Root space, flattened onto the ground.)
            Vector3 f = transform.InverseTransformDirection((handL.position - upL.position) + (handR.position - upR.position));
            f.y = 0f;
            forward0 = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            Vector3 l = transform.InverseTransformDirection(upL.position - upR.position);
            l.y = 0f;
            left0 = l.sqrMagnitude > 1e-6f ? l.normalized : Vector3.left;

            // Scale the design-unit hold offsets to this model: how far the hands can sit from the shoulder axis.
            float half = Vector3.Distance(upL.position, upR.position) * 0.5f;
            float reach = 0.97f * 0.5f * (armL.a + armL.b + armR.a + armR.b);
            float radius = reach > half ? Mathf.Sqrt(reach * reach - half * half) : reach * 0.5f;
            unit = radius / DesignRadius;
        }

        static Arm MakeArm(Transform up, Transform fore, Transform hand)
        {
            return new Arm
            {
                up = up, fore = fore, hand = hand,
                upRest = up.localRotation, foreRest = fore.localRotation, handRest = hand.localRotation,
                a = Vector3.Distance(up.position, fore.position),
                b = Vector3.Distance(fore.position, hand.position)
            };
        }

        Weapon MakeWeapon(Transform bone, Transform gripA, Transform gripB)
        {
            if (bone == null || gripA == null || gripB == null) return null;
            Vector3 a = transform.InverseTransformPoint(gripA.position);
            Vector3 b = transform.InverseTransformPoint(gripB.position);
            return new Weapon
            {
                bone = bone,
                bonePos = transform.InverseTransformPoint(bone.position),
                boneRot = Quaternion.Inverse(transform.rotation) * bone.rotation,
                gripA = a,
                shaftDir = (b - a).normalized
            };
        }

        Weapon Current()
        {
            switch (BerserkerWeapons.Of(gameObject))
            {
                case BerserkerWeapon.HeavyChainaxe: return heavy;
                case BerserkerWeapon.Eviscerator: return evisc;
                default: return null;
            }
        }

        void LateUpdate()
        {
            Weapon w = Current();
            if (w == null)
            {
                if (held)
                {
                    held = false;
                    ResetArm(armL);
                    ResetArm(armR);
                }
                return;
            }
            held = true;

            // Start from the rest arms, so nothing accumulates frame to frame.
            ResetArm(armL);
            ResetArm(armR);

            // The chest frame: the root's facing plus whatever twist the spine has been given.
            Quaternion delta = spine.rotation * Quaternion.Inverse(transform.rotation * spineRestRel);
            Quaternion frame = delta * transform.rotation;
            Vector3 fwd = frame * forward0;
            Vector3 up = frame * Vector3.up;
            Vector3 left = frame * left0;
            Vector3 pivot = (armL.up.position + armR.up.position) * 0.5f;

            Vector3 gripA = pivot + (fwd * RearForward + up * RearUp - left * RearSide) * unit;
            Vector3 dir = (fwd * ShaftForward + up * ShaftUp + left * ShaftSide).normalized;
            Vector3 gripB = gripA + dir * GripSpacing * unit;

            // Swing: rotate the whole hold about the shoulder axis.
            Quaternion pitch = Quaternion.AngleAxis(ChopPitch + TossPitch, Vector3.Cross(transform.up, transform.rotation * forward0));
            gripA = pivot + pitch * (gripA - pivot);
            gripB = pivot + pitch * (gripB - pivot);

            // Weapon pose: turn its shaft onto the hold direction, swing it, and put the rear grip in the rear hand.
            Quaternion aim = Quaternion.FromToRotation(frame * w.shaftDir, dir);
            Quaternion total = pitch * aim * frame;
            Vector3 gripOffset = w.gripA - w.bonePos;

            // Arms first: the weapon bone is a child of the right hand, so it has to be placed after the arms have moved.
            Solve(armR, gripA);
            Solve(armL, gripB);
            w.bone.SetPositionAndRotation(gripA - total * gripOffset, total * w.boneRot);
        }

        static void ResetArm(Arm arm)
        {
            arm.up.localRotation = arm.upRest;
            arm.fore.localRotation = arm.foreRest;
            arm.hand.localRotation = arm.handRest;
        }

        // Two-bone IK: bends the arm so the wrist reaches `target`, elbow kept toward where it hangs at rest.
        static void Solve(Arm arm, Vector3 target)
        {
            Vector3 shoulder = arm.up.position;
            Vector3 elbow = arm.fore.position;
            Vector3 wrist;
            Vector3 pole = elbow - shoulder;

            Vector3 toTarget = target - shoulder;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(arm.a - arm.b) + 0.01f, (arm.a + arm.b) * 0.999f);
            Vector3 u = toTarget.normalized;

            float x = (arm.a * arm.a - arm.b * arm.b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, arm.a * arm.a - x * x));
            Vector3 perp = Vector3.ProjectOnPlane(pole, u);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.down, u);
            perp.Normalize();

            Vector3 newElbow = shoulder + u * x + perp * h;
            arm.up.rotation = Quaternion.FromToRotation(elbow - shoulder, newElbow - shoulder) * arm.up.rotation;

            elbow = arm.fore.position;
            wrist = arm.hand.position;
            arm.fore.rotation = Quaternion.FromToRotation(wrist - elbow, shoulder + u * d - elbow) * arm.fore.rotation;
        }
    }
}
