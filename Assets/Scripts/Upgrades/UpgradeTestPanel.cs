#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Anathema.Upgrades
{
    // Offline testing aid (editor and development builds only; never present online). Press F1 in the training arena
    // to open it:
    //   - click any upgrade to turn it on or off (weapons replace each other, as in a real match);
    //   - "Roll 3 offers" runs the real pick: the same three-card picker you get in the prep phase (click or 1-3);
    //   - "Clear all" removes everything.
    // Fight the training dummies to try the weapons, Rage, bleed, armour and the rest. F6/F7/F8 are shortcuts for the two
    // weapons and clear. Added by UpgradeState.
    public class UpgradeTestPanel : MonoBehaviour
    {
        UpgradeState state;
        readonly UpgradePickerView picker = new UpgradePickerView();
        bool open;
        Texture2D pixel;
        GUIStyle header, row, rowOn, button, hint;

        void Awake()
        {
            state = GetComponent<UpgradeState>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) open = !open;
            picker.HandleKeys(state);
        }

        void Build()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            header = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            header.normal.textColor = new Color(0.86f, 0.70f, 0.30f);
            hint = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            hint.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            row = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            rowOn = new GUIStyle(row) { fontStyle = FontStyle.Bold };
            rowOn.normal.textColor = Color.white;
            rowOn.hover.textColor = Color.white;
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            if (state == null || Anathema.Net.NetGame.Online) return;
            Build();

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = 1080f;

            if (state.Pending)
            {
                // A rolled pick: the real picker (round numbers are just a running count here).
                picker.Draw(w, h, state, 1, 99f);
                GUI.matrix = Matrix4x4.identity;
                return;
            }

            if (!open)
            {
                GUI.Label(new Rect(20, 16, 420, 26), "F1: upgrade test panel", hint);
                GUI.matrix = Matrix4x4.identity;
                return;
            }

            UpgradeDef[] all = UpgradeCatalog.For(state.Character);
            if (all == null)
            {
                GUI.matrix = Matrix4x4.identity;
                return;
            }

            const float rowH = 30f;
            var panel = new Rect(20, 16, 470, 150 + all.Length * (rowH + 4f) + 60f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(panel, pixel);
            GUI.color = old;

            GUI.Label(new Rect(panel.x + 14, panel.y + 8, panel.width - 28, 28), "UPGRADE TEST  (F1 to close)", header);
            GUI.Label(new Rect(panel.x + 14, panel.y + 38, panel.width - 28, 54),
                "Click to toggle an upgrade, then hit the dummies. Weapons replace each other. The cards use the real picker.", hint);

            float y = panel.y + 96;
            if (GUI.Button(new Rect(panel.x + 14, y, 222, 34), "Roll 3 offers", button)) state.DebugRoll();
            if (GUI.Button(new Rect(panel.x + 248, y, 208, 34), "Clear all", button)) state.ResetAll();
            y += 46;

            foreach (UpgradeDef def in all)
            {
                bool on = state.Has(def.Bit);
                Color tint = UpgradeDef.CategoryColor(def.Category);
                GUIStyle style = on ? rowOn : row;

                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = on ? tint : new Color(0.25f, 0.25f, 0.28f);
                string label = (on ? "[x] " : "[  ] ") + UpgradeDef.CategoryLabel(def.Category).Substring(0, 3) + "  " + def.Name;
                if (GUI.Button(new Rect(panel.x + 14, y, panel.width - 28, rowH), new GUIContent(label, def.Description), style))
                    state.DebugToggle(def.Bit);
                GUI.backgroundColor = previous;
                y += rowH + 4f;
            }

            // Description of the row under the mouse.
            string tip = GUI.tooltip;
            if (!string.IsNullOrEmpty(tip))
                GUI.Label(new Rect(panel.x + 14, y + 4, panel.width - 28, 70), tip, hint);

            GUI.matrix = Matrix4x4.identity;
        }
    }
}
#endif
