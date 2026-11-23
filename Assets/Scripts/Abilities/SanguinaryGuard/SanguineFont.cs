using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Sanguine Font (Encarmine Axe's RMB): a pool of the guard's blood on the ground. Teammates standing in it (never
    // the guard himself) heal GuardUpgrade.FontHealPerSecond, in half-second ticks, for as long as it lasts. With
    // Wellspring, a teammate who steps out keeps healing for `linger` seconds (and so does everyone inside when it dries).
    // Every machine draws it; only the guard's own machine heals (SanguinaryKit.Heal, which fills Vitae like any heal).
    public class SanguineFont : MonoBehaviour
    {
        const float Tick = 0.5f;
        const float Height = 0.17f;   // above the arena's visible floor discs
        static readonly Color PoolColor = new Color(0.5f, 0.02f, 0.04f, 0.7f);
        static readonly Color SheenColor = new Color(1f, 0.2f, 0.15f, 0.35f);

        Health guard;
        bool heals;
        float radius, ends, linger, nextTick, born;
        readonly Dictionary<Health, float> healUntil = new Dictionary<Health, float>();
        Transform pool, sheen;
        Material poolMaterial, sheenMaterial, moteMaterial;
        float moteDebt;

        public static void Pour(Vector3 at, Health guard, bool heals, float seconds, float linger)
        {
            var go = new GameObject("Sanguine Font");
            go.transform.position = new Vector3(at.x, at.y + Height, at.z);
            var font = go.AddComponent<SanguineFont>();
            font.guard = guard;
            font.heals = heals;
            font.radius = GuardUpgrade.FontRadius;
            font.born = Time.time;
            font.ends = Time.time + seconds;
            font.linger = linger;
            font.nextTick = Time.time + Tick;
            font.Build();
        }

        void Build()
        {
            poolMaterial = VisualUtil.UnlitMaterial(PoolColor, 2990);
            sheenMaterial = VisualUtil.UnlitMaterial(SheenColor, 2991);
            moteMaterial = VisualUtil.UnlitMaterial(SanguinaryKit.BrightBlood, 2992);
            pool = Disc(poolMaterial, radius * 2f, 0.005f);
            sheen = Disc(sheenMaterial, radius * 1.3f, 0.007f);
            Psyker.PsykerFx.Ring(transform.position, 0.3f, radius, 0.3f, SanguinaryKit.Blood, 0.2f);
            Sfx.Play(Sfx.MagicImpact, 0.3f, 0.6f, 0.7f, 0.5f);
        }

        Transform Disc(Material material, float size, float thickness)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size, thickness, size);
            return go.transform;
        }

        void Update()
        {
            float now = Time.time;
            bool flowing = now < ends && guard != null;

            // The pool spreads out in a moment, ripples while it flows, and shrinks away when it dries.
            float open = Mathf.Clamp01((now - born) / 0.2f);
            float close = Mathf.Clamp01((ends - now) / 0.4f);
            float size = open * close;
            pool.localScale = new Vector3(radius * 2f * size, 0.005f, radius * 2f * size);
            float ripple = 0.85f + 0.15f * Mathf.Sin(now * 6f);
            sheen.localScale = new Vector3(radius * 1.3f * size * ripple, 0.007f, radius * 1.3f * size * ripple);

            if (flowing)
            {
                moteDebt += 8f * Time.deltaTime;
                while (moteDebt >= 1f)
                {
                    moteDebt -= 1f;
                    Vector2 p = Random.insideUnitCircle * radius * 0.9f;
                    Mote.Spawn(transform.position + new Vector3(p.x, 0.05f, p.y), moteMaterial);
                }
            }

            if (heals && now >= nextTick)
            {
                nextTick += Tick;
                if (flowing && !guard.IsDead)
                    foreach (Health ally in SanguinaryKit.AlliesNear(guard, transform.position, radius, includeSelf: false).ToArray())
                        healUntil[ally] = now + linger;

                var done = new List<Health>();
                foreach (var pair in healUntil)
                {
                    if (pair.Key == null || pair.Key.IsDead || now > pair.Value + 0.01f) { done.Add(pair.Key); continue; }
                    if (guard != null) SanguinaryKit.Heal(guard.gameObject, pair.Key, GuardUpgrade.FontHealPerSecond * Tick);
                }
                foreach (Health h in done) healUntil.Remove(h);
            }

            // Gone once dry and nobody is still lingering on its blood.
            if (!flowing && (healUntil.Count == 0 || !heals) && now > ends + 0.4f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (poolMaterial != null) Destroy(poolMaterial);
            if (sheenMaterial != null) Destroy(sheenMaterial);
            if (moteMaterial != null) Destroy(moteMaterial);
        }

        // A drop of blood rising off the pool.
        sealed class Mote : MonoBehaviour
        {
            float born, life;

            public static void Spawn(Vector3 at, Material material)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = at;
                go.transform.localScale = Vector3.one * Random.Range(0.06f, 0.11f);
                var m = go.AddComponent<Mote>();
                m.born = Time.time;
                m.life = Random.Range(0.5f, 0.9f);
            }

            void Update()
            {
                float t = (Time.time - born) / life;
                if (t >= 1f) { Destroy(gameObject); return; }
                transform.position += Vector3.up * (1.1f * Time.deltaTime);
                transform.localScale *= 1f - 0.9f * Time.deltaTime;
            }
        }
    }
}
