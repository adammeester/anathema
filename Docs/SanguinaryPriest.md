# Sanguinary Priest (design draft)

Blood Angels melee battle-medic. High risk, high reward: he heals his team *through* fighting, and his big swing is
earned by supporting, never by dealing damage.

**Look:** a Blood Angels veteran first, medic second. Gold armour with red trim, a winged jump pack, a long chainsword
(Encarmine-style). The Blood Chalice is his one healing prop, hung at the hip; the narthecium is a small gauntlet detail.

**Stats:** ~2.5 m, 170 HP (below the other marines: his healing only flows while he's in danger), 5.2 move speed.

## Passive: Vitae (the Blood Chalice)

- **Sources (never damage):**
  - Healing done on allies: 1 Vitae per point. Real healing only (no overheal, no self-healing).
  - Damage prevented for allies: 1 per point (Jump Pack Descent's damage reduction, Sanguine Parry's negated hit).
- **Front-line bonus:** Vitae earned while an enemy is within 3 m of him is x1.5.
- **Cap 100.** After 3s without healing or preventing damage, it drains 6/s.
- **At full** the next LMB is **Exsanguinating Arc** and spends the chalice:
  - 0.25s wind-up with a filling ground ring around him.
  - A **360 degree** spin slash, 3.3 m radius, 28 damage, 30% slow for 1s.
  - Every ally within 6 m heals for 40% of the total damage the Arc dealt.
  - Nothing from the Arc refills Vitae.
- **Risk:** while the chalice is full his blade glows for enemies and he takes 10% more damage.
- Pips above the action bar via `IPassiveStacks` (like Faith and Grit).
- No on-kill triggers anywhere in the kit.

## Kit

| Slot | Ability | Spec |
| --- | --- | --- |
| LMB | Chainsword | 2-hit chain, 100 degrees, 2.6 m, 9 damage, 0.7s cadence. Each hit sends a blood stream that heals the most injured ally within 10 m for 5 (nothing when nobody is hurt). Hits give energy. |
| RMB | Sanguine Lunge | 4 m dash-stab, 10 damage; allies within 4 m of the target heal 12. 6s cooldown. |
| Q | Jump Pack Descent | Leap up to 8 m, 10s cooldown, his only mobility. Near an enemy: 10 damage and knockback. On an ally: heal them 12, both take 20% less damage for 1.5s. |
| E | Chalice Offering | Allies within 4 m heal 16 over 2s, +50% per enemy within 4 m of him (max +100%). 12s cooldown. **EX Red Thirst** (35 energy): spend 15 of his own HP to heal the ally nearest the cursor for 30. |
| Space | Sanguine Parry | Counter: the hit is negated and the damage it would have done heals the ally nearest him (counts as damage prevented for Vitae). |
| R | Wings of the Angel | Full energy bar: 0.6s gather, leap-slam at the cursor; 6 m ring heals allies 30 and deals 20 to enemies. For 5s every LMB is an Exsanguinating Arc, on a 0.9s cadence. |

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
and a chainsword roar plus a choir sting. The glow fades over 0.5s afterwards.

## Upgrade sketch (Armament / Endurance / Ferocity)

- **Armament:** Power Axe (the Arc becomes a 4.5 m overhead line instead of a spin), then an Encarmine Sword (Arc radius 3.8 m).
- **Endurance:** Sanctified Blood (Vitae no longer drains).
- **Ferocity:** Brimming (cap 130, Arc damage up); Red Thirst Unbound (overheal counts at half).

## Open questions

- Solo modes (1v1, training arena): he can never charge. Allow self-healing at ~25% there, or accept it?
- Vitae fill rate: check that a full chalice takes roughly 4-6s of committed brawling while the team is taking damage.
- Front-line bonus readability: maybe a faint blood mist on him while it's active.

## Implementation notes

- Reuse Grit's "full passive powers the next LMB, then spends it" flow and its pips.
- New: counting effective healing done on allies, and damage prevented, per source.
- Add `Resources/Stats/SanguinaryPriest.json` alongside the abilities (it overrides ability fields at spawn).
