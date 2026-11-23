# Anathema

A Battlerite-style arena brawler set in the Warhammer 40k universe. Unity 6 (6000.4.2f1).


## Getting started

1. Open this folder in Unity Hub (Add > Add project from disk) with 6000.4.2f1.
2. On first load `Assets/Editor/ArenaSceneBuilder.cs` generates `Assets/Scenes/Arena.unity`
   (arena, player, camera, four training dummies). Rebuild any time via **Anathema > Rebuild Arena Scene**.
3. Open the scene and press Play.

## Controls

| Input | Action |
| --- | --- |
| WASD | Move (screen-relative, independent of facing) |
| Mouse | Aim; character always faces the cursor |
| Hold LMB | Basic attack |

## Layout

- `Scripts/Player` - `PlayerMovement`, `ProceduralWalk` (walk cycle driven off the model's bones), `MouseAim`, `BasicAttack`, `AimIndicator`, `PlayerHud`
- `Scripts/Combat` - `Health` (with team id), `Projectile`, `HealthBar`, `Respawner`
- `Scripts/Camera` - `TopDownCamera` (fixed pitch/yaw/distance, locked to the target)
- `Editor` - scene builder

## Tuning

Everything is serialized on the components: camera pitch/distance/FOV on `TopDownCamera`, speed on
`PlayerMovement`, damage/interval/range/projectile speed on `BasicAttack` (switch `Kind` to `Melee` for a cone swing).

Uses the legacy Input Manager (`Input.GetAxis`), so keep Active Input Handling on "Input Manager (Old)" or "Both".

## Characters

| Character | Menu item | Kit |
| --- | --- | --- |
| Grey Knight Interceptor | **Anathema > Rebuild Arena Scene** | LMB storm bolter, Q halberd sweep, RMB Smite (piercing psychic lance after a short telegraphed focus), E Consecrate (inscribe a warding sigil at the cursor: a ground telegraph fills for 0.6s, then it detonates, dealing a little damage and rooting enemies inside for 1.5s; C cancels and refunds), Space counter (riposte), R Purgatus ultimate (costs a full energy bar: 1s channel, then an expanding warp-fire ring that damages, knocks back and silences) |
| Khorne Berserker | **Anathema > Rebuild Arena Scene (Khorne Berserker)** | LMB alternating chain-axe chops, Shift whirling carnage (360 degree spin, 3 damage ticks), Q axe rush (dash forward, then a dual-axe sweep as it ends), E maim (low chop: 30% slow for 2.5s and a blood trail), Space counter (blood rage: damages the attacker, heals, resets Q and empowers the next chop), R Blood for the Blood God ultimate (costs a full energy bar: 0.75s rooted wind-up with a lane telegraph (C cancels and refunds), then a long charge that tears a blood fissure, damaging and stunning everything it passes through for 1.5s) |
| Psyker | **Anathema > Rebuild Arena Scene (Psyker)** | Human-sized glass-cannon caster (~1.75 m vs the marines' ~2.5 m+, 100 HP, smaller hitbox). LMB warp bolt, RMB chain lightning (leaps between up to 3 enemies), Q cripple (a bolt that stuns for 1.5s; EX gravity well drags enemies to a point, then detonates for 25 damage), E psychic barrier (a personal shield that absorbs 50 damage or fades after 5s; EX mind leech: a 2s tether that interrupts and silences the nearest enemy and drains health and energy into her), Space warp step (blink toward the cursor, stops short of walls, leaves a burning fire pool where she lands), R psychic blast ultimate (costs a full energy bar: 0.4s gather, then a 3s channelled white lightning beam that pierces enemies and follows the cursor at a limited turn rate; planted while channelling; stuns, panic, silence and knockback interrupt it; C during the gather refunds). Model: `blender -b --python Art/psyker.py` |
| Skitarii Ranger | **Anathema > Rebuild Arena Scene (Skitarii Ranger)** | Long-range sniper (~1.9 m, 105 HP). LMB galvanic shot (fast round, hits feed energy), RMB transuranic shot (hold to charge up to 1.6s, release for a piercing line shot: 30-95 damage and a 0.25-1.5s stun on everything hit; you are planted while charging), Q shrapnel field (a canister lobbed to the cursor that bursts into a 4.5s field: damage over time and a 35% slow while standing in it; EX scattershot, a short-range shotgun cone that damages, knocks back and slows), E disengage (a leap straight away from the cursor, stops short of walls), Space counter (the hit is negated and answered with an instant half-charged transuranic shot at the attacker, and disengage is reset), R culling volley (costs a full energy bar: 0.6s gather with a laser sight, then 4 huge piercing shots 0.95s apart along the cursor, 55 damage each and a slight slow; planted while firing; stuns, panic, silence and knockback interrupt it). New `Slowed` status (no blood trail). Model: `blender -b --python Art/skitarii_ranger.py` |
| Hospitaller | **Anathema > Rebuild Arena Scene (Hospitaller)** | Battle-sister healer (~1.85 m, 160 HP) carrying an open book, with three doves circling her head. LMB sanctified tether (click: a beam latches onto the first character along the aim and stays on by itself for 4s, 6s cooldown; on an enemy it burns every 0.25s, on an ally 35% of every hit they take is dealt to her instead; ends early past 14 m, on death or with C; a miss is refunded; EX Forked Tether, its own 8s cooldown shown while Shift is held: a second beam from her (two characters at most, 70% burn each); while she holds one it latches onto the next character along the aim and ends with the first, and with none held one cast grabs the character along the aim plus the one nearest the cursor within 5 m of them), RMB sanctus pulse (a blessing placed at the cursor pulses after 0.5s and splits 26 healing evenly between every ally inside a 2.2 m radius; 3 charges, each recharging separately over 6s), Q blinding radiance (frontal cone flare: damage and knockback), E dove strike (one dove homes in on each enemy the tether holds and cannot miss; with nothing tethered it is a skillshot straight along the aim that hits the first enemy; 8 damage and a 0.75s root, rooting the same target at most once per 3s; 3 charges, 8s recharge each, shown by how many doves are circling; EX Golden Dove: 10 damage and a 1.25s panic instead of the root, one per tethered enemy), Space Martyr's Grace counter (heals her and nearby allies, silences the attacker), R martyrdom (costs a full energy bar: 0.9s rooted gathering with holy fire streaming in and climbing her body, then for 5s half of every hit taken by allies who were within 10 m is dealt to her instead, she takes 40% less damage and shared damage cannot kill her; when it ends the absorbed pain bursts out as an expanding ring of holy fire: 10 + 80% of what she absorbed (max 80) and a push to enemies within 6 m). Passive Blessed Suffering: damage she takes for an ally (tether or Martyrdom) gives energy (1 per 2 damage) and builds Faith, 1 stack per 15 damage up to 5, each +6% damage; after 4s without sharing, stacks fade one per second. Faith shows as pips above her action bar (`IPassiveStacks`, drawn by `PlayerHud`). Her machine sends the tether's chosen targets to everyone (`NetPlayer.SendTargets`), so all machines link the same characters. `Health.AddDamageSharer` carries the tether's and Martyrdom's damage shares; `PsychicFireRing` takes a colour palette (her fire is gold-orange). Model: `blender -b --python Art/hospitaller.py` |

| Mandrake | **Anathema > Rebuild Arena Scene (Mandrake)** | Drukhari stealth assassin (~1.9 m, 155 HP). LMB glimmersteel blade (click or hold; a three-strike chain: forehand, backhand, then a 200 degree spinning cleave for double damage that adds a poison stack; 0.5s between the first strikes, 1.1s after the cleave; a click just before the next strike is ready is buffered; from stealth a strike also stuns for 0.5s), RMB balefire (after a 0.2s wind-up a 13m skillshot bolt of green witch-fire that deals 6 damage and sets the first enemy hit alight with a server-held `Balefire` status (5/s for 6s, `Hit.WithBalefire`); the next hit the Mandrake lands on them bursts it for all its remaining burn damage at once and cuts their healing received by 30% for 4s (`Health.CutHealing`); 8s cooldown), Q shadow jump (teleport up to 7m over walls; the next strike within 3s deals +10 damage; doesn't break stealth), E shadow veil (5s stealth: fully invisible to enemies, including bars, effects and the ENEMY tag, and skipped by auto-targeting abilities; a translucent blue ghost to you and allies; +25% move speed; any attack breaks it; EX shadow rend, 35 energy: after a 0.15s wind-up a short 80 degree cut (range 3, 14 damage) that stuns everything it hits for 1.25s; as a strike from stealth it deals +14 damage and adds 2 poison stacks), Space umbral riposte (his counter; a `Counter` subclass with a 0.6s guard: a negated hit gives 1s of stealth, a step to just behind the attacker if within 8m, and makes his next strike within 2s count as from stealth; 6s cooldown, 12s after a successful riposte). R shadow cross ultimate (costs a full energy bar: an X telegraphed at the cursor, then two invulnerable blink-dash slashes along its diagonals, 25 damage and 2 poison stacks (2/s) each; the crossing point is hit twice and detonates if the target was already poisoned). Passive Soul Harvest: each poison tick or glimmersteel blade hit that lands on an enemy below 40% health takes 1s off Shadow Veil's cooldown, up to 3s per target (the allowance starts over once that target is back above 40%); poison ticks run on the server, which tells the Mandrake's owner (`Poisoned.Ticked`, `NetPlayer.ServerSoulHarvest`). His attacks deal +10% to poisoned enemies. New `Poisoned` status (stacking damage over time, at 6 stacks it bursts for all its remaining damage; `Hit.WithPoison`) and `Stealthed` status (owner-held, published in `NetPlayer`'s status mask; hides every renderer on enemy screens). Model: `blender -b --python Art/mandrake.py` |
| Bullgryn | **Anathema > Rebuild Arena Scene (Bullgryn)** | Ogryn tank (~2.9 m model, 240 HP, the most on the roster; 4.8 move speed). LMB slab maul (click or hold; a 110 degree, 2.6m swing for 13 every 1s; every third connecting swing in a row shoves 1.5m), or with the round-1 Armament upgrade the Grenadier Gauntlet (a grenade lobbed to the cursor, clamped to 4-11m; keeping the maul is the round-1 Haymaker card (a passive charge on a 10s cooldown: while ready the maul glows green for everyone, and the next maul swing that connects deals 50% more and knocks 3m, stunning anyone knocked into terrain for 0.75s; a whiff keeps the charge, a Slab Slam doesn't use it; published as `FxPrimed` / `Ability.ShowsPrimed`), and maul-only upgrades follow from round 2: Big Swings (150 degrees, 3.1m, shove every second connecting swing) and Ground Pounder (Slab Slam hits everyone within 3.6m around him, 1s stun); owning any of them removes the gauntlet from offers. The gauntlet: 0.6s flight with a filling ring at the landing spot, 11 area damage in 2.2m every 1.1s; arcs over cover). RMB shield bash (0.15s wind-up, 90 degrees, 3m: 8 damage, interrupts, 2m knockback, 30% slow for 2s; 7s cooldown; unusable while Living Wall stands). Q bull charge (0.25s planted wind-up with a lane telegraph, then 7m in 0.35s, immune to crowd control (`IUnstoppable`); the first enemy met takes 12 and is carried along; into a wall (his Living Wall counts) they are stunned 1.25s on impact, otherwise 0.4s when dropped; 9s cooldown). E chain haul (0.3s wind-up, a 9m hook; an enemy takes 6 and is dragged to 1.5m in front of him, an ally is dragged to him through the supportive `AllyHaul` status; 12s cooldown). Space brace (no counter: hold to raise the slabshield; he drops into a crouch behind the shield and a translucent arc shows the area it covers; a 140 degree frontal arc, centred 0.5m in front of him, negates direct hits and projectiles and their riders on the server's copy (`Health.DamageModifier`), area damage gets through; a solid plate in front of him stops enemy projectiles aimed at allies behind; 45% move speed (`IMoveSpeedModifier`), 240 degrees/s turning, no attacking; 100 stamina, -1 per damage blocked (holding costs nothing), refills 20/s after 1s down; at 0 the guard breaks: the shield drops and he can't brace for 3s; a yellow `ShieldBar` above his health bar shows the stamina on every screen (red while locked out); blocked damage gives 1 energy per 3; EX ration break, 50 energy per use: instant, heals 20% of max health; 2 charges, 15s recharge each (pips on the slot while Shift is held, `Ability.ExChargeCapacity`)). R living wall ultimate (costs a full energy bar: a 0.4s planted wind-up, then he plants his slabshield 2m ahead (`Assets/Models/BullgrynShield.fbx`, exported by the same script) inside a 4m-wide arc of force bowed toward the aim (a larger copy of Brace's translucent `BlockArc`), for 6s, blocking movement and projectiles both ways; his held shield is hidden while it stands; press R or right-click again from anywhere to shove it: after a 0.25s warning it launches 3m the way it was facing in 0.16s, enemies in its path take 10 and are pushed along, and anyone pinned against terrain is stunned 1.5s; no Brace or Shield Bash while it stands). Passive Grit: 1 stack per 15 damage blocked, max 5; each stack is 4% armour and 8% shorter crowd control; at 5 stacks the next LMB is empowered and spends them (maul: Slab Slam, +60% damage, 0.75s stun; gauntlet: Krak Shell, 3.2m and a 40% slow for 1.5s); after 4s without blocking stacks fade one per 1.5s. Every copy counts the same blocks (`NetPlayer.ServerShieldBlocked`). Model: `blender -b --python Art/bullgryn.py` |
| Thousand Sons Sorcerer | **Anathema > Rebuild Arena Scene (Thousand Sons Sorcerer)** | Time-and-sand support (~2.9 m with the crest, 165 HP, no counter; seven slots, Battlerite style: R is a basic ability and the ultimate is on F). LMB Sands of Prospero (0.3s cast, 10 damage; every hit lays the **Curse of the Rubricae**, a server-held time bomb (`Combat/RubricMark.cs`) that bursts for 10 after 3.5s and is set off early by Bolt of Change, Sinking Sands and Dust Form; EX Bolt of Change, 25 energy, its own 5s cooldown (`Ability.ExReady`): a faster, longer bolt, 18 damage, knockback, bursts the curse). RMB Mending Sands (dust thrown on a 0.35s arc to the cursor, up to 11m: every teammate within 2.2m, himself included, is healed for 12; 3 charges, 6s each; the Rejuvenate upgrade also leaves Mending (`Combat/Mending.cs`), +6 after 3.5s). Space Dust Form (pours to the cursor as sand in 0.5-0.65s, through walls, invulnerable on the server's copy; heals allies he passes for 12 and blooms their Mending, bursts enemies' curses). Q Kine Shield (1.2s, a 180 degree frontal arc: direct hits from the front are negated on the server (`Health.DamageModifier`), melee attackers within 3.6m are knocked back, and enemy projectiles entering it are caught on every machine (`Projectile.Catchers`, `IProjectileCatcher`); the first is held, and Q again within 4.5s throws it back as his own bolt; EX Warp Rift, 25 energy: a `ReflectDome` at the cursor, 2.6m, 2s, 40% slow). E Sinking Sands (an hourglass lobbed 2-10m on a slow 0.95s arc; 18 area damage in 2.3m, a fading snare from a full stop over 1s (`Slowed.Apply(..., fade: true)`), bursts the curse). R Dustglass Touch (140 degree, 3.2m sweep: 4 damage, knockback, and 1s later the target is Petrified (`Combat/Petrified.cs`) for 2.7s: counts as a stun but sits behind a 20-point shell (`Health.StatusAbsorber`) that frees them when it breaks; 15s). F Weaver of Fates ultimate (full energy bar: the teammate nearest the cursor within 18m, or himself, is Timeslipped (`Combat/Timeslipped.cs`) for 2s: hidden, invulnerable, cooldowns cut 25%, then they reappear at their own cursor within 13m of where they vanished (over walls) and stun enemies within 2.5m for 1.75s). Passive Echoes of Prospero (`Abilities/Sorcerer/EchoesOfProspero.cs`): whenever one of his curses bursts, the ally nearest the victim within 6m (himself included) is healed for 4; resolved where the curse lives (`RubricMark.Burst`), and every machine draws the sand stream (`NetPlayer.ServerRubricEcho`). The Psyker's E is now called Psychic Barrier, so Kine Shield is his alone. Model: `blender -b --python Art/sorcerer.py` |
| Sanguinary Guard | **Anathema > Rebuild Arena Scene (Sanguinary Guard)** | Blood Angels melee battle-medic (marine-sized, 170 HP, 5.2 move speed; upgrades in `GuardUpgrade`, see the round-flow section; Sanguinary Guard look: gold armour, white wings, death-mask helm with a sunburst halo). LMB Power Sword (alternating cuts, 0.7s, 100 degrees, 2.6m, 9 damage; no lifesteal on plain cuts). RMB Blood Transfusion (0.12s, then a blood bolt along his aim, 12m at 20m/s: he pays 12 of his own health as he throws it (`Health.Sacrifice`, applied on the server through `NetPlayer.SendSacrifice`; never below 1, and what he pays stays healable so the Arc's lifesteal can win it back) and the first teammate it touches, never himself, heals 22; an enemy in the way soaks it up for no damage, and a miss is wasted; not castable below 15% health after paying; 4s). Q Jump Pack (rooted 0.55s spool-up while the twin engines on his pack light (`JetPackFx`: blue flames from the `Jet_L` / `Jet_R` nozzle bones, a blue light, smoke trails); C or crowd control during it cancels and refunds; then a 0.7s leap up to 12m to the cursor, 4m high, over walls and cover: a fast climb, a short hang, then a slam; enemies within 2.8m of the impact take 12 area damage and a 40% slow for 1.5s (nothing for allies); jagged blood-seamed cracks (`ImpactCracks`) open on the ground there and fade over 3s; 10s). E Chalice Offering (0.3s; he pays 15 health and teammates within 4m, not himself, heal 25, +50% per enemy within 4m up to +100%; 2 charges, 12s recharge each, 1s between casts; EX Chalice of Wrath, 35 energy and a charge, no health cost: every enemy within 4m takes 6 (empty chalice) to 18 (full Vitae), scaling with Vitae without spending it, and each landed hit heals him 80% of it (never fills Vitae); castable with nobody in reach, and a whiff still spends the charge and energy; a dark ground ring marks its reach during the 0.3s wind-up and a `BloodSplatter` bursts across the ground on landing). Space Sanguine Parry (counter: riposte 8 within 3.5m, heals the most injured teammate within 8m for 15, brings his next Chalice charge 50% closer). R Crimson Rush (0.2s planted wind-up with a lane telegraph, then a 6m jump-pack dash; the first enemy reached stops it and takes 14 and a 1.2s stun, nobody else is hit; 13s; the stun is the window to land the Arc on them; EX Blade Hurl, 50 energy: a 0.25s wind-up, then he hurls his sword along his aim (skillshot, 10m at 24m/s, 0.5m radius): the first enemy hit takes 10 and a 1.5s stun, then it flies back to his hand over 0.6s; until it is back he is disarmed (no Power Sword, Arc or Parry riposte; heals, Jump Pack, Chalice and the counter still work), the sword bone is hidden and every machine flies its own copy). F Wings of the Angel ultimate (full energy bar: 0.6s rooted gather while his own wings spread and blaze gold (`Wing_L` / `Wing_R` bones and "wing" / "wing_tip" materials swapped for glowing gold, a warm light and drifting golden motes; they beat in flight and slowly while ascended), a leap up to 9m, then a 6m ring heals teammates 30 and deals 20 with a knockback; for 5s every Power Sword swing is an Exsanguinating Arc, every 0.9s, without spending the chalice). Passive Vitae (`Abilities/SanguinaryGuard/Vitae.cs`): healing he gives (teammates or himself; overheal counts for nothing, seen through `Healing.Applying` on his own machine) fills a 0-100 chalice, x1.5 while an enemy is within 3m; health he pays for his abilities fills it too, 1 per point (`SanguinaryKit.Pay`); damage never fills it; drains 6/s after 3s without healing unless full. Full, his next LMB is the **Exsanguinating Arc**: 0.25s wind-up with a filling ground ring, then a 1.25-turn spin, 360 degrees, 3.3m, 28 damage and a 30% slow for 1s; every landed hit heals him for 80% of it (lifesteal, how he wins back the health he spends) and each other teammate within 6m for 40%, and during the ultimate each landed Arc brings his next Chalice charge 25% closer (that healing never refills the chalice); while full he takes 10% more damage (`Health.Expose` on the server's copy, from the published "empowered" flag). The blade glows in stages as Vitae fills (`BloodBladeEffect`: a sheen at a third, glow, light and dripping blood at two thirds, arcs, blood mist and a halo glyph when full; other players only see the full stage). `TopDownCamera.Shake` gives the Arc and the ultimate's crash a short shake on his own screen. Passive Red Thirst (`RedThirst.cs`): at or below 20% health he moves 20% faster, swings 30% faster (Power Sword and Arcs, through the new `IAttackSpeedModifier` read by `Enraged.AttackSpeedMultiplier`) and the Jump Pack slam deals 50% more; damage from Power Sword cuts and Crimson Rush fills Vitae 1 per point (`SanguinaryKit.FeedThirst`; not the Arc, slam, riposte or Chalice of Wrath), the one exception to "damage never fills it"; lifesteal unchanged; derived from replicated health, so every machine agrees; his eyes, blade and wing tips flare red with streaks, a red heartbeat light pulses on his chest and dark red vapour rises off him. Model: `blender -b --python Art/sanguinary_guard.py` |

Models are generated in Blender: `blender -b --python Art/khorne_berserker.py` writes `Assets/Models/KhorneBerserker.fbx`
(add `-- --render DIR` for preview PNGs). Abilities live in `Scripts/Abilities/<Character>/`.

## Between-round upgrades (Khorne Berserker first)

After every round except the last the match goes through the **prep phase** (Battlerite style):

1. **Report** (5s, frozen): the round result and damage report.
2. **Prep**: everyone is put back at their spawn at full health and may walk around inside a glowing barrier ring (`NetGame.PrepRadius`, 7m)
   but cannot leave it or fight (abilities are disabled, everyone is invulnerable, the gas and orbs wait). The **upgrade menu** opens: pick one of
   three (click a card or press 1-3). The sets are **fixed and identical for every player** (no randomness), defined per character
   (`BerserkerUpgrade.Sets`), and each is one **Armament**, one **Endurance** and one **Ferocity** card. Berserker:

   | Pick | Armament slot | Endurance | Ferocity |
   | --- | --- | --- | --- |
   | after round 1 | Eviscerator (Executioner) | Feel No Pain | Berserker's Fury |
   | after round 2 | Heavy Chainaxe (Reaver), **or Headsman's Mark if you hold the Eviscerator** | Blood-Fuelled Regeneration | Relentless Pursuit |
   | after round 3 | Whetted Edge (Eviscerator) / Rending Gouges (Heavy) / Chain Frenzy (no weapon) | Unstoppable | Skulltaker |
   | after round 4 | the first weapon upgrade not yet taken (Earthshaker, Whetted Edge, Headsman's Mark, Rending Gouges, Chain Frenzy) or Hamstring | Defiant Roar | Crimson Momentum |

   **The Armament slot is weapon-aware, so nobody can take a weapon every round:** until you hold a transformed weapon it offers the next weapon
   in the progression; once you hold one (one per match, and **for good**: no weapon can be swapped for another or back to the chainaxes, and owning a chainaxes-only upgrade - Keep the Dual Chainaxes, Chain Frenzy, Crimson Momentum, Relentless Pursuit - stops the weapons being offered, so a pick is never thrown away) it offers **upgrades unique to that weapon** instead:
   Eviscerator: Headsman's Mark (Descent recharges 30% faster, execute bonus from 70% health), Whetted Edge (20% faster wind-up, ignores 70% of armour, 2x vs shields);
   Heavy Chainaxe: Rending Gouges (bleeds 60% harder, 2s longer), Earthshaker (Gorecleave +25% reach, +50% knockback);
   dual chainaxes: Chain Frenzy (Axe Toss recharges 30% faster, each landed axe adds a Rage stack).
   The other two cards are never weapons, so a Berserker can also skip every weapon and build from them. Upgrades in no set
   (Hamstring, Skullthrone's Favour, Vengeful Blood, Gorehound's Vigour) stay in the catalog for the test panel and future sets.
   A character past its last set, or with no sets, falls back to a random draw.
   **Sanguinary Guard** (`GuardUpgrade`, slots draw a random alternative): after round 1 both weapons (Encarmine Axe, Glaive Encarmine) side by side plus Blood Ward / Deep Veins and Crimson Momentum / Unquenched / Overflowing Chalice; later picks offer the held weapon's own upgrades (Axe: Headsman's Due, Wellspring; Glaive: Dancing Blade, Blood Ricochet), or Death from Above / Seraphic Ascension to a guard who kept the sword, plus Sanctified Blood, Iron Halo, Wrath of Baal / Blade of Sanguinius (one card), Death Mask, Sanguinary Rite and Brimming Chalice. Weapons are for good here too. See `Docs/SanguinaryGuard.md` for the numbers.
3. **Lock-in**: once everyone has chosen (the menu stays at least 3s) or after 20s (undecided players get a random offer), picks are locked and a
   3s countdown runs, then the barrier drops and the round begins.

Upgrades last the rest of the match and reset on a rematch. Characters without an upgrade list skip the pick.

Khorne Berserker:

| | Dual chainaxes (default) | Heavy Chainaxe - The Reaver | Eviscerator - The Executioner |
| --- | --- | --- | --- |
| LMB | alternating chops | slow heavy sweep, greater reach, 150 degree arc | slow strike with a 0.55s wind-up, big single-target hit |
| RMB | Axe Toss | Gorecleave: wide frontal sweep, knockback + bleed | Executioner's Descent: overhead blow, up to +100% damage on low-health targets |
| Passive | Rage: consecutive hits stack attack speed (5 stacks, 3s window) | heavy hits inflict Bleed | attacks ignore 40% of armour and deal 1.5x damage to shields |

Weapons are optional and exclusive (the Heavy Chainaxe and Eviscerator replace each other; one weapon per match); the random pick for idle players never gives a weapon: a Berserker who never takes one stays competitive. Other upgrades: Ablative Plating, Gorehound's Vigour,
Blood-Fuelled Regeneration, Defiant Roar, Feel No Pain, Unstoppable (Endurance); Crimson Momentum, Gore Rush, Hamstring, Twin Axes,
Vengeful Blood, Skullthrone's Favour, Berserker's Fury, Relentless Pursuit, Skulltaker (Ferocity).

Thousand Sons Sorcerer (`Abilities/Sorcerer/SorcererUpgrade.cs`; each upgrade is applied inside the ability it changes), ported from Oldur's battlerites. The cards show three of the Cults of Prospero instead of the usual categories (`UpgradeDef.Themed`): Pavoni for Remedy, Raptora for Endurance, Corvidae for Ferocity. After round 1: Rejuvenate (Remedy: Mending Sands leaves Mending, +6 after 3.5s, which Dust Form sets off at once; this is Oldur's Rejuvenate, which the base ability no longer has), Sandshell (Endurance: Dust Form through an ally gives you both a 14 barrier for 3s), Hastened Ruin (Ferocity: a curse set off early deals +4). After round 2: Shifting Dunes (Remedy: each enemy hit by Sinking Sands gives a bonus Mending Sands charge, up to 5 in all, spent first and never recharged), Kinetic Ward (Endurance: each attack Kine Shield negates gives a barrier for 3.5s, 16 then 8 then 4), Echoing Hourglass (Ferocity: E again, from 0.5s after the first throw and for 2s, throws a lesser hourglass, 1.6m and 10 damage, the same snare, and it lays the curse instead of setting it off; it doesn't restart the main cooldown). After round 3: whichever Pavoni and Raptora cards are left, and Fires of Tzeentch (Pyrae: where Weaver of Fates brings someone back, a 2.5m pool of blue and pink warpfire burns enemies for 10/s for 4s; `FirePool.Tzeentch`, dealt by the returning character's machine and drawn elsewhere via `NetPlayer.SendWarpfire`). Later picks draw from what is left.

Mandrake (`Abilities/Mandrake/MandrakeUpgrade.cs`, effects in `MandrakeUpgradeEffects.cs`): each pick offers one Endurance, one venom and
one balefire card; the strongest three are spread one per pick (Veilwalker after round 1, Crippling Toxin after round 2, Twin Flames after
round 3), with Envenomed Edge and Smothering Fire alongside Veilwalker in the first pick. Endurance: Veilwalker (heal 5/s while stealthed), Shade's Embrace
(a successful riposte gives a 20 barrier for 3s), Reaping Shadows (Soul Harvest also refunds Shadow Jump). Venom: Crippling Toxin (his poison
slows 5% per stack, up to 15%), Envenomed Edge (the cleave lays 2 stacks), Hunter's Patience (+20% vs poisoned instead of +10%), Witchfire
Venom (capstone after round 4: a Balefire burst also detonates poison). Balefire: Twin Flames (2 charges, 10s each), Spreading Flame (a burst
lights a half-strength Balefire on enemies within 3m), Smothering Fire (the burst cuts healing by 40% instead of 30%).

Skitarii Ranger (`Abilities/SkitariiRanger/RangerUpgrade.cs`; each upgrade is applied inside the ability it changes): each pick offers one
Endurance, one Transuranic and one Galvanic/Shrapnel card; the strongest three are spread one per pick (Quickened Capacitors after round 1,
Arc Round after round 2, Servo-Leap after round 3). Endurance: Servo-Leap (Disengage holds 2 charges, 7s each), Refractor Field (landing a
Disengage gives a 20 barrier for 2s), Caltrop Burst (unlocks Disengage's caltrop EX), Augmetic Bracing (no knockback while charging
Transuranic Shot or firing Culling Volley). Transuranic: Quickened Capacitors (charges 30% faster), Executioner's Round (a full charge deals
+25% to enemies below half health), Overpenetration (+15% per enemy already pierced), Rad-Saturation (capstone after round 4: a full charge
irradiates, +15% damage taken from every source for 4s; `Combat/Irradiated.cs`). Galvanic/Shrapnel: Arc Round (unlocks Galvanic Shot's
arc EX), Galvanic Overload (+4 energy for a burst that lands every round), Lingering Shrapnel (field lasts 50% longer, 25% wider),
Point-Blank Doctrine (Scattershot costs 25 instead of 35 and shoves 50% farther). Always on, no upgrade needed: an enemy Scattershot
knocks into a wall is stunned for 1.5s.

- Code: `Scripts/Upgrades` (`UpgradeDef`, `UpgradeCatalog`, `UpgradeState`, `UpgradePickerView`); the Berserker list and numbers are in
  `Abilities/KhorneBerserker/BerserkerUpgrade.cs`, passives/effects in `BerserkerUpgradeEffects.cs`, `Combat/Rage.cs`. Weapon variants live inside
  `AxeChop` (LMB) and `AxeToss` (RMB) so the HUD slots, network cues and prefabs are unchanged. Weapon tuning fields are also in `Resources/Stats/KhorneBerserker.json`.
- Online, the owned-upgrade mask is a `NetPlayer` network variable, so replicas show the right weapon; damage is still resolved by the owner's machine.
  `Health` gained armour, bonus max health and armour-piercing and shield-breaking multipliers on `TakeDamage`. Both machines must run the same build.
- To add upgrades for another character: add a list like `BerserkerUpgrade.All` (and `Sets`) and register both in `UpgradeCatalog` (bit numbers are append-only).
- **Testing offline (editor / dev builds):** build the training arena with **Anathema > Rebuild Arena Scene (Khorne Berserker)**, press Play, and press
  **F1** for the upgrade test panel: click any upgrade to toggle it, **Next pick** runs the real picker with the next fixed set (click or 1-3), **Clear all** resets.
  Hit the dummies to try weapons, Rage (shown above the ability bar), bleed, armour, etc. F6 / F7 equip the Heavy Chainaxe / Eviscerator, F8 clears.
  **F2** (or the panel's button) restarts the "round" into the **prep phase** right in the training arena: you're put back at your spawn inside the
  barrier ring (can't leave it or fight), the three-card menu opens, and after you pick a 3s countdown drops the barrier. Upgrades picked this way persist, like in a match.
  The network sync and the multi-player lock-in need a real online match (host + join `127.0.0.1`, see Online).

## Online (2-6 players)

Menu flow: **Menu scene → Host / Join → character select → Versus scene** (best of 3 or 5 rounds, then Rematch or Main Menu).

- Up to 6 players (`NetGame.MaxSeats`). The host picks the **game mode** in the lobby: **Free for all** (every player is their own team: 1v1, 1v1v1, ...)
  or **Teams** (everyone picks Team A or B: 2v2, 3v3, ...). A match starts once at least two teams exist and everyone is ready.
- A round ends when only one team has anyone alive; that team wins the round (a round everyone dies in goes to the team of the last death).
  Dead players stay down until the round resets. If players leave mid-match it carries on while two teams remain.
- Teams are `Health.Team` ids (1-based; synced via `NetPlayer`). Spawns are computed in `NetGame.SpawnPosition` (ring for FFA, two lines for Teams) rather than read from the scene's `SpawnPoint_*` markers.

- `Assets/Scenes/Menu.unity` is the entry point; `Versus.unity` is the online arena; `Arena.unity` stays the offline training ground (dummies).
- Netcode for GameObjects with direct IP (UDP **7777**). The host clicks Host Game, the friend types the host's IP and clicks Join Game.
  Over the internet the host needs to forward UDP 7777, or both players use a VPN such as Tailscale / Radmin / ZeroTier.
- Each machine is authoritative for its own character (input, movement, abilities, health). Hits are detected by the attacker and
  forwarded to the victim's machine (`Health.TakeDamage` → `NetPlayer.SendDamage`); status effects (`Stunned/Silenced/Maimed/Rooted/Knockback.Apply`)
  are forwarded the same way. The other player's character is a "replica" (`Health.IsReplica`): it follows synced position/aim/health and replays the
  owner's ability visuals but never reads input, spends energy or deals damage. Ability code that moves the character or reads input must check `IsReplica`.
- Scripts: `Scripts/Net/NetGame.cs` (session, lobby, match flow, all menu screens), `NetPlayer.cs` (per-character sync + RPCs), `MenuBootstrap.cs`.
- **After adding or removing an ability, run Anathema > Rebuild Online Setup** so the networked player prefabs in `Assets/Resources/Net` pick it up
  (both machines must run the same build).
- Testing alone: build a Windows player (File > Build Profiles, scenes Menu/Versus/Arena are already listed), run it next to the editor,
  host in one and join `127.0.0.1` in the other.

## Arenas

The host picks one of the online arenas at random each match (`NetGame.ArenaScenes`). All share the 28m round footprint, the two
ready rooms and the gas; they differ in cover and dressing. Built by `ArenaSceneBuilder.CreateArena(map)`; open or rebuild them from **Anathema > Arenas**.

| Arena | Scene | Layout |
| --- | --- | --- |
| Colosseum | `Versus.unity` | Four barricades around the centre, four chevron bunkers on the diagonals |
| Shrine | `Versus_Shrine.unity` | Same layout as the Colosseum, dressed as a ruined chapel |
| Crucible | `Versus_Crucible.unity` | Forge temple with a **breakable floor** (below) |
| Hoarfrost Basilica | `Versus_Hoarfrost.unity` | Frozen cathedral with **slam-launched reliquary caskets** (below) |

**Crucible layout** (mirror-symmetric on both axes): an unbreakable dais in the centre (where the charge orb appears) ringed by four
anvils; a forge wall north and south of the centre, each split by a gap in the middle; a bulwark in front of each doorway; an L-shaped
bunker in each corner, open towards the centre; and an unbreakable steel walkway ring at the spawn radius plus steel approaches to the doorways.

**Breakable floor** (`Scripts/Arena/DestructibleFloor.cs`): the dark stone plates (with glowing seams) can be smashed; the light, seamless
steel deck can't. Ground slams break every plate under the impact, which drops into a pit of lava:

- Slams: Grey Knight **Daemonhammer Q slam** (2.7m radius), the Khorne Berserker's **Blood for the Blood God** (along the whole charge, 1.2m either side),
  the Sorcerer's **Sinking Sands** (where the hourglass lands) and the Psyker's **Gravity Well** (where it collapses).
  Call `DestructibleFloor.Slam(caster, from, to, radius)` from an ability's owner-side impact to add more.
- A pit flashes white-hot for 0.4s, then burns anyone standing in it (either team) for 10 damage/s (AoE, damage over time) for the rest
  of the round: the arena gets more dangerous as the fight goes on. The whole floor resets with the gas every round. (Tick `regenerate`
  on the component to have plates crust over and re-form 18s after breaking instead.)
- Online the caster's machine reports the slam (`NetPlayer.SendFloorBreak`); the server picks the plates from its own copy of the floor and
  tells every machine which ones broke, so everyone sees the same holes. Fire damage is server-side, like the gas.
- Testing offline: **Anathema > Arenas > Rebuild Crucible Training (Khorne Berserker / Grey Knight)** builds `Arena_Crucible.unity` with the
  usual dummies (the Grey Knight needs the Daemonhammer from the F1 upgrade panel to slam).

**Hoarfrost Basilica layout** (`Versus_Hoarfrost.unity`, mirror-symmetric on both axes): a frozen cathedral with an ice floor, tighter
than the other maps (25m radius instead of 28m). Four **frozen reliquaries** stand on gold plinths on the diagonals at (±6.5, ±6.5);
four screen walls on the axes 10.5m out form an inner ring of hard cover between them; eight ice-clad pillars ring the middle at 16m
(30 degrees either side of each axis); a broken altar wall stands north and south; and a bulwark in front of each doorway stops any
slide leaving through a doorway into a ready room.

**Reliquary caskets** (`Scripts/Arena/CasketField.cs`, `ReliquaryCasket.cs`, `Combat/CasketCarried.cs`): at rest a casket is solid cover
(it blocks movement and shots, and wall-slam abilities like Bull Charge treat it as a wall). A slam that lands on one launches it:

- Slams: the Bullgryn's **Slab Slam** (empowered maul), **Shield Bash** and **Living Wall** shove, the Berserker's **Gorecleave** and
  **Blood for the Blood God**, the Psyker's **Kine Slam** and the Grey Knight's **Daemonhammer Q slam**. A radial slam pushes the casket away
  from the impact, a swing pushes it away from the attacker, Living Wall pushes it along the wall's path. Call `CasketField.Slam`, `Cone` or
  `Sweep` from an ability's owner-side impact to add more (every `DestructibleFloor.Slam` already counts).
- It slides at 20 m/s in a straight line until solid scenery stops it (up to 42m: across the whole map), leaving a frost trail.
- The first enemy of the slammer in its path is pinned to its front and carried: no walking or steering. When they reach the wall
  (or a pillar, or another casket) they take **50 damage** and are **stunned for 2.5s** (Grit shortens it like any stun).
- **Escaping:** a carried enemy who casts a dedicated movement ability breaks free with no stun, and the casket slides on. That is any
  ability with a `MovementRange` (`Ability.EscapesCasket`): Teleport Strike, Axe Rush, Warp Step, Disengage, Shadow Jump, Dust Form,
  Winged Escape and Bull Charge. Ultimates don't count (Blood for the Blood God and Shadow Cross opt out), and the Hospitaller has none.
  A stun, silence or panic on the carried enemy shuts the escape off.
- Enemies who can't be shoved (Unstoppable, Enraged, Augmetic Bracing) stop the casket dead: 50 damage, no carry, no stun.
- A casket shatters when its slide ends and is back on its plinth 10s later (and at every round reset), the ice visibly re-forming; it waits while anyone stands on the plinth. A free slide that ends against
  another resting casket knocks that one on in the same direction.
- Online the caster's machine reports the slam to the server (named message `casketSlam`); the server picks the caskets, works out each
  slide and tells every machine (`casket`). Catches, escapes (`casketEscape`, sent before the movement ability's cue) and impacts are decided
  on the server only; the impact is resolved 0.12s after the casket stops, so an escape sent just before impact still counts.
- **Breakable ice** (`DestructibleFloor` in frozen-water mode): the clear ice plates break like the Crucible's, the packed snow (the centre,
  under cover, the plinths, the doorway approaches) doesn't. A hole is open water with floes in it: no damage, but **25% slower** while
  standing in it (`DestructibleFloor.SpeedMultiplierAt`, read by `MovementSim` on the server and the owner alike). It freezes over again
  20s later. Every casket-launching slam above cracks the ice where it lands (`CasketField.Slam`/`Cone`/`Sweep` call
  `DestructibleFloor.Crack`), so do Sinking Sands and Gravity Well, and so does a casket smashing into the wall.
- Testing offline: **Anathema > Arenas > Rebuild Hoarfrost Training (Bullgryn / Khorne Berserker)** builds `Arena_Hoarfrost.unity` with the
  usual dummies.
