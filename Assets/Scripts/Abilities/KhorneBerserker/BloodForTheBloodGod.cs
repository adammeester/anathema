using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Ultimate (R): Blood for the Blood God! Costs a full energy bar. The Berserker roars and braces for three quarters of a second
    // (rooted, with a ground telegraph of the charge lane that follows the aim and fills as the wind-up builds), then
    // charges a long way forward along the aim direction, tearing a blood fissure into the ground behind him. Every
    // enemy he passes through takes heavy damage once, is stunned and starts Bleeding (same as the axe toss).
    // Pressing C (or dying) during the wind-up cancels it and refunds the energy. Once the charge has started, C
    // stops it short and the energy is not refunded.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and the arms every LateUpdate
    public class BloodForTheBloodGod : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float windupDuration = 0.75f;
        [SerializeField] float chargeDistance = 14f;
        [SerializeField] float chargeDuration = 0.5f;
        [SerializeField] float hitRadius = 1.7f;
        [SerializeField] float damage = 38f;
        [SerializeField] float stunDuration = 1.5f;
        [SerializeField] float bleedDamagePerSecond = 3f; // same bleed as the axe toss
        [SerializeField] float bleedDuration = 5f;
        [SerializeField] float fissureWidth = 1.6f;
        [SerializeField] Color telegraphColor = new Color(1f, 0.1f, 0.05f, 0.15f);
        [SerializeField] Color fillColor = new Color(1f, 0.15f, 0.05f, 0.4f);

        [Header("Pose")]
        [SerializeField] float braceLean = -12f;     // leans back while winding up
        [SerializeField] float braceArms = -60f;     // axes hauled overhead
        [SerializeField] float chargeLean = 30f;
        [SerializeField] float armsBack = 65f;

        const float RecoverDuration = 0.25f;
        const float ChargePoseBlend = 0.1f;
        const float TelegraphHeight = 0.15f; // just above the arena's visible floor discs

        static readonly string[] BoneNames = { "Spine", "UpperArm_L", "UpperArm_R" };

        CharacterController controller;
        LimbPose pose;
        Energy energy;
        BloodFissure fissure;
        Transform telegraph;
        Transform telegraphFill;
        AudioSource roar;
        bool winding;
        bool charging;
        bool posed;
        float windupStart;
        float startTime;
        float travelled;
        Vector3 direction;
        Vector3 origin;
        float recoverEndTime;
        float recoverFromLean;
        float recoverFromArms;
        float lastLean;
        float lastArms;
        float chargeFromLean;
        float chargeFromArms;
        readonly HashSet<Health> struck = new HashSet<Health>();

        protected override bool CooldownStartsOnStrike => true;
        public override bool RootsMovement => winding;

        public override string DisplayName => "Blood God";
        public override string KeyLabel => "R";

        // The HUD greys the slot out until the bar is full.
        public override bool Available => energy != null && energy.Current >= energyCost
                                          && !Silenced.IsSilenced(gameObject) && !Stunned.IsStunned(gameObject);

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);

            telegraph = CreateStrip("Blood God Telegraph", telegraphColor);
            telegraphFill = CreateStrip("Blood God Charge", fillColor);
        }

        void OnDestroy()
        {
            foreach (Transform t in new[] { telegraph, telegraphFill })
                if (t != null) Destroy(t.gameObject);
        }

        static Transform CreateStrip(string name, Color color)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            Destroy(strip.GetComponent<Collider>());
            var renderer = strip.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = VisualUtil.UnlitMaterial(color);
            strip.SetActive(false);
            return strip.transform;
        }

        // Lays a strip on the ground from the player along `dir`, `length` long.
        void SetStrip(Transform strip, Vector3 dir, float length, float width, float lift)
        {
            Vector3 start = transform.position;
            strip.position = new Vector3(start.x, TelegraphHeight + lift, start.z) + dir * (length * 0.5f);
            strip.rotation = Quaternion.LookRotation(dir, Vector3.up);
            strip.localScale = new Vector3(width, 0.01f, Mathf.Max(0.01f, length));
        }

        protected override void Update()
        {
            base.Update();

            if (winding) UpdateWindup();
            else if (charging) UpdateCharge();
        }

        void UpdateWindup()
        {
            float t = Mathf.Clamp01((Time.time - windupStart) / windupDuration);
            Vector3 dir = Aim.AimDirection;
            float width = hitRadius * 2f;
            SetStrip(telegraph, dir, chargeDistance, width, 0f);
            SetStrip(telegraphFill, dir, chargeDistance * t, width, 0.005f);

            if (t >= 1f) Release();
        }

        void UpdateCharge()
        {
            float elapsed = Time.time - startTime;
            float remainingDistance = chargeDistance - travelled;
            float step = Mathf.Min(chargeDistance / chargeDuration * Time.deltaTime, remainingDistance);

            Vector3 before = transform.position;
            // On a replica the movement arrives through the network position, so only track the distance.
            if (IsReplica) { }
            else if (controller != null) controller.Move(direction * step);
            else transform.position += direction * step;
            Vector3 moved = IsReplica ? direction * step : transform.position - before;
            moved.y = 0f;
            travelled += moved.magnitude;

            HitAlong(before, transform.position);
            if (fissure != null) fissure.Reveal(Vector3.Dot(transform.position - origin, direction) + 1f);

            // Done on arriving, on timing out, or when a wall stops the charge dead.
            bool blocked = step > 0.001f && moved.magnitude < step * 0.2f;
            if (travelled >= chargeDistance - 0.01f || elapsed >= chargeDuration * 1.5f || blocked) EndCharge();
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;
        }

        protected override void Activate()
        {
            if (energy == null || !energy.TrySpend(energyCost))
            {
                RefundCooldown();
                return;
            }

            winding = true;
            windupStart = Time.time;
            roar = RoarSound.Play();
            telegraph.gameObject.SetActive(true);
            telegraphFill.gameObject.SetActive(true);
        }

        // During the wind-up this undoes the cast completely; once charging it stops the charge where it is.
        protected override void Cancel()
        {
            if (winding)
            {
                winding = false;
                RoarSound.Stop(roar);
                HideTelegraph();
                recoverFromLean = lastLean;
                recoverFromArms = lastArms;
                recoverEndTime = Time.time + RecoverDuration;
                if (energy != null) energy.Gain(energyCost);
                RefundCooldown();
            }
            else if (charging)
            {
                EndCharge();
            }
        }

        void HideTelegraph()
        {
            telegraph.gameObject.SetActive(false);
            telegraphFill.gameObject.SetActive(false);
        }

        void Release()
        {
            winding = false;
            HideTelegraph();

            charging = true;
            startTime = Time.time;
            travelled = 0f;
            direction = Aim.AimDirection;
            origin = transform.position;
            chargeFromLean = lastLean;
            chargeFromArms = lastArms;
            struck.Clear();
            fissure = BloodFissure.Spawn(origin, direction, chargeDistance, fissureWidth);
            fissure.Reveal(0f);
            StartCooldown();
        }

        void EndCharge()
        {
            charging = false;
            recoverFromLean = lastLean;
            recoverFromArms = lastArms;
            recoverEndTime = Time.time + RecoverDuration;
            if (fissure != null)
            {
                fissure.Finish();
                fissure = null;
            }
        }

        // Sweeps a capsule over the ground covered this frame so a fast charge can't skip past anyone.
        void HitAlong(Vector3 from, Vector3 to)
        {
            Collider[] colliders = Physics.OverlapCapsule(from + Vector3.up, to + Vector3.up, hitRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in colliders)
            {
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;

                if (!target.TakeDamage(damage, gameObject) || target.IsDead) continue;
                Stunned.Apply(target.gameObject, stunDuration);
                Bleeding.Apply(target.gameObject, gameObject, bleedDamagePerSecond, bleedDuration);
            }
        }

        // Wind-up: lean back with both axes hauled overhead. Charge: snap forward into a hard lean with the axes
        // dragged behind. Recovery: ease back to rest.
        void ApplyPose()
        {
            float lean;
            float arms;

            if (winding)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - windupStart) / windupDuration));
                lean = braceLean * t;
                arms = braceArms * t;
            }
            else if (charging)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - startTime) / ChargePoseBlend));
                lean = Mathf.Lerp(chargeFromLean, chargeLean, t);
                arms = Mathf.Lerp(chargeFromArms, armsBack, t);
            }
            else if (Time.time < recoverEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - Time.time) / RecoverDuration);
                lean = Mathf.Lerp(recoverFromLean, 0f, t);
                arms = Mathf.Lerp(recoverFromArms, 0f, t);
            }
            else
            {
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
