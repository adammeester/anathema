using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // The jump pack's twin engines: a blue flame out of each exhaust nozzle, a blue light, and grey smoke left hanging in
    // the air behind him. Thrust (0..1) sets how long and bright the flames are and how much smoke pours out: Jump Pack
    // Descent spools it up during its wind-up, holds it at full in the air, and cuts it on landing.
    // The nozzles are the model's Jet_L / Jet_R bones, each with a Jet_*_Tip child further along the engine's axis (the
    // engines are angled down and outward); without them, fixed spots on his back pointing down.
    // Purely visual: every machine drives its own copy from the ability's wind-up and leap.
    [DefaultExecutionOrder(200)] // after the bones have been posed this frame
    public class JetPackFx : MonoBehaviour
    {
        static readonly Color FlameOuter = new Color(0.25f, 0.55f, 1f, 0.55f);
        static readonly Color FlameCore = new Color(0.85f, 0.95f, 1f, 0.9f);
        static readonly Color Smoke = new Color(0.42f, 0.43f, 0.46f, 0.55f);

        const float FlameLength = 1.1f;   // at full thrust
        const float SmokePerSecond = 28f;  // per nozzle at full thrust

        sealed class Nozzle
        {
            public Transform bone;
            public Transform tip;         // further along the engine's axis: bone -> tip is the exhaust direction
            public Vector3 localAxis;
            public Vector3 fallback;      // offset from the character when there is no bone
            public Transform outer, core;
            public Light light;
            public float smokeDebt;
        }

        Nozzle[] nozzles;
        Material outerMaterial, coreMaterial;
        float thrust;
        float target;
        float rampSeconds = 0.1f;

        public static JetPackFx For(GameObject guard)
        {
            if (!guard.TryGetComponent(out JetPackFx fx)) fx = guard.AddComponent<JetPackFx>();
            return fx;
        }

        // Eases the flames toward `level` over `seconds`.
        public void SetThrust(float level, float seconds = 0.1f)
        {
            target = Mathf.Clamp01(level);
            rampSeconds = Mathf.Max(0.01f, seconds);
        }

        // A big billow of smoke from both nozzles at once (launch, landing, a spool that is cancelled).
        public void Puff(int perNozzle, float size)
        {
            if (nozzles == null) Build();
            foreach (Nozzle n in nozzles)
            {
                Vector3 at = NozzlePosition(n, out Vector3 down);
                for (int i = 0; i < perNozzle; i++)
                    SmokePuff.Spawn(at + down * Random.Range(0.1f, 0.5f) + Random.insideUnitSphere * 0.25f, size * Random.Range(0.8f, 1.3f),
                        Random.insideUnitSphere * 1.2f + Vector3.up * 0.4f);
            }
        }

        void Build()
        {
            Transform model = transform.Find("Model");
            outerMaterial = VisualUtil.UnlitMaterial(FlameOuter, 3010);
            coreMaterial = VisualUtil.UnlitMaterial(FlameCore, 3011);
            nozzles = new Nozzle[2];
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "L" : "R";
                var n = new Nozzle { fallback = new Vector3(i == 0 ? 0.34f : -0.34f, 0.95f, -0.44f) };
                if (model != null)
                    foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                    {
                        if (n.bone == null && t.name == "Jet_" + side) n.bone = t;
                        else if (n.tip == null && t.name == "Jet_" + side + "_Tip") n.tip = t;
                    }
                if (n.bone != null) n.localAxis = n.bone.InverseTransformDirection(Vector3.down).normalized;
                n.outer = Cone("Jet Flame " + side, outerMaterial);
                n.core = Cone("Jet Core " + side, coreMaterial);
                var lightGo = new GameObject("Jet Light " + side);
                lightGo.transform.SetParent(transform, false);
                n.light = lightGo.AddComponent<Light>();
                n.light.type = LightType.Point;
                n.light.color = new Color(0.35f, 0.6f, 1f);
                n.light.range = 3.5f;
                n.light.shadows = LightShadows.None;
                nozzles[i] = n;
            }
            Apply();
        }

        Transform Cone(string name, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.SetParent(transform, false);
            return g.transform;
        }

        Vector3 NozzlePosition(Nozzle n, out Vector3 down)
        {
            if (n.bone != null && n.tip != null && (n.tip.position - n.bone.position).sqrMagnitude > 1e-6f)
            {
                down = (n.tip.position - n.bone.position).normalized; // the engines are angled down and outward
                return n.bone.position;
            }
            if (n.bone != null)
            {
                down = n.bone.TransformDirection(n.localAxis).normalized;
                return n.bone.position;
            }
            down = Vector3.down;
            return transform.TransformPoint(n.fallback);
        }

        void LateUpdate()
        {
            if (nozzles == null)
            {
                if (target <= 0f) return;
                Build();
            }
            thrust = Mathf.MoveTowards(thrust, target, Time.deltaTime / rampSeconds);
            Apply();
        }

        void Apply()
        {
            bool on = thrust > 0.01f;
            foreach (Nozzle n in nozzles)
            {
                n.outer.gameObject.SetActive(on);
                n.core.gameObject.SetActive(on);
                n.light.enabled = on;
                if (!on) continue;

                Vector3 at = NozzlePosition(n, out Vector3 down);
                float flicker = 1f + 0.15f * Mathf.Sin(Time.time * 47f + n.fallback.x * 10f) + Random.Range(-0.08f, 0.08f);
                float length = FlameLength * thrust * flicker;
                Quaternion along = Quaternion.FromToRotation(Vector3.up, down);
                n.outer.SetPositionAndRotation(at + down * (length * 0.5f), along);
                float width = 0.26f * (0.6f + 0.4f * thrust);
                n.outer.localScale = new Vector3(width, length * 0.5f, width);
                n.core.SetPositionAndRotation(at + down * (length * 0.3f), along);
                n.core.localScale = new Vector3(0.13f, length * 0.55f, 0.13f);
                n.light.transform.position = at + down * 0.3f;
                n.light.intensity = 3f * thrust * flicker;

                // Smoke rolls off the end of the flame and hangs where it was made, so the leap leaves a trail.
                n.smokeDebt += SmokePerSecond * thrust * thrust * Time.deltaTime;
                while (n.smokeDebt >= 1f)
                {
                    n.smokeDebt -= 1f;
                    SmokePuff.Spawn(at + down * length * Random.Range(0.6f, 1f) + Random.insideUnitSphere * 0.12f,
                        Random.Range(0.35f, 0.6f), Random.insideUnitSphere * 0.4f + Vector3.up * 0.3f);
                }
            }
        }

        void OnDestroy()
        {
            if (outerMaterial != null) Destroy(outerMaterial);
            if (coreMaterial != null) Destroy(coreMaterial);
        }

        // One puff of smoke: swells, drifts and fades out.
        sealed class SmokePuff : MonoBehaviour
        {
            static Material shared;
            Material material;
            Vector3 velocity;
            float start, life, size;

            public static void Spawn(Vector3 at, float size, Vector3 velocity)
            {
                if (shared == null) shared = VisualUtil.UnlitMaterial(Smoke, 3005);
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Jet Smoke";
                Destroy(g.GetComponent<Collider>());
                var r = g.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var p = g.AddComponent<SmokePuff>();
                p.material = new Material(shared);
                r.sharedMaterial = p.material;
                p.velocity = velocity;
                p.start = Time.time;
                p.life = Random.Range(0.9f, 1.4f);
                p.size = size;
                g.transform.position = at;
                g.transform.localScale = Vector3.one * size * 0.4f;
            }

            void Update()
            {
                float t = (Time.time - start) / life;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                velocity *= 1f - 1.5f * Time.deltaTime;
                transform.position += (velocity + Vector3.up * 0.5f) * Time.deltaTime;
                transform.localScale = Vector3.one * size * Mathf.Lerp(0.4f, 1.6f, Mathf.Sqrt(t));
                Color c = Smoke;
                c.a = Smoke.a * (1f - t) * Mathf.Clamp01(t * 8f);
                material.color = c;
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }
    }
}
