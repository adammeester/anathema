using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // The Plague Surgeon has no counter. Press Space and a pair of insect wings tear out of his back: he becomes
    // invulnerable and flies in a straight line to the cursor (clamped to range), then drops back to the ground.
    // Walls stop the flight short. Pressing C (or dying) ends it early.
    // While flying he can't be hurt by anything, including gas and damage over time.
    public class WingedEscape : Ability
    {
        [SerializeField] float range = 9f;
        [SerializeField] float flightDuration = 0.4f;
        [SerializeField] float wingSpan = 1.3f;
        [SerializeField] Color wingColor = new Color(0.6f, 0.75f, 0.2f, 0.7f);

        CharacterController controller;
        Transform wingLeft;
        Transform wingRight;
        Material wingMaterial;
        bool flying;
        Vector3 direction;
        float speed;
        float endTime;

        public override string DisplayName => "Winged Escape";
        public override string KeyLabel => "Space";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 8f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            BuildWings();
        }

        void OnDestroy()
        {
            if (wingMaterial != null) Destroy(wingMaterial);
        }

        void BuildWings()
        {
            wingMaterial = VisualUtil.UnlitMaterial(wingColor);
            wingLeft = MakeWing("Wing L", -1f);
            wingRight = MakeWing("Wing R", 1f);
        }

        // A flat paddle pivoting at the shoulder blades.
        Transform MakeWing(string name, float side)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(side * 0.2f, 1.4f, -0.25f);

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(blade.GetComponent<Collider>());
            blade.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blade.GetComponent<Renderer>().sharedMaterial = wingMaterial;
            blade.transform.SetParent(pivot, false);
            blade.transform.localScale = new Vector3(wingSpan, 0.03f, 0.7f);
            blade.transform.localPosition = new Vector3(side * wingSpan * 0.5f, 0f, 0f);

            pivot.gameObject.SetActive(false);
            return pivot;
        }

        protected override void Update()
        {
            base.Update();
            if (!flying) return;

            // On a replica the movement arrives through the network position instead.
            if (!IsReplica && controller != null && controller.enabled)
                controller.Move(direction * (speed * Time.deltaTime));

            // Wing flap: a fast beat that opens up as the flight goes on.
            float flap = Mathf.Sin(Time.time * 45f) * 35f;
            wingLeft.localRotation = Quaternion.Euler(0f, 0f, 20f + flap);
            wingRight.localRotation = Quaternion.Euler(0f, 0f, -20f - flap);

            if (Time.time >= endTime) EndFlight();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.Space);
        }

        protected override void Activate()
        {
            Vector3 origin = transform.position;
            Vector3 to = Aim.AimPoint - origin;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) to = transform.forward;

            float distance = Mathf.Min(to.magnitude, range);
            direction = to.normalized;
            speed = distance / flightDuration;
            endTime = Time.time + flightDuration;
            PlagueSurgeonPose.For(gameObject).Wings(flightDuration + 0.15f);
            flying = true;

            if (!IsReplica) Health.Invulnerable = true;
            wingLeft.gameObject.SetActive(true);
            wingRight.gameObject.SetActive(true);
        }

        protected override void Cancel()
        {
            if (flying) EndFlight();
        }

        void EndFlight()
        {
            flying = false;
            Health.Invulnerable = false;
            wingLeft.gameObject.SetActive(false);
            wingRight.gameObject.SetActive(false);
        }
    }
}
