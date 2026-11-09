using System.Collections.Generic;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Press Q to charge forward along the aim direction with both axes dragged back, then whip them through a wide
    // frontal arc together as the dash ends. Damage lands when the dash ends. Pressing C during the dash cancels
    // it (the sweep never lands). The pose is driven through the skeleton; the legs are never touched.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and the arms every LateUpdate
    public class AxeRush : Ability
    {
        [SerializeField] float dashDistance = 6f;
        [SerializeField] float dashDuration = 0.25f;
        [SerializeField] float damage = 18f;
        [SerializeField] float range = 3f;
        [SerializeField] float arcDegrees = 170f;
        [SerializeField] float sweepDuration = 0.2f;
        [SerializeField] float recoverDuration = 0.15f;
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] Color slashColor = new Color(1f, 0.2f, 0.1f, 0.85f);

        [Header("Pose")]
        [SerializeField] float dashLean = 18f;
        [SerializeField] float armsBack = 55f;
        [SerializeField] float armsForward = 25f;
        [SerializeField] float sweepTwist = 60f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_L", "UpperArm_R" };

        CharacterController controller;
        LimbPose pose;
        Energy energy;
        bool active;
        bool posed;
        bool strikePending;
        Vector3 dashDirection;
        float dashEndTime;
        float sweepEndTime;
        float recoverEndTime;
        float twistDirection = 1f;
        float recoverFromTwist;
        float recoverFromLean;
        float recoverFromArms;
        float lastTwist;
        float lastLean;
        float lastArms;
        readonly HashSet<Health> struck = new HashSet<Health>();

        public override string DisplayName => "Rush";
        public override string KeyLabel => "Q";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 10f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override void Update()
        {
            base.Update();

            // On a replica the dash movement arrives through the network position instead.
            if (active && Time.time < dashEndTime && controller != null && !IsReplica)
            {
                float step = dashDistance / dashDuration * Time.deltaTime;
                controller.Move(dashDirection * step);
            }

            if (strikePending && Time.time >= dashEndTime)
            {
                strikePending = false;
                DealDamage();
                SlashEffect.Spawn(transform.position, dashDirection, range, arcDegrees, sweepDuration * 2f, slashColor);
            }
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            // Shift+Q is the EX Whirling Carnage instead.
            return Input.GetKeyDown(KeyCode.Q) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift);
        }

        protected override void Activate()
        {
            dashDirection = Aim.AimDirection;
            twistDirection = -twistDirection;
            active = true;
            strikePending = true;
            dashEndTime = Time.time + dashDuration;
            sweepEndTime = dashEndTime + sweepDuration;
            recoverEndTime = sweepEndTime + recoverDuration;
            recoverFromTwist = sweepTwist * twistDirection;
            recoverFromLean = 0f;
            recoverFromArms = armsForward;
        }

        protected override void Cancel()
        {
            if (!active) return;

            active = false;
            strikePending = false;
            recoverFromTwist = lastTwist;
            recoverFromLean = lastLean;
            recoverFromArms = lastArms;
            recoverEndTime = Time.time + recoverDuration;
        }

        void DealDamage()
        {
            struck.Clear();
            Vector3 origin = transform.position;
            Collider[] colliders = Physics.OverlapSphere(origin + Vector3.up, range, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider c in colliders)
            {
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;

                Vector3 to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(dashDirection, to) > arcDegrees * 0.5f) continue;

                if (!target.TakeDamage(damage, gameObject)) continue;
                if (energy != null) energy.Gain(energyPerHit);
                BloodImpact.Play(target.transform.position + Vector3.up, dashDirection);
            }
        }

        // Dash: lean in, axes dragged back and twisted to one side. Sweep: both axes whip across to the other side.
        // Recovery: ease back to rest.
        void ApplyPose()
        {
            float now = Time.time;
            float twist;
            float lean;
            float arms;
            float side = sweepTwist * twistDirection;

            if (active && now < dashEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (dashEndTime - now) / dashDuration);
                twist = Mathf.Lerp(0f, -side, t);
                lean = Mathf.Lerp(0f, dashLean, t);
                arms = Mathf.Lerp(0f, armsBack, t);
            }
            else if (active && now < sweepEndTime)
            {
                float t = 1f - (sweepEndTime - now) / sweepDuration;
                t = 1f - (1f - t) * (1f - t);
                twist = Mathf.Lerp(-side, side, t);
                lean = Mathf.Lerp(dashLean, 0f, t);
                arms = Mathf.Lerp(armsBack, -armsForward, t);
            }
            else if (now < recoverEndTime)
            {
                active = false;
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - now) / recoverDuration);
                twist = Mathf.Lerp(recoverFromTwist, 0f, t);
                lean = Mathf.Lerp(recoverFromLean, 0f, t);
                arms = Mathf.Lerp(recoverFromArms, 0f, t);
            }
            else
            {
                active = false;
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                lastTwist = lastLean = lastArms = 0f;
                return;
            }

            lastTwist = twist;
            lastLean = lean;
            lastArms = arms;
            posed = true;

            // Parent bones first: the arms' world rotation already includes the spine's.
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, twist);
            pose.Rotate("Spine", transform.right, lean);
            pose.Rotate("UpperArm_L", transform.right, arms);
            pose.Rotate("UpperArm_R", transform.right, arms);
        }
    }
}
