# Server-owned movement (step 1)

Status: implemented on branch `server-authoritative`, not yet tested in Unity.

## Goal

The server decides where every player is. Clients send movement **input**, not positions. Your own character still
moves the instant you press a key (prediction) and quietly corrects if the server disagrees. This gives the server
positions it can trust, which step 2 (lag-compensated hit checks) needs.

In scope: walking, gravity, speed modifiers (slow, maim, enrage, Rage, Reflect Dome), stun, root, counter guard,
ability roots (wind-ups that plant your feet), panic fleeing, knockback (including Gravity Well's pull), the prep-area
leash, spawning and round resets.

Not in scope (later steps): dashes, leaps and teleports are still performed by the client, but the server checks
them (see "Ability movement" below). Cooldowns, energy and hit detection stay client-side until step 4.

## Today

- `PlayerMovement.Update` runs only on the owner: reads WASD, applies modifiers, calls `CharacterController.Move`.
- The owner writes `NetPlayer.netPosition` (owner-write NetworkVariable). Every other machine, the server included,
  lerps a replica toward it. Replicas have the CharacterController switched off and use a plain capsule.
- Control effects (stun, root, slow, maim, panic, knockback) are decided by the server but applied and enforced only
  on the owner's machine (`StatusClientRpc`). Enraged and Rage stacks live only on the owner.

## The model

### 1. One shared movement step

A new pure-ish function used by both client and server:

```
MovementSim.Step(CharacterController controller, ref MoveState state, MoveInput input, MoveContext ctx, float dt)
```

- `MoveInput`: sequence number (uint), world-space move direction (Vector2 on XZ, clamped to length 1).
  The client converts WASD to world space using its camera before sending, so the server needs no camera.
- `MoveState`: position, vertical velocity, knockback velocity and remaining time.
- `MoveContext`: everything the step reads from status effects, gathered once per step: speed multiplier, "can't
  steer" (stun, root, guard, ability root), flee direction, frozen, prep leash centre and radius.

All of today's `PlayerMovement` logic moves into `Step` unchanged in behaviour. `Knockback.Update`'s movement moves in
as well, so a knockback is part of the same simulation instead of a separate mover.

Fixed timestep: **60 Hz**, run from an accumulator, never from `Time.deltaTime` directly. Same on client and server.

### 2. Client (the owner)

Every 60 Hz step:
1. Sample WASD, convert to a world direction, assign the next sequence number.
2. Run `Step` locally right away (prediction) and store `(input, resulting state)` in a history buffer.
3. Queue the input for sending.

Every network tick (NGO's 30 Hz tick), send the queued inputs plus the previous 3 again for redundancy, in one
**unreliable** RPC. A lost packet doesn't stall movement, because the next one repeats the inputs.

When an authoritative state arrives (`seq`, position, vertical velocity, knockback):
1. Drop history entries up to and including `seq`.
2. Compare the server position with what this client predicted for `seq`.
   - Under 1 cm: nothing to do (the common case).
   - Otherwise: set the state to the server's, replay every stored input after `seq` through `Step`, and land on a
     corrected present position.
3. Visual smoothing: the character's root moves to the corrected position immediately (so aiming and hits use the
   truth), but the visible model is offset and blends back over ~100 ms. Errors over 2 m (respawns, teleports) snap.

### 3. Server

Per player:
- Keeps a queue of received inputs, ignoring duplicates (sequence numbers already processed).
- Every 60 Hz step, consumes exactly one input and runs `Step` on its copy of the character.
  - Queue empty (packet loss or lag): repeat the last input for up to 6 steps (100 ms), then stand still.
  - Queue longer than 6: drop the oldest extras. A client can't move faster by sending inputs faster: the server
    consumes them at a fixed rate. This is the speed-hack protection.
- The server's copy of a remote player turns its CharacterController **on** (it collides with walls like the owner
  does) and its proxy capsule off.
- After each network tick, publishes the authoritative state:
  - to the owner: last processed `seq` plus the full `MoveState` (unreliable, every tick);
  - to everyone: position, through `netPosition`, which becomes **server-write**. Other clients keep interpolating as
    they do today.

### 4. Status effects move to the server

The server must know every effect that changes movement. Control effects become server-held, like ward, bleed and burn
already are:
- `StatusServerRpc` applies stun, root, slow, maim, panic and knockback to the **server's copy** (the truth), then
  forwards them to the owner, who needs them to predict and to block ability use.
- The owner's copies are applied when they arrive. A stun that lands between a prediction and the server's state is a
  normal correction (the "legitimate" kind).
- Panic breaking on a heavy hit happens on the server's copy directly. The forward to the owner stays.
- Enraged and Rage stacks are produced by the owner's own abilities today. In step 1 the server reads them from what the
  owner publishes (`netRageState` and the status mask), capped at the real maximums. They become fully server-side in
  step 4, together with cooldowns.
- The counter guard and ability roots already replay on the server's copy from the owner's cues, so the server can
  read `IsGuarding` and `RootsMovement` there.

### 5. Ability movement (dashes, leaps, teleports) in step 1

These six move the character on the owner's machine:

| Ability | Character | How it moves |
|---|---|---|
| Axe Rush | Khorne Berserker | dash, `controller.Move` |
| Blood for the Blood God | Khorne Berserker | charge, `controller.Move` |
| Warp Step | Psyker | teleport |
| Counter teleport | Grey Knight | teleport |
| Disengage | Skitarii Ranger | leap |
| Winged Escape | Plague Surgeon | flight |

In step 1 they still run on the client. While one is active, the client sends an `AbilityMove` input instead of WASD:
the ability's index plus the position it moved to. The server accepts it if:
- that ability was cued (its replay is running on the server's copy);
- the distance covered is within the ability's maximum range plus a 10% tolerance;
- the character isn't stunned or rooted on the server.

Otherwise the server rejects it and the client is corrected back. Step 3 moves these into `Step` on the server.

### 6. Spawns, resets and teleports by the game

Spawning, respawn and round resets are decided by the server. It sets the position and sends the state with a `reset`
flag. The owner clears its prediction history and snaps (no smoothing).

### 7. Collisions between players

Today players block each other through the replicas' proxy capsules. With prediction, the owner would collide with
where it *sees* other players (slightly in the past), while the server collides with where they *are*. That disagreement
causes corrections whenever players touch.

**Decision: players keep blocking each other.** Characters collide with each other exactly as today, on the server
(against the other players' real positions) and in the owner's prediction (against where it sees them). Brushing
against another player will cause small corrections; the model-only smoothing (section 2) hides the small ones. This can
be revisited with step 2's position history (predicting against rewound positions).

## What changes, file by file

- New `Player/MovementSim.cs`: `MoveInput`, `MoveState`, `MoveContext`, `Step`.
- `Player/PlayerMovement.cs`: becomes the client side (sampling, prediction, history, reconciliation, smoothing) and the
  server side (input queue, fixed-rate consumption, publishing). Offline it simply runs `Step` locally every tick.
- `Combat/Knockback.cs`: the component keeps its state but no longer moves anything; `Step` applies it.
- `Net/NetPlayer.cs`: `netPosition` becomes server-write. New RPCs: inputs (owner to server, unreliable), state (server
  to owner, unreliable). Controller and proxy setup for the server's copy. Control effects applied on the server's copy.
- `Combat/Respawner.cs`, `Net/NetGame.cs`: resets and spawns set the position on the server.
- The six abilities above: report their movement as `AbilityMove` input.
- `Net/NetPlayer.cs` (fix found while designing): ability cues are relayed with a ClientRpc, which a dedicated server
  never runs, so the server's copies never replayed abilities (no counter guard, no shield there). The server now
  replays cues itself.

## Risks and how to check them

- **Divergence (constant rubber-banding):** a debug overlay showing the correction count and size per second. In a
  local test (server and clients on one machine, no lag) it should read zero while walking.
- **Feel under latency:** test with Unity Transport's simulator (packet delay 100 ms, 2% loss): walking must stay
  smooth; being stunned should cause one small snap.
- **Offline play:** must be unchanged: `Step` runs locally with no prediction or network.

## Decisions

1. Players keep blocking each other (decided).
2. Correction thresholds (1 cm "ignore", 2 m "snap") and smoothing time (100 ms): defaults, tunable later.
