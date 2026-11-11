using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Short-lived line-renderer effects shared by the Psyker's abilities.
    public static class PsykerFx
    {
        public static readonly Color Cyan = new Color(0.35f, 0.9f, 1f, 0.9f);

        // A flat ring on the ground that expands from `from` to `to` radius while fading out.
        public static void Ring(Vector3 center, float from, float to, float duration, Color color, float width = 0.18f)
        {
            var fx = new GameObject("Psyker Ring").AddComponent<FadingShape>();
            fx.Init(center + Vector3.up * 0.1f, Vector3.forward, 360f, from, to, duration, color, width);
        }

        // A flat arc that sweeps outwards in front of the caster (currently unused).
        public static void Arc(Vector3 origin, Vector3 direction, float halfAngle, float range, float duration, Color color)
        {
            var fx = new GameObject("Psyker Arc").AddComponent<FadingShape>();
            fx.Init(origin + Vector3.up * 1.0f, direction, halfAngle * 2f, range * 0.3f, range, duration, color, 0.25f);
        }

        // A white lightning bolt through a list of points (e.g. caster -> target -> target...). Re-jitters every frame.
        public static void Bolt(System.Collections.Generic.List<Vector3> points, float duration = 0.3f)
        {
            if (points.Count < 2) return;
            new GameObject("Psyker Bolt").AddComponent<LightningBolt>().Init(points, duration);
            Sfx.Play(Sfx.ElectricShock, 0.5f, 0.95f, 1.1f);
        }

        class LightningBolt : MonoBehaviour
        {
            const int PerSegment = 8;

            Vector3[] anchors;
            LineRenderer[] lines;
            Material[] materials;
            float[] baseWidth;
            float[] alpha;
            float duration, start;

            public void Init(System.Collections.Generic.List<Vector3> points, float time)
            {
                anchors = points.ToArray();
                duration = Mathf.Max(0.05f, time);
                start = Time.time;

                // glow, two jagged arcs, white-hot core
                float[] widths = { 0.5f, 0.16f, 0.16f, 0.07f };
                float[] alphas = { 0.3f, 0.9f, 0.9f, 1f };
                lines = new LineRenderer[widths.Length];
                materials = new Material[widths.Length];
                baseWidth = widths;
                alpha = alphas;
                for (int i = 0; i < widths.Length; i++)
                {
                    var child = new GameObject("Layer");
                    child.transform.SetParent(transform, false);
                    var line = child.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = (anchors.Length - 1) * PerSegment + 1;
                    line.numCapVertices = 3;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    materials[i] = VisualUtil.UnlitMaterial(new Color(1f, 1f, 1f, alphas[i]), 3000 + i);
                    line.sharedMaterial = materials[i];
                    lines[i] = line;
                }
                Update();
            }

            void OnDestroy()
            {
                if (materials != null) foreach (Material m in materials) if (m != null) Destroy(m);
            }

            void Update()
            {
                float t = (Time.time - start) / duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    int n = lines[i].positionCount;
                    float jitter = i == 0 ? 0.03f : i == 3 ? 0.04f : 0.35f;
                    for (int k = 0; k < n; k++)
                    {
                        int seg = Mathf.Min(k / PerSegment, anchors.Length - 2);
                        float f = (k - seg * PerSegment) / (float)PerSegment;
                        Vector3 p = Vector3.Lerp(anchors[seg], anchors[seg + 1], f);
                        bool anchor = k % PerSegment == 0 || k == n - 1;
                        if (!anchor) p += Random.insideUnitSphere * jitter;
                        lines[i].SetPosition(k, p);
                    }

                    lines[i].startWidth = lines[i].endWidth = baseWidth[i] * (1f - t * 0.7f) * Random.Range(0.7f, 1.2f);
                    Color c = Color.white;
                    c.a = alpha[i] * (1f - t) * (Random.value < 0.2f ? 0.5f : 1f);
                    materials[i].color = c;
                }
            }
        }

        class FadingShape : MonoBehaviour
        {
            const int Points = 40;

            LineRenderer line;
            Material material;
            Vector3 center, dir;
            float span, from, to, duration, start, width;
            Color color;

            public void Init(Vector3 c, Vector3 d, float spanDegrees, float r0, float r1, float time, Color col, float w)
            {
                center = c;
                dir = new Vector3(d.x, 0f, d.z).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                span = spanDegrees;
                from = r0;
                to = r1;
                duration = Mathf.Max(0.05f, time);
                color = col;
                width = w;
                start = Time.time;

                line = gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = Points;
                line.loop = span >= 359f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                material = VisualUtil.UnlitMaterial(col, 3000);
                line.sharedMaterial = material;
                Update();
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }

            void Update()
            {
                float t = (Time.time - start) / duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                float radius = Mathf.Lerp(from, to, 1f - (1f - t) * (1f - t));
                for (int i = 0; i < Points; i++)
                {
                    float f = i / (float)(span >= 359f ? Points : Points - 1);
                    float angle = span >= 359f ? f * 360f : (f - 0.5f) * span;
                    Vector3 p = center + Quaternion.AngleAxis(angle, Vector3.up) * dir * radius;
                    line.SetPosition(i, p + Vector3.up * Random.Range(-0.04f, 0.04f));
                }

                line.startWidth = line.endWidth = width * (1f - t * 0.6f);
                Color c = color;
                c.a = color.a * (1f - t);
                material.color = c;
            }
        }
    }
}
