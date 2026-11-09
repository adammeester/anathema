using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Plague Surgeon sounds, synthesized in code like the other abilities (no audio assets). 2D because the camera is
    // far above the action.
    public static class PlagueSurgeonSound
    {
        const int SampleRate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        static AudioClip buzzClip;

        // A swarm of flies buzzing, played when a fly latches onto someone.
        public static void FlyBuzz()
        {
            if (buzzClip == null) buzzClip = BuildBuzz();

            var go = new GameObject("Fly Buzz Sound");
            var source = go.AddComponent<AudioSource>();
            source.clip = buzzClip;
            source.spatialBlend = 0f;
            source.volume = 0.35f;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.Play();
            Object.Destroy(go, buzzClip.length / source.pitch + 0.2f);
        }

        // Several flies at slightly different pitches, each with fast wing-beat tremolo and a wandering pitch, plus a
        // little high noise for the papery wings. Swells in, hangs, fades out.
        static AudioClip BuildBuzz()
        {
            const float length = 1.4f;
            const int flies = 4;
            int n = (int)(SampleRate * length);
            var data = new float[n];
            var rng = new System.Random(31);

            var phase = new float[flies];
            var baseHz = new float[flies];
            var wobbleHz = new float[flies];
            var wobbleOffset = new float[flies];
            var tremHz = new float[flies];
            for (int f = 0; f < flies; f++)
            {
                baseHz[f] = 150f + 35f * f + (float)rng.NextDouble() * 20f;
                wobbleHz[f] = 2f + (float)rng.NextDouble() * 4f;
                wobbleOffset[f] = (float)rng.NextDouble() * TwoPi;
                tremHz[f] = 28f + (float)rng.NextDouble() * 14f;
            }

            float low = 0f, peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float p = t / length;
                float env = Mathf.Clamp01(p / 0.12f) * Mathf.Clamp01((1f - p) / 0.35f);

                float sum = 0f;
                for (int f = 0; f < flies; f++)
                {
                    float hz = baseHz[f] * (1f + 0.08f * Mathf.Sin(t * TwoPi * wobbleHz[f] + wobbleOffset[f]));
                    phase[f] += hz / SampleRate;
                    float saw = (phase[f] - Mathf.Floor(phase[f])) * 2f - 1f;
                    float tremolo = 0.55f + 0.45f * Mathf.Sin(t * TwoPi * tremHz[f] + wobbleOffset[f]);
                    // Each fly drifts in and out of earshot.
                    float drift = 0.6f + 0.4f * Mathf.Sin(t * TwoPi * (0.9f + 0.4f * f) + wobbleOffset[f]);
                    sum += saw * tremolo * drift;
                }

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.15f * (white - low);
                float s = (sum / flies + (white - low) * 0.12f) * env;

                data[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            float gain = 0.9f / peak;
            for (int i = 0; i < n; i++) data[i] *= gain;

            var clip = AudioClip.Create("FlyBuzz", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
