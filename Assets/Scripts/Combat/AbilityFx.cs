using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Combat
{
    // Shared feedback for two moments the player must notice: a counter landing and a teleport. Line-renderer visuals
    // (rings, light pillars, sparks, streaks) and sounds synthesized in code (no assets), 2D because the camera is far away.
    public static class AbilityFx
    {
        const int SampleRate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        // ---- Public cues ------------------------------------------------------------------------------------------

        // A hit was negated: bright flash, shockwave rings, a burst of sparks and a ringing clang.
        public static void CounterTriggered(Vector3 position, float radius)
        {
            Color gold = new Color(1f, 0.93f, 0.55f, 1f);
            Color white = new Color(1f, 1f, 1f, 1f);
            float chest = 1.0f;

            Ring(position, 0.3f, radius * 1.6f, 0.35f, white, 0.35f, 0.15f);
            Ring(position, 0.2f, radius * 2.3f, 0.5f, gold, 0.22f, 0.1f);
            Beam(position, 3.2f, 0.9f, 0.3f, white);

            const int rays = 14;
            for (int i = 0; i < rays; i++)
            {
                float a = (i / (float)rays + UnityEngine.Random.Range(-0.02f, 0.02f)) * TwoPi;
                Vector3 dir = new Vector3(Mathf.Cos(a), UnityEngine.Random.Range(-0.1f, 0.35f), Mathf.Sin(a)).normalized;
                Ray(position + Vector3.up * chest, dir, radius * 0.5f, radius * UnityEngine.Random.Range(1.6f, 2.4f), 0.28f, i % 2 == 0 ? white : gold);
            }

            Play(Get("counter", BuildCounter), 0.65f, 0.97f, 1.03f);
        }

        // A teleport from `from` to `to`: the origin collapses in a flash, a streak of energy links the two points, and the
        // arrival erupts in a pillar of light. Replicas call this too so both players see and hear it.
        public static void Teleport(Vector3 from, Vector3 to, Color color)
        {
            Color white = new Color(1f, 1f, 1f, 1f);
            Color glow = color;
            glow.a = 1f;

            // Departure: a ring that implodes plus a tall pillar.
            Ring(from, 2.2f, 0.1f, 0.3f, glow, 0.3f, 0.12f);
            Beam(from, 4.5f, 1.1f, 0.4f, glow);
            Sparks(from, 10, 0.5f, 2.2f, 0.4f, glow);

            // Link between the two points.
            Streak(from + Vector3.up, to + Vector3.up, 0.35f, white, glow);

            // Arrival: bigger, brighter, with the shockwave rolling outward.
            Ring(to, 0.2f, 3.2f, 0.5f, white, 0.4f, 0.12f);
            Ring(to, 0.2f, 2.2f, 0.65f, glow, 0.25f, 0.1f);
            Beam(to, 6f, 1.5f, 0.55f, glow);
            Beam(to, 6f, 0.5f, 0.35f, white);
            Sparks(to, 16, 0.6f, 3.0f, 0.55f, glow);

            Play(Get("teleport", BuildTeleport), 0.7f, 0.97f, 1.03f);
        }

        // ---- Visual building blocks -------------------------------------------------------------------------------

        // Drives one LineRenderer for `duration` seconds, then cleans up. `tick` gets the 0..1 progress.
        sealed class Timed : MonoBehaviour
        {
            public float Duration;
            public Action<float> Tick;
            public Material Material;
            float start;

            void Awake() { start = Time.time; }

            void Update()
            {
                float t = (Time.time - start) / Duration;
                if (t >= 1f) { Destroy(gameObject); return; }
                Tick(t);
            }

            void OnDestroy() { if (Material != null) Destroy(Material); }
        }

        static LineRenderer NewLine(string name, float duration, Color color, int points, bool loop, Action<float, LineRenderer, Material> tick)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points;
            line.loop = loop;
            line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material material = VisualUtil.UnlitMaterial(color, 3010);
            line.sharedMaterial = material;

            var timed = go.AddComponent<Timed>();
            timed.Duration = Mathf.Max(0.05f, duration);
            timed.Material = material;
            timed.Tick = t => tick(t, line, material);
            return line;
        }

        static void Fade(Material material, Color color, float alpha)
        {
            color.a = color.a * Mathf.Clamp01(alpha);
            material.color = color;
        }

        // A thick flat ring that expands (or, if to < from, contracts) while fading.
        static void Ring(Vector3 center, float from, float to, float duration, Color color, float width, float height)
        {
            const int points = 48;
            Vector3 c = center + Vector3.up * height;
            NewLine("Fx Ring", duration, color, points, true, (t, line, mat) =>
            {
                float eased = 1f - (1f - t) * (1f - t);
                float r = Mathf.Lerp(from, to, eased);
                for (int i = 0; i < points; i++)
                {
                    float a = i / (float)points * TwoPi;
                    line.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }
                line.startWidth = line.endWidth = width * (1f - t * 0.7f);
                Fade(mat, color, 1f - t * t);
            });
        }

        // A vertical column of light that flares then narrows to nothing.
        static void Beam(Vector3 position, float height, float width, float duration, Color color)
        {
            NewLine("Fx Beam", duration, color, 2, false, (t, line, mat) =>
            {
                float rise = Mathf.Clamp01(t * 5f); // shoots up quickly
                line.SetPosition(0, position);
                line.SetPosition(1, position + Vector3.up * height * rise);
                line.startWidth = width * (1f - t);
                line.endWidth = width * 0.25f * (1f - t);
                Fade(mat, color, 1f - t);
            });
        }

        // A single spark flying outward from `origin`.
        static void Ray(Vector3 origin, Vector3 dir, float from, float to, float duration, Color color)
        {
            NewLine("Fx Spark", duration, color, 2, false, (t, line, mat) =>
            {
                float head = Mathf.Lerp(from, to, 1f - (1f - t) * (1f - t));
                float tail = Mathf.Lerp(from, to, Mathf.Max(0f, t - 0.25f) / 0.75f * t);
                line.SetPosition(0, origin + dir * tail);
                line.SetPosition(1, origin + dir * head);
                line.startWidth = 0.12f * (1f - t);
                line.endWidth = 0.02f;
                Fade(mat, color, 1f - t);
            });
        }

        // Sparks flying up and out from a ground point.
        static void Sparks(Vector3 position, int count, float minSpeed, float maxSpeed, float duration, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.value * TwoPi;
                Vector3 dir = new Vector3(Mathf.Cos(a) * 0.6f, UnityEngine.Random.Range(0.5f, 1.4f), Mathf.Sin(a) * 0.6f).normalized;
                Ray(position + Vector3.up * 0.2f, dir, minSpeed, maxSpeed * UnityEngine.Random.Range(0.6f, 1.2f) * 1.5f, duration, color);
            }
        }

        // A jagged bolt between two points: a wide coloured glow with a white core, re-jittered every frame.
        static void Streak(Vector3 a, Vector3 b, float duration, Color core, Color glow)
        {
            const int points = 10;
            for (int pass = 0; pass < 2; pass++)
            {
                bool isCore = pass == 1;
                Color color = isCore ? core : glow;
                float width = isCore ? 0.12f : 0.5f;
                NewLine("Fx Streak", duration, color, points, false, (t, line, mat) =>
                {
                    for (int i = 0; i < points; i++)
                    {
                        float f = i / (float)(points - 1);
                        Vector3 p = Vector3.Lerp(a, b, f);
                        if (i > 0 && i < points - 1) p += UnityEngine.Random.insideUnitSphere * 0.35f;
                        line.SetPosition(i, p);
                    }
                    line.startWidth = line.endWidth = width * (1f - t);
                    Fade(mat, color, 1f - t);
                });
            }
        }

        // ---- Sound ------------------------------------------------------------------------------------------------

        static AudioSource Play(AudioClip clip, float volume, float pitchMin, float pitchMax)
        {
            var go = new GameObject("Ability Fx Sound");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
            source.Play();
            UnityEngine.Object.Destroy(go, clip.length / source.pitch + 0.2f);
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

        static AudioClip Make(string name, float length, Func<float, float> sample)
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

            float gain = 0.9f / peak;
            for (int i = 0; i < n; i++) data[i] *= gain;

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

        static float Env(float t, float attack, float decay)
        {
            return Mathf.Clamp01(t / attack) * Mathf.Exp(-t * decay);
        }

        // Counter: a hard metallic clang (inharmonic partials that ring out), a sharp noise crack and a low thud.
        static AudioClip BuildCounter()
        {
            var rng = new System.Random(201);
            float low = 0f, thudPhase = 0f;
            float[] freq = { 640f, 1030f, 1590f, 2310f, 3170f };
            float[] amp = { 1f, 0.75f, 0.55f, 0.35f, 0.2f };
            float[] decay = { 5f, 7f, 9f, 12f, 16f };
            return Make("FxCounter", 0.8f, t =>
            {
                float ring = 0f;
                for (int i = 0; i < freq.Length; i++)
                    ring += Mathf.Sin(TwoPi * freq[i] * t) * amp[i] * Env(t, 0.001f, decay[i]);

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.3f * (white - low);
                float crack = (white - low) * Env(t, 0.0005f, 70f) * 1.2f;

                thudPhase += (55f + 70f * Mathf.Exp(-t * 35f)) / SampleRate;
                float thud = Mathf.Sin(TwoPi * thudPhase) * Env(t, 0.002f, 14f) * 1.2f;

                return ring * 0.55f + crack + thud;
            });
        }

        // Teleport: a rising charged whoosh, a hard pop at the instant of arrival, then a shimmering sparkle tail.
        static AudioClip BuildTeleport()
        {
            var rng = new System.Random(202);
            float low = 0f, sweepPhase = 0f, popPhase = 0f;
            const float arrive = 0.28f;
            float[] shimmer = { 1320f, 1980f, 2640f, 3520f };
            return Make("FxTeleport", 1.0f, t =>
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s = 0f;

                if (t < arrive)
                {
                    float p = t / arrive;
                    float rise = p * p;
                    low += Mathf.Lerp(0.03f, 0.4f, p) * (white - low);
                    sweepPhase += Mathf.Lerp(200f, 1500f, rise) / SampleRate;
                    s += low * rise * 2.2f + Mathf.Sin(TwoPi * sweepPhase) * rise * 0.5f;
                }

                float a = t - arrive;
                if (a >= 0f)
                {
                    popPhase += (45f + 80f * Mathf.Exp(-a * 30f)) / SampleRate;
                    s += Mathf.Sin(TwoPi * popPhase) * Env(a, 0.002f, 10f) * 1.5f;
                    s += (white - low) * Env(a, 0.0008f, 40f) * 1.1f;

                    float tail = 0f;
                    for (int i = 0; i < shimmer.Length; i++)
                        tail += Mathf.Sin(TwoPi * shimmer[i] * a) * (0.6f + 0.4f * Mathf.Sin(TwoPi * (9f + i * 3f) * a)) / (i + 1f);
                    s += tail * Env(a, 0.01f, 4.5f) * 0.45f;
                }
                return s;
            });
        }
    }
}
