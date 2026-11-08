using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // A crescent that fades out over the swing arc, then destroys itself.
    public class SlashEffect : MonoBehaviour
    {
        const int Segments = 24;

        Material material;
        Color color;
        float duration;
        float startTime;

        public static void Spawn(Vector3 origin, Vector3 direction, float radius, float arcDegrees, float duration, Color color)
        {
            var go = new GameObject("Slash");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(radius * 0.45f, radius, arcDegrees);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var effect = go.AddComponent<SlashEffect>();
            effect.material = VisualUtil.UnlitMaterial(color);
            renderer.sharedMaterial = effect.material;
            effect.color = color;
            effect.duration = duration;
            effect.startTime = Time.time;
        }

        void Update()
        {
            float t = (Time.time - startTime) / duration;
            if (t >= 1f)
            {
                Destroy(material);
                Destroy(gameObject);
                return;
            }

            Color c = color;
            c.a = color.a * (1f - t);
            material.color = c;
        }

        static Mesh BuildMesh(float inner, float outer, float arcDegrees)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var triangles = new int[Segments * 6];

            for (int i = 0; i <= Segments; i++)
            {
                float angle = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, i / (float)Segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                // Taper the crescent toward its tips.
                float taper = Mathf.Cos(Mathf.Lerp(-1f, 1f, i / (float)Segments) * Mathf.PI * 0.5f);
                vertices[i * 2] = dir * Mathf.Lerp(outer, inner, 1f - taper * 0.6f) + Vector3.up * 0.9f;
                vertices[i * 2 + 1] = dir * outer + Vector3.up * 0.9f;
            }

            for (int i = 0; i < Segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                // Material is the double-sided unlit sprite shader, so winding doesn't matter for visibility.
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
