using Anathema.Combat;
using UnityEngine;

namespace Anathema.Net
{
    // The prep area shown between rounds: a pulsing barrier ring and floor disc around the local fighter's spawn.
    // PlayerMovement keeps the fighter inside it (NetGame.PrepRadius). Created when prep starts, destroyed when the round begins.
    public class PrepArea : MonoBehaviour
    {
        const int Points = 64;
        static readonly Color Edge = new Color(0.95f, 0.75f, 0.30f, 0.9f);
        static readonly Color Floor = new Color(0.95f, 0.75f, 0.30f, 0.10f);

        LineRenderer ring;
        Material ringMaterial;
        Material floorMaterial;

        public static PrepArea Show(Vector3 centre)
        {
            var go = new GameObject("Prep Area");
            go.transform.position = new Vector3(centre.x, 0.12f, centre.z);
            return go.AddComponent<PrepArea>();
        }

        void Awake()
        {
            float radius = NetGame.PrepRadius;

            ringMaterial = VisualUtil.UnlitMaterial(Edge);
            var ringObject = new GameObject("Barrier");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Points;
            ring.startWidth = ring.endWidth = 0.18f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.sharedMaterial = ringMaterial;
            for (int i = 0; i < Points; i++)
            {
                float a = i / (float)Points * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }

            floorMaterial = VisualUtil.UnlitMaterial(Floor);
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Floor";
            Destroy(disc.GetComponent<Collider>());
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disc.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            disc.transform.SetParent(transform, false);
            disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        }

        void Update()
        {
            Color c = Edge;
            c.a = Edge.a * (0.55f + 0.45f * Mathf.Sin(Time.time * 4f));
            ringMaterial.color = c;
        }

        void OnDestroy()
        {
            if (ringMaterial != null) Destroy(ringMaterial);
            if (floorMaterial != null) Destroy(floorMaterial);
        }
    }
}
