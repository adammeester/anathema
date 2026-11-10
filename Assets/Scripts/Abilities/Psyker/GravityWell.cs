using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // A singularity of warp gravity. For its short life it drags every enemy of the owner inside its radius toward the
    // centre. Every machine builds its own copy for the visuals; only the owner's copy applies the pull (Knockback.Apply
    // forwards it to a remote victim).
    public class GravityWell : MonoBehaviour
    {
        const float PullInterval = 0.2f;

        static readonly Color Violet = new Color(0.65f, 0.35f, 1f, 0.9f);

        GameObject owner;
        int team;
        Vector3 center;
        float radius;
        float pullSpeed;
        float detonationDamage;
        float startTime;
        float endTime;
        bool pulls;
        float nextPull;
        float nextRing;
        Transform core;
        Material coreMaterial;
        Material discMaterial;
        Transform disc;

        public static GravityWell Spawn(GameObject owner, int team, Vector3 center, float radius, float duration, float pullSpeed, bool pulls, float detonationDamage = 0f)
        {
            var go = new GameObject("Gravity Well");
            go.transform.position = center + Vector3.up * 0.05f;
            var well = go.AddComponent<GravityWell>();
            well.owner = owner;
            well.team = team;
            well.center = center;
            well.radius = radius;
            well.pullSpeed = pullSpeed;
            well.startTime = Time.time;
            well.endTime = Time.time + duration;
            well.pulls = pulls;
            well.detonationDamage = detonationDamage;
            well.nextPull = Time.time;
            well.Build();
            return well;
        }

        void Build()
        {
            discMaterial = VisualUtil.UnlitMaterial(new Color(0.3f, 0.1f, 0.5f, 0.25f), 3000);
            coreMaterial = VisualUtil.UnlitMaterial(new Color(0.08f, 0.02f, 0.15f, 0.95f), 3002);

            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            d.name = "Well Disc";
            Destroy(d.GetComponent<Collider>());
            d.GetComponent<Renderer>().sharedMaterial = discMaterial;
            d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            d.transform.SetParent(transform, false);
            d.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            disc = d.transform;

            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Well Core";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = coreMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(transform, false);
            s.transform.localPosition = Vector3.up * 0.9f;
            core = s.transform;
        }

        void OnDestroy()
        {
            if (coreMaterial != null) Destroy(coreMaterial);
            if (discMaterial != null) Destroy(discMaterial);
        }

        void Update()
        {
            if (Time.time >= endTime)
            {
                Detonate();
                Destroy(gameObject);
                return;
            }

            float grow = Mathf.Clamp01((Time.time - startTime) / 0.15f);
            float fade = Mathf.Clamp01((endTime - Time.time) / 0.3f);
            core.localScale = Vector3.one * (0.9f + 0.15f * Mathf.Sin(Time.time * 14f)) * grow * fade;
            disc.localScale = new Vector3(radius * 2f * grow, 0.01f, radius * 2f * grow);

            // Rings collapse inward so the pull reads clearly.
            if (Time.time >= nextRing)
            {
                nextRing = Time.time + 0.25f;
                PsykerFx.Ring(center, radius, 0.4f, 0.45f, Violet, 0.12f);
            }

            if (pulls && Time.time >= nextPull)
            {
                nextPull = Time.time + PullInterval;
                Pull();
            }
        }

        // The well collapses in a burst: a flash everywhere, and (owner only) damage to every enemy still near the centre.
        void Detonate()
        {
            float blast = radius * 0.7f;
            PsykerFx.Ring(center, 0.3f, blast, 0.3f, Color.white, 0.35f);
            PsykerFx.Ring(center, 0.3f, blast * 1.2f, 0.4f, Violet, 0.2f);

            // Every machine marks whoever is caught in the blast as Charged (so everyone sees the rune), but only the
            // owner's copy deals damage. The detonation itself never gets the Charged bonus: it is applied as a plain hit.
            bool hitAny = false;
            foreach (Collider col in Physics.OverlapSphere(center + Vector3.up, blast + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == team || target.IsDead) continue;

                Vector3 to = col.ClosestPoint(center) - center;
                to.y = 0f;
                if (to.magnitude > blast) continue;

                if (pulls && detonationDamage > 0f && target.TakeDamage(detonationDamage, owner, true)) hitAny = true;
                if (!target.IsDead) Charged.Apply(target.gameObject, Charged.DefaultDuration);
            }

            if (hitAny) HitMarkerSound.Play();
        }

        void Pull()
        {
            foreach (Collider col in Physics.OverlapSphere(center + Vector3.up, radius + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == team || target.IsDead) continue;

                Vector3 to = center - target.transform.position;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > radius || distance < 0.6f) continue;

                float step = Mathf.Min(distance - 0.4f, pullSpeed * PullInterval);
                Knockback.Apply(target.gameObject, to, step, PullInterval);
            }
        }
    }
}
