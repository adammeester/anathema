using UnityEngine;

namespace Anathema.Combat
{
    // Blood burst for a bladed hit: a spray of droplets that arc away from the blow and leave splats on the floor,
    // plus a wet "chunk" sound. Everything is generated in code, so no art or audio assets are needed.
    public class BloodImpact : MonoBehaviour
    {
        const int DropletCount = 14;
        const float Gravity = 22f;
        const float FloorY = 0.14f;     // where BloodDrop splats sit
        const float MaxLife = 1.2f;

        static Material material;
        static AudioClip clip;
        static AudioClip squelchClip;

        readonly Transform[] droplets = new Transform[DropletCount];
        readonly Vector3[] velocities = new Vector3[DropletCount];
        readonly bool[] landed = new bool[DropletCount];
        float spawnTime;

        // point: where the blade struck (about chest height); direction: the way the blow travels.
        // squelchy: a wetter, gurgling sound for heavy embedding hits (the thrown axe).
        public static void Play(Vector3 point, Vector3 direction, float intensity = 1f, bool squelchy = false)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

            var go = new GameObject("Blood Impact");
            go.transform.position = point;
            var impact = go.AddComponent<BloodImpact>();
            impact.Init(direction, intensity);

            PlaySound(intensity, squelchy);
        }

        void Init(Vector3 direction, float intensity)
        {
            spawnTime = Time.time;
            if (material == null) material = VisualUtil.UnlitMaterial(new Color(0.6f, 0.03f, 0.03f, 1f));

            int count = Mathf.Clamp(Mathf.RoundToInt(DropletCount * intensity), 6, DropletCount);
            for (int i = 0; i < DropletCount; i++)
            {
                if (i >= count)
                {
                    landed[i] = true;
                    continue;
                }

                var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                drop.name = "Blood Droplet";
                Destroy(drop.GetComponent<Collider>());
                var renderer = drop.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                float size = Random.Range(0.06f, 0.16f);
                drop.transform.SetParent(transform, false);
                drop.transform.localPosition = Vector3.zero;
                drop.transform.localScale = new Vector3(size, size, size);
                droplets[i] = drop.transform;

                // Mostly forward along the blow, fanned out sideways, with a pop upward.
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                Vector3 v = direction * Random.Range(1.5f, 6.5f)
                          + side * Random.Range(-3f, 3f)
                          + Vector3.up * Random.Range(2.5f, 6.5f);
                velocities[i] = v;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool anyFlying = false;

            for (int i = 0; i < DropletCount; i++)
            {
                if (landed[i]) continue;

                velocities[i].y -= Gravity * dt;
                Transform t = droplets[i];
                t.position += velocities[i] * dt;

                if (t.position.y <= FloorY)
                {
                    landed[i] = true;
                    Vector3 p = t.position;
                    Destroy(t.gameObject);
                    // Not every droplet leaves a mark; keeps the floor from filling up.
                    if (Random.value < 0.5f) BloodDrop.Spawn(new Vector3(p.x, FloorY, p.z));
                }
                else
                {
                    anyFlying = true;
                }
            }

            if (!anyFlying || Time.time - spawnTime > MaxLife) Destroy(gameObject);
        }

        static void PlaySound(float intensity, bool squelchy)
        {
            AudioClip chosen;
            if (squelchy)
            {
                if (squelchClip == null) squelchClip = BuildSquelchClip();
                chosen = squelchClip;
            }
            else
            {
                if (clip == null) clip = BuildClip();
                chosen = clip;
            }

            var go = new GameObject("Blood Impact Sound");
            var source = go.AddComponent<AudioSource>();
            source.clip = chosen;
            source.spatialBlend = 0f; // 2D: the camera is far above the action, so 3D falloff would make it inaudible
            source.volume = Mathf.Clamp01(0.3f * intensity);
            source.pitch = Random.Range(0.85f, 1.15f);
            source.Play();
            Destroy(go, chosen.length / source.pitch + 0.1f);
        }

        // Bloody squelch: a soft dull thud, then wet bubbling noise whose tone wobbles and slowly sinks.
        static AudioClip BuildSquelchClip()
        {
            const int sampleRate = 44100;
            const float length = 0.7f;
            int n = (int)(sampleRate * length);
            var data = new float[n];
            var rng = new System.Random(4242);

            float lowpass = 0f;
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Dull thud, lower and softer than the regular hit.
                float freq = Mathf.Lerp(90f, 45f, Mathf.Clamp01(t / 0.12f));
                phase += 2f * Mathf.PI * freq / sampleRate;
                float thud = Mathf.Sin(phase) * Mathf.Exp(-t * 20f);

                // Wet body: very dark noise, cutoff wobbling like bubbles popping, sinking over time.
                float wobble = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * (22f - 14f * t));
                float alpha = Mathf.Lerp(0.16f, 0.025f, Mathf.Clamp01(t / 0.6f)) * (0.35f + 0.65f * wobble);
                lowpass += alpha * (noise - lowpass);
                float body = lowpass * (0.5f + 0.5f * wobble) * Mathf.Exp(-t * 4.5f) * 5f;

                // Short fleshy smack at the start.
                float smack = noise * Mathf.Exp(-t * 60f) * 0.25f;

                float s = thud * 0.8f + body + smack;
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }

            var c = AudioClip.Create("BloodSquelch", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        // Wet slash: a low thump, a bright slicing transient, and a squelchy low-passed noise tail.
        static AudioClip BuildClip()
        {
            const int sampleRate = 44100;
            const float length = 0.4f;
            int n = (int)(sampleRate * length);
            var data = new float[n];
            var rng = new System.Random(1337);

            float lowpass = 0f;
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Thump: sine sweeping 140Hz -> 50Hz.
                float freq = Mathf.Lerp(140f, 50f, Mathf.Clamp01(t / 0.15f));
                phase += 2f * Mathf.PI * freq / sampleRate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 18f);

                // Slice: very short bright noise.
                float slice = noise * Mathf.Exp(-t * 90f);

                // Squelch: noise through a low-pass whose cutoff closes over time.
                float alpha = Mathf.Lerp(0.5f, 0.04f, Mathf.Clamp01(t / 0.3f));
                lowpass += alpha * (noise - lowpass);
                float squelch = lowpass * Mathf.Exp(-t * 11f) * 2.2f;

                float s = thump * 0.9f + slice * 0.45f + squelch * 0.8f;
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }

            var c = AudioClip.Create("BloodImpact", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
