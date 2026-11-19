using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // R: Pestilent Burst. The surgeon looses a bloated fly along his aim that never latches on: it bursts the moment it
    // touches anything (a character or a wall) or reaches its maximum range, and every enemy around that spot is bitten
    // and panicked. His peel: it sends a diver running off his backline. Panic breaks on a solid hit, but damage over
    // time doesn't break it, so Festering keeps ticking on whoever runs. C during the wind-up cancels it.
    //
    // Online: every machine replays the fly and the burst from the cue; only the owner's copy deals the damage and panic.
    public class PestilentBurst : Ability
    {
        [SerializeField] float projectileSpeed = 18f;
        [SerializeField] float range = 13f;
        [SerializeField] float projectileRadius = 0.45f;
        [SerializeField] float muzzleHeight = 1.1f;
        [SerializeField] float burstRadius = 3.5f;
        [SerializeField] float burstDamage = 12f;
        [SerializeField] float burstPanic = 1.25f;

        protected override float WindupDuration => 0.3f;
        protected override Color WindupColor => new Color(0.65f, 0.75f, 0.15f, 1f);

        public override string DisplayName => "Pestilent Burst";
        public override string KeyLabel => "R";

        void Reset()
        {
            cooldown = 15f;
        }

        protected override void OnWindupStart() => PlagueSurgeonPose.For(gameObject).ReadyThrow(WindupDuration + 0.1f);

        protected override void OnWindupCancel() => PlagueSurgeonPose.For(gameObject).Release();

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R);

        protected override void Activate()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            PlagueSurgeonPose.For(gameObject).Throw();

            bool visualOnly = IsReplica;
            Debug.Log($"[PestilentBurst DEBUG] launched from {transform.position} dir {dir} replica={visualOnly}");
            PlagueFlyProjectile.Launch(gameObject, transform.position + Vector3.up * muzzleHeight + dir * 0.7f, dir,
                projectileSpeed, range, projectileRadius, 1.5f, 0.85f, visualOnly, null,
                (target, at) =>
                {
                    // Burst on the ground plane so the dome sits where the characters stand. No healing: his cloud heals.
                    at.y = transform.position.y;
                    // TEMPORARY: where it burst, what it touched, and who was close enough to be panicked.
                    string inRange = "";
                    foreach (Health h in FindObjectsByType<Health>())
                    {
                        Vector3 d = h.transform.position - at;
                        d.y = 0f;
                        inRange += $" {h.name}(team {h.Team}, {d.magnitude:0.0}m{(h.Team == Health.Team ? ", ally" : "")})";
                    }
                    Debug.Log($"[PestilentBurst DEBUG] burst at {at}, touched {(target != null ? target.name : "nothing (wall or max range)")}, " +
                              $"my team {Health.Team}, radius {burstRadius}. Everyone:{inRange}");
                    Infestation.Detonate(at, gameObject, Health.Team, burstRadius, burstDamage, 0f, burstPanic, visualOnly);
                });
        }

        protected override void Cancel()
        {
            // Nothing to cancel once the fly has left (a wind-up in progress is cancelled by the base class).
        }
    }
}
