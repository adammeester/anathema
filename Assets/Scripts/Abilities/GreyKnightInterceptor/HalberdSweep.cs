using System.Collections.Generic;
using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Hold Q to sweep the halberd across a wide frontal arc. The swing winds up first (damage lands when the
    // wind-up ends), so pressing C during the wind-up cancels it before it hits.
    //
    // The pose is driven through the skeleton: the spine twists and leans, and both arms are solved with IK onto the
    // shaft (the bolter is strapped to the right forearm, so that hand is free to take the rear grip). The halberd
    // is laid along the sweep direction, cocked high in the wind-up and chopping down through the arc. The legs are
    // never touched, so the feet stay planted.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and both arms every LateUpdate
    public class HalberdSweep : Ability
    {
        [SerializeField] float damage = 20f;
        [SerializeField] float range = 2.5f;
        [SerializeField] float arcDegrees = 120f;
        [SerializeField] float windupDuration = 0.3f;
        [SerializeField] float swingDuration = 0.2f;
        [SerializeField] float recoverDuration = 0.15f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] Color slashColor = new Color(0.7f, 0.9f, 1f, 0.8f);

        [Header("Pose")]
        [Tooltip("Fraction of the sweep yaw taken by the torso; the shaft itself always follows the full yaw.")]
        [SerializeField] float torsoTwistShare = 0.75f;
        [Tooltip("The chest is turned this far away from the halberd side while gripping, so both hands can reach the shaft across the body.")]
        [SerializeField] float chestCross = 35f;
        [SerializeField] float windupLean = 26f;        // leans back while winding up
        [SerializeField] float followThroughLean = 20f; // and drives forward through the cut
        [Tooltip("Visual only: how far past the edge of the damage arc the halberd is drawn back in the wind-up.")]
        [SerializeField] float windupOverrotate = 30f;
        [Tooltip("Visual only: how far past the far edge of the damage arc the swing carries through.")]
        [SerializeField] float followThroughOverrotate = 25f;
        [Tooltip("Shaft angle above horizontal at the top of the wind-up (cocked high over the shoulder).")]
        [SerializeField] float windupPitch = 65f;
        [Tooltip("Shaft angle above horizontal at the end of the cut (negative = chopping down through the arc).")]
        [SerializeField] float swingPitch = -8f;

        [Header("Two-handed grip")]
        [Tooltip("How far apart the hands are on the shaft, in metres.")]
        [SerializeField] float gripSpacing = 0.42f;
        [Tooltip("Rear (bolter) hand: forward of the shoulder line / below shoulder height, in metres.")]
        [SerializeField] float gripForward = 0.4f;
        [SerializeField] float gripDrop = 0.22f;

        [Header("Psychic empowerment (granted by the counter's teleport)")]
        [SerializeField] float empoweredDamageMultiplier = 1.75f;
        [SerializeField] Color empoweredSlashColor = new Color(0.75f, 0.5f, 1f, 0.9f);
        [SerializeField] float empoweredRangeBonus = 0.5f;

        static readonly string[] BoneNames =
        {
            "Spine", "UpperArm_L", "Forearm_L", "Hand_L", "UpperArm_R", "Forearm_R", "Hand_R", "Halberd"
        };

        PsychicHalberdEffect psychicEffect;
        [SerializeField] float empoweredArcBonus = 30f;
        float empoweredUntil;
        bool swingEmpowered;       // this swing was started empowered
        float currentArc = 120f;   // arc of the swing in progress (wider when empowered)
        public bool IsEmpowered => Time.time < empoweredUntil;

        // Charges the halberd for `duration` seconds; the next sweep to land is empowered and uses the charge up.
        public void Empower(float duration)
        {
            empoweredUntil = Time.time + duration;
            GreyKnightSound.Empower();
            if (psychicEffect != null && Halberd) psychicEffect.SetActive(true); // no halberd, no glowing blade
        }

        void EndEmpowerment()
        {
            empoweredUntil = 0f;
            if (psychicEffect != null) psychicEffect.SetActive(false);
        }

        LimbPose pose;
        Energy energy;
        bool active;           // wind-up or swing in progress (recovery is separate)
        bool posed;             // bones are currently away from rest
        bool strikePending;
        bool cooldownPending;   // strike has landed, waiting for the swing to finish before the cooldown starts
        float windupEndTime;
        float swingEndTime;
        float recoverEndTime;
        float swingDirection = 1f;
        float recoverFromYaw;
        float recoverFromLean;
        float recoverFromPitch;
        float recoverFromGrip;
        float lastYaw;
        float lastLean;
        float lastPitch;
        float lastGrip;

        // Two-handed grip. The left hand carries the halberd, the right hand (which has the bolter strapped to the
        // forearm, so it is free) takes the rear of the shaft.
        ArmIk armL, armR;
        Transform halberd, shoulderL, shoulderR;
        Vector3 shaftAxisLocal;   // the shaft and the blade's flat width, in the Halberd bone's local space
        Vector3 bladeWidthLocal;
        float armScale = 1f;      // model arm length relative to the reference rig, to scale the grip distances
        float leftSign = -1f;     // +1 if the character's left is along transform.right, else -1
        readonly HashSet<Health> struck = new HashSet<Health>();

        // With a weapon upgrade this slot becomes the weapon's own move (the hammer slam or the cannon bash), driven by
        // GreyKnightArmament; the halberd and its pose are then put away.
        GreyKnightArmament armament;
        GreyKnightWeapon Mode => GreyKnightUpgrade.WeaponOf(gameObject);
        bool Halberd => Mode == GreyKnightWeapon.Halberd || armament == null;

        protected override bool CooldownStartsOnStrike => Halberd;
        protected override float WindupDuration => Halberd ? 0f : armament.QWindup;
        protected override Color WindupColor => new Color(1f, 0.8f, 0.4f, 1f);

        public override string DisplayName => Halberd ? "Sweep" : Mode == GreyKnightWeapon.Daemonhammer ? "Hammer Slam" : "Overcharged Round";
        public override string KeyLabel => "Q";

        // The Overcharged Round needs the knight to plant his feet while the shell charges.
        public override bool RootsMovement => IsWindingUp && Mode == GreyKnightWeapon.Psycannon && armament != null;

        protected override void OnWindupStart()
        {
            if (armament != null) armament.QBegin();
        }

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 0.9f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            Transform model = transform.Find("Model");
            pose = new LimbPose(model, BoneNames);
            psychicEffect = PsychicHalberdEffect.Create(model);

            halberd = pose.Get("Halberd");
            shoulderL = pose.Get("UpperArm_L");
            shoulderR = pose.Get("UpperArm_R");
            Transform foreL = pose.Get("Forearm_L"), handL = pose.Get("Hand_L");
            Transform foreR = pose.Get("Forearm_R"), handR = pose.Get("Hand_R");
            if (halberd == null || shoulderL == null || foreL == null || handL == null
                || shoulderR == null || foreR == null || handR == null)
                return; // fallback model without the rig: the sweep falls back to the one-handed twist

            armL = new ArmIk(shoulderL, foreL, handL);
            armR = new ArmIk(shoulderR, foreR, handR);
            armScale = (armL.Reach + armR.Reach) * 0.5f / ReferenceArmReach;

            // At rest the model stands upright with the halberd vertical and the blade's flat facing the character's
            // right, so those root-space directions pin down the bone's own axes whatever the importer did to them.
            shaftAxisLocal = halberd.InverseTransformDirection(transform.up);
            bladeWidthLocal = halberd.InverseTransformDirection(transform.right);
            leftSign = Mathf.Sign(Vector3.Dot(shoulderL.position - shoulderR.position, transform.right));
        }

        const float ReferenceArmReach = 0.76f; // average upper arm + forearm length of the Grey Knight rig

        protected override void Update()
        {
            // The armament is added at runtime by UpgradeState, so look for it until it turns up.
            if (armament == null) armament = GetComponent<GreyKnightArmament>();
            CooldownScale = !Halberd && cooldown > 0f ? armament.QCooldown / cooldown : 1f;
            base.Update();

            // The charge fades if no sweep is made in time.
            if (psychicEffect != null && !IsEmpowered && empoweredUntil > 0f) EndEmpowerment();

            if (strikePending && Time.time >= windupEndTime)
            {
                strikePending = false;
                cooldownPending = true;
                bool empowered = swingEmpowered;
                int hits = DealDamage(empowered ? empoweredDamageMultiplier : 1f, empowered ? empoweredRangeBonus : 0f);
                GreyKnightSound.Swing(empowered);
                GreyKnightSound.Impact(hits);
                bool purifier = GreyKnightUpgrade.HasPurifier(gameObject);
                float reach = range + (empowered ? empoweredRangeBonus : 0f);
                SlashEffect.Spawn(transform.position, Aim.AimDirection, reach,
                    currentArc, swingDuration * 2f, purifier ? new Color(1f, 0.5f, 0.15f, 0.9f) : empowered ? empoweredSlashColor : slashColor);
                // Purifier Order: the swing leaves burning ground (damaging only on the owner's machine).
                if (purifier)
                    FirePool.Spawn(gameObject, Health.Team, transform.position + Aim.AimDirection * (reach * 0.55f),
                        GreyKnightUpgrade.GroundFireRadius, GreyKnightUpgrade.GroundFireSeconds, GreyKnightUpgrade.GroundFirePerSecond, !IsReplica);
                if (empowered) EndEmpowerment();
            }

            // The cooldown starts once the swing has finished.
            if (cooldownPending && Time.time >= swingEndTime)
            {
                cooldownPending = false;
                StartCooldown();
            }
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKey(KeyCode.Q);
        }

        protected override void Activate()
        {
            if (!Halberd)
            {
                // The weapon's move lands now (the wind-up has just finished). An empowered charge boosts it too.
                float boost = IsEmpowered ? empoweredDamageMultiplier : 1f;
                if (IsEmpowered) EndEmpowerment();
                armament.QStrike(boost);
                return;
            }

            swingDirection = -swingDirection;
            swingEmpowered = IsEmpowered;
            currentArc = arcDegrees + (swingEmpowered ? empoweredArcBonus : 0f);
            strikePending = true;
            active = true;
            windupEndTime = Time.time + windupDuration;
            swingEndTime = windupEndTime + swingDuration;
            recoverEndTime = swingEndTime + recoverDuration;
            recoverFromYaw = (currentArc * 0.5f + followThroughOverrotate) * swingDirection;
            recoverFromLean = followThroughLean;
            recoverFromPitch = swingPitch;
            recoverFromGrip = 1f;
        }

        protected override void Cancel()
        {
            if (armament != null) armament.CancelPose();

            // Cancelled mid-swing (the strike already landed): the cooldown starts now, not refunded.
            if (cooldownPending)
            {
                cooldownPending = false;
                StartCooldown();
            }

            if (!active) return;

            // Cancelled during the wind-up, before the strike lands: no cooldown.
            if (strikePending) RefundCooldown();

            // Ease back to rest from wherever the pose currently is.
            active = false;
            strikePending = false;
            recoverFromYaw = lastYaw;
            recoverFromLean = lastLean;
            recoverFromPitch = lastPitch;
            recoverFromGrip = lastGrip;
            recoverEndTime = Time.time + recoverDuration;
        }

        int DealDamage(float damageMultiplier, float rangeBonus)
        {
            int hits = 0;
            struck.Clear();
            Vector3 origin = transform.position;
            Collider[] colliders = Physics.OverlapSphere(origin + Vector3.up, range + rangeBonus, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider c in colliders)
            {
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;

                Vector3 to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(Aim.AimDirection, to) > currentArc * 0.5f) continue;

                if (!target.TakeDamage(damage * damageMultiplier, gameObject)) continue;
                hits++;
                if (energy != null) energy.Gain(energyPerHit);
                GreyKnightUpgrade.Ignite(gameObject, target);
            }

            return hits;
        }

        // Wind-up: twist back to one edge of the arc and lean away. Swing: whip through to the other edge, leaning in.
        // Recovery: ease back to rest.
        void ApplyPose()
        {
            // With a weapon upgrade the armament poses the arms; this pose only ever drives the halberd.
            if (!Halberd && !posed && !active) return;
            float now = Time.time;
            float yaw;
            float lean;
            float pitch;
            float grip; // 0 = arms at rest, 1 = both hands on the shaft

            if (active && now < windupEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (windupEndTime - now) / windupDuration);
                yaw = Mathf.Lerp(0f, -(currentArc * 0.5f + windupOverrotate) * swingDirection, t);
                lean = Mathf.Lerp(0f, -windupLean, t);
                pitch = Mathf.Lerp(0f, windupPitch, t);
                grip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.8f)); // the off hand reaches in early
            }
            else if (active && now < swingEndTime)
            {
                float t = 1f - (swingEndTime - now) / swingDuration;
                t = 1f - (1f - t) * (1f - t); // fast start, settles at the end
                float half = currentArc * 0.5f;
                yaw = Mathf.Lerp(-(half + windupOverrotate), half + followThroughOverrotate, t) * swingDirection;
                lean = Mathf.Lerp(-windupLean, followThroughLean, t);
                pitch = Mathf.Lerp(windupPitch, swingPitch, t); // chops down out of the cocked position through the arc
                grip = 1f;
            }
            else if (now < recoverEndTime)
            {
                active = false;
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - now) / recoverDuration);
                yaw = Mathf.Lerp(recoverFromYaw, 0f, t);
                lean = Mathf.Lerp(recoverFromLean, 0f, t);
                pitch = Mathf.Lerp(recoverFromPitch, 0f, t);
                grip = Mathf.Lerp(recoverFromGrip, 0f, t);
            }
            else
            {
                active = false;
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                lastYaw = 0f;
                lastLean = 0f;
                lastPitch = 0f;
                lastGrip = 0f;
                return;
            }

            lastYaw = yaw;
            lastLean = lean;
            lastPitch = pitch;
            lastGrip = grip;
            posed = true;

            // Parent bones first: the arms' world rotation already includes the spine's. The chest turns away from
            // the halberd side as the grip comes on, so the hands can meet across the body.
            float chestYaw = yaw * torsoTwistShare - leftSign * chestCross * grip;
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, chestYaw);
            pose.Rotate("Spine", transform.right, lean);

            if (armL != null && grip > 0.001f) PoseGrip(yaw, pitch, chestYaw, grip);
        }

        // Both hands on the shaft: the halberd lies along the sweep direction (full arc yaw, pitched by `pitch`), the
        // left hand leads and the right hand takes the rear. Both arms are solved with IK towards points on the shaft.
        void PoseGrip(float yaw, float pitch, float chestYaw, float grip)
        {
            Vector3 up = Vector3.up;
            Vector3 flat = Quaternion.AngleAxis(yaw, up) * transform.forward;
            Vector3 shaft = flat * Mathf.Cos(pitch * Mathf.Deg2Rad) + up * Mathf.Sin(pitch * Mathf.Deg2Rad);

            Vector3 chestForward = Quaternion.AngleAxis(chestYaw, up) * transform.forward;
            Vector3 chestLeft = leftSign * (Quaternion.AngleAxis(chestYaw, up) * transform.right);
            Vector3 shoulderMid = (shoulderL.position + shoulderR.position) * 0.5f;

            // The rear hand sits in front of the chest; the lead hand is one grip-spacing further along the shaft.
            Vector3 rearGrip = shoulderMid + chestForward * (gripForward * armScale) - up * (gripDrop * armScale);
            Vector3 leadGrip = rearGrip + shaft * (gripSpacing * armScale);

            // Elbows drop and flare outwards.
            Vector3 leadWrist = armL.Solve(leadGrip, chestLeft * 0.6f - up * 0.8f, grip);
            armR.Solve(leadWrist - shaft * (gripSpacing * armScale), -chestLeft * 0.6f - up * 0.8f, grip);

            // Lay the halberd along the shaft line with the blade's flat horizontal (so the edge leads the cut). It is
            // a child of the left hand, so it stays attached there until the grip has fully taken over.
            Quaternion toWorld = Quaternion.LookRotation(shaft, Vector3.Cross(up, shaft))
                                 * Quaternion.Inverse(Quaternion.LookRotation(shaftAxisLocal, bladeWidthLocal));
            halberd.rotation = Quaternion.Slerp(halberd.rotation, toWorld, grip);
        }
    }
}
