using Anathema.Combat;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Arena
{
    // A circle of runes on the ground around the charge orb's spot that counts down to its next appearance: the runes
    // light up one by one, clockwise from the north, and a gold arc fills the outer circle. In the last few seconds the
    // whole ring pulses. If the countdown is over but the orb can't appear yet (someone is standing on the spot), it
    // stays full and keeps pulsing. Visual only: OrbSpawner (the host, offline or online) starts each countdown and
    // online the host sends it to everyone (NetGame's chargeOrbTimer message).
    public class ChargeOrbRing : MonoBehaviour
    {
        const int RuneCount = 12;
        const float RuneRadius = 2.2f;
        const float InnerRadius = 1.75f;
        const float OuterRadius = 2.65f;
        const float RuneSize = 0.2f;
        const float Lift = 0.06f;
        const float PulseSeconds = 3f;
        const float FadeOutSeconds = 0.5f;
        const int CirclePoints = 72;

        static readonly Color Dim = new Color(0.42f, 0.30f, 0.12f, 0.35f);
        static readonly Color Lit = new Color(1f, 0.78f, 0.25f, 1f);
        static readonly Color Hot = new Color(1f, 0.95f, 0.7f, 1f);

        // Rune shapes as polylines (x along the circle, y outward from the centre, both -1..1). Strokes may retrace.
        static readonly Vector2[][] Glyphs =
        {
            new[] { new Vector2(0, -1), new Vector2(0, 1), new Vector2(-0.6f, 0.4f), new Vector2(0, 1), new Vector2(0.6f, 0.4f) },
            new[] { new Vector2(0, -1), new Vector2(0, 1), new Vector2(0, 0.1f), new Vector2(-0.6f, 0.9f), new Vector2(0, 0.1f), new Vector2(0.6f, 0.9f) },
            new[] { new Vector2(0, -1), new Vector2(0.6f, 0), new Vector2(0, 1), new Vector2(-0.6f, 0), new Vector2(0, -1) },
            new[] { new Vector2(-0.4f, -1), new Vector2(-0.4f, 1), new Vector2(0.45f, 0.5f), new Vector2(-0.4f, 0), new Vector2(0.45f, -1) },
            new[] { new Vector2(-0.5f, 1), new Vector2(0.5f, -1), new Vector2(0, 0), new Vector2(-0.5f, -1), new Vector2(0.5f, 1) },
            new[] { new Vector2(0, -1), new Vector2(0, 1), new Vector2(0.55f, 0.55f), new Vector2(0, 0.1f), new Vector2(0.55f, -0.35f) },
        };

        static ChargeOrbRing instance;
        static Material material;

        LineRenderer inner, outer, arc;
        readonly LineRenderer[] runes = new LineRenderer[RuneCount];

        bool counting;
        float deadline;
        float total;
        float fadeOutStart = float.NegativeInfinity;

        // Starts (or corrects) a countdown: the orb is due at `centre` in `remaining` seconds out of `totalSeconds`.
        public static void Begin(Vector3 centre, float remaining, float totalSeconds)
        {
            if (instance == null) instance = Create();
            centre.y = 0f;
            if ((instance.transform.position - centre).sqrMagnitude > 0.01f) instance.Place(centre);
            instance.counting = true;
            instance.total = Mathf.Max(0.1f, totalSeconds);
            instance.deadline = Time.time + Mathf.Max(0f, remaining);
            instance.fadeOutStart = float.NegativeInfinity;
        }

        // The orb appeared: a running countdown flashes full and fades out.
        public static void Appeared()
        {
            if (instance == null || !instance.counting) return;
            instance.counting = false;
            instance.fadeOutStart = Time.time;
        }

        // The round was reset: the ring is gone at once.
        public static void Clear()
        {
            if (instance == null) return;
            instance.counting = false;
            instance.fadeOutStart = float.NegativeInfinity;
        }

        static ChargeOrbRing Create()
        {
            if (material == null) material = VisualUtil.UnlitMaterial(Color.white);

            var go = new GameObject("ChargeOrbRing");
            var ring = go.AddComponent<ChargeOrbRing>();
            ring.inner = ring.NewLine("Inner", CirclePoints, true, 0.04f);
            ring.outer = ring.NewLine("Outer", CirclePoints, true, 0.07f);
            ring.arc = ring.NewLine("Arc", CirclePoints + 1, false, 0.14f);
            for (int i = 0; i < RuneCount; i++)
                ring.runes[i] = ring.NewLine("Rune" + i, Glyphs[i % Glyphs.Length].Length, false, 0.05f);
            ring.Place(Vector3.zero);
            return ring;
        }

        LineRenderer NewLine(string lineName, int points, bool loop, float width)
        {
            var child = new GameObject(lineName);
            child.transform.SetParent(transform, false);
            child.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lies flat, facing up
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.loop = loop;
            line.positionCount = points;
            line.startWidth = line.endWidth = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        // Clockwise seen from above, starting at north (+Z).
        static Vector3 Around(float angle) => new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

        void Place(Vector3 centre)
        {
            transform.position = centre;
            Vector3 c = centre + Vector3.up * Lift;
            for (int i = 0; i < CirclePoints; i++)
            {
                Vector3 d = Around(i / (float)CirclePoints * Mathf.PI * 2f);
                inner.SetPosition(i, c + d * InnerRadius);
                outer.SetPosition(i, c + d * OuterRadius);
            }

            for (int i = 0; i < RuneCount; i++)
            {
                // Centred in its twelfth of the circle, so a rune lights once the arc has passed it.
                float a = (i + 0.5f) / RuneCount * Mathf.PI * 2f;
                Vector3 radial = Around(a);
                Vector3 tangent = new Vector3(radial.z, 0f, -radial.x);
                Vector2[] glyph = Glyphs[i % Glyphs.Length];
                for (int p = 0; p < glyph.Length; p++)
                    runes[i].SetPosition(p, c + radial * (RuneRadius + glyph[p].y * RuneSize) + tangent * glyph[p].x * RuneSize);
            }
        }

        void Update()
        {
            bool fading = Time.time - fadeOutStart < FadeOutSeconds;
            bool show = (counting && !NetGame.InPrep && ChargeOrb.Current == null) || fading;
            SetVisible(show);
            if (!show) return;

            float fill, glow, alpha = 1f;
            if (fading)
            {
                fill = 1f;
                glow = 1f;
                alpha = 1f - (Time.time - fadeOutStart) / FadeOutSeconds;
            }
            else
            {
                float remaining = deadline - Time.time;
                fill = Mathf.Clamp01(1f - remaining / total);
                // A beat that quickens as the orb nears; a steady throb once it's overdue.
                glow = remaining < PulseSeconds
                    ? 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(14f, 6f, Mathf.Clamp01(remaining / PulseSeconds)))
                    : 0f;
            }

            Color baseLine = Color.Lerp(Dim, Lit, glow * 0.6f);
            Paint(inner, baseLine, alpha);
            Paint(outer, baseLine, alpha);

            // The arc over the outer circle, from north clockwise to the current fill.
            Vector3 c = transform.position + Vector3.up * (Lift + 0.01f);
            int points = arc.positionCount;
            for (int i = 0; i < points; i++)
                arc.SetPosition(i, c + Around(fill * Mathf.PI * 2f * i / (points - 1)) * OuterRadius);
            arc.enabled = fill > 0.001f;
            Paint(arc, Color.Lerp(Lit, Hot, glow), alpha);

            for (int i = 0; i < RuneCount; i++)
            {
                // Each rune brightens as the arc sweeps across its twelfth of the circle.
                float lit = Mathf.Clamp01(fill * RuneCount - i);
                Color col = lit >= 1f ? Color.Lerp(Lit, Hot, glow) : Color.Lerp(Dim, Lit, lit * lit);
                Paint(runes[i], col, alpha);
            }
        }

        static void Paint(LineRenderer line, Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            line.startColor = line.endColor = color;
        }

        bool visible = true;

        void SetVisible(bool on)
        {
            if (on == visible) return;
            visible = on;
            inner.enabled = outer.enabled = arc.enabled = on;
            foreach (LineRenderer r in runes) r.enabled = on;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
