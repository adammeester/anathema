using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // R: Crimson Rush. A short planted wind-up with a lane marked on the ground, then a jump-pack-boosted dash along it.
    // The first enemy he reaches stops the dash: a slash for `damage` that stuns them for `stun` seconds. Only that one
    // enemy is hit; anyone else in the lane is passed. Walls stop the dash. C, a stun, silence, panic or knockback
    // during the wind-up cancels it and refunds the cooldown.
    //
    // EX Blade Hurl (`exEnergyCost`): he winds back and hurls his sword along his aim instead. It spins through the air
    // (a skillshot, `exRange` at `exSpeed`) and the first enemy it meets takes `exDamage` and a `exStun` stun; walls
    // stop it too. Then it flies back to his hand over `exReturnTime`. Blade of Sanguinius makes it ricochet; Wrath of
    // Baal brings it back faster. The flying copy is whichever weapon he holds (GuardWeaponVisual hides the one in hand). Until it is back he is disarmed: no Power Sword,
    // no Exsanguinating Arc, no Parry riposte. Everything else (heals, Jump Pack, Chalice, the counter itself) still works.
    // Every machine flies its own copy of the sword, so the disarm and the hidden blade show everywhere; only the
    // owner's copy deals the hit.
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

        [Header("EX Blade Hurl")]
        [SerializeField] float exEnergyCost = 50f;
        [SerializeField] float exWindup = 0.25f;
        [SerializeField] float exRange = 10f;
        [SerializeField] float exSpeed = 24f;
        [SerializeField] float exRadius = 0.5f;
        [SerializeField] float exDamage = 10f;
        [SerializeField] float exStun = 1.5f;
        [SerializeField] float exReturnTime = 0.6f;

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

        // Blade Hurl
        BloodBladeEffect bladeGlow;
        GuardWeaponVisual.WeaponMaterials weaponMaterials;
        int bouncesLeft;
        readonly System.Collections.Generic.HashSet<Health> struck = new System.Collections.Generic.HashSet<Health>();
        Vector3 flightDir;
        bool disarmed;
        float disarmSince;
        GameObject thrown;        // the flying sword (with its Projectile) on the way out
        Transform returning;      // the sword spinning back to his hand
        Vector3 returnFrom;
        float returnStart;
        Vector3 lastThrownAt;
        bool throwing;            // the EX wind-up or release pose
        float throwPoseEnd;

        // While the sword is out of his hand he can't swing it (Chainsword, SanguineParry's riposte read this).
        public bool Disarmed => disarmed;

        protected override float WindupDuration =>
            (IsReplica ? ExActivation : Input.GetKey(KeyCode.LeftShift)) ? exWindup : windup;
        protected override Color WindupColor => SanguinaryKit.Blood;

        public override string DisplayName => "Crimson Rush";
        public override string KeyLabel => "R";
        public override float MovementRange => distance;
        public override bool RootsMovement => IsWindingUp;
        public override float ExCost => exEnergyCost;
        public override string ExName => "Blade Hurl";
        public override string ExDescription => "Hurl your sword: the first enemy it hits is stunned. You are disarmed until it flies back.";

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
            weaponMaterials = GuardWeaponVisual.WeaponMaterials.Create();
        }

        // The dash ends in a sword cut and the EX throws the sword: neither works while it is out of his hand.
        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R) && !disarmed;

        protected override void OnWindupStart()
        {
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
            direction = SanguinaryKit.FlatAim(aim - transform.position, transform);
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
            if (lane != null) Destroy(lane);

            if (ExActivation)
            {
                // Winding back for the throw: a narrow lane along his aim.
                lane = Bullgryn.BullgrynKit.GroundLane(transform.position, direction, exRange, exRadius * 2f, exWindup + 0.15f, SanguinaryKit.Blood);
                throwing = true;
                throwPoseEnd = float.PositiveInfinity;
                return;
            }

            lane = Bullgryn.BullgrynKit.GroundLane(transform.position, direction, distance, hitRadius * 2f, windup + duration, SanguinaryKit.Blood);
            JetPackFx.For(gameObject).SetThrust(0.35f, windup);
        }

        protected override void OnWindupCancel()
        {
            if (lane != null) Destroy(lane);
            if (ExActivation)
            {
                if (!IsReplica && energy != null) energy.Gain(exEnergyCost);
                throwing = false;
                return;
            }
            JetPackFx.For(gameObject).SetThrust(0f, 0.15f);
        }

        protected override void Activate()
        {
            if (!IsReplica) direction = SanguinaryKit.FlatAim(Aim.AimDirection, transform);
            if (ExActivation)
            {
                Hurl();
                return;
            }

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
            throwing = false;
        }

        protected override void Update()
        {
            base.Update();
            UpdateHurl();
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

        // ---- EX Blade Hurl ---------------------------------------------------------------------------------------------

        float ReturnTime => GuardUpgrade.Has(gameObject, GuardUpgrade.WrathOfBaal) ? GuardUpgrade.WrathReturnTime : exReturnTime;

        void Hurl()
        {
            if (lane != null) Destroy(lane);
            throwing = true;
            throwPoseEnd = Time.time + 0.2f;
            MeleeSound.PowerSwing(0.5f, 0.75f, 0.85f);

            // Blade of Sanguinius: it ricochets on from whoever it strikes.
            bouncesLeft = GuardUpgrade.Has(gameObject, GuardUpgrade.BladeOfSanguinius) ? GuardUpgrade.RicochetBounces : 0;
            struck.Clear();
            Vector3 origin = transform.position + Vector3.up * 1.3f + direction * 0.7f;
            Throw(origin, direction, exRange, exDamage, exStun);
            SetDisarmed(true);
        }

        // Every machine flies its own copy; only the owner's deals the hit (Projectile strikes only from the owner).
        void Throw(Vector3 origin, Vector3 dir, float reach, float dmg, float stunFor)
        {
            flightDir = dir;
            thrown = MakeWeapon("Hurled Blade");
            thrown.transform.position = origin;
            lastThrownAt = origin;
            thrown.AddComponent<Projectile>()
                  .Init(gameObject, Health.Team, dmg, exSpeed, reach, exRadius, dir, OnBladeHit)
                  .WithRiders(Hit.Direct(0f).WithStun(stunFor));
        }

        // The owner's blade struck (replicas' copies never report hits).
        void OnBladeHit(Health target)
        {
            if (this == null || target == null) return;
            HitMarkerSound.Play();
            MeleeSound.PowerImpact(0.5f);
            BloodImpact.Play(target.transform.position + Vector3.up, flightDir, 1.2f);
        }

        void UpdateHurl()
        {
            if (!disarmed) return;

            if (Health.IsDead)
            {
                SetDisarmed(false);
                return;
            }

            // Out: follow the flying blade; once it has struck, hit a wall or run out of range, it ricochets or turns home.
            if (thrown != null)
            {
                lastThrownAt = thrown.transform.position;
                return;
            }
            if (returning == null)
            {
                if (TryRicochet()) return;
                Sfx.Play(Sfx.Impact, 0.2f, 0.8f, 0.9f, 0.4f);
                returning = MakeWeapon("Returning Blade").transform;
                returnFrom = lastThrownAt;
                returnStart = Time.time;
            }

            float t = Mathf.Clamp01((Time.time - returnStart) / Mathf.Max(0.05f, ReturnTime));
            Vector3 hand = transform.position + Vector3.up * 1.3f + transform.right * 0.4f;
            // A low arc home, speeding up as it comes.
            Vector3 at = Vector3.Lerp(returnFrom, hand, t * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.6f);
            Vector3 travel = SanguinaryKit.Flat(hand - at);
            returning.position = at;
            if (travel.sqrMagnitude > 0.0001f) returning.rotation = Quaternion.LookRotation(travel);

            // Caught (or, as a safety net, it has been gone far too long).
            float longest = exRange / Mathf.Max(1f, exSpeed) * (1 + GuardUpgrade.RicochetBounces) + ReturnTime + 1.5f;
            if (t >= 1f || Time.time - disarmSince > longest)
            {
                MeleeSound.PowerImpact(0.3f);
                SetDisarmed(false);
            }
        }

        // The blade just stopped. If it stopped on an enemy and has bounces left, it leaps on to the nearest other enemy
        // within reach of that one, at reduced damage and stun. Worked out the same way on every machine (from where
        // the blade stopped), so everyone sees the same bounces; only the owner's copy hits.
        bool TryRicochet()
        {
            Health hit = NearestEnemy(lastThrownAt, exRadius + 1.2f, null);
            if (hit == null || bouncesLeft <= 0) return false;
            struck.Add(hit);
            Health next = NearestEnemy(hit.transform.position, GuardUpgrade.RicochetRange, struck);
            if (next == null) return false;

            bouncesLeft--;
            Vector3 from = hit.transform.position + Vector3.up * 1.2f;
            Vector3 to = SanguinaryKit.Flat(next.transform.position - hit.transform.position);
            if (to.sqrMagnitude < 0.01f) return false;
            Vector3 dir = to.normalized;
            Sfx.Play(Sfx.Impact, 0.25f, 1.1f, 1.25f, 0.4f);
            Throw(from + dir * 0.6f, dir, to.magnitude + 1.5f, exDamage * GuardUpgrade.RicochetDamageShare, exStun * GuardUpgrade.RicochetStunShare);
            return true;
        }

        Health NearestEnemy(Vector3 point, float radius, System.Collections.Generic.HashSet<Health> skip)
        {
            Health best = null;
            float bestSq = radius * radius;
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, point, transform.forward, radius, 360f))
            {
                if (enemy == null || enemy.IsDead || (skip != null && skip.Contains(enemy))) continue;
                float sq = SanguinaryKit.Flat(enemy.transform.position - point).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = enemy;
            }
            return best;
        }

        void SetDisarmed(bool on)
        {
            disarmed = on;
            if (on) disarmSince = Time.time;
            if (bladeGlow == null) bladeGlow = GetComponentInChildren<BloodBladeEffect>();
            if (bladeGlow != null) bladeGlow.Sheathed = on;
            if (on) return;
            if (thrown != null) Destroy(thrown);
            if (returning != null) Destroy(returning.gameObject);
            thrown = null;
            returning = null;
        }

        // A flying copy of the weapon he holds (sword, axe or glaive), red with its power field and trailing blood,
        // spinning flat like a wheel about its middle.
        GameObject MakeWeapon(string name)
        {
            GuardWeapon weapon = GuardUpgrade.WeaponOf(gameObject);
            float length = weapon == GuardWeapon.GlaiveEncarmine ? 2.4f : 1.3f;

            var root = new GameObject(name);
            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(root.transform, false);
            spinner.gameObject.AddComponent<Spin>();
            var holder = new GameObject("Holder").transform;
            holder.SetParent(spinner, false);
            holder.localRotation = Quaternion.Euler(90f, 0f, 0f);          // blade along the flight
            holder.localPosition = new Vector3(0f, 0f, -length * 0.5f);   // spun about its middle

            GuardWeaponVisual.BuildShape(weapon, holder, weaponMaterials, out _, out Transform tip);
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0f;
            trail.sharedMaterial = weaponMaterials.Glow;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        sealed class Spin : MonoBehaviour
        {
            void Update() => transform.Rotate(Vector3.up, 1440f * Time.deltaTime, Space.World);
        }

        // Lean into the dash with the blade swept back, then whip it through as he arrives. For the hurl: the sword arm
        // drawn back over the shoulder through the wind-up, then flung forward.
        void LateUpdate()
        {
            if (throwing)
            {
                posed = true;
                pose.Reset();
                if (Time.time < throwPoseEnd && float.IsPositiveInfinity(throwPoseEnd))
                {
                    pose.Rotate("Spine", Vector3.up, 30f);
                    pose.Rotate("UpperArm_R", transform.right, -120f);
                }
                else if (Time.time < throwPoseEnd)
                {
                    float r = 1f - (throwPoseEnd - Time.time) / 0.2f;
                    pose.Rotate("Spine", Vector3.up, Mathf.Lerp(30f, -25f, r));
                    pose.Rotate("UpperArm_R", transform.right, Mathf.Lerp(-120f, -20f, r));
                }
                else throwing = false;
                if (throwing) return;
            }

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
            if (thrown != null) Destroy(thrown);
            if (returning != null) Destroy(returning.gameObject);
            weaponMaterials?.Destroy();
        }
    }
}
