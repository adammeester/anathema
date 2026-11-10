using Anathema.Combat;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // The Berserker's secondary (right mouse). Which attack it is depends on the weapon (Armament upgrade):
    //   Dual chainaxes  - Axe Toss: hurl a spinning chain axe along the aim direction. The first enemy it hits takes
    //                     damage and starts Bleeding. Walls stop the axe. (Twin Axes upgrade: two charges, each recharging independently.)
    //   Heavy Chainaxe  - Gorecleave: a wide frontal sweep that hits everything in reach, knocks it back and causes Bleed.
    //   Eviscerator     - Executioner's Descent: an overhead strike with a long wind-up; bonus damage to low-health
    //                     targets, and it partly ignores armour and shields.
    // The strike leaves the hands when the wind-up ends, so C cancels it before then.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes the arms every LateUpdate
    public class AxeToss : Ability
    {
        [SerializeField] float damage = 12f;
        [SerializeField] float bleedDamagePerSecond = 3f;
        [SerializeField] float bleedDuration = 5f;
        [SerializeField] float projectileSpeed = 24f;
        [SerializeField] float range = 12f;
        [SerializeField] float projectileRadius = 0.4f;
        [SerializeField] float muzzleHeight = 1f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] float windupDuration = 0.12f;
        [SerializeField] float throwDuration = 0.08f;
        [SerializeField] float recoverDuration = 0.25f;

        [Header("Gorecleave (Heavy Chainaxe)")]
        [SerializeField] float cleaveDamage = 20f;
        [SerializeField] float cleaveRange = 4.2f;
        [SerializeField] float cleaveArcDegrees = 200f;
        [SerializeField] float cleaveWindupDuration = 0.3f;
        [SerializeField] float cleaveSwingDuration = 0.14f;
        [SerializeField] float cleaveRecoverDuration = 0.35f;
        [SerializeField] float cleaveCooldown = 7f;
        [SerializeField] float cleaveKnockbackDistance = 3.5f;
        [SerializeField] float cleaveKnockbackDuration = 0.25f;
        [SerializeField] float cleaveBleedDamagePerSecond = 4f;
        [SerializeField] float cleaveBleedDuration = 4f;

        [Header("Executioner's Descent (Eviscerator)")]
        [SerializeField] float descentDamage = 26f;
        [SerializeField] float descentRange = 3.2f;
        [SerializeField] float descentArcDegrees = 80f;
        [SerializeField] float descentWindupDuration = 0.55f;
        [SerializeField] float descentSwingDuration = 0.1f;
        [SerializeField] float descentRecoverDuration = 0.5f;
        [SerializeField] float descentCooldown = 6.5f;
        [SerializeField] float descentExecuteThreshold = 0.5f;     // bonus starts below this share of max health
        [SerializeField] float descentMaxBonus = 1f;               // +100% damage against a target on the brink
        [SerializeField] float descentArmorPierce = 0.4f;
        [SerializeField] float descentShieldBreak = 1.5f;

        [Header("Pose")]
        [SerializeField] float armRaise = 85f;
        [SerializeField] float armFollowThrough = 40f;

        static readonly string[] ArmBones = { "UpperArm_R" };
        static readonly string[] FullBones = { "Spine", "UpperArm_L", "UpperArm_R" };

        // The dual-axe toss only drives the right arm (so it can overlap a chop); the heavy weapons drive the spine and both arms.
        LimbPose armPose;
        LimbPose fullPose;
        LimbPose pose;
        Energy energy;
        Material metalMaterial;
        Material bloodMaterial;
        bool active;
        bool posed;
        bool throwPending;
        BerserkerWeapon weapon;      // the weapon this use started with
        float curWindup, curSwing, curRecover, curRaise, curFollow, curTwist;
        bool bothArms;
        float startTime;
        float lastArm;
        float lastTwist;
        float recoverFromTwist;
        float recoverFromArm;
        float recoverEndTime;

        public override string DisplayName
        {
            get
            {
                switch (BerserkerWeapons.Of(gameObject))
                {
                    case BerserkerWeapon.HeavyChainaxe: return "Cleave";
                    case BerserkerWeapon.Eviscerator: return "Descent";
                    default: return TwoCharges ? "Toss x" + ReadyCharges : "Toss";
                }
            }
        }

        // Twin Axes: the toss holds two charges that each recharge on their own.
        readonly float[] chargeReady = new float[2];
        int usedCharge = -1;

        bool TwoCharges => BerserkerWeapons.Of(gameObject) == BerserkerWeapon.DualChainaxes && UpgradeState.Has(gameObject, BerserkerUpgrade.TwinAxes);

        int ReadyCharges
        {
            get
            {
                int n = 0;
                foreach (float t in chargeReady) if (Time.time >= t) n++;
                return n;
            }
        }

        protected override bool HasSpareCharge => TwoCharges && ReadyCharges > 0;

        public override float CooldownRemaining =>
            TwoCharges ? (ReadyCharges > 0 ? 0f : Mathf.Max(0f, Mathf.Min(chargeReady[0], chargeReady[1]) - Time.time)) : base.CooldownRemaining;

        public override float CooldownNormalized
        {
            get
            {
                if (!TwoCharges) return base.CooldownNormalized;
                float full = cooldown * CooldownScale;
                return full > 0f ? Mathf.Clamp01(CooldownRemaining / full) : 0f;
            }
        }
        // One axe is ready and the other is still on its way back: report how far off it is.
        public override float ChargeRechargeNormalized
        {
            get
            {
                if (!TwoCharges || ReadyCharges != 1) return 0f;
                float full = cooldown * CooldownScale;
                if (full <= 0f) return 0f;
                float pending = Time.time >= chargeReady[0] ? chargeReady[1] : chargeReady[0];
                return Mathf.Clamp01((pending - Time.time) / full);
            }
        }

        public override string KeyLabel => "RMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 6f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            armPose = new LimbPose(transform.Find("Model"), ArmBones);
            fullPose = new LimbPose(transform.Find("Model"), FullBones);
            pose = armPose;
        }

        // True while this ability still holds its pose (wind-up, strike and recovery), and whether that pose uses the
        // spine and both arms (the heavy weapons). AxeChop will not start while a full-body pose is held.
        public bool PoseBusy => active || Time.time < recoverEndTime;
        public bool UsesFullBody => bothArms;

        // The weapon's cooldown relative to the authored one (`cooldown`, from the prefab/JSON, is the dual-axe toss).
        float WeaponCooldownScale()
        {
            if (cooldown <= 0f) return 1f;
            UpgradeState upgrades = Upgrades;
            switch (BerserkerWeapons.Of(gameObject))
            {
                case BerserkerWeapon.HeavyChainaxe: return cleaveCooldown / cooldown;
                case BerserkerWeapon.Eviscerator:
                    return descentCooldown / cooldown * (upgrades != null && upgrades.Has(BerserkerUpgrade.HeadsmansMark) ? BerserkerUpgrade.HeadsmanCooldownScale : 1f);
                default:
                    return upgrades != null && upgrades.Has(BerserkerUpgrade.ChainFrenzy) ? BerserkerUpgrade.FrenzyTossCooldownScale : 1f;
            }
        }

        UpgradeState upgradeState;
        UpgradeState Upgrades => upgradeState != null ? upgradeState : (upgradeState = GetComponent<UpgradeState>());
        AxeChop chop;
        TwoHandedGrip grip;

        protected override void Update()
        {
            CooldownScale = WeaponCooldownScale();
            base.Update();

            if (throwPending && !IsReplica && IsDisabled) InterruptSelf();

            if (throwPending && Time.time >= startTime + curWindup)
            {
                throwPending = false;
                switch (weapon)
                {
                    case BerserkerWeapon.HeavyChainaxe: Cleave(); break;
                    case BerserkerWeapon.Eviscerator: Descent(); break;
                    default: Throw(); break;
                }
            }
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            // The heavy weapons drive the spine and both arms, so they may not overlap a chop (and vice versa).
            if (chop == null) chop = GetComponent<AxeChop>();
            if (chop != null && chop.PoseBusy && BerserkerWeapons.Of(gameObject) != BerserkerWeapon.DualChainaxes) return false;
            return Input.GetMouseButtonDown(1);
        }

        protected override void Activate()
        {
            ResolveWeapon();
            active = true;
            throwPending = true;
            startTime = Time.time;

            usedCharge = -1;
            if (TwoCharges)
            {
                // Spend a ready charge (else the one closest to ready); only that charge starts recharging.
                usedCharge = Time.time >= chargeReady[0] ? 0 : Time.time >= chargeReady[1] ? 1 : (chargeReady[0] <= chargeReady[1] ? 0 : 1);
                chargeReady[usedCharge] = Time.time + cooldown * CooldownScale;
            }
        }

        void ResolveWeapon()
        {
            weapon = BerserkerWeapons.Of(gameObject);
            if (posed) pose.Reset();
            pose = weapon == BerserkerWeapon.DualChainaxes ? armPose : fullPose;
            switch (weapon)
            {
                case BerserkerWeapon.HeavyChainaxe:
                    curWindup = cleaveWindupDuration; curSwing = cleaveSwingDuration; curRecover = cleaveRecoverDuration;
                    curRaise = 80f; curFollow = 30f; curTwist = 50f; bothArms = true;
                    break;
                case BerserkerWeapon.Eviscerator:
                    curWindup = descentWindupDuration; curSwing = descentSwingDuration; curRecover = descentRecoverDuration;
                    curRaise = 125f; curFollow = 45f; curTwist = 0f; bothArms = true;
                    break;
                default:
                    curWindup = windupDuration; curSwing = throwDuration; curRecover = recoverDuration;
                    curRaise = armRaise; curFollow = armFollowThrough; curTwist = 0f; bothArms = false;
                    break;
            }
        }

        protected override void Cancel()
        {
            if (!active) return;

            if (throwPending)
            {
                RefundCooldown();
                if (usedCharge >= 0) chargeReady[usedCharge] = 0f;
            }
            usedCharge = -1;
            active = false;
            throwPending = false;
            recoverFromArm = lastArm;
            recoverFromTwist = lastTwist;
            recoverEndTime = Time.time + curRecover;
        }

        void Throw()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            LaunchAxe(dir);
        }

        void LaunchAxe(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * 0.7f;

            var go = new GameObject("Thrown Axe");
            go.transform.position = origin;
            BuildAxeVisual(go.transform);

            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit);
        }

        // Gorecleave: a wide frontal sweep. Everything in reach is hurt, thrown back and left bleeding.
        void Cleave()
        {
            Vector3 origin = transform.position;
            Vector3 aim = Aim.AimDirection;
            bool earthshaker = UpgradeState.Has(gameObject, BerserkerUpgrade.Earthshaker);
            bool rending = UpgradeState.Has(gameObject, BerserkerUpgrade.RendingGouges);
            float reach = cleaveRange * (earthshaker ? BerserkerUpgrade.EarthshakerRangeScale : 1f);
            float knock = cleaveKnockbackDistance * (earthshaker ? BerserkerUpgrade.EarthshakerKnockbackScale : 1f);
            SlashEffect.Spawn(origin, aim, reach, cleaveArcDegrees, curSwing * 3f, new Color(1f, 0.15f, 0.08f, 0.9f));

            foreach (Health target in BerserkerWeapons.EnemiesInCone(gameObject, Health, origin, aim, reach, cleaveArcDegrees))
            {
                float dealt = cleaveDamage * BerserkerUpgradeEffects.DamageScale(gameObject, target);
                if (!target.TakeDamage(dealt, gameObject)) continue;

                if (energy != null) energy.Gain(energyPerHit);
                BerserkerUpgradeEffects.RegisterHit(gameObject, dealt);
                BloodImpact.Play(target.transform.position + Vector3.up, aim, 1f, true);
                if (target.IsDead) continue;

                Vector3 push = target.transform.position - origin;
                push.y = 0f;
                if (push.sqrMagnitude < 0.0001f) push = aim;
                Knockback.Apply(target.gameObject, push, knock, cleaveKnockbackDuration);
                Bleeding.Apply(target.gameObject, gameObject,
                    rending ? cleaveBleedDamagePerSecond * BerserkerUpgrade.RendingBleedScale : cleaveBleedDamagePerSecond,
                    rending ? cleaveBleedDuration + BerserkerUpgrade.RendingExtraSeconds : cleaveBleedDuration);
            }
        }

        // Executioner's Descent: one overhead blow. The lower the target's health, the harder it lands.
        void Descent()
        {
            Vector3 origin = transform.position;
            Vector3 aim = Aim.AimDirection;
            float pierce = BerserkerUpgradeEffects.Pierce(gameObject, descentArmorPierce);
            float shieldBreak = BerserkerUpgradeEffects.ShieldBreak(gameObject, descentShieldBreak);
            SlashEffect.Spawn(origin, aim, descentRange, descentArcDegrees, curSwing * 4f, new Color(1f, 0.8f, 0.25f, 0.95f));

            foreach (Health target in BerserkerWeapons.EnemiesInCone(gameObject, Health, origin, aim, descentRange, descentArcDegrees))
            {
                float threshold = Mathf.Min(1f, descentExecuteThreshold * (UpgradeState.Has(gameObject, BerserkerUpgrade.HeadsmansMark) ? BerserkerUpgrade.HeadsmanThresholdScale : 1f));
                float wounded = Mathf.Clamp01((threshold - target.Normalized) / threshold);
                float dealt = descentDamage * (1f + wounded * descentMaxBonus) * BerserkerUpgradeEffects.DamageScale(gameObject, target);
                if (!target.TakeDamage(dealt, gameObject, false, pierce, shieldBreak)) continue;

                if (energy != null) energy.Gain(energyPerHit);
                BerserkerUpgradeEffects.RegisterHit(gameObject, dealt);
                BloodImpact.Play(target.transform.position + Vector3.up, aim, 1f, true);
            }
        }

        void OnHit(Health target)
        {
            if (energy != null) energy.Gain(energyPerHit);
            BerserkerUpgradeEffects.RegisterHit(gameObject, damage);
            // Chain Frenzy: every axe that lands feeds Rage a second time.
            if (UpgradeState.Has(gameObject, BerserkerUpgrade.ChainFrenzy)) BerserkerUpgradeEffects.GainRage(gameObject);
            BloodImpact.Play(target.transform.position + Vector3.up, target.transform.position - transform.position, 1f, true);
            if (!target.IsDead) Bleeding.Apply(target.gameObject, gameObject, bleedDamagePerSecond, bleedDuration);
        }

        // A handle and a blade head on a spinning child, so the Projectile root keeps pointing along its path.
        void BuildAxeVisual(Transform root)
        {
            if (metalMaterial == null) metalMaterial = VisualUtil.UnlitMaterial(new Color(0.55f, 0.57f, 0.6f));
            if (bloodMaterial == null) bloodMaterial = VisualUtil.UnlitMaterial(new Color(0.45f, 0.02f, 0.02f));

            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(root, false);
            spinner.gameObject.AddComponent<AxeSpin>();

            AddPart(spinner, new Vector3(0.08f, 0.7f, 0.08f), Vector3.zero, bloodMaterial);                 // handle
            AddPart(spinner, new Vector3(0.1f, 0.3f, 0.45f), new Vector3(0f, 0.3f, 0.2f), metalMaterial);   // blade
        }

        static void AddPart(Transform parent, Vector3 scale, Vector3 localPosition, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
        }

        // Wind-up: arm(s) hauled back and up. Strike: whips forward. Recovery: eases back to rest.
        // The dual-axe toss only moves the right arm; the heavy weapons use both arms and a spine twist.
        void ApplyPose()
        {
            if (grip == null) grip = GetComponent<TwoHandedGrip>();
            float now = Time.time;
            float windupEnd = startTime + curWindup;
            float throwEnd = windupEnd + curSwing;
            float arm;
            float twist;

            if (active && now < windupEnd)
            {
                float t = Mathf.SmoothStep(0f, 1f, (now - startTime) / curWindup);
                arm = Mathf.Lerp(0f, -curRaise, t);
                twist = Mathf.Lerp(0f, -curTwist, t);
            }
            else if (active && now < throwEnd)
            {
                float t = (now - windupEnd) / curSwing;
                arm = Mathf.Lerp(-curRaise, curFollow, t);
                twist = Mathf.Lerp(-curTwist, curTwist, t);
            }
            else if (active)
            {
                // Strike finished: ease back from the follow-through.
                active = false;
                recoverFromArm = curFollow;
                recoverFromTwist = curTwist;
                recoverEndTime = now + curRecover;
                arm = curFollow;
                twist = curTwist;
            }
            else if (now < recoverEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - now) / curRecover);
                arm = Mathf.Lerp(recoverFromArm, 0f, t);
                twist = Mathf.Lerp(recoverFromTwist, 0f, t);
            }
            else
            {
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                lastArm = 0f;
                lastTwist = 0f;
                if (grip != null) grip.TossPitch = 0f;
                return;
            }

            lastArm = arm;
            lastTwist = twist;
            posed = true;

            pose.Reset();
            if (curTwist != 0f) pose.Rotate("Spine", Vector3.up, twist);
            // Two-handed weapons swing as one piece (TwoHandedGrip drives both arms).
            if (grip != null && grip.Engaged) { grip.TossPitch = arm; return; }
            pose.Rotate("UpperArm_R", transform.right, arm);
            if (bothArms) pose.Rotate("UpperArm_L", transform.right, arm);
        }
    }
}
