using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Short-lived golden flashes shared by the Hospitaller's abilities.
    public class HospitallerFx : MonoBehaviour
    {
        const int RingPoints = 48;

        LineRenderer ring;
        Transform dome;
        Material ringMaterial, domeMaterial;
        Vector3 centre;
        Transform follow;
        float radius, duration, start, domeHeight;
        Color color;

        // An expanding ring on the ground with a low dome rising inside it. `follow` (optional) keeps it centred on someone.
        public static void Burst(Vector3 position, float radius, Color color, float duration = 0.5f, float domeHeight = 0.6f, Transform follow = null)
        {
            var go = new GameObject("Hospitaller Burst");
            var fx = go.AddComponent<HospitallerFx>();
            fx.centre = position;
            fx.radius = radius;
            fx.color = color;
            fx.duration = duration;
            fx.domeHeight = domeHeight;
            fx.follow = follow;
            fx.start = Time.time;
            fx.Build();
        }

        void Build()
        {
            ringMaterial = VisualUtil.UnlitMaterial(color, 3000);
            domeMaterial = VisualUtil.UnlitMaterial(new Color(color.r, color.g, color.b, 0.3f), 3001);
            ring = gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = RingPoints;
            ring.sharedMaterial = ringMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = domeMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(transform, false);
            dome = s.transform;
            Update();
        }

        void Update()
        {
            float t = (Time.time - start) / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            if (follow != null) centre = follow.position;
            float eased = 1f - (1f - t) * (1f - t);
            float r = radius * eased;
            Vector3 c = new Vector3(centre.x, centre.y + 0.08f, centre.z);
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i / (float)RingPoints * Mathf.PI * 2f;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            ring.startWidth = ring.endWidth = 0.18f * (1f - t) + 0.04f;
            ringMaterial.color = new Color(color.r, color.g, color.b, color.a * (1f - t));

            dome.position = c;
            dome.localScale = new Vector3(r * 2f, domeHeight * 2f * eased, r * 2f);
            domeMaterial.color = new Color(color.r, color.g, color.b, 0.3f * (1f - t));
        }

        void OnDestroy()
        {
            if (ringMaterial != null) Destroy(ringMaterial);
            if (domeMaterial != null) Destroy(domeMaterial);
        }
    }
}
