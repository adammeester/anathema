using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Press right mouse to loose a plague fly along the aim direction. It latches onto the first character it meets
    // (anyone but the surgeon; walls stop it):
    //   enemy -> the fly feeds on them, damaging them every half second
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
        [SerializeField] float enemyDamagePerSecond = 6f;
        [SerializeField] float allyHealPerSecond = 7f;
        [SerializeField] float exEnergyCost = 40f;
        [SerializeField] float burstRadius = 3.5f;
        [SerializeField] float burstDamage = 28f;
        [SerializeField] float burstHeal = 28f;
        [SerializeField] float burstPanic = 1.25f;
        [SerializeField] float energyPerTick = 2f;

        Energy energy;

        public override string DisplayName => "Pestilent Fly";
        public override string KeyLabel => "RMB";
        public override float ExCost => exEnergyCost;
        public override string ExDescription => "Bursts when done";

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
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);

            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            PlagueSurgeonPose.For(gameObject).Throw();

            var go = new GameObject("Pestilent Fly");
            go.transform.position = transform.position + Vector3.up * muzzleHeight + dir * 0.7f;
            go.AddComponent<Fly>().Init(this, dir, ExActivation, IsReplica);
        }

        protected override void Cancel()
        {
            // Nothing to cancel: the fly leaves instantly.
        }

        void OnLatch(Health target)
        {
            Infestation.Apply(
                target, gameObject, Health.Team, energy,
                attachDuration, enemyDamagePerSecond, allyHealPerSecond, energyPerTick);
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
