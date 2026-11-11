namespace Anathema.Combat
{
    // The "your shot landed" tick for ranged attacks and abilities that connect (Resources/Audio/Sfx/HitMarker).
    // Only the attacker's machine plays it.
    public static class HitMarkerSound
    {
        public static void Play(float volume = 0.4f)
        {
            Sfx.Play(Sfx.HitMarker, volume, 0.95f, 1.08f);
        }
    }
}
