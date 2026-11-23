using UnityEngine;

namespace Anathema.Net
{
    // "X ELIMINATED" strip across the upper screen for a couple of seconds after a fighter goes down: slides in, holds,
    // fades out. Blue-ish when it's your side that lost someone, red when it's the enemy. Drawn by NetGame online and by
    // TrainingEliminations in the offline training arena. Call from OnGUI in a 1080-high scaled GUI space.
    public static class EliminationBanner
    {
        public const float Duration = 2.6f;

        static Texture2D pixel;
        static GUIStyle nameStyle, whatStyle;

        // `t` is the seconds since the elimination.
        public static void Draw(float w, float h, float t, string name, bool friendly)
        {
            if (t < 0f || t > Duration || string.IsNullOrEmpty(name)) return;

            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 58, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                whatStyle = new GUIStyle(nameStyle);
            }

            float fadeIn = Mathf.Clamp01(t / 0.15f);
            float fadeOut = Mathf.Clamp01((Duration - t) / 0.5f);
            float alpha = Mathf.Min(fadeIn, fadeOut);
            float slide = (1f - Mathf.SmoothStep(0f, 1f, fadeIn)) * 40f;

            float y = h * 0.17f;
            Color accent = friendly ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.32f, 0.25f);
            Fill(new Rect(0, y, w, 96), new Color(0f, 0f, 0f, 0.6f * alpha));
            Fill(new Rect(0, y, w, 3), new Color(accent.r, accent.g, accent.b, 0.9f * alpha));
            Fill(new Rect(0, y + 93, w, 3), new Color(accent.r, accent.g, accent.b, 0.9f * alpha));

            nameStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
            whatStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, alpha);

            // "NAME" in white, "ELIMINATED" in the accent colour, centred together.
            string who = name + "  ";
            const string what = "ELIMINATED";
            float whoW = nameStyle.CalcSize(new GUIContent(who)).x;
            float whatW = whatStyle.CalcSize(new GUIContent(what)).x;
            float x = (w - whoW - whatW) * 0.5f + slide;
            GUI.Label(new Rect(x, y + 4, whoW, 88), who, nameStyle);
            GUI.Label(new Rect(x + whoW, y + 4, whatW, 88), what, whatStyle);
        }

        static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, pixel);
            GUI.color = old;
        }
    }
}
