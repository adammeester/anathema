using System;
using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // Space (hold): Brace. The Bullgryn raises his slabshield. Hits and projectiles that come from inside the frontal arc
    // are blocked outright, along with whatever stun, slow or knockback they carried; area damage still gets through.
    // The shield is also a solid plate in front of him, so enemy projectiles stop on it before reaching allies behind.
    // While braced he moves slowly, turns at a limited rate and can't attack. Blocked hits wear down a stamina bar; if it
    // runs dry the guard breaks and he can't brace again for a few seconds. Blocked damage builds Grit and energy.
    // Release Space (or press C) to lower the shield.
    //
    // EX (Shift+Space, costs energy per use): Ration Break. Instant: he wolfs down a ration and recovers a share of his
    // health (healable health only, like any heal). Two charges, each recharging on its own; each use costs the energy.
    //
    // Online: the guard is replayed on every copy from the owner's cues (raising and lowering are both cues to this
    // ability), and the server's copy decides each block. It tells everyone (NetPlayer.ServerShieldBlocked), so every
    // copy keeps the same stamina and Grit.
    [DefaultExecutionOrder(100)] // after MouseAim (facing) and the walk cycle (pose)
    public class Brace : Ability, IMoveSpeedModifier
    {
        [SerializeField] float arcDegrees = 140f;
        [SerializeField] float arcForward = 0.5f;           // the block's centre sits this far in front of him
        [SerializeField] float moveSpeedScale = 0.45f;
        [SerializeField] float turnRate = 240f;              // degrees per second while braced
        [SerializeField] float maxStamina = 100f;
        [SerializeField] float holdDrainPerSecond = 0f;     // 0: only blocked damage wears the shield down
        [SerializeField] float regenPerSecond = 20f;
        [SerializeField] float regenDelay = 1f;              // after lowering the shield
        [SerializeField] float minStaminaToRaise = 15f;
        [SerializeField] float guardBreakStagger = 0f;      // 0: a broken guard just drops the shield (and locks it out)
        [SerializeField] float guardBreakLockout = 3f;
        [SerializeField] float energyPerBlockedDamage = 1f / 3f;
        [SerializeField] Vector3 shieldSize = new Vector3(2f, 2.4f, 0.3f);
        [SerializeField] float shieldForward = 1.05f;

        [Header("EX: Ration Break")]
        [SerializeField] float exEnergyCost = 50f;           // per use
        [SerializeField] float eatDuration = 0.5f;           // how long the hand-to-mouth pose shows (no cast time)
        [SerializeField] float healFraction = 0.2f;          // of max health
        [SerializeField] float rationCooldown = 15f;         // per charge
        [SerializeField] int rationCharges = 2;

        [Header("Block arc (the translucent area shown while braced)")]
        [SerializeField] float arcRadius = 1.4f;
        [SerializeField] float arcHeight = 2.4f;

        [Header("Pose: a low, braced crouch behind the shield")]
        [SerializeField] float shieldArmRaise = 20f;    // forward
        [SerializeField] float shieldAcross = 20f;      // swung in front of the body
        [SerializeField] float shieldSquare = 38f;      // the shield turned back to face straight ahead
        [SerializeField] float braceLean = 22f;
        [SerializeField] float thighBend = 50f;
        [SerializeField] float kneeBend = 90f;
        [SerializeField] float crouchDrop = 0.24f;      // lowers the model so the bent legs keep the feet on the floor
        [SerializeField] float crouchBlendTime = 0.12f;
        [SerializeField] float eatArmRaise = 115f;

        // Bones the walk cycle (ProceduralWalk) re-poses from rest every frame, before this runs: the crouch is layered on
        // top of them and never reset, so he keeps walking (a short, slow shuffle at braced speed) in the braced pose.
        static readonly string[] WalkBones = { "Spine", "UpperArm_L", "UpperArm_R", "Thigh_L", "Thigh_R", "Shin_L", "Shin_R" };
        // Bones nothing else poses: reset to rest every frame this pose is active, and once when it ends.
        static readonly string[] HeldBones = { "Forearm_L", "Forearm_R", "Shield" };

        BlockArc arc;
        Transform model;
        float crouch;                       // 0 standing .. 1 fully crouched
        Vector3 crouchApplied;

        Energy energy;
        Grit grit;
        LimbPose pose;      // WalkBones
        LimbPose held;      // HeldBones
        bool posed;
        GameObject shieldGuard;
        Vector3 guardFacing = Vector3.forward;
        float stamina;
        float lastLowered = float.NegativeInfinity;
        float lockedUntil = float.NegativeInfinity;
        float[] rationReadyAt;              // when each charge is back (owner only)
        float eatPoseUntil = float.NegativeInfinity;
        bool wantRation;
        int toggleCueIndex = -1;

        public bool IsBracing { get; private set; }
        bool EatPosing => Time.time < eatPoseUntil;

        public float Stamina => stamina;
        public bool GuardBroken => Time.time < lockedUntil;
        public float MaxStamina => maxStamina * (UpgradeHas(BullgrynUpgrade.ReinforcedSlab) ? BullgrynUpgrade.ReinforcedSlabStaminaScale : 1f);

        public override string DisplayName => "Brace";
        public override string KeyLabel => "Space";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Ration Break";
        public override string ExDescription => "Ration Break";
        public override bool Available => Time.time >= lockedUntil && stamina >= minStaminaToRaise && !BullgrynKit.ShieldPlanted(gameObject);
        public override float CooldownRemaining => Mathf.Max(base.CooldownRemaining, lockedUntil - Time.time);
        // The shield's stamina, drawn as the faint curtain behind the slot's name.
        public override float ChargeRechargeNormalized => 1f - Mathf.Clamp01(stamina / MaxStamina);
        public float MoveSpeedMultiplier => IsBracing ? moveSpeedScale : 1f;

        // Ration Break's charges, shown as pips on the slot while Shift is held.
        public override int ExChargeCapacity => rationCharges;
        public override int ExChargesReady
        {
            get
            {
                int ready = 0;
                for (int i = 0; i < RationSlots.Length; i++) if (Time.time >= RationSlots[i]) ready++;
                return ready;
            }
        }
        protected override bool ExReady => ExChargesReady > 0;

        float[] RationSlots
        {
            get
            {
                int n = Mathf.Max(1, rationCharges);
                if (rationReadyAt == null || rationReadyAt.Length != n) rationReadyAt = new float[n];
                return rationReadyAt;
            }
        }

        void Reset()
        {
            cooldown = 0.2f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            grit = GetComponent<Grit>();
            model = transform.Find("Model");
            pose = new LimbPose(model, WalkBones);
            held = new LimbPose(model, HeldBones);
            stamina = maxStamina;
            BuildShieldGuard();
            // Drawn on every screen, so enemies can see which side is covered.
            arc = BlockArc.Create(transform, new Vector3(0f, 0f, arcForward), arcRadius, arcDegrees, arcHeight, BullgrynKit.Hazard);
            arc.gameObject.SetActive(false);
            // The shield's own bar, above his health bar.
            if (GetComponent<ShieldBar>() == null) gameObject.AddComponent<ShieldBar>();
        }

        bool UpgradeHas(int bit) => Anathema.Upgrades.UpgradeState.Has(gameObject, bit);

        // The solid plate in front of him while braced: enemy projectiles stop on it (it is part of him, so they "hit"
        // him and the block decides), and he can't collide with it himself.
        void BuildShieldGuard()
        {
            shieldGuard = new GameObject("Slabshield Guard");
            shieldGuard.transform.SetParent(transform, false);
            shieldGuard.transform.localPosition = new Vector3(0f, shieldSize.y * 0.5f, shieldForward);
            var box = shieldGuard.AddComponent<BoxCollider>();
            box.size = shieldSize;
            foreach (Collider own in GetComponents<Collider>()) Physics.IgnoreCollision(own, box);
            shieldGuard.SetActive(false);
        }

        protected override bool IsTriggered()
        {
            if (IsBracing || !Input.GetKeyDown(KeyCode.Space)) return false;
            if (BullgrynKit.ShieldPlanted(gameObject) || IsChargingOrHauling()) return false;
            wantRation = Input.GetKey(KeyCode.LeftShift);
            if (wantRation) return ExReady;
            return Time.time >= lockedUntil && stamina >= minStaminaToRaise;
        }

        bool IsChargingOrHauling()
        {
            return TryGetComponent(out BullCharge charge) && charge.IsCharging;
        }

        protected override void Activate()
        {
            // The owner decides whether this press is the Ration Break (and pays for it); replicas get the cue's EX flag.
            if (!IsReplica && wantRation && SpendRationCharge())
            {
                ExActivation = TrySpendEx(energy, exEnergyCost);
                if (!ExActivation) RefundRationCharge();
            }
            if (ExActivation)
            {
                Eat();
                return;
            }
            if (IsBracing) Lower();
            else Raise();
        }

        int spentSlot = -1;

        bool SpendRationCharge()
        {
            float[] slots = RationSlots;
            spentSlot = -1;
            for (int i = 0; i < slots.Length; i++)
                if (Time.time >= slots[i]) { spentSlot = i; break; }
            if (spentSlot < 0) return false;
            slots[spentSlot] = Time.time + rationCooldown;
            return true;
        }

        void RefundRationCharge()
        {
            if (spentSlot >= 0) RationSlots[spentSlot] = 0f;
            spentSlot = -1;
        }

        void Eat()
        {
            eatPoseUntil = Time.time + eatDuration;
            Sfx.Play(Sfx.MeleeImpact, 0.25f, 0.55f, 0.65f, 0.3f); // tearing into the ration
            // The heal is sent once, by the owner (it goes to the server when that is not this machine).
            if (!IsReplica) Health.Heal(Health.Max * healFraction, gameObject);
            Anathema.Abilities.Psyker.PsykerFx.Ring(transform.position + Vector3.up * 0.1f, 0.4f, 2.2f, 0.45f, BullgrynKit.Ration, 0.2f);
            Sfx.Play(Sfx.MagicImpact, 0.4f, 0.8f, 0.9f);
        }

        void Raise()
        {
            if (!IsReplica)
            {
                // Raising the shield drops anything else he was winding up.
                foreach (Ability other in GetComponents<Ability>())
                    if (other != this) other.InterruptForGuard();
            }
            IsBracing = true;
            guardFacing = BullgrynKit.FlatAim(Aim.AimDirection, transform);
            shieldGuard.SetActive(true);
            arc.gameObject.SetActive(true);
            Health.DamageModifier = OnIncomingDamage;
            Sfx.Play(Sfx.HammerSmash, 0.25f, 1.3f, 1.45f, 0.3f);
        }

        void Lower()
        {
            if (!IsBracing) return;
            IsBracing = false;
            lastLowered = Time.time;
            shieldGuard.SetActive(false);
            arc.gameObject.SetActive(false);
            if (Health.DamageModifier == (Func<float, GameObject, float>)OnIncomingDamage) Health.DamageModifier = null;
        }

        protected override void Cancel()
        {
            Lower();
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;

            if (IsBracing)
            {
                if (!IsReplica)
                {
                    if (IsDisabled)
                    {
                        InterruptSelf(); // stunned, silenced, panicked or shoved: the shield drops on every copy
                        return;
                    }
                    if (!Input.GetKey(KeyCode.Space))
                    {
                        Lower();
                        SendToggleCue();
                        return;
                    }
                }

                stamina = Mathf.Max(0f, stamina - holdDrainPerSecond * dt);
                Vector3 want = BullgrynKit.FlatAim(Aim.AimDirection, transform);
                guardFacing = Vector3.RotateTowards(guardFacing, want, turnRate * Mathf.Deg2Rad * dt, 0f);
                // Face the guard, overriding MouseAim's snap to the cursor. This must happen in Update: turning the
                // character in LateUpdate would swing the camera-facing health bars round, and would rotate the
                // movement code's world-space model smoothing offset before it is taken back off (the model drifts).
                if (!Health.IsDead) transform.rotation = Quaternion.LookRotation(guardFacing, Vector3.up);
                if (stamina <= 0f && Health.IsAuthority) ResolveGuardBreakOnAuthority(0f);
            }
            else if (Time.time - lastLowered >= regenDelay)
            {
                stamina = Mathf.Min(MaxStamina, stamina + regenPerSecond * dt);
            }
        }

        // Owner: lowering the shield is the same ability cue again; every other copy toggles it down.
        void SendToggleCue()
        {
            if (Health.Net == null || !Health.Net.IsSpawned) return;
            if (toggleCueIndex < 0) toggleCueIndex = Array.IndexOf(GetComponents<Ability>(), this);
            Health.Net.SendCue(toggleCueIndex, Aim.AimPoint);
        }

        // Runs where damage is resolved (the server's copy online, this machine offline).
        float OnIncomingDamage(float amount, GameObject source)
        {
            if (!IsBracing || source == null || source == gameObject) return amount;
            // Judged from the block's centre, just in front of him (the arc drawn on screen uses the same centre).
            Vector3 to = source.transform.position - (transform.position + guardFacing * arcForward);
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f || Vector3.Angle(guardFacing, to) > arcDegrees * 0.5f) return amount;

            ResolveGuardBreakOnAuthority(amount);
            return 0f;
        }

        void ResolveGuardBreakOnAuthority(float blocked)
        {
            bool breaks = stamina - blocked <= 0f;
            Blocked(blocked, breaks);
            if (breaks && guardBreakStagger > 0f) Stunned.Apply(gameObject, guardBreakStagger); // held by the server, forwarded to the owner
            if (Health.Net != null && Health.Net.IsSpawned) Health.Net.ServerShieldBlocked(blocked, breaks);
        }

        // Every other machine: the server's copy blocked a hit (and maybe broke the guard).
        public void BlockedFromServer(float amount, bool guardBreak)
        {
            Blocked(amount, guardBreak);
        }

        void Blocked(float amount, bool guardBreak)
        {
            if (amount > 0f)
            {
                stamina = Mathf.Max(0f, stamina - amount);
                if (grit != null) grit.AddBlocked(amount);
                if (!IsReplica && energy != null) energy.Gain(amount * energyPerBlockedDamage);
                arc.Flash();
                Vector3 spark = transform.position + Vector3.up * 1.4f + guardFacing * shieldForward;
                Anathema.Abilities.SkitariiRanger.RangerFx.Tracer(spark - transform.right * 0.5f, spark + transform.right * 0.5f, 0.1f, BullgrynKit.Hazard, 0.12f);
                Sfx.Play(Sfx.ChainswordImpact, 0.35f, 1.3f, 1.5f, 0.25f);
            }

            if (!guardBreak) return;
            Lower();
            stamina = 0f;
            lockedUntil = Time.time + guardBreakLockout;
            Anathema.Abilities.Psyker.PsykerFx.Ring(transform.position + Vector3.up * 0.1f, 0.3f, 1.8f, 0.3f, BullgrynKit.Hazard, 0.18f);
            Sfx.Play(Sfx.SlamExplosion, 0.45f, 1.2f, 1.3f, 0.5f);
        }

        void LateUpdate()
        {
            // Last frame's crouch offset comes off first (the movement code eases the model separately).
            if (model != null && crouchApplied != Vector3.zero)
            {
                model.position -= crouchApplied;
                crouchApplied = Vector3.zero;
            }
            crouch = Mathf.MoveTowards(crouch, IsBracing && !Health.IsDead ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, crouchBlendTime));

            if (crouch <= 0f && !EatPosing)
            {
                if (posed)
                {
                    held.Reset();
                    posed = false;
                }
                return;
            }

            posed = true;
            held.Reset();
            if (crouch > 0f)
            {
                // Sink low behind the shield: knees bent (the model drops so the feet stay planted), body hunched
                // forward into it, and the shield arm swung across the front.
                float k = Mathf.SmoothStep(0f, 1f, crouch);
                Vector3 right = transform.right;
                pose.Rotate("Spine", right, braceLean * k);
                pose.Rotate("UpperArm_L", Vector3.up, shieldAcross * k);
                pose.Rotate("UpperArm_L", right, -shieldArmRaise * k);
                held.Rotate("Forearm_L", right, -shieldArmRaise * 0.5f * k);
                held.Rotate("Shield", Vector3.up, -shieldSquare * k);
                pose.Rotate("UpperArm_R", right, -20f * k);
                foreach (string side in new[] { "L", "R" })
                {
                    pose.Rotate("Thigh_" + side, right, -thighBend * k);
                    pose.Rotate("Shin_" + side, right, kneeBend * k);
                }
                if (model != null)
                {
                    crouchApplied = Vector3.down * (crouchDrop * k);
                    model.position += crouchApplied;
                }
            }
            else
            {
                pose.Rotate("UpperArm_R", transform.right, -eatArmRaise);
                held.Rotate("Forearm_R", transform.right, -eatArmRaise * 0.5f);
            }
        }
    }
}
