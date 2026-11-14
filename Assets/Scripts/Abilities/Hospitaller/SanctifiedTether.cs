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
    // Two charges, each recharging on its own (`rechargeTime`, from the click), so she can hold two characters at once,
    // typically shielding an ally while burning an enemy. Each link keeps its own timer. A click skips characters she
    // already holds; with both links up a click does nothing. `cooldown` is only the short gap between two casts.
    // She is free to walk and use her other abilities meanwhile; stuns and her counter do not break it. A link ends when
    // its time runs out, its character dies or gets too far from her, she dies, or she presses C (all of them).
    // A click that latches onto nothing gives its charge back. Dove Strike sends a dove to every enemy the tether holds.
    //
    // EX (Shift+click, costs energy, uses a charge like any cast): Penance. A darker beam that only takes on an enemy:
    // for as long as it holds, they receive `penanceHealCut` less healing and take `penanceExposed` more damage from
    // every source, so her team can call the kill on them. Shift+click on an ally is an ordinary tether and costs no
    // energy. Both effects are refreshed on the copy that resolves the enemy's health (the server online) and lapse a
    // moment after the beam ends.
    //
    // Online: her machine sends the full list of characters she now holds (SendTargets) just before the cue, so every
    // machine keeps the links in that list and adds the new one; pressing C sends an empty list, meaning "release".
    // The server's copy holds the links, so the damage share happens where damage is resolved.
    public class SanctifiedTether : Ability
    {
        const int MaxCharges = 2;

        [SerializeField] float duration = 4f;
        [SerializeField] float range = 9f;
        [SerializeField] float leashRange = 11f;
        [SerializeField] float aimRadius = 0.6f;
        [SerializeField] float tickInterval = 0.25f;
        [SerializeField] float tickDamage = 1.5f;
        [SerializeField] float energyPerTick = 2f;
        [SerializeField] float shareFraction = 0.35f;
        [SerializeField] float muzzleHeight = 1.4f;
        [SerializeField] float rechargeTime = 4f;
        [SerializeField] float exEnergyCost = 30f;
        [SerializeField] float penanceHealCut = 0.4f;
        [SerializeField] float penanceExposed = 0.1f;

        const float PenanceRefresh = 0.3f; // how long each refresh of Penance's effects lasts on the enemy

        static readonly RaycastHit[] Hits = new RaycastHit[16];
        static readonly Color EnemyColor = new Color(1f, 0.45f, 0.15f, 0.9f);
        static readonly Color AllyColor = new Color(1f, 0.9f, 0.5f, 0.9f);
        static readonly Color PenanceColor = new Color(0.75f, 0.08f, 0.1f, 0.95f);

        class Link
        {
            public Health Target;
            public bool Ally;
            public bool Penance;
            public float Until;
            public float Carried;      // her machine: damage carried on this ally link so far (Absolution)
            public float TakenAtStart; // the enemy's RoundDamageTaken when Penance latched (Damnation)
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
        readonly float[] chargeReady = new float[MaxCharges];

        LineRenderer fizzle;
        Material fizzleMaterial;
        float fizzleUntil;
        Vector3 fizzleEnd;

        public override string DisplayName => "Sanctified Tether";
        public override string KeyLabel => "LMB";
        public override int ChargeCapacity => MaxCharges;
        public override int ChargesReady => ReadyCharges;
        public override float ExCost => exEnergyCost;
        public override string ExName => "Penance";
        public override string ExDescription => "Enemy heals less, takes more damage";

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

        // Does the tether hold `target` under Penance? (Wrathful Doves)
        public bool IsPenanced(Health target)
        {
            foreach (Link l in links) if (l.Penance && l.Target == target) return true;
            return false;
        }

        // Takes `seconds` off the spent charge that is closest to coming back (Wrathful Doves). Her machine only.
        public void ReduceRecharge(float seconds)
        {
            int best = -1;
            for (int i = 0; i < MaxCharges; i++)
                if (chargeReady[i] > Time.time && (best < 0 || chargeReady[i] < chargeReady[best])) best = i;
            if (best >= 0) chargeReady[best] -= seconds;
        }

        bool Has(int bit) => HospitallerUpgrade.Has(gameObject, bit);

        // Shared Burden raises it. Read on the server (Share) and on her machine (Tick), both of which see the mask.
        float ShareFraction => Has(HospitallerUpgrade.SharedBurden) ? HospitallerUpgrade.SharedBurdenFraction : shareFraction;

        // ---- Charges ------------------------------------------------------------------------------------------------

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

        // Earliest time a spent charge comes back (only meaningful when at least one is spent).
        float NextCharge
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (float t in chargeReady) if (t > Time.time && t < next) next = t;
                return next;
            }
        }

        // With a charge in hand only the short gap between casts counts; with none, the wait for the next charge.
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

        // Both beams already out: nothing more to cast until one ends.
        public override bool Available => links.Count < MaxCharges;

        void Reset()
        {
            cooldown = 0.3f;
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

        protected override bool IsTriggered()
        {
            if (PrimaryFireBlocked || ReadyCharges == 0 || links.Count >= MaxCharges) return false;
            // Holding the button keeps a first beam going; the second beam needs its own click.
            return links.Count == 0 ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        }

        protected override void Activate()
        {
            if (IsReplica)
            {
                ReplayFromTargets();
                return;
            }

            Health target = Acquire(out Vector3 end);
            if (target == null)
            {
                // Nothing to hold: a brief flicker of light down the aim line, and the click costs nothing.
                Fizzle(end);
                RefundCooldown();
                return;
            }

            // Penance only takes on an enemy; Shift+click on an ally is an ordinary tether and costs no energy.
            bool penance = false;
            if (Input.GetKey(KeyCode.LeftShift) && target.Team != Health.Team)
            {
                if (!TrySpendEx(energy, exEnergyCost))
                {
                    RefundCooldown();
                    return;
                }
                penance = true;
                ExActivation = true;
            }

            // Spend the charge that has been ready longest; only that one starts recharging.
            int used = 0;
            for (int i = 1; i < MaxCharges; i++)
                if (chargeReady[i] < chargeReady[used]) used = i;
            chargeReady[used] = Time.time + Recharge;

            var held = new List<Health>();
            foreach (Link l in links) held.Add(l.Target);
            held.Add(target);
            SendTargets(held);
            Attach(target, penance);
            Sfx.Play(Sfx.MagicAttack1, 0.35f, penance ? 0.8f : 1.1f, penance ? 0.9f : 1.25f, 0.6f);
        }

        // A replica follows the list her machine sent just before this cue: links not in it have ended there, and any
        // character in it not held here yet is the new link. No targets means her click linked nothing.
        void ReplayFromTargets()
        {
            List<Health> targets = pendingTargets;
            pendingTargets = null;
            if (targets == null || targets.Count == 0) return;

            for (int i = links.Count - 1; i >= 0; i--)
            {
                if (targets.Contains(links[i].Target)) continue;
                Detach(links[i]);
                links.RemoveAt(i);
            }

            // Only the last one is new. An earlier one this machine has already let go of (its timer ran out a frame
            // sooner here) stays gone rather than coming back with a fresh timer.
            Health added = targets[targets.Count - 1];
            if (added == null || added.IsDead || added == Health || Holds(added) || links.Count >= MaxCharges) return;
            bool penance = ExActivation && added.Team != Health.Team; // the cue says whether this was the EX
            Attach(added, penance);
            Sfx.Play(Sfx.MagicAttack1, 0.35f, penance ? 0.8f : 1.1f, penance ? 0.9f : 1.25f, 0.6f);
        }

        public override void ReceiveTargets(List<Health> targets)
        {
            if (targets.Count == 0) DetachAll(); // her machine released the tether (C)
            else pendingTargets = targets;
        }

        bool Holds(Health h)
        {
            foreach (Link l in links) if (l.Target == h) return true;
            return false;
        }

        // The first living character along the aim that she doesn't already hold (never herself), or null if a wall or
        // nothing comes first.
        Health Acquire(out Vector3 end)
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
                if (health != null && (health == Health || health.IsDead || Holds(health))) continue;
                if (health != null && health.Structure && health.Team == Health.Team) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                bestHealth = health; // null = a wall got there first
                end = origin + dir * h.distance;
            }

            return bestHealth;
        }

        void Attach(Health target, bool penance = false)
        {
            bool ally = target.Team == Health.Team;
            penance &= !ally;
            var link = new Link
            {
                Target = target,
                Ally = ally,
                Penance = penance,
                Until = Time.time + (penance && Has(HospitallerUpgrade.Judgement) ? HospitallerUpgrade.JudgementDuration : duration),
                TakenAtStart = target.RoundDamageTaken,
                NextTick = Time.time + tickInterval,
                DamageSeen = target.RoundDamageTaken,
                BeamMaterial = VisualUtil.UnlitMaterial(ally ? AllyColor : penance ? PenanceColor : EnemyColor, 3002),
                GlowMaterial = VisualUtil.UnlitMaterial(ally ? new Color(1f, 0.92f, 0.55f, 0.35f)
                    : penance ? new Color(0.8f, 0.1f, 0.12f, 0.45f) : new Color(1f, 0.5f, 0.2f, 0.35f), 3001)
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
            foreach (Link l in links) End(l);
            links.Clear();
        }

        // A link is over, however it ended. Her machine pays out the upgrades that trigger on that, then it is removed.
        void End(Link l)
        {
            if (!IsReplica && !Health.IsDead && !Anathema.Net.NetGame.Frozen && l.Target != null && !l.Target.IsDead)
            {
                // Absolution: the ally is mended for a share of the pain she carried for them.
                if (l.Ally && l.Carried > 0f && Has(HospitallerUpgrade.Absolution))
                    Healing.Apply(l.Target, l.Carried * HospitallerUpgrade.AbsolutionFraction, gameObject);

                // Damnation: Penance burns its target for a share of everything they took while it held.
                if (l.Penance && Has(HospitallerUpgrade.Damnation))
                {
                    float taken = l.Target.RoundDamageTaken - l.TakenAtStart;
                    float burn = Mathf.Min(HospitallerUpgrade.DamnationMax, taken * HospitallerUpgrade.DamnationFraction);
                    if (burn >= 1f)
                    {
                        HospitallerFx.Burst(l.Target.transform.position, 1.4f, PenanceColor, 0.4f, 1f);
                        l.Target.Strike(Hit.Direct(burn), gameObject, landed => { if (landed) HitMarkerSound.Play(); });
                    }
                }
            }
            Detach(l);
        }

        // Runs where an ally's damage is resolved (the server online). Her part goes straight to her own health.
        // Registered once per ally link, and an ally is never held twice, so each ally shares exactly once.
        float Share(float amount, GameObject source)
        {
            if (Health.IsDead || links.Count == 0) return amount;
            float carried = amount * ShareFraction;
            Health.TakeDamage(carried, source);
            return amount - carried;
        }

        protected override void Update()
        {
            base.Update();

            if (links.Count > 0)
            {
                if (Health.IsDead || (!IsReplica && Anathema.Net.NetGame.Frozen))
                {
                    DetachAll();
                }
                else
                {
                    for (int i = links.Count - 1; i >= 0; i--)
                    {
                        Link l = links[i];
                        if (l.Target == null || l.Target.IsDead || Time.time >= l.Until
                            || FlatDistance(l.Target.transform.position) > leashRange)
                        {
                            End(l);
                            links.RemoveAt(i);
                            continue;
                        }
                        // Penance lives where the enemy's health is resolved, which may be a replica of her.
                        if (l.Penance && l.Target.IsAuthority)
                        {
                            l.Target.CutHealing(penanceHealCut, PenanceRefresh);
                            l.Target.Expose(Has(HospitallerUpgrade.Judgement) ? HospitallerUpgrade.JudgementExposed : penanceExposed,
                                PenanceRefresh);
                            if (Has(HospitallerUpgrade.MartyrsDue)) MartyrsDueMark.Refresh(l.Target, Health, PenanceRefresh);
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
                float f = ShareFraction;
                if (delta > 0f && f < 1f)
                {
                    float carried = delta * f / (1f - f);
                    l.Carried += carried;
                    BlessedSuffering.OnShared(gameObject, energy, carried);
                }
                return;
            }

            if (Time.time < l.NextTick) return;
            l.NextTick += tickInterval;

            float damage = tickDamage;
            Faith faith = Faith.For(gameObject);
            // Searing Light: the burn flares while her Faith runs high.
            if (Has(HospitallerUpgrade.SearingLight) && faith.Stacks >= HospitallerUpgrade.SearingLightMinStacks)
                damage *= HospitallerUpgrade.SearingLightScale;
            damage = BlessedSuffering.Scale(Health, damage);

            l.Target.Strike(Hit.Direct(damage), gameObject, landed =>
            {
                if (!landed) return;
                HitMarkerSound.Play();
                if (energy != null) energy.Gain(energyPerTick);
                // Zealous Fervour: burning the wicked builds Faith as well.
                if (Has(HospitallerUpgrade.ZealousFervour)) faith.AddZeal(damage);
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
            // Torn down with the scene: just clean up, no payoffs.
            foreach (Link l in links) Detach(l);
            links.Clear();
            if (fizzleMaterial != null) Destroy(fizzleMaterial);
        }
    }
}
