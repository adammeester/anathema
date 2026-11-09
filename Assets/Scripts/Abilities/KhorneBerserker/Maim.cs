using System.Collections.Generic;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Press E for a low, crippling chop in front of you. Anything hit takes a little damage and is Maimed: 30% slower
    // for 2.5 seconds, bleeding a trail as it goes. The damage lands when the wind-up ends, so C cancels it before then.
    // EX (Shift+E, costs energy): The Butcher's Nails instead of the chop. The Berserker becomes Enraged: immune to
    // crowd control, 20% faster attacks and 10% faster movement.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and the arms every LateUpdate
    public class Maim : Ability
    {
        [SerializeField] float damage = 8f;
        [SerializeField] float range = 2.6f;
        [SerializeField] float arcDegrees = 100f;
        [SerializeField] float slowFraction = 0.3f;
        [SerializeField] float maimDuration = 2.5f;
        [SerializeField] float windupDuration = 0.15f;
        [SerializeField] float swingDuration = 0.1f;
        [SerializeField] float recoverDuration = 0.15f;
        [SerializeField] float energyPerHit = 8f;

        [Header("EX: The Butcher's Nails")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float nailsDuration = 6f;
        [SerializeField] Color slashColor = new Color(0.6f, 0.02f, 0.02f, 0.9f);

        [Header("Pose")]
        [SerializeField] float crouchLean = 22f;
        [SerializeField] float armsRaise = 50f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_L", "UpperArm_R" };

        LimbPose pose;
        Energy energy;
        bool active;
        bool posed;
        bool strikePending;
        float windupEndTime;
        float swingEndTime;
        float recoverEndTime;
        float recoverFromLean;
        float recoverFromArms;
        float lastLean;
        float lastArms;
        readonly HashSet<Health> struck = new HashSet<Health>();

        public override string DisplayName => "Maim";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExDescription => "The Butcher's Nails";
        public override string ExName => "Butcher's Nails";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override void Update()
        {
            base.Update();

            if (strikePending && !IsReplica && IsDisabled) InterruptSelf();

            if (strikePending && Time.time >= windupEndTime)
            {
                strikePending = false;
                DealDamage();
                SlashEffect.Spawn(transform.position, Aim.AimDirection, range, arcDegrees, swingDuration * 2f, slashColor);
            }
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.E);
        }

        protected override void Activate()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
            if (ExActivation)
            {
                // The Butcher's Nails: no chop, the Berserker drives the nails in and goes into a rage.
                Enraged.Apply(gameObject, nailsDuration);
                SlashEffect.Spawn(transform.position, Aim.AimDirection, 2.2f, 360f, 0.4f, new Color(1f, 0.1f, 0.05f, 0.9f));
                return;
            }

            active = true;
            strikePending = true;
            windupEndTime = Time.time + windupDuration;
            swingEndTime = windupEndTime + swingDuration;
            recoverEndTime = swingEndTime + recoverDuration;
            recoverFromLean = crouchLean;
            recoverFromArms = -armsRaise * 0.4f;
        }

        protected override void Cancel()
        {
            if (!active) return;

            if (strikePending) RefundCooldown();
            active = false;
            strikePending = false;
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
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(Aim.AimDirection, to) > arcDegrees * 0.5f) continue;

                float dealt = damage * BerserkerUpgradeEffects.DamageScale(gameObject, target);
                if (!target.TakeDamage(dealt, gameObject)) continue;
                if (energy != null) energy.Gain(energyPerHit);
                BerserkerUpgradeEffects.RegisterHit(gameObject, dealt);
                BloodImpact.Play(target.transform.position + Vector3.up, Aim.AimDirection);
                if (target.IsDead) continue;

                // Hamstring: a deeper slow, and the wound bleeds.
                bool hamstring = UpgradeState.Has(gameObject, BerserkerUpgrade.Hamstring);
                Maimed.Apply(target.gameObject, maimDuration, hamstring ? Mathf.Max(slowFraction, BerserkerUpgrade.HamstringSlowFraction) : slowFraction);
                if (hamstring) Bleeding.Apply(target.gameObject, gameObject, 3f, 4f);
            }
        }

        // Wind-up: both axes hauled up and back. Swing: crouch and chop low and forward. Recovery: ease back.
        void ApplyPose()
        {
            float now = Time.time;
            float lean;
            float arms;

            if (active && now < windupEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (windupEndTime - now) / windupDuration);
                lean = Mathf.Lerp(0f, -8f, t);
                arms = Mathf.Lerp(0f, -armsRaise, t);
            }
            else if (active && now < swingEndTime)
            {
                float t = 1f - (swingEndTime - now) / swingDuration;
                t = 1f - (1f - t) * (1f - t);
                lean = Mathf.Lerp(-8f, crouchLean, t);
                arms = Mathf.Lerp(-armsRaise, armsRaise * 0.4f, t);
            }
            else if (now < recoverEndTime)
            {
                active = false;
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - now) / recoverDuration);
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
                lastLean = lastArms = 0f;
                return;
            }

            lastLean = lean;
            lastArms = arms;
            posed = true;

            pose.Reset();
            pose.Rotate("Spine", transform.right, lean);
            pose.Rotate("UpperArm_L", transform.right, arms);
            pose.Rotate("UpperArm_R", transform.right, arms);
        }
    }
}
