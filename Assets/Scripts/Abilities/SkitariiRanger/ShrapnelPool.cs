using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // A field of whirling shrapnel on the ground. Enemies of the owner standing in it take damage over time and are
    // slowed while they stay (the slow lingers a moment after they leave). Every machine builds its own copy from the
    // cast cue so the field always shows; only the owner's copy deals damage and slows.
    public class ShrapnelPool : MonoBehaviour
    {
        const float TickInterval = 0.25f;
        const float FadeTime = 0.6f;
        const float SlowLinger = 0.5f;
        const int Shards = 40;
        const int RingPoints = 48;
        const float FallHeight = 9f;
        static readonly Vector3 ShardScale = new Vector3(0.07f, 0.55f, 0.07f);

        GameObject owner;
        int team;
        float radius;
        float startTime, endTime;
        float damagePerTick;
        float slow;
        bool dealsDamage;
        float nextTick;

        Material edgeMaterial, shardMaterial, glintMaterial;
        Transform[] shards;
        float[] angle, dist, speed, height;
        LineRenderer outline;

        public static ShrapnelPool Spawn(GameObject owner, int team, Vector3 center, float radius, float duration, float damagePerSecond, float slow, bool dealsDamage)
        {
            var go = new GameObject("Shrapnel Field");
            go.transform.position = center + Vector3.up * 0.05f;
            var pool = go.AddComponent<ShrapnelPool>();
            pool.owner = owner;
            pool.team = team;
            pool.radius = radius;
            pool.startTime = Time.time;
            pool.endTime = Time.time + duration;
            pool.damagePerTick = damagePerSecond * TickInterval;
            pool.slow = slow;
            pool.dealsDamage = dealsDamage;
            pool.nextTick = Time.time + TickInterval;
            pool.Build();
            return pool;
        }

        void Build()
        {
            edgeMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.6f, 0.2f, 0.45f), 3001);
            shardMaterial = VisualUtil.UnlitMaterial(new Color(0.6f, 0.62f, 0.66f, 0.95f), 3002);
            glintMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.85f, 0.5f, 0.95f), 3003);

            // Outline only: a glowing ring on the ground marks the field's edge.
            var ringObject = new GameObject("Edge");
            ringObject.transform.SetParent(transform, false);
            outline = ringObject.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = RingPoints;
            outline.numCapVertices = 2;
            outline.startWidth = outline.endWidth = 0.12f;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.sharedMaterial = edgeMaterial;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i / (float)RingPoints * Mathf.PI * 2f;
                outline.SetPosition(i, new Vector3(Mathf.Cos(a), 0.02f, Mathf.Sin(a)) * radius);
            }

            shards = new Transform[Shards];
            angle = new float[Shards];
            dist = new float[Shards];
            speed = new float[Shards];
            height = new float[Shards];
            for (int i = 0; i < Shards; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = "Shard";
                Destroy(s.GetComponent<Collider>());
                var r = s.GetComponent<Renderer>();
                r.sharedMaterial = i % 4 == 0 ? glintMaterial : shardMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.transform.SetParent(transform, false);
                s.transform.localScale = ShardScale;
                shards[i] = s.transform;
                Respawn(i, Random.Range(0f, FallHeight)); // staggered so the rain is already falling
            }
        }

        // Puts shard i at a random spot over the field at height y, with a fresh fall speed.
        void Respawn(int i, float y)
        {
            float a = Random.value * Mathf.PI * 2f;
            float d = Mathf.Sqrt(Random.value) * radius * 0.95f;
            angle[i] = a;
            dist[i] = d;
            height[i] = y;
            speed[i] = Random.Range(12f, 20f);
        }

        void OnDestroy()
        {
            foreach (Material m in new[] { edgeMaterial, shardMaterial, glintMaterial })
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
            float grow = Mathf.Clamp01((Time.time - startTime) / 0.25f);

            // Shards rain straight down over the field; on landing they go back up to fall again (until the fade).
            for (int i = 0; i < shards.Length; i++)
            {
                height[i] -= speed[i] * Time.deltaTime;
                if (height[i] <= 0f) Respawn(i, FallHeight + Random.Range(0f, 3f));
                float d = dist[i] * grow;
                shards[i].localPosition = new Vector3(Mathf.Cos(angle[i]) * d, Mathf.Max(0f, height[i]), Mathf.Sin(angle[i]) * d);
                shards[i].localRotation = Quaternion.identity;
                shards[i].localScale = ShardScale * fade;
            }

            SetAlpha(edgeMaterial, (0.7f + 0.2f * Mathf.Sin(Time.time * 8f)) * fade);

            if (dealsDamage && Time.time >= nextTick)
            {
                nextTick += TickInterval;
                Shred();
            }
        }

        static void SetAlpha(Material m, float a)
        {
            Color c = m.color;
            c.a = a;
            m.color = c;
        }

        void Shred()
        {
            Vector3 center = transform.position;
            bool hitAny = false;
            foreach (Collider col in Physics.OverlapSphere(center + Vector3.up, radius + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == team || target.IsDead) continue;

                Vector3 to = col.ClosestPoint(center) - center;
                to.y = 0f;
                if (to.magnitude > radius) continue;

                if (target.TakeDamage(damagePerTick, owner, true)) hitAny = true;
                if (!target.IsDead) Slowed.Apply(target.gameObject, TickInterval + SlowLinger, slow);
            }

            if (hitAny) HitMarkerSound.Play();
        }
    }
}
