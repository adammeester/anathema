using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anathema.EditorTools
{
    // Quick ways to look at every arena. "Open" loads the scene in the editor (then Play to walk around it);
    // "Rebuild" regenerates the scenes from ArenaSceneBuilder / NetSceneBuilder so code changes show up.
    public static class ArenaTestMenu
    {
        [MenuItem("Anathema/Arenas/Open Training Arena (Arena)")]
        static void OpenTraining() => Open("Assets/Scenes/Arena.unity");

        [MenuItem("Anathema/Arenas/Open Online Arena - Colosseum (Versus)")]
        static void OpenColosseum() => Open("Assets/Scenes/Versus.unity");

        [MenuItem("Anathema/Arenas/Open Online Arena - Shrine (Versus_Shrine)")]
        static void OpenShrine() => Open("Assets/Scenes/Versus_Shrine.unity");

        [MenuItem("Anathema/Arenas/Rebuild All Arenas (Training + Online)")]
        static void RebuildAll()
        {
            ArenaSceneBuilder.Build();   // training Arena.unity
            NetSceneBuilder.BuildAll();  // Menu, Versus, Versus_Shrine + prefabs
        }

        static void Open(string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(path)) { Debug.LogWarning($"{path} not found - run Anathema > Arenas > Rebuild All Arenas."); return; }
            EditorSceneManager.OpenScene(path);
        }
    }
}
