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

Models are generated in Blender: `blender -b --python Art/khorne_berserker.py` writes `Assets/Models/KhorneBerserker.fbx`
(add `-- --render DIR` for preview PNGs). Abilities live in `Scripts/Abilities/<Character>/`.

## Online 1v1

Menu flow: **Menu scene → Host / Join → character select → Versus scene** (first to 5 kills, then Rematch or Main Menu).

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
