using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Net
{
    // Round score drawn as marbles either side of the round timer (Battlerite style): one marble per round needed to
    // win the match, filled for each round a team has won. Your team is on the left, everyone else on the right.
    // Draws in whatever GUI space is current (NetGame's 1080p-scaled matrix, or PlayerHud's own scaling via `unit`).
    public static class RoundMarbles
    {
        static Texture2D disc, ring, sheen;

        public static readonly Color Mine = new Color(0.35f, 0.70f, 1f);
        public static readonly Color Theirs = new Color(1f, 0.30f, 0.28f);

        // cx: screen centre x. y: top of the strip. unit: GUI pixels per 1080p pixel (1 under NetGame's matrix).
        public static void Draw(float cx, float y, float unit, float gapFromCentre, float height, int needed, int mine, IList<int> others)
        {
            Build();
            float d = height * 0.62f, step = d + 6f * unit, groupGap = 18f * unit;
            float cy = y + height * 0.5f;
            needed = Mathf.Max(1, needed);

            // Left: my team, growing outwards from the timer.
            float x = cx - gapFromCentre - d;
            for (int i = 0; i < needed; i++, x -= step)
                One(new Rect(x, cy - d * 0.5f, d, d), Mine, i < mine);

            // Right: each other team as its own group.
            x = cx + gapFromCentre;
            for (int g = 0; g < others.Count; g++)
            {
                for (int i = 0; i < needed; i++, x += step)
                    One(new Rect(x, cy - d * 0.5f, d, d), Theirs, i < others[g]);
                x += groupGap - 6f * unit;
            }
        }

        static void One(Rect r, Color color, bool filled)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), disc);

            if (filled)
            {
                GUI.color = color;
                GUI.DrawTexture(r, disc);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(r.x + r.width * 0.15f, r.y + r.height * 0.05f, r.width * 0.7f, r.height * 0.45f), sheen);
            }
            else
            {
                GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);
                GUI.DrawTexture(r, disc);
                GUI.color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.9f);
                GUI.DrawTexture(r, ring);
            }
            GUI.color = old;
        }

        static void Build()
        {
            if (disc != null) return;
            const int N = 64;
            disc = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            ring = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            sheen = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (N - 1) * 0.5f;
            for (int py = 0; py < N; py++)
                for (int px = 0; px < N; px++)
                {
                    float dist = Mathf.Sqrt((px - c) * (px - c) + (py - c) * (py - c)) / c; // 0 centre, 1 edge
                    float edge = Mathf.Clamp01((1f - dist) * c * 0.5f);                    // soft 2px edge
                    disc.SetPixel(px, py, new Color(1f, 1f, 1f, edge));
                    float ringA = Mathf.Clamp01((dist - 0.68f) * c * 0.5f) * edge;
                    ring.SetPixel(px, py, new Color(1f, 1f, 1f, ringA));
                    // Highlight: soft ellipse, brightest at the top (row N-1 is the top of the texture).
                    float sx = (px - c) / c, sy = (py - c) / c;
                    float s = Mathf.Clamp01(1f - (sx * sx + sy * sy)) * Mathf.Clamp01(0.5f + sy * 0.5f);
                    sheen.SetPixel(px, py, new Color(1f, 1f, 1f, s * 0.45f));
                }
            disc.Apply();
            ring.Apply();
            sheen.Apply();
        }
    }
}
