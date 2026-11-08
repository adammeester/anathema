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
        public bool IsDead => Current <= 0f;

        // Online play: the owning machine is authoritative for this character. Everywhere else it is a "replica":
        // it follows synced state and only replays the owner's ability visuals (no damage, no input).
        public NetPlayer Net => net;
        public bool IsReplica => net != null && net.IsSpawned && !net.IsOwner;
        public bool IsMine => !IsReplica;

        // Lets an ability (e.g. a counter) rewrite or negate incoming damage. Receives (amount, source); returns the new amount.
        public Func<float, GameObject, float> DamageModifier;

        public event Action<Health, float, GameObject> Damaged;
        public event Action<Health, GameObject> Died;

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
            // Owner/offline only; replicas copy the owner's numbers.
            if (IsReplica || Recoverable <= 0f || IsDead) return;
            if (Time.time - lastDamageTime < recoverableDecayDelay) return;
            Recoverable = Mathf.Max(0f, Recoverable - recoverableDecayRate * Time.deltaTime);
        }

        // Entry point for everything that hurts a character. Online it decides where the damage is actually applied.
        public bool TakeDamage(float amount, GameObject source)
        {
            if (IsDead || amount <= 0f) return false;

            if (net != null && net.IsSpawned)
            {
                // A replica of the attacker is only replaying visuals; the real hit comes from the attacker's machine.
                if (NetPlayer.IsReplicaObject(source)) return false;

                if (!net.IsOwner)
                {
                    // Environmental damage (gas) is applied by the victim's own machine, so ignore it here.
                    if (source != null && !source.TryGetComponent(out NetPlayer _)) return false;
                    net.SendDamage(amount, source);
                    return true;
                }
            }

            return ApplyDamageLocal(amount, source);
        }

        // Applies damage to this machine's copy (counters and other modifiers run here).
        public bool ApplyDamageLocal(float amount, GameObject source)
        {
            if (IsDead || amount <= 0f) return false;

            if (DamageModifier != null) amount = DamageModifier(amount, source);
            if (amount <= 0f) return false;

            float dealt = Mathf.Min(amount, Current);
            Current -= dealt;
            // Only so much damage can ever be healed back; anything beyond that is lost for good (orbs only).
            Recoverable = Mathf.Min(Recoverable + dealt, maxHealth - Current, maxRecoverableChunks * ChunkSize);
            lastDamageTime = Time.time;
            if (Current <= 0f) Recoverable = 0f;

            Damaged?.Invoke(this, amount, source);
            if (Current <= 0f) Died?.Invoke(this, source);
            return true;
        }

        // Replicas copy the owner's health; a drop to zero still raises Died so local hooks (hiding, cancelling) run.
        public void SyncFromNetwork(float value, float recoverable)
        {
            bool wasDead = IsDead;
            Current = Mathf.Clamp(value, 0f, maxHealth);
            Recoverable = Mathf.Clamp(recoverable, 0f, maxHealth - Current);
            if (!wasDead && IsDead) Died?.Invoke(this, null);
        }

        // Healing abilities: restores recoverable (white) health only. Returns how much was actually healed.
        public float Heal(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;
            float healed = Mathf.Min(amount, Recoverable);
            Current += healed;
            Recoverable -= healed;
            return healed;
        }

        // Healing orbs: restore any lost health. Permanently lost health goes first (nothing else can restore it),
        // then the healable (black) part. Returns how much was actually healed.
        public float HealPermanent(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;
            float healed = Mathf.Min(amount, maxHealth - Current);
            float permanent = PermanentLoss;
            Current += healed;
            // Whatever the orb healed beyond the permanent loss came out of the healable part.
            Recoverable = Mathf.Max(0f, Recoverable - Mathf.Max(0f, healed - permanent));
            return healed;
        }

        public void ResetHealth()
        {
            Current = maxHealth;
            Recoverable = 0f;
        }
    }
}
