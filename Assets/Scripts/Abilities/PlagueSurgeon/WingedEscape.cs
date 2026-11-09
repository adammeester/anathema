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
        [SerializeField] float wingSpan = 2.6f;
        [SerializeField] Color wingColor = new Color(0.72f, 0.85f, 0.4f, 0.85f);
        const float SweepBack = 0.25f;

        CharacterController controller;
        Transform wingLeft;
        Transform wingRight;
        Material wingMaterial;
        Material veinMaterial;
        bool flying;
        Vector3 direction;
        float speed;
        float endTime;

        protected override float WindupDuration => 0.3f;
        protected override Color WindupColor => wingColor;

        protected override void OnWindupStart() => PlagueSurgeonPose.For(gameObject).Wings(WindupDuration + 0.1f);

        protected override void OnWindupCancel() => PlagueSurgeonPose.For(gameObject).Release();

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
            if (veinMaterial != null) Destroy(veinMaterial);
        }

        void BuildWings()
        {
            wingMaterial = VisualUtil.UnlitMaterial(Color.white);
            veinMaterial = VisualUtil.UnlitMaterial(new Color(0.18f, 0.22f, 0.05f, 0.85f));
            wingLeft = MakeWing("Wing L", -1f);
            wingRight = MakeWing("Wing R", 1f);
        }

        // A fly's wing pair (long forewing + short hindwing) pivoting at the shoulder blades.
        Transform MakeWing(string name, float side)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(side * 0.2f, 1.4f, -0.25f);

            AddWingPanel(pivot, side, wingSpan, 1.2f, 0.2f, 0f, 3);
            AddWingPanel(pivot, side, wingSpan * 0.65f, 0.8f, -0.5f, -18f * side, 2);

            pivot.gameObject.SetActive(false);
            return pivot;
        }

        // One membrane: a teardrop fan from the root, plus dark veins running along it.
        void AddWingPanel(Transform pivot, float side, float span, float chord, float zOffset, float yaw, int veins)
        {
            var panel = new GameObject("Membrane");
            panel.transform.SetParent(pivot, false);
            panel.transform.localPosition = new Vector3(0f, 0f, zOffset);
            panel.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            const int segments = 10;
            var verts = new Vector3[(segments + 1) * 2];
            var colors = new Color[verts.Length];
            var tris = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                // Narrow at the root, widest around 40% out, rounded tip; swept back along the span.
                float half = chord * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f)), 0.8f);
                float centre = -SweepBack * t;
                float x = side * span * t;
                verts[i * 2] = new Vector3(x, 0f, centre + half);
                verts[i * 2 + 1] = new Vector3(x, 0f, centre - half);
                Color c = wingColor;
                c.a *= Mathf.Lerp(1f, 0.55f, t); // clearer toward the tip
                colors[i * 2] = colors[i * 2 + 1] = c;
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                int k = i * 6;
                tris[k] = a; tris[k + 1] = c; tris[k + 2] = b;
                tris[k + 3] = b; tris[k + 4] = c; tris[k + 5] = d;
            }
            var mesh = new Mesh { name = "FlyWing", vertices = verts, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = panel.AddComponent<MeshRenderer>();
            mr.sharedMaterial = wingMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Veins: thin dark bars fanning from the root toward the rim.
            for (int v = 0; v < veins; v++)
            {
                float f = veins == 1 ? 0f : v / (float)(veins - 1) * 2f - 1f; // -1..1 across the chord
                var tip = new Vector3(side * span * 0.92f, 0f, -SweepBack * 0.92f + f * chord * 0.2f);
                var vein = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(vein.GetComponent<Collider>());
                var vr = vein.GetComponent<Renderer>();
                vr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                vr.sharedMaterial = veinMaterial;
                vein.transform.SetParent(panel.transform, false);
                vein.transform.localPosition = tip * 0.5f;
                vein.transform.localRotation = Quaternion.LookRotation(tip.normalized, Vector3.up);
                vein.transform.localScale = new Vector3(0.03f, 0.03f, tip.magnitude);
            }
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
