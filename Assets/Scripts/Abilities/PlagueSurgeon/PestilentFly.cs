using System;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Press right mouse to loose a plague fly along the aim direction. It latches onto the first character it meets
    // (anyone but the surgeon; walls stop it):
    //   enemy -> the fly feeds on them, damaging them every half second (and its bite leaves a stack of Festering)
    //   ally  -> the fly tends them, healing them every half second
    // EX (hold Shift, costs energy): Brood Fly. A bloated fly that splits as it latches on: a second fly leaps from the
    // first character it reaches to the nearest other character close by (friend or foe, same rules) and latches there
    // too. Feed on two enemies at once, or tend an ally while feeding on the enemy beside them.
    // (Pestilent Burst, the panicking fly that used to be this EX, is now its own ability on R.)
    public class PestilentFly : Ability
    {
        [SerializeField] float projectileSpeed = 18f;
        [SerializeField] float range = 13f;
        [SerializeField] float projectileRadius = 0.45f;
        [SerializeField] float muzzleHeight = 1.1f;
        [SerializeField] float attachDuration = 4f;
        [SerializeField] float enemyDamagePerSecond = 7f;
        [SerializeField] float allyHealPerSecond = 7f;
        [SerializeField] float energyPerTick = 2f;

        [Header("EX: Brood Fly")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float splitRange = 5f;

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

        // Swarm (the upgrade): two flies in reserve, each recharging on its own.
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
        public override string ExDescription => "Splits to a second target";
        public override string ExName => "Brood Fly";

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

            bool brood = ExActivation;
            PlagueFlyProjectile.Launch(gameObject, transform.position + Vector3.up * muzzleHeight + dir * 0.7f, dir,
                projectileSpeed, range, projectileRadius, brood ? 1.4f : 1.1f, brood ? 0.95f : 1.15f, IsReplica,
                (target, at) =>
                {
                    if (target == null) return;
                    OnLatch(target);
                    if (brood) Split(target);
                },
                null);
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

        // Brood Fly (owner only): a second fly leaps from the first host to the nearest other living character nearby.
        void Split(Health first)
        {
            Health best = null;
            float bestDistance = splitRange;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h == first || h == Health || h.IsDead || h.Structure) continue;
                if (h.Team != Health.Team && Stealthed.HiddenFrom(h.gameObject, Health.Team)) continue;
                Vector3 d = h.transform.position - first.transform.position;
                d.y = 0f;
                if (d.magnitude < bestDistance)
                {
                    bestDistance = d.magnitude;
                    best = h;
                }
            }
            if (best != null) OnLatch(best);
        }
    }

    // A plague fly in flight: a buzzing blob that travels in a straight line until it touches a character (anyone but
    // the thrower; the dead are skipped) or a wall, or runs out of range. Shared by Pestilent Fly and Pestilent Burst.
    // `onHit` gets the character it touched (null for a wall) and the point; it is only called on the thrower's own
    // machine (visual flies just vanish). `onEnd` runs on every machine where the fly stops, with whatever it touched or
    // where it ran out (null target), so a visual copy can draw a burst too.
    sealed class PlagueFlyProjectile : MonoBehaviour
    {
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        GameObject thrower;
        Vector3 direction;
        float speed;
        float remainingRange;
        float radius;
        bool visualOnly;
        Action<Health, Vector3> onHit;
        Action<Health, Vector3> onEnd;
        Transform body;

        public static void Launch(GameObject thrower, Vector3 origin, Vector3 direction, float speed, float range, float radius,
            float size, float buzzPitch, bool visualOnly, Action<Health, Vector3> onHit, Action<Health, Vector3> onEnd)
        {
            var go = new GameObject("Plague Fly");
            go.transform.position = origin;
            var fly = go.AddComponent<PlagueFlyProjectile>();
            fly.thrower = thrower;
            fly.direction = direction;
            fly.speed = speed;
            fly.remainingRange = range;
            fly.radius = radius;
            fly.visualOnly = visualOnly;
            fly.onHit = onHit;
            fly.onEnd = onEnd;
            go.transform.rotation = Quaternion.LookRotation(direction);

            fly.body = FlyVisual.Create(go.transform, size > 1.2f, size).transform;
            // It buzzes the whole way there, from the first moment (the recording's quiet start is skipped); the sound
            // goes when it latches on or bursts, and the swarm on the victim takes over.
            Sfx.Loop(Sfx.Flies2, go.transform, 0.3f, buzzPitch, 0f, true);
        }

        void Update()
        {
            if (thrower == null)
            {
                Destroy(gameObject);
                return;
            }

            float step = Mathf.Min(speed * Time.deltaTime, remainingRange);

            if (TryFindHit(step, out RaycastHit hit))
            {
                Vector3 at = transform.position + direction * hit.distance;
                Health target = hit.collider.GetComponentInParent<Health>();
                if (!visualOnly) onHit?.Invoke(target, at);
                onEnd?.Invoke(target, at);
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
            remainingRange -= step;
            // Erratic buzzing on top of the straight flight path.
            body.localPosition = new Vector3(Mathf.Sin(Time.time * 40f) * 0.12f, Mathf.Sin(Time.time * 53f) * 0.12f, 0f);
            if (remainingRange <= 0f)
            {
                onEnd?.Invoke(null, transform.position);
                Destroy(gameObject);
            }
        }

        // Nearest hit along this frame's step. The thrower and the dead are skipped; allies and enemies both count.
        bool TryFindHit(float distance, out RaycastHit best)
        {
            best = default;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                Health health = h.collider.GetComponentInParent<Health>();
                if (health != null && (health.gameObject == thrower || health.IsDead)) continue;
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
