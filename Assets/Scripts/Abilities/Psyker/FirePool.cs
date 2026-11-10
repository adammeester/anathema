using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // A pool of burning warp-fire on the ground. Enemies of the owner standing in it take damage over time until it
    // burns out. Every machine builds its own copy from the cast cue (so the flames always show), but only the owner's
    // copy deals damage, like every other ability.
    public class FirePool : MonoBehaviour
    {
        const float TickInterval = 0.25f;
        const float FadeTime = 0.7f;
        const int Flames = 16;

        GameObject owner;
        int team;
        float radius;
        float endTime;
        float startTime;
        float damagePerTick;
        bool dealsDamage;
        float nextTick;

        Material baseMaterial, coreMaterial, flameMaterial, tipMaterial;
        Transform[] flames;
        float[] phase;
        float[] size;
        Light glow;

        public static FirePool Spawn(GameObject owner, int team, Vector3 center, float radius, float duration, float damagePerSecond, bool dealsDamage)
        {
            var go = new GameObject("Fire Pool");
            go.transform.position = center + Vector3.up * 0.05f;
            var pool = go.AddComponent<FirePool>();
            pool.owner = owner;
            pool.team = team;
            pool.radius = radius;
            pool.startTime = Time.time;
            pool.endTime = Time.time + duration;
            pool.damagePerTick = damagePerSecond * TickInterval;
            pool.dealsDamage = dealsDamage;
            pool.nextTick = Time.time + TickInterval;
            pool.Build();
            return pool;
        }

        void Build()
        {
            baseMaterial = VisualUtil.UnlitMaterial(new Color(0.9f, 0.25f, 0.05f, 0.45f), 3000);
            coreMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.6f, 0.1f, 0.5f), 3001);
            flameMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.45f, 0.08f, 0.8f), 3002);
            tipMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.9f, 0.4f, 0.9f), 3003);

            Disc("Scorch", radius * 2f, baseMaterial);
            Disc("Embers", radius * 1.3f, coreMaterial, 0.01f);

            flames = new Transform[Flames];
            phase = new float[Flames];
            size = new float[Flames];
            for (int i = 0; i < Flames; i++)
            {
                Vector2 p = Random.insideUnitCircle * (radius * 0.9f);
                GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "Flame";
                Destroy(f.GetComponent<Collider>());
                var r = f.GetComponent<Renderer>();
                r.sharedMaterial = i % 3 == 0 ? tipMaterial : flameMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.transform.SetParent(transform, false);
                f.transform.localPosition = new Vector3(p.x, 0f, p.y);
                flames[i] = f.transform;
                phase[i] = Random.value * 10f;
                size[i] = Random.Range(0.35f, 0.65f) * Mathf.Clamp(radius * 0.5f, 0.6f, 1.2f);
            }

            var lightObject = new GameObject("Fire Glow");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 0.8f;
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.15f);
            glow.range = radius * 3f;
            glow.intensity = 1.6f;
            glow.shadows = LightShadows.None;
        }

        void Disc(string name, float diameter, Material material, float lift = 0f)
        {
            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            d.name = name;
            Destroy(d.GetComponent<Collider>());
            var r = d.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            d.transform.SetParent(transform, false);
            d.transform.localPosition = Vector3.up * lift;
            d.transform.localScale = new Vector3(diameter, 0.01f, diameter);
        }

        void OnDestroy()
        {
            foreach (Material m in new[] { baseMaterial, coreMaterial, flameMaterial, tipMaterial })
                if (m != null) Destroy(m);
        }

        void Update()
        {
            if (Time.time >= endTime)
            {
                Destroy(gameObject);
                return;
            }

            float fade = Mathf.Clamp01((endTime - Time.time) / FadeTime);
            float grow = Mathf.Clamp01((Time.time - startTime) / 0.2f);

            for (int i = 0; i < flames.Length; i++)
            {
                // Each flame licks up and down at its own rate, narrower as it rises.
                float t = Time.time * 7f + phase[i];
                float h = size[i] * (0.6f + 0.6f * Mathf.Abs(Mathf.Sin(t)) + 0.2f * Mathf.Sin(t * 2.3f)) * fade * grow;
                flames[i].localScale = new Vector3(size[i] * 0.6f * fade, Mathf.Max(0.01f, h * 1.8f), size[i] * 0.6f * fade);
                flames[i].localPosition = new Vector3(flames[i].localPosition.x, h * 0.9f, flames[i].localPosition.z);
            }

            SetAlpha(baseMaterial, 0.45f * fade);
            SetAlpha(coreMaterial, (0.35f + 0.15f * Mathf.Sin(Time.time * 9f)) * fade);
            glow.intensity = 1.6f * fade * (0.85f + 0.15f * Mathf.Sin(Time.time * 13f));

            if (dealsDamage && Time.time >= nextTick)
            {
                nextTick += TickInterval;
                Burn();
            }
        }

        static void SetAlpha(Material m, float a)
        {
            Color c = m.color;
            c.a = a;
            m.color = c;
        }

        void Burn()
        {
            Vector3 center = transform.position;
            bool marked = false;
            foreach (Collider col in Physics.OverlapSphere(center + Vector3.up, radius + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == team || target.IsDead) continue;

                Vector3 to = col.ClosestPoint(center) - center;
                to.y = 0f;
                if (to.magnitude > radius) continue;

                target.Strike(Hit.Direct(damagePerTick).AsAoe(), owner, landed =>
                {
                    if (!landed || marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
        }
    }
}
