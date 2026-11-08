using UnityEngine;

namespace Anathema.Combat
{
    // A huge guttural battle roar, synthesized in code (no audio assets): a rough low voice pushed through two
    // vowel-like resonances, with growl and distortion. It swells, holds, then tails off.
    public static class RoarSound
    {
        static AudioClip clip;

        // Starts the roar and returns its source so the caller can cut it short with Stop.
        public static AudioSource Play(float volume = 0.5f)
        {
            if (clip == null) clip = Build();

            var go = new GameObject("Roar");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f; // 2D: the camera is far above the action
            source.volume = volume;
            source.pitch = Random.Range(0.95f, 1.05f);
            source.Play();
            Object.Destroy(go, clip.length / source.pitch + 0.2f);
            return source;
        }

        public static void Stop(AudioSource source)
        {
            if (source != null) Object.Destroy(source.gameObject);
        }

        static AudioClip Build()
        {
            const int sampleRate = 44100;
            const float length = 1.6f;
            int n = (int)(sampleRate * length);
            var data = new float[n];
            var rng = new System.Random(666);

            float phase = 0f;
            // Two-pole resonators (formants) at a wide-open "aaah" vowel.
            float y1a = 0f, y2a = 0f, y1b = 0f, y2b = 0f;
            float lowNoise = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;

                // Loudness: swell fast, hold, then a long fall.
                float env = Mathf.Clamp01(t / 0.35f);
                env *= env;
                if (t > 1.0f) env *= Mathf.Clamp01((length - t) / (length - 1.0f));

                // Pitch: starts low, climbs to the peak of the roar, then drops away. Slow tremble on top.
                float pitch = 70f + 55f * Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI * 0.5f);
                if (t > 1.1f) pitch -= (t - 1.1f) * 60f;
                pitch *= 1f + 0.03f * Mathf.Sin(t * 2f * Mathf.PI * 6f);

                // Voice source: a raspy sawtooth.
                phase += pitch / sampleRate;
                phase -= Mathf.Floor(phase);
                float saw = phase * 2f - 1f;

                // Growl: chop the voice rapidly so it sounds torn rather than sung.
                float growl = 0.55f + 0.45f * Mathf.Sin(t * 2f * Mathf.PI * 38f);

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowNoise += 0.25f * (noise - lowNoise);
                float source = saw * growl + lowNoise * 0.9f;

                // Formants drift open and closed a little for the shape of a shout.
                float f1 = 650f + 150f * Mathf.Sin(t * 5f);
                float f2 = 1100f + 250f * Mathf.Sin(t * 3f + 1f);
                float r = 0.985f;
                float c1 = 2f * r * Mathf.Cos(2f * Mathf.PI * f1 / sampleRate);
                float c2 = 2f * r * Mathf.Cos(2f * Mathf.PI * f2 / sampleRate);
                float ya = source * 0.05f + c1 * y1a - r * r * y2a;
                float yb = source * 0.05f + c2 * y1b - r * r * y2b;
                y2a = y1a; y1a = ya;
                y2b = y1b; y1b = yb;

                // Mix with some raw low voice for chest weight, then drive it into soft clipping for grit.
                float s = (ya + yb * 0.7f) * 2.5f + saw * 0.25f;
                s = (float)System.Math.Tanh(s * 1.8f);

                data[i] = s * env * 0.8f;
            }

            var c = AudioClip.Create("Roar", n, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
