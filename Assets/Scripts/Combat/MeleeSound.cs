namespace Anathema.Combat
{
    // The sounds every melee attack shares, played when the swing happens. Blades cut (or whoosh through empty air
    // when the swing connects with nobody) and thud into whoever they hit; chain weapons rev through the swing and
    // grind into flesh. Only the attacker's own machine knows whether a swing connected, so other machines hear the cut.
    public static class MeleeSound
    {
        // A blade swing: the cut if it connected, the whoosh of a miss if not.
        public static void Swing(bool connected, float volume = 0.5f, float pitchMin = 0.92f, float pitchMax = 1.08f)
        {
            if (connected) Slash(volume, pitchMin, pitchMax);
            else Sfx.Play(Sfx.MeleeMiss, volume * 0.9f, pitchMin, pitchMax);
        }

        public static void Slash(float volume = 0.5f, float pitchMin = 0.92f, float pitchMax = 1.08f)
        {
            Sfx.Play(Sfx.Either(Sfx.MeleeSlash, Sfx.MeleeSlash2), volume, pitchMin, pitchMax);
        }

        // The blow landing on `hits` targets: a little louder with more of them.
        public static void Impact(int hits = 1)
        {
            if (hits <= 0) return;
            Sfx.Play(Sfx.MeleeImpact, UnityEngine.Mathf.Min(0.3f + 0.06f * hits, 0.5f), 0.9f, 1.1f);
        }

        // A chainaxe or eviscerator swing. `maxSeconds` cuts the rev short for fast, repeated swings.
        public static void ChainSwing(float volume = 0.45f, float pitchMin = 0.94f, float pitchMax = 1.06f, float maxSeconds = 0.9f)
        {
            Sfx.Play(Sfx.Either(Sfx.ChainswordSwing, Sfx.ChainswordSwing2), volume, pitchMin, pitchMax, maxSeconds);
        }

        // Chain teeth biting into a target.
        public static void ChainImpact(float volume = 0.4f)
        {
            Sfx.Play(Sfx.ChainswordImpact, volume, 0.92f, 1.08f, 0.8f);
        }
    }
}
