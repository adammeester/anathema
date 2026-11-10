using System;
using System.Collections.Generic;
using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // The Psycannon's round: a ball of psychic energy that flies straight and explodes when it touches an enemy or a wall,
    // or when it reaches the end of its range. The blast damages everything around it (falling off towards the edge).
    // Every machine flies its own copy so the shot always shows; only the owner's copy (`deals`) applies damage.
    public class PsychicShell : MonoBehaviour
    {
        public const float Damage = 26f;
        public const float BlastRadius = 2.3f;
        public const float Speed = 17f;
        public const float Range = 15f;
        public const float MinFalloff = 0.55f;   // damage at the very edge of the blast, as a fraction of the centre's
        const float HitRadius = 0.3f;

        static readonly RaycastHit[] Hits = new RaycastHit[16];

        GameObject owner;
        int team;
        Vector3 direction;
        float remaining;
        bool deals;
        bool fire;
        float groundY;
        Action<int> onHits;
        float damageScale = 1f, radiusScale = 1f, panicSeconds;
        float BlastSize => BlastRadius * radiusScale;
        Material coreMaterial, glowMaterial;

        // `fire`: the Purifier Order. The blast ignites what it hits and leaves burning ground.
        public static void Fire(GameObject owner, int team, Vector3 origin, Vector3 direction, bool deals, bool fire, Action<int> onHits, float damageScale = 1f, float radiusScale = 1f, float panicSeconds = 0f)
        {
            var go = new GameObject("Psychic Shell");
            go.transform.position = origin;
            var shell = go.AddComponent<PsychicShell>();
            shell.owner = owner;
            shell.team = team;
            shell.direction = direction.normalized;
            shell.remaining = Range;
            shell.deals = deals;
            shell.fire = fire;
            shell.groundY = owner != null ? owner.transform.position.y : 0f;
            shell.onHits = onHits;
            shell.damageScale = damageScale;
            shell.radiusScale = radiusScale;
            shell.panicSeconds = panicSeconds;
            shell.Build();
        }

        void Build()
        {
            Color baseColor = fire ? new Color(1f, 0.5f, 0.12f) : new Color(0.4f, 0.75f, 1f);
            coreMaterial = VisualUtil.UnlitMaterial(Color.Lerp(baseColor, Color.white, 0.8f), 3003);
            glowMaterial = VisualUtil.UnlitMaterial(new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f), 3002);

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            Destroy(core.GetComponent<Collider>());
            core.GetComponent<Renderer>().sharedMaterial = coreMaterial;
            core.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * (0.36f * Mathf.Sqrt(radiusScale));

            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Glow";
            Destroy(glow.GetComponent<Collider>());
            glow.GetComponent<Renderer>().sharedMaterial = glowMaterial;
            glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.transform.SetParent(transform, false);
            glow.transform.localScale = Vector3.one * (0.85f * Mathf.Sqrt(radiusScale));

            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.4f;
            trail.endWidth = 0f;
            trail.sharedMaterial = glowMaterial;
            trail.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f);
            trail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            if (coreMaterial != null) Destroy(coreMaterial);
            if (glowMaterial != null) Destroy(glowMaterial);
        }

        void Update()
        {
            float step = Mathf.Min(Speed * Time.deltaTime, remaining);
            if (TryFindHit(step, out Vector3 point))
            {
                Explode(point);
                return;
            }

            // The orb pulses as it flies.
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 30f);
            transform.GetChild(1).localScale = Vector3.one * (0.85f * Mathf.Sqrt(radiusScale) * pulse);

            transform.position += direction * step;
            remaining -= step;
            if (remaining <= 0f) Explode(transform.position);
        }

        // The first enemy or solid thing along this frame's step. Allies (the shooter included) and the dead are skipped.
        bool TryFindHit(float distance, out Vector3 point)
        {
            point = default;
            int count = Physics.SphereCastNonAlloc(transform.position, HitRadius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                Health hp = h.collider.GetComponentInParent<Health>();
                if (hp != null && (hp.Team == team || hp.IsDead)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    // A hit from inside a collider reports no point; the shell's own position is then the contact.
                    point = h.distance > 0f ? transform.position + direction * h.distance : transform.position;
                    found = true;
                }
            }
            return found;
        }

        void Explode(Vector3 point)
        {
            int hits = 0;
            if (deals)
            {
                var struck = new HashSet<Health>();
                foreach (Collider c in Physics.OverlapSphere(point, BlastSize + 1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Health target = c.GetComponentInParent<Health>();
                    if (target == null || target.Team == team || target.IsDead || struck.Contains(target)) continue;

                    float distance = (c.ClosestPoint(point) - point).magnitude;
                    if (distance > BlastSize) continue;

                    struck.Add(target);
                    float falloff = Mathf.Lerp(1f, MinFalloff, distance / BlastSize);
                    if (!target.TakeDamage(Damage * damageScale * falloff, owner, true)) continue;
                    hits++;
                    GreyKnightUpgrade.Ignite(owner, target);
                    if (panicSeconds > 0f && !target.IsDead) Panicked.Apply(target.gameObject, owner, panicSeconds);
                }
                onHits?.Invoke(hits);
            }

            BlastFx.Spawn(point, BlastSize, fire);
            GreyKnightSound.ConsecrateBoom();
            if (hits > 0) GreyKnightSound.PsychicImpact(hits);
            if (fire)
                FirePool.Spawn(owner, team, new Vector3(point.x, groundY, point.z), GreyKnightUpgrade.GroundFireRadius * 0.8f,
                    GreyKnightUpgrade.GroundFireSeconds, GreyKnightUpgrade.GroundFirePerSecond, deals);

            Destroy(gameObject);
        }
    }

    // A quick expanding burst: a translucent sphere, a flat ring on the ground and a flash of light.
    public class BlastFx : MonoBehaviour
    {
        const int RingPoints = 40;
        const float Duration = 0.4f;

        float radius;
        float startTime;
        Transform sphere;
        LineRenderer ring;
        Light flash;
        Material sphereMaterial, ringMaterial;
        Color color;
        Vector3 centre;
        bool flat;

        // `flat`: a ground shockwave (a ring only) rather than a burst in the air.
        public static void Spawn(Vector3 position, float radius, bool fire, bool flat = false)
        {
            var go = new GameObject("Blast");
            go.transform.position = position;
            var fx = go.AddComponent<BlastFx>();
            fx.radius = radius;
            fx.startTime = Time.time;
            fx.centre = position;
            fx.flat = flat;
            fx.color = fire ? new Color(1f, 0.5f, 0.12f) : new Color(0.45f, 0.78f, 1f);
            fx.Build();
        }

        void Build()
        {
            ringMaterial = VisualUtil.UnlitMaterial(color, 3003);
            var ringObject = new GameObject("Ring");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = RingPoints;
            ring.numCapVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.sharedMaterial = ringMaterial;

            if (!flat)
            {
                sphereMaterial = VisualUtil.UnlitMaterial(new Color(color.r, color.g, color.b, 0.45f), 3002);
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Burst";
                Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().sharedMaterial = sphereMaterial;
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.transform.SetParent(transform, false);
                sphere = s.transform;
            }

            var lightObject = new GameObject("Flash");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 0.8f;
            flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = color;
            flash.range = radius * 3f;
            flash.shadows = LightShadows.None;
            Update();
        }

        void OnDestroy()
        {
            if (sphereMaterial != null) Destroy(sphereMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
        }

        void Update()
        {
            float t = (Time.time - startTime) / Duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float ease = 1f - (1f - t) * (1f - t);
            float r = radius * Mathf.Lerp(0.25f, 1f, ease);
            const float y = 0.1f; // just above the floor
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i / (float)RingPoints * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(centre.x + Mathf.Cos(a) * r, y, centre.z + Mathf.Sin(a) * r));
            }
            ring.startWidth = ring.endWidth = 0.22f * (1f - t);

            Color c = color;
            c.a = 1f - t;
            ringMaterial.color = c;

            if (sphere != null)
            {
                sphere.localScale = Vector3.one * (r * 2f);
                Color sc = color;
                sc.a = 0.45f * (1f - t) * (1f - t);
                sphereMaterial.color = sc;
            }
            flash.intensity = 3f * (1f - t);
        }

    }
}
