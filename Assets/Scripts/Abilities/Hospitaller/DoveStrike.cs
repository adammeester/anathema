using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // E: Dove Strike. The doves circling her head dive at her enemies: each pecks for a little damage and roots briefly.
    //   - Enemies held by Sanctified Tether: one dove homes in on each of them (as many as she has charges for) and
    //     cannot miss.
    //   - Nothing tethered: a skillshot. One dove flies straight along the aim and strikes the first enemy in its path;
    //     allies are flown past and walls stop it.
    // Three charges, one per dove, each recharging on its own: the circling flock shows how many are left. Only the
    // first root on a target counts: a target rooted by a dove cannot be rooted by another for a few seconds (later
    // dives still do their damage), so three in a row is not a 2-second lockdown.
    //
    // EX (Shift, costs energy): Golden Dove. The doves turn gold, hit a little harder and panic their targets instead
    // of rooting them; tethered to two enemies, both are sent running.
    public class DoveStrike : Ability
    {
        const int MaxCharges = HospitallerDoves.Count;

        [SerializeField] float range = 12f;
        [SerializeField] float hitRadius = 0.5f;
        [SerializeField] float damage = 8f;
        [SerializeField] float rootSeconds = 0.75f;
        [SerializeField] float rootLockout = 3f;
        [SerializeField] float rechargeTime = 8f;
        [SerializeField] float flightSpeed = 18f;
        [SerializeField] float energyPerHit = 6f;
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float exDamage = 10f;
        [SerializeField] float exPanicSeconds = 1.25f;

        Energy energy;
        readonly float[] chargeReady = new float[MaxCharges];
        readonly Dictionary<Health, float> rootableAt = new Dictionary<Health, float>();

        public override string DisplayName => "Dove Strike";
        public override string KeyLabel => "E";
        public override int ChargeCapacity => MaxCharges;
        public override int ChargesReady => ReadyCharges;
        public override float ExCost => exEnergyCost;
        public override string ExName => "Golden Dove";
        public override string ExDescription => "Panics instead of rooting";

        float Recharge => rechargeTime * CooldownScale;

        int ReadyCharges
        {
            get
            {
                int n = 0;
                foreach (float t in chargeReady) if (Time.time >= t) n++;
                return n;
            }
        }

        float NextCharge
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (float t in chargeReady) if (t > Time.time && t < next) next = t;
                return next;
            }
        }

        // With a dove in hand only the short gap between casts counts; with none, the wait for the next one.
        public override float CooldownRemaining =>
            ReadyCharges > 0 ? base.CooldownRemaining : Mathf.Max(0f, NextCharge - Time.time);

        public override float CooldownNormalized =>
            ReadyCharges > 0 ? base.CooldownNormalized : (Recharge > 0f ? Mathf.Clamp01(CooldownRemaining / Recharge) : 0f);

        public override float ChargeRechargeNormalized
        {
            get
            {
                int ready = ReadyCharges;
                if (ready == 0 || ready == MaxCharges || Recharge <= 0f) return 0f;
                return Mathf.Clamp01((NextCharge - Time.time) / Recharge);
            }
        }

        void Reset()
        {
            cooldown = 0.25f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.E) && ReadyCharges > 0;
        }

        protected override void Activate()
        {
            // Her machine decides EX; a replica has it from the cue. Charges are counted on every machine, because the
            // flock shows them.
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
            bool golden = ExActivation;

            List<Health> targets = TetheredEnemies();
            HospitallerDoves doves = HospitallerDoves.Ensure(gameObject);
            Sfx.Play(Sfx.MagicAttack1, 0.3f, golden ? 1.05f : 1.3f, golden ? 1.15f : 1.45f, 0.5f);

            if (targets.Count == 0)
            {
                // Skillshot straight along the aim.
                Vector3 dir = Aim.AimPoint - transform.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
                SpendCharge();
                HospitallerPose.For(gameObject).Point(transform.position + dir * 4f + Vector3.up * 1.2f, 0.3f);
                Launch(doves, golden).Aim(dir);
                return;
            }

            HospitallerPose.For(gameObject).Point(targets[0].transform.position + Vector3.up * 1.2f, 0.3f);
            foreach (Health target in targets)
            {
                if (ReadyCharges == 0) break;
                SpendCharge();
                Launch(doves, golden).Home(target);
            }
        }

        // Every enemy the tether holds that is still in reach, first link first.
        List<Health> TetheredEnemies()
        {
            var list = new List<Health>();
            var tether = GetComponent<SanctifiedTether>();
            if (tether == null) return list;
            foreach (Health h in tether.EnemyTargets)
            {
                Vector3 d = h.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= range * 1.25f) list.Add(h);
            }
            return list;
        }

        void SpendCharge()
        {
            int used = 0;
            for (int i = 1; i < MaxCharges; i++)
                if (chargeReady[i] < chargeReady[used]) used = i;
            chargeReady[used] = Time.time + Recharge;
        }

        Flight Launch(HospitallerDoves doves, bool golden)
        {
            Transform bird = doves.Take(out int id);
            if (golden) doves.SetGolden(id, true);
            var go = new GameObject(golden ? "Golden Dove" : "Dove Strike");
            Flight flight = go.AddComponent<Flight>();
            flight.Init(this, doves, bird, id, golden, IsReplica);
            return flight;
        }

        // On her machine (or offline) when a dove reaches its target.
        void OnArrive(Health target, bool golden)
        {
            if (target == null || target.IsDead) return;

            // Wrathful Doves: every stack of Faith sharpens the dive, and a dive on a Penance target hastens the tether.
            bool wrathful = HospitallerUpgrade.Has(gameObject, HospitallerUpgrade.WrathfulDoves);
            float bonus = wrathful ? HospitallerUpgrade.WrathfulDamagePerStack * Faith.For(gameObject).Stacks : 0f;
            var tether = GetComponent<SanctifiedTether>();
            bool hastens = wrathful && tether != null && tether.IsPenanced(target);

            Hit hit;
            if (golden)
            {
                hit = Hit.Direct(BlessedSuffering.Scale(Health, exDamage + bonus)).WithPanic(exPanicSeconds);
            }
            else
            {
                hit = Hit.Direct(BlessedSuffering.Scale(Health, damage + bonus));
                bool roots = !rootableAt.TryGetValue(target, out float at) || Time.time >= at;
                if (roots)
                {
                    hit = hit.WithRoot(rootSeconds);
                    rootableAt[target] = Time.time + rootLockout;
                }
            }

            target.Strike(hit, gameObject, landed =>
            {
                if (!landed) return;
                HitMarkerSound.Play();
                if (energy != null) energy.Gain(energyPerHit);
                if (hastens) tether.ReduceRecharge(HospitallerUpgrade.WrathfulRechargeRefund);
            });
        }

        protected override void Cancel()
        {
            // A dove already in the air finishes its dive.
        }

        // One dove's dive. Homing: it chases its target and cannot miss. Skillshot: it drops from the flock to chest
        // height and flies straight along the aim, striking the first enemy in its way (allies are flown past, walls
        // stop it). Afterwards the bird goes back into hiding until its charge returns. Without a bird to borrow (none
        // circling), a small glow makes the trip instead.
        class Flight : MonoBehaviour
        {
            const float LineHeight = 1.1f;
            const float DropTime = 0.2f;
            static readonly RaycastHit[] Hits = new RaycastHit[16];

            DoveStrike owner;
            HospitallerDoves doves;
            Transform bird;
            int birdId;
            bool golden;
            bool visualOnly;

            Health target;                 // homing
            bool straight;                 // skillshot
            Vector3 direction;
            Vector3 linePosition;          // the skillshot's true position, on the flat line at chest height
            float startHeightOffset;       // the bird starts up in the flock and drops onto the line
            float remaining;
            float start;
            float giveUpAt;

            Transform fallback;
            Material fallbackMaterial;

            public void Init(DoveStrike source, HospitallerDoves flock, Transform borrowed, int id, bool gold, bool visual)
            {
                owner = source;
                doves = flock;
                bird = borrowed;
                birdId = id;
                golden = gold;
                visualOnly = visual;
                start = Time.time;
                giveUpAt = Time.time + 3f;

                if (bird == null)
                {
                    fallbackMaterial = VisualUtil.UnlitMaterial(gold ? new Color(1f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 0.95f, 0.9f));
                    GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(s.GetComponent<Collider>());
                    s.GetComponent<Renderer>().sharedMaterial = fallbackMaterial;
                    s.transform.localScale = Vector3.one * 0.3f;
                    s.transform.position = source.transform.position + Vector3.up * 2.1f;
                    fallback = s.transform;
                }
                else if (golden) bird.localScale = Vector3.one * 1.2f; // the golden dove is bigger
            }

            Transform Body => bird != null ? bird : fallback;

            public void Home(Health victim) => target = victim;

            public void Aim(Vector3 dir)
            {
                straight = true;
                direction = dir;
                remaining = owner.range;
                linePosition = owner.transform.position + Vector3.up * LineHeight + dir * 0.4f;
                Transform body = Body;
                startHeightOffset = body != null ? body.position.y - linePosition.y : 0f;
            }

            void Update()
            {
                Transform body = Body;
                if (body == null || owner == null || Time.time >= giveUpAt)
                {
                    Finish();
                    return;
                }

                if (straight) FlyStraight(body);
                else FlyHoming(body);
            }

            void FlyHoming(Transform body)
            {
                if (target == null || target.IsDead)
                {
                    Finish();
                    return;
                }

                Vector3 aimAt = target.transform.position + Vector3.up * LineHeight;
                Vector3 to = aimAt - body.position;
                float step = owner.flightSpeed * Time.deltaTime;
                if (to.magnitude <= Mathf.Max(0.5f, step))
                {
                    Strike(target);
                    return;
                }

                body.position += to.normalized * step;
                body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(to.normalized), 12f * Time.deltaTime);
            }

            void FlyStraight(Transform body)
            {
                float step = Mathf.Min(owner.flightSpeed * Time.deltaTime, remaining);
                if (TryHit(step, out Health hitHealth, out float distance))
                {
                    linePosition += direction * distance;
                    body.position = linePosition;
                    if (hitHealth != null) Strike(hitHealth);
                    else Finish(); // a wall
                    return;
                }

                linePosition += direction * step;
                remaining -= step;
                float drop = 1f - Mathf.Clamp01((Time.time - start) / DropTime);
                body.position = linePosition + Vector3.up * (startHeightOffset * drop * drop);
                body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(direction), 15f * Time.deltaTime);
                if (remaining <= 0f) Finish();
            }

            // The nearest enemy or wall along this frame's step. Allies, the dead and the Hospitaller are flown past.
            bool TryHit(float distance, out Health found, out float at)
            {
                found = null;
                at = float.MaxValue;
                bool any = false;
                int count = Physics.SphereCastNonAlloc(linePosition, owner.hitRadius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit h = Hits[i];
                    Health health = h.collider.GetComponentInParent<Health>();
                    if (health != null && (health.IsDead || health.Team == owner.Health.Team)) continue;
                    if (h.distance >= at) continue;
                    at = h.distance;
                    found = health; // null = a wall
                    any = true;
                }
                return any;
            }

            void Strike(Health victim)
            {
                HospitallerFx.Burst(victim.transform.position, 0.9f,
                    golden ? new Color(1f, 0.8f, 0.3f, 0.9f) : new Color(1f, 1f, 0.9f, 0.9f), 0.3f, 0.8f);
                Sfx.Play(Sfx.MagicImpact, 0.3f, 1.3f, 1.5f, 0.5f);
                if (!visualOnly) owner.OnArrive(victim, golden);
                Finish();
            }

            void Finish()
            {
                if (bird != null && doves != null) doves.Return(birdId);
                bird = null;
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (bird != null && doves != null) doves.Return(birdId);
                if (fallback != null) Destroy(fallback.gameObject);
                if (fallbackMaterial != null) Destroy(fallbackMaterial);
            }
        }
    }
}
