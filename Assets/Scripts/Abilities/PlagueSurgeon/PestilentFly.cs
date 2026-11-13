using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Press right mouse to loose a plague fly along the aim direction. It latches onto the first character it meets
    // (anyone but the surgeon; walls stop it):
    //   enemy -> the fly feeds on them, damaging them every half second (and its bite leaves a stack of Festering)
    //   ally  -> the fly tends them, healing them every half second
    // EX (hold Shift, costs energy): the fly never attaches. It bursts the moment it touches anything (a character or a
    // wall) or reaches its maximum range, damaging and panicking every enemy around that spot and healing every ally.
    public class PestilentFly : Ability
    {
        [SerializeField] float projectileSpeed = 18f;
        [SerializeField] float range = 13f;
        [SerializeField] float projectileRadius = 0.45f;
        [SerializeField] float muzzleHeight = 1.1f;
        [SerializeField] float attachDuration = 4f;
        [SerializeField] float enemyDamagePerSecond = 7f;
        [SerializeField] float allyHealPerSecond = 7f;
        [SerializeField] float exEnergyCost = 40f;
        [SerializeField] float burstRadius = 3.5f;
        [SerializeField] float burstDamage = 28f;
        [SerializeField] float burstHeal = 28f;
        [SerializeField] float burstPanic = 1.25f;
        [SerializeField] float energyPerTick = 2f;

        Energy energy;

        protected override float WindupDuration => 0.3f;
        protected override Color WindupColor => new Color(0.55f, 0.65f, 0.2f, 1f);

        // The EX energy is spent when the wind-up starts and given back if it is cancelled.
        protected override void OnWindupStart()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
            PlagueSurgeonPose.For(gameObject).ReadyThrow(WindupDuration + 0.1f);
        }

        protected override void OnWindupCancel()
        {
            PlagueSurgeonPose.For(gameObject).Release();
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        public override string DisplayName => "Pestilent Fly";
        public override int ChargeCapacity => TwoCharges ? 2 : 1;
        public override int ChargesReady => TwoCharges ? ReadyCharges : 0;

        // Swarm: two flies in reserve, each recharging on its own.
        readonly float[] chargeReady = new float[2];

        bool TwoCharges => PlagueSurgeonUpgradeEffects.Has(gameObject, PlagueSurgeonUpgrade.Swarm);

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

        // One fly is ready and the other is still on its way: report how far off it is.
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
        public override float ExCost => exEnergyCost;
        public override string ExDescription => "Bursts when done";
        public override string ExName => "Pestilent Burst";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 6f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1);
        }

        protected override void Activate()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            PlagueSurgeonPose.For(gameObject).Throw();

            if (TwoCharges && !IsReplica)
            {
                // Spend a ready charge (else the one closest to ready); only that one starts recharging.
                int used = Time.time >= chargeReady[0] ? 0 : Time.time >= chargeReady[1] ? 1 : (chargeReady[0] <= chargeReady[1] ? 0 : 1);
                chargeReady[used] = Time.time + cooldown * CooldownScale;
            }

            var go = new GameObject("Pestilent Fly");
            go.transform.position = transform.position + Vector3.up * muzzleHeight + dir * 0.7f;
            go.AddComponent<Fly>().Init(this, dir, ExActivation, IsReplica);
        }

        protected override void Cancel()
        {
            // Nothing to cancel once the fly has left (a wind-up in progress is cancelled by the base class).
        }

        void OnLatch(Health target)
        {
            Infestation.Apply(
                target, gameObject, Health.Team, energy,
                attachDuration, enemyDamagePerSecond, allyHealPerSecond, energyPerTick);
            // A fly that bites an enemy leaves a stack of Festering, the same rot as one glob of Plague Spit.
            if (target.Team != Health.Team) PlagueSurgeonUpgradeEffects.ApplySpitRot(gameObject, target, energy);
        }

        void OnDetonate(Vector3 position, bool visualOnly)
        {
            // Burst on the ground plane so the dome sits where the character stands.
            position.y = transform.position.y;
            Infestation.Detonate(position, gameObject, Health.Team, burstRadius, burstDamage, burstHeal, burstPanic, visualOnly);
        }

        // The flying insect itself: a buzzing blob that travels in a straight line until it touches someone.
        class Fly : MonoBehaviour
        {
            static readonly RaycastHit[] Hits = new RaycastHit[16];

            PestilentFly owner;
            Vector3 direction;
            float remainingRange;
            bool ex;
            bool visualOnly;
            Transform body;

            public void Init(PestilentFly source, Vector3 moveDirection, bool exVersion, bool visual)
            {
                owner = source;
                direction = moveDirection;
                ex = exVersion;
                visualOnly = visual;
                remainingRange = source.range;
                transform.rotation = Quaternion.LookRotation(direction);

                body = FlyVisual.Create(transform, ex, ex ? 1.5f : 1.1f).transform;
                // It buzzes the whole way there, from the first moment (the recording's quiet start is skipped); the sound
                // goes when it latches on or bursts, and the swarm on the victim takes over.
                Sfx.Loop(Sfx.Flies2, transform, 0.3f, ex ? 0.85f : 1.15f, 0f, true); // from the moment it leaves his hand
            }

            void Update()
            {
                float step = Mathf.Min(owner.projectileSpeed * Time.deltaTime, remainingRange);

                if (TryFindHit(step, out RaycastHit hit))
                {
                    if (ex)
                    {
                        owner.OnDetonate(transform.position + direction * hit.distance, visualOnly);
                    }
                    else
                    {
                        Health target = hit.collider.GetComponentInParent<Health>();
                        if (target != null && !visualOnly) owner.OnLatch(target);
                    }
                    Destroy(gameObject);
                    return;
                }

                transform.position += direction * step;
                remainingRange -= step;
                // Erratic buzzing on top of the straight flight path.
                body.localPosition = new Vector3(Mathf.Sin(Time.time * 40f) * 0.12f, Mathf.Sin(Time.time * 53f) * 0.12f, 0f);
                if (remainingRange <= 0f)
                {
                    if (ex) owner.OnDetonate(transform.position, visualOnly);
                    Destroy(gameObject);
                }
            }

            // Nearest hit along this frame's step. The surgeon and the dead are skipped; allies and enemies both count.
            bool TryFindHit(float distance, out RaycastHit best)
            {
                best = default;
                int count = Physics.SphereCastNonAlloc(transform.position, owner.projectileRadius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
                float bestDistance = float.MaxValue;
                bool found = false;

                for (int i = 0; i < count; i++)
                {
                    RaycastHit h = Hits[i];
                    Health health = h.collider.GetComponentInParent<Health>();
                    if (health != null && (health.gameObject == owner.gameObject || health.IsDead)) continue;
                    if (h.distance < bestDistance)
                    {
                        bestDistance = h.distance;
                        best = h;
                        found = true;
                    }
                }

                return found;
            }

        }
    }
}
