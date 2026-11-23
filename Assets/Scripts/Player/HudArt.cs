using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Player
{
    // Grimdark HUD art (riveted iron, worn brass, bone and wax), painted in code so it stays crisp at any resolution:
    // the round ability-slot frame, the rounded health/energy bar frames and the faction emblem on the health bar.
    // Shapes are authored in design units (the coordinates of the original SVG mock-ups) and painted per pixel from
    // signed distances, scaled to screen pixels. PlayerHud rebuilds the textures whenever its scale changes.
    public sealed class HudArt : IDisposable
    {
        public enum Emblem { PuritySeal, Aquila, Khorne, Nurgle, Tzeentch, Drukhari }

        // Design-space layout the HUD lines its drawing up against.
        public const float SlotRadius = 55f;     // the round slot's interior
        public const float SlotRingRadius = 62f; // the brass band, where charge gems sit
        public const float BarHalfHeight = 11f;  // the bar frame's interior (a capsule)
        public const float EmblemOffset = -14f;  // emblem centre, measured outward from the bar interior's outer end

        // A painted texture and the design-space box it covers.
        public readonly struct Art
        {
            public readonly Texture2D Tex;
            public readonly Rect Box;
            public Art(Texture2D tex, Rect box) { Tex = tex; Box = box; }

            // Screen rect for this art when design (0,0) sits at `anchor` and one design unit is `k` pixels.
            public Rect Place(Vector2 anchor, float k) => new Rect(anchor.x + Box.xMin * k, anchor.y + Box.yMin * k, Tex.width, Tex.height);
        }

        public float SlotK { get; private set; } // pixels per design unit for slot art
        public float BarK { get; private set; }  // pixels per design unit for bar art

        public Art SlotFrame, SlotRing, SlotDisc, SlotMask, Gem;
        public Art BarFrameLeft, BarFrameRight, BarMask, BarSheen, EmblemArt;
        public Texture2D Pill; // key-label plate, in screen pixels; drawn three-sliced (caps + stretched middle)

        // barDesignWidth: the bar interior's width in design units (its height is 2 * BarHalfHeight).
        public static HudArt Build(float slotK, float barK, float barDesignWidth, Emblem emblem, float pillHeight)
        {
            var a = new HudArt { SlotK = slotK, BarK = barK };
            a.SlotFrame = Bake(PaintSlotFrame(slotK));
            a.SlotRing = Bake(PaintSlotRing(slotK));
            a.SlotDisc = Bake(PaintSlotDisc(slotK));
            a.SlotMask = Bake(PaintSlotMask(slotK));
            a.Gem = Bake(PaintGem(slotK));
            a.BarFrameLeft = Bake(PaintBarFrame(barK, barDesignWidth, true));
            a.BarFrameRight = Bake(PaintBarFrame(barK, barDesignWidth, false));
            a.BarMask = Bake(PaintBarMask(barK, barDesignWidth, false));
            a.BarSheen = Bake(PaintBarMask(barK, barDesignWidth, true));
            a.EmblemArt = Bake(PaintEmblem(barK, emblem));
            a.Pill = Bake(PaintPill(pillHeight)).Tex;
            return a;
        }

        public static Emblem EmblemFor(string character)
        {
            switch (character)
            {
                case "Bullgryn": return Emblem.Aquila;
                case "KhorneBerserker": return Emblem.Khorne;
                case "PlagueSurgeon": return Emblem.Nurgle;
                case "Sorcerer": return Emblem.Tzeentch;
                case "Mandrake": return Emblem.Drukhari;
                default: return Emblem.PuritySeal; // the Imperium
            }
        }

        public void Dispose()
        {
            foreach (var t in new[] { SlotFrame.Tex, SlotRing.Tex, SlotDisc.Tex, SlotMask.Tex, Gem.Tex, BarFrameLeft.Tex,
                         BarFrameRight.Tex, BarMask.Tex, BarSheen.Tex, EmblemArt.Tex, Pill })
                if (t != null) UnityEngine.Object.Destroy(t);
        }

        static Art Bake(Canvas c)
        {
            var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels(c.Pixels);
            tex.Apply(false, true);
            return new Art(tex, c.Box);
        }

        // ---------- Palette ----------

        static readonly Color Iron = Hex(0x2a2420), IronDark = Hex(0x120e0a), IronPlate = Hex(0x3a3027), Edge = Hex(0x0c0907);
        static readonly Color Brass = Hex(0x7a5c2e), BrassLight = Hex(0xc9a35a, 0.5f), BrassDark = Hex(0x1a120a);
        static readonly Color SpikeInner = Hex(0x6e5a3a), Rivet = Hex(0xb8924c), Bone = Hex(0xcfc4a8), BoneLine = Hex(0x1a140c);

        static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        // ---------- Pieces ----------

        // Round slot frame: iron rim, worn brass band, eight rivets, a gothic spike on top and a skull at the bottom.
        // The interior (radius SlotRadius) is left clear; the slot's contents are drawn underneath.
        public static Canvas PaintSlotFrame(float k)
        {
            var c = new Canvas(new Rect(-88f, -88f, 176f, 176f), k);
            c.Poly(new[] { V(-11, -64), V(0, -86), V(11, -64) }, Iron, IronDark, 2f);
            c.Poly(new[] { V(-4, -66), V(0, -80), V(4, -66) }, SpikeInner);
            c.Circle(V(0, 0), 69f, IronPlate, Edge, 3f);
            c.Ring(V(0, 0), 62f, 7f, Brass);
            c.Ring(V(0, 0), 65f, 1f, BrassLight);
            c.Ring(V(0, 0), 58.5f, 1.5f, BrassDark);
            for (int i = 0; i < 8; i++)
            {
                float ang = (22.5f + 45f * i) * Mathf.Deg2Rad;
                c.Circle(V(MathF.Cos(ang) * 65f, MathF.Sin(ang) * 65f), 2.6f, Rivet, IronDark, 1f);
            }
            var scratch = new Color(0f, 0f, 0f, 0.5f);
            c.Line(V(40, -52), V(46, -46), 1f, scratch);
            c.Line(V(-50, 40), V(-44, 47), 1f, scratch);
            c.Line(V(-38, -56), V(-33, -58), 1f, scratch);
            c.EraseCircle(V(0, 0), SlotRadius);
            Skull(c, V(0, 70));
            return c;
        }

        static void Skull(Canvas c, Vector2 o)
        {
            Func<Vector2, float> head = p => MathF.Min(Sdf.Circle(p, o + V(0, -3), 12f), Sdf.RoundRect(p, new Rect(o.x - 7f, o.y, 14f, 12f), 2f));
            var bounds = new Rect(o.x - 14f, o.y - 17f, 28f, 31f);
            c.Fill(p => head(p) - 0.75f, bounds, BoneLine);
            c.Fill(p => head(p) + 0.75f, bounds, Bone);
            c.Circle(o + V(-4.5f, -3f), 3.2f, IronDark);
            c.Circle(o + V(4.5f, -3f), 3.2f, IronDark);
            c.Poly(new[] { o + V(0, 1), o + V(1.8f, 5), o + V(-1.8f, 5) }, IronDark);
            for (int i = -1; i <= 1; i++) c.Line(o + V(3f * i, 8f), o + V(3f * i, 12f), 1f, BoneLine);
        }

        // White ring on the interior's edge, tinted with the character's accent colour when drawn.
        public static Canvas PaintSlotRing(float k)
        {
            var c = new Canvas(new Rect(-88f, -88f, 176f, 176f), k);
            c.Ring(V(0, 0), SlotRadius, 3f, Color.white);
            return c;
        }

        // The slot's interior: a dark, grimy disc.
        public static Canvas PaintSlotDisc(float k)
        {
            var c = new Canvas(new Rect(-88f, -88f, 176f, 176f), k);
            Color inner = Hex(0x2a1e16), outer = Hex(0x060403);
            c.Fill(p => Sdf.Circle(p, Vector2.zero, SlotRadius + 1f), Circ(Vector2.zero, SlotRadius + 1f),
                p => Color.Lerp(inner, outer, Mathf.Clamp01((p - V(0, -6)).magnitude / 62f)));
            var grime = new Color(0f, 0f, 0f, 0.3f);
            c.Circle(V(-30, -34), 9f, grime);
            c.Circle(V(34, 30), 12f, grime);
            c.Circle(V(-36, 22), 7f, grime);
            return c;
        }

        // White disc the size of the interior: tinted overlays and curtains are clipped to it.
        public static Canvas PaintSlotMask(float k)
        {
            var c = new Canvas(new Rect(-88f, -88f, 176f, 176f), k);
            c.Circle(V(0, 0), SlotRadius, Color.white);
            return c;
        }

        // A charge gem set into the brass band (tinted gold when ready, dark when spent).
        public static Canvas PaintGem(float k)
        {
            var c = new Canvas(new Rect(-9f, -9f, 18f, 18f), k);
            c.Circle(V(0, 0), 6.5f, Hex(0xd8d8d8), IronDark, 2f);
            c.Circle(V(-2f, -2f), 2f, new Color(1f, 1f, 1f, 0.9f));
            return c;
        }

        // Rounded bar frame around an interior running from x = 0 to `width`, y = -11..11 (design units). A spike points
        // out from the outer end (left for the health bar, right for the energy bar) and another rises from the middle.
        public static Canvas PaintBarFrame(float k, float width, bool spikeLeft)
        {
            var c = new Canvas(new Rect(-42f, -36f, width + 84f, 58f), k);
            float mid = width * 0.5f;
            if (spikeLeft) c.Poly(new[] { V(-6, -9), V(-38, 0), V(-6, 9) }, Iron, IronDark, 2f);
            else c.Poly(new[] { V(width + 6, -9), V(width + 38, 0), V(width + 6, 9) }, Iron, IronDark, 2f);
            c.Poly(new[] { V(mid - 6, -19), V(mid, -33), V(mid + 6, -19) }, Iron, IronDark, 2f);
            c.Poly(new[] { V(mid - 3, -20), V(mid, -29), V(mid + 3, -20) }, SpikeInner);
            c.Capsule(new Rect(-8f, -20f, width + 16f, 40f), IronPlate, Edge, 3f);
            c.CapsuleStroke(new Rect(-3.5f, -15.5f, width + 7f, 31f), 4f, Brass);
            c.CapsuleStroke(new Rect(-5.5f, -17.5f, width + 11f, 35f), 0.8f, BrassLight);
            foreach (float f in new[] { 0.19f, 0.77f }) c.Circle(V(width * f, -17.5f), 2f, Rivet, IronDark, 0.8f);
            foreach (float f in new[] { 0.19f, 0.5f, 0.77f }) c.Circle(V(width * f, 17.5f), 2f, Rivet, IronDark, 0.8f);
            var scratch = new Color(0f, 0f, 0f, 0.45f);
            c.Line(V(width * 0.33f, -18f), V(width * 0.33f + 5f, -14f), 0.9f, scratch);
            c.Line(V(width * 0.64f, 14f), V(width * 0.64f + 6f, 17f), 0.9f, scratch);
            var hole = new Rect(0f, -BarHalfHeight, width, BarHalfHeight * 2f);
            c.Erase(p => Sdf.Capsule(p, hole), hole);
            c.CapsuleStroke(hole, 1.5f, Color.black);
            return c;
        }

        // The bar interior as a white capsule (fills are clipped to it), or its sheen: white fading out down the top half.
        public static Canvas PaintBarMask(float k, float width, bool sheen)
        {
            var hole = new Rect(0f, -BarHalfHeight, width, BarHalfHeight * 2f);
            var c = new Canvas(hole, k);
            if (!sheen) c.Capsule(hole, Color.white);
            else c.Fill(p => Sdf.Capsule(p, hole), hole,
                p => new Color(1f, 1f, 1f, Mathf.Lerp(0.26f, 0f, Mathf.Clamp01((p.y + BarHalfHeight) / (BarHalfHeight * 2f * 0.55f)))));
            return c;
        }

        // The faction emblem that sits on the health bar's outer end, centred on (0,0).
        public static Canvas PaintEmblem(float k, Emblem kind)
        {
            var c = new Canvas(new Rect(-30f, -30f, 60f, 58f), k);
            switch (kind)
            {
                case Emblem.PuritySeal:
                {
                    Color parchment = Hex(0xd4c398), parchment2 = Hex(0xc9b78a), ink = Hex(0x5a4a2e);
                    c.Poly(new[] { V(-10, 8), V(-3, 10), V(-8, 26), V(-15, 24) }, parchment, ink, 0.8f);
                    c.Poly(new[] { V(3, 10), V(10, 8), V(14, 24), V(7, 26) }, parchment2, ink, 0.8f);
                    c.Line(V(-10, 15), V(-5, 16), 0.8f, ink);
                    c.Line(V(-11, 19), V(-6, 20), 0.8f, ink);
                    c.Line(V(5, 15), V(10, 14), 0.8f, ink);
                    c.Line(V(6, 19), V(11, 18), 0.8f, ink);
                    c.Circle(V(0, 0), 17f, Hex(0x8a1a14), Hex(0x3a0806), 2f);
                    c.Ring(V(0, 0), 11f, 1.5f, Hex(0xc2392c));
                    c.Poly(new[] { V(-4, 0), V(0, -5), V(4, 0), V(0, 5) }, Hex(0xc2392c));
                    break;
                }
                case Emblem.Aquila:
                {
                    // Double-headed eagle in gold on an iron medallion, its wings spreading past the rim.
                    Color gold = Hex(0xe3b862), shade = Hex(0xa8792e), outline = Hex(0x2a1a08);
                    c.Circle(V(0, 0), 17f, Hex(0x2a2420), Hex(0x8a6a3a), 2f);
                    var leftWing = new[] { V(-3, -4), V(-12, -12), V(-27, -15), V(-22, -10), V(-28, -7), V(-21, -4), V(-26, 0),
                        V(-18, 1), V(-21, 5), V(-12, 4), V(-4, 4) };
                    var rightWing = Array.ConvertAll(leftWing, p => V(-p.x, p.y));
                    c.Poly(leftWing, gold, outline, 0.9f);
                    c.Poly(rightWing, gold, outline, 0.9f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        c.Line(V(-13 * s, -10), V(-24 * s, -12), 0.8f, shade); // feather lines
                        c.Line(V(-12 * s, -5), V(-22 * s, -5), 0.8f, shade);
                        c.Line(V(-10 * s, 1), V(-18 * s, 3), 0.8f, shade);
                        c.Line(V(-1.5f * s, -4), V(-5 * s, -10), 3.2f, outline);   // neck
                        c.Line(V(-1.5f * s, -4), V(-5 * s, -10), 1.8f, gold);
                        c.Circle(V(-5.5f * s, -11), 3.2f, gold, outline, 0.9f); // head
                        c.Poly(new[] { V(-8 * s, -12), V(-11.5f * s, -10.5f), V(-8 * s, -9.5f) }, gold, outline, 0.7f); // beak
                        c.Circle(V(-5.2f * s, -11.6f), 0.8f, outline); // eye
                    }
                    c.Poly(new[] { V(0, -6), V(4, -1), V(3, 8), V(0, 11), V(-3, 8), V(-4, -1) }, gold, outline, 0.9f); // body
                    c.Poly(new[] { V(-3, 9), V(0, 16), V(3, 9) }, shade, outline, 0.8f); // tail
                    c.Line(V(0, -3), V(0, 8), 0.8f, shade);
                    break;
                }
                case Emblem.Khorne:
                {
                    Color spike = Hex(0x8a6a3a);
                    c.Poly(new[] { V(-4, -15), V(0, -24), V(4, -15) }, spike, IronDark, 1f);
                    c.Poly(new[] { V(-15, -4), V(-24, 0), V(-15, 4) }, spike, IronDark, 1f);
                    c.Poly(new[] { V(15, -4), V(24, 0), V(15, 4) }, spike, IronDark, 1f);
                    c.Poly(new[] { V(-4, 15), V(0, 24), V(4, 15) }, spike, IronDark, 1f);
                    c.Circle(V(0, 0), 17f, Hex(0x4a0806), Hex(0xc9a35a), 2.5f);
                    Color rune = Hex(0xe3b862);
                    c.Line(V(-8, -9), V(7, 6), 3.2f, rune);
                    c.Line(V(8, -9), V(-7, 6), 3.2f, rune);
                    c.Line(V(-9, 8), V(9, 8), 3.2f, rune);
                    break;
                }
                case Emblem.Nurgle:
                {
                    c.Circle(V(0, 0), 17f, Hex(0x1f2a0c), Hex(0x6a6a2a), 2.5f);
                    Color pox = Hex(0x8cd933);
                    c.Ring(V(0, -5), 6f, 2.4f, pox);
                    c.Ring(V(-5.4f, 3.5f), 6f, 2.4f, pox);
                    c.Ring(V(5.4f, 3.5f), 6f, 2.4f, pox);
                    c.Circle(V(0, 0.6f), 2f, Hex(0xc4f060));
                    c.Circle(V(-12, -9), 1.8f, Hex(0x3a4a14));
                    c.Circle(V(11, 11), 1.5f, Hex(0x3a4a14));
                    break;
                }
                case Emblem.Tzeentch:
                {
                    c.Circle(V(0, 0), 17f, Hex(0x0a2238), Hex(0xc9a35a), 2.5f);
                    var flame = new List<Vector2>();
                    flame.AddRange(Quad(V(0, -12), V(8.5f, -3.5f), V(0, 4), 10));
                    flame.AddRange(Quad(V(0, 4), V(-8.5f, -3.5f), V(0, -12), 10));
                    c.Poly(flame.ToArray(), Hex(0x4ab8ff));
                    c.Circle(V(0, -3.5f), 2.1f, Hex(0x0a2238));
                    c.Polyline(Quad(V(-10, 1), V(0, 13), V(10, 1), 14), 2.4f, Hex(0xe3b862));
                    c.Polyline(Quad(V(-6, 7), V(0, 11), V(6, 7), 10), 1.4f, Hex(0x4ab8ff));
                    break;
                }
                case Emblem.Drukhari:
                {
                    Color edge = Hex(0x73ff4d);
                    c.Poly(new[] { V(0, -22), V(3.6f, -6), V(12, -12), V(6, 1.2f), V(8.4f, 18), V(0, 7.2f), V(-8.4f, 18), V(-6, 1.2f), V(-12, -12), V(-3.6f, -6) },
                        Hex(0x121410), edge, 1.5f);
                    c.Poly(new[] { V(0, -13), V(2.4f, -2.4f), V(0, 3.6f), V(-2.4f, -2.4f) }, edge);
                    var faint = new Color(edge.r, edge.g, edge.b, 0.7f);
                    c.Line(V(-7, -7), V(-3.6f, -2.4f), 1f, faint);
                    c.Line(V(7, -7), V(3.6f, -2.4f), 1f, faint);
                    break;
                }
            }
            return c;
        }

        // Key-label plate: a dark pill with a light rim, `height` screen pixels tall and twice as wide.
        public static Canvas PaintPill(float height)
        {
            float h = MathF.Max(8f, MathF.Round(height));
            var c = new Canvas(new Rect(0f, 0f, h * 2f, h), 1f);
            c.Capsule(new Rect(1f, 1f, h * 2f - 2f, h - 2f), new Color(0.04f, 0.045f, 0.07f, 0.97f), Hex(0xf0d89a), 2f);
            return c;
        }

        // ---------- Helpers ----------

        static Vector2 V(float x, float y) => new Vector2(x, y);
        static Rect Circ(Vector2 c, float r) => new Rect(c.x - r, c.y - r, r * 2f, r * 2f);

        static Vector2[] Quad(Vector2 a, Vector2 ctrl, Vector2 b, int steps)
        {
            var pts = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps, u = 1f - t;
                pts[i] = a * (u * u) + ctrl * (2f * u * t) + b * (t * t);
            }
            return pts;
        }

        // Signed distance functions, in design units (negative inside).
        static class Sdf
        {
            public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

            public static float RoundRect(Vector2 p, Rect rect, float radius)
            {
                Vector2 q = new Vector2(MathF.Abs(p.x - rect.center.x), MathF.Abs(p.y - rect.center.y)) - rect.size * 0.5f + new Vector2(radius, radius);
                return new Vector2(MathF.Max(q.x, 0f), MathF.Max(q.y, 0f)).magnitude + MathF.Min(MathF.Max(q.x, q.y), 0f) - radius;
            }

            public static float Capsule(Vector2 p, Rect rect) => RoundRect(p, rect, MathF.Min(rect.width, rect.height) * 0.5f);

            public static float Segment(Vector2 p, Vector2 a, Vector2 b, float halfWidth)
            {
                Vector2 pa = p - a, ba = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / MathF.Max(1e-6f, Vector2.Dot(ba, ba)));
                return (pa - ba * t).magnitude - halfWidth;
            }

            public static float Polygon(Vector2 p, Vector2[] v)
            {
                float d = (p - v[0]).sqrMagnitude, s = 1f;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    Vector2 e = v[j] - v[i], w = p - v[i];
                    float ee = Vector2.Dot(e, e);
                    if (ee < 1e-9f) continue; // repeated point (e.g. where two curves join)
                    Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / ee);
                    d = MathF.Min(d, b.sqrMagnitude);
                    bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * MathF.Sqrt(d);
            }
        }

        // A pixel buffer painted in design units. Coverage comes from the signed distance, so every edge is anti-aliased.
        public sealed class Canvas
        {
            public readonly int W, H;
            public readonly Rect Box;
            public readonly Color[] Pixels; // bottom row first, as Texture2D.SetPixels expects
            readonly float k;

            public Canvas(Rect box, float k)
            {
                Box = box;
                this.k = k;
                W = Math.Max(1, (int)MathF.Ceiling(box.width * k));
                H = Math.Max(1, (int)MathF.Ceiling(box.height * k));
                Pixels = new Color[W * H];
            }

            public void Fill(Func<Vector2, float> sdf, Rect bounds, Color color) => Fill(sdf, bounds, _ => color);

            public void Fill(Func<Vector2, float> sdf, Rect bounds, Func<Vector2, Color> paint)
            {
                Span(bounds, out int x0, out int x1, out int y0, out int y1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(Box.xMin + (x + 0.5f) / k, Box.yMin + (y + 0.5f) / k);
                    float cov = Mathf.Clamp01(0.5f - sdf(p) * k);
                    if (cov <= 0f) continue;
                    Color src = paint(p);
                    float a = src.a * cov;
                    if (a <= 0f) continue;
                    int i = (H - 1 - y) * W + x;
                    Color dst = Pixels[i];
                    float keep = dst.a * (1f - a), outA = a + keep;
                    Pixels[i] = new Color((src.r * a + dst.r * keep) / outA, (src.g * a + dst.g * keep) / outA, (src.b * a + dst.b * keep) / outA, outA);
                }
            }

            public void Erase(Func<Vector2, float> sdf, Rect bounds)
            {
                Span(bounds, out int x0, out int x1, out int y0, out int y1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(Box.xMin + (x + 0.5f) / k, Box.yMin + (y + 0.5f) / k);
                    float cov = Mathf.Clamp01(0.5f - sdf(p) * k);
                    if (cov > 0f) Pixels[(H - 1 - y) * W + x].a *= 1f - cov;
                }
            }

            void Span(Rect b, out int x0, out int x1, out int y0, out int y1)
            {
                x0 = Math.Max(0, (int)MathF.Floor((b.xMin - Box.xMin) * k) - 2);
                x1 = Math.Min(W - 1, (int)MathF.Ceiling((b.xMax - Box.xMin) * k) + 2);
                y0 = Math.Max(0, (int)MathF.Floor((b.yMin - Box.yMin) * k) - 2);
                y1 = Math.Min(H - 1, (int)MathF.Ceiling((b.yMax - Box.yMin) * k) + 2);
            }

            // Filled shape with an optional stroke centred on its edge (as in SVG).
            void Shape(Func<Vector2, float> sdf, Rect bounds, Color fill, Color? stroke, float strokeWidth)
            {
                float hw = strokeWidth * 0.5f;
                if (stroke.HasValue && strokeWidth > 0f)
                {
                    var grown = new Rect(bounds.x - hw, bounds.y - hw, bounds.width + strokeWidth, bounds.height + strokeWidth);
                    Fill(p => sdf(p) - hw, grown, stroke.Value);
                    Fill(p => sdf(p) + hw, bounds, fill);
                }
                else Fill(sdf, bounds, fill);
            }

            public void Circle(Vector2 c, float r, Color fill, Color? stroke = null, float strokeWidth = 0f)
                => Shape(p => Sdf.Circle(p, c, r), Circ(c, r), fill, stroke, strokeWidth);

            public void Ring(Vector2 c, float r, float width, Color color)
                => Fill(p => MathF.Abs(Sdf.Circle(p, c, r)) - width * 0.5f, Circ(c, r + width), color);

            public void EraseCircle(Vector2 c, float r) => Erase(p => Sdf.Circle(p, c, r), Circ(c, r));

            public void Capsule(Rect rect, Color fill, Color? stroke = null, float strokeWidth = 0f)
                => Shape(p => Sdf.Capsule(p, rect), rect, fill, stroke, strokeWidth);

            public void CapsuleStroke(Rect rect, float width, Color color)
            {
                var grown = new Rect(rect.x - width, rect.y - width, rect.width + width * 2f, rect.height + width * 2f);
                Fill(p => MathF.Abs(Sdf.Capsule(p, rect)) - width * 0.5f, grown, color);
            }

            public void Poly(Vector2[] pts, Color fill, Color? stroke = null, float strokeWidth = 0f)
                => Shape(p => Sdf.Polygon(p, pts), Bounds(pts), fill, stroke, strokeWidth);

            public void Line(Vector2 a, Vector2 b, float width, Color color)
            {
                var bounds = Bounds(new[] { a, b });
                Fill(p => Sdf.Segment(p, a, b, width * 0.5f), new Rect(bounds.x - width, bounds.y - width, bounds.width + width * 2f, bounds.height + width * 2f), color);
            }

            public void Polyline(Vector2[] pts, float width, Color color)
            {
                var bounds = Bounds(pts);
                Fill(p =>
                {
                    float d = float.MaxValue;
                    for (int i = 1; i < pts.Length; i++) d = MathF.Min(d, Sdf.Segment(p, pts[i - 1], pts[i], width * 0.5f));
                    return d;
                }, new Rect(bounds.x - width, bounds.y - width, bounds.width + width * 2f, bounds.height + width * 2f), color);
            }

            static Rect Bounds(Vector2[] pts)
            {
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                foreach (var p in pts) { x0 = MathF.Min(x0, p.x); y0 = MathF.Min(y0, p.y); x1 = MathF.Max(x1, p.x); y1 = MathF.Max(y1, p.y); }
                return Rect.MinMaxRect(x0, y0, x1, y1);
            }
        }
    }
}
