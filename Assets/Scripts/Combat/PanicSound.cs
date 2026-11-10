using UnityEngine;

namespace Anathema.Combat
{
    // A short, terrified shriek synthesized in code (no audio assets): a high voice that jumps up in pitch with a
    // fast tremble, pushed through two bright vowel resonances and roughened with a little noise.
    public static class PanicSound
    {
        static AudioClip clip;

        public static void Play(float volume = 0.22f)
        {
            if (clip == null) clip = Build();

            var go = new GameObject("Panic Shriek");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f; // 2D: the camera is far above the action
            source.volume = volume;
            source.pitch = Random.Range(0.92f, 1.1f);
            source.Play();
            Object.Destroy(go, clip.length / source.pitch + 0.2f);
        }

        static AudioClip Build()
        {
            const int sampleRate = 44100;
            const float length = 0.9f;
            int n = (int)(sampleRate * length);
            var data = new float[n];
            var rng = new System.Random(911);

            float phase = 0f;
            float y1a = 0f, y2a = 0f, y1b = 0f, y2b = 0f;
            float peak = 0.0001f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;

                // Pitch: lunges up from ~500Hz to ~1100Hz, trembling quickly, then sags.
                float lunge = Mathf.Clamp01(t / 0.18f);
                float sag = Mathf.Clamp01((t - 0.55f) / 0.35f);
                float pitch = Mathf.Lerp(500f, 1100f, lunge * lunge) * Mathf.Lerp(1f, 0.8f, sag);
                pitch *= 1f + 0.04f * Mathf.Sin(t * Mathf.PI * 2f * 14f);
                phase += pitch / sampleRate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float voice = saw + (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f;

                // Two bright "eee" resonances (two-pole band-passes).
                float a = Resonate(voice, 900f, 0.985f, sampleRate, ref y1a, ref y2a);
                float b = Resonate(voice, 2600f, 0.98f, sampleRate, ref y1b, ref y2b);
                float s = a * 0.6f + b * 0.5f;

                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((length - t) / 0.3f);
                data[i] = s * env;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }

            for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;

            var c = AudioClip.Create("PanicShriek", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Resonate(float x, float freq, float r, int sampleRate, ref float y1, ref float y2)
        {
            float w = 2f * Mathf.PI * freq / sampleRate;
            float y = (1f - r) * x + 2f * r * Mathf.Cos(w) * y1 - r * r * y2;
            y2 = y1;
            y1 = y;
            return y * 4f;
        }
    }
}
