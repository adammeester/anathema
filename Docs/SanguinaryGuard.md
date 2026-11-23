# Sanguinary Guard (design draft)

Blood Angels melee battle-medic. High risk, high reward: he heals his team *through* fighting, and his big swing is
earned by supporting, never by dealing damage.

**Look:** Sanguinary Guard style (`Art/sanguinary_guard.py`): burnished gold armour over a black bodyglove, a gold
death-mask helm framed by a sunburst halo, a red right pauldron and a white left one, ruby blood drops at the collar and
belt, a scroll across the chest, great white feathered wings on the jump pack, and a long blade with a chain-wheel at
its base. The Blood Chalice hangs at his hip and a narthecium sits on his left forearm, so he still reads as a battle-medic.

**Stats:** ~2.5 m, 170 HP (below the other marines: his healing only flows while he's in danger), 5.2 move speed.

## Passive: Vitae (the Blood Chalice)

- **Sources (never damage, except under Red Thirst):**
  - Healing done on allies and himself: 1 Vitae per point. Real healing only (no overheal).
  - Health he pays for Blood Transfusion and Chalice Offering: 1 Vitae per point.
  - (Draft idea, not built yet: damage prevented for allies.)
- **Front-line bonus:** healing given while an enemy is within 3 m of him is x1.5.
- **Cap 100.** After 3s without healing or preventing damage, it drains 6/s.
- **At full** the next LMB is **Exsanguinating Arc** and spends the chalice:
  - 0.25s wind-up with a filling ground ring around him.
  - Signature combo: Crimson Rush's 1.2s stun is the window to land the Arc on the stunned enemy.
  - A **360 degree** spin slash, 3.3 m radius, 28 damage, 30% slow for 1s.
  - Lifesteal: each landed hit heals him for 80% of its damage (22 per enemy). This is his only self-heal.
  - Every other ally within 6 m heals for 40% of each hit.
  - A landed Arc brings his next Chalice charge forward by 50% of its recharge (25% per Arc during Wings of the Angel).
  - Nothing from the Arc refills Vitae.
- **Risk:** while the chalice is full his blade glows for enemies and he takes 10% more damage.
- Pips above the action bar via `IPassiveStacks` (like Faith and Grit).
- No on-kill triggers anywhere in the kit.

## Passive: Red Thirst

- The Blood Angels' curse. At or below **20% health** the thirst takes him:
  - +20% move speed, +30% attack speed (Power Sword, the Arc and ascended Arcs).
  - Jump Pack Descent's slam deals +50% (12 to 18).
  - Lifesteal is unchanged.
  - Damage from Power Sword cuts and Crimson Rush fills Vitae, 1 per point (not the Arc, the slam, the riposte or Chalice of Wrath). The one exception to "never damage", so a guard bled dry can fight his way back to an Arc.
- It lifts as soon as he is back above 20%. His health costs can push him into it (never below the 15% floor).
- Look: eye lenses flare blood red and streak as he moves, a red heartbeat light on his chest, dark red vapour rising.
- Derived from replicated health, so every machine agrees with no extra network traffic.

## Kit

| Slot | Ability | Spec |
| --- | --- | --- |
| LMB | Power Sword | 2-hit chain, 100 degrees, 2.6 m, 9 damage, 0.7s cadence. No lifesteal: his blood comes back on the Arc. |
| RMB | Blood Transfusion | Skillshot: a blood bolt along his aim (12 m, 20 m/s). He pays 12 health as he throws it; the first teammate it touches (never himself) heals 22. Enemies in the way soak it up for no damage; a miss is wasted blood. What he pays fills Vitae and stays healable, so the Arc's lifesteal wins it back. Not castable below 15% health. 4s cooldown. |
| Q | Jump Pack Descent | Rooted 0.55s spool-up (twin engines light with blue flame and smoke; cancellable), then launch high toward the cursor (up to 12 m, over walls), then slam down: 12 damage and a 40% slow for 1.5s in 2.8 m, with ground cracks that fade over 3s. Pure offence: nothing for allies. 10s cooldown, his only mobility. |
| E | Chalice Offering | He pays 15 health; teammates within 4 m (not himself) heal 20, +50% per enemy within 4 m of him (max +100%). 2 charges, each recharging over 12s. **EX Chalice of Wrath** (35 energy, uses a charge, no health): every enemy within 4 m takes 18 and each landed hit heals him 80% of it (no Vitae). Needs an enemy in reach. |
| Space | Sanguine Parry | Counter (1.2s guard): the hit is negated, +10 energy, an attacker within 3.5 m takes 8, and the most injured teammate within 8 m (himself included) heals 15. 4s cooldown. |
| R | Crimson Rush | 0.2s wind-up with a lane telegraph, then a 6 m jump-pack dash; the first enemy reached stops it and takes 14 and a 1.2s stun. Only that enemy is hit. 13s cooldown. Set-up for the Arc: the stun outlasts its wind-up. |
| F | Wings of the Angel | Full energy bar: 0.6s gather while his own wings spread and blaze gold (they stay lit, beating slowly, while he is ascended), leap-slam at the cursor; 6 m ring heals allies 30 and deals 20 to enemies. For 5s every LMB is an Exsanguinating Arc, on a 0.9s cadence. |

## Visuals

Charged weapon effect built like `PsychicHalberdEffect` (glow sleeve, point light, crackling arcs), blood red, in stages:

| Vitae | Look |
| --- | --- |
| 33% | Faint red sheen on the blade |
| 66% | Glow sleeve and light; blood drips from the blade |
| 100% | Strong pulsing glow, red arcs off the blade, blood mist trailing him, red halo glyph above his head |

**Exsanguinating Arc animation:** a low crouch with the blade drawn back during the wind-up, then 1.25 turns in ~0.3s
with the blade at arm's length. Thick red crescent trail (`SlashEffect`, blood palette, wider and longer-lived), the ground
ring flashes outward, a blood-mist burst, red streams snaking to each healed ally, a short camera shake for his own player,
and a power-field crackle and blade cut plus a choir sting. The glow fades over 0.5s afterwards.

## Upgrade sketch (Armament / Endurance / Ferocity)

- **Armament:** Power Axe (the Arc becomes a 4.5 m overhead line instead of a spin), then an Encarmine Sword (Arc radius 3.8 m).
- **Endurance:** Sanctified Blood (Vitae no longer drains).
- **Ferocity:** Brimming (cap 130, Arc damage up); Overflowing Chalice (overheal counts at half).

## Built in pass 1 (differences from the draft above)

- **Self-healing fills Vitae at the full rate** (decided after the draft), so he can charge in 1v1 and training.
  His heals (blood stream, Chalice Offering, Parry) all include himself as a target.
- **Damage prevented is not a Vitae source yet**: only healing counts. Tracking prevented damage needs a server
  report; left for later if the healing-only fill feels too slow.
- **Sanguine Parry** heals a flat 15 instead of "the damage it negated" (the counter payoff isn't told the amount).
- **Chalice Offering** heals instantly rather than over 2s.
- **Jump Pack** is pure offence: the ally heal and protection were removed.
- The +10% damage taken while full also applies during Wings of the Angel's 5s (both publish the same "empowered" flag).
- Other players see only the full-chalice glow stage; the 33% and 66% stages show on his own screen.
- No upgrade tree yet.

## Open questions

- Vitae fill rate: check that a full chalice takes roughly 4-6s of committed brawling while the team is taking damage.
- Front-line bonus readability: maybe a faint blood mist on him while it's active.

## Implementation notes

- Reuse Grit's "full passive powers the next LMB, then spends it" flow and its pips.
- New: counting effective healing done on allies, and damage prevented, per source.
- Add `Resources/Stats/SanguinaryGuard.json` alongside the abilities (it overrides ability fields at spawn).
