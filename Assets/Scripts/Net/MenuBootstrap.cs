using UnityEngine;

namespace Anathema.Net
{
    // Sits in the Menu scene: makes sure the online session object exists and the cursor is free for the menus.
    public class MenuBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Application.runInBackground = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            NetGame.EnsureExists();
        }
    }
}
