using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Cracks in the ground where something heavy has landed (Jump Pack Descent): jagged fractures radiating from the
    // impact site, each a dark gouge with a thin glowing blood-red seam, some forking as they run out. They snap open
    // in a fraction of a second, the seams' glow dies down, then the whole thing fades away. Purely visual, local to
    // whichever machine spawns it (every machine plays the landing).
    public class ImpactCracks : MonoBehaviour
    {
        const float OpenTime = 0.12f;
        const float Height = 0.04f;

        static readonly Color Gouge = new Color(0.06f, 0.03f, 0.03f, 0.9f);
        static readonly Color Seam = new Color(1f, 0.2f, 0.12f, 1f);

        LineRenderer[] gouges;
        LineRenderer[] seams;
        Vector3[][] paths;
        Material gougeMaterial;
        Material seamMaterial;
        float start;
        float duration;

        public static void Spawn(Vector3 centre, float radius, float seconds)
        {
            var go = new GameObject("Impact Cracks");
            go.transform.position = new Vector3(centre.x, centre.y + Height, centre.z);
            go.AddComponent<ImpactCracks>().Build(radius, Mathf.Max(0.5f, seconds));
        }

        void Build(float radius, float seconds)
        {
            start = Time.time;
            duration = seconds;
            gougeMaterial = VisualUtil.UnlitMaterial(Gouge, 2990);
            seamMaterial = VisualUtil.UnlitMaterial(Seam, 2995);

            int main = Random.Range(7, 10);
            var list = new System.Collections.Generic.List<Vector3[]>();
            var widths = new System.Collections.Generic.List<float>();
            float offset = Random.value * 360f;
            for (int i = 0; i < main; i++)
            {
                float angle = offset + i * 360f / main + Random.Range(-14f, 14f);
                float length = radius * Random.Range(0.75f, 1.15f);
                Vector3[] path = Jagged(Vector3.zero, angle, length, 6);
                list.Add(path);
                widths.Add(0.16f);
                // Most fractures fork once part way along.
                if (Random.value < 0.7f)
                {
                    int from = Random.Range(2, 4);
                    float forkAngle = angle + Random.Range(25f, 45f) * (Random.value < 0.5f ? -1f : 1f);
                    list.Add(Jagged(path[from], forkAngle, length * Random.Range(0.3f, 0.5f), 4));
                    widths.Add(0.09f);
                }
            }
            // A crushed ring of short splinters right under the landing.
            for (int i = 0; i < 6; i++)
            {
                float a = offset + i * 60f + Random.Range(-20f, 20f);
                Vector3 from = Dir(a) * radius * 0.25f;
                list.Add(Jagged(from, a + 90f, radius * 0.22f, 3));
                widths.Add(0.07f);
            }

            paths = list.ToArray();
            gouges = new LineRenderer[paths.Length];
            seams = new LineRenderer[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                gouges[i] = NewLine("Gouge", gougeMaterial, widths[i], paths[i].Length);
                seams[i] = NewLine("Seam", seamMaterial, widths[i] * 0.35f, paths[i].Length);
            }
            Update();
        }

        static Vector3 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        // A zig-zag fracture from `from` heading `angle` degrees for `length`, in `segments` kinks.
        static Vector3[] Jagged(Vector3 from, float angle, float length, int segments)
        {
            var points = new Vector3[segments + 1];
            points[0] = from;
            float heading = angle;
            for (int i = 1; i <= segments; i++)
            {
                heading += Random.Range(-28f, 28f);
                points[i] = points[i - 1] + Dir(heading) * (length / segments);
            }
            return points;
        }

        LineRenderer NewLine(string name, Material material, float width, int count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat on the ground
            line.positionCount = count;
            line.sharedMaterial = material;
            line.numCornerVertices = 1;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Wide at the root, tapering to a hairline at the tip.
            line.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, width * 0.15f));
            return line;
        }

        void Update()
        {
            float age = Time.time - start;
            if (age >= duration)
            {
                Destroy(gameObject);
                return;
            }

            // Snap open from the centre outward.
            float open = Mathf.Clamp01(age / OpenTime);
            for (int i = 0; i < paths.Length; i++)
            {
                Vector3[] p = paths[i];
                float reach = open * (p.Length - 1);
                for (int k = 0; k < p.Length; k++)
                {
                    Vector3 point = k <= reach ? p[k]
                        : Vector3.Lerp(p[Mathf.FloorToInt(reach)], p[Mathf.Min(p.Length - 1, Mathf.FloorToInt(reach) + 1)], reach - Mathf.Floor(reach));
                    // The lines lie in the object's XY plane once rotated flat, so ground XZ maps to local XY.
                    var local = new Vector3(point.x, point.z, 0f);
                    gouges[i].SetPosition(k, local);
                    seams[i].SetPosition(k, local);
                }
            }

            // The seams glow hot, then cool over the first half; everything fades over the last 40%.
            float t = age / duration;
            float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            Color s = Seam;
            s.a = Mathf.Clamp01(1f - t / 0.5f) * fade;
            seamMaterial.color = s;
            Color g = Gouge;
            g.a = Gouge.a * fade;
            gougeMaterial.color = g;
        }

        void OnDestroy()
        {
            if (gougeMaterial != null) Destroy(gougeMaterial);
            if (seamMaterial != null) Destroy(seamMaterial);
        }
    }
}
