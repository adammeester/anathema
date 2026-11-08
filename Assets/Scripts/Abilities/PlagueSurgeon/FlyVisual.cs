using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // The look of a plague fly, built from primitives: a banded abdomen, a hairy thorax, a head with big red compound
    // eyes, six dangling legs and two translucent wings that flap. Faces local +Z. The EX fly is gold instead of
    // sickly green. Owns its own materials and cleans them up with it.
    public class FlyVisual : MonoBehaviour
    {
        Transform wingL;
        Transform wingR;
        float phase;
        readonly List<Material> materials = new List<Material>();

        // Builds a fly under `parent` at the given overall scale (1 = about half a metre long, wings included).
        public static FlyVisual Create(Transform parent, bool ex, float scale)
        {
            var go = new GameObject("Fly Visual");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var visual = go.AddComponent<FlyVisual>();
            visual.Build(ex);
            return visual;
        }

        void Build(bool ex)
        {
            phase = Random.value * 10f;
            Color bodyColor = ex ? new Color(0.85f, 0.7f, 0.12f) : new Color(0.3f, 0.42f, 0.08f);
            Color bandColor = ex ? new Color(0.45f, 0.3f, 0.05f) : new Color(0.12f, 0.16f, 0.04f);
            Material body = Mat(bodyColor);
            Material band = Mat(bandColor);
            Material eye = Mat(new Color(0.9f, 0.1f, 0.05f));
            Material wing = Mat(new Color(0.85f, 0.95f, 0.8f, 0.45f), 3001);

            // Abdomen: a long egg with dark bands, hanging a little low behind the thorax.
            Part("Abdomen", PrimitiveType.Sphere, body, new Vector3(0f, -0.02f, -0.2f), new Vector3(0.2f, 0.19f, 0.36f));
            for (int i = 0; i < 3; i++)
                Part("Band", PrimitiveType.Sphere, band, new Vector3(0f, -0.02f, -0.12f - i * 0.1f), new Vector3(0.205f - i * 0.02f, 0.195f - i * 0.02f, 0.035f));
            Part("Stinger", PrimitiveType.Sphere, band, new Vector3(0f, -0.04f, -0.39f), new Vector3(0.05f, 0.05f, 0.1f));

            // Thorax and head.
            Part("Thorax", PrimitiveType.Sphere, band, new Vector3(0f, 0.01f, 0.02f), new Vector3(0.2f, 0.2f, 0.2f));
            Part("Head", PrimitiveType.Sphere, body, new Vector3(0f, 0f, 0.17f), new Vector3(0.15f, 0.14f, 0.13f));
            for (int s = -1; s <= 1; s += 2)
                Part("Eye", PrimitiveType.Sphere, eye, new Vector3(0.07f * s, 0.02f, 0.2f), new Vector3(0.1f, 0.11f, 0.1f));

            // Legs: thin sticks angled down and out.
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    Transform leg = Part("Leg", PrimitiveType.Cube, band, new Vector3(0.08f * s, -0.1f, 0.08f - i * 0.07f), new Vector3(0.012f, 0.16f, 0.012f));
                    leg.localRotation = Quaternion.Euler(i * 8f - 8f, 0f, 28f * s);
                }
            }

            // Wings: pivot at the thorax so rotating the pivot flaps the blade.
            wingL = Wing("Wing L", wing, 1f);
            wingR = Wing("Wing R", wing, -1f);
        }

        Transform Wing(string name, Material wing, float side)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0.06f * side, 0.1f, 0.0f);
            Part("Blade", PrimitiveType.Sphere, wing, new Vector3(0.2f * side, 0f, -0.04f), new Vector3(0.4f, 0.015f, 0.17f), pivot);
            return pivot;
        }

        Transform Part(string name, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Transform parent = null)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            p.name = name;
            Destroy(p.GetComponent<Collider>());
            Renderer r = p.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.transform.SetParent(parent != null ? parent : transform, false);
            p.transform.localPosition = position;
            p.transform.localScale = scale;
            return p.transform;
        }

        Material Mat(Color color, int renderQueue = -1)
        {
            Material m = VisualUtil.UnlitMaterial(color, renderQueue);
            materials.Add(m);
            return m;
        }

        void Update()
        {
            // Fast, slightly irregular flapping: wings sweep between up and a flat-out position.
            float flap = Mathf.Sin((Time.time + phase) * 75f);
            float angle = 10f + flap * 38f;
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        void OnDestroy()
        {
            foreach (Material m in materials)
                if (m != null) Destroy(m);
        }
    }
}
