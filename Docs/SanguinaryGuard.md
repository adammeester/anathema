# Sanguinary Guard (design draft)

Blood Angels melee battle-medic. High risk, high reward: he heals his team *through* fighting, and his big swing is
earned by supporting, never by dealing damage.

**Look:** Sanguinary Guard style (`Art/sanguinary_guard.py`): burnished gold armour over a black bodyglove, a gold
death-mask helm framed by a sunburst halo, a red right pauldron and a white left one, ruby blood drops at the collar and
belt, a scroll across the chest, great white feathered wings on the jump pack, and a long blade with a chain-wheel at
its base. The Blood Chalice hangs at his hip and a narthecium sits on his left forearm, so he still reads as a battle-medic.

**Stats:** ~2.5 m, 170 HP (below the other marines: his healing only flows while he's in danger), 5.2 move speed.

## Passive: Vitae (the Blood Chalice)

- **Sources (damage only as Spilled Blood and under Red Thirst):**
  - Healing done on allies and himself: 1 Vitae per point. Real healing only (no overheal).
  - Health he pays for Blood Transfusion and Chalice Offering: 1 Vitae per point.
  - A successful Sanguine Parry: fills it completely (his next swing is the Arc).
  - Spilled Blood: health he loses to an enemy within 4 m, 0.5 per point (not from range, not hits a shield soaks). The answer to being dived: it only tops up an engine already running (healing is about 5x more Vitae per health lost) and can't fill the chalice on its own (200 damage for 100, from 170 health).
  - (Draft idea, not built yet: damage prevented for allies.)
- **Front-line bonus:** healing given while an enemy is within 3 m of him is x1.5.
- **Cap 100.** After 5s without gaining any, it drains 4/s (a full chalice never drains).
- **At full** the next LMB is **Exsanguinating Arc** and spends the chalice:
  - 0.25s wind-up with a filling ground ring around him.
  - Signature combo: Crimson Rush's 1.2s stun is the window to land the Arc on the stunned enemy.
  - A **360 degree** spin slash, 3.3 m radius, 28 damage, 30% slow for 1s.
  - Lifesteal: each landed hit heals him for 80% of its damage (22 per enemy). This is his only self-heal.
  - Blood Returns: a landed Arc also heals him half of the health he has paid since his last landed Arc (pay 40, hit one: 20 + 22 back). A whiff returns nothing; the debt carries over to the next Arc.
  - Every other ally within 6 m heals for 40% of each hit.
  - Only during Wings of the Angel: each landed Arc brings his next Chalice charge forward by 25% of its recharge.
  - Nothing from the Arc refills Vitae.
- **Risk:** while the chalice is full his blade glows for enemies and he takes 10% more damage.
- Pips above the action bar via `IPassiveStacks` (like Faith and Grit).
- No on-kill triggers anywhere in the kit.

## Passive: Red Thirst

- The Blood Angels' curse. At or below **20% health** the thirst takes him:
  - +20% move speed, +30% attack speed (Power Sword, the Arc and ascended Arcs).
  - Jump Pack Descent's slam deals +50% (12 to 18).
  - Lifesteal is unchanged.
  - Damage from Power Sword cuts and Crimson Rush fills Vitae, 2 per point (not the Arc, the slam or Chalice of Wrath). The one exception to "never damage", so a guard bled dry can fight his way back to an Arc.
- It lifts as soon as he is back above 20%. His health costs can push him into it (they then lock until he is back above it).
- Look: eye lenses, the blade (middle and point, whatever weapon) and both wing tips flare blood red and streak as he moves, a red heartbeat light on his chest, dark red vapour rising.
- Derived from replicated health, so every machine agrees with no extra network traffic.

## Kit

| Slot | Ability | Spec |
| --- | --- | --- |
| LMB | Power Sword | 2-hit chain, 100 degrees, 2.6 m, 9 damage, 0.7s cadence. No lifesteal: his blood comes back on the Arc. |
| RMB | Blood Transfusion | 0.3s wind-up (cancellable): his free arm draws back, blood gathers in his palm and the locked aim shows as a lane on the ground, so enemies can read it and step in to block. Skillshot: a blood bolt along his aim (12 m, 20 m/s). He pays 12 health as he throws it; the first teammate it touches (never himself) heals 22. Enemies in the way soak it up: a 1-damage hit their counter can catch, then a bleed of 3/s for 3s (the heal is lost); a miss is wasted blood. What he pays fills Vitae and stays healable, so the Arc's lifesteal wins it back. Locked only while Red Thirst has him; usable at any other health (it can carry him into the thirst). 4s cooldown. |
| Q | Jump Pack Descent | Rooted 0.55s spool-up (twin engines light with blue flame and smoke; cancellable), then launch high toward the cursor (up to 12 m, over walls), then slam down: 12 damage and a 40% slow for 1.5s in 2.8 m, with ground cracks that fade over 3s. Nothing for allies. He lands braced: 30% less damage taken for 1.5s, so diving out of a focus is a real escape. 10s cooldown, his only mobility. |
| E | Chalice Offering | Wind-up shows a gold template: a soft gold pool and rim with turning spokes over the heal reach, and a dashed red ring at the enemy-bonus range (Chalice of Wrath shows a dark-red filling ring instead). He pays 15 health; teammates within 4 m (not himself) heal 20, +50% per enemy within 5 m of him (max +100%). 3 charges, each recharging over 12s. **EX Chalice of Wrath** (35 energy, uses a charge, no health): every enemy within 4 m takes 6 (empty chalice) to 18 (full), scaling with Vitae without spending it; each landed hit heals him 80% of it (no Vitae). Castable with nobody in reach; a whiff still spends the charge and energy. A ground ring marks its reach during the wind-up and a blood splash bursts across the ground on landing. |
| Space | Sanguine Parry | Counter (1.2s guard): the hit is negated and +10 energy. No automatic riposte: instead Vitae fills to the brim, so his next swing is the Exsanguinating Arc and he retaliates himself. His next Chalice charge comes 50% of its recharge sooner. The parry heals no one itself: the Arc it sets up does. 4s cooldown. |
| R | Crimson Rush | 0.2s wind-up with a lane telegraph, then a 6 m jump-pack dash; the first enemy reached stops it and takes 14 and a 1.2s stun. Only that enemy is hit. 13s cooldown. Set-up for the Arc: the stun outlasts its wind-up. **EX Blade Hurl** (50 energy, shares the cooldown): 0.25s wind-up, then he hurls his sword (skillshot, 10 m at 24 m/s): the first enemy hit takes 10 and a 1.5s stun. It flies back to his hand over 0.6s; until then he is disarmed (no Power Sword or Arc; heals, Jump Pack, Chalice and the counter still work). A ranged peel and a set-up for Jump Pack (spool-up plus flight is 1.25s). |
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

## Upgrades (`GuardUpgrade`, built)

Weapons keep Vitae and Red Thirst; they replace the cuts, the Arc and Blood Transfusion. A weapon is for good: no swapping back.

**Armament**
- **Encarmine Axe (the Butcher):** chops 15 damage, every 1.0s, 2.8 m, 110 degrees. Arc → **Executioner's Cleave**: 4.5 m x 1.6 m line in front, 40 damage (×1.2 with Brimming), same lifesteal and ally share, overhead pose, lane telegraph. RMB → **Sanguine Font**: pay 12 health, a 2.5 m blood pool up to 8 m away heals allies in it (not him) 7/s for 3s; 7s cooldown.
  - Headsman's Due: Cleave slows 50% instead of 30%.
  - Wellspring: Font lasts 5s and allies who leave keep healing 2s.
- **Glaive Encarmine (the Duellist):** thrusts 10 damage, 3.4 m, 70 degrees. Arc → **Twin Crescent**: two spins 0.2s apart at 70% each (lifesteal and slow twice; the Wings refund counts once). Transfusion heals 18, then bounces to the nearest other ally within 6 m for 10.
  - Dancing Blade: hitting the same enemy with both spins gives 20 energy.
  - Blood Ricochet: the bounce heals the full 18.

**Endurance**
- Sanctified Blood: Vitae never drains.
- Deep Veins: +25 max health, and Red Thirst no longer locks Transfusion and Offering.
- Iron Halo: once per round, at 20% health or less, a 40 shield for 3s.
- Blood Ward: paying health gives a shield of half the amount for 2s.

**Ferocity**
- Brimming Chalice: Vitae cap 130; Arc damage +20%.
- Overflowing Chalice: overhealing fills Vitae at half rate.
- Unquenched: Red Thirst from 30% health.
- Wrath of Baal: Blade Hurl's weapon returns in 0.3s.
- Death from Above: the Jump Pack slam stuns 0.5s.
- Crimson Momentum: Crimson Rush / Blade Hurl cooldown ×0.7 (9s).
- Sanguinary Rite: Chalice Offering charges recharge in 9s instead of 12s.
- Death Mask: Chalice of Wrath panics for 0.8s.
- Seraphic Ascension: Wings of the Angel ascension 7s.
- Blade of Sanguinius: Blade Hurl ricochets to the nearest other enemy within 6 m, up to 2 bounces, half damage and half stun each; it flies home from its last hit. Every machine works the bounces out from where the blade stopped.

Offers (`GuardUpgrade.Sets`, each slot shows a random still-available alternative): pick 1 offers both weapons plus Blood Ward / Deep Veins and Crimson Momentum / Unquenched / Overflowing Chalice; later picks offer the held weapon's upgrades (or Death from Above / Seraphic Ascension to a sword guard), the rest of Endurance, and Wrath of Baal / Blade of Sanguinius on one card.

## Built in pass 1 (differences from the draft above)

- **Self-healing fills Vitae at the full rate** (decided after the draft), so he can charge in 1v1 and training.
  His heals (blood stream, Chalice Offering, Parry) all include himself as a target.
- **Damage prevented is not a Vitae source yet**: only healing counts. Tracking prevented damage needs a server
  report; left for later if the healing-only fill feels too slow.
- **Sanguine Parry** no longer heals or ripostes: it fills Vitae to the brim so his next swing is the Arc, which does the healing.
- **Chalice Offering** heals instantly rather than over 2s.
- **Jump Pack** is pure offence: the ally heal and protection were removed.
- The +10% damage taken while full also applies during Wings of the Angel's 5s (both publish the same "empowered" flag).
- Other players see only the full-chalice glow stage; the 33% and 66% stages show on his own screen.

## Open questions

- Vitae fill rate: check that a full chalice takes roughly 4-6s of committed brawling while the team is taking damage.
- Front-line bonus readability: maybe a faint blood mist on him while it's active.

## Implementation notes

- Reuse Grit's "full passive powers the next LMB, then spends it" flow and its pips.
- New: counting effective healing done on allies, and damage prevented, per source.
- Add `Resources/Stats/SanguinaryGuard.json` alongside the abilities (it overrides ability fields at spawn).
