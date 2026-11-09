using UnityEngine;

namespace Anathema.Upgrades
{
    // The between-rounds upgrade screen: three cards (armament / endurance / ferocity). Click one, or press 1-3.
    // Drawn by NetGame inside its 1080p-scaled OnGUI, so all sizes here are in 1080p pixels.
    public sealed class UpgradePickerView
    {
        static readonly Color Gold = new Color(0.86f, 0.70f, 0.30f);
        static readonly Color DarkGold = new Color(0.45f, 0.34f, 0.12f);
        static readonly Color Panel = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        static readonly Color PanelHover = new Color(0.11f, 0.12f, 0.17f, 0.98f);

        const float CardW = 430f, CardH = 500f, CardGap = 36f;

        Texture2D pixel;
        GUIStyle titleStyle, subtitleStyle, categoryStyle, nameStyle, bodyStyle, keyStyle, ownedStyle;

        void Build()
        {
            if (pixel != null) return;

            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            GUIStyle Make(int size, FontStyle fontStyle, TextAnchor anchor, Color color, bool wrap = false)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, alignment = anchor, wordWrap = wrap };
                style.normal.textColor = color;
                return style;
            }

            titleStyle = Make(64, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            subtitleStyle = Make(26, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.75f, 0.7f, 0.65f));
            categoryStyle = Make(22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);
            nameStyle = Make(34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true);
            bodyStyle = Make(23, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.82f), true);
            keyStyle = Make(24, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            ownedStyle = Make(21, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.75f, 0.7f, 0.65f), true);
        }

        void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, pixel);
            GUI.color = old;
        }

        // Number keys 1-3 pick an offer. Call every frame while the screen is up.
        public void HandleKeys(UpgradeState state)
        {
            if (state == null || !state.Pending) return;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) state.Choose(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) state.Choose(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) state.Choose(2);
        }

        public void Draw(float w, float h, UpgradeState state, int nextRound, float secondsLeft)
        {
            if (state == null || !state.Pending) return;
            Build();

            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.62f)); // the prep area stays visible behind the menu
            GUI.Label(new Rect(0, 70, w, 80), "CHOOSE AN UPGRADE", titleStyle);
            GUI.Label(new Rect(0, 150, w, 40),
                $"Prepare for round {nextRound}  ·  upgrades last for the rest of the match  ·  press 1, 2 or 3", subtitleStyle);

            int count = state.Offers.Count;
            float total = count * CardW + (count - 1) * CardGap;
            float x = (w - total) * 0.5f;
            float y = 220f;

            for (int i = 0; i < count; i++)
            {
                if (DrawCard(new Rect(x + i * (CardW + CardGap), y, CardW, CardH), state.Offers[i], i)) state.Choose(i);
            }

            string owned = OwnedSummary(state);
            if (owned.Length > 0) GUI.Label(new Rect(w * 0.5f - 600f, y + CardH + 24f, 1200f, 70f), "Taken so far:  " + owned, ownedStyle);

            // Unchosen picks are settled at random when the timer runs out.
            GUI.Label(new Rect(0, h - 110f, w, 40f), $"A random upgrade is taken in {Mathf.Max(0, Mathf.CeilToInt(secondsLeft))}s if you don't choose", subtitleStyle);
        }

        // Returns true when the card is clicked.
        bool DrawCard(Rect r, UpgradeDef def, int index)
        {
            Color accent = UpgradeDef.CategoryColor(def.Category);
            bool hover = r.Contains(Event.current.mousePosition);

            Fill(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), hover ? accent : new Color(accent.r * 0.5f, accent.g * 0.5f, accent.b * 0.5f));
            Fill(r, hover ? PanelHover : Panel);

            var header = new Rect(r.x, r.y, r.width, 44f);
            Fill(header, accent);
            GUI.Label(header, UpgradeDef.CategoryLabel(def.Category), categoryStyle);

            GUI.Label(new Rect(r.x + 20f, r.y + 62f, r.width - 40f, 110f), def.Name, nameStyle);
            Fill(new Rect(r.x + 40f, r.y + 178f, r.width - 80f, 2f), DarkGold);
            GUI.Label(new Rect(r.x + 26f, r.y + 196f, r.width - 52f, r.height - 196f - 64f), def.Description, bodyStyle);

            GUI.Label(new Rect(r.x, r.yMax - 56f, r.width, 44f), $"[ {index + 1} ]", keyStyle);

            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static string OwnedSummary(UpgradeState state)
        {
            UpgradeDef[] all = UpgradeCatalog.For(state.Character);
            if (all == null) return "";

            var names = new System.Collections.Generic.List<string>();
            foreach (UpgradeDef def in all)
                if (state.Has(def.Bit)) names.Add(def.Name);
            return string.Join("  ·  ", names);
        }
    }
}
