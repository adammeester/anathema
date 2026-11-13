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
            public bool ultimate, ex, passive;
            public Texture2D icon;
        }

        public class CharacterInfo
        {
            public string name, epithet, blurb, prefabPath;
            public Role role;
            public Color accent;
            public AbilityInfo[] abilities;
            public AbilityInfo passive;   // null when the champion has no passive
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
                passive = P("Rage", "Consecutive hits build Rage stacks, each speeding up your attacks. All stacks fall away at once if no hit lands within the window; at full Rage the window is longer. Replaced by the weapon's own passive when you take the Eviscerator or Heavy Chainaxe.",
                    "+6% attack speed per stack  |  Max 5 stacks  |  2.2s window (+0.8s at full Rage)"),
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
                    A("RMB", "Pestilent Fly", "Loose a plague fly along your aim. It latches onto the first character it meets: an enemy is fed on and damaged (its bite also adds a stack of Festering), an ally is tended and healed. Walls stop it.",
                        "Enemy 6/s  |  Ally heal 7/s  |  Lasts 4s  |  Cooldown 6s  |  Range 13"),
                    A("E", "Restorative Cloud", "Vent a cloud at the cursor that lingers on the ground and heals every ally standing in it, you included. The poison is halved, so enemies still suffer. C during the wind-up cancels it.",
                        "Heal 10/s  |  Enemy damage 4.5/s  |  Cooldown 7s  |  Range 10  |  Radius 3  |  Lasts 5s"),
                    A("Space", "Winged Escape", "You have no counter. Wings tear out of your back: you become invulnerable and fly in a straight line to the cursor, then drop back to the ground. You fly over walls; C ends it early.",
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
                passive = P("Charged", "Chain Lightning leaves everything it strikes Charged. Warp Bolt, Chain Lightning and Kine Slam all hit Charged enemies harder, and Gravity Well Charges whoever it catches.",
                    "Charged for 6s  |  +40% damage against Charged enemies"),
                abilities = new[]
                {
                    A("LMB", "Warp Bolt", "Hold to hurl crackling bolts of warp energy along your aim. Fast and accurate. Each hit builds energy. Deals 40% more damage to Charged enemies.",
                        "Damage 9 (12.6 vs Charged)  |  Cooldown 0.55s  |  Range 14  |  +8 energy per hit"),
                    A("RMB", "Chain Lightning", "An instant bolt of white lightning strikes the first enemy along your aim, then leaps to the nearest other enemies, each jump weaker than the last. Everything it strikes is left Charged for 6s, and enemies that are already Charged take 40% more damage.",
                        "Damage 16 (22.4 vs Charged, x0.7 per jump)  |  Cooldown 5s  |  Range 12  |  2 jumps  |  +6 energy per hit"),
                    A("Q", "Cripple", "Fire a slow, heavy bolt of crippling warp force at the first enemy in its path. It does a little damage and stuns the target. C during the wind-up cancels it.",
                        "Damage 5  |  Stun 1.5s  |  Cooldown 8s  |  Range 14  |  +12 energy per hit"),
                    A("E", "Kine Shield", "Wrap yourself in a telekinetic shield that soaks damage, area and over-time damage included. It fades if unused, and recasting replaces it. C drops it.",
                        "Absorbs 50  |  Lasts 5s  |  Cooldown 10s"),
                    A("Space", "Warp Step", "You have no counter. Step through the warp, vanishing and reappearing at the cursor (over walls, but never into one). A pool of warp-fire is left where you land, burning enemies inside it.",
                        "Cooldown 6s  |  Range 8  |  Fire 12/s  |  Radius 2.2  |  Lasts 4s"),
                    A("R", "Psychic Blast", "Costs a full energy bar. After a brief gather you stand planted and channel a beam of white lightning that pierces everything in its path. The beam follows your cursor at a limited turn rate, so sweep it across enemies. Stuns, panic, silence and knockback interrupt it; C during the gather cancels and refunds.",
                        "Damage 7 per 0.25s  |  Channel 3s  |  Range 15  |  Costs 100 energy", true),
                    EX("Shift+Q", "Gravity Well", "Instead of the bolt, a singularity opens at the cursor and drags every enemy inside to its centre, then collapses in a burst that damages and Charges whoever is left near it.",
                        "Detonation 25  |  Range 12  |  Radius 4  |  Lasts 1.8s  |  Costs 35 energy"),
                    EX("Shift+E", "Psychic Scream", "Lost if you take the Force Stave. Instead of the shield, a wail of fear rolls out from you. Every enemy in range is gripped by panic and runs from you, unable to attack or use abilities. It does no damage.",
                        "Panic 2s  |  Radius 4.2  |  Costs 40 energy")
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
            },
            new CharacterInfo
            {
                name = "Hospitaller", epithet = "The Sister of Mercy", prefabPath = "Net/Player_Hospitaller",
                role = Role.Support, accent = new Color(1f, 0.85f, 0.45f),
                blurb = "Battle-sister healer. Tethers foes to burn them or allies to share their pain, and blesses the ground to mend.",
                footnote = "Hold Shift in game to use EX abilities",
                passive = P("Blessed Suffering", "Damage you take for an ally (through your tether or Martyrdom) gives you energy and builds Faith. Every stack of Faith makes all your damage stronger. Go a few seconds without sharing any damage and the stacks fade one by one. Your Faith shows above your action bar.",
                    "1 energy per 2 damage shared  |  1 Faith per 15 damage shared, max 5  |  +6% damage per stack  |  Fades after 4s"),
                abilities = new[]
                {
                    A("LMB", "Sanctified Tether", "Click to cast a beam of light that latches onto the first character along your aim and stays on by itself while you move and cast. An enemy is burned every quarter second. An ally has part of every hit they take dealt to you instead. Ends when its time runs out, if the target gets out of range, or if you press C. A click that hits nothing costs no cooldown.",
                        "Lasts 4s  |  Cooldown 6s  |  Enemy 1.5 per 0.25s  |  Ally: you take 35% of their damage  |  Range 11  |  Leash 14"),
                    A("RMB", "Sanctus Pulse", "Bless the ground at the cursor. After a short delay it pulses once, and its healing is split evenly between every ally inside, you included. One ally alone receives it all. Holds three charges that recharge separately, so you can place them back to back.",
                        "Heal 26 shared  |  Delay 0.5s  |  Radius 2.2  |  Range 10  |  3 charges, 6s recharge each"),
                    A("Q", "Blinding Radiance", "Thrust the icon forward: it flares, burning every enemy in a short frontal cone and hurling them back. C during the wind-up cancels it.",
                        "Damage 10  |  Knockback 3.5  |  Range 4  |  110 degree arc  |  Cooldown 8s"),
                    A("E", "Dove Strike", "Your circling doves dive at your enemies, damaging and rooting them. If your tether holds enemies, one dove homes in on each of them and cannot miss. Otherwise it is a skillshot: one dove flies straight along your aim and strikes the first enemy in its path. Three charges, one per dove; the doves around your head show how many are left. A target rooted by a dove can't be rooted by another for a few seconds, but later dives still hit.",
                        "Damage 8  |  Root 0.75s (once per 3s per target)  |  Range 12  |  3 charges, 8s recharge each  |  +6 energy per hit"),
                    A("Space", "Martyr's Grace", "Counter: the hit is negated. You and every ally near you are healed, and the attacker is struck and silenced.",
                        "Heal 15  |  Radius 6  |  Damage 6  |  Silence 1.2s  |  Cooldown 4s  |  Guard 1.2s"),
                    A("R", "Martyrdom", "Costs a full energy bar. Holy fire gathers around you (rooted), then for 5s every ally near you is bound to you by a burning thread: half of every hit they take is dealt to you instead, and you take 40% less damage. Shared damage can't kill you. When it ends, the pain you bore bursts out as a ring of holy fire that burns and pushes back nearby enemies; the more you absorbed, the harder it hits. Stuns, silence, panic and knockback during the gathering interrupt it; C cancels.",
                        "Lasts 5s  |  Bind radius 10  |  Share 50%  |  -40% damage taken  |  Burst 10 + 80% of absorbed (max 80), radius 6  |  Costs 100 energy", true),
                    EX("Shift+LMB", "Forked Tether", "A second beam from you, so you hold two characters at once, enemy or ally, each burning a little less. Already holding one? It latches onto the next character along your aim and ends with the first. Holding none? One cast grabs the character along your aim and the one nearest your cursor beside them. Has its own cooldown, shown while you hold Shift. Dove Strike then sends a dove to each tethered enemy.",
                        "2 targets max  |  70% burn per target  |  Own cooldown 8s  |  Costs 30 energy"),
                    EX("Shift+E", "Golden Dove", "Your doves turn gold: they hit a little harder and panic their targets instead of rooting them. With two enemies tethered, both are sent running.",
                        "Damage 10  |  Panic 1.25s  |  Uses a dove charge per dove  |  Costs 35 energy")
                }
            },
            new CharacterInfo
            {
                name = "Mandrake", epithet = "The Shadow Assassin", prefabPath = "Net/Player_Mandrake",
                role = Role.Melee, accent = new Color(0.45f, 1f, 0.3f),
                blurb = "Drukhari stealth assassin. Vanishes into the shadows, steps between them and strikes with poisoned glimmersteel.",
                footnote = "Hold Shift in game to use EX abilities",
                passive = P("Soul Harvest", "Every poison tick or blade hit that lands on an enemy below 40% health takes time off Shadow Veil's cooldown, so you can vanish again to finish them. Your blades also hit poisoned enemies harder. Poison stacks deal damage over time; at 6 stacks the poison bursts, dealing all its remaining damage at once.",
                    "-1s Shadow Veil cooldown per hit or tick on enemies below 40%  |  Up to 3s per target  |  +15% damage to poisoned enemies  |  Poison bursts at 6 stacks"),
                abilities = new[]
                {
                    A("LMB", "Glimmersteel Blade", "Click or hold to cut in a three-strike chain: a forehand, a backhand, then a wide spinning cleave that hits harder, reaches further and poisons everything it cuts. The first two strikes come quickly; after the cleave there is a longer pause before the chain starts again. Each strike is its own attack, and a click made just before the next strike is ready is remembered. A strike thrown from Shadow Veil also briefly stuns.",
                        "Damage 8 / 8 / 16 cleave  |  0.5s between strikes, 1.1s after the cleave  |  Cleave 200 degrees, range 3.2  |  +1 poison stack  |  Stun 0.5s from stealth  |  +7 energy per hit"),
                    A("RMB", "Balefire", "Hurl a bolt of green witch-fire along your aim. The first enemy it strikes is set alight and burns over time. Land any hit of your own on them while it burns and the balefire bursts, dealing all of its remaining burn damage at once and cutting the healing they receive for a few seconds. C during the wind-up cancels it.",
                        "Damage 6  |  Burn 5/s for 6s (30 total)  |  Range 13  |  Bursts on your next hit  |  Burst: -30% healing received for 4s  |  Cooldown 8s"),
                    A("Q", "Shadow Jump", "Step into one shadow and out of another at the cursor, passing over walls. Arriving charges your blade: your next strike within a few seconds deals bonus damage. Doesn't break Shadow Veil.",
                        "Range 7  |  +10 damage on the next strike within 3s  |  Cooldown 9s"),
                    A("E", "Shadow Veil", "Melt into the shadows. Enemies can't see you at all (you and your allies see a translucent blue ghost), abilities that pick their own targets ignore you, and you move faster. Any attack breaks the veil; Shadow Jump doesn't. Enemy attacks still hit you if they happen to connect.",
                        "Lasts 5s  |  +25% move speed  |  Cooldown 12s"),
                    A("Space", "Umbral Riposte", "Counter: raise a brief guard. If an enemy hits you during it, the hit is negated, you vanish into stealth and step out of the attacker's shadow, right behind them. Your next strike within 2s counts as a strike from stealth, even once you reappear: Shadow Rend gets its bonus damage and poison. A guard that catches nothing comes back quickly.",
                        "Guard 0.6s  |  Vanish 1s  |  Step behind within 8  |  Stealth strike within 2s  |  Cooldown 6s, 12s after a riposte"),
                    A("R", "Shadow Cross", "Costs a full energy bar. A ghostly X is marked at the cursor; then you blink to one end of the first diagonal and tear along it in one huge slash, blink to the other diagonal and tear across it, carving an X. Everyone along either line is cut and poisoned; anyone where the lines cross is hit twice, and if they were already poisoned the stacks reach 6 and burst. You are invulnerable while slashing. Stuns, silence, panic and knockback during the wind-up interrupt it; C cancels and refunds.",
                        "Damage 25 per slash  |  +2 poison stacks per slash (2/s)  |  Each slash 9 long, 1.4 either side  |  Range 9  |  Costs 100 energy", true),
                    EX("Shift+E", "Shadow Rend", "Instead of the veil, a short, vicious cut in front of you that stuns everything it hits. As a strike from stealth (out of an Umbral Riposte) it also deals bonus damage and adds two stacks of poison. C during the wind-up cancels it.",
                        "Damage 14 (28 from stealth)  |  Stun 1.25s  |  +2 poison stacks from stealth  |  Range 3  |  Cooldown 14s  |  Costs 35 energy"),
                    EX("Shift+Q", "Umbral Return", "Shadow Jump as normal, but you leave a shadow mark where you stood. Press Q again within 3s to snap back to it, as long as it is still within jump range. The return doesn't break Shadow Veil or charge your blade again. C lets the mark fade.",
                        "Return window 3s  |  Range 7  |  Costs 35 energy")
                }
            }
        };

        static AbilityInfo A(string key, string name, string description, string stats, bool ultimate = false)
            => new AbilityInfo { key = key, name = name, description = description, stats = stats, ultimate = ultimate };

        static AbilityInfo P(string name, string description, string stats)
            => new AbilityInfo { key = "Passive", name = name, description = description, stats = stats, passive = true };

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
        bool alive;

        public bool Alive => alive;

        // --- Lifecycle ---------------------------------------------------------------------------------------------

        public void Show()
        {
            if (alive) return;
            alive = true;

            foreach (var c in Roster)
            {
                foreach (var a in c.abilities)
                    if (a.icon == null) a.icon = MakeIcon(a, c.accent);
                if (c.passive != null && c.passive.icon == null) c.passive.icon = MakeIcon(c.passive, c.accent);
            }

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

                HideExtraGear(model);

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

        // The Berserker mesh carries every weapon, each on its own bone; in-game BerserkerWeaponVisual hides the ones
        // not wielded. Here only the starting dual chainaxes should show.
        // Likewise the Grey Knight mesh carries every order's gear and every weapon; show only the starting kit
        // (halberd + bolter), not the Purifier/Paladin helms and pauldrons or the unlocked weapons.
        public static void HideExtraGear(GameObject model)
        {
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                switch (t.name)
                {
                    case "Heavy_Chainaxe": case "Eviscerator":
                    case "Helm_White": case "Pauldron_White_L": case "Pauldron_White_R":
                    case "Helm_Gold": case "Pauldron_Gold_L": case "Pauldron_Gold_R":
                    case "Daemonhammer": case "Psycannon":
                        t.localScale = Vector3.zero;
                        break;
                }
        }

        void RenderPortrait(int index, GameObject model)
        {
            var rt = new RenderTexture(PortraitSize, PortraitSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            Portraits[index] = rt;
            RenderHeadShot(stageCamera, model, rt);
        }

        // Frames the model's head and renders it into rt with the given (disabled, hand-rendered) camera.
        public static void RenderHeadShot(Camera cam, GameObject model, RenderTexture rt)
        {
            Transform head = FindDeep(model.transform, "Head");
            Vector3 headPos = head != null ? head.position : model.transform.position + Vector3.up * 1.7f;

            model.SetActive(true);
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
            }

            if (Mathf.Abs(dragDelta) > 0.01f)
            {
                yaw -= dragDelta * 0.6f;
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
