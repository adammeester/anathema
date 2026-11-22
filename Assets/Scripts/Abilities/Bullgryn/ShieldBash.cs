using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // RMB: Shield Bash. A quick shove with the slabshield's rim at short range: light damage, interrupts channels and
    // wind-ups, knocks back, then slows. Works with either weapon (it is the gauntlet build's answer to divers).
    // Unusable while Living Wall stands (the shield is in the ground). C during the wind-up cancels it.
    [DefaultExecutionOrder(50)]
    public class ShieldBash : Ability
    {
        [SerializeField] float damage = 8f;
        [SerializeField] float range = 3f;
        [SerializeField] float arcDegrees = 90f;
        [SerializeField] float windup = 0.15f;
        [SerializeField] float knockbackDistance = 2f;
        [SerializeField] float knockbackDuration = 0.2f;
        [SerializeField] float slow = 0.3f;
        [SerializeField] float slowDuration = 2f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] float poseDuration = 0.35f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_L", "Forearm_L" };

        Energy energy;
        LimbPose pose;
        bool posed;
        float poseStart = float.NegativeInfinity;

        public override string DisplayName => "Shield Bash";
        public override string KeyLabel => "RMB";
        public override bool Available => !BullgrynKit.ShieldPlanted(gameObject);

        protected override float WindupDuration => windup;
        protected override Color WindupColor => BullgrynKit.Steel;

        void Reset()
        {
            cooldown = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override bool IsTriggered()
        {
            if (!Input.GetMouseButtonDown(1) || BullgrynKit.Busy(gameObject) || BullgrynKit.ShieldPlanted(gameObject)) return false;
            return !(TryGetComponent(out BullCharge charge) && charge.IsCharging);
        }

        protected override void OnWindupStart()
        {
            poseStart = Time.time;
        }

        protected override void Activate()
        {
            Vector3 dir = BullgrynKit.FlatAim(Aim.AimDirection, transform);
            SlashEffect.Spawn(transform.position, dir, range, arcDegrees, 0.2f, BullgrynKit.Steel);
            Sfx.Play(Sfx.HammerSmash, 0.45f, 1.05f, 1.2f, 0.5f);

            if (IsReplica) return;
            bool hard = UpgradeHasBonebreaker();
            int hits = 0;
            foreach (Health enemy in BullgrynKit.EnemiesInCone(Health, transform.position, dir, range, arcDegrees))
            {
                Vector3 push = enemy.transform.position - transform.position;
                push.y = 0f;
                Hit bash = Hit.Direct(damage)
                    .WithSilence(0.1f, interrupt: true)
                    .WithKnockback(push.sqrMagnitude > 0.0001f ? push.normalized : dir, knockbackDistance, knockbackDuration)
                    .WithSlow(hard ? BullgrynUpgrade.BonebreakerSlowSeconds : slowDuration, hard ? BullgrynUpgrade.BonebreakerSlow : slow);
                hits++;
                enemy.Strike(bash, gameObject, landed =>
                {
                    if (landed && energy != null) energy.Gain(energyPerHit);
                });
            }
            if (hits > 0) MeleeSound.Impact(hits);
            Anathema.Arena.CasketField.Cone(gameObject, transform.position, dir, range, arcDegrees);
        }

        bool UpgradeHasBonebreaker() => Anathema.Upgrades.UpgradeState.Has(gameObject, BullgrynUpgrade.Bonebreaker);

        protected override void Cancel()
        {
        }

        void LateUpdate()
        {
            float t = (Time.time - poseStart) / Mathf.Max(0.01f, windup + poseDuration);
            if (t < 0f || t >= 1f)
            {
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                return;
            }
            // Draw the shield back during the wind-up, then punch it out and ease home.
            float split = windup / (windup + poseDuration);
            float arm = t < split ? Mathf.Lerp(0f, 25f, t / split) : Mathf.Lerp(-70f, 0f, Mathf.SmoothStep(0f, 1f, (t - split) / (1f - split)));
            posed = true;
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, t < split ? 15f : -10f);
            pose.Rotate("UpperArm_L", transform.right, arm);
        }
    }
}
