using UnityEngine;

namespace Anathema.Combat
{
    // Passive status: consecutive hits build Rage stacks. Each stack speeds up attacks; stacks fall away all at once if
    // no hit lands within the window. Upgrades can raise the cap, stretch the window, add movement speed per stack or
    // add a flat attack speed bonus (ExtraAttackSpeed). Owner machine only (it follows the hits that machine lands).
    public class Rage : MonoBehaviour
    {
        public const int DefaultMaxStacks = 5;
        public const float DefaultWindow = 2.2f;
        public const float AttackSpeedPerStack = 0.06f;
        public const float FullRageBonusWindow = 0.8f; // extra seconds once stacks are maxed

        int stacks;
        float expireTime;
        Health health;

        // Flat attack speed bonus from other sources (e.g. Berserker's Fury); set by the owner's upgrade effects.
        public float ExtraAttackSpeed { get; set; }
        // Movement speed gained per stack (Relentless Pursuit); 0 when not owned.
        public float MovePerStack { get; set; }

        // Set by the owner's upgrade effects while a transformed weapon is held: the weapon's own passive replaces Rage,
        // so stacks neither build nor count. Flat bonuses (ExtraAttackSpeed) still apply.
        public bool Suppressed { get; set; }

        public int Stacks => !Suppressed && Time.time < expireTime ? stacks : 0;
        public int LastMax { get; private set; } = DefaultMaxStacks;
        public const float ExpiringSeconds = 0.9f; // the last stretch of the window, shown as a pulse

        // Display state, valid on every machine. The owner reads its own stacks; other clients read what the owner
        // published (NetPlayer.RageState: stacks | max << 4 | expiring << 8).
        public int DisplayStacks { get; private set; }
        public int DisplayMax { get; private set; } = DefaultMaxStacks;
        public bool DisplayExpiring { get; private set; }

        public static int NetState(GameObject owner)
        {
            if (!owner.TryGetComponent(out Rage rage)) return 0;
            int s = rage.Stacks;
            if (s <= 0) return 0;
            bool expiring = rage.expireTime - Time.time < ExpiringSeconds;
            return (s & 15) | ((rage.LastMax & 15) << 4) | (expiring ? 1 << 8 : 0);
        }

        void RefreshDisplay()
        {
            if (health != null && health.IsReplica && health.Net != null)
            {
                int state = health.Net.RageState;
                DisplayStacks = state & 15;
                DisplayMax = Mathf.Max(1, (state >> 4) & 15);
                DisplayExpiring = (state & (1 << 8)) != 0;
            }
            else
            {
                DisplayStacks = Stacks;
                DisplayMax = LastMax;
                DisplayExpiring = DisplayStacks > 0 && expireTime - Time.time < ExpiringSeconds;
            }
        }

        public static Rage EnsureOn(GameObject owner)
        {
            return owner.TryGetComponent(out Rage rage) ? rage : owner.AddComponent<Rage>();
        }

        // Register a landed hit.
        public static void Gain(GameObject owner, int maxStacks = DefaultMaxStacks, float window = DefaultWindow)
        {
            Rage rage = EnsureOn(owner);
            if (rage.Suppressed) return;
            rage.LastMax = maxStacks;
            rage.stacks = Mathf.Min(maxStacks, rage.Stacks + 1);
            // At full Rage (the rune is out) the window is longer, and every further hit refreshes it.
            rage.expireTime = Time.time + (rage.stacks >= maxStacks ? window + FullRageBonusWindow : window);
        }

        // 1 + stack and flat attack speed bonuses.
        public static float AttackSpeedMultiplier(GameObject owner)
        {
            // Another player's character: Rage is tracked on its owner's machine, which publishes the bonus.
            if (owner.TryGetComponent(out Health health) && health.IsReplica && health.Net != null)
                return 1f + health.Net.RageAttackBonus;

            return owner.TryGetComponent(out Rage rage) ? 1f + rage.Stacks * AttackSpeedPerStack + rage.ExtraAttackSpeed : 1f;
        }

        public static float MoveSpeedMultiplier(GameObject owner)
        {
            if (!owner.TryGetComponent(out Rage rage)) return 1f;
            // Another player's character (e.g. the server simulating its movement): stacks are tracked on its owner's
            // machine, which publishes them.
            int stacks = owner.TryGetComponent(out Health health) && health.IsReplica && health.Net != null
                ? health.Net.RageState & 15
                : rage.Stacks;
            return 1f + stacks * rage.MovePerStack;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            if (!TryGetComponent(out RageIndicator _)) gameObject.AddComponent<RageIndicator>();
        }

        void Update()
        {
            if (health != null && health.IsDead) stacks = 0;
            RefreshDisplay();
        }
    }
}
