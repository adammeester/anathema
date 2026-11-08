using System.Collections.Generic;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Hold left mouse to hack with the chain axes, left and right in turn. Each chop winds the working arm up,
    // lands its damage when the wind-up ends, then follows through. Pressing C during the wind-up cancels it.
    // The pose is driven through the skeleton (spine twist plus the chopping arm); the legs are never touched.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and the arms every LateUpdate
    public class AxeChop : Ability
    {
        [SerializeField] float damage = 9f;
        [SerializeField] float range = 2.4f;
        [SerializeField] float arcDegrees = 100f;
        [SerializeField] float windupDuration = 0.1f;
        [SerializeField] float swingDuration = 0.1f;
        [SerializeField] float recoverDuration = 0.3f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] Color slashColor = new Color(1f, 0.25f, 0.15f, 0.8f);

        [Header("Empowered (after a successful counter)")]
        [SerializeField] float empoweredDamageMultiplier = 2f;
        [SerializeField] float empoweredRangeMultiplier = 1.35f;
        [SerializeField] Color empoweredColor = new Color(1f, 0.85f, 0.3f, 0.95f);

        [Header("Pose")]
        [SerializeField] float spineTwist = 28f;
        [SerializeField] float armRaise = 65f;
        [SerializeField] float armFollowThrough = 15f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_L", "UpperArm_R" };

        LimbPose pose;
        Energy energy;
        bool active;
        bool posed;
        bool strikePending;
        bool leftArm;
        bool empowered;         // the next chop is empowered
        bool strikeEmpowered;   // the chop in flight is empowered
        GameObject aura;
        Material auraMaterial;
        float windupEndTime;
        float swingEndTime;
        float recoverEndTime;
        float recoverFromTwist;
        float recoverFromArm;
        float lastTwist;
        float lastArm;
        readonly HashSet<Health> struck = new HashSet<Health>();

        public override string DisplayName => "Chop";
        public override string KeyLabel => "LMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 0.75f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);

            // Ground ring under the Berserker while the next chop is empowered.
            aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aura.name = "Empowered Aura";
            Destroy(aura.GetComponent<Collider>());
            aura.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraMaterial = VisualUtil.UnlitMaterial(empoweredColor);
            aura.GetComponent<Renderer>().sharedMaterial = auraMaterial;
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = new Vector3(0f, 0.16f, 0f); // above the arena's visible floor discs
            aura.transform.localScale = new Vector3(2.2f, 0.01f, 2.2f);
            aura.SetActive(false);
        }

        // Makes the next chop hit harder and reach further. Called by the counter's payoff.
        public void Empower()
        {
            empowered = true;
            aura.SetActive(true);
        }

        protected override void Update()
        {
            base.Update();

            if (empowered)
            {
                Color c = empoweredColor;
                c.a = empoweredColor.a * (0.45f + 0.35f * Mathf.Sin(Time.time * 10f));
                auraMaterial.color = c;
            }

            if (strikePending && Time.time >= windupEndTime)
            {
                strikePending = false;
                DealDamage();
                float reach = strikeEmpowered ? range * empoweredRangeMultiplier : range;
                SlashEffect.Spawn(transform.position, Aim.AimDirection, reach, arcDegrees, swingDuration * 2f,
                    strikeEmpowered ? empoweredColor : slashColor);
                strikeEmpowered = false;
            }
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButton(0);
        }

        protected override void Activate()
        {
            leftArm = !leftArm;
            strikeEmpowered = empowered;
            empowered = false;
            aura.SetActive(false);
            strikePending = true;
            active = true;
            windupEndTime = Time.time + windupDuration;
            swingEndTime = windupEndTime + swingDuration;
            recoverEndTime = swingEndTime + recoverDuration;
            recoverFromTwist = SwingTwist();
            recoverFromArm = armFollowThrough;
        }

        protected override void Cancel()
        {
            if (!active) return;

            // A chop cancelled before it lands gives its empowerment back.
            if (strikePending && strikeEmpowered) Empower();
            strikeEmpowered = false;

            active = false;
            strikePending = false;
            recoverFromTwist = lastTwist;
            recoverFromArm = lastArm;
            recoverEndTime = Time.time + recoverDuration;
        }

        // Left axe chops across to the right and vice versa, so the spine winds toward the working side first.
        float SwingTwist()
        {
            return spineTwist * (leftArm ? -1f : 1f);
        }

        void DealDamage()
        {
            struck.Clear();
            Vector3 origin = transform.position;
            float reach = strikeEmpowered ? range * empoweredRangeMultiplier : range;
            float dealt = strikeEmpowered ? damage * empoweredDamageMultiplier : damage;
            Collider[] colliders = Physics.OverlapSphere(origin + Vector3.up, reach, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider c in colliders)
            {
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;

                Vector3 to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(Aim.AimDirection, to) > arcDegrees * 0.5f) continue;

                if (!target.TakeDamage(dealt, gameObject)) continue;
                if (energy != null) energy.Gain(energyPerHit);
                BloodImpact.Play(target.transform.position + Vector3.up, Aim.AimDirection, strikeEmpowered ? 1f : 0.7f);
            }
        }

        void ApplyPose()
        {
            float now = Time.time;
            float twist;
            float arm;

            if (active && now < windupEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (windupEndTime - now) / windupDuration);
                twist = Mathf.Lerp(0f, -SwingTwist(), t);
                arm = Mathf.Lerp(0f, -armRaise, t);
            }
            else if (active && now < swingEndTime)
            {
                float t = 1f - (swingEndTime - now) / swingDuration;
                t = 1f - (1f - t) * (1f - t);
                twist = Mathf.Lerp(-SwingTwist(), SwingTwist(), t);
                arm = Mathf.Lerp(-armRaise, armFollowThrough, t);
            }
            else if (now < recoverEndTime)
            {
                active = false;
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - now) / recoverDuration);
                twist = Mathf.Lerp(recoverFromTwist, 0f, t);
                arm = Mathf.Lerp(recoverFromArm, 0f, t);
            }
            else
            {
                active = false;
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                lastTwist = 0f;
                lastArm = 0f;
                return;
            }

            lastTwist = twist;
            lastArm = arm;
            posed = true;

            // Parent bones first: the arm's world rotation already includes the spine's.
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, twist);
            pose.Rotate(leftArm ? "UpperArm_L" : "UpperArm_R", transform.right, arm);
        }
    }
}
