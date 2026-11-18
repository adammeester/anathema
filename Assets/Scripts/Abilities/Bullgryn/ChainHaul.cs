using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // E: Chain Haul. After a short wind-up he hurls a hooked chain along his aim. The first character it reaches is
    // dragged to him: an enemy takes a little damage and is hauled in front of him; an ally takes none and is hauled to
    // him out of trouble. Walls stop the chain. C during the wind-up cancels it.
    public class ChainHaul : Ability
    {
        [SerializeField] float windup = 0.3f;
        [SerializeField] float range = 9f;
        [SerializeField] float speed = 30f;
        [SerializeField] float hookRadius = 0.45f;
        [SerializeField] float damage = 6f;
        [SerializeField] float stopDistance = 1.5f;     // hauled characters end up this far from him
        [SerializeField] float haulSpeed = 16f;         // metres per second of the drag
        [SerializeField] float energyPerHit = 10f;

        Energy energy;

        public override string DisplayName => "Chain Haul";
        public override string KeyLabel => "E";

        protected override float WindupDuration => windup;
        protected override Color WindupColor => BullgrynKit.Steel;

        void Reset()
        {
            cooldown = 12f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            if (!Input.GetKeyDown(KeyCode.E) || BullgrynKit.Busy(gameObject)) return false;
            return !(TryGetComponent(out BullCharge charge) && charge.IsCharging);
        }

        protected override void Activate()
        {
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint - transform.position : Aim.AimDirection;
            Vector3 dir = BullgrynKit.FlatAim(aim, transform);
            Sfx.Play(Sfx.AxeToss, 0.4f, 0.7f, 0.8f, 0.5f);
            ChainHook.Throw(this, transform.position + Vector3.up * 1.3f + dir * 0.8f, dir, range, speed, hookRadius);
        }

        protected override void Cancel()
        {
        }

        // The hook reached a character. Every copy draws the haul; only the owner's machine acts on it.
        internal float OnHooked(Health target)
        {
            Vector3 to = transform.position - target.transform.position;
            to.y = 0f;
            float pull = Mathf.Max(0f, to.magnitude - stopDistance);
            float seconds = Mathf.Max(0.1f, pull / haulSpeed);
            if (IsReplica || pull <= 0.05f) return seconds;

            Vector3 dir = to.normalized;
            if (target.Team == Health.Team)
            {
                BullgrynKit.HaulAlly(target.gameObject, dir, pull, seconds, gameObject);
                return seconds;
            }

            target.Strike(Hit.Direct(damage).WithKnockback(dir, pull, seconds), gameObject, landed =>
            {
                if (!landed) return;
                if (energy != null) energy.Gain(energyPerHit);
                HitMarkerSound.Play();
            });
            return seconds;
        }
    }

    // The flying hook and its chain back to the Bullgryn's hand. Flies straight until it reaches a character (not him,
    // not the dead) or solid scenery or runs out of range, then reels back in (with whoever it caught).
    public class ChainHook : MonoBehaviour
    {
        ChainHaul owner;
        Vector3 dir;
        float remaining, speed, radius;
        Health caught;
        float reelEnd, reelStart;
        bool reeling;
        LineRenderer chain;
        Transform hand;
        Material hookMaterial;

        public static void Throw(ChainHaul owner, Vector3 from, Vector3 dir, float range, float speed, float radius)
        {
            GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hook.name = "Chain Hook";
            Destroy(hook.GetComponent<Collider>());
            hook.transform.position = from;
            hook.transform.rotation = Quaternion.LookRotation(dir);
            hook.transform.localScale = new Vector3(0.3f, 0.12f, 0.4f);
            var h = hook.AddComponent<ChainHook>();
            h.owner = owner;
            h.dir = dir;
            h.remaining = range;
            h.speed = speed;
            h.radius = radius;
            h.hookMaterial = VisualUtil.UnlitMaterial(new Color(0.55f, 0.57f, 0.6f, 1f));
            hook.GetComponent<Renderer>().sharedMaterial = h.hookMaterial;
            h.chain = BullgrynKit.NewLine(hook.transform, new Color(0.45f, 0.47f, 0.5f, 1f), 0.07f, 2, false);
            Transform model = owner.transform.Find("Model");
            if (model != null)
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Hand_R") { h.hand = t; break; }
        }

        Vector3 HandPosition => hand != null ? hand.position : owner.transform.position + Vector3.up * 1.3f;

        void Update()
        {
            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!reeling) Fly();
            else Reel();

            if (this == null || chain == null) return;
            chain.SetPosition(0, HandPosition);
            chain.SetPosition(1, transform.position);
        }

        void Fly()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remaining);
            if (FindHit(step, out RaycastHit hit, out Health target))
            {
                transform.position = hit.point;
                if (target != null)
                {
                    caught = target;
                    StartReel(owner.OnHooked(target));
                    Sfx.Play(Sfx.ChainswordImpact, 0.35f, 0.8f, 0.9f, 0.3f);
                }
                else StartReel(0.2f);
                return;
            }
            transform.position += dir * step;
            remaining -= step;
            if (remaining <= 0f) StartReel(0.2f);
        }

        void StartReel(float seconds)
        {
            reeling = true;
            reelStart = Time.time;
            reelEnd = Time.time + seconds;
        }

        void Reel()
        {
            if (caught != null)
                transform.position = caught.transform.position + Vector3.up * 1.2f;
            else
            {
                float t = Mathf.Clamp01((Time.time - reelStart) / Mathf.Max(0.01f, reelEnd - reelStart));
                transform.position = Vector3.Lerp(transform.position, HandPosition, t);
            }
            if (Time.time >= reelEnd) Destroy(gameObject);
        }

        bool FindHit(float step, out RaycastHit best, out Health target)
        {
            best = default;
            target = null;
            float bestDistance = float.MaxValue;
            bool found = false;
            foreach (RaycastHit h in Physics.SphereCastAll(transform.position, radius, dir, step, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(owner.transform)) continue;
                Health health = h.collider.GetComponentInParent<Health>();
                if (health != null && (health.IsDead || health.Structure)) continue;
                if (h.distance >= bestDistance) continue;
                bestDistance = h.distance;
                best = h;
                target = health;
                found = true;
            }
            return found;
        }

        void OnDestroy()
        {
            if (hookMaterial != null) Destroy(hookMaterial);
            if (chain != null) Destroy(chain.sharedMaterial);
        }
    }
}
