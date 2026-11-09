using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Ultimate (R): Psychic Blast. Costs a full energy bar. After a brief gather the Psyker unleashes a channelled beam
    // of white lightning that pierces everything in its path until it hits a wall. She is planted while channelling, but
    // the beam follows the cursor (at a limited turn rate), so it can be swept across enemies. Every tick damages each
    // enemy in the beam.
    // The channel is interrupted by stuns, panic, silence and knockback (and by C or death). An interrupted blast keeps
    // its cooldown and the energy is gone; only pressing C during the gather refunds it.
    public class PsychicBlast : Ability
    {
        const int Arcs = 4;
        const int Segments = 20;

        [SerializeField] float energyCost = 100f;
        [SerializeField] float gatherDuration = 0.4f;
        [SerializeField] float channelDuration = 3f;
        [SerializeField] float range = 15f;
        [SerializeField] float beamRadius = 0.45f;
        [SerializeField] float tickInterval = 0.25f;
        [SerializeField] float damagePerTick = 7f;
        [SerializeField] float turnRate = 110f; // degrees per second the beam can swing toward the cursor
        [SerializeField] float originHeight = 1.2f;

        enum Phase { Idle, Gather, Channel }

        struct Layer
        {
            public LineRenderer line;
            public Material material;
            public float width;
            public float jitter;
            public float alpha;
        }

        Energy energy;
        Phase phase;
        float phaseStart;
        float nextTick;
        Vector3 beamDir;
        readonly List<Layer> layers = new List<Layer>();
        readonly HashSet<Health> struck = new HashSet<Health>();
        GameObject beamRoot;

        protected override bool CooldownStartsOnStrike => true;

        public override string DisplayName => "Psychic Blast";
        public override string KeyLabel => "R";
        public override bool RootsMovement => phase != Phase.Idle;
        public override bool Available => energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject);

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        void OnDestroy()
        {
            DestroyBeam();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;
        }

        protected override void Activate()
        {
            // Replicas replay the visuals only; the owner already spent the energy here.
            if (!IsReplica && (energy == null || !energy.TrySpend(energyCost)))
            {
                RefundCooldown();
                return;
            }

            phase = Phase.Gather;
            phaseStart = Time.time;
            beamDir = FlatAim();
            BuildBeam();
        }

        Vector3 FlatAim()
        {
            Vector3 d = Aim.AimDirection;
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
        }

        protected override void Update()
        {
            base.Update();
            if (phase == Phase.Idle) return;

            if (Health.IsDead) return; // Cancel() has already run

            if (!IsReplica && IsInterrupted())
            {
                Interrupt();
                return;
            }

            // The beam swings toward the cursor at a limited rate, so it has to be dragged across targets.
            Vector3 aim = FlatAim();
            beamDir = Vector3.RotateTowards(beamDir, aim, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
            beamDir.y = 0f;
            beamDir.Normalize();

            FindBeam(out Vector3 start, out Vector3 end);

            if (phase == Phase.Gather)
            {
                float t = Mathf.Clamp01((Time.time - phaseStart) / gatherDuration);
                DrawBeam(start, end, 0.1f + 0.25f * t, true);
                if (t >= 1f)
                {
                    phase = Phase.Channel;
                    phaseStart = Time.time;
                    nextTick = Time.time;
                    struck.Clear();
                }
                return;
            }

            DrawBeam(start, end, 1f, false);
            if (!IsReplica && Time.time >= nextTick)
            {
                nextTick += tickInterval;
                Damage(start, end);
            }

            if (Time.time - phaseStart >= channelDuration) End();
        }

        bool IsInterrupted()
        {
            return Stunned.IsStunned(gameObject) || Panicked.IsPanicked(gameObject) || Silenced.IsSilenced(gameObject)
                   || GetComponent<Knockback>() != null;
        }

        // Owner only: stop, and tell the other machine to stop drawing the beam too (a -1 cue cancels abilities).
        void Interrupt()
        {
            Cancel();
            if (Health.Net != null && Health.Net.IsSpawned) Health.Net.SendCue(-1, Aim.AimPoint);
        }

        void End()
        {
            phase = Phase.Idle;
            DestroyBeam();
            if (!IsReplica) StartCooldown();
        }

        protected override void Cancel()
        {
            if (phase == Phase.Idle) return;

            bool gathering = phase == Phase.Gather;
            phase = Phase.Idle;
            DestroyBeam();
            if (IsReplica) return;

            // Cancelled before the beam started: refund. Once it has fired the energy stays spent.
            if (gathering && !Health.IsDead && !IsInterrupted() && energy != null)
            {
                energy.Gain(energyCost);
                RefundCooldown();
            }
            else
            {
                StartCooldown();
            }
        }

        // The beam runs from chest height along beamDir and stops at the first wall or pillar.
        void FindBeam(out Vector3 start, out Vector3 end)
        {
            start = transform.position + Vector3.up * originHeight + beamDir * 0.6f;
            float length = range;

            foreach (RaycastHit hit in Physics.SphereCastAll(start, beamRadius, beamDir, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance > 0f) length = Mathf.Min(length, hit.distance);
            }

            end = start + beamDir * length;
        }

        void Damage(Vector3 start, Vector3 end)
        {
            Vector3 direction = (end - start).normalized;
            float length = Vector3.Distance(start, end);
            bool hitAny = false;
            struck.Clear();

            foreach (RaycastHit hit in Physics.SphereCastAll(start, beamRadius, direction, length, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = hit.collider.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;
                if (target.TakeDamage(damagePerTick, gameObject, true)) hitAny = true;
            }

            if (hitAny) HitMarkerSound.Play();
        }

        // ---- visuals: layered, re-jittered line renderers, all white ----------------------------------------------------

        void BuildBeam()
        {
            DestroyBeam();
            beamRoot = new GameObject("Psychic Blast");
            AddLayer(2, beamRadius * 3.2f, 0.04f, 0.28f, 3000);
            for (int i = 0; i < Arcs; i++) AddLayer(Segments, beamRadius * 0.45f, 0.5f, 0.95f, 3001);
            AddLayer(2, beamRadius * 0.9f, 0.03f, 1f, 3002);
        }

        void AddLayer(int points, float width, float jitter, float alpha, int queue)
        {
            var child = new GameObject("Layer");
            child.transform.SetParent(beamRoot.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.positionCount = points;
            line.useWorldSpace = true;
            line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material material = VisualUtil.UnlitMaterial(new Color(1f, 1f, 1f, alpha), queue);
            line.sharedMaterial = material;
            layers.Add(new Layer { line = line, material = material, width = width, jitter = jitter, alpha = alpha });
        }

        void DrawBeam(Vector3 start, Vector3 end, float strength, bool telegraph)
        {
            if (beamRoot == null) return;

            Vector3 dir = (end - start).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            right = right.sqrMagnitude < 0.0001f ? Vector3.right : right.normalized;

            foreach (Layer l in layers)
            {
                int n = l.line.positionCount;
                for (int k = 0; k < n; k++)
                {
                    float f = k / (float)(n - 1);
                    Vector3 p = Vector3.Lerp(start, end, f);
                    if (k > 0 && k < n - 1)
                    {
                        float amp = l.jitter * (0.5f + 0.5f * strength) * Mathf.Sin(f * Mathf.PI);
                        p += (right * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * amp;
                    }
                    l.line.SetPosition(k, p);
                }

                float w = l.width * strength * (n > 2 ? Random.Range(0.6f, 1.25f) : Random.Range(0.9f, 1.1f));
                l.line.startWidth = l.line.endWidth = w;
                Color c = Color.white;
                // The gather shows a thin, faint line that swells into the beam; once firing it strobes slightly.
                c.a = l.alpha * (telegraph ? 0.35f : (Random.value < 0.15f ? 0.6f : 1f));
                l.material.color = c;
            }
        }

        void DestroyBeam()
        {
            foreach (Layer l in layers)
                if (l.material != null) Destroy(l.material);
            layers.Clear();
            if (beamRoot != null) Destroy(beamRoot);
            beamRoot = null;
        }
    }
}
