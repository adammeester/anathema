using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // R: Noospheric Snare. The Ranger plants a cog-mine of the Adeptus Mechanicus at the cursor (pulled in to range):
    // a brass gear-wheel around a galvanic core, sanctified with a burst of binharic cant. It is woven into the
    // noosphere, the Mechanicus' shared data-veil, so the Ranger and her allies see it through their own data-links
    // (a brass cog, a cyan core and thin data-threads rising from it) while enemies, cut off from that net, see
    // nothing at all.
    // After a short arming whirr it waits. The first enemy to step onto it sets it off: a galvanic arc-discharge
    // lashes out, damaging and stunning every enemy close by. Up to two snares at a time: planting a third dismantles
    // the oldest, and planting one close to another replaces it (so two can never be stacked into a chain stun). Each
    // lasts a while, then powers down; they are lost if the Ranger dies.
    //
    // Online: every machine plants its own copy from the cue (enemies' copies stay invisible). The owner's copy
    // decides when it is tripped, deals the damage and stun, and sends a second cue (the EX flag) so the other
    // machines set off their copies too.
    public class NoosphericSnare : Ability
    {
        [SerializeField] float range = 6f;
        [SerializeField] float plantWindup = 0.35f;
        [SerializeField] float armTime = 1f;
        [SerializeField] float lifetime = 25f;
        [SerializeField] float triggerRadius = 1.1f;
        [SerializeField] float blastRadius = 2.2f;
        [SerializeField] float damage = 20f;
        [SerializeField] float stunDuration = 1.25f;
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] int maxSnares = 2;
        [SerializeField] float minSpacing = 3f;   // a snare planted closer than this to another replaces it

        public static readonly Color Brass = new Color(0.72f, 0.55f, 0.25f, 1f);
        public static readonly Color Core = new Color(0.4f, 0.95f, 1f, 1f);

        Energy energy;
        // Oldest first. Every machine plants and replaces in the same order from the same cues, so the lists agree.
        readonly List<Snare> active = new List<Snare>();

        public override string DisplayName => "Noospheric Snare";
        public override string KeyLabel => "R";

        // The detonation cue on a replica fires at once; only planting winds up.
        protected override float WindupDuration => IsReplica && ExActivation ? 0f : plantWindup;
        protected override Color WindupColor => Core;

        void Reset()
        {
            cooldown = 14f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override void OnWindupStart()
        {
            RangerPose.For(gameObject).Aim(plantWindup + 0.1f, false);
            // Binharic cant: a short burst of machine chatter as the cog is sanctified.
            Sfx.Play(Sfx.TeleportCrackle, 0.25f, 1.5f, 1.7f, 0.4f);
        }

        protected override void OnWindupCancel() => RangerPose.For(gameObject).Release();

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R);

        protected override void Activate()
        {
            active.RemoveAll(s => s == null);

            // A replica told one of the owner's snares went off (the cue's aim point is where it stood): set off this
            // machine's copy of it.
            if (IsReplica && ExActivation)
            {
                Snare nearest = Nearest(Aim.AimPoint, float.MaxValue);
                if (nearest != null)
                {
                    active.Remove(nearest);
                    nearest.Detonate(false);
                }
                return;
            }

            Vector3 from = transform.position;
            Vector3 to = Aim.AimPoint - from;
            to.y = 0f;
            if (to.magnitude > range) to = to.normalized * range;
            Vector3 at = new Vector3(from.x + to.x, from.y, from.z + to.z);

            // Too close to another snare: that one is replaced. Otherwise, at the limit, the oldest goes.
            for (Snare near = Nearest(at, minSpacing); near != null; near = Nearest(at, minSpacing))
            {
                active.Remove(near);
                near.Dismantle();
            }
            while (active.Count >= Mathf.Max(1, maxSnares))
            {
                active[0].Dismantle();
                active.RemoveAt(0);
            }

            active.Add(Snare.Plant(this, at));
            Sfx.Play(Sfx.HammerSmash, 0.2f, 1.6f, 1.8f, 0.3f);
        }

        // The planted snare closest to `point` on the ground, within `within`, or null.
        Snare Nearest(Vector3 point, float within)
        {
            Snare best = null;
            float bestDistance = within;
            foreach (Snare s in active)
            {
                if (s == null) continue;
                Vector3 d = s.transform.position - point;
                d.y = 0f;
                if (d.magnitude <= bestDistance)
                {
                    bestDistance = d.magnitude;
                    best = s;
                }
            }
            return best;
        }

        // Owner: a snare was tripped. Hit everything in the blast and tell the other machines.
        void OnTripped(Snare snare)
        {
            if (!active.Remove(snare)) return;
            Vector3 at = snare.transform.position;
            snare.Detonate(true);

            if (Health.Net != null && Health.Net.IsSpawned)
            {
                int index = System.Array.IndexOf(GetComponents<Ability>(), this);
                Health.Net.SendCue(index + ExCueOffset, at);
            }
        }

        void Strike(Vector3 at, List<Health> struck)
        {
            Hit hit = Hit.Direct(damage).AsAoe().WithStun(stunDuration);
            foreach (Health target in struck)
            {
                target.Strike(hit, gameObject, landed =>
                {
                    if (!landed) return;
                    HitMarkerSound.Play();
                    if (energy != null) energy.Gain(energyPerHit);
                });
            }
        }

        // C doesn't recall planted snares; only the Ranger's death does.
        protected override void Cancel()
        {
            if (Health != null && Health.IsDead) DismantleAll();
        }

        void OnDestroy() => DismantleAll();

        void DismantleAll()
        {
            foreach (Snare s in active) if (s != null) s.Dismantle();
            active.Clear();
        }

        // True when this machine's player is on `team`, or there is no local player (offline): they see the snare.
        static bool VisibleHere(int team)
        {
            var local = Anathema.Net.NetPlayer.Local;
            return local == null || local.Health == null || local.Health.Team == team;
        }

        // The planted cog-mine.
        sealed class Snare : MonoBehaviour
        {
            const int Teeth = 10;
            const int Threads = 3;

            NoosphericSnare owner;
            int team;
            bool real;
            float plantedAt;
            float expires;
            bool done;

            Transform cog;
            Transform core;
            readonly List<Renderer> renderers = new List<Renderer>();
            readonly List<LineRenderer> threads = new List<LineRenderer>();
            readonly List<Material> materials = new List<Material>();
            Material coreMaterial;
            Material threadMaterial;

            public static Snare Plant(NoosphericSnare owner, Vector3 at)
            {
                var go = new GameObject("Noospheric Snare");
                go.transform.position = at;
                var s = go.AddComponent<Snare>();
                s.owner = owner;
                s.team = owner.Health.Team;
                s.real = !owner.IsReplica;
                s.plantedAt = Time.time;
                s.expires = Time.time + owner.lifetime;
                s.Build();
                return s;
            }

            void Build()
            {
                Material brass = VisualUtil.UnlitMaterial(Brass, 3000);
                Material dark = VisualUtil.UnlitMaterial(new Color(0.25f, 0.18f, 0.1f, 1f), 3000);
                coreMaterial = VisualUtil.UnlitMaterial(Core, 3002);
                threadMaterial = VisualUtil.UnlitMaterial(new Color(Core.r, Core.g, Core.b, 0.5f), 3001);
                materials.Add(brass);
                materials.Add(dark);
                materials.Add(coreMaterial);
                materials.Add(threadMaterial);

                // The cog: a flat brass wheel with square teeth around its rim and a dark hub ring.
                cog = new GameObject("Cog").transform;
                cog.SetParent(transform, false);
                cog.localPosition = Vector3.up * 0.06f;
                Part(PrimitiveType.Cylinder, cog, Vector3.zero, new Vector3(0.9f, 0.03f, 0.9f), brass);
                Part(PrimitiveType.Cylinder, cog, Vector3.up * 0.02f, new Vector3(0.55f, 0.03f, 0.55f), dark);
                for (int i = 0; i < Teeth; i++)
                {
                    float a = i * 360f / Teeth;
                    Transform tooth = Part(PrimitiveType.Cube, cog, Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.5f,
                        new Vector3(0.14f, 0.05f, 0.14f), brass);
                    tooth.localRotation = Quaternion.Euler(0f, a, 0f);
                }
                // Four spokes from hub to rim, like the cog of the Mechanicus icon.
                for (int i = 0; i < 4; i++)
                {
                    Transform spoke = Part(PrimitiveType.Cube, cog, Vector3.up * 0.03f, new Vector3(0.06f, 0.025f, 0.8f), brass);
                    spoke.localRotation = Quaternion.Euler(0f, 45f * i, 0f);
                }

                // The galvanic core: a glowing sphere at the hub.
                core = Part(PrimitiveType.Sphere, transform, Vector3.up * 0.12f, Vector3.one * 0.18f, coreMaterial);

                // Noosphere data-threads, rising from the core: the allies' data-links drawing it.
                for (int i = 0; i < Threads; i++)
                {
                    var t = new GameObject("Data Thread").AddComponent<LineRenderer>();
                    t.transform.SetParent(transform, false);
                    t.useWorldSpace = true;
                    t.positionCount = 6;
                    t.startWidth = 0.025f;
                    t.endWidth = 0f;
                    t.sharedMaterial = threadMaterial;
                    t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    threads.Add(t);
                    renderers.Add(t);
                }
                ApplyVisibility();
            }

            Transform Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
            {
                GameObject p = GameObject.CreatePrimitive(type);
                Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(parent, false);
                p.transform.localPosition = localPosition;
                p.transform.localScale = scale;
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers.Add(r);
                return p.transform;
            }

            void ApplyVisibility()
            {
                bool show = VisibleHere(team);
                foreach (Renderer r in renderers)
                    if (r != null && r.enabled != show) r.enabled = show;
            }

            bool Armed => Time.time >= plantedAt + owner.armTime;

            void Update()
            {
                if (done) return;
                if (owner == null || Time.time >= expires)
                {
                    Dismantle();
                    return;
                }

                // Decided every frame (cheap), so the local player spawning late is picked up.
                ApplyVisibility();
                Animate();

                if (!real || !Armed || Anathema.Net.NetGame.Frozen) return;
                foreach (Health h in FindObjectsByType<Health>())
                {
                    if (h.IsDead || h.Team == team || h.Structure) continue;
                    Vector3 d = h.transform.position - transform.position;
                    d.y = 0f;
                    if (d.magnitude > owner.triggerRadius) continue;
                    owner.OnTripped(this);
                    return;
                }
            }

            void Animate()
            {
                // The cog spins up while arming, then turns slowly; the core pulses brighter once armed.
                float arming = Mathf.Clamp01((Time.time - plantedAt) / owner.armTime);
                cog.Rotate(0f, Mathf.Lerp(540f, 40f, arming) * Time.deltaTime, 0f, Space.Self);
                float pulse = Armed ? 0.75f + 0.25f * Mathf.Sin(Time.time * 5f) : 0.3f + 0.4f * arming;
                Color c = Core;
                c.a = pulse;
                coreMaterial.color = c;
                core.localScale = Vector3.one * (0.14f + 0.06f * pulse);

                // Thin data-threads flickering up from the core.
                Vector3 baseAt = transform.position + Vector3.up * 0.12f;
                for (int i = 0; i < threads.Count; i++)
                {
                    LineRenderer t = threads[i];
                    float phase = Time.time * 2f + i * 2.1f;
                    for (int k = 0; k < t.positionCount; k++)
                    {
                        float f = k / (float)(t.positionCount - 1);
                        Vector3 sway = new Vector3(Mathf.Sin(phase + f * 6f), 0f, Mathf.Cos(phase * 1.3f + f * 5f)) * 0.08f * f;
                        t.SetPosition(k, baseAt + Vector3.up * (f * 0.9f) + sway);
                    }
                }
            }

            // The arc-discharge. Every machine draws it (enemies too: the snare reveals itself as it fires); with
            // `strike`, this is the owner's copy and it deals the hit.
            public void Detonate(bool strike)
            {
                if (done) return;
                done = true;
                Vector3 at = transform.position;
                Vector3 centre = at + Vector3.up * 0.3f;
                float radius = owner != null ? owner.blastRadius : 2.2f;

                var struck = new List<Health>();
                foreach (Health h in FindObjectsByType<Health>())
                {
                    if (h.IsDead || h.Team == team || h.Structure) continue;
                    Vector3 d = h.transform.position - at;
                    d.y = 0f;
                    if (d.magnitude <= radius) struck.Add(h);
                }

                // Galvanic arcs: one to each victim, the rest lashing out at random along the ground.
                foreach (Health h in struck) Arc(centre, h.transform.position + Vector3.up * 1f);
                for (int i = 0; i < 6; i++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    Arc(centre, at + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * radius);
                }
                RangerFx.Tracer(at + Vector3.up * 0.05f, at + Vector3.up * 1.6f, 0.5f, Core, 0.2f, 0.6f);
                Sfx.Play(Sfx.ElectricShock, 0.55f, 0.95f, 1.1f, 1f);

                if (strike && owner != null && struck.Count > 0) owner.Strike(at, struck);
                Destroy(gameObject);
            }

            // A jagged lightning arc from a to b, drawn as short tracer segments.
            static void Arc(Vector3 a, Vector3 b)
            {
                const int Segments = 5;
                Vector3 prev = a;
                for (int i = 1; i <= Segments; i++)
                {
                    float f = i / (float)Segments;
                    Vector3 next = Vector3.Lerp(a, b, f);
                    if (i < Segments) next += Random.insideUnitSphere * 0.25f;
                    RangerFx.Tracer(prev, next, 0.06f, Core, 0.25f);
                    prev = next;
                }
            }

            // Powered down (time ran out, a new one replaced it, or the Ranger died): it just goes dark.
            public void Dismantle()
            {
                if (done) return;
                done = true;
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                foreach (Material m in materials) if (m != null) Destroy(m);
            }
        }
    }
}
