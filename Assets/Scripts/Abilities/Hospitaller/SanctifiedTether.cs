using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // LMB: Sanctified Tether. Click to cast a beam of light from the icon along the aim; it latches onto the first
    // character it meets (walls stop it) and stays on them by itself for `duration` seconds:
    //   enemy -> the light burns them, a damage tick every quarter second (each landed tick builds energy);
    //   ally  -> she shares their pain: a part of every hit they take (after armour, before shields) is dealt to her
    //            instead, and that pain becomes energy (Blessed Suffering).
    // She is free to walk and use her other abilities meanwhile; stuns and her counter do not break it. A link ends when
    // the time runs out, its character dies or gets too far from her, she dies, or she presses C (all of them).
    // The cooldown starts on the click; a click that latches onto nothing is refunded.
    //
    // EX (Shift+click, costs energy, its own cooldown): Forked Tether. A second beam from her, so she holds two
    // characters at once (never more), enemy or ally, each burning at a reduced rate:
    //   - already holding one: the new beam latches onto the first other character along the aim, and ends with the
    //     first link;
    //   - holding none: one cast throws both beams, at the character along the aim and at the character nearest the
    //     cursor within a few metres of them.
    // The EX cooldown is separate from the normal one (the HUD shows it while Shift is held), and neither touches the
    // other. Dove Strike sends a dove to every enemy the tether holds.
    //
    // Online: her machine picks the targets and sends them (SendTargets) just before the cue, so every machine links the
    // same characters; pressing C sends an empty target list, meaning "release". The server's copy holds the links, so
    // the damage share happens where damage is resolved.
    public class SanctifiedTether : Ability
    {
        [SerializeField] float duration = 4f;
        [SerializeField] float range = 9f;
        [SerializeField] float leashRange = 11f;
        [SerializeField] float aimRadius = 0.6f;
        [SerializeField] float tickInterval = 0.25f;
        [SerializeField] float tickDamage = 1.5f;
        [SerializeField] float energyPerTick = 2f;
        [SerializeField] float shareFraction = 0.35f;
        [SerializeField] float muzzleHeight = 1.4f;
        [SerializeField] float exEnergyCost = 30f;
        [SerializeField] float exCooldown = 8f;
        [SerializeField] int exForks = 1;
        [SerializeField] float forkRange = 5f;
        [SerializeField] float exDamageScale = 0.7f;

        static readonly RaycastHit[] Hits = new RaycastHit[16];
        static readonly Color EnemyColor = new Color(1f, 0.45f, 0.15f, 0.9f);
        static readonly Color AllyColor = new Color(1f, 0.9f, 0.5f, 0.9f);

        class Link
        {
            public Health Target;
            public bool Ally;
            public float NextTick;
            public float DamageSeen; // the ally's RoundDamageTaken when last checked (Blessed Suffering)
            public LineRenderer Beam;
            public Material BeamMaterial;
            public Transform Glow;
            public Material GlowMaterial;
        }

        Energy energy;
        readonly List<Link> links = new List<Link>();
        readonly List<Health> enemyTargets = new List<Health>();
        List<Health> pendingTargets; // a replica: the targets her machine sent for the next cue
        float until;
        float damageScale = 1f;

        LineRenderer fizzle;
        Material fizzleMaterial;
        float fizzleUntil;
        Vector3 fizzleEnd;

        public override string DisplayName => "Sanctified Tether";
        public override string KeyLabel => "LMB";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Forked Tether";
        public override string ExDescription => "Forks to a second target";

        // The first character the tether holds (null if none).
        public Health Target => links.Count > 0 ? links[0].Target : null;

        // Every living enemy the tether holds, first link first.
        public IReadOnlyList<Health> EnemyTargets
        {
            get
            {
                enemyTargets.Clear();
                foreach (Link l in links)
                    if (!l.Ally && l.Target != null && !l.Target.IsDead) enemyTargets.Add(l.Target);
                return enemyTargets;
            }
        }

        void Reset()
        {
            cooldown = 6f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            HospitallerDoves.Ensure(gameObject);
            Faith.For(gameObject); // so the HUD shows the Faith pips from the start

            fizzleMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.9f, 0.5f, 0.4f), 3002);
            fizzle = NewBeam("Tether Fizzle", fizzleMaterial);
        }

        LineRenderer NewBeam(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer beam = go.AddComponent<LineRenderer>();
            beam.useWorldSpace = true;
            beam.positionCount = 12;
            beam.numCapVertices = 3;
            beam.sharedMaterial = material;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.enabled = false;
            return beam;
        }

        // ---- EX: its own cooldown, shown on the HUD while Shift is held ---------------------------------------------

        float exReadyAt;
        bool exRequested;
        float normalCooldownBefore;

        bool ExHeld => !IsReplica && Input.GetKey(KeyCode.LeftShift);
        float ExRemaining => Mathf.Max(0f, exReadyAt - Time.time);
        bool ExReady => ExRemaining <= 0f && links.Count < 2;

        // The EX goes off whatever the normal cooldown says.
        protected override bool HasSpareCharge => ExHeld && ExReady;

        public override float CooldownRemaining => ExHeld ? ExRemaining : base.CooldownRemaining;

        public override float CooldownNormalized =>
            ExHeld ? (exCooldown > 0f ? Mathf.Clamp01(ExRemaining / (exCooldown * CooldownScale)) : 0f) : base.CooldownNormalized;

        // With Shift held and two characters already held there is nothing to add: the EX slot greys out.
        public override bool Available => !ExHeld || links.Count < 2;

        protected override bool IsTriggered()
        {
            if (PrimaryFireBlocked) return false;

            if (ExHeld)
            {
                if (!ExReady || !Input.GetMouseButtonDown(0)) return false;
                exRequested = true;
                normalCooldownBefore = base.CooldownRemaining; // put back afterwards: the EX does not use it
                return true;
            }

            return links.Count == 0 && Input.GetMouseButton(0);
        }

        protected override void Activate()
        {
            if (IsReplica)
            {
                ReplayFromTargets();
                return;
            }

            if (exRequested)
            {
                exRequested = false;
                SetCooldownRemaining(normalCooldownBefore);
                CastEx();
                return;
            }

            DetachAll();
            Health first = Acquire(null, out Vector3 end);
            if (first == null)
            {
                // Nothing to hold: a brief flicker of light down the aim line, and the click costs nothing.
                Fizzle(end);
                RefundCooldown();
                return;
            }

            var targets = new List<Health> { first };
            SendTargets(targets);
            Begin(targets, false);
        }

        // Her machine: the EX. A miss costs nothing (no energy, no EX cooldown).
        void CastEx()
        {
            ExActivation = false;

            if (links.Count == 1)
            {
                // A second beam alongside the one she holds; it ends with the first.
                Health held = links[0].Target;
                Health second = Acquire(held, out Vector3 end);
                if (second == null)
                {
                    Fizzle(end);
                    return;
                }
                if (!TrySpendEx(energy, exEnergyCost)) return;

                ExActivation = true;
                exReadyAt = Time.time + exCooldown * CooldownScale;
                SendTargets(new List<Health> { held, second });
                damageScale = exDamageScale;
                Attach(second);
                Sfx.Play(Sfx.MagicAttack1, 0.35f, 1.2f, 1.35f, 0.6f);
                return;
            }

            DetachAll();
            Health first = Acquire(null, out Vector3 miss);
            if (first == null)
            {
                Fizzle(miss);
                return;
            }
            if (!TrySpendEx(energy, exEnergyCost)) return;

            ExActivation = true;
            exReadyAt = Time.time + exCooldown * CooldownScale;
            var targets = new List<Health> { first };
            AddFork(targets);
            SendTargets(targets);
            Begin(targets, true);
        }

        void Begin(List<Health> targets, bool ex)
        {
            damageScale = ex ? exDamageScale : 1f;
            until = Time.time + duration;
            foreach (Health h in targets)
                if (h != null && !h.IsDead && h != Health) Attach(h);
            if (links.Count > 0) Sfx.Play(Sfx.MagicAttack1, 0.35f, 1.1f, 1.25f, 0.6f);
        }

        // A replica follows the targets her machine sent just before this cue. No targets means her click linked
        // nothing (a miss, or a fork with no one to fork to), so there is nothing to change.
        void ReplayFromTargets()
        {
            List<Health> targets = pendingTargets;
            pendingTargets = null;
            if (targets == null || targets.Count == 0) return;

            // A fork of the tether already held: keep the first link (and its timer), add the new one.
            if (links.Count == 1 && targets.Count == 2 && targets[0] == links[0].Target)
            {
                damageScale = exDamageScale;
                if (targets[1] != null && !targets[1].IsDead) Attach(targets[1]);
                return;
            }

            DetachAll();
            Begin(targets, ExActivation);
        }

        public override void ReceiveTargets(List<Health> targets)
        {
            if (targets.Count == 0) DetachAll(); // her machine released the tether (C)
            else pendingTargets = targets;
        }

        // The first living character along the aim (never herself, and `skip` is passed through), or null if a wall or
        // nothing comes first.
        Health Acquire(Health skip, out Vector3 end)
        {
            Vector3 dir = Aim.AimPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();
            Vector3 origin = transform.position + Vector3.up * muzzleHeight;
            end = origin + dir * range;

            int count = Physics.SphereCastNonAlloc(origin, aimRadius, dir, Hits, range, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Health bestHealth = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                Health health = h.collider.GetComponentInParent<Health>();
                if (health != null && (health == Health || health == skip || health.IsDead)) continue;
                if (health != null && health.Structure && health.Team == Health.Team) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                bestHealth = health; // null = a wall got there first
                end = origin + dir * h.distance;
            }

            return bestHealth;
        }

        // EX: the other characters (either side) within reach of the first one, nearest the cursor first.
        void AddFork(List<Health> targets)
        {
            Health first = targets[0];
            Vector3 cursor = Aim.AimPoint;
            var candidates = new List<Health>();
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h == Health || h == first || h.IsDead || Stealthed.HiddenFrom(h.gameObject, Health.Team)) continue;
                if (h.Structure && h.Team == Health.Team) continue;
                Vector3 d = h.transform.position - first.transform.position;
                d.y = 0f;
                if (d.magnitude <= forkRange) candidates.Add(h);
            }

            candidates.Sort((a, b) =>
                FlatSqr(a.transform.position - cursor).CompareTo(FlatSqr(b.transform.position - cursor)));
            for (int i = 0; i < candidates.Count && i < exForks; i++) targets.Add(candidates[i]);
        }

        static float FlatSqr(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude;
        }

        void Attach(Health target)
        {
            bool ally = target.Team == Health.Team;
            var link = new Link
            {
                Target = target,
                Ally = ally,
                NextTick = Time.time + tickInterval,
                DamageSeen = target.RoundDamageTaken,
                BeamMaterial = VisualUtil.UnlitMaterial(ally ? AllyColor : EnemyColor, 3002),
                GlowMaterial = VisualUtil.UnlitMaterial(ally ? new Color(1f, 0.92f, 0.55f, 0.35f) : new Color(1f, 0.5f, 0.2f, 0.35f), 3001)
            };
            link.Beam = NewBeam("Tether Beam", link.BeamMaterial);
            link.Beam.enabled = true;

            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Tether Glow";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = link.GlowMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            link.Glow = s.transform;

            if (ally) target.AddDamageSharer(Share);
            links.Add(link);
        }

        void Detach(Link link)
        {
            if (link.Target != null && link.Ally) link.Target.RemoveDamageSharer(Share);
            if (link.Beam != null) Destroy(link.Beam.gameObject);
            if (link.Glow != null) Destroy(link.Glow.gameObject);
            if (link.BeamMaterial != null) Destroy(link.BeamMaterial);
            if (link.GlowMaterial != null) Destroy(link.GlowMaterial);
        }

        void DetachAll()
        {
            foreach (Link l in links) Detach(l);
            links.Clear();
        }

        // Runs where an ally's damage is resolved (the server online). Her part goes straight to her own health.
        float Share(float amount, GameObject source)
        {
            if (Health.IsDead || links.Count == 0) return amount;
            float carried = amount * shareFraction;
            Health.TakeDamage(carried, source);
            return amount - carried;
        }

        protected override void Update()
        {
            base.Update();

            if (links.Count > 0)
            {
                if (Health.IsDead || Time.time >= until || (!IsReplica && Anathema.Net.NetGame.Frozen))
                {
                    DetachAll();
                }
                else
                {
                    for (int i = links.Count - 1; i >= 0; i--)
                    {
                        Link l = links[i];
                        if (l.Target == null || l.Target.IsDead || FlatDistance(l.Target.transform.position) > leashRange)
                        {
                            Detach(l);
                            links.RemoveAt(i);
                            continue;
                        }
                        if (!IsReplica) Tick(l);
                    }
                }
            }

            DrawBeams();
        }

        void Tick(Link l)
        {
            if (l.Ally)
            {
                // Blessed Suffering: energy for the damage she bears. Energy lives on her own machine but the share is
                // dealt on the server, so it is worked out from the ally's (synced) damage taken: they keep (1 - f) of
                // each hit, so she carried f / (1 - f) of what they took.
                float taken = l.Target.RoundDamageTaken;
                float delta = taken - l.DamageSeen;
                l.DamageSeen = taken;
                if (delta > 0f && shareFraction < 1f)
                    BlessedSuffering.OnShared(gameObject, energy, delta * shareFraction / (1f - shareFraction));
                return;
            }

            if (Time.time < l.NextTick) return;
            l.NextTick += tickInterval;
            float scale = damageScale;
            l.Target.Strike(Hit.Direct(BlessedSuffering.Scale(Health, tickDamage * scale)), gameObject, landed =>
            {
                if (!landed) return;
                HitMarkerSound.Play();
                if (energy != null) energy.Gain(energyPerTick * scale);
            });
        }

        float FlatDistance(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void Fizzle(Vector3 end)
        {
            fizzleUntil = Time.time + 0.12f;
            fizzleEnd = end;
        }

        void DrawBeams()
        {
            HospitallerPose pose = HospitallerPose.For(gameObject);
            float t = Time.time;

            if (links.Count > 0)
            {
                Vector3 firstChest = links[0].Target.transform.position + Vector3.up * 1.2f;
                pose.Point(firstChest, 0.15f);
                Vector3 icon = pose.IconTip;

                for (int i = 0; i < links.Count; i++)
                {
                    Link l = links[i];
                    Vector3 end = l.Target.transform.position + Vector3.up * 1.2f;
                    // Every beam comes from her icon.
                    Shape(l.Beam, icon, end, t + i);
                    l.Glow.position = end;
                    l.Glow.localScale = Vector3.one * (0.55f + 0.1f * Mathf.Sin(t * 12f + i));
                }
            }

            if (t < fizzleUntil)
            {
                pose.Point(fizzleEnd, 0.15f);
                fizzle.enabled = true;
                Shape(fizzle, pose.IconTip, fizzleEnd, t);
            }
            else if (fizzle.enabled) fizzle.enabled = false;
        }

        static void Shape(LineRenderer beam, Vector3 start, Vector3 end, float t)
        {
            Vector3 dir = end - start;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            int n = beam.positionCount;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                float wobble = Mathf.Sin(k * Mathf.PI) * 0.08f * Mathf.Sin(t * 22f + k * 9f);
                beam.SetPosition(i, start + dir * k + side * wobble + Vector3.up * wobble * 0.5f);
            }
            float w = 0.1f + 0.03f * Mathf.Sin(t * 30f);
            beam.startWidth = w * 0.7f;
            beam.endWidth = w * 1.3f;
        }

        // Cancel also runs when her counter goes up and when an interrupting silence lands, and a replica cannot tell
        // those from C. The tether is already applied, so only C (on her machine) or death ends it here; C then
        // tells the other machines to let go.
        protected override void Cancel()
        {
            if (links.Count == 0) return;
            if (Health.IsDead)
            {
                DetachAll();
                return;
            }
            if (!IsReplica && Input.GetKeyDown(CancelKey))
            {
                DetachAll();
                SendTargets(new List<Health>());
            }
        }

        void OnDestroy()
        {
            DetachAll();
            if (fizzleMaterial != null) Destroy(fizzleMaterial);
        }
    }
}
