using UnityEngine;

namespace Anathema.Net
{
    // Everything the character-select screen needs that is not plain OnGUI layout: the roster data, a small 3D stage
    // that shows the full model of the highlighted champion, portrait renders of every champion's head (for the tiles)
    // and procedurally drawn ability icons. The stage lives far from the origin and is torn down when the lobby closes.
    public class CharacterSelectView
    {
        public enum Role { Melee, Ranged, Support }

        public class AbilityInfo
        {
            public string key, name, description, stats;
            public bool ultimate, ex;
            public Texture2D icon;
        }

        public class CharacterInfo
        {
            public string name, epithet, blurb, prefabPath;
            public Role role;
            public Color accent;
            public AbilityInfo[] abilities;
            public string footnote;
        }

        public static readonly CharacterInfo[] Roster =
        {
            new CharacterInfo
            {
                name = "Grey Knight Interceptor", epithet = "The Psychic Warrior-Monk", prefabPath = "Net/Player_GreyKnight",
                role = Role.Ranged, accent = new Color(0.45f, 0.75f, 1f),
                blurb = "Psychic warrior-monk. Ranged and precise, with a counter that turns into a teleport.",
                footnote = "Hold Shift in game to use EX abilities",
                abilities = new[]
                {
                    A("LMB", "Storm Bolter", "Hold to fire bolter rounds along your aim. Each hit builds energy.",
                        "Damage 8  |  Cooldown 0.85s  |  Range 14  |  +8 energy per hit"),
                    A("RMB", "Smite", "You focus for a moment while a crackling line tracks your cursor, then a psychic lance fires along it, piercing every enemy in its path until it hits a wall. C during the focus cancels it and refunds the cooldown.",
                        "Damage 30  |  Cooldown 4s  |  Range 16  |  0.35s focus  |  +10 energy per hit"),
                    A("Q", "Halberd Sweep", "Hold to sweep your halberd across a wide frontal arc. The damage lands when the wind-up ends; C during the wind-up cancels it.",
                        "Damage 20  |  Cooldown 0.9s  |  Range 2.5  |  120 degree arc  |  +8 energy per hit"),
                    A("E", "Consecrate", "Inscribe a warding sigil at the cursor. A ground telegraph fills for 0.6s, then it detonates, damaging and rooting every enemy inside. C cancels and refunds.",
                        "Damage 10  |  Cooldown 8s  |  Range 10  |  Radius 3  |  Root 0.75s"),
                    A("Space", "Counter", "Raise a psychic guard. A hit it catches is negated and answered with a halberd riposte. Afterwards, left-clicking teleports you to the aimed point and empowers your next halberd sweep.",
                        "Riposte 15  |  Cooldown 4s  |  Guard 1.2s  |  Teleport range 9  |  Window 2.5s"),
                    A("R", "Purgatus", "Costs a full energy bar. Channel for a moment while a telegraph fills, then release an expanding ring of warp-fire that damages, knocks back and silences everything it sweeps over. C during the channel cancels and refunds.",
                        "Damage 38  |  Radius 7  |  Knockback 4  |  Silence 1.5s  |  Costs 100 energy", true),
                    EX("Shift+E", "Sanctum", "Instead of a detonating sigil, a psychic dome rises at once at the cursor. Enemy projectiles are reflected and enemies inside are slowed.",
                        "Radius 4  |  Lasts 6s  |  50% slow  |  Costs 35 energy")
                }
            },
            new CharacterInfo
            {
                name = "Khorne Berserker", epithet = "The Frenzied Brawler", prefabPath = "Net/Player_KhorneBerserker",
                role = Role.Melee, accent = new Color(1f, 0.32f, 0.22f),
                blurb = "Frenzied melee brawler. Closes the gap fast and wins by staying on top of you.",
                footnote = "Hold Shift in game to use EX abilities",
                abilities = new[]
                {
                    A("LMB", "Chain-Axe Chops", "Hold to hack with the chain axes, left and right in turn. C during the wind-up cancels a chop. After a counter, the next chop hits twice as hard and reaches further.",
                        "Damage 9  |  Cooldown 0.75s  |  Range 2.4  |  100 degree arc  |  +8 energy per hit"),
                    A("RMB", "Axe Toss", "Hurl a spinning chain axe along your aim. The first enemy it hits takes damage and starts bleeding. Walls stop it. C before the throw leaves your hand cancels it.",
                        "Damage 12  |  Bleed 3/s for 5s  |  Cooldown 6s  |  Range 12  |  +8 energy per hit"),
                    A("Q", "Axe Rush", "Charge forward along your aim with both axes dragged back, then whip them through a wide frontal arc as the dash ends. C during the dash cancels the sweep.",
                        "Damage 18  |  Cooldown 10s  |  Dash 6  |  170 degree arc  |  +10 energy per hit"),
                    A("E", "Maim", "A low, crippling chop in front of you. Anything hit takes damage, is slowed and bleeds a trail as it goes. C during the wind-up cancels it.",
                        "Damage 8  |  Cooldown 7s  |  Range 2.6  |  30% slow for 2.5s  |  +8 energy per hit"),
                    A("Space", "Blood Rage", "Counter: the hit is negated and answered with a savage axe burst that damages the attacker and heals you. It also resets Axe Rush and empowers your next chop.",
                        "Damage 18  |  Heal 10% of max health  |  Cooldown 4s  |  Guard 1.2s  |  Burst radius 2.2"),
                    A("R", "Blood for the Blood God", "Costs a full energy bar. You roar and brace, rooted, while a lane telegraph builds, then charge a long way forward tearing a blood fissure behind you. Everything you pass through is damaged, stunned and bleeding. C during the wind-up cancels and refunds.",
                        "Damage 38  |  Stun 1.5s  |  Charge 14  |  Bleed 3/s for 5s  |  Costs 100 energy", true),
                    EX("Shift+Q", "Whirling Carnage", "Spin on the spot with both chain axes out, shredding everything around you. Damage ticks three times during the spin. C ends it early.",
                        "Damage 9 x 3 ticks  |  Range 3  |  Cooldown 5s  |  Costs 35 energy"),
                    EX("Shift+E", "Butcher's Nails", "Instead of the chop, you become Enraged: immune to crowd control, with 20% faster attacks and 10% faster movement.",
                        "Lasts 6s  |  +20% attack speed  |  +10% move speed  |  Costs 35 energy")
                }
            },
            new CharacterInfo
            {
                name = "Plague Surgeon", epithet = "The Contagion Caster", prefabPath = "Net/Player_PlagueSurgeon",
                role = Role.Support, accent = new Color(0.55f, 0.85f, 0.2f),
                blurb = "Support caster. Poison clouds and plague flies that hurt foes and mend allies. No counter, just wings.",
                footnote = "Hold Shift in game to use EX abilities",
                abilities = new[]
                {
                    A("LMB", "Plague Spit", "Hold to spit globs of plague along your aim. Each adds a stack of Festering to the target: a weak damage over time. Stacks last only 2.5 attack intervals, so the rot builds only while your hits keep landing.",
                        "Damage 5 + 1.5/s per stack  |  Cooldown 0.7s  |  Range 13  |  +6 energy per hit"),
                    A("RMB", "Pestilent Fly", "Loose a plague fly along your aim. It latches onto the first character it meets: an enemy is fed on and damaged, an ally is tended and healed. Walls stop it.",
                        "Enemy 6/s  |  Ally heal 7/s  |  Lasts 4s  |  Cooldown 6s  |  Range 13"),
                    A("E", "Restorative Cloud", "Vent a cloud at the cursor that lingers on the ground and heals every ally standing in it, you included. The poison is halved, so enemies still suffer. C during the wind-up cancels it.",
                        "Heal 10/s  |  Enemy damage 4.5/s  |  Cooldown 7s  |  Range 10  |  Radius 3  |  Lasts 5s"),
                    A("Space", "Winged Escape", "You have no counter. Wings tear out of your back: you become invulnerable and fly in a straight line to the cursor, then drop back to the ground. Walls stop the flight short; C ends it early.",
                        "Cooldown 8s  |  Range 9  |  Invulnerable while flying"),
                    A("R", "Ward of Plague", "Costs a full energy bar. Wrap the ally nearest your cursor (or yourself, if none) in a shield of plague-light. When its strength or time runs out it ruptures and infects every enemy near the holder with heavy Festering.",
                        "Shield 60  |  Lasts 8s  |  Range 14  |  Costs 100 energy", true),
                    EX("Shift+RMB", "Pestilent Burst", "The fly never attaches. It bursts the moment it touches anything or reaches maximum range, damaging and panicking every enemy around that spot and healing every ally.",
                        "Damage 28  |  Heal 28  |  Panic 1.25s  |  Radius 3.5  |  Costs 40 energy"),
                    EX("Shift+E", "Virulent Cloud", "A full-strength plague cloud that poisons every enemy standing in it, tick by tick.",
                        "Damage 9/s  |  Cooldown 7s  |  Radius 3  |  Lasts 5s  |  Costs 35 energy")
                }
            },
            new CharacterInfo
            {
                name = "Psyker", epithet = "The Warp Channeler", prefabPath = "Net/Player_Psyker",
                role = Role.Ranged, accent = new Color(0.7f, 0.5f, 1f),
                blurb = "Fragile warp caster. Fast bolts, chain lightning and crowd control, with a blink that leaves fire behind. No counter.",
                footnote = "Hold Shift in game to use EX abilities",
                abilities = new[]
                {
                    A("LMB", "Warp Bolt", "Hold to hurl crackling bolts of warp energy along your aim. Fast and accurate. Each hit builds energy.",
                        "Damage 9  |  Cooldown 0.55s  |  Range 14  |  +8 energy per hit"),
                    A("RMB", "Chain Lightning", "An instant bolt of white lightning strikes the first enemy along your aim, then leaps to the nearest other enemies, each jump weaker than the last.",
                        "Damage 16 (x0.7 per jump)  |  Cooldown 5s  |  Range 12  |  2 jumps  |  +6 energy per hit"),
                    A("Q", "Cripple", "Fire a slow, heavy bolt of crippling warp force at the first enemy in its path. It does a little damage and stuns the target. C during the wind-up cancels it.",
                        "Damage 5  |  Stun 1.5s  |  Cooldown 8s  |  Range 14  |  +12 energy per hit"),
                    A("E", "Kine Shield", "Wrap yourself in a telekinetic shield that soaks damage, area and over-time damage included. It fades if unused, and recasting replaces it. C drops it.",
                        "Absorbs 50  |  Lasts 5s  |  Cooldown 10s"),
                    A("Space", "Warp Step", "You have no counter. Step through the warp, vanishing and reappearing at the cursor (stopped short of walls). A pool of warp-fire is left where you land, burning enemies inside it.",
                        "Cooldown 6s  |  Range 8  |  Fire 12/s  |  Radius 2.2  |  Lasts 4s"),
                    A("R", "Psychic Blast", "Costs a full energy bar. After a brief gather you stand planted and channel a beam of white lightning that pierces everything in its path. The beam follows your cursor at a limited turn rate, so sweep it across enemies. Stuns, panic, silence and knockback interrupt it; C during the gather cancels and refunds.",
                        "Damage 7 per 0.25s  |  Channel 3s  |  Range 15  |  Costs 100 energy", true),
                    EX("Shift+Q", "Gravity Well", "Instead of the bolt, a singularity opens at the cursor and drags every enemy inside to its centre, then collapses in a burst that damages whoever is left near it.",
                        "Detonation 25  |  Range 12  |  Radius 4  |  Lasts 1.8s  |  Costs 35 energy"),
                    EX("Shift+E", "Psychic Scream", "Lost if you take the Force Stave. Instead of the shield, a wail of fear rolls out from you. Every enemy in range is gripped by panic and runs from you, unable to attack or use abilities. It does no damage.",
                        "Panic 2s  |  Radius 4.2  |  Costs 40 energy"),
                    EX("Shift+RMB", "Mind Leech", "Force Stave only. Instead of Kine Slam, tether yourself to the enemy nearest the cursor. It interrupts and silences them at once, then drains their health, healing you and feeding your energy. Breaks if the target dies or strays out of range, or if you are disrupted.",
                        "Drain 16/s  |  Heal 16/s  |  Silence 2s  |  Range 11  |  Lasts 2s  |  Costs 40 energy")
                }
            },
            new CharacterInfo
            {
                name = "Skitarii Ranger", epithet = "The Arquebus Marksman", prefabPath = "Net/Player_SkitariiRanger",
                role = Role.Ranged, accent = new Color(0.35f, 0.9f, 0.95f),
                blurb = "Long-range marksman. Piercing charged shots and a servo leap to keep distance, with a counter that answers in kind.",
                footnote = "Hold Shift in game to use EX abilities",
                abilities = new[]
                {
                    A("LMB", "Galvanic Shot", "Hold to fire fast galvanic rounds from the arquebus. Modest damage, long reach. Each hit builds energy.",
                        "Damage 14  |  Cooldown 0.75s  |  Range 22  |  +9 energy per hit"),
                    A("RMB", "Transuranic Shot", "Hold to charge the arquebus (you are planted while it builds), release to fire one huge piercing shot. The longer the charge, the harder it hits and the longer it stuns everything in the line. Stuns, silence, panic and knockback break the charge; C cancels it.",
                        "Damage 30-95  |  Stun 0.25-1.5s  |  Charge 1.6s  |  Cooldown 6s  |  Range 34  |  +25 energy per hit"),
                    A("Q", "Shrapnel Canister", "Lob a canister to the cursor. It bursts into a field of whirling metal that damages enemies over time and slows anyone standing in it. C during the wind-up cancels it.",
                        "Damage 14/s  |  35% slow  |  Cooldown 8s  |  Range 12  |  Radius 3.5  |  Lasts 4.5s  |  +10 energy per hit"),
                    A("E", "Disengage", "A quick servo-assisted leap straight away from the cursor, vaulting low obstacles and stopping short of walls. Gets you out of melee and back to sniping range.",
                        "Cooldown 7s  |  Distance 7"),
                    A("Space", "Counter", "Raise a guard. A hit it catches is negated and answered with a half-charged Transuranic shot fired straight back at the attacker, and Disengage is ready again.",
                        "Riposte shot at 50% charge  |  Cooldown 4s  |  Guard 1.2s"),
                    A("R", "Culling Volley", "Costs a full energy bar. After a short gather you plant yourself and fire four enormous shots at fixed intervals, each along your cursor's direction at that moment. Each pierces everything in its line, hits hard and slows. A laser sight shows where the next shot goes. Stuns, panic, silence and knockback interrupt it; C during the gather cancels and refunds.",
                        "Damage 55 x 4 shots  |  Range 60  |  20% slow for 2s  |  Costs 100 energy", true),
                    EX("Shift+Q", "Scattershot", "Instead of the canister, fire a short-range shotgun blast in a cone that hurts, knocks back and slows everything in it.",
                        "Damage 32  |  Range 7  |  Knockback 5  |  40% slow for 2.5s  |  Costs 35 energy"),
                    EX("Shift+RMB", "Overcharge Round", "Instead of charging, fire an instant Transuranic shot at 60% of a full charge, with no planted wind-up.",
                        "Costs 40 energy")
                }
            }
        };

        static AbilityInfo A(string key, string name, string description, string stats, bool ultimate = false)
            => new AbilityInfo { key = key, name = name, description = description, stats = stats, ultimate = ultimate };

        static AbilityInfo EX(string key, string name, string description, string stats)
            => new AbilityInfo { key = key, name = name, description = description, stats = stats, ex = true };

        const int PortraitSize = 256;
        const int StageSize = 1024;
        static readonly Vector3 StageOrigin = new Vector3(0f, -2000f, 0f);

        GameObject stage;
        Camera stageCamera;
        GameObject[] models;
        Vector3[] focus;      // where the full-body camera looks, per character
        float[] distance;     // how far back it sits
        public RenderTexture StageTexture { get; private set; }
        public RenderTexture[] Portraits { get; private set; }

        int shown = -1;
        float yaw = 20f;
        float spinVelocity;
        bool alive;

        public bool Alive => alive;

        // --- Lifecycle ---------------------------------------------------------------------------------------------

        public void Show()
        {
            if (alive) return;
            alive = true;

            foreach (var c in Roster)
                foreach (var a in c.abilities)
                    if (a.icon == null) a.icon = MakeIcon(a, c.accent);

            stage = new GameObject("CharacterSelectStage");
            stage.transform.position = StageOrigin;

            StageTexture = new RenderTexture(StageSize, StageSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            Portraits = new RenderTexture[Roster.Length];

            var camGo = new GameObject("StageCamera");
            camGo.transform.SetParent(stage.transform, false);
            stageCamera = camGo.AddComponent<Camera>();
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            stageCamera.fieldOfView = 28f;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = 60f;
            stageCamera.enabled = false; // rendered by hand
            stageCamera.allowHDR = false;

            CreateLight("Key", Quaternion.Euler(35f, 150f, 0f), new Color(1f, 0.93f, 0.82f), 1.25f);
            CreateLight("Rim", Quaternion.Euler(20f, -30f, 0f), new Color(0.5f, 0.6f, 1f), 0.7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            models = new GameObject[Roster.Length];
            focus = new Vector3[Roster.Length];
            distance = new float[Roster.Length];

            for (int i = 0; i < Roster.Length; i++)
            {
                var prefab = Resources.Load<GameObject>(Roster[i].prefabPath);
                Transform source = prefab != null ? prefab.transform.Find("Model") : null;
                if (source == null) continue;

                // Instantiating just the visual child leaves out every gameplay/network component of the prefab.
                var model = Object.Instantiate(source.gameObject, stage.transform);
                model.name = Roster[i].name;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                models[i] = model;

                Bounds b = Measure(model);
                focus[i] = new Vector3(0f, b.center.y - StageOrigin.y, 0f);
                float radius = Mathf.Max(b.extents.y, b.extents.x * 0.9f, 0.5f);
                distance[i] = radius * 1.15f / Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

                RenderPortrait(i, model);
                model.SetActive(false);
            }

            shown = -1;
        }

        public void Hide()
        {
            if (!alive) return;
            alive = false;
            if (stage != null) Object.Destroy(stage);
            if (StageTexture != null) { StageTexture.Release(); Object.Destroy(StageTexture); }
            if (Portraits != null)
                foreach (var p in Portraits)
                    if (p != null) { p.Release(); Object.Destroy(p); }
            StageTexture = null;
            Portraits = null;
        }

        static void CreateLight(string name, Quaternion rotation, Color color, float intensity)
        {
            var go = new GameObject(name + " Light");
            go.transform.rotation = rotation;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.transform.SetParent(GameObject.Find("CharacterSelectStage").transform, true);
        }

        static Bounds Measure(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(model.transform.position + Vector3.up, Vector3.one);
            bool first = true;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // --- Rendering ---------------------------------------------------------------------------------------------

        void RenderPortrait(int index, GameObject model)
        {
            var rt = new RenderTexture(PortraitSize, PortraitSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            Portraits[index] = rt;

            Transform head = FindDeep(model.transform, "Head");
            Vector3 headPos = head != null ? head.position : model.transform.position + Vector3.up * 1.7f;

            model.SetActive(true);
            var cam = stageCamera;
            float oldFov = cam.fieldOfView;
            cam.fieldOfView = 24f;
            cam.targetTexture = rt;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.18f, 1f);
            cam.transform.position = headPos + new Vector3(0f, 0.04f, 1.9f);
            cam.transform.LookAt(headPos + new Vector3(0f, 0.02f, 0f));
            cam.Render();
            cam.targetTexture = null;
            cam.fieldOfView = oldFov;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            model.SetActive(false);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // Call every frame from OnGUI (Repaint) with the champion to show. dragDelta spins the model.
        public void Render(int index, float dragDelta)
        {
            if (!alive || models == null || index < 0 || index >= models.Length || models[index] == null) return;

            if (index != shown)
            {
                if (shown >= 0 && models[shown] != null) models[shown].SetActive(false);
                models[index].SetActive(true);
                shown = index;
                yaw = 20f;
                spinVelocity = 0f;
            }

            if (Mathf.Abs(dragDelta) > 0.01f)
            {
                yaw -= dragDelta * 0.6f;
                spinVelocity = 0f;
            }
            else
            {
                // Settle back to a three-quarter view.
                yaw = Mathf.LerpAngle(yaw, 20f, 1f - Mathf.Exp(-1.5f * Time.unscaledDeltaTime));
            }

            models[index].transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 f = StageOrigin + focus[index];
            stageCamera.transform.position = f + new Vector3(0f, 0.15f, distance[index]);
            stageCamera.transform.LookAt(f);
            stageCamera.targetTexture = StageTexture;
            stageCamera.Render();
            stageCamera.targetTexture = null;
        }

        // --- Ability icons -----------------------------------------------------------------------------------------

        // No icon art exists yet, so each ability gets a round badge: dark core, accent-coloured glow, a ring (gold for
        // ultimates) and a glyph derived from its name. Swap in real art by assigning AbilityInfo.icon.
        static Texture2D MakeIcon(AbilityInfo ability, Color accent)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            Color ring = ability.ex ? new Color(0.75f, 0.5f, 1f) : ability.ultimate ? new Color(1f, 0.82f, 0.35f) : new Color(0.85f, 0.65f, 0.25f);
            int seed = 0;
            foreach (char ch in ability.name) seed = seed * 31 + ch;
            var rng = new System.Random(seed);
            float a1 = (float)rng.NextDouble() * Mathf.PI * 2f;
            int spokes = 3 + rng.Next(0, 5);

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = Color.clear;
                if (r <= 1f)
                {
                    float aa = Mathf.Clamp01((1f - r) * n * 0.5f);
                    if (r > 0.88f)
                    {
                        c = ring;
                    }
                    else if (r > 0.80f)
                    {
                        c = new Color(0.05f, 0.04f, 0.04f);
                    }
                    else
                    {
                        float glow = Mathf.Pow(1f - r / 0.8f, 1.6f);
                        Color core = Color.Lerp(new Color(0.07f, 0.05f, 0.06f), accent, glow * 0.75f);
                        // radial spokes give each badge its own silhouette
                        float ang = Mathf.Atan2(dy, dx) + a1;
                        float spoke = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * spokes * 0.5f)), 6f) * Mathf.Clamp01(r * 1.4f) * (1f - r) * 1.6f;
                        c = Color.Lerp(core, Color.Lerp(accent, Color.white, 0.55f), Mathf.Clamp01(spoke));
                        // bright centre mark
                        if (r < 0.14f) c = Color.Lerp(c, Color.white, (1f - r / 0.14f) * 0.8f);
                    }
                    c.a = aa;
                }
                px[y * n + x] = c;
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
