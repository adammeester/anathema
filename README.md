# Anathema

A Battlerite-style arena brawler set in the Warhammer 40k universe. Unity 6 (6000.4.2f1).

## Current scope

Core feel only: movement, mouse aim, camera, basic attack, health. No abilities yet.

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

Models are generated in Blender: `blender -b --python Art/khorne_berserker.py` writes `Assets/Models/KhorneBerserker.fbx`
(add `-- --render DIR` for preview PNGs). Abilities live in `Scripts/Abilities/<Character>/`.

## Between-round upgrades (Khorne Berserker first)

After every round except the last the match goes through the **prep phase** (Battlerite style):

1. **Report** (5s, frozen): the round result and damage report.
2. **Prep**: everyone is put back at their spawn at full health and may walk around inside a glowing barrier ring (`NetGame.PrepRadius`, 7m)
   but cannot leave it or fight (abilities are disabled, everyone is invulnerable, the gas and orbs wait). The **upgrade menu** opens: pick one of
   three (click a card or press 1-3), one per category: **Armament** (weapon transformations), **Endurance** (defensive), **Ferocity** (ability enhancements).
3. **Lock-in**: once everyone has chosen (the menu stays at least 3s) or after 20s (undecided players get a random offer), picks are locked and a
   3s countdown runs, then the barrier drops and the round begins.

Upgrades last the rest of the match and reset on a rematch. Characters without an upgrade list skip the pick.

Khorne Berserker:

| | Dual chainaxes (default) | Heavy Chainaxe - The Reaver | Eviscerator - The Executioner |
| --- | --- | --- | --- |
| LMB | alternating chops | slow heavy sweep, greater reach, 150 degree arc | slow strike with a 0.55s wind-up, big single-target hit |
| RMB | Axe Toss | Gorecleave: wide frontal sweep, knockback + bleed | Executioner's Descent: overhead blow, up to +100% damage on low-health targets |
| Passive | Rage: consecutive hits stack attack speed (5 stacks, 3s window) | heavy hits inflict Bleed | attacks ignore 40% of armour and shields |

Weapons are optional and exclusive (one per match: once you hold one, no other weapon is offered; a weapon you decline keeps being offered until you take another upgrade type or a weapon, and the random pick for idle players never gives a weapon): a Berserker who never takes one stays competitive. Other upgrades: Ablative Plating, Gorehound's Vigour,
Blood-Fuelled Regeneration, Defiant Roar, Feel No Pain, Unstoppable (Endurance); Crimson Momentum, Gore Rush, Hamstring, Twin Axes,
Vengeful Blood, Skullthrone's Favour, Berserker's Fury, Relentless Pursuit, Skulltaker (Ferocity).

- Code: `Scripts/Upgrades` (`UpgradeDef`, `UpgradeCatalog`, `UpgradeState`, `UpgradePickerView`); the Berserker list and numbers are in
  `Abilities/KhorneBerserker/BerserkerUpgrade.cs`, passives/effects in `BerserkerUpgradeEffects.cs`, `Combat/Rage.cs`. Weapon variants live inside
  `AxeChop` (LMB) and `AxeToss` (RMB) so the HUD slots, network cues and prefabs are unchanged. Weapon tuning fields are also in `Resources/Stats/KhorneBerserker.json`.
- Online, the owned-upgrade mask is a `NetPlayer` network variable, so replicas show the right weapon; damage is still resolved by the owner's machine.
  `Health` gained armour, bonus max health and an armour-piercing flag on `TakeDamage`. Both machines must run the same build.
- To add upgrades for another character: add a list like `BerserkerUpgrade.All` and register it in `UpgradeCatalog` (bit numbers are append-only).
- Offline training ground has no rounds: in the editor / dev builds F6 / F7 equip the Heavy Chainaxe / Eviscerator and F8 clears them.

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
