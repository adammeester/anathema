using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // LMB: click (or hold) to cut with the glimmersteel blade in a three-strike chain: a forehand, a backhand, then a wide spinning
    // cleave that hits harder, reaches further and leaves every enemy it cuts poisoned. A pause longer than the combo
    // window starts the chain over. Every strike is its own attack; the first two follow each other quickly, and after
    // the cleave a longer recovery (finisherCooldown) gives the chain its rhythm. A click shortly before the next strike
    // is ready is remembered (inputBuffer), so clicking in time never drops a strike. Each strike lands when its short wind-up ends; C during the wind-up cancels it.
    // Attacking breaks stealth, and a strike thrown from stealth briefly stuns what it hits. A waiting Shadow Jump bonus
    // is added to the strike.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes the spine and arms every LateUpdate
    public class GlimmersteelBlade : Ability
    {
        [SerializeField] float damage = 8f;
        [SerializeField] float range = 2.5f;
        [SerializeField] float arcDegrees = 90f;
        [SerializeField] float finisherDamage = 16f;
        [SerializeField] float finisherRange = 3.2f;
        [SerializeField] float finisherArcDegrees = 200f;   // the cleave
        [SerializeField] float windupDuration = 0.12f;
        [SerializeField] float finisherWindupDuration = 0.2f;
        [SerializeField] float swingDuration = 0.1f;
        [SerializeField] float recoverDuration = 0.22f;
        [SerializeField] float comboWindow = 1.2f;
        [SerializeField] float finisherCooldown = 1.1f;    // the pause after the cleave, before the chain starts again
        [SerializeField] float inputBuffer = 0.25f;
        [SerializeField] float energyPerHit = 7f;
        [SerializeField] int finisherPoisonStacks = 1;
        [SerializeField] float poisonDamagePerSecond = 1f;  // per stack
        [SerializeField] float poisonDuration = 4f;

        [Header("Pose")]
        [SerializeField] float spineTwist = 32f;
        [SerializeField] float armRaise = 70f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_R", "UpperArm_L" };

        LimbPose pose;
        Energy energy;
        int comboStep;          // strikes landed in the current chain (0-2)
        float lastStrikeTime = float.NegativeInfinity;
        int currentStep;        // the strike in flight: 0, 1 or 2 (the finisher)
        bool active;
        bool strikePending;
        bool posed;
        float strikeAt, swingEnd, recoverEnd;
        float curWindup;
        float lastClickTime = float.NegativeInfinity;

        public override string DisplayName => "Glimmersteel";
        public override string KeyLabel => "LMB";
        protected override bool ScalesWithAttackSpeed => true;

        // Which strike the next click throws (0, 1 or 2 = cleave), for the HUD and visuals.
        public int NextStep => Time.time - lastStrikeTime > comboWindow ? 0 : comboStep;

        void Reset()
        {
            cooldown = 0.5f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override bool IsTriggered()
        {
            if (PrimaryFireBlocked) return false;
            if (Input.GetMouseButtonDown(0)) lastClickTime = Time.time;
            return Input.GetMouseButton(0) || Time.time - lastClickTime <= inputBuffer;
        }

        protected override void Activate()
        {
            currentStep = NextStep;
            lastClickTime = float.NegativeInfinity; // the buffered click is spent
            comboStep = (currentStep + 1) % 3;
            lastStrikeTime = Time.time;

            float speed = Enraged.AttackSpeedMultiplier(gameObject);
            curWindup = (currentStep == 2 ? finisherWindupDuration : windupDuration) / speed;
            strikeAt = Time.time + curWindup;
            swingEnd = strikeAt + swingDuration / speed;
            recoverEnd = swingEnd + recoverDuration / speed;
            strikePending = true;
            active = true;
        }

        protected override void Cancel()
        {
            if (!active) return;
            if (strikePending)
            {
                RefundCooldown();
                // A cancelled strike doesn't advance the chain.
                comboStep = currentStep;
            }
            strikePending = false;
            active = false;
            recoverEnd = Time.time + recoverDuration;
        }

        protected override void Update()
        {
            // The cooldown that follows a strike is fixed when it is thrown: the cleave is followed by the long recovery.
            // (Only changed while ready, so the HUD's cooldown sweep stays consistent.)
            if (CooldownRemaining <= 0f && cooldown > 0f) CooldownScale = NextStep == 2 ? finisherCooldown / cooldown : 1f;
            base.Update();
            if (strikePending && !IsReplica && IsDisabled) InterruptSelf();

            if (strikePending && Time.time >= strikeAt)
            {
                strikePending = false;
                Strike();
            }
        }

        void Strike()
        {
            bool finisher = currentStep == 2;
            Vector3 dir = MandrakeKit.FlatAim(Aim.AimDirection, transform);
            float reach = finisher ? finisherRange : range;
            float arc = finisher ? finisherArcDegrees : arcDegrees;

            float bonus = 0f;
            bool fromStealth = false;
            if (!IsReplica) bonus = MandrakeKit.BeginStrike(gameObject, out fromStealth);
            bool shadowed = bonus > 0f || (IsReplica && ShowingShadowBonus());

            SlashEffect.Spawn(transform.position, dir, reach, arc, swingDuration * (finisher ? 3f : 2f),
                shadowed || finisher ? MandrakeKit.BaleflameBright : MandrakeKit.Baleflame);
            if (finisher) SlashEffect.Spawn(transform.position, -dir, reach * 0.8f, 360f - arc, swingDuration * 3f, MandrakeKit.Shadow);

            int hits = 0;
            if (!IsReplica)
            {
                foreach (Health target in MandrakeKit.EnemiesInCone(Health, transform.position, dir, reach, arc))
                {
                    float dealt = ((finisher ? finisherDamage : damage) + bonus) * MandrakeKit.VenomScale(gameObject, target);
                    Hit hit = Hit.Direct(dealt);
                    int stacks = finisher ? finisherPoisonStacks : 0;
                    if (stacks > 0 && MandrakeUpgradeEffects.Has(gameObject, MandrakeUpgrade.EnvenomedEdge)) stacks += MandrakeUpgrade.EnvenomedEdgeExtraStacks;
                    if (stacks > 0) hit = hit.WithPoison(stacks, poisonDamagePerSecond, poisonDuration);
                    if (fromStealth) hit = hit.WithStun(MandrakeKit.StealthStrikeStun);
                    hits++;
                    float healthBefore = target.Current;
                    target.Strike(hit, gameObject, landed =>
                    {
                        if (!landed) return;
                        if (energy != null) energy.Gain(energyPerHit);
                        SoulHarvest.NoteBladeHit(gameObject, target, healthBefore, dealt);
                    });
                }
            }
            MeleeSound.Slash(finisher ? 0.55f : 0.45f, finisher ? 0.8f : 1.05f, finisher ? 0.9f : 1.2f);
            if (hits > 0) MeleeSound.Impact(hits);
        }

        bool ShowingShadowBonus()
        {
            var jump = GetComponent<ShadowJump>();
            return jump != null && jump.ShowsEmpowered;
        }

        void LateUpdate()
        {
            float now = Time.time;
            // Forehand swings right-to-left, backhand the other way, the cleave whips all the way round.
            float side = currentStep == 1 ? -1f : 1f;
            float twistAmount = currentStep == 2 ? spineTwist * 1.6f : spineTwist;
            float twist, arm;

            if (active && now < strikeAt)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (strikeAt - now) / Mathf.Max(0.01f, curWindup));
                twist = Mathf.Lerp(0f, -twistAmount * side, t);
                arm = Mathf.Lerp(0f, -armRaise, t);
            }
            else if (active && now < swingEnd)
            {
                float t = 1f - (swingEnd - now) / Mathf.Max(0.01f, swingEnd - strikeAt);
                twist = Mathf.Lerp(-twistAmount * side, twistAmount * side, t);
                arm = Mathf.Lerp(-armRaise, 10f, t);
            }
            else if (now < recoverEnd)
            {
                active = false;
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEnd - now) / Mathf.Max(0.01f, recoverDuration));
                twist = Mathf.Lerp(twistAmount * side, 0f, t);
                arm = Mathf.Lerp(10f, 0f, t);
            }
            else
            {
                active = false;
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                return;
            }

            posed = true;
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, twist);
            pose.Rotate("UpperArm_R", transform.right, arm);
        }
    }
}
