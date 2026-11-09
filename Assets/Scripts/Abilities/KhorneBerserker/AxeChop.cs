using System.Collections.Generic;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Hold left mouse to hack with the chain axes, left and right in turn. Each chop winds the working arm up,
    // lands its damage when the wind-up ends, then follows through. Pressing C during the wind-up cancels it.
    // The pose is driven through the skeleton (spine twist plus the chopping arm); the legs are never touched.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and the arms every LateUpdate
    public class AxeChop : Ability
    {
        [SerializeField] float damage = 9f;
        [SerializeField] float range = 2.4f;
        [SerializeField] float arcDegrees = 100f;
        [SerializeField] float windupDuration = 0.1f;
        [SerializeField] float swingDuration = 0.1f;
        [SerializeField] float recoverDuration = 0.3f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] Color slashColor = new Color(1f, 0.25f, 0.15f, 0.8f);

        [Header("Heavy Chainaxe - The Reaver (Armament upgrade)")]
        [SerializeField] float heavyDamage = 16f;
        [SerializeField] float heavyRange = 3.5f;
        [SerializeField] float heavyArcDegrees = 150f;
        [SerializeField] float heavyWindupDuration = 0.22f;
        [SerializeField] float heavySwingDuration = 0.14f;
        [SerializeField] float heavyRecoverDuration = 0.5f;
        [SerializeField] float heavyCooldown = 1.3f;
        [SerializeField] float heavyBleedDamagePerSecond = 2.5f;   // heavy hits inflict Bleed
        [SerializeField] float heavyBleedDuration = 3f;
        [SerializeField] float heavySpineTwist = 42f;
        [SerializeField] float heavyArmRaise = 85f;

        [Header("Eviscerator - The Executioner (Armament upgrade)")]
        [SerializeField] float eviscDamage = 30f;
        [SerializeField] float eviscRange = 2.8f;
        [SerializeField] float eviscArcDegrees = 70f;
        [SerializeField] float eviscWindupDuration = 0.55f;        // the pronounced wind-up is the risk
        [SerializeField] float eviscSwingDuration = 0.1f;
        [SerializeField] float eviscRecoverDuration = 0.6f;
        [SerializeField] float eviscCooldown = 1.8f;
        [SerializeField] float eviscArmorPierce = 0.4f;            // share of armour ignored (passive)
        [SerializeField] float eviscShieldBreak = 1.5f;            // damage multiplier against shields (passive)
        [SerializeField] float eviscSpineTwist = 10f;
        [SerializeField] float eviscArmRaise = 115f;

        [Header("Empowered (after a successful counter)")]
        [SerializeField] float empoweredDamageMultiplier = 2f;
        [SerializeField] float empoweredRangeMultiplier = 1.35f;
        [SerializeField] Color empoweredColor = new Color(1f, 0.85f, 0.3f, 0.95f);

        [Header("Pose")]
        [SerializeField] float spineTwist = 28f;
        [SerializeField] float armRaise = 65f;
        [SerializeField] float armFollowThrough = 15f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_L", "UpperArm_R" };

        LimbPose pose;
        Energy energy;
        bool active;
        bool posed;
        bool strikePending;
        bool leftArm;
        bool empowered;         // the next chop is empowered
        bool strikeEmpowered;   // the chop in flight is empowered
        GameObject aura;
        Material auraMaterial;
        float windupEndTime;
        float swingEndTime;
        float recoverEndTime;
        float recoverFromTwist;
        float recoverFromArm;
        float lastTwist;
        float lastArm;

        float chopSpeed = 1f;   // attack speed multiplier the current chop started with

        // The chop in flight, resolved from the wielded weapon when it started.
        BerserkerWeapon weapon;
        float curDamage, curRange, curArc, curWindup, curSwing, curRecover, curTwist, curRaise, curFollow;
        float dualCooldown;     // the prefab/JSON cooldown, used by the default dual chainaxes

        protected override bool ScalesWithAttackSpeed => true;

        public override string DisplayName
        {
            get
            {
                switch (BerserkerWeapons.Of(gameObject))
                {
                    case BerserkerWeapon.HeavyChainaxe: return "Sweep";
                    case BerserkerWeapon.Eviscerator: return "Strike";
                    default: return "Chop";
                }
            }
        }
        public override string KeyLabel => "LMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 0.75f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);

            // Ground ring under the Berserker while the next chop is empowered.
            aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aura.name = "Empowered Aura";
            Destroy(aura.GetComponent<Collider>());
            aura.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraMaterial = VisualUtil.UnlitMaterial(empoweredColor);
            aura.GetComponent<Renderer>().sharedMaterial = auraMaterial;
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = new Vector3(0f, 0.16f, 0f); // above the arena's visible floor discs
            aura.transform.localScale = new Vector3(2.2f, 0.01f, 2.2f);
            aura.SetActive(false);
        }

        // Makes the next chop hit harder and reach further. Called by the counter's payoff.
        public void Empower()
        {
            empowered = true;
            aura.SetActive(true);
        }

        void Start()
        {
            dualCooldown = cooldown; // after CharacterStats has applied its JSON
        }

        // Each weapon has its own swing speed; the cooldown follows it.
        float WeaponCooldown()
        {
            switch (BerserkerWeapons.Of(gameObject))
            {
                case BerserkerWeapon.HeavyChainaxe: return heavyCooldown;
                case BerserkerWeapon.Eviscerator: return eviscCooldown;
                default: return dualCooldown;
            }
        }

        protected override void Update()
        {
            if (dualCooldown > 0f) cooldown = WeaponCooldown();
            base.Update();

            if (strikePending && !IsReplica && IsDisabled) InterruptSelf();

            if (empowered)
            {
                Color c = empoweredColor;
                c.a = empoweredColor.a * (0.45f + 0.35f * Mathf.Sin(Time.time * 10f));
                auraMaterial.color = c;
            }

            if (strikePending && Time.time >= windupEndTime)
            {
                strikePending = false;
                DealDamage();
                float reach = strikeEmpowered ? curRange * empoweredRangeMultiplier : curRange;
                SlashEffect.Spawn(transform.position, Aim.AimDirection, reach, curArc, curSwing * 2f,
                    strikeEmpowered ? empoweredColor : slashColor);
                strikeEmpowered = false;
            }
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButton(0);
        }

        protected override void Activate()
        {
            leftArm = !leftArm;
            ResolveWeapon();
            strikeEmpowered = empowered;
            empowered = false;
            aura.SetActive(false);
            strikePending = true;
            active = true;
            // Enraged swings faster: every phase of the chop shrinks with attack speed.
            chopSpeed = Enraged.AttackSpeedMultiplier(gameObject);
            windupEndTime = Time.time + curWindup / chopSpeed;
            swingEndTime = windupEndTime + curSwing / chopSpeed;
            recoverEndTime = swingEndTime + curRecover / chopSpeed;
            recoverFromTwist = SwingTwist();
            recoverFromArm = curFollow;
        }

        // Pins this chop's numbers to the weapon in hand.
        void ResolveWeapon()
        {
            weapon = BerserkerWeapons.Of(gameObject);
            switch (weapon)
            {
                case BerserkerWeapon.HeavyChainaxe:
                    curDamage = heavyDamage; curRange = heavyRange; curArc = heavyArcDegrees;
                    curWindup = heavyWindupDuration; curSwing = heavySwingDuration; curRecover = heavyRecoverDuration;
                    curTwist = heavySpineTwist; curRaise = heavyArmRaise; curFollow = armFollowThrough;
                    break;
                case BerserkerWeapon.Eviscerator:
                    curDamage = eviscDamage; curRange = eviscRange; curArc = eviscArcDegrees;
                    curWindup = eviscWindupDuration; curSwing = eviscSwingDuration; curRecover = eviscRecoverDuration;
                    curTwist = eviscSpineTwist; curRaise = eviscArmRaise; curFollow = armFollowThrough * 2f;
                    if (UpgradeState.Has(gameObject, BerserkerUpgrade.WhettedEdge)) curWindup *= BerserkerUpgrade.WhettedWindupScale;
                    break;
                default:
                    curDamage = damage; curRange = range; curArc = arcDegrees;
                    curWindup = windupDuration; curSwing = swingDuration; curRecover = recoverDuration;
                    curTwist = spineTwist; curRaise = armRaise; curFollow = armFollowThrough;
                    break;
            }
        }

        protected override void Cancel()
        {
            if (!active) return;

            // A chop cancelled before it lands gives its empowerment back.
            if (strikePending && strikeEmpowered) Empower();
            strikeEmpowered = false;
            if (strikePending) RefundCooldown();

            active = false;
            strikePending = false;
            recoverFromTwist = lastTwist;
            recoverFromArm = lastArm;
            chopSpeed = 1f;
            recoverEndTime = Time.time + curRecover;
        }

        // Left axe chops across to the right and vice versa, so the spine winds toward the working side first.
        float SwingTwist()
        {
            return curTwist * (leftArm ? -1f : 1f);
        }

        void DealDamage()
        {
            Vector3 origin = transform.position;
            float reach = strikeEmpowered ? curRange * empoweredRangeMultiplier : curRange;
            float baseDamage = strikeEmpowered ? curDamage * empoweredDamageMultiplier : curDamage;
            // The Eviscerator's passive: its blows partly ignore armour and shields.
            float pierce = weapon == BerserkerWeapon.Eviscerator
                ? (UpgradeState.Has(gameObject, BerserkerUpgrade.WhettedEdge) ? BerserkerUpgrade.WhettedPierce : eviscArmorPierce)
                : 0f;
            float shieldBreak = weapon == BerserkerWeapon.Eviscerator
                ? (UpgradeState.Has(gameObject, BerserkerUpgrade.WhettedEdge) ? BerserkerUpgrade.WhettedShieldBreak : eviscShieldBreak)
                : 1f;
            bool rending = UpgradeState.Has(gameObject, BerserkerUpgrade.RendingGouges);

            foreach (Health target in BerserkerWeapons.EnemiesInCone(gameObject, Health, origin, Aim.AimDirection, reach, curArc))
            {
                float dealt = baseDamage * BerserkerUpgradeEffects.DamageScale(gameObject, target);
                if (!target.TakeDamage(dealt, gameObject, false, pierce, shieldBreak)) continue;

                if (energy != null) energy.Gain(energyPerHit);
                BerserkerUpgradeEffects.RegisterHit(gameObject, dealt);
                BloodImpact.Play(target.transform.position + Vector3.up, Aim.AimDirection, strikeEmpowered ? 1f : 0.7f);
                // The Reaver's passive: heavy hits inflict Bleed.
                if (weapon == BerserkerWeapon.HeavyChainaxe && !target.IsDead)
                    Bleeding.Apply(target.gameObject, gameObject,
                        rending ? heavyBleedDamagePerSecond * BerserkerUpgrade.RendingBleedScale : heavyBleedDamagePerSecond,
                        rending ? heavyBleedDuration + BerserkerUpgrade.RendingExtraSeconds : heavyBleedDuration);
            }
        }

        void ApplyPose()
        {
            float now = Time.time;
            float twist;
            float arm;

            if (active && now < windupEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (windupEndTime - now) * chopSpeed / curWindup);
                twist = Mathf.Lerp(0f, -SwingTwist(), t);
                arm = Mathf.Lerp(0f, -curRaise, t);
            }
            else if (active && now < swingEndTime)
            {
                float t = 1f - (swingEndTime - now) * chopSpeed / curSwing;
                t = 1f - (1f - t) * (1f - t);
                twist = Mathf.Lerp(-SwingTwist(), SwingTwist(), t);
                arm = Mathf.Lerp(-curRaise, curFollow, t);
            }
            else if (now < recoverEndTime)
            {
                active = false;
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - now) * chopSpeed / curRecover);
                twist = Mathf.Lerp(recoverFromTwist, 0f, t);
                arm = Mathf.Lerp(recoverFromArm, 0f, t);
            }
            else
            {
                active = false;
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                lastTwist = 0f;
                lastArm = 0f;
                return;
            }

            lastTwist = twist;
            lastArm = arm;
            posed = true;

            // Parent bones first: the arm's world rotation already includes the spine's.
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, twist);
            pose.Rotate(leftArm ? "UpperArm_L" : "UpperArm_R", transform.right, arm);
        }
    }
}
