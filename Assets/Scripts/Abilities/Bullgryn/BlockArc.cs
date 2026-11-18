using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // The translucent arc that shows where the slabshield blocks: a faint wedge on the ground plus a curved, glowing
    // curtain standing on its rim, bright at the foot and fading out toward the top. Built in local space with the arc
    // centred on +Z, around the local origin; parent it and position it to place it. Used by Brace (around him) and by
    // Living Wall (a much larger arc behind the planted shield). Flash() pulses it, e.g. when a hit is blocked.
    public class BlockArc : MonoBehaviour
    {
        const int Segments = 28;

        Material material;
        Color color;
        float flashUntil = float.NegativeInfinity;
        float baseAlpha = 1f;

        public static BlockArc Create(Transform parent, Vector3 localCentre, float radius, float arcDegrees, float height, Color color)
        {
            var go = new GameObject("Block Arc");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = Quaternion.identity;
            var arc = go.AddComponent<BlockArc>();
            arc.color = color;
            arc.Build(radius, arcDegrees, height);
            return arc;
        }

        void Build(float radius, float arcDegrees, float height)
        {
            material = VisualUtil.UnlitMaterial(Color.white, 3001);

            var mesh = new Mesh { name = "Block Arc" };
            int n = Segments + 1;
            // Ground wedge: a centre vertex plus the rim. Curtain: two rows (foot and top) along the rim.
            var verts = new Vector3[1 + n + n * 2];
            var colors = new Color[verts.Length];
            var tris = new int[Segments * 3 + Segments * 6];

            Color ground = new Color(color.r, color.g, color.b, 0.16f);
            Color groundRim = new Color(color.r, color.g, color.b, 0.3f);
            Color foot = new Color(color.r, color.g, color.b, 0.42f);
            Color top = new Color(color.r, color.g, color.b, 0f);

            verts[0] = new Vector3(0f, 0.04f, 0f);
            colors[0] = new Color(color.r, color.g, color.b, 0.04f);
            float half = arcDegrees * 0.5f * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)Segments);
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[1 + i] = d * radius + Vector3.up * 0.04f;
                colors[1 + i] = groundRim;
                verts[1 + n + i * 2] = d * radius + Vector3.up * 0.04f;
                colors[1 + n + i * 2] = foot;
                verts[1 + n + i * 2 + 1] = d * radius + Vector3.up * height;
                colors[1 + n + i * 2 + 1] = top;
            }
            colors[0] = ground;

            int t = 0;
            for (int i = 0; i < Segments; i++)
            {
                tris[t++] = 0;
                tris[t++] = 1 + i;
                tris[t++] = 2 + i;
            }
            for (int i = 0; i < Segments; i++)
            {
                int a = 1 + n + i * 2, b = a + 2;
                tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                tris[t++] = b; tris[t++] = a + 1; tris[t++] = b + 1;
            }

            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // 0..1 overall opacity (e.g. fading the wall's arc out at the end).
        public void SetAlpha(float alpha) => baseAlpha = Mathf.Clamp01(alpha);

        public void Flash(float seconds = 0.15f) => flashUntil = Time.time + seconds;

        void Update()
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
            float flash = Time.time < flashUntil ? 2.2f : 1f;
            material.color = new Color(flash, flash, flash, Mathf.Clamp01(baseAlpha * pulse * (flash > 1f ? 1.6f : 1f)));
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
        }
    }
}
