using UnityEngine;

namespace Anathema.Combat
{
    // Silences the game while its window is not focused, so two instances running on one PC (a local multiplayer test)
    // each only sound like their own player: lines meant for the owner alone, such as "ultimate ready", are not heard
    // from the other window. Volume is dropped rather than audio paused, so sounds keep their timing and simply play on
    // unheard.
    public static class BackgroundMute
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
            OnFocusChanged(Application.isFocused);
        }

        static void OnFocusChanged(bool focused) => AudioListener.volume = focused ? 1f : 0f;
    }
}
