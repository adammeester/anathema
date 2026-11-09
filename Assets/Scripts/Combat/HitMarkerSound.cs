using UnityEngine;

namespace Anathema.Combat
{
    // A small, generic "your shot landed" tick for projectile basic attacks. Synthesized in code like the other
    // sounds (no audio assets) and 2D, because the camera is far above the action.
    public static class HitMarkerSound
    {
        const int SampleRate = 44100;

        static AudioClip clip;

        public static void Play(float volume = 0.6f)
        {
            if (clip == null) clip = Build();

            var go = new GameObject("Hit Marker Sound");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.pitch = Random.Range(0.95f, 1.1f);
            source.Play();
            Object.Destroy(go, clip.length / source.pitch + 0.1f);
        }

        // A short bright tick: a quick pitched blip over a tiny noise click.
        static AudioClip Build()
        {
            const float length = 0.16f;
            int n = (int)(SampleRate * length);
            var data = new float[n];
            var rng = new System.Random(7);
            float phase = 0f, body = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                phase += (1500f + 700f * Mathf.Exp(-t * 40f)) / SampleRate;
                float tone = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 22f);
                body += (260f + 200f * Mathf.Exp(-t * 50f)) / SampleRate;
                float thump = Mathf.Sin(body * Mathf.PI * 2f) * Mathf.Exp(-t * 30f) * 0.8f;
                float click = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 200f) * 0.5f;
                data[i] = Mathf.Clamp(tone * 0.7f + thump + click, -1f, 1f);
            }

            // Short fade-out so the end never clicks.
            int fade = Mathf.Min(n / 2, 100);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;

            var c = AudioClip.Create("HitMarker", n, 1, SampleRate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
