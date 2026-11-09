using Anathema.Abilities;
using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Player
{
    // Bottom-centre HUD drawn with OnGUI. Health on the left, ability bar (cooldowns) centred, energy on the right.
    // Everything is laid out for a 1080p screen and scaled by screen height.
    [RequireComponent(typeof(Health))]
    public class PlayerHud : MonoBehaviour
    {
        // Layout, in 1080p pixels.
        const float SlotSize = 92f;
        const float SlotGap = 10f;
        const float BarWidth = 380f;
        const float BarHeight = 50f;
        const float BarGap = 28f;
        const float BottomMargin = 30f;
        const float Frame = 4f;
        const float HealthSegmentSize = Health.ChunkSize; // health per chunk on the health bar

        static readonly Color Gold = new Color(0.86f, 0.70f, 0.30f);
        static readonly Color DarkGold = new Color(0.45f, 0.34f, 0.12f);
        static readonly Color Panel = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        static readonly Color HealthColor = new Color(0.80f, 0.12f, 0.16f);
        static readonly Color EnergyColor = new Color(0.20f, 0.65f, 1f);

        Health health;
        Energy energy;
        Ability[] abilities;
        Rage rage;
        UpgradeState upgrades;
        Texture2D pixel;
        Texture2D sheen;        // white, alpha fading top (bright) to bottom (clear)
        float scale = 1f;

        void Awake()
        {
            health = GetComponent<Health>();
            energy = GetComponent<Energy>();
            abilities = GetComponents<Ability>();

            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            sheen = new Texture2D(1, 16) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f; // row 0 is the bottom
                sheen.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.38f, t * t)));
            }
            sheen.Apply();

        }

        void OnGUI()
        {
            scale = Screen.height / 1080f;

            int count = abilities.Length;
            float slot = SlotSize * scale, gap = SlotGap * scale;
            float abilityWidth = count > 0 ? count * slot + (count - 1) * gap : 0f;
            float centerX = Screen.width * 0.5f;
            float bottom = Screen.height - BottomMargin * scale;
            float barW = BarWidth * scale, barH = BarHeight * scale, barGap = BarGap * scale;
            float barY = bottom - barH - Frame * scale; // bars sit on the slots' baseline

            DrawBar(new Rect(centerX - abilityWidth * 0.5f - barGap - barW, barY, barW, barH),
                health.Normalized, HealthColor,
                $"{Mathf.FloorToInt(health.Current)} / {Mathf.CeilToInt(health.Max)}", true,
                health.Max, HealthSegmentSize, health.RecoverableNormalized);

            if (energy != null)
            {
                DrawBar(new Rect(centerX + abilityWidth * 0.5f + barGap, barY, barW, barH),
                    energy.Normalized, EnergyColor,
                    $"{Mathf.FloorToInt(energy.Current)} / {Mathf.CeilToInt(energy.Max)}", false, energy.Max, 0f);
            }

            float x = centerX - abilityWidth * 0.5f;
            float slotY = bottom - slot - Frame * scale;
            foreach (Ability ability in abilities)
            {
                DrawSlot(new Rect(x, slotY, slot, slot), ability);
                x += slot + gap;
            }

            DrawPassives(centerX, slotY, abilityWidth);
        }

        // ---------- Passives and upgrades ----------

        // The Rage passive's stacks above the ability bar, and the upgrades taken this match at the bottom-left.
        void DrawPassives(float centerX, float slotY, float abilityWidth)
        {
            if (rage == null) rage = GetComponent<Rage>();
            if (rage != null && rage.Stacks > 0)
            {
                float bonus = Rage.AttackSpeedMultiplier(gameObject) - 1f;
                var r = new Rect(centerX - abilityWidth * 0.5f, slotY - 54f * scale, abilityWidth, 34f * scale);
                DrawText(r, $"RAGE  {new string('|', rage.Stacks)}   +{Mathf.RoundToInt(bonus * 100f)}% attack speed",
                    Mathf.RoundToInt(22f * scale), FontStyle.Bold, new Color(1f, 0.35f, 0.25f));
            }

            if (upgrades == null) upgrades = GetComponent<UpgradeState>();
            if (upgrades == null || upgrades.Mask == 0) return;

            UpgradeDef[] all = UpgradeCatalog.For(upgrades.Character);
            if (all == null) return;

            float lineH = 26f * scale;
            float y = Screen.height - 30f * scale - lineH;
            for (int i = all.Length - 1; i >= 0; i--)
            {
                if (!upgrades.Has(all[i].Bit)) continue;
                DrawText(new Rect(24f * scale, y, 360f * scale, lineH), all[i].Name, Mathf.RoundToInt(18f * scale),
                    FontStyle.Bold, UpgradeDef.CategoryColor(all[i].Category));
                y -= lineH;
            }
        }

        // ---------- Bars ----------

        // fillFromLeft = false makes the bar drain towards the centre (mirrored), so the pair frames the ability bar.
        void DrawBar(Rect r, float value, Color color, string label, bool fillFromLeft, float maxValue, float segmentSize, float recoverable = 0f)
        {
            float f = Frame * scale;
            bool isHealth = segmentSize > 0f;
            value = Mathf.Clamp01(value);

            // Health: border only. Healable missing health is black, permanently lost health is left empty (see-through).
            // Other bars (energy) keep the solid framed backing.
            if (isHealth) DrawBorder(r, f);
            else
            {
                DrawFrame(r, f);
                DrawRect(r, Color.black);
            }

            float shown = isHealth ? Mathf.Clamp01(value + recoverable) : 1f; // how much of the bar is not empty
            if (isHealth && shown > value) DrawFill(r, shown, Color.black, fillFromLeft);
            Rect fill = DrawFill(r, value, color, fillFromLeft);
            if (fill.width > 0f) GUI.DrawTexture(new Rect(fill.x, fill.y, fill.width, fill.height * 0.55f), sheen);

            // Black dividers split the bar into chunks (one per `segmentSize` health, or 10 chunks for non-health bars).
            // On the health bar they only show over the filled/black part, never over the empty part.
            int segments = isHealth ? Mathf.Max(1, Mathf.RoundToInt(maxValue / segmentSize)) : 10;
            for (int i = 1; i < segments; i++)
            {
                float frac = (float)i / segments;
                if (frac > shown + 0.0001f) break;
                float tx = r.x + r.width * frac;
                DrawRect(new Rect(tx - 1.5f * scale, r.y, 3f * scale, r.height), Color.black);
            }

            DrawText(r, label, Mathf.RoundToInt(22f * scale), FontStyle.Bold, Color.white);
        }

        Rect DrawFill(Rect r, float t, Color color, bool fromLeft)
        {
            float w = r.width * t;
            var rect = new Rect(fromLeft ? r.x : r.xMax - w, r.y, w, r.height);
            if (w > 0f) DrawRect(rect, color);
            return rect;
        }

        // ---------- Ability slots ----------

        void DrawSlot(Rect r, Ability ability)
        {
            float f = Frame * scale;
            float cd = ability.CooldownNormalized;
            bool ready = cd <= 0f && !ability.IsCasting && ability.Available; // mid wind-up: dim, but no cooldown curtain yet

            // While Shift is held (EX mode) abilities without an EX version are greyed out and the EX ones are highlighted.
            bool exMode = Input.GetKey(KeyCode.LeftShift);
            bool hasEx = ability.ExCost > 0f;
            bool exLit = exMode && hasEx && !ability.ExBlocked;
            bool exDim = exMode && !exLit;

            DrawFrame(r, f, exLit ? EnergyColor : (ready && !exDim ? Gold : DarkGold));
            DrawRect(r, exLit ? new Color(0.10f, 0.22f, 0.36f, 1f) : new Color(0.12f, 0.14f, 0.20f, 1f));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * 0.5f), sheen);

            // Ability name, wrapped inside the slot.
            DrawText(new Rect(r.x, r.y + r.height * 0.18f, r.width, r.height * 0.64f), exLit ? ability.ExName : ability.DisplayName,
                Mathf.RoundToInt(19f * scale), FontStyle.Bold, ready ? Color.white : new Color(1f, 1f, 1f, 0.55f));

            if (!ready && cd <= 0f && !ability.IsCasting)
            {
                // Not on cooldown, just unavailable (e.g. ultimate without enough energy): plain grey-out, no timer.
                DrawRect(r, new Color(0f, 0f, 0f, 0.55f));
            }
            else if (!ready)
            {
                // Dark curtain that drains upward as the cooldown runs out, with a bright leading edge.
                float h = r.height * cd;
                DrawRect(new Rect(r.x, r.y, r.width, h), new Color(0f, 0f, 0f, 0.72f));
                DrawRect(new Rect(r.x, r.y + h - 2f * scale, r.width, 2f * scale), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
                DrawText(r, ability.CooldownRemaining.ToString("0.0"), Mathf.RoundToInt(30f * scale), FontStyle.Bold, Color.white);
            }

            // Key badge on the bottom edge.
            if (!string.IsNullOrEmpty(ability.KeyLabel))
            {
                float bw = Mathf.Max(r.width * 0.5f, 46f * scale), bh = 24f * scale;
                var badge = new Rect(r.center.x - bw * 0.5f, r.yMax - bh * 0.5f + f * 0.5f, bw, bh);
                DrawRect(new Rect(badge.x - 2f * scale, badge.y - 2f * scale, badge.width + 4f * scale, badge.height + 4f * scale), ready ? Gold : DarkGold);
                DrawRect(badge, new Color(0.04f, 0.05f, 0.08f, 1f));
                DrawText(badge, ability.KeyLabel, Mathf.RoundToInt(15f * scale), FontStyle.Bold, ready ? Gold : new Color(0.7f, 0.7f, 0.7f));
            }

            if (exDim)
            {
                DrawRect(new Rect(r.x - f, r.y - f, r.width + 2f * f, r.height + 2f * f), new Color(0f, 0f, 0f, 0.65f));
            }
            else if (exLit)
            {
                // Energy cost at the top of the slot; dimmed when the surgeon can't afford it.
                bool affordable = energy != null && energy.Current >= ability.ExCost;
                DrawText(new Rect(r.x, r.y + 2f * scale, r.width, 22f * scale), $"EX {Mathf.CeilToInt(ability.ExCost)}",
                    Mathf.RoundToInt(16f * scale), FontStyle.Bold, affordable ? EnergyColor : new Color(EnergyColor.r, EnergyColor.g, EnergyColor.b, 0.5f));
            }
        }

        // ---------- Primitives ----------

        // Gold trim with a dark backing panel around rect r.
        void DrawFrame(Rect r, float f, Color? trim = null)
        {
            Color c = trim ?? Gold;
            DrawRect(new Rect(r.x - f - 3f * scale, r.y - f - 3f * scale, r.width + 2f * (f + 3f * scale), r.height + 2f * (f + 3f * scale)), Panel);
            DrawRect(new Rect(r.x - f, r.y - f, r.width + 2f * f, r.height + 2f * f), c);
            DrawRect(new Rect(r.x - f * 0.5f, r.y - f * 0.5f, r.width + f, r.height + f), new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f));
        }

        // Gold outline only; the inside stays see-through.
        void DrawBorder(Rect r, float f)
        {
            DrawRect(new Rect(r.x - f, r.y - f, r.width + 2f * f, f), Gold);
            DrawRect(new Rect(r.x - f, r.yMax, r.width + 2f * f, f), Gold);
            DrawRect(new Rect(r.x - f, r.y, f, r.height), Gold);
            DrawRect(new Rect(r.xMax, r.y, f, r.height), Gold);
        }

        void DrawText(Rect r, string text, int size, FontStyle fontStyle, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(8, size),
                fontStyle = fontStyle,
                wordWrap = true
            };
            // Drop shadow for legibility over bright fills.
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1.5f * scale, r.y + 1.5f * scale, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }
    }
}
