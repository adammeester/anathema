using UnityEngine;

namespace Anathema.Combat
{
    // The player's master volume (0..1), set from the in-game menu and remembered between launches. Applied through
    // AudioListener.volume, which BackgroundMute also drives (an unfocused window stays silent whatever this is).
    public static class GameVolume
    {
        const string Pref = "Anathema.MasterVolume";
        static float master = -1f;

        public static float Master
        {
            get
            {
                if (master < 0f)
                {
                    master = 1f;
                    try { master = Mathf.Clamp01(PlayerPrefs.GetFloat(Pref, 1f)); } catch { }
                }
                return master;
            }
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(value, Master)) return;
                master = value;
                try { PlayerPrefs.SetFloat(Pref, value); } catch { }
                Apply();
            }
        }

        public static void Apply() => Apply(Application.isFocused);

        public static void Apply(bool focused) => AudioListener.volume = focused ? Master : 0f;

        public static void Save()
        {
            try { PlayerPrefs.Save(); } catch { }
        }
    }
}
