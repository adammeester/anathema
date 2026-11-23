using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // R: Crimson Rush. A short planted wind-up with a lane marked on the ground, then a jump-pack-boosted dash along it.
    // The first enemy he reaches stops the dash: a slash for `damage` that stuns them for `stun` seconds. Only that one
    // enemy is hit; anyone else in the lane is passed. Walls stop the dash. C, a stun, silence, panic or knockback
    // during the wind-up cancels it and refunds the cooldown.
    [DefaultExecutionOrder(50)]
    public class CrimsonRush : Ability
    {
        [SerializeField] float windup = 0.2f;
        [SerializeField] float distance = 6f;
        [SerializeField] float duration = 0.22f;
        [SerializeField] float hitRadius = 1.1f;
        [SerializeField] float damage = 14f;
        [SerializeField] float stun = 1.2f;
        [SerializeField] float energyPerHit = 10f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_R" };

        CharacterController controller;
        Energy energy;
        LimbPose pose;
        bool dashing;
        bool posed;
        Vector3 direction;
        float dashEnd;
        float poseEnd;
        GameObject lane;

        protected override float WindupDuration => windup;
        protected override Color WindupColor => SanguinaryKit.Blood;

        public override string DisplayName => "Crimson Rush";
        public override string KeyLabel => "R";
        public override float MovementRange => distance;
        public override bool RootsMovement => IsWindingUp;

        void Reset()
        {
            cooldown = 13f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R);

        protected override void OnWindupStart()
        {
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
            direction = SanguinaryKit.FlatAim(aim - transform.position, transform);
            if (lane != null) Destroy(lane);
            lane = Bullgryn.BullgrynKit.GroundLane(transform.position, direction, distance, hitRadius * 2f, windup + duration, SanguinaryKit.Blood);
            JetPackFx.For(gameObject).SetThrust(0.35f, windup);
        }

        protected override void OnWindupCancel()
        {
            if (lane != null) Destroy(lane);
            JetPackFx.For(gameObject).SetThrust(0f, 0.15f);
        }

        protected override void Activate()
        {
            if (!IsReplica) direction = SanguinaryKit.FlatAim(Aim.AimDirection, transform);
            dashing = true;
            dashEnd = Time.time + duration;
            poseEnd = dashEnd + 0.3f;
            var jets = JetPackFx.For(gameObject);
            jets.SetThrust(1f, 0.05f);
            jets.Puff(3, 0.7f);
            MeleeSound.PowerSwing(0.45f);
        }

        protected override void Cancel()
        {
            if (lane != null) Destroy(lane);
            if (dashing) EndDash();
        }

        protected override void Update()
        {
            base.Update();
            if (!dashing) return;

            // The owner moves and looks for the first enemy; a replica follows the network position and only draws.
            if (!IsReplica)
            {
                if (controller != null && controller.enabled)
                    controller.Move(direction * (distance / duration * Time.deltaTime));
                Health first = FirstEnemyAhead();
                if (first != null)
                {
                    Strike(first);
                    EndDash();
                    return;
                }
            }
            if (Time.time >= dashEnd) EndDash();
        }

        Health FirstEnemyAhead()
        {
            Health best = null;
            float bestSq = float.PositiveInfinity;
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, transform.position, direction, hitRadius + 0.6f, 160f))
            {
                float sq = SanguinaryKit.Flat(enemy.transform.position - transform.position).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = enemy;
            }
            return best;
        }

        void Strike(Health target)
        {
            target.Strike(Hit.Direct(damage).WithStun(stun), gameObject, landed =>
            {
                if (!landed || this == null) return;
                if (energy != null) energy.Gain(energyPerHit);
                SanguinaryKit.FeedThirst(gameObject, damage);
                HitMarkerSound.Play();
                MeleeSound.PowerImpact(0.45f);
                BloodImpact.Play(target.transform.position + Vector3.up, direction, 1.3f);
            });
        }

        void EndDash()
        {
            dashing = false;
            if (lane != null) Destroy(lane);
            JetPackFx.For(gameObject).SetThrust(0f, 0.2f);
            SlashEffect.Spawn(transform.position, direction, hitRadius + 1.2f, 120f, 0.35f, SanguinaryKit.BrightBlood);
        }

        // Lean into the dash with the blade swept back, then whip it through as he arrives.
        void LateUpdate()
        {
            if (!dashing && Time.time >= poseEnd)
            {
                if (posed) pose.Reset();
                posed = false;
                return;
            }
            float t = dashing ? 0f : 1f - (poseEnd - Time.time) / 0.3f;
            posed = true;
            pose.Reset();
            pose.Rotate("Spine", transform.right, Mathf.Lerp(20f, 0f, t));
            pose.Rotate("Spine", Vector3.up, Mathf.Lerp(35f, -25f, t));
            pose.Rotate("UpperArm_R", transform.right, Mathf.Lerp(-60f, -10f, t));
        }

        void OnDestroy()
        {
            if (lane != null) Destroy(lane);
        }
    }
}
