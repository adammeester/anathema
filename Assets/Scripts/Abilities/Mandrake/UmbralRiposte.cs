using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // Space: Umbral Riposte, the Mandrake's counter. Press to raise a brief guard. If an enemy hits the Mandrake during it, the hit is negated, the Mandrake vanishes into stealth and steps
    // out of the attacker's shadow, right behind them (if they are close enough). The next strike within a couple of
    // seconds counts as a strike from stealth even after the vanish ends: Shadow Rend gets its bonus damage and
    // poison, and the blade its stun.
    // A guard that catches nothing has a short cooldown; a successful riposte a long one.
    // The guard and negation run through Counter (the server decides online); the vanish, step and primed strike are
    // the owner's, and the server allows the step (OnServerCounterTriggered).
    [DefaultExecutionOrder(100)] // keep in step with Counter
    public class UmbralRiposte : Counter
    {
        [SerializeField] float guardDuration = 0.6f;
        [SerializeField] float successCooldown = 12f;
        [SerializeField] float vanishDuration = 1f;
        [SerializeField] float shadowStrikeWindow = 2f;
        [Tooltip("How far the Mandrake can be from the attacker and still step behind them.")]
        [SerializeField] float stepReach = 8f;
        [SerializeField] float behindDistance = 1.3f;

        static readonly Color Guard = new Color(MandrakeKit.Baleflame.r, MandrakeKit.Baleflame.g, MandrakeKit.Baleflame.b, 0.4f);
        static readonly Color Glow = new Color(0.1f, 0.3f, 0.15f, 0.45f);

        CharacterController controller;
        float shadowStrikeUntil = float.NegativeInfinity;

        public override string DisplayName => "Umbral Riposte";
        protected override float GuardDuration => guardDuration;
        protected override Color GuardColor => Guard;
        protected override Color GlowColor => Glow;

        void Reset()
        {
            cooldown = 6f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
        }

        // Owner, from MandrakeKit.BeginStrike: true (once) if this strike is the one primed by a riposte.
        public bool ConsumeShadowStrike()
        {
            if (Time.time >= shadowStrikeUntil) return false;
            shadowStrikeUntil = float.NegativeInfinity;
            return true;
        }

        // Server: the owner is about to step behind the attacker.
        protected override void OnServerCounterTriggered()
        {
            var movement = GetComponent<Anathema.Player.PlayerMovement>();
            if (movement != null) movement.GrantAbilityMove(stepReach + behindDistance + 1f, 1f);
        }

        protected override void OnCounterSuccess(GameObject attacker)
        {
            SetCooldownRemaining(successCooldown);
            if (Health.IsDead) return;

            Vector3 from = transform.position;
            SlashEffect.Spawn(from, MandrakeKit.FlatAim(Aim.AimDirection, transform), 1.6f, 360f, 0.3f, MandrakeKit.Shadow);
            MeleeSound.Slash(0.45f, 0.7f, 0.8f);

            // Vanish first so enemies never see where it steps to.
            Stealthed.Apply(gameObject, vanishDuration);
            shadowStrikeUntil = Time.time + shadowStrikeWindow;

            if (attacker == null) return;
            Health target = attacker.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.Team == Health.Team) return;

            Vector3 behind = BehindSpot(target.transform, out Vector3 faceDir);
            if (behind == from) return;

            if (controller != null) controller.enabled = false;
            transform.SetPositionAndRotation(behind, Quaternion.LookRotation(faceDir));
            if (controller != null) controller.enabled = true;
            AbilityFx.Teleport(from, behind, MandrakeKit.Baleflame); // only drawn here: the Mandrake is already hidden elsewhere
        }

        // A clear spot just behind the attacker (opposite the way it faces), or the Mandrake's own position if the
        // attacker is out of reach or there is nowhere to stand.
        Vector3 BehindSpot(Transform attacker, out Vector3 faceDir)
        {
            Vector3 from = transform.position;
            Vector3 toAttacker = attacker.position - from;
            toAttacker.y = 0f;
            faceDir = toAttacker.sqrMagnitude > 0.0001f ? toAttacker.normalized : transform.forward;
            if (toAttacker.magnitude > stepReach) return from;

            Vector3 back = -attacker.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.0001f ? back.normalized : faceDir;
            float distance = LandingSpot.Clear(transform, attacker.position, back, behindDistance);
            if (distance < 0.5f) return from;

            Vector3 spot = attacker.position + back * distance;
            spot.y = from.y;
            faceDir = -back;
            return spot;
        }
    }
}
