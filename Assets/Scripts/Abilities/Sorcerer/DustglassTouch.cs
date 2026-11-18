using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // R: Dustglass Touch. A sweep of the staff in front of the Sorcerer that hurls enemies back and lays a creeping
    // rubric on them: a second later they are encased in dust-glass (Petrified), unable to move or act, until the
    // shell has soaked enough damage to shatter or the effect runs out. Freeze a diver, or wall an enemy off for a moment;
    // but hitting them breaks them free sooner.
    public class DustglassTouch : Ability
    {
        [SerializeField] float damage = 4f;
        [SerializeField] float range = 3.2f;
        [SerializeField] float arcDegrees = 140f;
        [SerializeField] float windup = 0.3f;
        [SerializeField] float knockback = 2.5f;
        [SerializeField] float knockbackDuration = 0.25f;
        [SerializeField] float petrifyDelay = 1f;
        [SerializeField] float petrifySeconds = 2.7f;
        [SerializeField] float petrifyShield = 20f;
        [SerializeField] float energyPerHit = 5f;

        Energy energy;

        public override string DisplayName => "Dustglass Touch";
        public override string KeyLabel => "R";

        protected override float WindupDuration => windup;
        protected override Color WindupColor => SorcererFx.Glass;

        void Reset()
        {
            cooldown = 15f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R);

        protected override void Activate()
        {
            Vector3 dir = SorcererFx.Flat(Aim.AimDirection, transform);
            Vector3 origin = transform.position;
            Anathema.Abilities.Psyker.PsykerFx.Arc(origin, dir, arcDegrees * 0.5f, range, 0.3f, SorcererFx.Glass);
            Anathema.Abilities.Psyker.PsykerFx.Arc(origin, dir, arcDegrees * 0.5f, range * 0.7f, 0.25f, SorcererFx.Warp);
            MeleeSound.Slash();
            if (IsReplica) return;

            int hits = 0;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team) continue;
                Vector3 to = h.transform.position - origin;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > range + 0.4f) continue;
                if (distance > 0.05f && Vector3.Angle(dir, to) > arcDegrees * 0.5f) continue;

                Vector3 push = distance > 0.05f ? to.normalized : dir;
                Hit touch = Hit.Direct(damage).WithKnockback(push, knockback, knockbackDuration)
                    .With(HitRider.Petrify(petrifySeconds, petrifyShield, petrifyDelay));
                hits++;
                h.Strike(touch, gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(energyPerHit);
                });
            }
            MeleeSound.Swing(hits > 0);
            MeleeSound.Impact(hits);
        }

        protected override void Cancel()
        {
        }
    }
}
