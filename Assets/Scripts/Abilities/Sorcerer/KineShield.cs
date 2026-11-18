using System;
using Anathema.Abilities.Bullgryn;
using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // Q: Kine Shield. The Sorcerer throws up a half-circle of telekinetic force in front of him for a moment. Attacks and
    // projectiles from the front are negated; melee attackers are hurled back. The first enemy bolt it stops is held
    // spinning in the shield: press Q again within a few seconds to fling it back along your aim, now his.
    //
    // EX (Shift+Q): Warp Rift. Tear open a rift at the cursor instead: enemies inside are slowed and enemy projectiles
    // that enter it are turned back (a ReflectDome).
    //
    // Online: projectiles fly on every machine, so each copy of the shield snatches its own copy of the bolt (like the
    // reflect dome), and a bolt caught on the shooter's machine never sends a hit. Direct hits are judged on the server's
    // copy (DamageModifier), which also hurls melee attackers back.
    public class KineShield : Ability, IProjectileCatcher, IMoveSpeedModifier
    {
        [SerializeField] float shieldDuration = 1.2f;
        [SerializeField] float arcDegrees = 180f;
        [SerializeField] float catchRadius = 1.9f;
        [SerializeField] float meleeRange = 3.6f;
        [SerializeField] float meleeKnockback = 2.5f;
        [SerializeField] float meleeKnockbackDuration = 0.25f;
        [SerializeField] float moveSpeedScale = 0.7f;
        [SerializeField] float recastWindow = 4.5f;
        [SerializeField] float throwRange = 14f;
        [SerializeField] float throwMinSpeed = 24f;
        [SerializeField] float throwRadius = 0.35f;
        [SerializeField] float throwEnergyPerHit = 8f;

        [Header("EX: Warp Rift")]
        [SerializeField] float exEnergyCost = 25f;
        [SerializeField] float riftRange = 9f;
        [SerializeField] float riftRadius = 2.6f;
        [SerializeField] float riftDuration = 2f;
        [SerializeField] float riftSlow = 0.4f;

        static readonly Color ShieldColor = new Color(0.35f, 0.8f, 1f, 0.85f);

        Energy energy;
        BlockArc arc;
        bool shieldUp;
        float shieldEnd;
        float lastRaise = float.NegativeInfinity;
        float raiseCooldownEnd;
        int negated; // attacks negated since the shield went up (Kinetic Ward)

        // The caught bolt: held until thrown or until the window closes.
        bool holding;
        float holdingUntil;
        float caughtDamage;
        float caughtSpeed;
        Transform heldOrb;
        Material heldMaterial;

        public override string DisplayName => holding ? "Kine Shield (throw)" : "Kine Shield";
        public override string KeyLabel => "Q";
        public override float ExCost => holding ? 0f : exEnergyCost;
        public override string ExName => "Warp Rift";
        public override string ExDescription => "Warp Rift";
        public float MoveSpeedMultiplier => shieldUp ? moveSpeedScale : 1f;

        // A held bolt can be thrown whatever the cooldown says.
        protected override bool HasSpareCharge => holding;

        void Reset()
        {
            cooldown = 8f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            // Drawn on every screen, so enemies can see which side is covered.
            arc = BlockArc.Create(transform, Vector3.zero, catchRadius, arcDegrees, 2.4f, ShieldColor);
            arc.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            Projectile.Catchers.Remove(this);
            if (heldMaterial != null) Destroy(heldMaterial);
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Q);

        protected override void Activate()
        {
            if (!IsReplica)
            {
                if (holding)
                {
                    Throw();
                    return;
                }
                ExActivation = TrySpendEx(energy, exEnergyCost);
                if (ExActivation) Rift();
                else Raise();
                return;
            }

            // Replica: the owner can't raise again before the cooldown, so a cue soon after a raise is the throw (even if
            // this copy happened to miss the catch).
            if (ExActivation) Rift();
            else if (holding || Time.time - lastRaise < shieldDuration + recastWindow + 0.3f) Throw();
            else Raise();
        }

        void Raise()
        {
            shieldUp = true;
            negated = 0;
            lastRaise = Time.time;
            shieldEnd = Time.time + shieldDuration;
            raiseCooldownEnd = Time.time + cooldown * CooldownScale;
            arc.gameObject.SetActive(true);
            if (!Projectile.Catchers.Contains(this)) Projectile.Catchers.Add(this);
            Health.DamageModifier = OnIncomingDamage;
            Anathema.Abilities.Psyker.PsykerFx.Arc(transform.position, transform.forward, arcDegrees * 0.5f, catchRadius, 0.25f, ShieldColor);
            Sfx.Play(Sfx.TeleportCrackle, 0.35f, 1.3f, 1.45f, 0.5f);
        }

        void Lower()
        {
            if (!shieldUp) return;
            shieldUp = false;
            arc.gameObject.SetActive(false);
            Projectile.Catchers.Remove(this);
            if (Health.DamageModifier == (Func<float, GameObject, float>)OnIncomingDamage) Health.DamageModifier = null;
        }

        void Rift()
        {
            Vector3 offset = Aim.AimPoint - transform.position;
            offset.y = 0f;
            if (offset.magnitude > riftRange) offset = offset.normalized * riftRange;
            Vector3 spot = transform.position + offset;
            ReflectDome.Spawn(gameObject, spot, riftRadius, riftDuration, riftSlow, SorcererFx.Warp);
            Sfx.Play(Sfx.ElectricShock, 0.35f, 0.7f, 0.8f, 0.6f);
        }

        // Every machine, for its own copy of each enemy projectile.
        public bool TryCatch(Projectile projectile)
        {
            if (!shieldUp || Health.IsDead || projectile.OwnerTeam == Health.Team) return false;
            Vector3 to = projectile.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > catchRadius || Vector3.Angle(transform.forward, to) > arcDegrees * 0.5f) return false;

            arc.Flash();
            Sfx.Play(Sfx.MagicImpact, 0.35f, 1.4f, 1.6f, 0.4f);
            if (!IsReplica) Ward();
            if (!holding)
            {
                holding = true;
                holdingUntil = Time.time + recastWindow;
                caughtDamage = projectile.Damage;
                caughtSpeed = projectile.Speed;
                ShowHeld(true);
            }
            return true;
        }

        // Where damage is resolved (the server's copy online). Direct hits from the front are negated; a melee attacker
        // close enough is hurled back.
        float OnIncomingDamage(float amount, GameObject source)
        {
            if (!shieldUp || source == null || source == gameObject) return amount;
            Vector3 to = source.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f || Vector3.Angle(transform.forward, to) > arcDegrees * 0.5f) return amount;

            arc.Flash();
            // Projectiles are rewarded where they are caught (the owner); direct hits here, where they are judged.
            if (Health.IsAuthority) Ward();
            if (to.magnitude <= meleeRange && source.TryGetComponent(out Health attacker) && attacker.Team != Health.Team)
                Knockback.Apply(source, to.normalized, meleeKnockback, meleeKnockbackDuration);
            return 0f;
        }

        // Kinetic Ward (upgrade): every negated attack gives a barrier, smaller each time.
        void Ward()
        {
            if (!SorcererUpgrade.Has(gameObject, SorcererUpgrade.KineticWard)) return;
            float[] steps = SorcererUpgrade.KineticWardSteps;
            float amount = steps[Mathf.Min(negated, steps.Length - 1)];
            negated++;
            float cap = 0f;
            foreach (float a in steps) cap += a;
            Barrier.Grant(gameObject, amount, SorcererUpgrade.KineticWardSeconds, cap, gameObject);
        }

        void Throw()
        {
            Vector3 dir = SorcererFx.Flat(Aim.AimDirection, transform);
            Vector3 origin = transform.position + Vector3.up * 1.5f + dir * 0.9f;
            float damage = caughtDamage;
            float speed = Mathf.Max(throwMinSpeed, caughtSpeed);
            holding = false;
            ShowHeld(false);
            Lower();

            GameObject bolt = SorcererFx.Bolt("Kine Throw", origin, throwRadius, SorcererFx.Warp, SorcererFx.Sand);
            Sfx.Play(Sfx.PsycannonShot, 0.4f, 0.9f, 1f, 0.5f);
            bolt.AddComponent<Projectile>().Init(gameObject, Health.Team, Mathf.Max(1f, damage), speed, throwRange, throwRadius, dir, OnThrowHit);

            // The throw doesn't restart the shield's cooldown.
            if (!IsReplica) SetCooldownRemaining(raiseCooldownEnd - Time.time);
        }

        void OnThrowHit(Health target)
        {
            HitMarkerSound.Play();
            if (energy != null) energy.Gain(throwEnergyPerHit);
        }

        void ShowHeld(bool on)
        {
            if (on && heldOrb == null)
            {
                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Held Bolt";
                Destroy(orb.GetComponent<Collider>());
                heldMaterial = VisualUtil.UnlitMaterial(SorcererFx.Warp, 3003);
                orb.GetComponent<Renderer>().sharedMaterial = heldMaterial;
                orb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                orb.transform.SetParent(transform, false);
                heldOrb = orb.transform;
            }
            else if (!on && heldOrb != null)
            {
                Destroy(heldOrb.gameObject);
                heldOrb = null;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (shieldUp && (Time.time >= shieldEnd || Health.IsDead)) Lower();
            if (holding && (Time.time >= holdingUntil || Health.IsDead))
            {
                holding = false;
                ShowHeld(false);
                Anathema.Abilities.Psyker.PsykerFx.Ring(transform.position + Vector3.up * 1.4f + transform.forward * 0.8f, 0.4f, 0.05f, 0.25f, SorcererFx.Warp, 0.08f);
            }
            if (heldOrb != null)
            {
                // The bolt spins in the crook of the staff, in front of him.
                float pulse = 0.32f + 0.05f * Mathf.Sin(Time.time * 16f);
                heldOrb.localScale = Vector3.one * pulse;
                heldOrb.localPosition = new Vector3(0.45f + 0.1f * Mathf.Cos(Time.time * 7f), 1.7f, 0.75f + 0.1f * Mathf.Sin(Time.time * 7f));
            }
        }

        protected override void Cancel()
        {
            Lower();
            if (Health != null && Health.IsDead)
            {
                holding = false;
                ShowHeld(false);
            }
        }
    }
}
