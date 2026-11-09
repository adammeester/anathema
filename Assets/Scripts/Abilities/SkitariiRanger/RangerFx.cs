using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // Shared visuals and the piercing line-trace used by the Ranger's rifle shots.
    public static class RangerFx
    {
        public static readonly Color Cyan = new Color(0.4f, 0.95f, 1f, 1f);
        public static readonly Color Ember = new Color(1f, 0.6f, 0.2f, 1f);

        // A bright streak from a to b that thins and fades out. `width` is the starting width of the glow layer.
        public static void Tracer(Vector3 a, Vector3 b, float width, Color color, float duration, float opacity = 1f)
        {
            new GameObject("Ranger Tracer").AddComponent<TracerFx>().Init(a, b, width, color, duration, opacity);
        }

        // Walks a fat line from `start` along `dir`, stopping at the first wall or pillar (characters do not block it).
        // `hits` receives every enemy of `team` in the line, nearest first. Returns the end point of the line.
        public static Vector3 Trace(Transform self, int team, Vector3 start, Vector3 dir, float range, float radius, List<Health> hits)
        {
            hits?.Clear();
            float length = range;
            RaycastHit[] all = Physics.SphereCastAll(start, radius, dir, range, ~0, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in all)
            {
                if (hit.transform.IsChildOf(self) || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance > 0f) length = Mathf.Min(length, hit.distance);
            }

            if (hits != null)
            {
                System.Array.Sort(all, (x, y) => x.distance.CompareTo(y.distance));
                foreach (RaycastHit hit in all)
                {
                    if (hit.distance > length) break;
                    Health target = hit.collider.GetComponentInParent<Health>();
                    if (target == null || target.Team == team || target.IsDead || hits.Contains(target)) continue;
                    hits.Add(target);
                }
            }

            return start + dir * length;
        }

        sealed class TracerFx : MonoBehaviour
        {
            LineRenderer glow, core;
            Material glowMaterial, coreMaterial;
            Color color;
            float width, duration, start, opacity = 1f;

            public void Init(Vector3 a, Vector3 b, float w, Color c, float d, float o)
            {
                opacity = o;
                width = w;
                color = c;
                duration = Mathf.Max(0.05f, d);
                start = Time.time;
                glowMaterial = VisualUtil.UnlitMaterial(new Color(c.r, c.g, c.b, 0.5f), 3000);
                coreMaterial = VisualUtil.UnlitMaterial(Color.white, 3001);
                glow = Layer("Glow", a, b, glowMaterial);
                core = Layer("Core", a, b, coreMaterial);
                Update();
            }

            LineRenderer Layer(string name, Vector3 a, Vector3 b, Material m)
            {
                var child = new GameObject(name);
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, a);
                line.SetPosition(1, b);
                line.numCapVertices = 3;
                line.sharedMaterial = m;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return line;
            }

            void Update()
            {
                float t = (Time.time - start) / duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                float fade = 1f - t;
                glow.startWidth = glow.endWidth = width * (0.4f + 0.6f * fade);
                core.startWidth = core.endWidth = width * 0.3f * fade;
                glowMaterial.color = new Color(color.r, color.g, color.b, 0.5f * fade * opacity);
                coreMaterial.color = new Color(1f, 1f, 1f, fade * opacity);
            }

            void OnDestroy()
            {
                if (glowMaterial != null) Destroy(glowMaterial);
                if (coreMaterial != null) Destroy(coreMaterial);
            }
        }
    }
}
