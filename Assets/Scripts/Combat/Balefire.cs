using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: green, witch-lit flames (the Mandrake's Balefire). It burns like Burning, but it is tied to the
    // character who lit it: the next time that character lands any hit on the victim (Health.ApplyHitLocal calls
    // OnStruck), everything the balefire still had left to burn lands at once and the flames go out. Damage-over-time
    // ticks (poison, other burns) don't set it off; only real hits do. The burst also cuts the victim's healing
    // received for a few seconds (Health.CutHealing).
    // Re-applying refreshes the duration and keeps the stronger burn.
    // Like Burning it lives on the server's copy of the victim (it deals the damage there); everyone else draws a
    // visual-only copy from the server's published state (NetPlayer.SyncServerFx).
    public class Balefire : MonoBehaviour
    {
        const float TickInterval = 0.5f;
        const int Flames = 6;
        public const float BurstHealingCut = 0.3f;      // healing received after the burst is cut by this share...
        public const float BurstHealingCutSeconds = 4f; // ...for this long
        const float RingRadius = 0.6f;

        // Green witch-fire, matching the baleflame on the Mandrake's hand and runes. A cooler emerald with pale tips, so
        // it stays a little apart from the yellow-green of his poison.
        public static readonly Color Core = new Color(0.2f, 1f, 0.45f, 0.8f);
        public static readonly Color Tip = new Color(0.75f, 1f, 0.75f, 0.9f);
        static readonly Color RingColor = new Color(0.1f, 0.75f, 0.3f, 0.45f);

        float endTime;
        float damagePerSecond;
        float duration;   // how long the burn was set to last when it was last lit
        float nextTickTime;
        GameObject source;
        Health health;
        bool visualOnly; // drawn from the server's published state: shows the flames, the server takes the damage

        Transform[] flames;
        float[] phase;
        Material flameMaterial;
        Material tipMaterial;
        Transform ring;
        Material ringMaterial;

        // What the flames would still deal if left to burn out.
        public float RemainingDamage => damagePerSecond * Mathf.Max(0f, endTime - Time.time);

        public static Balefire Apply(GameObject target, GameObject damageSource, float damagePerSecond, float duration)
        {
            if (target == null) return null;
            if (target.TryGetComponent(out Health targetHealth))
            {
                if (targetHealth.Structure || targetHealth.IsDead) return null;
                if (!targetHealth.IsAuthority)
                {
                    targetHealth.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Balefire, damagePerSecond, duration, Vector3.zero, damageSource);
                    return null;
                }
            }

            var balefire = target.GetComponent<Balefire>();
            if (balefire == null)
            {
                balefire = target.AddComponent<Balefire>();
                balefire.nextTickTime = Time.time + TickInterval;
            }
            balefire.visualOnly = false;
            balefire.source = damageSource;
            balefire.endTime = Time.time + duration;
            balefire.damagePerSecond = Mathf.Max(balefire.damagePerSecond, damagePerSecond);
            balefire.duration = duration;
            return balefire;
        }

        // Authority: `attacker` just landed a hit on `victim`. If it is the one who lit the victim's balefire, the
        // flames burst.
        public static void OnStruck(GameObject victim, GameObject attacker)
        {
            if (victim == null || attacker == null) return;
            if (!victim.TryGetComponent(out Balefire b) || b.visualOnly || b.source != attacker) return;
            b.Detonate();
        }

        // Authority only: a balefire has just burst. Receives (victim, the one who lit it, its burn per second, the
        // duration it was lit with), after the burst damage and healing cut have landed (the victim may be dead).
        public static event System.Action<Health, GameObject, float, float> Burst;

        void Detonate()
        {
            float burst = RemainingDamage;
            endTime = 0f; // spent, whatever happens below
            Destroy(this);
            PlayBurst(transform.position);
            if (health != null && health.Net != null && health.Net.IsSpawned) health.Net.ServerBalefireBurst();
            if (health != null && burst > 0f) health.TakeDamage(burst, source, false, dot: true);
            if (health != null && !health.IsDead) health.CutHealing(BurstHealingCut, BurstHealingCutSeconds);
            if (health != null) Burst?.Invoke(health, source, damagePerSecond, duration);
        }

        // The burst, at the victim's feet: a white-green flash, a fireball of witch-fire that swells and burns out, a
        // shockwave racing along the ground, a column of flame shooting up, tongues of fire and embers flung outward, and
        // a ring of green flame left smouldering on the ground. Every machine plays it (NetPlayer.ServerBalefireBurst).
        const float BurstScale = 0.7f;

        public static void PlayBurst(Vector3 at)
        {
            var go = new GameObject("Balefire Burst");
            go.transform.position = at;
            go.transform.localScale = Vector3.one * BurstScale; // every part lives in local space, so this sizes the whole blast
            go.AddComponent<BurstFx>();
        }

        sealed class BurstFx : MonoBehaviour
        {
            const float Life = 1.4f;
            const float BodyHeight = 1f;  // the blast centres on the torso; the ground is at local y = 0
            const int Tongues = 10;
            const int Embers = 22;
            const int Smoke = 6;
            const int GroundFlames = 8;
            const float GroundRingRadius = 1.6f;

            static readonly Color Flash = new Color(0.92f, 1f, 0.9f, 1f);
            static readonly Color Outer = new Color(0.05f, 0.55f, 0.2f, 0.7f);
            static readonly Color SmokeColor = new Color(0.04f, 0.12f, 0.07f, 0.5f);

            Transform flash, core, outer, wave, column, scorch;
            Transform[] tongues, embers, smoke, groundFlames;
            Vector3[] tongueVelocity, emberVelocity, smokeDrift;
            float[] groundPhase;
            Material flashMat, coreMat, outerMat, waveMat, columnMat, scorchMat, tongueMat, emberMat, smokeMat;
            float start;

            void Awake()
            {
                start = Time.time;
                flashMat = VisualUtil.UnlitMaterial(Flash, 3006);
                coreMat = VisualUtil.UnlitMaterial(Tip, 3005);
                outerMat = VisualUtil.UnlitMaterial(Outer, 3004);
                waveMat = VisualUtil.UnlitMaterial(Core, 3003);
                columnMat = VisualUtil.UnlitMaterial(Core, 3004);
                scorchMat = VisualUtil.UnlitMaterial(RingColor, 3001);
                tongueMat = VisualUtil.UnlitMaterial(Core, 3004);
                emberMat = VisualUtil.UnlitMaterial(Tip, 3005);
                smokeMat = VisualUtil.UnlitMaterial(SmokeColor, 3002);

                Vector3 body = Vector3.up * BodyHeight;
                flash = Part("Burst Flash", flashMat, body);
                core = Part("Burst Fireball", coreMat, body);
                outer = Part("Burst Fireball Outer", outerMat, body);
                wave = Part("Burst Shockwave", waveMat, Vector3.up * 0.05f);
                column = Part("Burst Column", columnMat, Vector3.up * 0.2f);
                scorch = Part("Burst Scorch", scorchMat, Vector3.up * 0.03f);
                flash.localScale = core.localScale = outer.localScale = wave.localScale = column.localScale = scorch.localScale = Vector3.zero;

                tongues = new Transform[Tongues];
                tongueVelocity = new Vector3[Tongues];
                for (int i = 0; i < Tongues; i++)
                {
                    float a = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / Tongues;
                    Vector3 dir = new Vector3(Mathf.Cos(a), Random.Range(0.15f, 0.7f), Mathf.Sin(a)).normalized;
                    tongues[i] = Part("Burst Tongue", tongueMat, body);
                    tongues[i].localScale = Vector3.zero;
                    tongueVelocity[i] = dir * Random.Range(6f, 9f);
                }

                embers = new Transform[Embers];
                emberVelocity = new Vector3[Embers];
                for (int i = 0; i < Embers; i++)
                {
                    embers[i] = Part("Burst Ember", emberMat, body + Random.insideUnitSphere * 0.3f);
                    Vector3 dir = Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) + 0.4f;
                    emberVelocity[i] = dir.normalized * Random.Range(4f, 10f);
                }

                smoke = new Transform[Smoke];
                smokeDrift = new Vector3[Smoke];
                for (int i = 0; i < Smoke; i++)
                {
                    Vector2 r = Random.insideUnitCircle * 0.6f;
                    smoke[i] = Part("Burst Smoke", smokeMat, new Vector3(r.x, BodyHeight + Random.Range(0.2f, 0.8f), r.y));
                    smoke[i].localScale = Vector3.zero;
                    smokeDrift[i] = new Vector3(r.x * 0.8f, Random.Range(1.2f, 2f), r.y * 0.8f);
                }

                groundFlames = new Transform[GroundFlames];
                groundPhase = new float[GroundFlames];
                for (int i = 0; i < GroundFlames; i++)
                {
                    float a = i * Mathf.PI * 2f / GroundFlames;
                    groundFlames[i] = Part("Burst Ground Flame", i % 2 == 0 ? tongueMat : emberMat,
                        new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * GroundRingRadius);
                    groundFlames[i].localScale = Vector3.zero;
                    groundPhase[i] = Random.value * 10f;
                }

                Sfx.Play(Sfx.SlamExplosion, 0.55f, 1.15f, 1.3f, 0.8f);
                Sfx.Play(Sfx.GroundFire, 0.45f, 1.1f, 1.25f, 0.8f);
                Sfx.Play(Sfx.Flamethrower, 0.3f, 0.8f, 0.95f, 0.5f);
            }

            Transform Part(string partName, Material material, Vector3 localPosition)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.name = partName;
                Destroy(p.GetComponent<Collider>());
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                p.transform.SetParent(transform, false);
                p.transform.localPosition = localPosition;
                return p.transform;
            }

            static void Fade(Material m, Color baseColor, float alpha)
            {
                baseColor.a *= Mathf.Clamp01(alpha);
                m.color = baseColor;
            }

            static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

            void Update()
            {
                float age = Time.time - start;
                if (age >= Life)
                {
                    Destroy(gameObject);
                    return;
                }
                float dt = Time.deltaTime;

                // Flash: a blinding pop at the heart of the blast.
                float ft = Mathf.Clamp01(age / 0.15f);
                flash.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.6f, EaseOut(ft));
                Fade(flashMat, Flash, 1f - ft);

                // Fireball: a bright core inside a darker emerald shell, both swelling fast; the core burns out first and
                // the shell lifts and thins.
                float ct = Mathf.Clamp01(age / 0.35f);
                core.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.4f, EaseOut(ct));
                Fade(coreMat, Tip, (1f - ct) * (1f - ct));
                float ot = Mathf.Clamp01(age / 0.7f);
                outer.localScale = new Vector3(1f, 0.9f, 1f) * Mathf.Lerp(0.8f, 3.8f, EaseOut(ot));
                outer.localPosition = Vector3.up * (BodyHeight + 0.6f * ot);
                Fade(outerMat, Outer, 1f - ot);

                // Shockwave: a thin ring of green fire racing across the ground.
                float wt = Mathf.Clamp01(age / 0.45f);
                float wr = Mathf.Lerp(0.6f, 7f, EaseOut(wt));
                wave.localScale = new Vector3(wr, 0.04f, wr);
                Fade(waveMat, Core, (1f - wt) * 0.8f);

                // Column: a pillar of flame shoots up out of the blast, then thins and fades.
                float pt = Mathf.Clamp01(age / 0.5f);
                float height = Mathf.Lerp(0.5f, 5f, EaseOut(Mathf.Clamp01(age / 0.25f)));
                float width = Mathf.Lerp(1.1f, 0.2f, pt);
                column.localScale = new Vector3(width, height, width);
                column.localPosition = Vector3.up * (height * 0.5f);
                Fade(columnMat, Core, 1f - pt);

                // Tongues: long streaks of fire flung outward, slowing and shrinking as they go.
                float tt = Mathf.Clamp01(age / 0.45f);
                for (int i = 0; i < tongues.Length; i++)
                {
                    tongueVelocity[i] *= Mathf.Exp(-5f * dt);
                    tongues[i].localPosition += tongueVelocity[i] * dt;
                    tongues[i].localRotation = Quaternion.LookRotation(tongueVelocity[i] + Vector3.up * 0.001f);
                    float w = Mathf.Lerp(0.45f, 0.05f, tt);
                    tongues[i].localScale = new Vector3(w, w, Mathf.Lerp(1.6f, 0.3f, tt));
                }
                Fade(tongueMat, Core, 1f - Mathf.InverseLerp(0.25f, Life, age));

                // Embers: fly out and up, fall back, skid along the ground and shrink away.
                float et = Mathf.Clamp01(age / 1.1f);
                for (int i = 0; i < embers.Length; i++)
                {
                    emberVelocity[i] += Physics.gravity * 0.5f * dt;
                    Vector3 p = embers[i].localPosition + emberVelocity[i] * dt;
                    if (p.y < 0.05f)
                    {
                        p.y = 0.05f;
                        emberVelocity[i] = new Vector3(emberVelocity[i].x * 0.5f, 0f, emberVelocity[i].z * 0.5f);
                    }
                    embers[i].localPosition = p;
                    embers[i].localScale = Vector3.one * Mathf.Lerp(0.18f, 0f, et);
                }
                Fade(emberMat, Tip, 1f - Mathf.InverseLerp(0.6f, Life, age));

                // Smoke: dark green-black puffs rise out of the fireball and drift apart.
                float st = Mathf.Clamp01((age - 0.1f) / (Life - 0.1f));
                if (st > 0f)
                {
                    for (int i = 0; i < smoke.Length; i++)
                    {
                        smoke[i].localPosition += smokeDrift[i] * dt;
                        smoke[i].localScale = Vector3.one * Mathf.Lerp(0.4f, 1.8f, EaseOut(st));
                    }
                }
                Fade(smokeMat, SmokeColor, Mathf.Sin(st * Mathf.PI));

                // Scorch: a ring of witch-fire left burning where the victim stood, flickering, then guttering out.
                float sr = Mathf.Lerp(0.5f, GroundRingRadius * 2f, EaseOut(Mathf.Clamp01(age / 0.25f)));
                scorch.localScale = new Vector3(sr, 0.01f, sr);
                float gutter = 1f - Mathf.InverseLerp(0.7f, Life, age);
                Fade(scorchMat, RingColor, gutter);
                float grow = EaseOut(Mathf.Clamp01((age - 0.1f) / 0.2f));
                for (int i = 0; i < groundFlames.Length; i++)
                {
                    float t = Time.time * 10f + groundPhase[i];
                    float h = (0.4f + 0.3f * Mathf.Abs(Mathf.Sin(t))) * grow * gutter;
                    float w = 0.2f * grow * gutter;
                    groundFlames[i].localScale = new Vector3(w, h, w);
                    Vector3 lp = groundFlames[i].localPosition;
                    groundFlames[i].localPosition = new Vector3(lp.x, h * 0.5f, lp.z);
                }
            }

            void OnDestroy()
            {
                foreach (Material m in new[] { flashMat, coreMat, outerMat, waveMat, columnMat, scorchMat, tongueMat, emberMat, smokeMat })
                    if (m != null) Destroy(m);
            }
        }

        public static bool IsBurning(GameObject target) => target != null && target.TryGetComponent(out Balefire _);

        // A balefire that is really doing damage here (not a drawn copy).
        public static bool IsBurningForReal(GameObject target) => target.TryGetComponent(out Balefire b) && !b.visualOnly;

        // Online: draws a balefire the server reports; lasts until the server says it has stopped.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Balefire _)) return;
            var balefire = target.AddComponent<Balefire>();
            balefire.visualOnly = true;
            balefire.endTime = float.MaxValue;
            balefire.nextTickTime = float.MaxValue;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            flameMaterial = VisualUtil.UnlitMaterial(Core, 3002);
            tipMaterial = VisualUtil.UnlitMaterial(Tip, 3003);
            ringMaterial = VisualUtil.UnlitMaterial(RingColor, 3001);

            // A witch-fire circle burns on the ground around the victim's feet.
            GameObject ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "Balefire Ring";
            Destroy(ringGo.GetComponent<Collider>());
            var ringRenderer = ringGo.GetComponent<Renderer>();
            ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ring = ringGo.transform;

            flames = new Transform[Flames];
            phase = new float[Flames];
            for (int i = 0; i < Flames; i++)
            {
                GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "Balefire Flame";
                Destroy(f.GetComponent<Collider>());
                var r = f.GetComponent<Renderer>();
                r.sharedMaterial = i % 2 == 0 ? flameMaterial : tipMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.transform.SetParent(transform, false);
                flames[i] = f.transform;
                phase[i] = Random.value * 10f;
            }
        }

        void OnDestroy()
        {
            if (flames != null) foreach (Transform f in flames) if (f != null) Destroy(f.gameObject);
            if (ring != null) Destroy(ring.gameObject);
            if (flameMaterial != null) Destroy(flameMaterial);
            if (tipMaterial != null) Destroy(tipMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
        }

        void Update()
        {
            if (health == null || health.IsDead || Time.time >= endTime)
            {
                Destroy(this);
                return;
            }

            if (Time.time >= nextTickTime)
            {
                nextTickTime += TickInterval;
                if (!visualOnly) health.TakeDamage(damagePerSecond * TickInterval, source, false, dot: true);
                if (health.IsDead) return;
            }

            // The ground circle pulses.
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            ring.localScale = new Vector3(RingRadius * 2f * pulse, 0.01f, RingRadius * 2f * pulse);

            // Tall green tongues of flame stand on the circle and lick upward, each stretching and shrinking on its
            // own beat (they hold their places rather than drifting up, unlike poison's rising droplets).
            for (int i = 0; i < flames.Length; i++)
            {
                float t = Time.time * 9f + phase[i];
                float angle = i * Mathf.PI * 2f / flames.Length + 0.15f * Mathf.Sin(t * 0.5f);
                float height = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(t)) + (i % 2 == 0 ? 0.25f : 0f);
                float width = 0.16f + 0.04f * Mathf.Sin(t * 1.7f);
                flames[i].localPosition = new Vector3(Mathf.Cos(angle) * RingRadius * 0.75f, height * 0.5f + 0.1f, Mathf.Sin(angle) * RingRadius * 0.75f);
                flames[i].localScale = new Vector3(width, height, width);
            }
        }
    }
}
