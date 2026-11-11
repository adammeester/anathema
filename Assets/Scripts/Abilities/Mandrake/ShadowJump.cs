using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // Space: the Mandrake steps into one shadow and out of another at the cursor (pulled in to max range; it passes over
    // walls but never lands inside one). Arriving charges its blade: the next strike within a few seconds (Glimmersteel or
    // Ambush) deals bonus damage. Jumping doesn't break stealth, and while hidden from a viewer it leaves no flash there.
    public class ShadowJump : Ability
    {
        [SerializeField] float range = 7f;
        [SerializeField] float bonusDamage = 10f;
        [SerializeField] float bonusWindow = 3f;

        CharacterController controller;
        float bonusUntil = float.NegativeInfinity;
        GameObject aura;
        Material auraMaterial;

        public override string DisplayName => "Shadow Jump";
        public override string KeyLabel => "Space";
        public override float MovementRange => range;

        // Online: the owner publishes the charge so every machine shows the glowing blade aura.
        public override bool ShowsEmpowered => Time.time < bonusUntil;

        public override void ShowReplicaEmpowered(bool on)
        {
            if (!IsReplica) return;
            bonusUntil = on ? float.PositiveInfinity : float.NegativeInfinity;
        }

        void Reset()
        {
            cooldown = 9f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();

            aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aura.name = "Shadow Charge Aura";
            Destroy(aura.GetComponent<Collider>());
            aura.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraMaterial = VisualUtil.UnlitMaterial(MandrakeKit.Baleflame, 3001);
            aura.GetComponent<Renderer>().sharedMaterial = auraMaterial;
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            aura.transform.localScale = new Vector3(1.9f, 0.01f, 1.9f);
            aura.SetActive(false);
        }

        void OnDestroy()
        {
            if (auraMaterial != null) Destroy(auraMaterial);
        }

        // Owner: the bonus damage for the strike being thrown now (spends the charge), or 0.
        public float ConsumeBonus()
        {
            if (Time.time >= bonusUntil) return 0f;
            bonusUntil = float.NegativeInfinity;
            return bonusDamage;
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Space);

        protected override void Activate()
        {
            Vector3 from = transform.position;
            Vector3 dir = Aim.AimPoint - from;
            dir.y = 0f;
            float distance = Mathf.Min(range, dir.magnitude);
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            distance = LandingSpot.Clear(transform, from, dir, distance);
            Vector3 to = from + dir * distance;

            if (!IsReplica && distance >= 0.5f)
            {
                if (controller != null) controller.enabled = false;
                transform.position = to;
                if (controller != null) controller.enabled = true;
            }

            if (!Stealthed.HiddenHere(gameObject) && distance >= 0.5f) AbilityFx.Teleport(from, to, MandrakeKit.Baleflame);
            if (!IsReplica) bonusUntil = Time.time + bonusWindow;
        }

        protected override void Update()
        {
            base.Update();
            bool on = ShowsEmpowered && !Health.IsDead;
            if (aura.activeSelf != on) aura.SetActive(on);
            if (on)
            {
                Color c = MandrakeKit.Baleflame;
                c.a *= 0.45f + 0.35f * Mathf.Sin(Time.time * 12f);
                auraMaterial.color = c;
            }
        }

        protected override void Cancel()
        {
        }
    }
}
