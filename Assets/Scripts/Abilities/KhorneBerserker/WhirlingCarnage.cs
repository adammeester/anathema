using System.Collections.Generic;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Hold Shift to spin on the spot with both chain axes out, shredding everything around you. The body turns a full
    // circle and damage ticks three times during the spin (each target is hit once per tick). Pressing C ends it early.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes the arms every LateUpdate
    public class WhirlingCarnage : Ability
    {
        [SerializeField] float damagePerTick = 9f;
        [SerializeField] float range = 3f;
        [SerializeField] int ticks = 3;
        [SerializeField] float spinDuration = 0.7f;
        [SerializeField] float recoverDuration = 0.15f;
        [SerializeField] float energyPerHit = 6f;
        [SerializeField] Color slashColor = new Color(1f, 0.2f, 0.1f, 0.8f);

        [Header("Pose")]
        [SerializeField] float armLift = 60f;

        static readonly string[] BoneNames = { "UpperArm_L", "UpperArm_R" };

        LimbPose pose;
        Transform model;
        Energy energy;
        bool spinning;
        bool posed;
        float startTime;
        float recoverEndTime;
        int ticksDone;
        readonly HashSet<Health> struck = new HashSet<Health>();

        public override string DisplayName => "Whirl";
        public override string KeyLabel => "Shift";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 5f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            model = transform.Find("Model");
            pose = new LimbPose(model, BoneNames);
        }

        protected override void Update()
        {
            base.Update();
            if (!spinning) return;

            float elapsed = Time.time - startTime;
            // Tick evenly through the spin, the last one landing as it completes.
            while (ticksDone < ticks && elapsed >= spinDuration * (ticksDone + 1) / ticks)
            {
                ticksDone++;
                DealDamage();
                SlashEffect.Spawn(transform.position, Aim.AimDirection, range, 360f, 0.25f, slashColor);
            }
            if (elapsed >= spinDuration) EndSpin();
        }

        void LateUpdate()
        {
            float now = Time.time;
            float yaw = 0f;
            float lift;

            if (spinning)
            {
                float t = Mathf.Clamp01((now - startTime) / spinDuration);
                yaw = 360f * Mathf.SmoothStep(0f, 1f, t);
                lift = armLift * Mathf.Clamp01((now - startTime) / 0.1f);
            }
            else if (now < recoverEndTime)
            {
                lift = armLift * Mathf.Clamp01((recoverEndTime - now) / recoverDuration);
            }
            else
            {
                if (posed)
                {
                    pose.Reset();
                    if (model != null) model.localRotation = Quaternion.identity;
                    posed = false;
                }
                return;
            }

            posed = true;
            if (model != null) model.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Lift both arms out to the sides. The axis is the spinning model's own forward, so it still reads mid-spin.
            // The model faces +Z in Unity, so its left (_L) arm hangs on -X and lifts with a negative roll.
            Vector3 roll = model != null ? model.forward : transform.forward;
            pose.Reset();
            pose.Rotate("UpperArm_L", roll, -lift);
            pose.Rotate("UpperArm_R", roll, lift);
        }

        protected override bool IsTriggered()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        protected override void Activate()
        {
            spinning = true;
            startTime = Time.time;
            ticksDone = 0;
        }

        protected override void Cancel()
        {
            if (spinning) EndSpin();
        }

        void EndSpin()
        {
            spinning = false;
            recoverEndTime = Time.time + recoverDuration;
            if (model != null) model.localRotation = Quaternion.identity;
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

                if (target.TakeDamage(damagePerTick, gameObject) && energy != null) energy.Gain(energyPerHit);
            }
        }
    }
}
