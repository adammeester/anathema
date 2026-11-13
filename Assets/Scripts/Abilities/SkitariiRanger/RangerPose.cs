using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // Arm, leg and rifle pose for the Ranger. The model rests in a relaxed carry (rifle low across the body, muzzle down).
    // While an ability asks for the aim pose this blends the rifle up to the shoulder along the aim line and solves both
    // arms onto it with IK (right hand on the grip, left hand on the fore-end). It runs after ProceduralWalk, which also
    // writes the arms every LateUpdate, and also plants the legs in a braced sniper stance (the left foot is the front one).
    // The ultimate asks for the "deployed" variant: the barrel extension and the support pole under the barrel (both
    // built from primitives in code and only present while deployed, so they never show in the model) pop out.
    // Works on replicas too, because the abilities replay on the remote machine and call the same methods.
    [DefaultExecutionOrder(50)]
    public class RangerPose : MonoBehaviour
    {
        const float FadeIn = 0.18f;
        const float FadeOut = 0.25f;
        const float DeploySpeed = 4f;
        const float ForeOffset = 0.3f;   // how far ahead of the right hand the left hand holds the rifle
        const float MuzzleOffset = 1.36f;     // muzzle distance ahead of the right hand
        const float ExtensionLength = 0.5f;
        const float BraceOffset = 0.7f;       // support pole attaches this far ahead of the right hand
        const float CrouchDepth = 0.22f;  // how far the hips drop in the sniper stance

        ArmIk ikL, ikR, legL, legR;
        Transform hips, footL, footR;
        Quaternion footRestLocalL, footRestLocalR; // foot rotation relative to the character, so the feet stay flat
        float ankleHeight;
        bool hasLegs;
        Transform shoulderR, handL, handR, rifle;
        Vector3 restDirLocal;            // rest rifle direction, in this character's space
        Quaternion restRotLocal;         // rest rifle rotation, in this character's space
        Vector3 boneDir;                 // rest rifle direction, in the rifle bone's own space
        float outwardR;
        bool valid;

        float start;
        float until;
        float strength = 1f;
        bool plantLegs = true;
        bool deployed;
        float deploy;

        public static RangerPose For(GameObject character)
        {
            var pose = character.GetComponent<RangerPose>();
            if (pose == null) pose = character.AddComponent<RangerPose>();
            return pose;
        }

        void Awake() => SetupRig(true);

        // Finds the bones and builds the IK solvers. Also re-run after a script reload in play mode (measureRest false):
        // the ArmIk solvers are plain C# objects Unity can't keep across the reload, while `valid`, the bone references
        // and the rest measurements survive it. The rest measurements must not be retaken then, because the model is no
        // longer at rest (the walk has posed the arms), which would knock the rifle axis and the barrel extension off.
        // The solvers themselves only measure bone lengths and bone-local directions, which don't depend on the pose.
        void SetupRig(bool measureRest)
        {
            Transform model = transform.Find("Model");
            if (model == null)
            {
                enabled = false;
                return;
            }

            Transform upperL = null, foreL = null, upperR = null, foreR = null;
            Transform thighL = null, shinL = null, thighR = null, shinR = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "UpperArm_L": if (upperL == null) upperL = t; break;
                    case "Forearm_L": if (foreL == null) foreL = t; break;
                    case "Hand_L": if (handL == null) handL = t; break;
                    case "UpperArm_R": if (upperR == null) upperR = t; break;
                    case "Forearm_R": if (foreR == null) foreR = t; break;
                    case "Hand_R": if (handR == null) handR = t; break;
                    case "Rifle_R": if (rifle == null) rifle = t; break;
                    case "Hips": if (hips == null) hips = t; break;
                    case "Thigh_L": if (thighL == null) thighL = t; break;
                    case "Shin_L": if (shinL == null) shinL = t; break;
                    case "Foot_L": if (footL == null) footL = t; break;
                    case "Thigh_R": if (thighR == null) thighR = t; break;
                    case "Shin_R": if (shinR == null) shinR = t; break;
                    case "Foot_R": if (footR == null) footR = t; break;
                }
            }

            valid = upperL && foreL && handL && upperR && foreR && handR && rifle;
            if (!valid)
            {
                enabled = false; // fallback model without the rig
                return;
            }

            ikL = new ArmIk(upperL, foreL, handL);
            ikR = new ArmIk(upperR, foreR, handR);
            shoulderR = upperR;
            hasLegs = hips && thighL && shinL && footL && thighR && shinR && footR;
            if (hasLegs)
            {
                legL = new ArmIk(thighL, shinL, footL);
                legR = new ArmIk(thighR, shinR, footR);
            }

            if (!measureRest) return;

            outwardR = Mathf.Sign(Vector3.Dot(upperR.position - upperL.position, transform.right));

            // At rest the left hand sits on the barrel straight ahead of the right hand, so that line is the rifle's axis.
            Vector3 restDir = (handL.position - handR.position).normalized;
            restDirLocal = transform.InverseTransformDirection(restDir);
            restRotLocal = Quaternion.Inverse(transform.rotation) * rifle.rotation;
            boneDir = Quaternion.Inverse(rifle.rotation) * restDir;

            if (hasLegs)
            {
                footRestLocalL = Quaternion.Inverse(transform.rotation) * footL.rotation;
                footRestLocalR = Quaternion.Inverse(transform.rotation) * footR.rotation;
                ankleHeight = footL.position.y - transform.position.y;
            }

            if (deployRoot == null) BuildDeployables();
        }


        // Holds the aim pose for `duration` more seconds. Repeat calls extend it. `braced` also deploys the barrel
        // extension and the support pole.
        // `strength` scales how far the rifle comes up (1 = full shoulder aim); `plantLegs` adds the sniper stance.
        public void Aim(float duration, bool braced, float strength = 1f, bool plantLegs = true)
        {
            float now = Time.time;
            if (now >= until) start = now;
            until = now + duration;
            deployed = braced;
            this.strength = strength;
            this.plantLegs = plantLegs;
        }

        public void Release()
        {
            if (until > Time.time + 0.1f) until = Time.time + 0.1f;
        }

        void LateUpdate()
        {
            if (!valid) return;
            if (ikR == null)
            {
                SetupRig(false); // scripts were reloaded during play mode: rebuild the solvers, keep the rest pose
                if (!valid) return;
            }

            // The attachments grow and shrink on their own schedule, so they retract after the pose ends.
            bool wantDeployed = deployed && Time.time < until;
            deploy = Mathf.MoveTowards(deploy, wantDeployed ? 1f : 0f, DeploySpeed * Time.deltaTime);

            ApplyPose();
            UpdateDeployables(deploy); // after the pose, so they follow the rifle's final position
        }

        // ---- deployables: built in code (never part of the model) and only active while the ultimate has them out ------

        GameObject deployRoot;
        Transform pole, foot, collar, extBarrel, extTip;
        Transform[] coils, extRings;
        Material steelMaterial, bronzeMaterial;

        Transform Part(PrimitiveType type, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = material;
            g.transform.SetParent(deployRoot.transform, false);
            return g.transform;
        }

        void BuildDeployables()
        {
            deployRoot = new GameObject("Ranger Deployables");
            deployRoot.transform.SetParent(transform, false);
            steelMaterial = Anathema.Combat.VisualUtil.UnlitMaterial(new Color(0.17f, 0.18f, 0.2f));
            bronzeMaterial = Anathema.Combat.VisualUtil.UnlitMaterial(new Color(0.6f, 0.38f, 0.16f));

            pole = Part(PrimitiveType.Cylinder, steelMaterial);
            collar = Part(PrimitiveType.Cube, bronzeMaterial);
            foot = Part(PrimitiveType.Cylinder, steelMaterial);
            coils = new Transform[6];
            for (int i = 0; i < coils.Length; i++) coils[i] = Part(PrimitiveType.Cylinder, bronzeMaterial);

            extBarrel = Part(PrimitiveType.Cylinder, steelMaterial);
            extTip = Part(PrimitiveType.Cylinder, bronzeMaterial);
            extRings = new Transform[3];
            for (int i = 0; i < extRings.Length; i++) extRings[i] = Part(PrimitiveType.Cylinder, bronzeMaterial);

            deployRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (steelMaterial != null) Destroy(steelMaterial);
            if (bronzeMaterial != null) Destroy(bronzeMaterial);
        }

        // d: 0 = nothing, 1 = fully out. The pole grows down to the ground from under the barrel; the extension slides out of the muzzle.
        void UpdateDeployables(float d)
        {
            bool on = d > 0.001f;
            if (deployRoot.activeSelf != on) deployRoot.SetActive(on);
            if (!on) return;

            float e = Mathf.SmoothStep(0f, 1f, d);
            Vector3 barrel = (rifle.rotation * boneDir).normalized;
            Vector3 origin = rifle.position + Vector3.up * 0.07f; // the barrel runs a little above the hand

            // Support pole.
            Vector3 top = origin + barrel * BraceOffset;
            float length = Mathf.Max(0.1f, top.y - (transform.position.y + 0.03f)) * e;
            pole.position = top + Vector3.down * (length * 0.5f);
            pole.rotation = Quaternion.identity;
            pole.localScale = new Vector3(0.035f, length * 0.5f, 0.035f);
            collar.position = top;
            collar.rotation = Quaternion.LookRotation(barrel);
            collar.localScale = new Vector3(0.1f, 0.08f, 0.1f);
            foot.position = top + Vector3.down * length;
            foot.localScale = new Vector3(0.14f, 0.025f, 0.14f);
            for (int i = 0; i < coils.Length; i++)
            {
                coils[i].position = top + Vector3.down * (length * (0.12f + 0.07f * i));
                coils[i].localScale = new Vector3(0.075f, 0.01f, 0.075f);
            }

            // Barrel extension.
            Vector3 muzzle = origin + barrel * MuzzleOffset;
            float l = ExtensionLength * e;
            Quaternion along = Quaternion.LookRotation(barrel) * Quaternion.Euler(90f, 0f, 0f);
            extBarrel.SetPositionAndRotation(muzzle + barrel * (l * 0.5f), along);
            extBarrel.localScale = new Vector3(0.055f, l * 0.5f, 0.055f);
            for (int i = 0; i < extRings.Length; i++)
            {
                extRings[i].SetPositionAndRotation(muzzle + barrel * (l * (0.2f + 0.3f * i)), along);
                extRings[i].localScale = new Vector3(0.09f, 0.012f, 0.09f);
            }
            extTip.SetPositionAndRotation(muzzle + barrel * l, along);
            extTip.localScale = new Vector3(0.11f, 0.05f, 0.11f);
            deployedMuzzle = muzzle + barrel * (l + TipClearance);
        }

        const float TipClearance = 0.08f; // shots appear just past the extension's tip, not inside it
        Vector3 deployedMuzzle;

        // Where the deployed barrel extension ends (as posed last frame), so the ultimate's shots leave from its tip.
        // False while the extension isn't out (or the model has no rig).
        public bool TryGetDeployedMuzzle(out Vector3 point)
        {
            point = deployedMuzzle;
            return valid && deployRoot != null && deployRoot.activeSelf;
        }

        // Sniper stance: hips dropped, left foot planted forward and right foot back, knees bent toward the target.
        // `side` points to the character's right (the right leg's outside). Runs before the arms so they follow the hips.
        void BraceLegs(Vector3 dir, Vector3 side, float weight)
        {
            hips.position += Vector3.down * (CrouchDepth * weight);

            Vector3 root = transform.position;
            float y = root.y + ankleHeight;
            Vector3 frontFoot = root + dir * 0.5f - side * 0.2f;
            Vector3 backFoot = root - dir * 0.42f + side * 0.2f;
            frontFoot.y = backFoot.y = y;

            legL.Solve(frontFoot, dir - side * 0.2f, weight);
            legR.Solve(backFoot, dir + side * 0.2f, weight);

            // Keep the feet flat on the ground, turned to the aim line.
            Quaternion yaw = Quaternion.FromToRotation(transform.forward, dir);
            footL.rotation = Quaternion.Slerp(footL.rotation, yaw * transform.rotation * footRestLocalL, weight);
            footR.rotation = Quaternion.Slerp(footR.rotation, yaw * transform.rotation * footRestLocalR, weight);
        }

        void ApplyPose()
        {


            if (Time.time >= until) return;

            float fade = Mathf.Min(Mathf.Clamp01((Time.time - start) / FadeIn), Mathf.Clamp01((until - Time.time) / FadeOut));
            float weight = Mathf.SmoothStep(0f, 1f, fade) * strength;

            Vector3 dir = Vector3.forward;
            var aim = GetComponent<MouseAim>();
            if (aim != null) dir = aim.AimDirection;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;

            Vector3 side = transform.right * outwardR; // the right arm's outside

            if (hasLegs && plantLegs) BraceLegs(dir, side, weight);

            Vector3 gripTarget = shoulderR.position + dir * 0.3f - side * 0.2f + Vector3.down * 0.12f;
            ikR.Solve(gripTarget, side + Vector3.down * 0.6f, weight);

            // Swing the rifle about the right hand until it points down the aim line.
            Quaternion restWorld = transform.rotation * restRotLocal;
            Quaternion aimed = Quaternion.FromToRotation(transform.rotation * restDirLocal, dir) * restWorld;
            rifle.rotation = Quaternion.Slerp(rifle.rotation, aimed, weight);

            Vector3 barrel = rifle.rotation * boneDir;
            Vector3 foreTarget = rifle.position + barrel * ForeOffset;
            ikL.Solve(foreTarget, -side + Vector3.down * 0.6f, weight);
        }
    }
}
