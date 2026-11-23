using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Shared pieces of the Sanguinary Guard's kit: colours, ally / enemy searches, the one way his abilities heal
    // (so Vitae sees every heal), and the blood stream drawn from him to whoever he heals.
    public static class SanguinaryKit
    {
        public static readonly Color Blood = new Color(0.85f, 0.05f, 0.08f, 0.9f);
        public static readonly Color BrightBlood = new Color(1f, 0.25f, 0.2f, 0.95f);
        public static readonly Color DarkBlood = new Color(0.45f, 0.02f, 0.04f, 0.85f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.35f, 0.9f);

        static readonly List<Health> buffer = new List<Health>();

        // Living teammates (himself included when `includeSelf`) within `radius` of `centre`. The list is reused.
        public static List<Health> AlliesNear(Health self, Vector3 centre, float radius, bool includeSelf = true)
        {
            buffer.Clear();
            if (self == null) return buffer;
            foreach (Health h in Object.FindObjectsByType<Health>())
            {
                if (h == null || h.IsDead || h.Structure || h.Team != self.Team) continue;
                if (h == self && !includeSelf) continue;
                if (Flat(h.transform.position - centre).sqrMagnitude > radius * radius) continue;
                buffer.Add(h);
            }
            return buffer;
        }

        // How many living enemies stand within `radius` of `centre`.
        public static int EnemiesNear(Health self, Vector3 centre, float radius)
        {
            if (self == null) return 0;
            int count = 0;
            foreach (Health h in Object.FindObjectsByType<Health>())
            {
                if (h == null || h.IsDead || h.Structure || h.Team == self.Team) continue;
                if (Flat(h.transform.position - centre).sqrMagnitude <= radius * radius) count++;
            }
            return count;
        }

        // The teammate (himself included) missing the most healable health within `radius`; null if nobody is hurt.
        public static Health MostInjuredAlly(Health self, Vector3 centre, float radius)
        {
            Health best = null;
            float most = 0.5f;
            foreach (Health h in AlliesNear(self, centre, radius))
            {
                if (h.Recoverable <= most) continue;
                most = h.Recoverable;
                best = h;
            }
            return best;
        }

        // The teammate (himself included) nearest `point` within `radius` of it.
        public static Health AllyNearest(Health self, Vector3 point, float radius)
        {
            Health best = null;
            float bestSq = radius * radius;
            foreach (Health h in AlliesNear(self, point, radius))
            {
                float sq = Flat(h.transform.position - point).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = h;
            }
            return best;
        }

        // Every heal the guard gives goes through here: Vitae counts it (Healing.Applying), unless `feedsVitae` is
        // false (the Exsanguinating Arc's own healing never refills the chalice). Owner only.
        public static void Heal(GameObject guard, Health target, float amount, bool feedsVitae = true)
        {
            if (guard == null || target == null || target.IsDead || amount <= 0f) return;
            if (feedsVitae || !guard.TryGetComponent(out Vitae vitae))
            {
                Healing.Apply(target, amount, guard);
                return;
            }
            vitae.Suppressed++;
            try { Healing.Apply(target, amount, guard); }
            finally { vitae.Suppressed--; }
        }

        // He never pays health he doesn't have: an ability that costs `cost` can only be cast while it leaves him at or
        // above this share of his max health.
        public const float SacrificeFloor = 0.15f;

        public static bool CanPay(Health guard, float cost) =>
            guard != null && !guard.IsDead && guard.Current - cost >= guard.Max * SacrificeFloor;

        // Owner: spend `cost` of his own health (Health.Sacrifice; the server's copy applies it online). What he pays
        // fills Vitae.
        public static void Pay(Health guard, float cost)
        {
            if (guard == null || cost <= 0f) return;
            float paid = Mathf.Min(cost, guard.Current - 1f);
            guard.Sacrifice(cost);
            if (paid > 0f && guard.TryGetComponent(out Vitae vitae)) vitae.OnSacrifice(paid);
            Mist(guard.transform.position, 0.6f, 0.35f);
        }

        // Owner, from a landed hit's callback: under Red Thirst the damage he deals fills Vitae (see RedThirst). Only
        // Power Sword cuts and Crimson Rush call this (not the Arc, the slam, the riposte or Chalice of Wrath).
        public static void FeedThirst(GameObject guard, float damage)
        {
            if (guard != null && guard.TryGetComponent(out RedThirst thirst)) thirst.OnDamageDealt(damage);
        }

        // The teammate (never himself) nearest `point` within `radius` of it.
        public static Health OtherAllyNearest(Health self, Vector3 point, float radius)
        {
            Health best = null;
            float bestSq = radius * radius;
            foreach (Health h in AlliesNear(self, point, radius, includeSelf: false))
            {
                float sq = Flat(h.transform.position - point).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = h;
            }
            return best;
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        public static Vector3 FlatAim(Vector3 aim, Transform fallback)
        {
            aim.y = 0f;
            return aim.sqrMagnitude < 0.0001f ? fallback.forward : aim.normalized;
        }

        // ---- Visuals --------------------------------------------------------------------------------------------------

        // A ribbon of blood droplets arcing from the guard to the ally he heals. Every machine plays it.
        public static void BloodStream(Vector3 from, Transform to)
        {
            if (to == null) return;
            var go = new GameObject("Blood Stream");
            go.AddComponent<StreamFx>().Init(from + Vector3.up * 1.2f, to);
        }

        // A puff of blood mist that swells and fades.
        public static void Mist(Vector3 centre, float radius, float duration = 0.5f)
        {
            var go = new GameObject("Blood Mist");
            go.transform.position = centre;
            go.AddComponent<MistFx>().Init(radius, duration);
        }

        sealed class StreamFx : MonoBehaviour
        {
            const float Flight = 0.3f;
            const int Drops = 6;
            const float Stagger = 0.035f;

            Vector3 from;
            Transform to;
            Vector3 lastTo;
            float start;
            Transform[] drops;
            Material material;

            public void Init(Vector3 origin, Transform target)
            {
                from = origin;
                to = target;
                lastTo = target.position + Vector3.up;
                start = Time.time;
                material = VisualUtil.UnlitMaterial(BrightBlood, 3003);
                drops = new Transform[Drops];
                for (int i = 0; i < Drops; i++)
                {
                    GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(g.GetComponent<Collider>());
                    var r = g.GetComponent<Renderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    g.transform.SetParent(transform, false);
                    g.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.1f, i / (float)(Drops - 1));
                    drops[i] = g.transform;
                }
                Update();
            }

            void Update()
            {
                if (to != null) lastTo = to.position + Vector3.up * 1.1f;
                float age = Time.time - start;
                if (age > Flight + Stagger * Drops)
                {
                    Destroy(gameObject);
                    return;
                }
                float height = Mathf.Min(1.6f, Vector3.Distance(from, lastTo) * 0.25f);
                for (int i = 0; i < Drops; i++)
                {
                    float t = Mathf.Clamp01((age - i * Stagger) / Flight);
                    drops[i].gameObject.SetActive(t > 0f && t < 1f);
                    Vector3 p = Vector3.Lerp(from, lastTo, t);
                    p.y += height * 4f * t * (1f - t);
                    drops[i].position = p;
                }
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }

        sealed class MistFx : MonoBehaviour
        {
            Transform sphere;
            Material material;
            float radius, duration, start;

            public void Init(float r, float seconds)
            {
                radius = r;
                duration = Mathf.Max(0.05f, seconds);
                start = Time.time;
                material = VisualUtil.UnlitMaterial(DarkBlood, 3002);
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                var rend = s.GetComponent<Renderer>();
                rend.sharedMaterial = material;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.transform.SetParent(transform, false);
                sphere = s.transform;
                Update();
            }

            void Update()
            {
                float t = (Time.time - start) / duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                float size = radius * 2f * Mathf.Lerp(0.4f, 1f, 1f - (1f - t) * (1f - t));
                sphere.localScale = new Vector3(size, size * 0.35f, size);
                sphere.localPosition = Vector3.up * 0.6f;
                Color c = DarkBlood;
                c.a = DarkBlood.a * 0.55f * (1f - t);
                material.color = c;
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }
    }
}
