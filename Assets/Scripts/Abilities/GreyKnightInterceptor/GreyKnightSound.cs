using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Every Grey Knight sound, synthesized in code like the roar and blood impacts (no audio assets). Armour and steel
    // are dull thumps and a faint ring; psychic power is shimmering sine partials and crackle; flame is low rushing noise.
    // All cues are 2D because the camera is far above the action.
    public static class GreyKnightSound
    {
        const int SampleRate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        // ---- Cues -------------------------------------------------------------------------------------------------

        // A halberd cutting the air; the empowered swing is lower and carries an electric surge.
        public static void Swing(bool empowered)
        {
            Play(Get("swing", BuildSwing), empowered ? 0.4f : 0.32f, empowered ? 0.8f : 0.95f, empowered ? 0.9f : 1.1f);
            if (empowered) Play(Get("shimmer", BuildShimmer), 0.2f, 1.1f, 1.3f);
        }

        // The halberd taking a psychic charge.
        public static void Empower()
        {
            Play(Get("shimmer", BuildShimmer), 0.3f, 0.95f, 1.05f);
        }

        // A subtle weighty hit: dull thump, short electric snap. Louder with more targets, but never harsh.
        public static void Impact(int hits = 1)
        {
            if (hits <= 0) return;
            Play(Get("impact", BuildImpact), Mathf.Min(0.18f + 0.06f * hits, 0.38f), 0.85f, 1.15f);
        }

        // Psychic strike on a target: a thump with a crackle on top.
        public static void PsychicImpact(int hits = 1)
        {
            if (hits <= 0) return;
            Play(Get("impact", BuildImpact), Mathf.Min(0.16f + 0.05f * hits, 0.32f), 0.7f, 0.9f);
            Play(Get("crackle", BuildCrackle), Mathf.Min(0.18f + 0.04f * hits, 0.32f), 0.9f, 1.2f);
        }

        // The focus before Smite: a low buzzing hum with snapping arcs. Returns the source so a cancel can cut it.
        public static AudioSource SmiteCharge()
        {
            return Play(Get("smiteCharge", BuildSmiteCharge), 0.3f, 0.98f, 1.02f);
        }

        public static void SmiteFire(float volume = 0.5f)
        {
            Play(Get("smiteFire", BuildSmiteFire), volume, 0.95f, 1.05f);
        }

        // The sigil igniting: a low rumbling flame with soft pops.
        public static AudioSource ConsecrateArm()
        {
            return Play(Get("consecrateArm", BuildConsecrateArm), 0.3f, 1f, 1f);
        }

        // The ground erupting: a heavy whoomph and a roar of flame.
        public static void ConsecrateBoom(float volume = 0.55f)
        {
            Play(Get("consecrateBoom", BuildConsecrateBoom), volume, 0.95f, 1.05f);
        }

        // Gathering flame during the Purgatus channel; the caller raises the pitch as the charge builds.
        public static AudioSource FireChannel()
        {
            AudioSource s = Play(Get("fireChannel", BuildFireChannel), 0.3f, 0.8f, 0.8f, loop: true);
            return s;
        }

        // The released ring of psychic fire.
        public static void FireRelease()
        {
            Play(Get("fireRelease", BuildFireRelease), 0.6f, 0.95f, 1.05f);
        }

        // A violent electric snap.
        public static void Parry()
        {
            Play(Get("parry", BuildParry), 0.4f, 0.95f, 1.05f);
        }

        // A crackling blink.
        public static void Blink()
        {
            Play(Get("blink", BuildBlink), 0.4f, 0.95f, 1.05f);
        }

        public static void Stop(AudioSource source)
        {
            if (source != null) UnityEngine.Object.Destroy(source.gameObject);
        }

        // ---- Playback ---------------------------------------------------------------------------------------------

        static AudioSource Play(AudioClip clip, float volume, float pitchMin, float pitchMax, bool loop = false)
        {
            var go = new GameObject("Grey Knight Sound");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
            source.loop = loop;
            source.Play();
            if (!loop) UnityEngine.Object.Destroy(go, clip.length / source.pitch + 0.2f);
            return source;
        }

        static AudioClip Get(string key, Func<AudioClip> build)
        {
            if (!clips.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                clip = build();
                clips[key] = clip;
            }
            return clip;
        }

        // ---- Synthesis helpers ------------------------------------------------------------------------------------

        static AudioClip Make(string name, float length, Func<float, float> sample, bool normalise = true)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float s = sample((float)i / SampleRate);
                data[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            if (normalise)
            {
                float gain = 0.9f / peak;
                for (int i = 0; i < n; i++) data[i] *= gain;
            }

            // Short fades so nothing clicks at the ends.
            int fade = Mathf.Min(n / 2, 200);
            for (int i = 0; i < fade; i++)
            {
                float f = i / (float)fade;
                data[i] *= f;
                data[n - 1 - i] *= f;
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float phase) { return Mathf.Sin(phase * TwoPi); }

        // Fast attack, exponential decay.
        static float Env(float t, float attack, float decay)
        {
            return Mathf.Clamp01(t / attack) * Mathf.Exp(-t * decay);
        }

        // A band-ish noise source: white noise through a one-pole low-pass (use Highpass to remove the lows).
        sealed class Noise
        {
            readonly System.Random rng;
            float low;
            public Noise(int seed) { rng = new System.Random(seed); }
            public float White() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
            public float Lowpass(float coefficient)
            {
                low += coefficient * (White() - low);
                return low;
            }
            public float Highpass(float coefficient)
            {
                float w = White();
                low += coefficient * (w - low);
                return w - low;
            }
        }

        // ---- Clips ------------------------------------------------------------------------------------------------

        // Psychic whoosh: air rushing past with an electric buzz riding it and a few crackles as the blade passes.
        static AudioClip BuildSwing()
        {
            var noise = new Noise(11);
            var gate = new System.Random(12);
            float phase = 0f, spark = 0f;
            return Make("GKSwing", 0.4f, t =>
            {
                float p = Mathf.Clamp01(t / 0.4f);
                float shape = Mathf.Sin(p * Mathf.PI);
                shape *= shape;
                float air = noise.Lowpass(0.05f + 0.35f * shape) * shape * 1.6f;
                phase += (70f + 90f * shape) / SampleRate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float buzz = saw * (0.6f + 0.4f * Mathf.Sin(t * TwoPi * 90f)) * shape * 0.25f;
                if (gate.NextDouble() < 0.003 * shape) spark = 1f;
                spark *= 0.997f;
                return air + buzz + noise.Highpass(0.06f) * spark * 0.5f;
            });
        }

        // Psychic surge: a pulsing electric hum that swells and fades, with arcing snaps.
        static AudioClip BuildShimmer()
        {
            var noise = new Noise(15);
            var gate = new System.Random(16);
            float phase = 0f, spark = 0f;
            return Make("GKShimmer", 0.7f, t =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.7f) * Mathf.PI);
                phase += (60f + 60f * env) / SampleRate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float hum = saw * (0.5f + 0.5f * Mathf.Sin(t * TwoPi * 24f));
                if (gate.NextDouble() < 0.004 * env) spark = 1f;
                spark *= 0.997f;
                return (hum * 0.5f + noise.Lowpass(0.04f) * 0.6f + noise.Highpass(0.06f) * spark * 0.7f) * env;
            });
        }

        // Armoured thump with a short electric snap.
        static AudioClip BuildImpact()
        {
            var noise = new Noise(23);
            float phase = 0f;
            return Make("GKImpact", 0.35f, t =>
            {
                float freq = 45f + 70f * Mathf.Exp(-t * 40f);
                phase += freq / SampleRate;
                float thump = Sine(phase) * Env(t, 0.002f, 16f);
                float click = noise.Lowpass(0.35f) * Env(t, 0.001f, 70f) * 0.9f;
                float snap = noise.Highpass(0.08f) * Env(t, 0.001f, 40f) * 0.4f;
                return thump + click + snap;
            });
        }

        // Electrical crackle: sparse snaps of mid-range noise (no thin hiss).
        static AudioClip BuildCrackle()
        {
            var noise = new Noise(31);
            var gate = new System.Random(32);
            float gateLevel = 0f;
            return Make("GKCrackle", 0.3f, t =>
            {
                if (gate.NextDouble() < 0.0025) gateLevel = 1f;
                gateLevel *= 0.9985f;
                return noise.Highpass(0.06f) * gateLevel * Mathf.Clamp01((0.3f - t) / 0.2f);
            });
        }

        // Building electricity: a low buzzing hum that grows rougher, with snapping arcs. Nothing high or whiny.
        static AudioClip BuildSmiteCharge()
        {
            var noise = new Noise(41);
            var gate = new System.Random(42);
            float phase = 0f;
            float spark = 0f;
            return Make("GKSmiteCharge", 0.4f, t =>
            {
                float p = Mathf.Clamp01(t / 0.35f);
                phase += Mathf.Lerp(55f, 110f, p) / SampleRate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float buzz = saw * (0.7f + 0.3f * Mathf.Sin(t * TwoPi * 100f));
                if (gate.NextDouble() < 0.0008 + 0.005 * p) spark = 1f;
                spark *= 0.997f;
                float snap = noise.Highpass(0.05f) * spark;
                float s = buzz * 0.5f + snap * 0.8f + noise.Lowpass(0.03f) * 0.5f * p;
                return s * (0.3f + 0.7f * p) * Mathf.Clamp01((0.4f - t) / 0.05f);
            });
        }

        // Lightning: a sharp crack, a harsh low buzz of arcing current, rolling thunder and a crackling tail.
        static AudioClip BuildSmiteFire()
        {
            var noise = new Noise(51);
            var gate = new System.Random(52);
            float buzzPhase = 0f, boomPhase = 0f, spark = 0f;
            return Make("GKSmiteFire", 1.0f, t =>
            {
                float crack = noise.Lowpass(0.5f) * Env(t, 0.0005f, 45f) * 1.4f;
                buzzPhase += (130f * Mathf.Exp(-t * 3f) + 70f) / SampleRate;
                float saw = (buzzPhase - Mathf.Floor(buzzPhase)) * 2f - 1f;
                float buzz = saw * (0.6f + 0.4f * Mathf.Sin(t * TwoPi * 120f)) * Env(t, 0.001f, 7f);
                boomPhase += (36f + 40f * Mathf.Exp(-t * 10f)) / SampleRate;
                float boom = Sine(boomPhase) * Env(t, 0.004f, 4f) * 1.1f;
                float thunder = noise.Lowpass(0.025f) * Env(t, 0.03f, 2.6f) * 3.5f;
                if (gate.NextDouble() < 0.0035 * Mathf.Exp(-t * 2.5f)) spark = 1f;
                spark *= 0.9985f;
                float tail = noise.Highpass(0.06f) * spark * 0.7f;
                return crack + buzz * 0.5f + boom + thunder + tail;
            });
        }

        // A flame catching: a low rumble swelling under soft pops and crackles.
        static AudioClip BuildConsecrateArm()
        {
            var noise = new Noise(55);
            var gate = new System.Random(56);
            float pop = 0f;
            return Make("GKConsecrateArm", 0.8f, t =>
            {
                float p = Mathf.Clamp01(t / 0.8f);
                float env = Mathf.Sin(p * Mathf.PI * 0.5f) * Mathf.Clamp01((0.8f - t) / 0.2f);
                if (gate.NextDouble() < 0.0006 + 0.002 * p) pop = 1f;
                pop *= 0.998f;
                float rumble = noise.Lowpass(0.035f) * 3f * env;
                float crackle = noise.Highpass(0.1f) * pop * 0.5f;
                return rumble + crackle;
            });
        }

        // A fireball: a heavy whoomph, a rush of roaring flame that dies away, and embers crackling.
        static AudioClip BuildConsecrateBoom()
        {
            var noise = new Noise(61);
            var gate = new System.Random(62);
            float boomPhase = 0f, ember = 0f;
            return Make("GKConsecrateBoom", 1.5f, t =>
            {
                boomPhase += (32f + 50f * Mathf.Exp(-t * 8f)) / SampleRate;
                float boom = Sine(boomPhase) * Env(t, 0.008f, 3.2f) * 1.2f;
                float swell = Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI * 0.5f) * Mathf.Exp(-Mathf.Max(0f, t - 0.2f) * 2.2f);
                float roar = noise.Lowpass(Mathf.Lerp(0.2f, 0.04f, Mathf.Clamp01(t / 1.0f))) * swell * 2.8f;
                if (gate.NextDouble() < 0.002) ember = 1f;
                ember *= 0.9985f;
                float crackle = noise.Highpass(0.1f) * ember * 0.5f * Mathf.Clamp01((1.5f - t) / 0.8f);
                return boom + roar + crackle;
            });
        }

        // Seamless loop of low flame roar with soft pops, rising slightly in the middle.
        static AudioClip BuildFireChannel()
        {
            const float length = 1.2f;
            const float overlap = 0.15f;
            int n = (int)(SampleRate * length);
            int m = (int)(SampleRate * overlap);
            var noise = new Noise(71);
            var gate = new System.Random(72);
            float pop = 0f;
            var raw = new float[n + m];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / SampleRate;
                if (gate.NextDouble() < 0.0008) pop = 1f;
                pop *= 0.9985f;
                float rumble = noise.Lowpass(0.04f) * (0.8f + 0.2f * Mathf.Sin(t * TwoPi * 3f));
                float hiss = noise.Highpass(0.3f) * 0.08f;
                raw[i] = rumble * 3.0f + hiss + noise.White() * pop * 0.25f;
            }

            // Blend the tail over the head so the loop joins without a click.
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            for (int i = 0; i < m; i++)
            {
                float f = i / (float)m;
                data[i] = raw[i] * f + raw[n + i] * (1f - f);
            }

            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;

            var clip = AudioClip.Create("GKFireChannel", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // The ring bursts outward: a heavy whump, then a roaring rush that fades with crackling embers.
        static AudioClip BuildFireRelease()
        {
            var noise = new Noise(81);
            var gate = new System.Random(82);
            float boomPhase = 0f, ember = 0f;
            return Make("GKFireRelease", 1.6f, t =>
            {
                boomPhase += (30f + 45f * Mathf.Exp(-t * 9f)) / SampleRate;
                float whump = Sine(boomPhase) * Env(t, 0.01f, 3.5f) * 1.1f;
                float p = Mathf.Clamp01(t / 1.6f);
                float swell = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI * 0.5f) * Mathf.Exp(-Mathf.Max(0f, t - 0.3f) * 2.2f);
                float roar = noise.Lowpass(Mathf.Lerp(0.25f, 0.04f, p)) * swell * 2.6f;
                if (gate.NextDouble() < 0.002) ember = 1f;
                ember *= 0.9985f;
                float crackle = noise.Highpass(0.25f) * ember * 0.45f * Mathf.Clamp01((1.6f - t) / 0.8f);
                return whump + roar + crackle;
            });
        }

        // Counter: a violent electric snap, a short burst of arcing buzz and a punchy thud.
        static AudioClip BuildParry()
        {
            var noise = new Noise(91);
            var gate = new System.Random(92);
            float phase = 0f, thudPhase = 0f, spark = 0f;
            return Make("GKParry", 0.6f, t =>
            {
                float snap = noise.Lowpass(0.45f) * Env(t, 0.0005f, 80f) * 1.3f;
                phase += (160f * Mathf.Exp(-t * 6f) + 80f) / SampleRate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float buzz = saw * (0.6f + 0.4f * Mathf.Sin(t * TwoPi * 110f)) * Env(t, 0.001f, 9f);
                thudPhase += (50f + 60f * Mathf.Exp(-t * 30f)) / SampleRate;
                float thud = Sine(thudPhase) * Env(t, 0.002f, 12f);
                if (gate.NextDouble() < 0.004 * Mathf.Exp(-t * 5f)) spark = 1f;
                spark *= 0.998f;
                return snap + buzz * 0.5f + thud * 0.8f + noise.Highpass(0.06f) * spark * 0.6f;
            });
        }

        // Blink: a rising electric whine-free surge of crackling static, then a thump as the knight lands.
        static AudioClip BuildBlink()
        {
            var noise = new Noise(101);
            var gate = new System.Random(102);
            float phase = 0f, spark = 0f;
            return Make("GKBlink", 0.55f, t =>
            {
                float p = Mathf.Clamp01(t / 0.35f);
                float surge = t < 0.35f ? Mathf.Sin(p * Mathf.PI * 0.5f) : 0f;
                phase += (50f + 90f * p) / SampleRate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float buzz = saw * (0.6f + 0.4f * Mathf.Sin(t * TwoPi * 60f)) * surge * 0.4f;
                if (gate.NextDouble() < 0.006 * surge) spark = 1f;
                spark *= 0.997f;
                float air = noise.Lowpass(0.08f) * surge * 0.8f;
                float arrive = t - 0.35f;
                float pop = arrive > 0f ? (Sine(70f * arrive) * Env(arrive, 0.002f, 20f) + noise.Highpass(0.07f) * Env(arrive, 0.001f, 50f) * 0.7f) : 0f;
                return buzz + air + noise.Highpass(0.06f) * spark * 0.6f + pop;
            });
        }
    }
}
