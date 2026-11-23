using System.Collections;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // Q: Bull Charge. A short wind-up with the lane marked on the ground (planted), then he barrels forward. Nothing can
    // stun, slow or shove him while he charges. The first enemy he meets is struck and carried along in front of him;
    // if the carry drives them into a wall (his own Living Wall counts) they are stunned on impact, otherwise they are
    // dropped with a brief stun at the end. Stuns, silence, panic and knockback during the wind-up interrupt it
    // (refunded); C cancels it.
    [DefaultExecutionOrder(50)]
    public class BullCharge : Ability, IUnstoppable
    {
        [SerializeField] float windup = 0.25f;
        [SerializeField] float distance = 8f;
        [SerializeField] float duration = 0.35f;
        [SerializeField] float damage = 12f;
        [SerializeField] float catchRadius = 1f;
        [SerializeField] float carryLead = 1.1f;        // the carried enemy is pushed this far past where the charge ends
        [SerializeField] float wallStun = 1.25f;
        [SerializeField] float dropStun = 0.4f;
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] float laneWidth = 1.6f;

        [Header("Pose")]
        [SerializeField] float lean = 22f;
        [SerializeField] float shieldForward = 60f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_L", "UpperArm_R" };

        CharacterController controller;
        Energy energy;
        LimbPose pose;
        bool posed;
        Vector3 dir = Vector3.forward;
        float endTime;
        bool caught;

        public bool IsCharging { get; private set; }
        public bool Unstoppable => IsCharging;

        public override string DisplayName => "Bull Charge";
        public override string KeyLabel => "Q";
        public override float MovementRange => distance;
        public override bool RootsMovement => IsCharging || IsWindingUp;

        protected override float WindupDuration => windup;
        protected override Color WindupColor => BullgrynKit.Hazard;

        void Reset()
        {
            cooldown = 9f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.Q) && !BullgrynKit.Busy(gameObject);
        }

        protected override void OnWindupStart()
        {
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint - transform.position : Aim.AimDirection;
            dir = BullgrynKit.FlatAim(aim, transform);
            BullgrynKit.GroundLane(transform.position, dir, distance, laneWidth, windup, BullgrynKit.Hazard);
        }

        protected override void Activate()
        {
            IsCharging = true;
            caught = false;
            endTime = Time.time + duration;
            Sfx.Play(Sfx.Fissure, 0.35f, 1.3f, 1.4f, 0.5f);
        }

        protected override void Cancel()
        {
            IsCharging = false;
        }

        protected override void Update()
        {
            // Headlong: a faster recharge (only changed while ready, so the HUD's sweep stays consistent).
            if (CooldownRemaining <= 0f)
                CooldownScale = Anathema.Upgrades.UpgradeState.Has(gameObject, BullgrynUpgrade.Headlong) ? BullgrynUpgrade.HeadlongCooldownScale : 1f;
            base.Update();
            if (!IsCharging) return;

            if (Time.time >= endTime || Health.IsDead)
            {
                IsCharging = false;
                return;
            }

            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            // On a replica the movement arrives through the network position instead.
            if (IsReplica || controller == null || !controller.enabled) return;
            controller.Move(dir * (distance / duration) * Time.deltaTime);
            if (!caught) TryCatch();
        }

        void TryCatch()
        {
            Vector3 probe = transform.position + Vector3.up + dir * (catchRadius * 0.9f);
            Health best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Collider c in Physics.OverlapSphere(probe, catchRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h == Health || h.Team == Health.Team || h.IsDead) continue;
                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = h;
            }
            if (best != null) Catch(best);
        }

        void Catch(Health target)
        {
            caught = true;
            float remaining = Mathf.Max(0.05f, endTime - Time.time);
            float carry = distance / duration * remaining + carryLead;
            bool slams = BullgrynKit.FindWall(target, dir, carry, out float wallDistance, out Vector3 impact);
            float caughtAt = Time.time;

            Hit hit = Hit.Direct(damage).WithKnockback(dir, carry, remaining);
            target.Strike(hit, gameObject, landed =>
            {
                if (!landed) return;
                if (energy != null) energy.Gain(energyPerHit);
                MeleeSound.Impact();
                float reach = slams ? remaining * Mathf.Clamp01(wallDistance / carry) : remaining;
                StartCoroutine(StunLater(target, Mathf.Max(0f, reach - (Time.time - caughtAt)), slams ? wallStun : dropStun,
                    slams ? impact : target.transform.position, slams));
            });
        }

        IEnumerator StunLater(Health target, float delay, float stun, Vector3 at, bool slam)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (target == null || target.IsDead) yield break;
            Stunned.Apply(target.gameObject, stun); // hands the stun to the server online
            if (!slam) yield break;
            Anathema.Abilities.Psyker.PsykerFx.Ring(new Vector3(at.x, target.transform.position.y, at.z), 0.2f, 1.6f, 0.3f, BullgrynKit.Hazard, 0.3f);
            Sfx.Play(Sfx.SlamExplosion, 0.5f, 0.8f, 0.9f, 0.6f);
        }

        void LateUpdate()
        {
            if (!IsCharging && !IsWindingUp)
            {
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                return;
            }
            posed = true;
            pose.Reset();
            pose.Rotate("Spine", transform.right, lean);
            pose.Rotate("UpperArm_L", transform.right, -shieldForward);
            pose.Rotate("UpperArm_R", transform.right, 25f);
        }
    }
}
