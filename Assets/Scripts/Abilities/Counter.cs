using System;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities
{
    // Generic counter every character has: press Space to raise a guard for a short window. The first hit taken in
    // that window is negated, then OnCounterSuccess runs. Press C to drop the guard early.
    // Characters subclass this (in Abilities/<Character>/) to supply their own response to a successful counter.
    [DefaultExecutionOrder(100)] // after the walk cycle and other abilities have posed the skeleton this frame
    public abstract class Counter : Ability
    {
        [SerializeField] float duration = 1.2f;
        [SerializeField] float guardRadius = 1.6f;
        [SerializeField] Color guardColor = new Color(0.4f, 0.7f, 1f, 0.45f);

        [Header("Guard pose")]
        [SerializeField] float leanBackDegrees = 14f;
        [SerializeField] float poseBlendTime = 0.1f;
        [SerializeField] float glowScale = 1.06f;
        [SerializeField] Color glowColor = new Color(0.3f, 0.6f, 1f, 0.4f);

        GameObject guardVisual;
        Transform model;
        GameObject glowShell;
        Transform[] sourceBones;
        Transform[] glowBones;
        Material glowMaterial;
        float startTime;
        float endTime;

        public bool IsGuarding { get; private set; }

        public override string DisplayName => "Counter";
        public override string KeyLabel => "Space";

        // Applied only when added in the editor, so the scene builder gets a longer cooldown than the base default.
        void Reset()
        {
            cooldown = 4f;
        }

        protected override void Awake()
        {
            base.Awake();

            guardVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            guardVisual.name = "Counter Guard";
            Destroy(guardVisual.GetComponent<Collider>());
            guardVisual.transform.SetParent(transform, false);
            guardVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            guardVisual.transform.localScale = new Vector3(guardRadius * 2f, 0.02f, guardRadius * 2f);
            guardVisual.GetComponent<Renderer>().sharedMaterial = VisualUtil.UnlitMaterial(guardColor);
            guardVisual.SetActive(false);

            BuildGlowShell();
        }

        // A slightly enlarged translucent copy of the model, shown while guarding.
        void BuildGlowShell()
        {
            model = transform.Find("Model");
            if (model == null) return;

            glowMaterial = VisualUtil.UnlitMaterial(glowColor, 3000);
            sourceBones = model.GetComponentsInChildren<Transform>(true);
            glowShell = Instantiate(model.gameObject, model);
            glowBones = glowShell.GetComponentsInChildren<Transform>(true);
            glowShell.name = "Counter Glow";
            glowShell.transform.localPosition = Vector3.zero;
            glowShell.transform.localRotation = Quaternion.identity;
            glowShell.transform.localScale = Vector3.one * glowScale;

            foreach (Component c in glowShell.GetComponentsInChildren<Component>(true))
            {
                if (c is Renderer r)
                {
                    var materials = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = glowMaterial;
                    r.sharedMaterials = materials;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else if (c is Collider col) Destroy(col);
                // The copy must be a dumb mesh: scripts (e.g. the walk cycle) would pose its skeleton and fight the sync below.
                else if (c is Behaviour behaviour) Destroy(behaviour);
            }

            glowShell.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();
            if (IsGuarding && Time.time >= endTime) Cancel();
        }

        // Leans the whole model back and mirrors the skeleton pose onto the glow shell.
        void LateUpdate()
        {
            if (!IsGuarding) return;

            float blend = Mathf.Clamp01((Time.time - startTime) / poseBlendTime);
            if (model != null)
                model.localRotation = Quaternion.Euler(-leanBackDegrees * Mathf.SmoothStep(0f, 1f, blend), 0f, 0f);

            // The glow shell has its own copy of the skeleton, so mirror the real one's pose onto it. Both hierarchies
            // were cloned from the same model, so they line up index for index (index 0 is each hierarchy's root).
            if (glowBones != null && glowBones.Length == sourceBones.Length)
            {
                for (int i = 1; i < glowBones.Length; i++)
                    glowBones[i].SetLocalPositionAndRotation(sourceBones[i].localPosition, sourceBones[i].localRotation);
            }

            if (glowMaterial != null)
            {
                Color c = glowColor;
                c.a = glowColor.a * (0.75f + 0.25f * Mathf.Sin((Time.time - startTime) * 14f)) * blend;
                glowMaterial.color = c;
            }
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.Space);
        }

        protected override void Activate()
        {
            IsGuarding = true;
            startTime = Time.time;
            endTime = startTime + duration;
            guardVisual.SetActive(true);
            if (glowShell != null) glowShell.SetActive(true);
            Health.DamageModifier = OnIncomingDamage;
        }

        protected override void Cancel()
        {
            if (!IsGuarding) return;

            IsGuarding = false;
            guardVisual.SetActive(false);
            if (glowShell != null) glowShell.SetActive(false);
            if (model != null) model.localRotation = Quaternion.identity;
            if (Health.DamageModifier == (Func<float, GameObject, float>)OnIncomingDamage) Health.DamageModifier = null;
        }

        float OnIncomingDamage(float amount, GameObject source)
        {
            Cancel();
            OnCounterSuccess(source);
            return 0f;
        }

        // The character-specific payoff. `attacker` is whoever dealt the negated hit (may be null).
        protected abstract void OnCounterSuccess(GameObject attacker);
    }
}
