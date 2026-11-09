using UnityEngine;

namespace Anathema.Combat
{
    // Passive status: consecutive hits build Rage stacks. Each stack speeds up attacks; stacks fall away all at once if
    // no hit lands within the window. Upgrades can raise the cap, stretch the window, add movement speed per stack or
    // add a flat attack speed bonus (ExtraAttackSpeed). Owner machine only (it follows the hits that machine lands).
    public class Rage : MonoBehaviour
    {
        public const int DefaultMaxStacks = 5;
        public const float DefaultWindow = 3f;
        public const float AttackSpeedPerStack = 0.06f;

        int stacks;
        float expireTime;
        Health health;

        // Flat attack speed bonus from other sources (e.g. Berserker's Fury); set by the owner's upgrade effects.
        public float ExtraAttackSpeed { get; set; }
        // Movement speed gained per stack (Relentless Pursuit); 0 when not owned.
        public float MovePerStack { get; set; }

        public int Stacks => Time.time < expireTime ? stacks : 0;
        public int LastMax { get; private set; } = DefaultMaxStacks;

        public static Rage EnsureOn(GameObject owner)
        {
            return owner.TryGetComponent(out Rage rage) ? rage : owner.AddComponent<Rage>();
        }

        // Register a landed hit.
        public static void Gain(GameObject owner, int maxStacks = DefaultMaxStacks, float window = DefaultWindow)
        {
            Rage rage = EnsureOn(owner);
            rage.LastMax = maxStacks;
            rage.stacks = Mathf.Min(maxStacks, rage.Stacks + 1);
            rage.expireTime = Time.time + window;
        }

        // 1 + stack and flat attack speed bonuses.
        public static float AttackSpeedMultiplier(GameObject owner)
        {
            return owner.TryGetComponent(out Rage rage) ? 1f + rage.Stacks * AttackSpeedPerStack + rage.ExtraAttackSpeed : 1f;
        }

        public static float MoveSpeedMultiplier(GameObject owner)
        {
            return owner.TryGetComponent(out Rage rage) ? 1f + rage.Stacks * rage.MovePerStack : 1f;
        }

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void Update()
        {
            if (health != null && health.IsDead) stacks = 0;
        }
    }
}
