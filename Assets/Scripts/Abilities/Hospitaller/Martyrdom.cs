using System.Collections.Generic;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // R: Martyrdom. Costs a full energy bar. She raises the icon and holy fire gathers around her (rooted, 0.9s): embers
    // stream in from all sides and flames climb her body, so everyone can see it coming. Then for 5s she takes on her
    // team's pain:
    //   - every ally within 10m at the moment it lands is bound to her by a burning thread; half of every hit they take
    //     is dealt to her instead (after their armour, before their shields), and she takes 40% less damage herself;
    //   - the shared damage never kills her (it stops at 1 health); direct hits still can;
    //   - she burns the whole time, and the damage she bears feeds Blessed Suffering (energy, and once she drops below
    //     40% health, a harder burst).
    // When it ends (time runs out, or she dies) the pain bursts out of her as a ring of holy fire racing outwards:
    // every enemy it reaches is burned for a base amount plus a share of everything she absorbed, and pushed back.
    // A thread snaps if its ally dies or gets too far away. Stuns, silence, panic and knockback during the gathering
    // interrupt it and C cancels it (energy is only spent when it lands); once it lands nothing stops it but death.
    //
    // Online: every machine replays the cast from the cue and builds the threads; the server's copy is where the
    // sharing and her damage reduction actually happen. Her own machine works out how much she absorbed from the
    // allies' synced damage totals (as the tether does) and deals the burst.
    public class Martyrdom : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float gatherTime = 0.9f;
        [SerializeField] float duration = 5f;
        [SerializeField] float linkRadius = 10f;
        [SerializeField] float leashRange = 14f;
        [SerializeField] float shareFraction = 0.5f;
        [SerializeField] float damageReduction = 0.4f;
        [SerializeField] float burstRadius = 6f;
        [SerializeField] float burstExpandTime = 0.55f;
        [SerializeField] float burstBaseDamage = 10f;
        [SerializeField] float burstPerAbsorbed = 0.8f;
        [SerializeField] float burstMaxDamage = 80f;
        [SerializeField] float burstKnockback = 3f;

        public static readonly PsychicFireRing.Palette HolyFire = PsychicFireRing.Palette.Make(
            new Color(1f, 0.32f, 0.05f), new Color(1f, 0.7f, 0.25f), new Color(1f, 0.97f, 0.82f), new Color(1f, 0.6f, 0.25f));

        static readonly Color ThreadColor = new Color(1f, 0.6f, 0.2f, 0.85f);

        class Link
        {
            public Health Ally;
            public LineRenderer Line;
            public float DamageSeen;
        }

        Energy energy;
        readonly List<Link> links = new List<Link>();
        Material threadMaterial;
        PsychicFireRing fire;
        AudioSource fireSound;
        float gatherStart;
        bool active;
        float until;
        float absorbed;

        public override string DisplayName => "Martyrdom";
        public override string KeyLabel => "R";
        public override bool RootsMovement => IsWindingUp;
        public override bool Available => energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject);

        protected override float WindupDuration => gatherTime;
        protected override Color WindupColor => new Color(1f, 0.55f, 0.15f, 1f);

        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            threadMaterial = VisualUtil.UnlitMaterial(ThreadColor, 3002);
        }

        protected override bool IsTriggered()
        {
            return !active && Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;
        }

        // ---- Gathering (the wind-up: shown on every machine) ---------------------------------------------------------

        protected override void OnWindupStart()
        {
            gatherStart = Time.time;
            StartFire();
            HospitallerPose.For(gameObject).Raise(gatherTime + 0.3f);
        }

        protected override void OnWindupCancel()
        {
            StopFire();
            HospitallerPose.For(gameObject).Release();
        }

        void StartFire()
        {
            if (fire == null) fire = PsychicFireRing.CreateChannel(HolyFire);
            if (fireSound == null) fireSound = Sfx.Loop(Sfx.GroundFire, transform, 0.3f, 1.05f, 0.2f);
        }

        void StopFire()
        {
            if (fire != null) fire.EndChannel();
            fire = null;
            Sfx.Stop(fireSound, 0.4f);
            fireSound = null;
        }

        // ---- Landing --------------------------------------------------------------------------------------------------

        protected override void Activate()
        {
            // TrySpend always succeeds on a replica (the owner already paid).
            if (energy == null || !energy.TrySpend(energyCost))
            {
                StopFire();
                RefundCooldown();
                return;
            }

            active = true;
            until = Time.time + duration;
            absorbed = 0f;
            StartFire();
            Sfx.Play(Sfx.MagicAttack3, 0.45f, 0.8f, 0.9f, 1.2f);

            // Where her health is decided (the server online), she hardens for the whole of it.
            if (Health.IsAuthority) Health.GrantTemporaryArmor(damageReduction, duration);

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h == Health || h.IsDead || h.Structure || h.Team != Health.Team) continue;
                if (FlatDistance(h.transform.position) > linkRadius) continue;
                Bind(h);
            }
        }

        void Bind(Health ally)
        {
            var go = new GameObject("Martyr Thread");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 10;
            line.numCapVertices = 2;
            line.sharedMaterial = threadMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            links.Add(new Link { Ally = ally, Line = line, DamageSeen = ally.RoundDamageTaken });
            ally.AddDamageSharer(Share);
        }

        void Unbind(Link link)
        {
            if (link.Ally != null) link.Ally.RemoveDamageSharer(Share);
            if (link.Line != null) Destroy(link.Line.gameObject);
        }

        // Runs where an ally's damage is resolved (the server online): half of it is hers, but it never kills her.
        float Share(float amount, GameObject source)
        {
            if (!active || Health.IsDead) return amount;
            float carried = Mathf.Min(amount * shareFraction, Mathf.Max(0f, Health.Current - 1f));
            if (carried <= 0f) return amount;
            Health.TakeDamage(carried, source);
            return amount - carried;
        }

        // ---- While it burns ---------------------------------------------------------------------------------------

        protected override void Update()
        {
            base.Update();
            Vector3 feet = transform.position;

            if (IsWindingUp && fire != null)
            {
                float progress = Mathf.Clamp01((Time.time - gatherStart) / gatherTime);
                fire.UpdateChannel(feet, 4f, progress);
                fire.UpdateWreath(feet, 0.35f, 1.9f, 0.3f + 0.7f * progress);
            }

            if (!active) return;

            if (Health.IsDead || Time.time >= until)
            {
                Burst();
                return;
            }

            if (fire != null) fire.UpdateWreath(feet, 0.4f, 2f, 1f);
            UpdateThreads();
        }

        void UpdateThreads()
        {
            Vector3 chest = transform.position + Vector3.up * 1.3f;
            float t = Time.time;

            for (int i = links.Count - 1; i >= 0; i--)
            {
                Link link = links[i];
                if (link.Ally == null || link.Ally.IsDead || FlatDistance(link.Ally.transform.position) > leashRange)
                {
                    Unbind(link);
                    links.RemoveAt(i);
                    continue;
                }

                // Her machine counts what she carried: the ally kept (1 - f) of each hit, so she took f / (1 - f) of it.
                if (!IsReplica)
                {
                    float taken = link.Ally.RoundDamageTaken;
                    float delta = taken - link.DamageSeen;
                    link.DamageSeen = taken;
                    if (delta > 0f && shareFraction < 1f)
                    {
                        float carried = delta * shareFraction / (1f - shareFraction);
                        absorbed += carried;
                        BlessedSuffering.OnShared(gameObject, energy, carried);
                    }
                }

                // A flickering thread of fire from her chest to theirs.
                Vector3 end = link.Ally.transform.position + Vector3.up * 1.2f;
                Vector3 dir = end - chest;
                Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
                int n = link.Line.positionCount;
                for (int k = 0; k < n; k++)
                {
                    float f = k / (float)(n - 1);
                    float wobble = Mathf.Sin(f * Mathf.PI) * 0.12f * Mathf.Sin(t * 18f + f * 7f + i);
                    link.Line.SetPosition(k, chest + dir * f + side * wobble + Vector3.up * (Mathf.Sin(f * Mathf.PI) * 0.25f));
                }
                float w = 0.07f + 0.03f * Mathf.Sin(t * 25f + i);
                link.Line.startWidth = w;
                link.Line.endWidth = w * 0.6f;
            }
        }

        // ---- The end: the pain bursts out as a ring of fire ----------------------------------------------------------

        void Burst()
        {
            active = false;
            foreach (Link link in links) Unbind(link);
            links.Clear();
            StopFire();

            Vector3 centre = transform.position;
            PsychicFireRing.Spawn(centre, burstRadius, burstExpandTime, HolyFire);
            Sfx.Play(Sfx.SlamExplosion, 0.5f, 0.9f, 1f, 1.5f);
            if (IsReplica) return;

            float damage = Mathf.Min(burstMaxDamage, burstBaseDamage + absorbed * burstPerAbsorbed);
            damage = BlessedSuffering.Scale(Health, damage);
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team) continue;
                Vector3 to = h.transform.position - centre;
                to.y = 0f;
                if (to.magnitude > burstRadius) continue;
                Vector3 push = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                h.Strike(Hit.Direct(damage).AsAoe().WithKnockback(push, burstKnockback, 0.3f), gameObject, landed =>
                {
                    if (landed) HitMarkerSound.Play();
                });
            }
        }

        float FlatDistance(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        protected override void Cancel()
        {
            // The gathering is cancelled by the base class (refunded). Once it has landed only her death ends it, and
            // that is handled in Update so the burst still goes off.
        }

        void OnDestroy()
        {
            foreach (Link link in links) Unbind(link);
            links.Clear();
            if (fire != null) Destroy(fire.gameObject);
            if (threadMaterial != null) Destroy(threadMaterial);
        }
    }
}
