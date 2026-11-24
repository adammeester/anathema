using System.Collections.Generic;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.CameraRig;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // LMB (click or hold): the Sanguinary Guard's ornate power sword. Alternating forehand and backhand cuts in a frontal arc.
    // The Armament upgrades swap the weapon (GuardUpgrade): the Encarmine Axe chops slower and harder and its Arc is
    // Executioner's Cleave, a straight overhead line; the Glaive Encarmine thrusts long and narrow and its Arc is Twin
    // Crescent, two spins at 70% damage each.
    // Plain cuts only deal damage and build energy: no lifesteal. The blood comes back on the Exsanguinating Arc.
    //
    // Exsanguinating Arc: with a full Vitae chalice (or while Wings of the Angel lasts) the next swing is the Arc, a spin
    // slash. A ring fills on the ground around him during a longer wind-up, then he spins a full turn and a quarter with the
    // blade at arm's length: everything around him is cut and slowed. Every landed hit draws a large share of its damage
    // back into him (that is how he wins back the health he spends healing), and heals each other teammate near him for
    // a share of its damage. During Wings of the Angel each landed Arc also brings his next Chalice Offering charge forward. The Arc's own healing never refills the chalice. Online "Arc" travels as the cue's EX flag.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes the spine and arms every LateUpdate
    public class Chainsword : Ability
    {
        [Header("Chainsword")]
        [SerializeField] float damage = 9f;
        [SerializeField] float range = 2.6f;
        [SerializeField] float arcDegrees = 100f;
        [SerializeField] float windupDuration = 0.12f;
        [SerializeField] float swingDuration = 0.12f;
        [SerializeField] float recoverDuration = 0.2f;
        [SerializeField] float energyPerHit = 6f;

        [Header("Exsanguinating Arc")]
        [SerializeField] float arcWindup = 0.25f;
        [SerializeField] float arcSpinDuration = 0.3f;
        [SerializeField] float arcRecover = 0.25f;
        [SerializeField] float arcRange = 3.3f;
        [SerializeField] float arcDamage = 28f;
        [SerializeField] float arcSlow = 0.3f;
        [SerializeField] float arcSlowDuration = 1f;
        [SerializeField] float arcLifesteal = 0.8f;         // share of each landed Arc hit that heals him
        [SerializeField] float arcBloodReturn = 0.5f;       // Blood Returns: share of the health paid since the last landed Arc that it heals back
        [SerializeField] float arcHealShare = 0.4f;         // share of each landed Arc hit that heals each other teammate
        [SerializeField] float arcHealRadius = 6f;
        [SerializeField] float arcEnergyPerHit = 10f;
        [SerializeField] float arcSpinDegrees = 450f;
        [SerializeField] float ascendedCooldown = 0.9f;
        [SerializeField] float ascendedChaliceRefund = 0.25f; // during Wings of the Angel, each landed Arc brings the next Chalice charge forward by this much of its recharge

        [Header("Pose")]
        [SerializeField] float spineTwist = 35f;
        [SerializeField] float armRaise = 80f;
        [SerializeField] float crouchLean = 22f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_R", "Forearm_R" };

        Energy energy;
        Vitae vitae;
        WingsOfTheAngel wings;
        ChaliceOffering chalice;
        CrimsonRush rush;   // Blade Hurl takes the sword out of his hand
        BloodBladeEffect blade;
        LimbPose pose;
        Transform model;
        bool posed;
        bool modelSpun;
        bool active;
        bool strikePending;
        bool arc;
        float side = 1f;
        float strikeAt, swingEnd, recoverEnd, curWindup, curSwing, curRecover;
        GameObject telegraph;
        float secondSpinAt = -1f;                                   // Glaive: when Twin Crescent's second spin lands
        readonly HashSet<Health> firstSpinHits = new HashSet<Health>();


        public override string DisplayName => Weapon == GuardWeapon.EncarmineAxe ? "Encarmine Axe" : Weapon == GuardWeapon.GlaiveEncarmine ? "Glaive Encarmine" : "Power Sword";
        public override string KeyLabel => "LMB";
        public override bool ShowsEmpowered => vitae != null && (vitae.Full || Ascended);
        protected override bool ScalesWithAttackSpeed => true;

        bool Ascended => wings != null && wings.Ascended;

        // The weapon in hand (Armament upgrades) sets the cuts, the cadence and the shape of the Arc.
        GuardWeapon Weapon => GuardUpgrade.WeaponOf(gameObject);
        float SwingDamage => Weapon == GuardWeapon.EncarmineAxe ? GuardUpgrade.AxeDamage : Weapon == GuardWeapon.GlaiveEncarmine ? GuardUpgrade.GlaiveDamage : damage;
        float SwingRange => Weapon == GuardWeapon.EncarmineAxe ? GuardUpgrade.AxeRange : Weapon == GuardWeapon.GlaiveEncarmine ? GuardUpgrade.GlaiveRange : range;
        float SwingDegrees => Weapon == GuardWeapon.EncarmineAxe ? GuardUpgrade.AxeDegrees : Weapon == GuardWeapon.GlaiveEncarmine ? GuardUpgrade.GlaiveDegrees : arcDegrees;
        float SwingCooldown => Weapon == GuardWeapon.EncarmineAxe ? GuardUpgrade.AxeCooldown : cooldown;
        // Brimming Chalice makes every Arc hit harder.
        float ArcDamage => arcDamage * (GuardUpgrade.Has(gameObject, GuardUpgrade.BrimmingChalice) ? GuardUpgrade.BrimmingArcDamage : 1f);

        void Reset()
        {
            cooldown = 0.7f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            vitae = GetComponent<Vitae>();
            wings = GetComponent<WingsOfTheAngel>();
            chalice = GetComponent<ChaliceOffering>();
            rush = GetComponent<CrimsonRush>();
            model = transform.Find("Model");
            pose = new LimbPose(model, BoneNames);
        }

        void Start()
        {
            blade = BloodBladeEffect.Create(model, vitae);
        }

        public override void ShowReplicaEmpowered(bool on)
        {
            if (vitae != null) vitae.SetReplicaFull(on);
        }

        // No sword in hand (thrown by Blade Hurl): no cut, no Arc, and the icon greys out.
        bool Disarmed => rush != null && rush.Disarmed;
        public override bool Available => !Disarmed;

        protected override bool IsTriggered()
        {
            if (PrimaryFireBlocked || Disarmed) return false;
            return Input.GetMouseButton(0);
        }

        protected override void Activate()
        {
            // The owner decides whether this swing is the Arc; replicas are told through the cue's EX flag.
            if (!IsReplica) ExActivation = vitae != null && (vitae.Full || Ascended);
            arc = ExActivation;
            // A full chalice is spent by the Arc; Wings of the Angel's free Arcs leave it alone.
            if (arc && !IsReplica && vitae != null && vitae.Full && !Ascended) vitae.Spend();

            side = -side;
            curWindup = arc ? arcWindup : windupDuration;
            curSwing = arc ? arcSpinDuration + (Weapon == GuardWeapon.GlaiveEncarmine ? GuardUpgrade.CrescentGap : 0f) : swingDuration;
            curRecover = arc ? arcRecover : recoverDuration;
            strikeAt = Time.time + curWindup;
            swingEnd = strikeAt + curSwing;
            recoverEnd = swingEnd + curRecover;
            strikePending = true;
            active = true;

            if (arc)
            {
                if (telegraph != null) Destroy(telegraph);
                if (Weapon == GuardWeapon.EncarmineAxe)
                    telegraph = Bullgryn.BullgrynKit.GroundLane(transform.position, SanguinaryKit.FlatAim(Aim.AimDirection, transform),
                        GuardUpgrade.CleaveLength, GuardUpgrade.CleaveWidth, curWindup, SanguinaryKit.Blood);
                else
                    telegraph = Bullgryn.BullgrynKit.GroundCircle(transform.position, arcRange, curWindup, SanguinaryKit.Blood);
                telegraph.transform.SetParent(transform, true);
                Sfx.Play(Sfx.TeleportCrackle, 0.3f, 0.7f, 0.8f, 0.5f); // the power field surging before the arc
            }
        }

        protected override void Cancel()
        {
            if (!active) return;
            if (strikePending)
            {
                RefundCooldown();
                // A cancelled Arc gives the chalice back (it was spent as the wind-up began).
                if (arc && !IsReplica && vitae != null && !Ascended) vitae.Add(vitae.Max);
            }
            if (telegraph != null) Destroy(telegraph);
            strikePending = false;
            secondSpinAt = -1f;
            active = false;
            recoverEnd = Time.time + curRecover;
        }

        protected override void Update()
        {
            // Wings of the Angel: every swing is an Arc, on a slower cadence (only changed while ready, so the HUD's
            // cooldown sweep stays consistent).
            if (CooldownRemaining <= 0f && cooldown > 0f) CooldownScale = (Ascended ? ascendedCooldown : SwingCooldown) / cooldown;
            base.Update();
            if (strikePending && !IsReplica && IsDisabled) InterruptSelf();

            if (strikePending && Time.time >= strikeAt)
            {
                strikePending = false;
                if (arc) ArcStrike();
                else Swing();
            }
            if (secondSpinAt > 0f && Time.time >= secondSpinAt)
            {
                secondSpinAt = -1f;
                SpinStrike(GuardUpgrade.CrescentShare, false, null, firstSpinHits);
            }
        }

        void Swing()
        {
            Vector3 dir = SanguinaryKit.FlatAim(Aim.AimDirection, transform);
            SlashEffect.Spawn(transform.position, dir, SwingRange, SwingDegrees, swingDuration * 2f, SanguinaryKit.Blood);
            MeleeSound.PowerSwing(0.4f);

            if (IsReplica) return;

            bool marked = false;
            float dealt = SwingDamage;
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, transform.position, dir, SwingRange, SwingDegrees).ToArray())
            {
                enemy.Strike(Hit.Direct(dealt), gameObject, landed =>
                {
                    if (!landed || this == null) return;
                    if (energy != null) energy.Gain(energyPerHit);
                    SanguinaryKit.FeedThirst(gameObject, dealt);
                    if (!marked)
                    {
                        marked = true;
                        MeleeSound.PowerImpact(0.35f);
                    }
                });
            }
        }

        // The Arc for the weapon he holds: the sword's spin, the Glaive's two spins, or the Axe's straight cleave.
        void ArcStrike()
        {
            if (telegraph != null) Destroy(telegraph);
            switch (Weapon)
            {
                case GuardWeapon.EncarmineAxe:
                    CleaveStrike();
                    break;
                case GuardWeapon.GlaiveEncarmine:
                    // Twin Crescent: this spin now, the second one a moment later (Update).
                    firstSpinHits.Clear();
                    SpinStrike(GuardUpgrade.CrescentShare, true, firstSpinHits, null);
                    secondSpinAt = Time.time + GuardUpgrade.CrescentGap;
                    break;
                default:
                    SpinStrike(1f, true, null, null);
                    break;
            }
        }

        void SpinStrike(float share, bool first, HashSet<Health> record, HashSet<Health> dancing)
        {
            Vector3 here = transform.position;
            Vector3 dir = SanguinaryKit.FlatAim(Aim.AimDirection, transform);

            // The spin: two blood crescents chasing each other round him, a ring flashing outward and a burst of mist.
            SlashEffect.Spawn(here, dir, arcRange, 360f, arcSpinDuration + 0.35f, SanguinaryKit.Blood);
            SlashEffect.Spawn(here, -dir, arcRange * 0.8f, 360f, arcSpinDuration + 0.2f, SanguinaryKit.BrightBlood);
            Psyker.PsykerFx.Ring(here + Vector3.up * 0.1f, 0.4f, arcRange + 0.4f, 0.3f, SanguinaryKit.BrightBlood, 0.25f);
            SanguinaryKit.Mist(here, arcRange * 0.9f, 0.6f);
            MeleeSound.PowerSwing(0.55f, first ? 0.75f : 0.9f, first ? 0.85f : 1f);
            if (first) Sfx.Play(Sfx.MagicImpact, 0.4f, 0.55f, 0.65f, 0.8f);

            List<Health> targets = Mandrake.MandrakeKit.EnemiesInCone(Health, here, dir, arcRange, 360f);
            ArcHits(targets, here, ArcDamage * share, arcSlow, first, record, dancing);
            if (!IsReplica) Anathema.Arena.CasketField.Cone(gameObject, here, dir, arcRange, 360f);
        }

        // Executioner's Cleave (Encarmine Axe): one overhead blow down a straight line in front of him.
        void CleaveStrike()
        {
            Vector3 here = transform.position;
            Vector3 dir = SanguinaryKit.FlatAim(Aim.AimDirection, transform);
            float length = GuardUpgrade.CleaveLength, half = GuardUpgrade.CleaveWidth * 0.5f;

            SlashEffect.Spawn(here, dir, length, 28f, 0.5f, SanguinaryKit.Blood);
            SlashEffect.Spawn(here, dir, length * 0.8f, 18f, 0.35f, SanguinaryKit.BrightBlood);
            for (int i = 1; i <= 3; i++) SanguinaryKit.Mist(here + dir * (length * i / 3.5f), 0.7f, 0.45f);
            BloodSplatter.Spawn(here + dir * (length * 0.6f), 1.2f);
            MeleeSound.PowerSwing(0.6f, 0.6f, 0.7f);
            Sfx.Play(Sfx.HammerSmash, 0.5f, 0.7f, 0.8f, 0.6f);

            var targets = new List<Health>();
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, here, dir, length + half, 360f))
            {
                Vector3 to = SanguinaryKit.Flat(enemy.transform.position - here);
                float along = Vector3.Dot(to, dir);
                if (along < -0.3f || along > length + 0.3f) continue;
                if ((to - dir * along).magnitude > half + 0.4f) continue;
                targets.Add(enemy);
            }
            float slow = GuardUpgrade.Has(gameObject, GuardUpgrade.HeadsmansDue) ? GuardUpgrade.HeadsmanSlow : arcSlow;
            ArcHits(targets, here, ArcDamage * (GuardUpgrade.CleaveDamage / arcDamage), slow, true, null, null);
            if (!IsReplica) Anathema.Arena.CasketField.Cone(gameObject, here, dir, length, 30f);
        }

        // One Arc blow landing on `targets`: damage and slow, lifesteal to him, a share to every other teammate near
        // him, and (first blow only) the Wings of the Angel chalice refund. `record` collects who was hit; a hit on
        // someone in `dancing` (Glaive, second spin) pays Dancing Blade's energy once.
        void ArcHits(List<Health> targets, Vector3 here, float dealt, float slow, bool first, HashSet<Health> record, HashSet<Health> dancing)
        {
            int hits = targets.Count;
            if (hits > 0) Sfx.Play(Sfx.HammerSmash, 0.45f, 0.8f, 0.9f, 0.6f);

            if (IsReplica)
            {
                if (hits > 0)
                {
                    foreach (Health enemy in targets.ToArray())
                        SanguinaryKit.BloodStream(enemy.transform.position, transform);
                    foreach (Health ally in SanguinaryKit.AlliesNear(Health, here, arcHealRadius, includeSelf: false).ToArray())
                        SanguinaryKit.BloodStream(here, ally.transform);
                }
                return;
            }

            if (first) TopDownCamera.Shake(0.18f, 0.25f);
            bool marked = false, danced = false;
            bool dancingBlade = dancing != null && GuardUpgrade.Has(gameObject, GuardUpgrade.DancingBlade);
            foreach (Health enemy in targets.ToArray())
            {
                Hit hit = Hit.Direct(dealt).WithSlow(arcSlowDuration, slow);
                enemy.Strike(hit, gameObject, landed =>
                {
                    if (!landed || this == null) return;
                    if (energy != null) energy.Gain(arcEnergyPerHit);
                    record?.Add(enemy);
                    if (dancingBlade && !danced && dancing.Contains(enemy))
                    {
                        danced = true;
                        if (energy != null) energy.Gain(GuardUpgrade.DancingBladeEnergy);
                    }
                    if (!marked)
                    {
                        marked = true;
                        HitMarkerSound.Play();
                        // Once per Arc that lands during Wings of the Angel: the chalice refills faster.
                        if (first && Ascended && chalice != null) chalice.RefundCharge(ascendedChaliceRefund);
                        // Blood Returns: the first blow of a landed Arc pays back part of the health he has bled since the last.
                        if (first && vitae != null)
                        {
                            float back = vitae.TakePaidSinceArc() * arcBloodReturn;
                            if (back > 0f) SanguinaryKit.Heal(gameObject, Health, back, feedsVitae: false);
                        }
                        MeleeSound.Impact(hits);
                    }
                    BloodImpact.Play(enemy.transform.position + Vector3.up, SanguinaryKit.Flat(enemy.transform.position - transform.position), 1.2f);
                    // Lifesteal: most of the cut's blood comes back into him...
                    SanguinaryKit.Heal(gameObject, Health, dealt * arcLifesteal, feedsVitae: false);
                    SanguinaryKit.BloodStream(enemy.transform.position, transform);
                    // ...and a share goes to every other teammate near him. None of it refills the chalice.
                    foreach (Health ally in SanguinaryKit.AlliesNear(Health, transform.position, arcHealRadius, includeSelf: false).ToArray())
                    {
                        SanguinaryKit.Heal(gameObject, ally, dealt * arcHealShare, feedsVitae: false);
                        SanguinaryKit.BloodStream(enemy.transform.position, ally.transform);
                    }
                });
            }
        }

        void LateUpdate()
        {
            float now = Time.time;
            if (!active && now >= recoverEnd)
            {
                EndPose();
                return;
            }

            float twist, arm, lean = 0f, spin = 0f;
            if (arc && Weapon == GuardWeapon.EncarmineAxe)
            {
                // Executioner's Cleave: the axe raised high overhead, then brought straight down. No spin.
                if (active && now < strikeAt)
                {
                    float t = Mathf.SmoothStep(0f, 1f, 1f - (strikeAt - now) / Mathf.Max(0.01f, curWindup));
                    twist = 0f;
                    arm = Mathf.Lerp(0f, -armRaise * 1.5f, t);
                    lean = Mathf.Lerp(0f, -10f, t);
                }
                else if (now < swingEnd)
                {
                    float t = 1f - (swingEnd - now) / Mathf.Max(0.01f, curSwing);
                    twist = 0f;
                    arm = Mathf.Lerp(-armRaise * 1.5f, 25f, t);
                    lean = Mathf.Lerp(-10f, crouchLean, t);
                }
                else
                {
                    active = false;
                    float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEnd - now) / Mathf.Max(0.01f, curRecover));
                    twist = 0f;
                    arm = Mathf.Lerp(25f, 0f, t);
                    lean = Mathf.Lerp(crouchLean, 0f, t);
                }
            }
            else if (arc)
            {
                if (active && now < strikeAt)
                {
                    // Crouch, blade drawn back low behind him.
                    float t = Mathf.SmoothStep(0f, 1f, 1f - (strikeAt - now) / Mathf.Max(0.01f, curWindup));
                    twist = Mathf.Lerp(0f, spineTwist * 1.4f, t);
                    arm = Mathf.Lerp(0f, 40f, t);
                    lean = Mathf.Lerp(0f, crouchLean, t);
                }
                else if (now < swingEnd)
                {
                    // The spin: the whole body turns, the blade held out at arm's length.
                    float t = 1f - (swingEnd - now) / Mathf.Max(0.01f, curSwing);
                    t = 1f - (1f - t) * (1f - t);
                    // The Glaive's Twin Crescent turns a full extra time round.
                    spin = (Weapon == GuardWeapon.GlaiveEncarmine ? arcSpinDegrees + 360f : arcSpinDegrees) * t;
                    twist = Mathf.Lerp(spineTwist * 1.4f, 0f, t);
                    arm = -armRaise * 0.9f;
                    lean = Mathf.Lerp(crouchLean, crouchLean * 0.4f, t);
                }
                else
                {
                    active = false;
                    float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEnd - now) / Mathf.Max(0.01f, curRecover));
                    twist = 0f;
                    arm = Mathf.Lerp(-armRaise * 0.9f, 0f, t);
                    lean = Mathf.Lerp(crouchLean * 0.4f, 0f, t);
                }
            }
            else
            {
                float s = spineTwist * side;
                if (active && now < strikeAt)
                {
                    float t = Mathf.SmoothStep(0f, 1f, 1f - (strikeAt - now) / Mathf.Max(0.01f, curWindup));
                    twist = Mathf.Lerp(0f, s, t);
                    arm = Mathf.Lerp(0f, -armRaise, t);
                }
                else if (now < swingEnd)
                {
                    float t = 1f - (swingEnd - now) / Mathf.Max(0.01f, curSwing);
                    twist = Mathf.Lerp(s, -s, t);
                    arm = Mathf.Lerp(-armRaise, 10f, t);
                }
                else
                {
                    active = false;
                    float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEnd - now) / Mathf.Max(0.01f, curRecover));
                    twist = Mathf.Lerp(-s, 0f, t);
                    arm = Mathf.Lerp(10f, 0f, t);
                }
            }

            posed = true;
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, twist);
            if (lean != 0f) pose.Rotate("Spine", transform.right, lean);
            pose.Rotate("UpperArm_R", transform.right, arm);
            if (model != null && (arc || modelSpun))
            {
                model.localRotation = Quaternion.Euler(0f, spin, 0f);
                modelSpun = spin != 0f;
            }
        }

        void EndPose()
        {
            if (modelSpun && model != null) model.localRotation = Quaternion.identity;
            modelSpun = false;
            if (!posed) return;
            pose.Reset();
            posed = false;
        }

        void OnDestroy()
        {
            if (telegraph != null) Destroy(telegraph);
            if (blade != null) Destroy(blade.gameObject);
        }
    }
}
