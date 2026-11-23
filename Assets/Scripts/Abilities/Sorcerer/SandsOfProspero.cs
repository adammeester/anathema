using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // LMB: Sands of Prospero. Hold to hurl bolts of enchanted sand. Each hit lays the Curse of the Rubricae: a time bomb that
    // bursts for extra damage a few seconds later (see RubricMark). Several of the Sorcerer's abilities set it off early.
    //
    // EX (Shift+LMB): Bolt of Change. A faster, longer-reaching bolt that hits harder, knocks the target back and sets
    // off their Curse of the Rubricae at once. It has a cooldown of its own.
    public class SandsOfProspero : Ability
    {
        [SerializeField] float damage = 10f;
        [SerializeField] float range = 13f;
        [SerializeField] float projectileSpeed = 22f;
        [SerializeField] float projectileRadius = 0.3f;
        [SerializeField] float windup = 0.3f;
        [SerializeField] float energyPerHit = 7f;
        [SerializeField] float markDamage = 10f;
        [SerializeField] float markSeconds = 3.5f;

        [Header("EX: Bolt of Change")]
        [SerializeField] float exEnergyCost = 25f;
        [SerializeField] float exCooldown = 5f;
        [SerializeField] float exDamage = 18f;
        [SerializeField] float exRange = 17f;
        [SerializeField] float exProjectileSpeed = 34f;
        [SerializeField] float exKnockback = 2.5f;
        [SerializeField] float exKnockbackDuration = 0.25f;

        const float MuzzleHeight = 1.6f;
        const float MuzzleForward = 0.7f;

        Energy energy;
        float exReadyAt;

        public override string DisplayName => "Sands of Prospero";
        public override string KeyLabel => "LMB";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Bolt of Change";
        public override string ExDescription => "Bolt of Change";
        protected override bool ExReady => Time.time >= exReadyAt;

        protected override float WindupDuration => windup;
        protected override Color WindupColor => SorcererFx.Sand;

        void Reset()
        {
            cooldown = 0.55f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetMouseButton(0) && !PrimaryFireBlocked;

        protected override void OnWindupStart()
        {
            // The free (non-staff) hand draws back for the throw, on the owner and replicas alike.
            SorcererPose.For(gameObject).Raise(SorcererPose.Hand.Free, windup);
            if (IsReplica) return;
            ExActivation = Time.time >= exReadyAt && TrySpendEx(energy, exEnergyCost);
            if (ExActivation) exReadyAt = Time.time + exCooldown;
        }

        protected override void OnWindupCancel()
        {
            SorcererPose.For(gameObject).Drop(SorcererPose.Hand.Free);
            if (IsReplica || !ExActivation) return;
            if (energy != null) energy.Gain(exEnergyCost);
            exReadyAt = 0f;
        }

        protected override void Activate()
        {
            Vector3 dir = SorcererFx.Flat(Aim.AimDirection, transform);
            SorcererPose pose = SorcererPose.For(gameObject);
            // Thrown from the free hand's side, flinging it out along the aim.
            Vector3 origin = transform.position + Vector3.up * MuzzleHeight + dir * MuzzleForward + pose.SideOffset(SorcererPose.Hand.Free);
            pose.Strike(SorcererPose.Hand.Free, transform.position + dir * 5f, 0.05f);

            if (ExActivation)
            {
                GameObject bolt = SorcererFx.Bolt("Bolt of Change", origin, projectileRadius * 1.25f, SorcererFx.Warp, SorcererFx.Sand);
                Sfx.Play(Sfx.PsycannonShot, 0.4f, 1.2f, 1.35f, 0.5f);
                Hit riders = Hit.Direct(0f).With(HitRider.RubricBurst()).WithKnockback(dir, exKnockback, exKnockbackDuration);
                bolt.AddComponent<Projectile>().Init(gameObject, Health.Team, exDamage, exProjectileSpeed, exRange, projectileRadius, dir, OnExHit)
                    .WithRiders(riders);
                return;
            }

            GameObject sand = SorcererFx.Bolt("Sands of Prospero", origin, projectileRadius, SorcererFx.Sand, SorcererFx.Warp);
            Sfx.Play(Sfx.Any(Sfx.MagicAttack1, Sfx.MagicAttack3), 0.32f, 0.85f, 0.95f, 0.3f);
            sand.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit)
                .WithRiders(Hit.Direct(0f).With(HitRider.RubricCurse(markDamage, markSeconds)));
        }

        void OnHit(Health target)
        {
            HitMarkerSound.Play();
            Sfx.Play(Sfx.MagicImpact, 0.28f, 1.1f, 1.25f, 0.6f);
            if (energy != null) energy.Gain(energyPerHit);
        }

        void OnExHit(Health target)
        {
            HitMarkerSound.Play();
            Sfx.Play(Sfx.MagicImpact, 0.4f, 0.8f, 0.9f, 0.6f);
        }

        protected override void Cancel()
        {
        }
    }
}
