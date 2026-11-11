using System;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Combat
{
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] int team;

        // Battlerite-style health: damage first becomes "recoverable" (shown as black in the bar). Healing abilities
        // (Heal) can restore it. If it isn't healed for a while it decays into permanent loss (an empty, see-through
        // part of the bar), which only healing orbs (HealPermanent) can restore. Damage beyond maxRecoverableFraction
        // of a few chunks (maxRecoverableChunks) is permanent immediately.
        [SerializeField] float recoverableDecayDelay = 4f;   // seconds after the last hit before white starts decaying
        [SerializeField] float recoverableDecayRate = 6f;    // white health lost to permanent loss per second

        // Health bars are split into chunks; only this many chunks of damage can be healable (black), the rest is unrecoverable.
        public const float ChunkSize = 10f;
        [SerializeField] int maxRecoverableChunks = 3;

        NetPlayer net;
        float lastDamageTime = float.NegativeInfinity;

        public float Max => maxHealth;
        public float Current { get; private set; }
        // The white, healable part sitting just above Current.
        public float Recoverable { get; private set; }
        public float RecoverableNormalized => maxHealth > 0f ? Recoverable / maxHealth : 0f;
        // Health lost for good (only orbs can bring it back).
        public float PermanentLoss => Mathf.Max(0f, maxHealth - Current - Recoverable);
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public int Team => team;
        // Post-round damage report: totals since the last reset (owner/offline only; NetPlayer syncs them to the opponent).
        public float RoundDamageTaken { get; private set; }
        public float RoundHealed { get; private set; }
        public bool IsDead => Current <= 0f;

        // Armour from Endurance upgrades: the fraction of every hit (damage over time included) that is shrugged off.
        // Attacks with `pierce` ignore that share of it. Capped so nothing becomes immune.
        public const float MaxArmor = 0.6f;
        float armor;
        public float Armor
        {
            get => armor;
            set => armor = Mathf.Clamp(value, 0f, MaxArmor);
        }

        // Crowd control (stun, silence, root, panic, maim, slow) lasts this much as long on this character (Unstoppable).
        public float CrowdControlScale { get; set; } = 1f;

        public static float CrowdControlScaleOf(GameObject target)
        {
            return target != null && target.TryGetComponent(out Health h) ? h.CrowdControlScale : 1f;
        }

        // A short burst of extra armour (Feel No Pain), on top of the permanent kind.
        float tempArmor;
        float tempArmorEnd;

        public void GrantTemporaryArmor(float fraction, float seconds)
        {
            tempArmor = Mathf.Max(Time.time < tempArmorEnd ? tempArmor : 0f, fraction);
            tempArmorEnd = Mathf.Max(tempArmorEnd, Time.time + seconds);
        }

        public float EffectiveArmor => Mathf.Min(MaxArmor, armor + (Time.time < tempArmorEnd ? tempArmor : 0f));

        // Extra resistance to area damage and damage over time only (Disgustingly Resilient), on top of armour.
        float areaResistance;
        public float AreaResistance
        {
            get => areaResistance;
            set => areaResistance = Mathf.Clamp(value, 0f, MaxArmor);
        }

        // A short window in which damage over time is reduced (Field Triage). Authority only; refreshed while it lasts.
        float dotWardFraction;
        float dotWardEnd;

        public void GrantDotWard(float fraction, float seconds)
        {
            dotWardFraction = Mathf.Max(Time.time < dotWardEnd ? dotWardFraction : 0f, Mathf.Clamp01(fraction));
            dotWardEnd = Mathf.Max(dotWardEnd, Time.time + seconds);
        }

        // What the last hit resolved here was (set just before Damaged is raised), so listeners can tell an attack from
        // area damage or a damage-over-time tick.
        public bool LastHitAoe { get; private set; }
        public bool LastHitDot { get; private set; }

        // Martyr's Resolve (Paladin Order): once per round a lethal hit is survived at 1 health, followed by brief immunity.
        public const float LastStandSeconds = 2f;
        public bool LastStand { get; set; }
        bool lastStandUsed;
        float lastStandUntil = float.NegativeInfinity;

        // Extra maximum health from upgrades. Set absolutely (not added), so re-applying it is harmless.
        float bonusMaxHealth;

        // Online play: the owning machine is authoritative for this character. Everywhere else it is a "replica":
        // it follows synced state and only replays the owner's ability visuals (no damage, no input).
        public NetPlayer Net => net;
        public bool IsReplica => net != null && net.IsSpawned && !net.IsOwner;
        public bool IsMine => !IsReplica;

        // The machine whose copy of this character is the truth for health, shields and damage: the server (dedicated
        // or host) when online, this machine otherwise. Everywhere else the numbers are copied from the server.
        // (Movement, aim and the enforcement of stuns and roots stay with the owner: see IsReplica.)
        public bool IsAuthority => StrikeRelay == null && (net == null || !net.IsSpawned || net.IsServer);

        // A neutral object rather than a character (the arena's charge orb): only a character's attacks hurt it (no
        // gas, bleeds or burns), status effects never take hold, it cannot be shoved or healed, and none of its
        // lost health is healable.
        public bool Structure { get; set; }

        // Set on a client's copy of a server-owned object that has no NetPlayer (the charge orb): this copy is then
        // not the authority, and attacks on it are handed to this to pass on to the server.
        public Action<Hit, GameObject, Action<bool>> StrikeRelay;

        // Lets an ability (e.g. a counter) rewrite or negate incoming damage. Receives (amount, source); returns the new amount.
        public Func<float, GameObject, float> DamageModifier;

        // A shield: soaks up incoming damage (AoE and damage over time included, unlike DamageModifier) and returns
        // whatever is left over to go through to health. Receives (amount, source).
        public Func<float, GameObject, float> Absorber;

        // A second, independent shield slot (e.g. the Psyker's teleport shield) so it never fights over `Absorber`
        // with an ability shield. It soaks first; whatever it leaves goes on to `Absorber`.
        public Func<float, GameObject, float> BonusAbsorber;

        // A third shield slot for a short generic barrier (see Barrier: Overdose, Chitinous Wings). It soaks first.
        public Func<float, GameObject, float> BarrierAbsorber;

        // Each passes part of every hit (after armour, before shields) to someone else, in the order added. Receives
        // (amount, source); returns the amount this character still takes. Added by the Hospitaller's Sanctified Tether
        // and Martyrdom on allies; runs on the authority.
        readonly System.Collections.Generic.List<Func<float, GameObject, float>> damageSharers =
            new System.Collections.Generic.List<Func<float, GameObject, float>>();

        public void AddDamageSharer(Func<float, GameObject, float> sharer)
        {
            if (sharer != null && !damageSharers.Contains(sharer)) damageSharers.Add(sharer);
        }

        public void RemoveDamageSharer(Func<float, GameObject, float> sharer) => damageSharers.Remove(sharer);

        // I-frames: while set, nothing hurts this character (including AoE, gas and bleeds, which skip DamageModifier).
        public bool Invulnerable { get; set; }

        public event Action<Health, float, GameObject> Damaged;
        public event Action<Health, GameObject> Died;
        // Raised on the victim's machine with the health actually lost (after modifiers/absorbs), for damage-report credit.
        public event Action<float, GameObject> DamageTaken;

        void Awake()
        {
            net = GetComponent<NetPlayer>();
            Current = maxHealth;
        }

        public void SetTeam(int teamId)
        {
            team = teamId;
        }

        public void Configure(float max, int teamId)
        {
            maxHealth = max;
            team = teamId;
            Current = max;
            Recoverable = 0f;
        }

        void Update()
        {
            // Authority only; everyone else copies the server's numbers.
            if (!IsAuthority || Recoverable <= 0f || IsDead) return;
            if (Time.time - lastDamageTime < recoverableDecayDelay) return;
            Recoverable = Mathf.Max(0f, Recoverable - recoverableDecayRate * Time.deltaTime);
        }

        public void SetBonusMaxHealth(float bonus)
        {
            float delta = bonus - bonusMaxHealth;
            if (Mathf.Approximately(delta, 0f)) return;

            bonusMaxHealth = bonus;
            maxHealth += delta;
            // Gaining max health also grants that much current health; a fighter who is down stays down.
            if (!IsDead) Current = Mathf.Clamp(Current + delta, 1f, maxHealth);
        }

        // Entry point for everything that hurts a character. Online it decides where the damage is actually applied.
        // `pierce` (0-1) is the share of the victim's armour this hit ignores; `shieldBreak` multiplies how much of a
        // shield (Absorber) the hit uses up (1.5 = it burns through shields 50% faster).
        // `dot` marks a damage-over-time tick (bleed, burn, gas, plague): it never breaks panic.
        // Server-side damage that is not an attack: bleed and burn ticks, the gas. Only the authority applies it; every
        // other machine ignores it (its copy of the effect is visual only). Attacks use Strike.
        public bool TakeDamage(float amount, GameObject source, bool aoe = false, float pierce = 0f, float shieldBreak = 1f, bool dot = false)
        {
            if (IsDead || amount <= 0f || !IsAuthority) return false;
            return ApplyDamageLocal(amount, source, aoe, pierce, shieldBreak, dot);
        }

        // The way an attack hurts someone: damage and its status effects as one unit. `onResult` (optional) is told
        // whether the hit actually landed (false if it was countered, dodged, blocked or illegal), for hit markers and
        // energy gain. On the authority (the server, or offline) it is resolved straight away; anywhere else it is sent
        // to the server, which validates and resolves it against its own copy of the target.
        // `trusted`: the server's own effects (e.g. a ward's rupture) whose source is another player's character.
        public void Strike(Hit hit, GameObject source, Action<bool> onResult = null, bool trusted = false)
        {
            if (IsDead || hit.Damage <= 0f)
            {
                onResult?.Invoke(false);
                return;
            }

            // Another player's character only replays visuals on this machine (its projectiles fly here too); the real
            // hit comes from that player's own machine.
            if (!trusted && NetPlayer.IsReplicaObject(source)) return;

            // Hitting a structure (the charge orb) builds no energy: the attacker's on-hit gains all run inside this
            // callback, so they are switched off while it runs. Only the kill pays out (see ChargeOrb).
            if (Structure && onResult != null)
            {
                Action<bool> inner = onResult;
                onResult = landed =>
                {
                    Energy.Suppressed++;
                    try { inner(landed); }
                    finally { Energy.Suppressed--; }
                };
            }

            if (!IsAuthority)
            {
                // Environmental damage (gas) is the server's business.
                if (source != null && !source.TryGetComponent(out NetPlayer _)) return;
                if (StrikeRelay != null) StrikeRelay(hit, source, onResult);
                else net.SendHit(hit, source, onResult);
                return;
            }

            onResult?.Invoke(ApplyHitLocal(hit, source));
        }

        // On the authority: resolve the damage (counters, shields, armour...), then apply the riders if it landed and
        // left the victim standing.
        public bool ApplyHitLocal(Hit hit, GameObject source)
        {
            if (!ApplyDamageLocal(hit.Damage, source, hit.Aoe, hit.Pierce, hit.ShieldBreak, hit.Dot)) return false;
            // A landed hit from whoever lit this character's balefire sets it off (before this hit's own riders, so the
            // hit that lights it doesn't burst it).
            if (!IsDead && !hit.Dot) Balefire.OnStruck(gameObject, source);
            if (!IsDead && !Structure) hit.ApplyRiders(gameObject, source);
            return true;
        }

        // A single non-DoT hit that costs at least this share of max health snaps the victim out of panic.
        public const float PanicBreakFraction = 0.05f;

        // Applies damage to this machine's copy (counters and other modifiers run here). Authority only.
        public bool ApplyDamageLocal(float amount, GameObject source, bool aoe = false, float pierce = 0f, float shieldBreak = 1f, bool dot = false)
        {
            if (IsDead || Invulnerable || Time.time < lastStandUntil || amount <= 0f || NetGame.Frozen) return false;
            if (Structure && (dot || source == null || !source.TryGetComponent(out Health _))) return false;

            if (!aoe && DamageModifier != null) amount = DamageModifier(amount, source);
            if (amount <= 0f) return false;

            pierce = Mathf.Clamp01(pierce);
            float armour = EffectiveArmor;
            if (armour > 0f) amount *= 1f - armour * (1f - pierce);
            if ((aoe || dot) && areaResistance > 0f) amount *= 1f - areaResistance;
            if (dot && Time.time < dotWardEnd) amount *= 1f - dotWardFraction;

            // Shares of the hit carried by someone else (the Hospitaller); each returns what is left for this one.
            for (int i = 0; i < damageSharers.Count && amount > 0f; i++) amount = damageSharers[i](amount, source);
            if (amount <= 0f) return true;

            // A fully absorbed hit still counts as landing (the attacker gets their energy) but doesn't touch health.
            // A shield-breaking hit uses up the shield faster: the shield sees the hit scaled up, and whatever it could not
            // absorb is scaled back down before it reaches health.
            if (BarrierAbsorber != null || BonusAbsorber != null || Absorber != null)
            {
                float breaker = Mathf.Max(1f, shieldBreak);
                amount *= breaker;
                if (BarrierAbsorber != null) amount = BarrierAbsorber(amount, source);
                if (amount > 0f && BonusAbsorber != null) amount = BonusAbsorber(amount, source);
                if (amount > 0f && Absorber != null) amount = Absorber(amount, source);
                amount /= breaker;
                if (amount <= 0f) return true;
            }

            float dealt = Mathf.Min(amount, Current);
            if (LastStand && !lastStandUsed && amount >= Current)
            {
                // Martyr's Resolve: the first lethal hit each round leaves one health and a moment of immunity.
                dealt = Mathf.Max(0f, Current - 1f);
                lastStandUsed = true;
                lastStandUntil = Time.time + LastStandSeconds;
            }
            Current -= dealt;
            RoundDamageTaken += dealt;
            DamageTaken?.Invoke(dealt, source);
            // Only so much damage can ever be healed back; anything beyond that is lost for good (orbs only).
            Recoverable = Structure ? 0f : Mathf.Min(Recoverable + dealt, maxHealth - Current, maxRecoverableChunks * ChunkSize);
            lastDamageTime = Time.time;
            if (Current <= 0f) Recoverable = 0f;
            if (!dot && dealt >= maxHealth * PanicBreakFraction) Panicked.Cancel(gameObject);

            LastHitAoe = aoe;
            LastHitDot = dot;
            Damaged?.Invoke(this, amount, source);
            if (Current <= 0f) Died?.Invoke(this, source);
            return true;
        }

        // Every machine but the authority copies the server's health; a drop to zero still raises Died so local hooks
        // (hiding, cancelling abilities) run.
        public void SyncFromNetwork(float value, float recoverable, float damageTaken, float healed)
        {
            bool wasDead = IsDead;
            Current = Mathf.Clamp(value, 0f, maxHealth);
            Recoverable = Mathf.Clamp(recoverable, 0f, maxHealth - Current);
            RoundDamageTaken = damageTaken;
            RoundHealed = healed;
            if (!wasDead && IsDead) Died?.Invoke(this, null);
        }

        // Raised on the authority with the health actually restored, for the damage report's healing credit.
        public event Action<float, GameObject> Healed;

        // Raised on the authority for every heal that reaches a living character: (target, amount asked for, amount
        // actually restored, healer). The difference is overhealing (e.g. Overdose turns it into a barrier).
        public static event Action<Health, float, float, GameObject> HealResolved;

        // Healing abilities: restores recoverable (white) health only. Returns how much was actually healed (0 when
        // the heal had to be sent to the server, which applies and credits it). `source` is the healer, for credit.
        public float Heal(float amount, GameObject source = null)
        {
            if (IsDead || Structure || amount <= 0f) return 0f;
            if (!IsAuthority)
            {
                net.SendHeal(amount, false, source);
                return 0f;
            }

            // Poison sours healing (orbs, below, are unaffected).
            amount *= Poisoned.HealingMultiplier(gameObject);
            float healed = Mathf.Min(amount, Recoverable);
            Current += healed;
            Recoverable -= healed;
            RoundHealed += healed;
            if (healed > 0f) Healed?.Invoke(healed, source);
            HealResolved?.Invoke(this, amount, healed, source);
            return healed;
        }

        // Healing orbs: restore any lost health. Permanently lost health goes first (nothing else can restore it),
        // then the healable (black) part. Returns how much was actually healed (0 when sent to the server).
        public float HealPermanent(float amount, GameObject source = null)
        {
            if (IsDead || Structure || amount <= 0f) return 0f;
            if (!IsAuthority)
            {
                net.SendHeal(amount, true, source);
                return 0f;
            }

            float healed = Mathf.Min(amount, maxHealth - Current);
            float permanent = PermanentLoss;
            Current += healed;
            RoundHealed += healed;
            // Whatever the orb healed beyond the permanent loss came out of the healable part.
            Recoverable = Mathf.Max(0f, Recoverable - Mathf.Max(0f, healed - permanent));
            if (healed > 0f) Healed?.Invoke(healed, source);
            return healed;
        }

        public void ResetHealth()
        {
            RoundDamageTaken = 0f;
            RoundHealed = 0f;
            Current = maxHealth;
            Recoverable = 0f;
            lastStandUsed = false;
            lastStandUntil = float.NegativeInfinity;
        }
    }
}
