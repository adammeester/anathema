using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities
{
    // Base for character-specific abilities. Each ability is its own component, added to the player only for the
    // character that owns it, and lives in Scripts/Abilities/<Character>/.
    // Pressing C (or dying) calls Cancel() on every ability, so Cancel must be safe to call when nothing is active.
    [RequireComponent(typeof(MouseAim), typeof(Health))]
    public abstract class Ability : MonoBehaviour
    {
        public const KeyCode CancelKey = KeyCode.C;

        [SerializeField] protected float cooldown = 1f;

        protected MouseAim Aim { get; private set; }
        protected Health Health { get; private set; }

        // Set by an ability that has borrowed the left mouse button (e.g. click-to-teleport) so the basic attack stays quiet.
        public static bool PrimaryFireBlocked { get; set; }

        float nextUseTime;

        // Shown on the HUD ability bar.
        public virtual string DisplayName => GetType().Name;
        public virtual string KeyLabel => "";

        // False while something other than the cooldown stops this ability being used (e.g. an ultimate without a
        // full energy bar). Only affects how the HUD draws the slot.
        public virtual bool Available => true;

        // True while this ability holds the character in place (PlayerMovement stops walking; gravity still applies).
        public virtual bool RootsMovement => false;

        public float CooldownRemaining => IsCasting ? 0f : Mathf.Max(0f, nextUseTime - Time.time);
        // 1 = just used, 0 = ready.
        public float CooldownNormalized => cooldown > 0f ? Mathf.Clamp01(CooldownRemaining / cooldown) : 0f;

        protected virtual void Awake()
        {
            Aim = GetComponent<MouseAim>();
            Health = GetComponent<Health>();
        }

        bool awaitRelease;

        // Online: another player's character. Abilities there only replay visuals when the owner's machine says so
        // (ReplayActivate / ReplayCancel); they never read this machine's input or deal damage.
        protected bool IsReplica => Health.IsReplica;

        int abilityIndex = -1;

        // Called on a replica when the owner activated this ability.
        public void ReplayActivate()
        {
            if (!Health.IsDead) Activate();
        }

        public void ReplayCancel()
        {
            Cancel();
        }

        protected virtual void Update()
        {
            if (IsReplica)
            {
                if (Health.IsDead) Cancel();
                return;
            }

            bool cancelPressed = Input.GetKeyDown(CancelKey);
            if (Health.IsDead || cancelPressed) Cancel();
            if (cancelPressed && Health.Net != null && Health.Net.IsSpawned) Health.Net.SendCue(-1, Aim.AimPoint);
            if (Health.IsDead || Anathema.Net.NetGame.Frozen) return;

            bool triggered = IsTriggered();
            // After a refunded cancel, a held key must be released before it can fire again, otherwise
            // cancelling a hold-to-use ability would instantly restart it.
            if (awaitRelease)
            {
                if (triggered) return;
                awaitRelease = false;
            }

            if (Time.time < nextUseTime || !triggered || Silenced.IsSilenced(gameObject) || Stunned.IsStunned(gameObject)) return;

            // Wind-up abilities hold the cooldown back until their effect lands (they call StartCooldown()).
            nextUseTime = CooldownStartsOnStrike ? float.PositiveInfinity : Time.time + cooldown;
            Activate();

            // Tell the other machines to replay this ability's visuals.
            if (Health.Net != null && Health.Net.IsSpawned)
            {
                if (abilityIndex < 0) abilityIndex = System.Array.IndexOf(GetComponents<Ability>(), this);
                Health.Net.SendCue(abilityIndex, Aim.AimPoint);
            }
        }

        // True for abilities with a wind-up: the cooldown only begins when the ability calls StartCooldown().
        protected virtual bool CooldownStartsOnStrike => false;

        // True from pressing the key until the strike lands, for abilities with CooldownStartsOnStrike.
        public bool IsCasting => float.IsPositiveInfinity(nextUseTime);

        // Makes the ability ready right now (e.g. another ability's payoff resets it). Does not interrupt a cast in progress.
        public void ResetCooldown()
        {
            if (!IsCasting) nextUseTime = 0f;
        }

        protected void StartCooldown()
        {
            nextUseTime = Time.time + cooldown;
        }

        // Call from Cancel() when the ability is stopped before its effect lands (e.g. during a wind-up):
        // the cooldown is cleared as if it was never used.
        protected void RefundCooldown()
        {
            nextUseTime = 0f;
            awaitRelease = true;
        }

        protected abstract bool IsTriggered();
        protected abstract void Activate();

        // Stop any animation, effect or window this ability has running. The cooldown is not refunded by default;
        // abilities with a wind-up call RefundCooldown() when cancelled before the effect lands.
        protected abstract void Cancel();
    }
}
