using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Right-click to loose a psychic lance. The knight focuses for a moment (a crackling telegraph line shows where the
    // lance will go and tracks the cursor), then the lance fires along the aim line, piercing every enemy in it
    // until it hits a wall. Pressing C during the focus cancels it before it fires and refunds the cooldown.
    public class Smite : Ability
    {
        const int TelegraphPoints = 12;

        [SerializeField] float damage = 30f;
        [SerializeField] float range = 16f;
        [SerializeField] float beamRadius = 0.35f;
        [SerializeField] float chargeDuration = 0.35f;
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] float originHeight = 1.2f;
        [SerializeField] Color telegraphColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] Color beamColor = new Color(0.5f, 0.8f, 1f, 0.95f);

        Energy energy;
        LineRenderer telegraph;
        Material telegraphMaterial;
        bool charging;
        AudioSource chargeSound;
        float fireTime;
        readonly HashSet<Health> struck = new HashSet<Health>();

        protected override bool CooldownStartsOnStrike => true;

        public override string DisplayName => "Smite";
        public override string KeyLabel => "RMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 4f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();

            var go = new GameObject("Smite Telegraph");
            go.transform.SetParent(transform, false);
            telegraph = go.AddComponent<LineRenderer>();
            telegraph.positionCount = TelegraphPoints;
            telegraph.useWorldSpace = true;
            telegraph.startWidth = telegraph.endWidth = beamRadius * 0.5f;
            telegraphMaterial = VisualUtil.UnlitMaterial(telegraphColor);
            telegraph.sharedMaterial = telegraphMaterial;
            telegraph.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            telegraph.enabled = false;
        }

        void OnDestroy()
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
        }

        protected override void Update()
        {
            base.Update();
            if (!charging) return;

            FindBeam(out Vector3 start, out Vector3 end);

            // The telegraph crackles with growing violence as the charge builds.
            float build = Mathf.Clamp01(1f - (fireTime - Time.time) / chargeDuration);
            Vector3 right = Vector3.Cross(Vector3.up, Aim.AimDirection).normalized;
            for (int i = 0; i < TelegraphPoints; i++)
            {
                float f = i / (float)(TelegraphPoints - 1);
                Vector3 p = Vector3.Lerp(start, end, f);
                if (i > 0 && i < TelegraphPoints - 1)
                    p += (right * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * (0.05f + 0.2f * build);
                telegraph.SetPosition(i, p);
            }
            telegraph.startWidth = telegraph.endWidth = beamRadius * (0.3f + 0.5f * build);
            Color tc = telegraphColor;
            tc.a = Mathf.Lerp(telegraphColor.a, 0.9f, build);
            telegraphMaterial.color = tc;

            if (Time.time >= fireTime) Fire(start, end);
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1);
        }

        protected override void Activate()
        {
            charging = true;
            fireTime = Time.time + chargeDuration;
            telegraph.enabled = true;
            chargeSound = GreyKnightSound.SmiteCharge();
        }

        protected override void Cancel()
        {
            if (!charging) return;

            GreyKnightSound.Stop(chargeSound);
            charging = false;
            telegraph.enabled = false;
            RefundCooldown();
        }

        // The beam runs from chest height along the aim direction and stops at the first wall or pillar.
        void FindBeam(out Vector3 start, out Vector3 end)
        {
            start = transform.position + Vector3.up * originHeight + Aim.AimDirection * 0.6f;
            float length = range;

            foreach (RaycastHit hit in Physics.SphereCastAll(start, beamRadius, Aim.AimDirection, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance > 0f) length = Mathf.Min(length, hit.distance);
            }

            end = start + Aim.AimDirection * length;
        }

        void Fire(Vector3 start, Vector3 end)
        {
            charging = false;
            telegraph.enabled = false;

            struck.Clear();
            var hitPoints = new List<Vector3>();
            Vector3 direction = (end - start).normalized;
            float length = Vector3.Distance(start, end);

            foreach (RaycastHit hit in Physics.SphereCastAll(start, beamRadius, direction, length, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = hit.collider.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;
                if (target.TakeDamage(damage, gameObject) && energy != null) energy.Gain(energyPerHit);
                hitPoints.Add(target.transform.position + Vector3.up * originHeight);
            }

            BeamEffect.Spawn(start, end, beamRadius * 2f, hitPoints);
            GreyKnightSound.Stop(chargeSound);
            GreyKnightSound.SmiteFire();
            GreyKnightSound.PsychicImpact(hitPoints.Count);
            StartCooldown();
        }

        // Psychic lightning: a wide violet glow, a white-hot core and several jagged arcs that re-roll every frame so
        // the bolt crackles, plus a spark burst at the end of the lance and on every enemy it pierces.
        class BeamEffect : MonoBehaviour
        {
            const float Duration = 0.4f;
            const int Arcs = 4;
            const int Segments = 18;
            const int SparksPerBurst = 7;

            struct Layer
            {
                public LineRenderer line;
                public Material material;
                public Color color;
                public float width;
                public float jitter;
            }

            readonly List<Layer> layers = new List<Layer>();
            readonly List<Layer> sparks = new List<Layer>();
            readonly List<Vector3> burstPoints = new List<Vector3>();
            Vector3 start, end;
            float startTime;

            public static void Spawn(Vector3 start, Vector3 end, float width, List<Vector3> hitPoints)
            {
                var go = new GameObject("Smite Lightning");
                var fx = go.AddComponent<BeamEffect>();
                fx.start = start;
                fx.end = end;
                fx.startTime = Time.time;

                fx.burstPoints.Add(end);
                fx.burstPoints.AddRange(hitPoints);

                var glow = new Color(0.1f, 0.4f, 1f, 0.35f);
                var arc = new Color(0.4f, 0.75f, 1f, 0.95f);
                fx.AddLayer(fx.layers, 2, width * 3.5f, glow, 0.05f, 3000);
                for (int i = 0; i < Arcs; i++) fx.AddLayer(fx.layers, Segments, width * 0.55f, arc, 0.55f, 3001);
                fx.AddLayer(fx.layers, 2, width * 0.8f, Color.white, 0.03f, 3002);

                for (int p = 0; p < fx.burstPoints.Count; p++)
                    for (int i = 0; i < SparksPerBurst; i++) fx.AddLayer(fx.sparks, 4, width * 0.25f, Color.white, 0.25f, 3002);
            }

            void AddLayer(List<Layer> list, int points, float width, Color color, float jitter, int queue)
            {
                var child = new GameObject("Layer");
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.positionCount = points;
                line.useWorldSpace = true;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Material material = VisualUtil.UnlitMaterial(color, queue);
                line.sharedMaterial = material;
                list.Add(new Layer { line = line, material = material, color = color, width = width, jitter = jitter });
            }

            void OnDestroy()
            {
                foreach (Layer l in layers) if (l.material != null) Destroy(l.material);
                foreach (Layer l in sparks) if (l.material != null) Destroy(l.material);
            }

            void Update()
            {
                float t = (Time.time - startTime) / Duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                // Fast fade with an occasional strobe dip.
                float alpha = (1f - t * t) * (Random.value < 0.25f ? 0.4f : 1f);

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
                            // Offsets taper to zero at both ends so every arc stays anchored to the lance.
                            float amp = l.jitter * (0.6f + 0.8f * t) * Mathf.Sin(f * Mathf.PI);
                            p += (right * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * amp;
                        }
                        l.line.SetPosition(k, p);
                    }

                    float w = l.width * (1f - t * 0.7f) * (n > 2 ? Random.Range(0.6f, 1.2f) : 1f);
                    l.line.startWidth = l.line.endWidth = w;
                    Color c = l.color;
                    c.a = l.color.a * alpha;
                    l.material.color = c;
                }

                int s = 0;
                foreach (Vector3 origin in burstPoints)
                {
                    for (int i = 0; i < SparksPerBurst && s < sparks.Count; i++, s++)
                    {
                        Layer sp = sparks[s];
                        // Each spark is a short jagged streak that flies outward and shrinks.
                        Vector3 outward = Quaternion.AngleAxis(360f / SparksPerBurst * i + s * 37f, Vector3.up) * right;
                        outward += Vector3.up * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(s * 12.9898f)));
                        outward.Normalize();
                        float reach = Mathf.Lerp(0.2f, 1.6f, t);
                        sp.line.SetPosition(0, origin + outward * reach * 0.6f);
                        sp.line.SetPosition(1, origin + outward * reach * 0.8f);
                        sp.line.SetPosition(2, origin + outward * reach * 0.9f + Random.insideUnitSphere * 0.06f);
                        sp.line.SetPosition(3, origin + outward * reach);
                        sp.line.startWidth = sp.width * (1f - t);
                        sp.line.endWidth = 0f;
                        Color c = sp.color;
                        c.a = 1f - t;
                        sp.material.color = c;
                    }
                }
            }
        }
    }
}
