using UnityEngine;

namespace Anathema.Combat
{
    // The look of being stunned: the character's model judders and arcs of lightning crackle around the body.
    // Purely visual. Stunned.Apply shows it on the victim's machine, and also on the attacker's machine when the stun
    // is forwarded to a remote player, so both players see it.
    public class StunFx : MonoBehaviour
    {
        const int Arcs = 4;
        const int Points = 7;
        const float RerollInterval = 0.05f;
        const float ShakeDistance = 0.07f;
        const float ShakeTilt = 4f;

        float endTime;
        Transform model;
        Vector3 modelPosition;
        Quaternion modelRotation;
        Vector3 center;
        float height;
        float radius;
        float nextReroll;
        LineRenderer[] glow;
        LineRenderer[] core;
        Material glowMaterial;
        Material coreMaterial;

        public static void Show(GameObject target, float duration)
        {
            if (target == null || duration <= 0f) return;

            var fx = target.GetComponent<StunFx>();
            if (fx == null) fx = target.AddComponent<StunFx>();
            fx.endTime = Mathf.Max(fx.endTime, Time.time + duration);
        }

        void Awake()
        {
            model = transform.Find("Model");
            if (model != null)
            {
                modelPosition = model.localPosition;
                modelRotation = model.localRotation;
            }

            // Size the lightning to the character (a human is much smaller than a marine).
            Bounds bounds = new Bounds(transform.position + Vector3.up, Vector3.zero);
            bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r is TrailRenderer || r.transform.parent != null && r.transform.parent.name == "Stun Stars") continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            height = any ? Mathf.Clamp(bounds.size.y, 1.4f, 3f) : 2f;
            radius = any ? Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.4f, 1.1f) : 0.6f;
            center = any ? bounds.center - transform.position : Vector3.up;

            glowMaterial = VisualUtil.UnlitMaterial(new Color(0.5f, 0.8f, 1f, 0.45f), 3004);
            coreMaterial = VisualUtil.UnlitMaterial(new Color(1f, 1f, 0.85f, 1f), 3005);
            glow = BuildLines(glowMaterial, 0.16f);
            core = BuildLines(coreMaterial, 0.05f);
        }

        LineRenderer[] BuildLines(Material material, float width)
        {
            var lines = new LineRenderer[Arcs];
            for (int i = 0; i < Arcs; i++)
            {
                var go = new GameObject("Stun Lightning");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = Points;
                line.startWidth = line.endWidth = width;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.sharedMaterial = material;
                lines[i] = line;
            }
            return lines;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            // Judder: the model is jolted around every frame (the root, which owns the collider and movement, stays put).
            if (model != null)
            {
                model.localPosition = modelPosition + Random.insideUnitSphere * ShakeDistance;
                model.localRotation = modelRotation * Quaternion.Euler(Random.Range(-ShakeTilt, ShakeTilt), 0f, Random.Range(-ShakeTilt, ShakeTilt));
            }

            if (Time.time < nextReroll) return;
            nextReroll = Time.time + RerollInterval;

            Vector3 origin = transform.position + center;
            for (int i = 0; i < Arcs; i++)
            {
                // Each arc leaps between two random points around the body, with jagged kinks, and flickers on and off.
                bool on = Random.value > 0.2f;
                glow[i].enabled = core[i].enabled = on;
                if (!on) continue;

                Vector3 a = BodyPoint(origin);
                Vector3 b = BodyPoint(origin);
                for (int k = 0; k < Points; k++)
                {
                    float f = k / (float)(Points - 1);
                    Vector3 p = Vector3.Lerp(a, b, f);
                    if (k > 0 && k < Points - 1) p += Random.insideUnitSphere * 0.18f;
                    glow[i].SetPosition(k, p);
                    core[i].SetPosition(k, p);
                }
            }
        }

        Vector3 BodyPoint(Vector3 origin)
        {
            Vector2 ring = Random.insideUnitCircle.normalized * (radius * Random.Range(0.8f, 1.15f));
            return origin + new Vector3(ring.x, Random.Range(-0.5f, 0.5f) * height, ring.y);
        }

        void OnDestroy()
        {
            if (model != null)
            {
                model.localPosition = modelPosition;
                model.localRotation = modelRotation;
            }

            foreach (LineRenderer l in glow ?? new LineRenderer[0]) if (l != null) Destroy(l.gameObject);
            foreach (LineRenderer l in core ?? new LineRenderer[0]) if (l != null) Destroy(l.gameObject);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (coreMaterial != null) Destroy(coreMaterial);
        }
    }
}
