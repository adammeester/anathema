using Anathema.Abilities;
using Anathema.Combat;
using Anathema.Upgrades;
using System.Collections.Generic;
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
        const float SlotGap = 28f;   // room for the round frames' brass rims
        const float BarWidth = 380f;
        const float BarHeight = 50f;
        const float BarGap = 44f;    // room for the bar frame and the slot rim beside it
        const float BottomMargin = 30f;
        const float Frame = 4f;
        const float TooltipScale = 1.2f; // the Alt-hover ability tooltip, relative to the rest of the HUD
        const float HealthSegmentSize = Health.ChunkSize; // health per chunk on the health bar

        static readonly Color Gold = new Color(0.86f, 0.70f, 0.30f);
        static readonly Color DarkGold = new Color(0.45f, 0.34f, 0.12f);
        static readonly Color Panel = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        static readonly Color HealthColor = new Color(0.80f, 0.12f, 0.16f);
        static readonly Color EnergyColor = new Color(0.20f, 0.65f, 1f);

        Health health;
        Energy energy;
        Ability[] abilities;
        UpgradeState upgrades;
        Texture2D pixel;
        Texture2D sheen;        // white, alpha fading top (bright) to bottom (clear)
        float scale = 1f;

        // Painted frames (round ability slots, rounded health/energy bars, faction emblem), rebuilt when the scale changes.
        HudArt art;
        float artScale;
        bool characterLooked;
        string characterKey;    // e.g. "KhorneBerserker"; picks the emblem and the ability icon folder
        HudArt.Emblem emblem;
        Color accent = Gold;    // the character's roster colour, on the slots' inner rings

        void Awake()
        {
            health = GetComponent<Health>();
            energy = GetComponent<Energy>();
            abilities = System.Array.FindAll(GetComponents<Ability>(), a => a.ShownOnBar);

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
            EnsureArt();

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
            Ability hovered = null;
            Rect hoveredSlot = default;
            bool canHover = MouseAim.AltHeld && Cursor.visible;
            foreach (Ability ability in abilities)
            {
                var r = new Rect(x, slotY, slot, slot);
                DrawSlot(r, ability);
                if (canHover && r.Contains(Event.current.mousePosition)) { hovered = ability; hoveredSlot = r; }
                x += slot + gap;
            }

            DrawPassives(centerX, slotY, abilityWidth);
            DrawPassiveStacks(centerX, slotY);
            DrawAllies();
            if (hovered != null) DrawAbilityTooltip(hovered, hoveredSlot);
        }

        // ---------- Ability tooltip (hold Alt and hover a slot) ----------

        Net.CharacterSelectView.CharacterInfo rosterEntry;
        bool rosterLooked;

        // Same text as the champion-select tooltip, found in the roster by the slot's name (or key, if an upgrade renamed it).
        void DrawAbilityTooltip(Ability ability, Rect slotRect)
        {
            bool ex = Input.GetKey(KeyCode.LeftShift) && (ability.ExSibling != null || ability.ExCost > 0f);
            Ability shown = ex && ability.ExSibling != null ? ability.ExSibling : ability;
            string name = ex ? shown.ExName : shown.DisplayName;
            string key = ex ? "Shift+" + shown.KeyLabel : shown.KeyLabel;
            Net.CharacterSelectView.AbilityInfo info = FindAbilityInfo(name, key, ex);

            Color accent = ex ? new Color(0.8f, 0.6f, 1f) : (rosterEntry != null ? rosterEntry.accent : Gold);
            string tag = ex ? "EX ABILITY" : info != null && info.ultimate ? "ULTIMATE" : "ABILITY";
            string description = info != null ? info.description : "";
            string stats = info != null ? info.stats : "";

            float ts = scale * TooltipScale;
            float tipW = 460f * ts, pad = 14f * ts, textW = tipW - pad * 2f;
            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24f * ts), fontStyle = FontStyle.Bold, wordWrap = false };
            nameStyle.normal.textColor = accent;
            var tagStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15f * ts), fontStyle = FontStyle.Bold, wordWrap = false };
            tagStyle.normal.textColor = new Color(0.6f, 0.58f, 0.55f);
            var descStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19f * ts), wordWrap = true };
            descStyle.normal.textColor = new Color(0.9f, 0.88f, 0.85f);
            var statStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17f * ts), wordWrap = true };
            statStyle.normal.textColor = Gold;

            float descH = string.IsNullOrEmpty(description) ? 0f : descStyle.CalcHeight(new GUIContent(description), textW);
            float statH = string.IsNullOrEmpty(stats) ? 0f : statStyle.CalcHeight(new GUIContent(stats), textW);
            float tipH = 66f * ts + descH + (statH > 0f ? 14f * ts + statH : 0f) + 12f * ts;

            // Above the slot, centred on it, kept on screen.
            var tip = new Rect(slotRect.center.x - tipW * 0.5f, slotRect.y - tipH - 36f * ts, tipW, tipH); // clears the frame's top spike
            tip.x = Mathf.Clamp(tip.x, 10f * ts, Screen.width - tipW - 10f * ts);
            tip.y = Mathf.Max(10f * ts, tip.y);

            DrawRect(new Rect(tip.x - 2f * ts, tip.y - 2f * ts, tip.width + 4f * ts, tip.height + 4f * ts), accent);
            DrawRect(tip, new Color(0.04f, 0.05f, 0.08f, 0.98f));
            string title = string.IsNullOrEmpty(key) ? name : $"{name}  [{key}]";

            // The ability's icon beside the name, in a ring of the tooltip's colour.
            Texture2D icon = IconFor(shown, ex);
            float iconW = 0f;
            if (icon != null)
            {
                float d = 52f * ts;
                var iconRect = new Rect(tip.x + pad, tip.y + 7f * ts, d, d);
                GUI.DrawTexture(iconRect, icon);
                float k = d * 0.5f / HudArt.SlotRadius;
                Color previous = GUI.color;
                GUI.color = accent;
                GUI.DrawTexture(ArtRect(art.SlotRing, iconRect.center, k), art.SlotRing.Tex);
                GUI.color = previous;
                iconW = d + 12f * ts;
            }
            GUI.Label(new Rect(tip.x + pad + iconW, tip.y + 8f * ts, textW - iconW, 32f * ts), title, nameStyle);
            GUI.Label(new Rect(tip.x + pad + iconW, tip.y + 38f * ts, textW - iconW, 24f * ts), tag, tagStyle);
            float y = tip.y + 66f * ts;
            if (descH > 0f) GUI.Label(new Rect(tip.x + pad, y, textW, descH), description, descStyle);
            if (statH > 0f)
            {
                y += descH + 4f * ts;
                DrawRect(new Rect(tip.x + pad, y, textW, Mathf.Max(1f, ts)), DarkGold);
                GUI.Label(new Rect(tip.x + pad, y + 6f * ts, textW, statH), stats, statStyle);
            }
        }

        Net.CharacterSelectView.AbilityInfo FindAbilityInfo(string name, string key, bool ex)
        {
            if (!rosterLooked)
            {
                rosterLooked = true;
                if (upgrades == null) upgrades = GetComponent<UpgradeState>();
                string character = upgrades != null ? upgrades.Character : null;
                if (!string.IsNullOrEmpty(character))
                    foreach (var c in Net.CharacterSelectView.Roster)
                        if (c.prefabPath.EndsWith("Player_" + character, System.StringComparison.Ordinal)) { rosterEntry = c; break; }
            }
            if (rosterEntry == null || rosterEntry.abilities == null) return null;

            foreach (var a in rosterEntry.abilities)
                if (a.ex == ex && string.Equals(a.name, name, System.StringComparison.OrdinalIgnoreCase)) return a;
            if (!string.IsNullOrEmpty(key))
                foreach (var a in rosterEntry.abilities)
                    if (a.ex == ex && string.Equals(a.key, key, System.StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        // ---------- Passive stacks (e.g. the Hospitaller's Faith) ----------

        IPassiveStacks passiveStacks;

        // A row of pips centred just above the action bar: filled for each stack, dark sockets for the rest, a thin bar
        // under them filling towards the next stack, and a soft pulse when the passive is full.
        void DrawPassiveStacks(float centerX, float slotY)
        {
            if (passiveStacks == null) passiveStacks = GetComponent<IPassiveStacks>();
            if (passiveStacks == null || passiveStacks.MaxStacks <= 0) return;

            int max = passiveStacks.MaxStacks;
            int stacks = Mathf.Clamp(passiveStacks.Stacks, 0, max);
            Color color = passiveStacks.StackColor;
            float pip = 20f * scale, gap = 9f * scale;
            float rowW = max * pip + (max - 1) * gap;
            float y = slotY - 44f * scale - pip; // above the slot frames' top spikes
            float x = centerX - rowW * 0.5f;
            bool full = stacks >= max;
            float pulse = full ? 0.75f + 0.25f * Mathf.Sin(Time.time * 6f) : 1f;

            // Name to the left of the row.
            DrawText(new Rect(x - 130f * scale, y - 4f * scale, 120f * scale, pip + 8f * scale), passiveStacks.PassiveName.ToUpperInvariant(),
                Mathf.RoundToInt(18f * scale), FontStyle.Bold, stacks > 0 ? color : new Color(1f, 1f, 1f, 0.45f));

            for (int i = 0; i < max; i++)
            {
                var r = new Rect(x + i * (pip + gap), y, pip, pip);
                if (full) DrawRect(new Rect(r.x - 3f * scale, r.y - 3f * scale, r.width + 6f * scale, r.height + 6f * scale),
                    new Color(color.r, color.g, color.b, 0.35f * pulse));
                DrawRect(r, Color.black);
                DrawRect(new Rect(r.x + 2f * scale, r.y + 2f * scale, r.width - 4f * scale, r.height - 4f * scale),
                    i < stacks ? new Color(color.r * pulse, color.g * pulse, color.b * pulse, 1f) : new Color(0.14f, 0.13f, 0.12f, 1f));
            }

            float next = passiveStacks.NextStackProgress;
            if (!full && next > 0f)
            {
                var bar = new Rect(x, y + pip + 4f * scale, rowW, 3f * scale);
                DrawRect(bar, new Color(0f, 0f, 0f, 0.6f));
                DrawRect(new Rect(bar.x, bar.y, bar.width * next, bar.height), new Color(color.r, color.g, color.b, 0.8f));
            }
        }

        // ---------- Allies ----------

        // Nameplates: teammates down the left edge (portrait, health, energy, upgrades), enemies down the right edge
        // (portrait, health, upgrades; their energy stays hidden). Eliminated fighters are greyed and crossed out.
        void DrawAllies()
        {
            float y = 180f * scale;
            float enemyY = 180f * scale;

            foreach (Net.NetPlayer p in Net.NetPlayer.All)
            {
                if (p == null || p.Health == null || p.Health == health) continue;
                bool ally = p.Health.Team == health.Team;

                UpgradeState up = p.GetComponent<UpgradeState>();
                var owned = new List<UpgradeDef>();
                UpgradeDef[] all = up != null ? UpgradeCatalog.For(up.Character) : null;
                if (all != null) foreach (UpgradeDef d in all) if (up.Has(d.Bit)) owned.Add(d);

                string name = Net.NetGame.Online ? Net.NetGame.NameOf(p.OwnerClientId)
                    : up != null && !string.IsNullOrEmpty(up.Character) ? up.Character : p.name;
                string key = up != null ? up.Character : null;
                bool dead = p.Health.IsDead || p.Health.Current <= 0f;
                if (ally)
                    DrawAllyFrame(ref y, name, key, p.Health.Current, p.Health.Max, p.EnergyCurrent, p.EnergyMax, owned, dead, false);
                else
                    DrawAllyFrame(ref enemyY, name, key, p.Health.Current, p.Health.Max, 0f, 0f, owned, dead, true);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Training arena has no teammates: F9 shows two made-up ones so the frames can be checked.
            if (!Net.NetGame.Online)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F9) demoAllies = !demoAllies;
                if (demoAllies)
                {
                    UpgradeDef[] cat = upgrades != null ? UpgradeCatalog.For(upgrades.Character) : null;
                    var few = new List<UpgradeDef>();
                    var many = new List<UpgradeDef>();
                    if (cat != null)
                        for (int i = 0; i < cat.Length; i++)
                        {
                            if (i < 3) few.Add(cat[i]);
                            many.Add(cat[i]);
                        }
                    DrawDemoRoundBanner();
                    string key = upgrades != null ? upgrades.Character : null;
                    DrawAllyFrame(ref y, "Demo Ally A", key, 140f, 200f, 65f, 100f, few, false, false);
                    DrawAllyFrame(ref y, "Demo Ally B", key, 0f, 250f, 0f, 100f, many, true, false);
                    DrawAllyFrame(ref enemyY, "Demo Enemy A", key, 180f, 250f, 0f, 0f, many, false, true);
                    DrawAllyFrame(ref enemyY, "Demo Enemy B", key, 0f, 200f, 0f, 0f, few, true, true);
                }
            }
#endif
            DrawHoverTip();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool demoAllies;

        // Mock of NetGame's top-centre round counter, score and timer, for checking layout offline.
        void DrawDemoRoundBanner()
        {
            float cx = Screen.width * 0.5f, top = 14f * scale, hgt = 44f * scale;
            var timer = new Rect(cx - 60f * scale, top, 120f * scale, hgt);
            DrawFrame(timer, 3f * scale, DarkGold);
            int secs = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            DrawText(timer, $"{secs / 60}:{secs % 60:00}", Mathf.RoundToInt(26f * scale), FontStyle.Bold, Color.white);

            // Best of 3: you have won 1 round, the enemy 0.
            Net.RoundMarbles.Draw(cx, top, scale, 74f * scale, hgt, 2, 1, new[] { 0 });
        }
#endif

        static readonly Color EnemyHealthColor = new Color(0.62f, 0.10f, 0.12f);
        static readonly Color AllyHealthColor = new Color(0.22f, 0.72f, 0.30f);
        static readonly Color DeadGrey = new Color(0.32f, 0.32f, 0.34f);

        // Battlerite-style frame: a head-shot portrait with the health bar (and, for allies, energy) under it, and the
        // fighter's upgrades in a grid beside the portrait (hover one for its name). Enemy frames are mirrored on the
        // right edge. An eliminated fighter's frame is greyed out with a big cross over it.
        void DrawAllyFrame(ref float y, string name, string key, float current, float max, float energyNow, float energyMax,
            List<UpgradeDef> owned, bool dead, bool enemy)
        {
            const int PerRow = 5;
            float portrait = 84f * scale, barH = 18f * scale, gap = 8f * scale;
            float pip = 24f * scale, pipGap = 4f * scale, nameH = 20f * scale;
            float edge = 26f * scale;
            float x = enemy ? Screen.width - edge - portrait : edge;
            bool showEnergy = !enemy;

            // Portrait.
            var pr = new Rect(x, y, portrait, portrait);
            DrawRect(new Rect(pr.x - 3f * scale, pr.y - 3f * scale, pr.width + 6f * scale, pr.height + 6f * scale), Panel);
            DrawRect(pr, new Color(0.10f, 0.12f, 0.18f, 1f));
            Texture tex = Net.AllyPortraits.Get(key);
            if (tex != null)
            {
                Color prev = GUI.color;
                if (dead) GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                GUI.DrawTexture(pr, tex, ScaleMode.ScaleAndCrop, false);
                GUI.color = prev;
            }
            if (dead) DrawRect(pr, new Color(0.18f, 0.18f, 0.2f, 0.7f)); // washes the portrait out to grey
            DrawBorder(pr, 3f * scale);

            // Health bar under the portrait, same width.
            var bar = new Rect(pr.x, pr.yMax + 7f * scale, portrait, barH);
            DrawBorder(bar, 2f * scale);
            DrawRect(bar, dead ? DeadGrey * 0.5f : Color.black);
            Color hpColor = enemy ? EnemyHealthColor : AllyHealthColor;
            Rect fill = DrawFill(bar, dead || max <= 0f ? 0f : Mathf.Clamp01(current / max), hpColor, !enemy);
            if (fill.width > 0f) GUI.DrawTexture(new Rect(fill.x, fill.y, fill.width, fill.height * 0.55f), sheen);
            DrawText(bar, dead ? "DOWN" : Mathf.CeilToInt(current).ToString(), Mathf.RoundToInt(13f * scale), FontStyle.Bold,
                dead ? new Color(0.7f, 0.7f, 0.7f) : Color.white);

            // Energy (allies only): a slimmer blue bar directly under the health bar.
            float energyH = 10f * scale;
            float barsBottom = bar.yMax;
            if (showEnergy)
            {
                var ebar = new Rect(bar.x, bar.yMax + 5f * scale, portrait, energyH);
                DrawBorder(ebar, 2f * scale);
                DrawRect(ebar, dead ? DeadGrey * 0.5f : Color.black);
                Rect efill = DrawFill(ebar, dead || energyMax <= 0f ? 0f : Mathf.Clamp01(energyNow / energyMax), EnergyColor, true);
                if (efill.width > 0f) GUI.DrawTexture(new Rect(efill.x, efill.y, efill.width, efill.height * 0.55f), sheen);
                barsBottom = ebar.yMax;
            }

            // Name and upgrades beside the portrait (right of it for allies, left of it for enemies).
            float gridW = PerRow * (pip + pipGap) - pipGap;
            float gx = enemy ? pr.x - gap - 3f * scale - gridW : pr.xMax + gap + 3f * scale;
            float nameW = 190f * scale;
            var nameRect = new Rect(enemy ? pr.x - gap - 3f * scale - nameW : gx, pr.y, nameW, nameH);
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = enemy ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                fontSize = Mathf.Max(8, Mathf.RoundToInt(15f * scale)), fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip
            };
            nameStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(nameRect.x + 1.5f * scale, nameRect.y + 1.5f * scale, nameRect.width, nameRect.height), name, nameStyle);
            nameStyle.normal.textColor = dead ? new Color(0.5f, 0.5f, 0.5f) : enemy ? new Color(1f, 0.45f, 0.4f) : Gold;
            GUI.Label(nameRect, name, nameStyle);

            Vector2 mouse = Event.current.mousePosition;
            for (int n = 0; n < owned.Count; n++)
            {
                int col = n % PerRow;
                float px = enemy ? gx + gridW - pip - col * (pip + pipGap) : gx + col * (pip + pipGap);
                var r = new Rect(px, pr.y + nameH + 6f * scale + n / PerRow * (pip + pipGap), pip, pip);
                Color c = dead ? DeadGrey : owned[n].Accent;
                DrawRect(new Rect(r.x - 1.5f * scale, r.y - 1.5f * scale, r.width + 3f * scale, r.height + 3f * scale), Panel);
                DrawRect(r, new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f));
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * 0.5f), sheen);
                string nm = owned[n].Name;
                DrawText(r, string.IsNullOrEmpty(nm) ? "?" : nm.Substring(0, 1), Mathf.RoundToInt(14f * scale), FontStyle.Bold,
                    dead ? new Color(0.65f, 0.65f, 0.65f) : Color.white);
                if (r.Contains(mouse)) { hoverTip = nm; hoverRect = r; hoverColor = owned[n].Accent; hoverLeft = enemy; }
            }

            int pipRows = owned.Count > 0 ? Mathf.CeilToInt(owned.Count / (float)PerRow) : 0;
            float upgradesH = nameH + 6f * scale + pipRows * (pip + pipGap);
            float frameH = Mathf.Max(barsBottom - pr.y, upgradesH);

            // Eliminated: a big red cross over the portrait and bars.
            if (dead)
            {
                var cross = new Rect(pr.x - 4f * scale, pr.y - 4f * scale, portrait + 8f * scale, barsBottom - pr.y + 8f * scale);
                DrawCross(cross, 9f * scale, new Color(0.85f, 0.12f, 0.12f, 0.92f));
            }

            y += frameH + 22f * scale;
        }

        // Two thick diagonal strokes corner to corner (with a dark outline so it reads on any portrait).
        void DrawCross(Rect r, float thickness, Color color)
        {
            float len = Mathf.Sqrt(r.width * r.width + r.height * r.height);
            float angle = Mathf.Atan2(r.height, r.width) * Mathf.Rad2Deg;
            Matrix4x4 saved = GUI.matrix;
            foreach (float a in new[] { angle, -angle })
            {
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(a, r.center);
                float o = 2.5f * scale;
                DrawRect(new Rect(r.center.x - len * 0.5f - o, r.center.y - thickness * 0.5f - o, len + o * 2f, thickness + o * 2f), new Color(0f, 0f, 0f, 0.8f));
                DrawRect(new Rect(r.center.x - len * 0.5f, r.center.y - thickness * 0.5f, len, thickness), color);
            }
            GUI.matrix = saved;
        }

        string hoverTip;
        Rect hoverRect;
        Color hoverColor;
        bool hoverLeft; // enemy frames are on the right edge, so their tooltips open to the left

        void DrawHoverTip()
        {
            if (string.IsNullOrEmpty(hoverTip)) return;
            float w = Mathf.Max(120f, hoverTip.Length * 9.5f) * scale, h = 26f * scale;
            var r = new Rect(hoverLeft ? hoverRect.x - 8f * scale - w : hoverRect.xMax + 8f * scale, hoverRect.center.y - h * 0.5f, w, h);
            DrawRect(new Rect(r.x - 2f * scale, r.y - 2f * scale, r.width + 4f * scale, r.height + 4f * scale), hoverColor);
            DrawRect(r, new Color(0.04f, 0.05f, 0.08f, 0.96f));
            DrawText(r, hoverTip, Mathf.RoundToInt(15f * scale), FontStyle.Bold, Color.white);
            hoverTip = null;
        }

        // ---------- Passives and upgrades ----------

        // The upgrades taken this match at the bottom-left.
        void DrawPassives(float centerX, float slotY, float abilityWidth)
        {
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
                    FontStyle.Bold, all[i].Accent);
                y -= lineH;
            }
        }

        // ---------- Bars ----------

        // fillFromLeft = false makes the bar drain towards the centre (mirrored), so the pair frames the ability bar.
        void DrawBar(Rect r, float value, Color color, string label, bool fillFromLeft, float maxValue, float segmentSize, float recoverable = 0f)
        {
            bool isHealth = segmentSize > 0f;
            value = Mathf.Clamp01(value);

            // The bar is a capsule inside an iron-and-brass frame; every fill is clipped to the capsule through its mask.
            float k = art.BarK;
            var anchor = new Vector2(r.x, r.center.y);
            Rect mask = art.BarMask.Place(anchor, k);

            // Health: healable missing health is black, permanently lost health is left empty (see-through).
            // Other bars (energy) have a solid black backing.
            float shown = isHealth ? Mathf.Clamp01(value + recoverable) : 1f; // how much of the bar is not empty
            if (shown > value) DrawArtPart(art.BarMask.Tex, mask, FillRect(r, shown, fillFromLeft), Color.black);
            Rect fill = FillRect(r, value, fillFromLeft);
            DrawArtPart(art.BarMask.Tex, mask, fill, color);
            DrawArtPart(art.BarSheen.Tex, mask, fill, Color.white);

            // Black dividers split the bar into chunks (one per `segmentSize` health, or 10 chunks for non-health bars).
            // On the health bar they only show over the filled/black part, never over the empty part. Near the rounded
            // ends they are cut short to stay inside the capsule.
            int segments = isHealth ? Mathf.Max(1, Mathf.RoundToInt(maxValue / segmentSize)) : 10;
            for (int i = 1; i < segments; i++)
            {
                float frac = (float)i / segments;
                if (frac > shown + 0.0001f) break;
                float tx = r.x + r.width * frac;
                float half = CapsuleHalfHeight(r, tx);
                DrawRect(new Rect(tx - 1.5f * scale, r.center.y - half, 3f * scale, half * 2f), Color.black);
            }

            // Frame on top; its end spike points away from the ability bar. The faction emblem sits on the health bar's
            // outer end.
            DrawArt(fillFromLeft ? art.BarFrameLeft : art.BarFrameRight, anchor, k, Color.white);
            if (isHealth)
            {
                float ex = fillFromLeft ? r.x + HudArt.EmblemOffset * k : r.xMax - HudArt.EmblemOffset * k;
                DrawArt(art.EmblemArt, new Vector2(ex, r.center.y), k, Color.white);
            }

            DrawText(r, label, Mathf.RoundToInt(22f * scale), FontStyle.Bold, Color.white);
        }

        static Rect FillRect(Rect r, float t, bool fromLeft)
        {
            float w = r.width * Mathf.Clamp01(t);
            return new Rect(fromLeft ? r.x : r.xMax - w, r.y, w, r.height);
        }

        // Half the height of the capsule inscribed in r at screen x (the full half-height away from the rounded ends).
        static float CapsuleHalfHeight(Rect r, float x)
        {
            float rad = r.height * 0.5f;
            float d = Mathf.Min(x - r.x, r.xMax - x);
            if (d >= rad) return rad;
            float t = rad - d;
            return Mathf.Sqrt(Mathf.Max(0f, rad * rad - t * t));
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
            // An EX kept in its own component takes over its base ability's slot while Shift is held.
            if (Input.GetKey(KeyCode.LeftShift) && ability.ExSibling != null) ability = ability.ExSibling;
            float cd = ability.CooldownNormalized;
            bool ready = cd <= 0f && !ability.IsCasting && ability.Available; // mid wind-up: dim, but no cooldown curtain yet

            // While Shift is held (EX mode) abilities without an EX version are greyed out and the EX ones are highlighted.
            bool exMode = Input.GetKey(KeyCode.LeftShift);
            bool hasEx = ability.ExCost > 0f;
            bool exShow = exMode && hasEx;                 // EX version always visible while EX is open
            bool exLit = exShow && !ability.ExBlocked;     // ...but only lit when affordable
            bool exDim = exMode && !hasEx;
            bool exLocked = exShow && !exLit;              // EX ability visible but can't afford: greyed out

            // The slot is round: a grimy disc inside an iron-and-brass frame. Curtains and tints are clipped to the disc
            // through its mask, so they keep the round edge.
            float k = art.SlotK;
            Vector2 c = r.center;
            Rect disc = art.SlotMask.Place(c, k);
            DrawArt(art.SlotDisc, c, k, Color.white);
            // The ability's painted icon (if it has one) fills the disc, dimmed while the ability can't be used.
            Texture2D icon = IconFor(ability, exShow);
            if (icon != null)
            {
                Color previous = GUI.color;
                GUI.color = ready && !exLocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
                GUI.DrawTexture(r, icon);
                GUI.color = previous;
            }
            if (exLit) DrawArt(art.SlotMask, c, k, new Color(0.15f, 0.40f, 0.75f, icon != null ? 0.22f : 0.35f));

            // Partial recharge (e.g. one of two axes spent): a faint curtain with a thin leading edge, behind the name.
            float recharge = ability.ChargeRechargeNormalized;
            if (recharge > 0f && !exDim)
            {
                float rh = r.height * recharge;
                DrawArtPart(art.SlotMask.Tex, disc, new Rect(r.x, r.y, r.width, rh), new Color(0f, 0f, 0f, 0.38f));
                DrawArtPart(art.SlotMask.Tex, disc, new Rect(r.x, r.y + rh - 2f * scale, r.width, 2f * scale), new Color(Gold.r, Gold.g, Gold.b, 0.45f));
            }

            // Without an icon, the ability's name, wrapped inside the slot (inset a little, as the slot is round).
            if (icon == null) DrawText(new Rect(r.x + r.width * 0.08f, r.y + r.height * 0.2f, r.width * 0.84f, r.height * 0.6f), exShow ? ability.ExName : ability.DisplayName,
                Mathf.RoundToInt(19f * scale), FontStyle.Bold, ready && !exLocked ? Color.white : new Color(1f, 1f, 1f, 0.55f));

            if (!ready && cd <= 0f && !ability.IsCasting)
            {
                // Not on cooldown, just unavailable (e.g. ultimate without enough energy): plain grey-out, no timer.
                DrawArt(art.SlotMask, c, k, new Color(0f, 0f, 0f, 0.55f));
            }
            else if (!ready)
            {
                // Dark curtain that drains upward as the cooldown runs out, with a bright leading edge.
                float h = r.height * cd;
                DrawArtPart(art.SlotMask.Tex, disc, new Rect(r.x, r.y, r.width, h), new Color(0f, 0f, 0f, 0.72f));
                DrawArtPart(art.SlotMask.Tex, disc, new Rect(r.x, r.y + h - 2f * scale, r.width, 2f * scale), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
                DrawText(r, ability.CooldownRemaining.ToString("0.0"), Mathf.RoundToInt(30f * scale), FontStyle.Bold, Color.white);
            }

            // The frame on top: its brass dulls while the ability isn't ready, and the inner ring carries the
            // character's colour (blue for a lit EX).
            bool bright = exLit || (ready && !exDim);
            DrawArt(art.SlotFrame, c, k, bright ? Color.white : new Color(0.62f, 0.60f, 0.58f, 1f));
            Color ring = exLit ? EnergyColor : accent;
            DrawArt(art.SlotRing, c, k, new Color(ring.r, ring.g, ring.b, bright ? 0.9f : 0.4f));

            // Charge gems set into the top of the brass band: gold for each ready charge, dark for spent ones.
            if (ability.ChargeCapacity > 1 && !exShow)
                DrawChargeGems(c, ability.ChargeCapacity, ability.ChargesReady);
            else if (exShow && ability.ExChargeCapacity > 1)
                DrawChargeGems(c, ability.ExChargeCapacity, ability.ExChargesReady);

            if (exDim)
            {
                DrawArt(art.SlotMask, c, k, new Color(0f, 0f, 0f, 0.65f));
                DrawArt(art.SlotFrame, c, k, new Color(0f, 0f, 0f, 0.65f));
            }

            // Key plate on the frame's lower right (the skull keeps the bottom).
            if (!string.IsNullOrEmpty(ability.KeyLabel))
                DrawKeyPlate(c + new Vector2(36f, 46f) * k, ability.KeyLabel, ready && !exDim);

            if (exShow)
            {
                if (exLocked) DrawArt(art.SlotMask, c, k, new Color(0f, 0f, 0f, 0.5f));
                // Energy cost at the top of the slot; dimmed when the surgeon can't afford it.
                bool affordable = energy != null && energy.Current >= ability.ExCost;
                DrawText(new Rect(r.x, r.y + 2f * scale, r.width, 22f * scale), $"EX {Mathf.CeilToInt(ability.ExCost)}",
                    Mathf.RoundToInt(16f * scale), FontStyle.Bold, affordable ? EnergyColor : new Color(EnergyColor.r, EnergyColor.g, EnergyColor.b, 0.5f));
            }
        }

        // Gems along the top of the slot's brass band, centred under the spike.
        void DrawChargeGems(Vector2 c, int capacity, int ready)
        {
            float k = art.SlotK, step = 16f * Mathf.Deg2Rad;
            for (int i = 0; i < capacity; i++)
            {
                float a = -Mathf.PI * 0.5f + (i - (capacity - 1) * 0.5f) * step;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (HudArt.SlotRingRadius * k);
                DrawArt(art.Gem, p, k, i < ready ? Gold : new Color(0.12f, 0.11f, 0.10f, 1f));
            }
        }

        // A dark pill with a brass rim, centred on `center`, sized to the label: the pill texture's rounded caps are drawn
        // as they are and its straight middle is stretched.
        void DrawKeyPlate(Vector2 center, string label, bool lit)
        {
            Texture2D pill = art.Pill;
            float h = pill.height, cap = h * 0.5f;
            int fontSize = Mathf.RoundToInt(15f * scale);
            var measure = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold };
            float w = Mathf.Max(h * 1.3f, measure.CalcSize(new GUIContent(label)).x + h * 0.7f);
            var plate = new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);

            Color previous = GUI.color;
            GUI.color = lit ? Gold : DarkGold;
            GUI.DrawTextureWithTexCoords(new Rect(plate.x, plate.y, cap, h), pill, new Rect(0f, 0f, 0.25f, 1f));
            GUI.DrawTextureWithTexCoords(new Rect(plate.x + cap, plate.y, w - 2f * cap, h), pill, new Rect(0.25f, 0f, 0.5f, 1f));
            GUI.DrawTextureWithTexCoords(new Rect(plate.xMax - cap, plate.y, cap, h), pill, new Rect(0.75f, 0f, 0.25f, 1f));
            GUI.color = previous;
            DrawText(plate, label, fontSize, FontStyle.Bold, lit ? Gold : new Color(0.7f, 0.7f, 0.7f));
        }

        // ---------- Painted art ----------

        void EnsureArt()
        {
            if (!characterLooked)
            {
                characterLooked = true;
                string character = CharacterStats.Identify(gameObject);
                characterKey = character;
                emblem = HudArt.EmblemFor(character);
                if (!string.IsNullOrEmpty(character))
                    foreach (var c in Net.CharacterSelectView.Roster)
                        if (c.prefabPath.EndsWith("Player_" + character, System.StringComparison.Ordinal)) { accent = c.accent; break; }
            }
            if (art != null && Mathf.Approximately(artScale, scale)) return;

            art?.Dispose();
            artScale = scale;
            art = HudArt.Build(
                slotK: SlotSize * scale * 0.5f / HudArt.SlotRadius,
                barK: BarHeight * scale * 0.5f / HudArt.BarHalfHeight,
                barDesignWidth: BarWidth / BarHeight * 2f * HudArt.BarHalfHeight,
                emblem: emblem,
                pillHeight: 26f * scale);
        }

        void OnDestroy() => art?.Dispose();

        // Ability icons, loaded from Resources/AbilityIcons/<Character>/<AbilityClass>.png (rendered from the SVGs in
        // Art/AbilityIcons by Tools/IconRender). An EX version kept in the same component is <AbilityClass>_EX and falls
        // back to the base icon. Abilities without an icon return null and keep their name in the slot.
        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        Texture2D IconFor(Ability ability, bool ex)
        {
            if (ability == null || string.IsNullOrEmpty(characterKey)) return null;
            string type = ability.GetType().Name;
            if (ex)
            {
                Texture2D exIcon = LoadIcon(type + "_EX");
                if (exIcon != null) return exIcon;
            }
            return LoadIcon(type);
        }

        Texture2D LoadIcon(string name)
        {
            if (!icons.TryGetValue(name, out Texture2D tex))
            {
                tex = Resources.Load<Texture2D>($"AbilityIcons/{characterKey}/{name}");
                icons[name] = tex; // remember misses too
            }
            return tex;
        }

        // Screen rect for art drawn at any size: design (0,0) at `center`, `k` pixels per design unit.
        static Rect ArtRect(HudArt.Art a, Vector2 center, float k)
            => new Rect(center.x + a.Box.xMin * k, center.y + a.Box.yMin * k, a.Box.width * k, a.Box.height * k);

        // Draws art with its design origin at `anchor`, tinted.
        void DrawArt(HudArt.Art a, Vector2 anchor, float k, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(a.Place(anchor, k), a.Tex);
            GUI.color = previous;
        }

        // Draws only the `part` (screen rect) of a texture placed at `placed`, by cropping its texture coordinates: a
        // white mask drawn this way gives a tinted curtain that keeps the mask's round edge.
        void DrawArtPart(Texture2D tex, Rect placed, Rect part, Color tint)
        {
            float x0 = Mathf.Max(part.xMin, placed.xMin), x1 = Mathf.Min(part.xMax, placed.xMax);
            float y0 = Mathf.Max(part.yMin, placed.yMin), y1 = Mathf.Min(part.yMax, placed.yMax);
            if (x1 <= x0 || y1 <= y0) return;
            var uv = new Rect((x0 - placed.x) / placed.width, 1f - (y1 - placed.y) / placed.height,
                (x1 - x0) / placed.width, (y1 - y0) / placed.height);
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(x0, y0, x1, y1), tex, uv);
            GUI.color = previous;
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
