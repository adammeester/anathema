using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryPriest
{
    // RMB: Sanguine Lunge. A short dash along the aim that ends in a narrow stab. If it lands, the blood is shared:
    // every teammate within `allyHealRadius` of the target (himself included) is healed. Rewards diving onto the enemy
    // a teammate is already fighting. C during the dash cancels it (the stab never lands).
    [DefaultExecutionOrder(50)]
    public class SanguineLunge : Ability
    {
        [SerializeField] float dashDistance = 4f;
        [SerializeField] float dashDuration = 0.16f;
        [SerializeField] float damage = 10f;
        [SerializeField] float range = 2.4f;
        [SerializeField] float arcDegrees = 60f;
        [SerializeField] float allyHeal = 12f;
        [SerializeField] float allyHealRadius = 4f;
        [SerializeField] float energyPerHit = 8f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_R" };

        CharacterController controller;
        Energy energy;
        LimbPose pose;
        bool active;
        bool posed;
        Vector3 direction;
        float dashEnd;
        float poseEnd;

        protected override float WindupDuration => 0.1f;
        protected override Color WindupColor => SanguinaryKit.Blood;

        public override string DisplayName => "Sanguine Lunge";
        public override string KeyLabel => "RMB";
        public override float MovementRange => dashDistance;

        void Reset()
        {
            cooldown = 6f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override bool IsTriggered() => Input.GetMouseButtonDown(1);

        protected override void Activate()
        {
            direction = SanguinaryKit.FlatAim(Aim.AimDirection, transform);
            dashEnd = Time.time + dashDuration;
            poseEnd = dashEnd + 0.25f;
            active = true;
            MeleeSound.PowerSwing(0.35f, 1.05f, 1.15f);
        }

        protected override void Cancel()
        {
            active = false;
        }

        protected override void Update()
        {
            base.Update();
            if (!active) return;

            if (Time.time < dashEnd)
            {
                // On a replica the movement arrives through the network position.
                if (!IsReplica && controller != null && controller.enabled)
                    controller.Move(direction * (dashDistance / dashDuration * Time.deltaTime));
                return;
            }

            active = false;
            Stab();
        }

        void Stab()
        {
            Vector3 here = transform.position;
            SlashEffect.Spawn(here, direction, range, arcDegrees, 0.3f, SanguinaryKit.BrightBlood);
            if (IsReplica) return;

            Health first = null;
            float firstSq = float.PositiveInfinity;
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, here, direction, range, arcDegrees))
            {
                float sq = SanguinaryKit.Flat(enemy.transform.position - here).sqrMagnitude;
                if (sq >= firstSq) continue;
                firstSq = sq;
                first = enemy;
            }
            if (first == null) return;

            Health target = first;
            target.Strike(Hit.Direct(damage), gameObject, landed =>
            {
                if (!landed || this == null) return;
                if (energy != null) energy.Gain(energyPerHit);
                HitMarkerSound.Play();
                MeleeSound.PowerImpact(0.4f);
                BloodImpact.Play(target.transform.position + Vector3.up, direction, 1f);
                foreach (Health ally in SanguinaryKit.AlliesNear(Health, target.transform.position, allyHealRadius).ToArray())
                {
                    SanguinaryKit.Heal(gameObject, ally, allyHeal);
                    SanguinaryKit.BloodStream(target.transform.position, ally.transform);
                }
            });
        }

        void LateUpdate()
        {
            if (Time.time >= poseEnd)
            {
                if (posed) pose.Reset();
                posed = false;
                return;
            }
            float t = 1f - (poseEnd - Time.time) / (dashDuration + 0.25f);
            posed = true;
            pose.Reset();
            pose.Rotate("Spine", transform.right, Mathf.Lerp(18f, 0f, t));
            pose.Rotate("UpperArm_R", transform.right, Mathf.Lerp(-95f, -20f, t));
        }
    }
}
