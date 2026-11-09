using UnityEngine;

namespace Anathema.Combat
{
    // A small, generic "your shot landed" tick for projectile basic attacks. Synthesized in code like the other
    // sounds (no audio assets) and 2D, because the camera is far above the action.
    public static class HitMarkerSound
    {
        const int SampleRate = 44100;

        static AudioClip clip;

        public static void Play(float volume = 0.3f)
        {
            if (clip == null) clip = Build();

            var go = new GameObject("Hit Marker Sound");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.pitch = Random.Range(0.9f, 1.15f);
            source.Play();
            Object.Destroy(go, clip.length / source.pitch + 0.1f);
        }

        // A short dull hit, built from filtered noise and a low thump.
        static AudioClip Build()
        {
            // A dull organic thwack: a soft low thump, a muffled noise slap and a brief damp tail. No pitched tone.
            const float length = 0.14f;
            int n = (int)(SampleRate * length);
            var data = new float[n];
            var rng = new System.Random(7);
            float phase = 0f, slap = 0f, tail = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Thump: low sine falling fast, like a fist on a body.
                phase += Mathf.Lerp(150f, 60f, Mathf.Clamp01(t / 0.05f)) / SampleRate;
                float thump = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 35f);

                // Slap: noise heavily low-passed, so it is a muffled smack rather than a bright click.
                slap += 0.22f * (noise - slap);
                float smack = slap * Mathf.Exp(-t * 55f) * 2.2f;

                // Damp tail: even darker noise that dies away quickly.
                tail += 0.06f * (noise - tail);
                float body = tail * Mathf.Exp(-t * 28f) * 2.5f;

                data[i] = Mathf.Clamp(thump * 0.8f + smack + body, -1f, 1f);
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
