using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Combat
{
    // A dome of psychic force. Enemy projectiles that enter it (including ultimates built on Projectile) are turned
    // around and sent back to where they came from, now belonging to the dome's owner. Enemies standing inside it are
    // slowed (PlayerMovement and DummyMover read SlowMultiplier). Every machine builds its own copy from the cast cue,
    // so reflection and slow are computed locally and deterministically from the same centre and radius.
    public class ReflectDome : MonoBehaviour
    {
        const float FadeTime = 0.4f;

        public static readonly List<ReflectDome> All = new List<ReflectDome>();

        GameObject ownerObject;
        Health ownerHealth;
        Vector3 center;
        float radius;
        float slowFraction;
        float endTime;
        Material shellMaterial;
        Material ringMaterial;
        Color shellColor;
        Color ringColor;

        public int Team => ownerHealth != null ? ownerHealth.Team : -1;
        public GameObject Owner => ownerObject;

        public static ReflectDome Spawn(GameObject owner, Vector3 position, float radius, float duration, float slowFraction, Color color)
        {
            var go = new GameObject("Reflect Dome");
            go.transform.position = position;
            var dome = go.AddComponent<ReflectDome>();
            dome.ownerObject = owner;
            dome.ownerHealth = owner.GetComponent<Health>();
            dome.center = position;
            dome.radius = radius;
            dome.slowFraction = slowFraction;
            dome.endTime = Time.time + duration;
            dome.shellColor = new Color(color.r, color.g, color.b, 0.16f);
            dome.ringColor = new Color(color.r, color.g, color.b, 0.55f);
            dome.Build();
            return dome;
        }

        // Movement multiplier for anything standing inside an enemy dome (1 = unaffected).
        public static float SlowMultiplier(GameObject target)
        {
            if (All.Count == 0) return 1f;
            var health = target.GetComponent<Health>();
            if (health == null) return 1f;

            float multiplier = 1f;
            foreach (ReflectDome dome in All)
            {
                if (dome.Team == health.Team || !dome.Contains(target.transform.position)) continue;
                multiplier = Mathf.Min(multiplier, 1f - dome.slowFraction);
            }
            return multiplier;
        }

        public bool Contains(Vector3 point)
        {
            return (point - center).sqrMagnitude <= radius * radius;
        }

        void Build()
        {
            shellMaterial = VisualUtil.UnlitMaterial(shellColor);
            ringMaterial = VisualUtil.UnlitMaterial(ringColor);

            // Hemisphere look: a full sphere centred on the floor, the lower half hidden underground.
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Dome Shell";
            Destroy(shell.GetComponent<Collider>());
            shell.transform.SetParent(transform, false);
            shell.transform.localScale = Vector3.one * (radius * 2f);
            var shellRenderer = shell.GetComponent<Renderer>();
            shellRenderer.sharedMaterial = shellMaterial;
            shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Dome Floor";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ring.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var ringRenderer = ring.GetComponent<Renderer>();
            ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnEnable()
        {
            All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        void OnDestroy()
        {
            if (shellMaterial != null) Destroy(shellMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
        }

        void Update()
        {
            float remaining = endTime - Time.time;
            if (remaining <= 0f || (ownerHealth != null && ownerHealth.IsDead))
            {
                Destroy(gameObject);
                return;
            }

            float fade = Mathf.Clamp01(remaining / FadeTime);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
            Color s = shellColor;
            s.a *= fade * pulse;
            shellMaterial.color = s;
            Color r = ringColor;
            r.a *= fade;
            ringMaterial.color = r;
        }
    }
}
