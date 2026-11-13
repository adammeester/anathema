using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Ultimate (R): Psychic Blast. Costs a full energy bar. After a gather the Psyker unleashes a channelled beam
    // of white lightning that pierces everything in its path until it hits a wall. She is planted while channelling, but
    // the beam follows the cursor (at a limited turn rate), so it can be swept across enemies. Every tick damages each
    // enemy in the beam.
    // The gather is animated: she leans back, hauls the staff overhead and draws her free hand back while a ball of
    // warp light swells in it and rings of power close in on her; on release she lunges forward with the hand thrust
    // out along the beam.
    // The channel is interrupted by stuns, panic, silence and knockback (and by C or death). An interrupted blast keeps
    // its cooldown and the energy is gone; only pressing C during the gather refunds it.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes Spine and the arms every LateUpdate
    public class PsychicBlast : Ability
    {
        const int Arcs = 4;
        const int Segments = 20;
        const float ReleaseBlend = 0.12f;
        const float RecoverDuration = 0.3f;
        const float RingInterval = 0.16f;

        static readonly string[] BoneNames = { "Spine", "Head", "UpperArm_L", "UpperArm_R", "Hand_L" };

        [SerializeField] float energyCost = 100f;
        [SerializeField] float gatherDuration = 0.65f;
        [SerializeField] float channelDuration = 3f;
        [SerializeField] float range = 15f;
        [SerializeField] float beamRadius = 0.45f;
        [SerializeField] float tickInterval = 0.25f;
        [SerializeField] float damagePerTick = 8f;
        [SerializeField] float turnRate = 110f; // degrees per second the beam can swing toward the cursor
        [SerializeField] float originHeight = 1.2f;

        // Rotations in degrees about the character's right axis: negative pitches back / raises an arm forward-up.
        [Header("Pose")]
        [SerializeField] float gatherLean = -14f;        // leans back as the power builds
        [SerializeField] float gatherHead = -10f;        // chin up
        [SerializeField] float gatherStaffArm = -115f;   // staff hauled overhead
        [SerializeField] float gatherReachArm = 35f;     // free hand drawn back, cupping the gathering orb
        [SerializeField] float gatherTremble = 4f;       // the strain shakes her harder as the gather completes
        [SerializeField] float channelLean = 12f;        // lunges into the beam
        [SerializeField] float channelHead = 4f;
        [SerializeField] float channelStaffArm = -45f;
        [SerializeField] float channelReachArm = -85f;   // free hand thrust out along the beam
        [SerializeField] float orbSize = 0.4f;

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
        AudioSource voiceLine;

        LimbPose pose;
        bool posed;
        float lastLean, lastHead, lastStaff, lastReach;
        float fromLean, fromHead, fromStaff, fromReach;
        float recoverEndTime;
        float nextRing;
        Transform orb;
        Transform orbCore;
        Material orbMaterial;
        Material orbCoreMaterial;

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
            Voice.AnnounceUltimateReady(energy, Health, Voice.PsykerUltimateReady, () => energyCost);
            Voice.Preload(Voice.PsykerUltimate);
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        void OnDestroy()
        {
            DestroyBeam();
            DestroyOrb();
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
            nextRing = Time.time;
            beamDir = FlatAim();
            BuildBeam();
            BuildOrb();
            // The line starts with the cast itself and plays over the wind-up; it is cut off if the gather is cancelled.
            voiceLine = Voice.Shout(Voice.PsykerUltimate);
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

                // Rings of power closing in on her, tighter and brighter as the gather completes.
                if (Time.time >= nextRing)
                {
                    nextRing = Time.time + RingInterval;
                    Color c = Color.Lerp(PsykerFx.Cyan, Color.white, t);
                    c.a = 0.35f + 0.5f * t;
                    PsykerFx.Ring(transform.position, 2.6f - 0.8f * t, 0.35f, RingInterval * 2f, c, 0.08f + 0.1f * t);
                }

                if (t >= 1f)
                {
                    phase = Phase.Channel;
                    phaseStart = Time.time;
                    nextTick = Time.time;
                    struck.Clear();
                    fromLean = lastLean; fromHead = lastHead; fromStaff = lastStaff; fromReach = lastReach;
                    DestroyOrb();
                    PsykerFx.Ring(transform.position, 0.4f, 3.2f, 0.35f, Color.white, 0.3f);
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
            BeginRecover();
            if (!IsReplica) StartCooldown();
        }

        protected override void Cancel()
        {
            if (phase == Phase.Idle) return;

            bool gathering = phase == Phase.Gather;
            phase = Phase.Idle;
            DestroyBeam();
            DestroyOrb();
            BeginRecover();
            if (gathering) Sfx.Stop(voiceLine, 0.15f); // the blast never came: don't finish the war cry
            voiceLine = null;
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
            bool marked = false;
            struck.Clear();

            foreach (RaycastHit hit in Physics.SphereCastAll(start, beamRadius, direction, length, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = hit.collider.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;
                target.Strike(Hit.Direct(damagePerTick).AsAoe(), gameObject, landed =>
                {
                    if (!landed || marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
        }

        // ---- pose: wind-up, release and recovery --------------------------------------------------------------------

        void LateUpdate()
        {
            ApplyPose();
            UpdateOrb();
        }

        void BeginRecover()
        {
            fromLean = lastLean; fromHead = lastHead; fromStaff = lastStaff; fromReach = lastReach;
            recoverEndTime = Time.time + RecoverDuration;
        }

        // Gather: lean back, staff overhead, free hand drawn back, shaking with the strain. Channel: snap forward into
        // the beam with the hand thrust out. Afterwards: ease back to rest.
        void ApplyPose()
        {
            float lean, head, staff, reach, tremble = 0f;

            if (phase == Phase.Gather)
            {
                float raw = Mathf.Clamp01((Time.time - phaseStart) / gatherDuration);
                float t = Mathf.SmoothStep(0f, 1f, raw);
                lean = gatherLean * t;
                head = gatherHead * t;
                staff = gatherStaffArm * t;
                reach = gatherReachArm * t;
                tremble = gatherTremble * raw * raw * (Mathf.PerlinNoise(Time.time * 18f, 0.37f) * 2f - 1f);
            }
            else if (phase == Phase.Channel)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - phaseStart) / ReleaseBlend));
                lean = Mathf.Lerp(fromLean, channelLean, t);
                head = Mathf.Lerp(fromHead, channelHead, t);
                staff = Mathf.Lerp(fromStaff, channelStaffArm, t);
                reach = Mathf.Lerp(fromReach, channelReachArm, t);
                // The beam's recoil keeps her shuddering slightly while it runs.
                tremble = gatherTremble * 0.4f * (Mathf.PerlinNoise(Time.time * 22f, 0.71f) * 2f - 1f);
            }
            else if (Time.time < recoverEndTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEndTime - Time.time) / RecoverDuration);
                lean = Mathf.Lerp(fromLean, 0f, t);
                head = Mathf.Lerp(fromHead, 0f, t);
                staff = Mathf.Lerp(fromStaff, 0f, t);
                reach = Mathf.Lerp(fromReach, 0f, t);
            }
            else
            {
                if (posed)
                {
                    pose.Reset();
                    posed = false;
                }
                lastLean = lastHead = lastStaff = lastReach = 0f;
                return;
            }

            lastLean = lean; lastHead = head; lastStaff = staff; lastReach = reach;
            posed = true;

            Vector3 right = transform.right;
            pose.Reset();
            pose.Rotate("Spine", right, lean);
            pose.Rotate("Spine", transform.forward, tremble);
            pose.Rotate("Head", right, head);
            pose.Rotate("UpperArm_R", right, staff);
            pose.Rotate("UpperArm_L", right, reach);
        }

        // ---- the orb of warp light swelling in her free hand during the gather ---------------------------------------

        void BuildOrb()
        {
            DestroyOrb();
            orbMaterial = VisualUtil.UnlitMaterial(new Color(PsykerFx.Cyan.r, PsykerFx.Cyan.g, PsykerFx.Cyan.b, 0.35f), 3000);
            orbCoreMaterial = VisualUtil.UnlitMaterial(Color.white, 3001);
            orb = CreateSphere("Psychic Blast Orb", orbMaterial);
            orbCore = CreateSphere("Core", orbCoreMaterial);
            orbCore.SetParent(orb, false);
            orbCore.localScale = Vector3.one * 0.45f;
            orb.localScale = Vector3.zero;
        }

        static Transform CreateSphere(string name, Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            Destroy(sphere.GetComponent<Collider>());
            var renderer = sphere.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = material;
            return sphere.transform;
        }

        void UpdateOrb()
        {
            if (orb == null || phase != Phase.Gather) return;

            float t = Mathf.Clamp01((Time.time - phaseStart) / gatherDuration);
            Transform hand = pose.Get("Hand_L") ?? pose.Get("UpperArm_L");
            orb.position = hand != null ? hand.position : transform.position + Vector3.up * originHeight + transform.forward * 0.4f;
            // Swells over the gather and pulses faster as it nears release.
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * Mathf.Lerp(12f, 40f, t));
            orb.localScale = Vector3.one * orbSize * Mathf.Lerp(0.15f, 1f, t) * pulse;
            Color c = Color.Lerp(PsykerFx.Cyan, Color.white, t * t);
            c.a = 0.25f + 0.35f * t;
            orbMaterial.color = c;
        }

        void DestroyOrb()
        {
            if (orb != null) Destroy(orb.gameObject);
            orb = orbCore = null;
            if (orbMaterial != null) Destroy(orbMaterial);
            if (orbCoreMaterial != null) Destroy(orbCoreMaterial);
            orbMaterial = orbCoreMaterial = null;
        }

        // ---- visuals: layered, re-jittered line renderers, all white ----------------------------------------------------

        void BuildBeam()
        {
            DestroyBeam();
            beamRoot = new GameObject("Psychic Blast");
            Sfx.Loop(Sfx.ElectricBeam, beamRoot.transform, 0.45f, 0.9f);
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
