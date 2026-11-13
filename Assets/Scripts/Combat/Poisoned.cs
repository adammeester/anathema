using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: a stacking toxin. Each stack deals damage over time; re-applying adds stacks and refreshes the
    // duration. Reaching DetonateStacks (or an ability calling DetonateNow) makes it burst: everything the poison still had left to deal lands at once and the
    // poison is spent. Green droplets bead and rise off the victim, one per stack.
    // Like Bleeding and Burning it lives on the server's copy of the victim (it deals the damage there); everyone else
    // draws a visual-only copy from the server's published state (NetPlayer.SyncServerFx).
    public class Poisoned : MonoBehaviour
    {
        public const int DetonateStacks = 6;     // at this many stacks the poison bursts
        const float TickInterval = 0.5f;
        const int Droplets = DetonateStacks;

        public static readonly Color Green = new Color(0.45f, 1f, 0.25f, 0.85f);

        float endTime;
        float damagePerSecondPerStack;
        int stacks;
        float nextTickTime;
        GameObject source;
        Health health;
        bool visualOnly; // drawn from the server's published state: shows the toxin, the server takes the damage

        Transform[] drops;
        float[] phase;
        Material dropMaterial;

        public int Stacks => stacks;

        // Authority only: a poison tick (or the burst) just dealt damage to the victim. Receives (victim, source).
        public static event System.Action<Health, GameObject> Ticked;

        // Authority only: poison was just laid on the victim (and didn't burst on the spot). Receives (victim, source).
        public static event System.Action<Health, GameObject> Applied;

        // Adds `addStacks` stacks (capped) of `damagePerSecondPerStack` each and refreshes the duration.
        public static Poisoned Apply(GameObject target, GameObject damageSource, int addStacks, float damagePerSecondPerStack, float duration)
        {
            if (target == null || addStacks <= 0) return null;
            if (target.TryGetComponent(out Health targetHealth))
            {
                if (targetHealth.Structure || targetHealth.IsDead) return null;
                if (!targetHealth.IsAuthority)
                {
                    // The stack count rides in direction.x so the status message keeps its usual (a, b) layout.
                    targetHealth.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Poison, damagePerSecondPerStack, duration,
                        new Vector3(addStacks, 0f, 0f), damageSource);
                    return null;
                }
            }

            var poisoned = target.GetComponent<Poisoned>();
            if (poisoned == null)
            {
                poisoned = target.AddComponent<Poisoned>();
                poisoned.nextTickTime = Time.time + TickInterval;
            }
            poisoned.visualOnly = false;
            poisoned.source = damageSource;
            poisoned.stacks = Mathf.Clamp(poisoned.stacks + addStacks, 1, DetonateStacks);
            poisoned.damagePerSecondPerStack = Mathf.Max(poisoned.damagePerSecondPerStack, damagePerSecondPerStack);
            poisoned.endTime = Time.time + duration;
            if (poisoned.stacks >= DetonateStacks)
            {
                poisoned.Detonate();
                return null;
            }
            if (poisoned.health != null) Applied?.Invoke(poisoned.health, damageSource);
            return poisoned;
        }

        // Authority: sets off whatever poison `target` carries right now, whatever its stack count (for abilities that
        // pop poison early). The burst deals everything the poison had left and looks smaller the fewer stacks it had.
        // False if there was no real poison to set off.
        public static bool DetonateNow(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out Poisoned p) || p.visualOnly || p.stacks <= 0) return false;
            p.Detonate();
            return true;
        }

        // Authority: the whole remaining poison lands as one burst, then the poison is gone.
        void Detonate()
        {
            float remaining = Mathf.Max(0f, endTime - Time.time);
            float burst = damagePerSecondPerStack * stacks * remaining;
            float size = BurstSize(stacks);
            stacks = 0;
            endTime = 0f; // spent, whatever happens below
            Destroy(this);
            PlayBurst(transform.position, size);
            if (health != null && health.Net != null && health.Net.IsSpawned) health.Net.ServerPoisonBurst(size);
            if (health != null && burst > 0f && health.TakeDamage(burst, source, true, dot: true)) Ticked?.Invoke(health, source);
        }

        // A full (DetonateStacks) burst is size 1; an early one with a single stack is a bit over half that.
        static float BurstSize(int stacks) => Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(1, DetonateStacks, stacks));

        // The detonation, at the victim's feet: a pale flash, a swelling toxic shell and a shockwave along the ground, a
        // spray of droplets that splat where they land, a spreading puddle of bile and gas that lingers and rises.
        // Every machine plays it (NetPlayer.ServerPoisonBurst).
        public static void PlayBurst(Vector3 at, float size = 1f)
        {
            var go = new GameObject("Poison Burst");
            go.transform.position = at;
            go.AddComponent<PoisonBurstFx>().size = Mathf.Max(0.1f, size);
        }

        sealed class PoisonBurstFx : MonoBehaviour
        {
            const float Life = 1.7f;
            const float BodyHeight = 1f; // the blast centres on the torso; the ground is at local y = 0
            const int Spray = 18;
            const int Puffs = 7;

            static readonly Color Flash = new Color(0.9f, 1f, 0.6f, 1f);
            static readonly Color Bile = new Color(0.25f, 0.6f, 0.1f, 0.8f);
            static readonly Color Gas = new Color(0.35f, 0.7f, 0.15f, 0.45f);

            public float size = 1f;

            Transform flash, shell, wave, puddle;
            Transform[] drops;
            Vector3[] dropVelocity;
            bool[] landed;
            float[] landTime;
            Transform[] puffs;
            Vector3[] puffDrift;
            float[] puffDelay;
            Material flashMat, shellMat, waveMat, puddleMat, dropMat, gasMat;
            float start;

            // Start, not Awake: PlayBurst sets `size` right after AddComponent.
            void Start()
            {
                start = Time.time;
                flashMat = VisualUtil.UnlitMaterial(Flash, 3005);
                shellMat = VisualUtil.UnlitMaterial(Green, 3004);
                waveMat = VisualUtil.UnlitMaterial(Green, 3003);
                puddleMat = VisualUtil.UnlitMaterial(Bile, 3001);
                dropMat = VisualUtil.UnlitMaterial(Green, 3004);
                gasMat = VisualUtil.UnlitMaterial(Gas, 3002);

                Vector3 body = Vector3.up * BodyHeight;
                flash = Part("Burst Flash", flashMat, body);
                shell = Part("Burst Shell", shellMat, body);
                wave = Part("Burst Shockwave", waveMat, Vector3.up * 0.05f);
                puddle = Part("Burst Puddle", puddleMat, Vector3.up * 0.02f);
                flash.localScale = shell.localScale = wave.localScale = puddle.localScale = Vector3.zero;

                drops = new Transform[Spray];
                dropVelocity = new Vector3[Spray];
                landed = new bool[Spray];
                landTime = new float[Spray];
                for (int i = 0; i < Spray; i++)
                {
                    drops[i] = Part("Burst Drop", dropMat, body + Random.insideUnitSphere * 0.25f);
                    drops[i].localScale = Vector3.zero;
                    Vector3 dir = Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) * 0.8f + 0.35f; // mostly out and up, so they arc and rain down
                    dropVelocity[i] = dir.normalized * Random.Range(3.5f, 7.5f) * Mathf.Sqrt(size);
                }

                puffs = new Transform[Puffs];
                puffDrift = new Vector3[Puffs];
                puffDelay = new float[Puffs];
                for (int i = 0; i < Puffs; i++)
                {
                    Vector2 r = Random.insideUnitCircle * 0.9f * size;
                    puffs[i] = Part("Burst Gas", gasMat, new Vector3(r.x, Random.Range(0.3f, 1.4f), r.y));
                    puffs[i].localScale = Vector3.zero;
                    puffDrift[i] = new Vector3(r.x, 0f, r.y) * 0.6f + Vector3.up * Random.Range(0.4f, 0.9f);
                    puffDelay[i] = Random.Range(0.05f, 0.2f);
                }

                Sfx.Play(Sfx.DiseaseBurst, 0.55f, 1.1f, 1.3f, 1f);
                Sfx.Play(Sfx.SlamExplosion, 0.3f, 1.4f, 1.6f, 0.5f);
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

                // Flash: a quick pale pop at the heart of the blast.
                float ft = Mathf.Clamp01(age / 0.18f);
                flash.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.9f, EaseOut(ft)) * size;
                Fade(flashMat, Flash, 1f - ft);

                // Shell: the toxic blast swelling outward.
                float st = Mathf.Clamp01(age / 0.5f);
                shell.localScale = new Vector3(1f, 0.85f, 1f) * Mathf.Lerp(0.6f, 3.6f, EaseOut(st)) * size;
                Fade(shellMat, Green, (1f - st) * (1f - st) * 0.9f);

                // Shockwave: a thin disc racing out across the ground.
                float wt = Mathf.Clamp01(age / 0.4f);
                float wr = Mathf.Lerp(0.5f, 5.5f, EaseOut(wt)) * size;
                wave.localScale = new Vector3(wr, 0.04f, wr);
                Fade(waveMat, Green, (1f - wt) * 0.7f);

                // Puddle: bile spreads under the victim, sits, then soaks away.
                float pr = Mathf.Lerp(0.3f, 2.4f, EaseOut(Mathf.Clamp01(age / 0.3f))) * size;
                puddle.localScale = new Vector3(pr, 0.02f, pr);
                Fade(puddleMat, Bile, 1f - Mathf.InverseLerp(0.8f, Life, age));

                // Droplets: fly stretched along their path, then splat flat where they hit the ground.
                for (int i = 0; i < drops.Length; i++)
                {
                    Transform d = drops[i];
                    if (!landed[i])
                    {
                        dropVelocity[i] += Physics.gravity * dt;
                        Vector3 p = d.localPosition + dropVelocity[i] * dt;
                        if (p.y <= 0.03f)
                        {
                            p.y = 0.03f;
                            landed[i] = true;
                            landTime[i] = age;
                            d.localRotation = Quaternion.identity;
                        }
                        else
                        {
                            d.localRotation = Quaternion.LookRotation(dropVelocity[i]);
                            float stretch = Mathf.Clamp(dropVelocity[i].magnitude * 0.25f, 1f, 2.5f);
                            d.localScale = new Vector3(0.13f, 0.13f, 0.13f * stretch) * Mathf.Sqrt(size);
                        }
                        d.localPosition = p;
                    }
                    else
                    {
                        float lt = Mathf.Clamp01((age - landTime[i]) / 0.15f);
                        float splat = Mathf.Lerp(0.15f, 0.42f, EaseOut(lt)) * Mathf.Sqrt(size);
                        d.localScale = new Vector3(splat, 0.02f, splat);
                    }
                }
                Fade(dropMat, Green, 1f - Mathf.InverseLerp(Life - 0.6f, Life, age));

                // Gas: green puffs bloom just after the blast and drift up as they thin out.
                for (int i = 0; i < puffs.Length; i++)
                {
                    float gt = Mathf.Clamp01((age - puffDelay[i]) / (Life - puffDelay[i]));
                    if (gt <= 0f) continue;
                    puffs[i].localPosition += puffDrift[i] * dt;
                    puffs[i].localScale = Vector3.one * Mathf.Lerp(0.5f, 1.7f, EaseOut(gt)) * size;
                }
                Fade(gasMat, Gas, 1f - Mathf.InverseLerp(0.4f, Life, age));
            }

            void OnDestroy()
            {
                foreach (Material m in new[] { flashMat, shellMat, waveMat, puddleMat, dropMat, gasMat })
                    if (m != null) Destroy(m);
            }
        }

        // True on every machine while the target shows poison (real or drawn), e.g. for bonus damage against it.
        public static bool IsPoisoned(GameObject target) => target != null && target.TryGetComponent(out Poisoned _);

        // A poison that is really doing damage here (not a drawn copy).
        public static bool IsPoisonedForReal(GameObject target) => target.TryGetComponent(out Poisoned p) && !p.visualOnly;


        // Online: draws a poison the server reports; lasts until the server says it has stopped.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Poisoned _)) return;
            var poisoned = target.AddComponent<Poisoned>();
            poisoned.visualOnly = true;
            poisoned.stacks = 3; // the stack count isn't published; draw a middling amount
            poisoned.endTime = float.MaxValue;
            poisoned.nextTickTime = float.MaxValue;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            dropMaterial = VisualUtil.UnlitMaterial(Green, 3002);
            drops = new Transform[Droplets];
            phase = new float[Droplets];
            for (int i = 0; i < Droplets; i++)
            {
                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                d.name = "Poison Droplet";
                Destroy(d.GetComponent<Collider>());
                var r = d.GetComponent<Renderer>();
                r.sharedMaterial = dropMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.transform.SetParent(transform, false);
                drops[i] = d.transform;
                phase[i] = Random.value;
            }
        }

        void OnDestroy()
        {
            if (drops != null) foreach (Transform d in drops) if (d != null) Destroy(d.gameObject);
            if (dropMaterial != null) Destroy(dropMaterial);
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
                if (!visualOnly && health.TakeDamage(damagePerSecondPerStack * stacks * TickInterval, source, true, dot: true))
                    Ticked?.Invoke(health, source);
                if (health.IsDead) return;
            }

            // Droplets rise off the body and fade out; one per stack is shown.
            int shown = Mathf.Clamp(stacks, 1, Droplets);
            for (int i = 0; i < drops.Length; i++)
            {
                bool on = i < shown;
                if (drops[i].gameObject.activeSelf != on) drops[i].gameObject.SetActive(on);
                if (!on) continue;
                float t = Mathf.Repeat(Time.time * 0.7f + phase[i], 1f);
                float angle = (phase[i] * 6.28f) + i * 1.1f;
                float s = 0.16f * (1f - t * 0.7f);
                drops[i].localPosition = new Vector3(Mathf.Cos(angle) * 0.38f, 0.5f + t * 1.6f, Mathf.Sin(angle) * 0.38f);
                drops[i].localScale = new Vector3(s, s * 1.3f, s);
            }
        }
    }
}
