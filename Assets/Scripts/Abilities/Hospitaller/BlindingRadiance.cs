using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Q: Blinding Radiance. She thrusts the icon forward and it flares: every enemy in a short frontal cone is burned a
    // little and hurled back. Her way of peeling a melee attacker off herself or an ally. C during the wind-up cancels it.
    public class BlindingRadiance : Ability
    {
        [SerializeField] float range = 4f;
        [SerializeField] float arc = 110f;
        [SerializeField] float damage = 10f;
        [SerializeField] float knockbackDistance = 3.5f;
        [SerializeField] float knockbackDuration = 0.3f;
        [SerializeField] float energyPerHit = 8f;

        static readonly Color Gold = new Color(1f, 0.85f, 0.4f, 0.9f);

        Energy energy;

        public override string DisplayName => "Blinding Radiance";
        public override string KeyLabel => "Q";

        protected override float WindupDuration => 0.25f;
        protected override Color WindupColor => Gold;

        protected override void OnWindupStart() => HospitallerPose.For(gameObject).Point(transform.position + Aim.AimDirection * 3f + Vector3.up * 1.4f, WindupDuration + 0.2f);

        protected override void OnWindupCancel() => HospitallerPose.For(gameObject).Release();

        void Reset()
        {
            cooldown = 8f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.Q);
        }

        protected override void Activate()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

            HospitallerPose.For(gameObject).Point(transform.position + dir * 3f + Vector3.up * 1.4f, 0.35f);
            HospitallerFx.Burst(transform.position + dir * (range * 0.5f), range * 0.6f, Gold, 0.35f, 1.2f);
            Sfx.Play(Sfx.MagicAttack3, 0.4f, 1.1f, 1.2f, 0.8f);
            if (IsReplica) return;

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team) continue;
                Vector3 to = h.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude > range || Vector3.Angle(dir, to) > arc * 0.5f) continue;

                Vector3 push = to.sqrMagnitude > 0.01f ? to.normalized : dir;
                h.Strike(Hit.Direct(BlessedSuffering.Scale(Health, damage)).WithKnockback(push, knockbackDistance, knockbackDuration), gameObject, landed =>
                {
                    if (!landed) return;
                    HitMarkerSound.Play();
                    if (energy != null) energy.Gain(energyPerHit);
                });
            }
        }

        protected override void Cancel()
        {
        }
    }
}
