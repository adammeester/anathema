using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the Thousand Sons Sorcerer's Curse of the Rubricae, an echo of the Rubric of Ahriman: a time bomb that crumbles its victim toward dust. After `seconds` it bursts for `damage`,
    // credited to whoever laid it. Some of the Sorcerer's abilities set it off early (Detonate): the burst then lands at
    // once. Re-applying refreshes the timer and keeps the larger burst; it does not stack.
    // Like Balefire it lives on the server's copy of the victim (it deals the damage there); everyone else draws a
    // visual-only copy from the server's published state (NetPlayer.SyncServerFx), and every machine plays the burst
    // (NetPlayer.ServerRubricBurst).
    public class RubricMark : MonoBehaviour
    {
        const int Grains = 8;
        const float RingRadius = 0.55f;

        public static readonly Color Sand = new Color(0.95f, 0.78f, 0.38f, 0.9f);
        static readonly Color RingColor = new Color(0.85f, 0.62f, 0.25f, 0.55f);
        static readonly Color GlyphColor = new Color(0.35f, 0.8f, 1f, 0.8f);

        float endTime;
        float duration;
        float damage;
        GameObject source;
        Health health;
        bool visualOnly;

        Transform ring;
        Transform timer;
        Transform[] grains;
        float[] phase;
        Material ringMaterial, timerMaterial, grainMaterial;

        // Authority (or sent there): lays a curse that bursts for `damage` after `seconds`.
        public static void Apply(GameObject target, GameObject damageSource, float damage, float seconds)
        {
            if (target == null || damage <= 0f || seconds <= 0f) return;
            if (!target.TryGetComponent(out Health h) || h.Structure || h.IsDead) return;
            if (!h.IsAuthority)
            {
                if (h.Net != null) h.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.RubricMark, damage, seconds, Vector3.zero, damageSource);
                return;
            }

            var curse = target.GetComponent<RubricMark>();
            if (curse == null) curse = target.AddComponent<RubricMark>();
            curse.visualOnly = false;
            curse.source = damageSource;
            curse.damage = Mathf.Max(Time.time < curse.endTime ? curse.damage : 0f, damage);
            curse.duration = seconds;
            curse.endTime = Time.time + seconds;
        }

        // Sets the curse off now, if `target` carries one. Sent to the server from anywhere else.
        public static void Detonate(GameObject target, GameObject by)
        {
            if (target == null || !target.TryGetComponent(out Health h) || h.IsDead) return;
            if (!h.IsAuthority)
            {
                if (h.Net != null && IsMarked(target)) h.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.RubricBurst, 0f, 0f, Vector3.zero, by);
                return;
            }
            if (target.TryGetComponent(out RubricMark c) && !c.visualOnly) c.BurstNow(true);
        }

        // True on every machine while a curse is shown (the server's real one or a drawn copy).
        public static bool IsMarked(GameObject target) => target != null && target.TryGetComponent(out RubricMark _);
        public static bool IsMarkedForReal(GameObject target) => target.TryGetComponent(out RubricMark c) && !c.visualOnly;

        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out RubricMark _)) return;
            var c = target.AddComponent<RubricMark>();
            c.visualOnly = true;
            c.duration = 3.5f;
            c.endTime = float.MaxValue;
        }

        // Authority only: a curse has just burst. Receives (victim, the one who laid it), after the burst damage landed
        // (the victim may be dead). The Sorcerer's Echoes of Prospero listens for it.
        public static event System.Action<Health, GameObject> Burst;

        // `early`: set off by one of the Sorcerer's abilities rather than running out (Hastened Ruin adds to it).
        void BurstNow(bool early = false)
        {
            float amount = damage;
            if (early && Anathema.Abilities.Sorcerer.SorcererUpgrade.Has(source, Anathema.Abilities.Sorcerer.SorcererUpgrade.HastenedRuin))
                amount += Anathema.Abilities.Sorcerer.SorcererUpgrade.HastenedRuinBonus;
            GameObject by = source;
            endTime = 0f;
            Destroy(this);
            PlayBurst(transform.position);
            if (health != null && health.Net != null && health.Net.IsSpawned) health.Net.ServerRubricBurst();
            // Area damage: the burst comes from the curse itself, so a facing block does not stop it.
            if (health != null && amount > 0f) health.TakeDamage(amount, by, aoe: true);
            if (health != null) Burst?.Invoke(health, by);
        }

        void Awake()
        {
            health = GetComponent<Health>();
            ringMaterial = VisualUtil.UnlitMaterial(RingColor, 3001);
            timerMaterial = VisualUtil.UnlitMaterial(GlyphColor, 3002);
            grainMaterial = VisualUtil.UnlitMaterial(Sand, 3003);

            ring = Disc("Curse of the Rubricae Ring", ringMaterial, 0.05f);
            timer = Disc("Curse of the Rubricae Timer", timerMaterial, 0.06f);

            grains = new Transform[Grains];
            phase = new float[Grains];
            for (int i = 0; i < Grains; i++)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Dust Grain";
                Destroy(g.GetComponent<Collider>());
                var r = g.GetComponent<Renderer>();
                r.sharedMaterial = grainMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.transform.SetParent(transform, false);
                g.transform.localScale = Vector3.one * 0.09f;
                grains[i] = g.transform;
                phase[i] = i / (float)Grains;
            }
        }

        Transform Disc(string discName, Material m, float height)
        {
            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            d.name = discName;
            Destroy(d.GetComponent<Collider>());
            var r = d.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            d.transform.SetParent(transform, false);
            d.transform.localPosition = Vector3.up * height;
            return d.transform;
        }

        void Update()
        {
            if (health == null || health.IsDead)
            {
                Destroy(this);
                return;
            }
            if (!visualOnly && Time.time >= endTime)
            {
                BurstNow();
                return;
            }

            // The ring stays; the inner glyph shrinks as the curse runs down (a drawn copy just pulses).
            float left = visualOnly ? 0.5f + 0.5f * Mathf.Sin(Time.time * 4f) : Mathf.Clamp01((endTime - Time.time) / Mathf.Max(0.01f, duration));
            ring.localScale = new Vector3(RingRadius * 2f, 0.01f, RingRadius * 2f);
            float inner = RingRadius * 2f * Mathf.Lerp(0.15f, 0.95f, left);
            timer.localScale = new Vector3(inner, 0.008f, inner);

            // Sand grains pour down an hourglass-shaped spiral around the victim, faster as the burst nears.
            float speed = Mathf.Lerp(2.2f, 0.6f, left);
            for (int i = 0; i < grains.Length; i++)
            {
                float t = Mathf.Repeat(Time.time * speed * 0.5f + phase[i], 1f);
                float height = Mathf.Lerp(2.1f, 0.15f, t);
                float waist = Mathf.Abs(t - 0.5f) * 2f; // narrow in the middle
                float radius = Mathf.Lerp(0.15f, 0.65f, waist);
                float a = (phase[i] + t * 1.5f) * Mathf.PI * 2f;
                grains[i].localPosition = new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
            }
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
            if (timer != null) Destroy(timer.gameObject);
            if (grains != null) foreach (Transform g in grains) if (g != null) Destroy(g.gameObject);
            if (ringMaterial != null) Destroy(ringMaterial);
            if (timerMaterial != null) Destroy(timerMaterial);
            if (grainMaterial != null) Destroy(grainMaterial);
        }

        // The burst: a gold flash, a ring of sand racing outward and grains flung up. Every machine plays it.
        public static void PlayBurst(Vector3 at)
        {
            var go = new GameObject("Curse of the Rubricae Burst");
            go.transform.position = at;
            go.AddComponent<BurstFx>();
        }

        sealed class BurstFx : MonoBehaviour
        {
            const float Life = 0.8f;
            const int Count = 16;

            Transform flash, wave;
            Transform[] motes;
            Vector3[] velocity;
            Material flashMat, waveMat, moteMat;
            float start;

            void Awake()
            {
                start = Time.time;
                flashMat = VisualUtil.UnlitMaterial(new Color(1f, 0.92f, 0.65f, 0.9f), 3005);
                waveMat = VisualUtil.UnlitMaterial(Sand, 3003);
                moteMat = VisualUtil.UnlitMaterial(Sand, 3004);
                flash = Part(PrimitiveType.Sphere, flashMat, Vector3.up * 1f);
                wave = Part(PrimitiveType.Cylinder, waveMat, Vector3.up * 0.05f);
                motes = new Transform[Count];
                velocity = new Vector3[Count];
                for (int i = 0; i < Count; i++)
                {
                    motes[i] = Part(PrimitiveType.Sphere, moteMat, Vector3.up * 1f);
                    motes[i].localScale = Vector3.one * 0.12f;
                    Vector3 d = Random.onUnitSphere;
                    d.y = Mathf.Abs(d.y) + 0.3f;
                    velocity[i] = d.normalized * Random.Range(3f, 7f);
                }
                Sfx.Play(Sfx.MagicImpact, 0.45f, 0.7f, 0.8f, 0.6f);
                Sfx.Play(Sfx.Impact, 0.3f, 1.2f, 1.35f, 0.4f);
            }

            Transform Part(PrimitiveType type, Material m, Vector3 local)
            {
                GameObject p = GameObject.CreatePrimitive(type);
                Destroy(p.GetComponent<Collider>());
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.transform.SetParent(transform, false);
                p.transform.localPosition = local;
                return p.transform;
            }

            void Update()
            {
                float age = Time.time - start;
                if (age >= Life)
                {
                    Destroy(gameObject);
                    return;
                }
                float t = age / Life;
                flash.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.2f, Mathf.Sqrt(Mathf.Clamp01(age / 0.2f)));
                SetAlpha(flashMat, 0.9f * (1f - Mathf.Clamp01(age / 0.25f)));
                float r = Mathf.Lerp(0.4f, 4f, Mathf.Sqrt(t));
                wave.localScale = new Vector3(r, 0.02f, r);
                SetAlpha(waveMat, 0.8f * (1f - t));
                for (int i = 0; i < motes.Length; i++)
                {
                    velocity[i] += Physics.gravity * 0.6f * Time.deltaTime;
                    motes[i].localPosition += velocity[i] * Time.deltaTime;
                }
                SetAlpha(moteMat, 1f - t);
            }

            static void SetAlpha(Material m, float a)
            {
                Color c = m.color;
                c.a = Mathf.Clamp01(a);
                m.color = c;
            }

            void OnDestroy()
            {
                if (flashMat != null) Destroy(flashMat);
                if (waveMat != null) Destroy(waveMat);
                if (moteMat != null) Destroy(moteMat);
            }
        }
    }
}
