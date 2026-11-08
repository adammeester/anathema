using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Press right mouse to hurl a spinning chain axe along the aim direction. The first enemy it hits takes damage
    // and starts Bleeding: damage over time for a few seconds. Walls stop the axe. The throw leaves the hand when the
    // wind-up ends, so C cancels it before then.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes the arms every LateUpdate
    public class AxeToss : Ability
    {
        [SerializeField] float damage = 12f;
        [SerializeField] float bleedDamagePerSecond = 3f;
        [SerializeField] float bleedDuration = 5f;
        [SerializeField] float projectileSpeed = 24f;
        [SerializeField] float range = 12f;
        [SerializeField] float projectileRadius = 0.4f;
        [SerializeField] float muzzleHeight = 1f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] float windupDuration = 0.12f;
        [SerializeField] float throwDuration = 0.08f;
        [SerializeField] float recoverDuration = 0.25f;

        [Header("Pose")]
        [SerializeField] float armRaise = 85f;
        [SerializeField] float armFollowThrough = 40f;

        static readonly string[] BoneNames = { "UpperArm_R" };

        LimbPose pose;
        Energy energy;
        Material metalMaterial;
        Material bloodMaterial;
        bool active;
        bool posed;
        bool throwPending;
        float startTime;
        float lastArm;
        float recoverFromArm;
        float recoverEndTime;

        public override string DisplayName => "Toss";
        public override string KeyLabel => "RMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 6f;
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

            if (throwPending && Time.time >= startTime + windupDuration)
            {
                throwPending = false;
                Throw();
            }
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1);
        }

        protected override void Activate()
        {
            active = true;
            throwPending = true;
            startTime = Time.time;
        }

        protected override void Cancel()
        {
            if (!active) return;

            active = false;
            throwPending = false;
            recoverFromArm = lastArm;
            recoverEndTime = Time.time + recoverDuration;
        }

        void Throw()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * 0.7f;

            var go = new GameObject("Thrown Axe");
            go.transform.position = origin;
            BuildAxeVisual(go.transform);

            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit);
        }

        void OnHit(Health target)
        {
            if (energy != null) energy.Gain(energyPerHit);
            BloodImpact.Play(target.transform.position + Vector3.up, target.transform.position - transform.position, 1f, true);
            if (!target.IsDead) Bleeding.Apply(target.gameObject, gameObject, bleedDamagePerSecond, bleedDuration);
        }

        // A handle and a blade head on a spinning child, so the Projectile root keeps pointing along its path.
        void BuildAxeVisual(Transform root)
        {
            if (metalMaterial == null) metalMaterial = VisualUtil.UnlitMaterial(new Color(0.55f, 0.57f, 0.6f));
            if (bloodMaterial == null) bloodMaterial = VisualUtil.UnlitMaterial(new Color(0.45f, 0.02f, 0.02f));

            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(root, false);
            spinner.gameObject.AddComponent<AxeSpin>();

            AddPart(spinner, new Vector3(0.08f, 0.7f, 0.08f), Vector3.zero, bloodMaterial);                 // handle
            AddPart(spinner, new Vector3(0.1f, 0.3f, 0.45f), new Vector3(0f, 0.3f, 0.2f), metalMaterial);   // blade
        }

        static void AddPart(Transform parent, Vector3 scale, Vector3 localPosition, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
        }

        // Wind-up: right arm hauled back and up. Throw: whips forward. Recovery: eases back to rest.
        void ApplyPose()
        {
            float now = Time.time;
            float windupEnd = startTime + windupDuration;
            float throwEnd = windupEnd + throwDuration;
            float arm;

            if (active && now < windupEnd)
            {
                float t = Mathf.SmoothStep(0f, 1f, (now - startTime) / windupDuration);
                arm = Mathf.Lerp(0f, -armRaise, t);
            }
            else if (active && now < throwEnd)
            {
                float t = (now - windupEnd) / throwDuration;
                arm = Mathf.Lerp(-armRaise, armFollowThrough, t);
            }
            else if (active)
            {
                // Throw finished: ease back from the follow-through.
                active = false;
                recoverFromArm = armFollowThrough;
                recoverEndTime = now + recoverDuration;
                arm = armFollowThrough;
            }
            else if (now < recoverEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - now) / recoverDuration);
                arm = Mathf.Lerp(recoverFromArm, 0f, t);
            }
            else
            {
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                lastArm = 0f;
                return;
            }

            lastArm = arm;
            posed = true;

            pose.Reset();
            pose.Rotate("UpperArm_R", transform.right, arm);
        }
    }
}
