using System.Collections.Generic;
using Anathema.Abilities.Psyker;
using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    public enum GreyKnightPose { None, HammerSweep, HammerSlam, CannonOvercharge }

    // Everything the Grey Knight's weapon upgrades add, in one component (added by UpgradeState at runtime, so the prefab
    // needs no rebuild; it runs on every machine and follows the synced upgrade mask).
    //
    //   Visuals  - the model is one skinned mesh carrying every weapon, each weighted to its own bone (Art/grey_knight.py):
    //              Halberd + Bolter (the starting kit), Daemonhammer, Psycannon and the white Purifier helm. The ones not
    //              in use are hidden by scaling their bones to zero.
    //   Grip     - the hammer and the cannon are two-handed: both arms are solved (two-bone IK) onto the weapon's two grip
    //              markers, and the abilities swing the hold through Yaw / Pitch / Thrust.
    //   Attacks  - it is the BasicAttack override (LMB): the hammer sweep and the cannon shell. HalberdSweep (Q) asks it
    //              for the hammer slam and the cannon bash. Smite, Consecrate, the Counter and Purgatus are untouched.
    [DefaultExecutionOrder(200)] // after every ability has posed the skeleton this frame
    public class GreyKnightArmament : MonoBehaviour, IPrimaryAttackOverride
    {
        // ---- Daemonhammer: LMB sweep ---------------------------------------------------------------------------------
        float SweepDamage = 30f;
        float SweepRange = 3.4f;
        float SweepArc = 130f;
        float SweepWindup = 0.42f, SweepStrike = 0.16f, SweepRecover = 0.26f;
        float SweepCooldown = 0.95f;
        float SweepEnergy = 8f;

        // ---- Daemonhammer: Q slam ------------------------------------------------------------------------------------
        float SlamDamage = 42f;
        float SlamRadius = 2.7f;
        float SlamReach = 2.3f;
        float SlamWindup = 0.6f, SlamStrike = 0.14f, SlamRecover = 0.4f;
        float SlamCooldown = 6f;
        float SlamStun = 0.6f;
        float SlamEnergy = 10f;

        // ---- Psycannon: LMB shell, Q bash ----------------------------------------------------------------------------
        float ShellWindup = 0.12f;
        float ShellCooldown = 1.5f;
        float ShellEnergy = 8f;
        float OverchargeWindup = 0.8f;
        float OverchargeCooldown = 7f;
        float OverchargeDamage = 2.5f;   // times a normal shell
        float OverchargeRadius = 2f;     // times a normal blast
        float OverchargePanic = 2.5f;    // seconds

        // ---- Hold geometry (design units relative to the shoulder centre, scaled to the model) -----------------------
        // Hammer: rear hand low and to the right, haft leaning up and to the left like a shouldered hammer.
        const float HammerRearForward = 0.22f, HammerRearUp = -0.2f, HammerRearSide = 0.14f;
        static readonly Vector3 HammerDir = new Vector3(0.3f, 0.78f, 0.55f); // forward, up, left of the chest
        // Cannon: centred in front of the belly; the rest orientation of the model is kept.
        const float CannonRearForward = 0.05f, CannonRearUp = -0.48f;
        const float CannonSide = 0.22f;      // carried to the right of the body, not centred
        const float CannonCarryYaw = 14f;    // barrel angled across the body towards the left
        const float CannonCarryPitch = 7f; // barrel tipped slightly down, carried at the hip
        // The most the hands may sit from the shoulder axis (design units) with the arms nearly straight.
        const float DesignRadius = 0.47f;

        static readonly Color SlashColor = new Color(1f, 0.85f, 0.55f, 0.85f);
        static readonly Color FireSlashColor = new Color(1f, 0.5f, 0.15f, 0.9f);

        struct Arm
        {
            public Transform up, fore, hand;
            public Quaternion upRest, foreRest, handRest;
            public float a, b; // upper arm and forearm length
        }

        class Held
        {
            public Transform bone;
            public Vector3 bonePos;   // root space, at rest
            public Quaternion boneRot;
            public Vector3 gripA;     // root space, at rest: the rear hand
            public Vector3 shaftDir;  // root space, at rest: rear grip to front grip
            public float spacing;     // distance between the two grips
            public bool keepRest;     // true: the weapon keeps the model's rest orientation (the cannon)
        }

        Health health;
        Energy energy;
        MouseAim aim;
        Counter counter;

        Transform spine, halberdBone, bolterBone, helmBone, pauldronBoneL, pauldronBoneR, helmGoldBone, pauldronGoldL, pauldronGoldR, hammerHead, cannonMuzzle;
        Arm armL, armR;
        Quaternion spineRestRel;
        Held hammer, cannon;
        float unit = 1f;
        Vector3 forward0, left0;
        bool held;

        GreyKnightWeapon shownWeapon = (GreyKnightWeapon)(-1);
        bool shownPurifier, shownPaladin;
        bool visualsReady;

        // Pose state.
        GreyKnightPose pose;
        float poseStart, poseWindup, poseStrike, poseRecover;
        float dirSign = 1f;
        float yaw, pitch, thrust;     // the hold's current offsets (decay to zero when no pose runs)
        float kick;                   // cannon recoil, 1 = just fired

        readonly HashSet<Health> struck = new HashSet<Health>();

        GreyKnightWeapon Weapon => GreyKnightUpgrade.WeaponOf(gameObject);
        bool Replica => health != null && health.IsReplica;

        void Awake()
        {
            Anathema.Player.CharacterStats.ApplyTo(gameObject, this);
            health = GetComponent<Health>();
            energy = GetComponent<Energy>();
            aim = GetComponent<MouseAim>();
            counter = GetComponent<Counter>();

            Transform model = transform.Find("Model");
            if (model == null) { enabled = false; return; }

            Transform upL = null, foreL = null, handL = null, upR = null, foreR = null, handR = null;
            Transform hammerBone = null, hammerA = null, hammerB = null, cannonBone = null, cannonA = null, cannonB = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Spine": if (spine == null) spine = t; break;
                    case "UpperArm_L": if (upL == null) upL = t; break;
                    case "Forearm_L": if (foreL == null) foreL = t; break;
                    case "Hand_L": if (handL == null) handL = t; break;
                    case "UpperArm_R": if (upR == null) upR = t; break;
                    case "Forearm_R": if (foreR == null) foreR = t; break;
                    case "Hand_R": if (handR == null) handR = t; break;
                    case "Halberd": if (halberdBone == null) halberdBone = t; break;
                    case "Bolter": if (bolterBone == null) bolterBone = t; break;
                    case "Helm_White": if (helmBone == null) helmBone = t; break;
                    case "Helm_Gold": if (helmGoldBone == null) helmGoldBone = t; break;
                    case "Pauldron_Gold_L": if (pauldronGoldL == null) pauldronGoldL = t; break;
                    case "Pauldron_Gold_R": if (pauldronGoldR == null) pauldronGoldR = t; break;
                    case "Pauldron_White_L": if (pauldronBoneL == null) pauldronBoneL = t; break;
                    case "Pauldron_White_R": if (pauldronBoneR == null) pauldronBoneR = t; break;
                    case "Daemonhammer": if (hammerBone == null) hammerBone = t; break;
                    case "Hammer_GripA": if (hammerA == null) hammerA = t; break;
                    case "Hammer_GripB": if (hammerB == null) hammerB = t; break;
                    case "Hammer_Head": if (hammerHead == null) hammerHead = t; break;
                    case "Psycannon": if (cannonBone == null) cannonBone = t; break;
                    case "Cannon_GripA": if (cannonA == null) cannonA = t; break;
                    case "Cannon_GripB": if (cannonB == null) cannonB = t; break;
                    case "Cannon_Muzzle": if (cannonMuzzle == null) cannonMuzzle = t; break;
                }
            }

            visualsReady = true;
            hammer = MakeHeld(hammerBone, hammerA, hammerB, false);
            cannon = MakeHeld(cannonBone, cannonA, cannonB, true);
            this.hammerBone = hammerBone;
            this.cannonBone = cannonBone;

            // Without the rig the hold can not be posed (the older model has no weapon bones): the abilities still work.
            if (spine == null || upL == null || foreL == null || handL == null || upR == null || foreR == null || handR == null)
            {
                hammer = cannon = null;
                return;
            }

            armL = MakeArm(upL, foreL, handL);
            armR = MakeArm(upR, foreR, handR);
            spineRestRel = Quaternion.Inverse(transform.rotation) * spine.rotation;

            // The model's own facing and side, read from the rest pose rather than assumed.
            Vector3 f = transform.InverseTransformDirection((handL.position - upL.position) + (handR.position - upR.position));
            f.y = 0f;
            forward0 = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            Vector3 l = transform.InverseTransformDirection(upL.position - upR.position);
            l.y = 0f;
            left0 = l.sqrMagnitude > 1e-6f ? l.normalized : Vector3.left;

            float half = Vector3.Distance(upL.position, upR.position) * 0.5f;
            float reach = 0.97f * 0.5f * (armL.a + armL.b + armR.a + armR.b);
            float radius = reach > half ? Mathf.Sqrt(reach * reach - half * half) : reach * 0.5f;
            unit = radius / DesignRadius;
        }

        Transform hammerBone, cannonBone;

        static Arm MakeArm(Transform up, Transform fore, Transform hand)
        {
            return new Arm
            {
                up = up, fore = fore, hand = hand,
                upRest = up.localRotation, foreRest = fore.localRotation, handRest = hand.localRotation,
                a = Vector3.Distance(up.position, fore.position),
                b = Vector3.Distance(fore.position, hand.position)
            };
        }

        Held MakeHeld(Transform bone, Transform gripA, Transform gripB, bool keepRest)
        {
            if (bone == null || gripA == null || gripB == null) return null;
            Vector3 a = transform.InverseTransformPoint(gripA.position);
            Vector3 b = transform.InverseTransformPoint(gripB.position);
            return new Held
            {
                bone = bone,
                bonePos = transform.InverseTransformPoint(bone.position),
                boneRot = Quaternion.Inverse(transform.rotation) * bone.rotation,
                gripA = a,
                shaftDir = (b - a).normalized,
                spacing = Vector3.Distance(a, b),
                keepRest = keepRest
            };
        }

        // ================================================================================================================
        // IPrimaryAttackOverride: the left mouse button
        // ================================================================================================================

        public bool Active => Weapon != GreyKnightWeapon.Halberd;
        public string AttackName => Weapon == GreyKnightWeapon.Daemonhammer ? "Hammer Strike" : "Psycannon";
        public float AttackWindup => Weapon == GreyKnightWeapon.Daemonhammer ? SweepWindup : ShellWindup;
        public float AttackCooldown => Weapon == GreyKnightWeapon.Daemonhammer ? SweepCooldown : ShellCooldown;

        public void AttackBegin()
        {
            if (Weapon != GreyKnightWeapon.Daemonhammer) return;
            dirSign = -dirSign; // successive swings alternate direction
            BeginPose(GreyKnightPose.HammerSweep, SweepWindup, SweepStrike, SweepRecover);
        }

        public void AttackStrike()
        {
            if (Weapon == GreyKnightWeapon.Daemonhammer) HammerSweep();
            else FireShell();
        }

        public void AttackCancel() => CancelPose();

        // ================================================================================================================
        // Q (HalberdSweep asks for these while a weapon is held)
        // ================================================================================================================

        public float QWindup => Weapon == GreyKnightWeapon.Daemonhammer ? SlamWindup : OverchargeWindup;
        public float QCooldown => Weapon == GreyKnightWeapon.Daemonhammer ? SlamCooldown : OverchargeCooldown;

        public void QBegin()
        {
            if (Weapon == GreyKnightWeapon.Daemonhammer) BeginPose(GreyKnightPose.HammerSlam, SlamWindup, SlamStrike, SlamRecover);
            else
            {
                dirSign = -dirSign;
                BeginPose(GreyKnightPose.CannonOvercharge, OverchargeWindup, 0.1f, 0.45f);
            }
        }

        public void QStrike(float damageScale)
        {
            if (Weapon == GreyKnightWeapon.Daemonhammer) HammerSlam(damageScale);
            else Overcharge(damageScale);
        }

        public void CancelPose() => pose = GreyKnightPose.None;

        // ================================================================================================================
        // Damage
        // ================================================================================================================

        static bool Purifier(GameObject g) => GreyKnightUpgrade.HasPurifier(g);

        // Everything enemy in front of the Grey Knight within `range` and the arc.
        int ArcHits(float range, float arc, float damage, float knockback, float knockbackTime, float energyGain)
        {
            int hits = 0;
            struck.Clear();
            Vector3 origin = transform.position;
            foreach (Collider c in Physics.OverlapSphere(origin + Vector3.up, range, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target.Team == health.Team || target.IsDead || !struck.Add(target)) continue;

                Vector3 to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(aim.AimDirection, to) > arc * 0.5f) continue;

                if (!target.TakeDamage(damage, gameObject)) continue;
                hits++;
                if (energy != null) energy.Gain(energyGain);
                GreyKnightUpgrade.Ignite(gameObject, target);
                if (knockback > 0f && !target.IsDead) Knockback.Apply(target.gameObject, to.sqrMagnitude > 0.0001f ? to : aim.AimDirection, knockback, knockbackTime);
            }
            if (hits > 0) HitMarkerSound.Play();
            return hits;
        }

        // A burning patch on the ground (Purifier Order), shown everywhere, damaging only on the owner's machine.
        void GroundFire(Vector3 centre, float radius)
        {
            FirePool.Spawn(gameObject, health.Team, new Vector3(centre.x, transform.position.y, centre.z), radius,
                GreyKnightUpgrade.GroundFireSeconds, GreyKnightUpgrade.GroundFirePerSecond, !Replica);
        }

        void HammerSweep()
        {
            bool fire = Purifier(gameObject);
            int hits = Replica ? 0 : ArcHits(SweepRange, SweepArc, SweepDamage, 0f, 0f, SweepEnergy);
            SlashEffect.Spawn(transform.position, aim.AimDirection, SweepRange, SweepArc, SweepStrike * 2f + 0.1f, fire ? FireSlashColor : SlashColor);
            GreyKnightSound.Swing(false);
            GreyKnightSound.Impact(hits);
        }

        void HammerSlam(float damageScale)
        {
            bool fire = Purifier(gameObject);
            Vector3 centre = transform.position + aim.AimDirection * SlamReach;
            int hits = 0;
            if (!Replica)
            {
                struck.Clear();
                foreach (Collider c in Physics.OverlapSphere(centre + Vector3.up, SlamRadius + 1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Health target = c.GetComponentInParent<Health>();
                    if (target == null || target.Team == health.Team || target.IsDead || struck.Contains(target)) continue;

                    Vector3 to = c.ClosestPoint(centre) - centre;
                    to.y = 0f;
                    if (to.magnitude > SlamRadius) continue;

                    struck.Add(target);
                    if (!target.TakeDamage(SlamDamage * damageScale, gameObject, true)) continue;
                    hits++;
                    if (energy != null) energy.Gain(SlamEnergy);
                    GreyKnightUpgrade.Ignite(gameObject, target);
                    if (!target.IsDead) Stunned.Apply(target.gameObject, SlamStun);
                }
                if (hits > 0) HitMarkerSound.Play();
            }

            BlastFx.Spawn(centre, SlamRadius, fire, true);
            GreyKnightSound.ConsecrateBoom();
            GreyKnightSound.Impact(hits);
            if (fire) GroundFire(centre, GreyKnightUpgrade.GroundFireRadius * 1.3f);
        }


        // The Overcharged Round (Q): one huge shell that panics everything it catches.
        void Overcharge(float boost)
        {
            FireShell(OverchargeDamage * boost, OverchargeRadius, OverchargePanic);
            kick = 2f;
        }

        void FireShell(float damageScale = 1f, float radiusScale = 1f, float panicSeconds = 0f)
        {
            Vector3 dir = aim.AimDirection;
            Vector3 origin = cannonMuzzle != null && Weapon == GreyKnightWeapon.Psycannon
                ? cannonMuzzle.position
                : transform.position + Vector3.up * 1.3f + dir * 1.1f;

            // The muzzle is offset from the body's centre line: aim from it at the cursor (flat), unless the cursor is too close.
            Vector3 toAim = aim.AimPoint - origin;
            toAim.y = 0f;
            if (toAim.sqrMagnitude > 0.25f) dir = toAim.normalized;
            dir.y = 0f;
            origin.y = Mathf.Max(origin.y, transform.position.y + 0.9f);

            PsychicShell.Fire(gameObject, health.Team, origin, dir, !Replica, Purifier(gameObject), hits =>
            {
                if (energy != null && hits > 0) energy.Gain(ShellEnergy * hits);
            }, damageScale, radiusScale, panicSeconds);
            kick = 1f;
            GreyKnightSound.SmiteFire(0.18f);
        }

        // ================================================================================================================
        // Pose
        // ================================================================================================================

        void BeginPose(GreyKnightPose kind, float windup, float strike, float recover)
        {
            pose = kind;
            poseStart = Time.time;
            poseWindup = windup;
            poseStrike = strike;
            poseRecover = recover;
        }

        // The hold's yaw (swing about the vertical), pitch (about the shoulder axis; negative lifts the weapon up and
        // back) and thrust (forward push), by pose and phase. With no pose they ease back to zero.
        void TickPose()
        {
            float dt = Time.deltaTime;
            float targetYaw = 0f, targetPitch = 0f, targetThrust = 0f;
            bool running = pose != GreyKnightPose.None;

            if (running)
            {
                float t = Time.time - poseStart;
                float wind = Mathf.Max(0.01f, poseWindup);
                float strike = Mathf.Max(0.01f, poseStrike);
                float recover = Mathf.Max(0.01f, poseRecover);

                // Phase values: a = wind-up progress, b = strike progress, c = recovery progress.
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / wind));
                float b = Mathf.Clamp01((t - wind) / strike);
                b = 1f - (1f - b) * (1f - b);
                float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - wind - strike) / recover));
                bool striking = t >= wind;
                bool recovering = t >= wind + strike;

                switch (pose)
                {
                    case GreyKnightPose.HammerSweep:
                    {
                        float back = -(SweepArc * 0.5f + 20f) * dirSign, through = (SweepArc * 0.5f + 25f) * dirSign;
                        if (!striking) { targetYaw = Mathf.Lerp(0f, back, a); targetPitch = Mathf.Lerp(0f, -18f, a); }
                        else if (!recovering) { targetYaw = Mathf.Lerp(back, through, b); targetPitch = Mathf.Lerp(-18f, 6f, b); }
                        else { targetYaw = Mathf.Lerp(through, 0f, c); targetPitch = Mathf.Lerp(6f, 0f, c); }
                        break;
                    }
                    case GreyKnightPose.HammerSlam:
                    {
                        if (!striking) { targetPitch = Mathf.Lerp(0f, -80f, a); targetThrust = Mathf.Lerp(0f, -0.1f, a); }
                        else if (!recovering) { targetPitch = Mathf.Lerp(-80f, 28f, b); targetThrust = Mathf.Lerp(-0.1f, 0.2f, b); }
                        else { targetPitch = Mathf.Lerp(28f, 0f, c); targetThrust = Mathf.Lerp(0.2f, 0f, c); }
                        break;
                    }
                    case GreyKnightPose.CannonOvercharge:
                    {
                        // The barrel rises as the round charges, then the cannon slams back from the recoil.
                        if (!striking) { targetPitch = Mathf.Lerp(0f, -24f, a); targetThrust = Mathf.Lerp(0f, -0.08f, a); }
                        else if (!recovering) { targetPitch = Mathf.Lerp(-24f, -6f, b); targetThrust = Mathf.Lerp(-0.08f, -0.3f, b); }
                        else { targetPitch = Mathf.Lerp(-6f, 0f, c); targetThrust = Mathf.Lerp(-0.3f, 0f, c); }
                        break;
                    }
                }

                if (t >= wind + strike + recover) pose = GreyKnightPose.None;
                yaw = targetYaw;
                pitch = targetPitch;
                thrust = targetThrust;
            }
            else
            {
                float k = 1f - Mathf.Exp(-14f * dt);
                yaw = Mathf.Lerp(yaw, 0f, k);
                pitch = Mathf.Lerp(pitch, 0f, k);
                thrust = Mathf.Lerp(thrust, 0f, k);
            }

            kick = Mathf.MoveTowards(kick, 0f, dt * 7f);
        }

        // ================================================================================================================
        // Visibility
        // ================================================================================================================

        static void Show(Transform bone, bool visible)
        {
            if (bone != null) bone.localScale = visible ? Vector3.one : Vector3.zero;
        }

        void UpdateVisibility()
        {
            if (!visualsReady) return;
            GreyKnightWeapon w = Weapon;
            bool purifier = Purifier(gameObject);
            bool paladin = GreyKnightUpgrade.HasPaladin(gameObject);
            if (w == shownWeapon && purifier == shownPurifier && paladin == shownPaladin) return;

            shownWeapon = w;
            shownPurifier = purifier;
            shownPaladin = paladin;
            Show(halberdBone, w == GreyKnightWeapon.Halberd);
            Show(bolterBone, w == GreyKnightWeapon.Halberd);
            Show(hammerBone, w == GreyKnightWeapon.Daemonhammer);
            Show(cannonBone, w == GreyKnightWeapon.Psycannon);
            Show(helmBone, purifier);
            Show(pauldronBoneL, purifier);
            Show(pauldronBoneR, purifier);
            Show(helmGoldBone, paladin);
            Show(pauldronGoldL, paladin);
            Show(pauldronGoldR, paladin);
        }

        void LateUpdate()
        {
            UpdateVisibility();
            TickPose();

            Held w = null;
            switch (Weapon)
            {
                case GreyKnightWeapon.Daemonhammer: w = hammer; break;
                case GreyKnightWeapon.Psycannon: w = cannon; break;
            }

            if (w == null)
            {
                if (held)
                {
                    held = false;
                    ResetArm(armL);
                    ResetArm(armR);
                }
                return;
            }
            held = true;

            // While the Counter's guard is up it poses the knight itself.
            if (counter != null && counter.IsGuarding) return;

            ResetArm(armL);
            ResetArm(armR);

            // Half the swing twists the chest, the other half swings the hold relative to it.
            if (Mathf.Abs(yaw) > 0.01f) spine.rotation = Quaternion.AngleAxis(yaw * 0.5f, Vector3.up) * spine.rotation;

            // The chest frame: the root's facing plus whatever twist the spine now has.
            Quaternion delta = spine.rotation * Quaternion.Inverse(transform.rotation * spineRestRel);
            Quaternion frame = delta * transform.rotation;
            Vector3 fwd = frame * forward0;
            Vector3 up = frame * Vector3.up;
            Vector3 left = frame * left0;
            Vector3 pivot = (armL.up.position + armR.up.position) * 0.5f;

            float push = thrust + kick * -0.14f;
            Vector3 gripA;
            Vector3 dir;
            if (w.keepRest)
            {
                gripA = pivot + (fwd * (CannonRearForward + push) + up * CannonRearUp - left * CannonSide) * unit;
                dir = Quaternion.AngleAxis(-CannonCarryYaw, up) * (frame * w.shaftDir);
            }
            else
            {
                gripA = pivot + (fwd * (HammerRearForward + push) + up * HammerRearUp - left * HammerRearSide) * unit;
                dir = (fwd * HammerDir.x + up * HammerDir.y + left * HammerDir.z).normalized;
            }

            Quaternion swing = Quaternion.AngleAxis(yaw * 0.5f, up);
            gripA = pivot + swing * (gripA - pivot);
            dir = swing * dir;
            Vector3 gripB = gripA + dir * w.spacing;

            // Pitch about the shoulder axis (and a little recoil kick on the cannon).
            float pitchDegrees = pitch + kick * -5f + (w.keepRest ? CannonCarryPitch : 0f);
            Quaternion pitchQ = Quaternion.AngleAxis(pitchDegrees, Vector3.Cross(up, fwd));
            gripA = pivot + pitchQ * (gripA - pivot);
            gripB = pivot + pitchQ * (gripB - pivot);

            Quaternion aimQ = Quaternion.FromToRotation(frame * w.shaftDir, dir);
            Quaternion total = pitchQ * aimQ * frame;
            Vector3 gripOffset = w.gripA - w.bonePos;

            // Arms first: the weapon bone is a child of the right hand, so it must be placed after the arms have moved.
            Solve(armR, gripA);
            Solve(armL, gripB);
            w.bone.SetPositionAndRotation(gripA - total * gripOffset, total * w.boneRot);
        }

        static void ResetArm(Arm arm)
        {
            arm.up.localRotation = arm.upRest;
            arm.fore.localRotation = arm.foreRest;
            arm.hand.localRotation = arm.handRest;
        }

        // Two-bone IK: bends the arm so the wrist reaches `target`, elbow kept toward where it hangs at rest.
        static void Solve(Arm arm, Vector3 target)
        {
            Vector3 shoulder = arm.up.position;
            Vector3 elbow = arm.fore.position;
            Vector3 pole = elbow - shoulder;

            Vector3 toTarget = target - shoulder;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(arm.a - arm.b) + 0.01f, (arm.a + arm.b) * 0.999f);
            Vector3 u = toTarget.normalized;

            float x = (arm.a * arm.a - arm.b * arm.b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, arm.a * arm.a - x * x));
            Vector3 perp = Vector3.ProjectOnPlane(pole, u);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.down, u);
            perp.Normalize();

            Vector3 newElbow = shoulder + u * x + perp * h;
            arm.up.rotation = Quaternion.FromToRotation(elbow - shoulder, newElbow - shoulder) * arm.up.rotation;

            elbow = arm.fore.position;
            Vector3 wrist = arm.hand.position;
            arm.fore.rotation = Quaternion.FromToRotation(wrist - elbow, shoulder + u * d - elbow) * arm.fore.rotation;
        }
    }
}
