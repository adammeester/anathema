using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Combat
{
    // Plays the recorded sound effects in Resources/Audio/Sfx by file name. Everything is 2D because the camera is
    // far above the action. One-shots get a little random pitch so repeats don't sound like a loop, can be cut short
    // with a fade (`maxSeconds`) when the recording is longer than the action, and the same clip is never started
    // twice within a few hundredths of a second (many targets hit on one frame would otherwise stack into a bang).
    // Loops fade in and out, and end by themselves when the object they were started for is destroyed.
    public static class Sfx
    {
        public const string AxeToss = "AxeToss";
        public const string ChainswordImpact = "ChainswordImpact";
        public const string ChainswordSwing = "ChainswordSwing";
        public const string ChainswordSwing2 = "ChainswordSwing2";
        public const string CounterHit = "CounterHit";
        public const string DiseaseCloud = "DiseaseCloud";
        public const string DiseaseCloud2 = "DiseaseCloud2";
        public const string DiseaseBurst = "DiseaseCloude";
        public const string ElectricBeam = "ElectricBeam";
        public const string ElectricShock = "ElectricShock";
        public const string Fissure = "Fissure";
        public const string Flamethrower = "Flamethrower";
        public const string Flies = "Flies";
        public const string GroundFire = "GroundFire";
        public const string HammerSmash = "HammerSmash";
        public const string HeavyBolterShot = "HeavyBolterShot";
        public const string HitMarker = "HitMarker";
        public const string Impact = "Impact";
        public const string Longrifle = "Longrifle";
        public const string MagicImpact = "MagicImpact";
        public const string MeleeImpact = "MeleeImpact";
        public const string MeleeMiss = "MeleeMiss";
        public const string MeleeSlash = "MeleeSlash";
        public const string MeleeSlash2 = "MeleeSlash2";
        public const string PlagueWeaponShot = "PlagueWeaponShot";
        public const string PsycannonShot = "PsycannonShot";
        public const string SlamExplosion = "SlamExplosion";
        public const string Stun = "Stun";
        public const string TeleportCrackle = "TeleportCrackle";

        const string Folder = "Audio/Sfx/";
        const float MinRepeatInterval = 0.04f;

        // The mix: a volume scale per recording, on top of whatever volume the caller asks for. This is the one place to
        // turn a whole kind of sound up or down. Anything not listed plays at 1.
        const float SwingLevel = 0.55f; // every melee swing
        const float ShotLevel = 0.55f;  // every gunshot
        const float ImpactLevel = 0.55f;      // blows and blasts landing
        const float ChainImpactLevel = 0.3f;  // chain teeth biting: the harshest recording, and it repeats the most
        static readonly Dictionary<string, float> mix = new Dictionary<string, float>
        {
            { ChainswordSwing, SwingLevel }, { ChainswordSwing2, SwingLevel },
            { MeleeSlash, SwingLevel }, { MeleeSlash2, SwingLevel }, { MeleeMiss, SwingLevel },
            { Longrifle, ShotLevel }, { HeavyBolterShot, ShotLevel }, { PsycannonShot, ShotLevel }, { PlagueWeaponShot, ShotLevel },
            { ChainswordImpact, ChainImpactLevel },
            { MeleeImpact, ImpactLevel }, { HammerSmash, ImpactLevel }, { Impact, ImpactLevel }, { MagicImpact, ImpactLevel },
        };

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        // One of two recordings of the same thing, picked at random.
        public static string Either(string a, string b) => Random.value < 0.5f ? a : b;

        // A one-shot. `maxSeconds` > 0 fades it out after that long instead of letting the whole recording play.
        public static void Play(string name, float volume = 0.5f, float pitchMin = 0.95f, float pitchMax = 1.05f, float maxSeconds = 0f)
        {
            AudioClip clip = Clip(name);
            if (clip == null) return;

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(name, out float last) && now - last < MinRepeatInterval) return;
            lastPlayed[name] = now;

            AudioSource source = NewSource(name, clip, volume, Random.Range(pitchMin, pitchMax), false);
            float length = clip.length / Mathf.Max(0.01f, source.pitch);
            var voice = source.gameObject.AddComponent<SfxVoice>();
            if (maxSeconds > 0f && maxSeconds < length) voice.FadeOutAt(maxSeconds, Mathf.Min(0.25f, maxSeconds * 0.4f));
            else Object.Destroy(source.gameObject, length + 0.1f);
        }

        // A looping sound that lasts as long as `anchor` exists (the beam, cloud or effect it belongs to): it fades in,
        // and fades out and removes itself once the anchor is destroyed. Stop() ends it sooner.
        public static AudioSource Loop(string name, Transform anchor, float volume = 0.4f, float pitch = 1f, float fadeIn = 0.15f)
        {
            AudioClip clip = Clip(name);
            if (clip == null || anchor == null) return null; // nothing to tie it to: it would never end

            AudioSource source = NewSource(name, clip, volume, pitch, true);
            source.gameObject.AddComponent<SfxVoice>().Follow(anchor, fadeIn);
            return source;
        }

        // A spoken line: played as recorded (no pitch change). Returns the source so it can be cut off with Stop, or null
        // when there is no such recording yet (lines are optional, so a missing one is silent rather than a warning).
        public static AudioSource Say(string name, float volume = 0.7f)
        {
            AudioClip clip = Clip(name, false);
            if (clip == null) return null;

            AudioSource source = NewSource(name, clip, volume, 1f, false);
            source.gameObject.AddComponent<SfxVoice>();
            Object.Destroy(source.gameObject, clip.length + 0.1f);
            return source;
        }

        public static void Stop(AudioSource source, float fade = 0.25f)
        {
            if (source == null) return;
            if (source.TryGetComponent(out SfxVoice voice)) voice.FadeOutAt(0f, fade);
            else Object.Destroy(source.gameObject);
        }

        static AudioSource NewSource(string name, AudioClip clip, float volume, float pitch, bool loop)
        {
            var go = new GameObject("Sfx " + name);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.volume = mix.TryGetValue(name, out float level) ? volume * level : volume;
            source.pitch = pitch;
            source.loop = loop;
            source.Play();
            return source;
        }

        static AudioClip Clip(string name, bool warnIfMissing = true)
        {
            if (clips.TryGetValue(name, out AudioClip clip)) return clip; // a missing clip is cached as null: warn once
            clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null && warnIfMissing) Debug.LogWarning($"Sfx: no clip at Resources/{Folder}{name}");
            clips[name] = clip;
            return clip;
        }
    }

    // Fades one playing sound in and out, and ends it when the object it belongs to is gone.
    public class SfxVoice : MonoBehaviour
    {
        AudioSource source;
        float fullVolume;
        float fadeInSeconds, startTime;
        Transform anchor;
        bool anchored;
        bool ending;
        float endStart, endSeconds;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            fullVolume = source.volume;
            startTime = Time.unscaledTime;
        }

        public void Follow(Transform owner, float fadeIn)
        {
            anchor = owner;
            anchored = owner != null;
            fadeInSeconds = fadeIn;
            if (fadeIn > 0f) source.volume = 0f;
        }

        // Start fading `delay` seconds from now, over `fade` seconds, then remove the sound.
        public void FadeOutAt(float delay, float fade)
        {
            if (ending && delay > 0f) return;
            ending = true;
            endStart = Time.unscaledTime + delay;
            endSeconds = Mathf.Max(0.01f, fade);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (anchored && anchor == null && !ending) FadeOutAt(0f, 0.3f);

            float level = fadeInSeconds > 0f ? Mathf.Clamp01((now - startTime) / fadeInSeconds) : 1f;
            if (ending && now >= endStart)
            {
                float t = (now - endStart) / endSeconds;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                level *= 1f - t;
            }
            source.volume = fullVolume * level;
        }
    }
}
