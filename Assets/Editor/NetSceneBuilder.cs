using System.IO;
using System.Reflection;
using Anathema.Arena;
using Anathema.CameraRig;
using Anathema.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anathema.EditorTools
{
    // Builds everything the online game needs: the two networked player prefabs (Resources/Net), the Menu scene and
    // the Versus scene (the online arena), then registers the scenes in the build settings.
    // Runs once automatically if any of it is missing; re-run from Anathema > Rebuild Online Setup.
    // The offline training scene (Arena.unity) is left alone.
    [InitializeOnLoad]
    public static class NetSceneBuilder
    {
        const string MenuPath = "Assets/Scenes/Menu.unity";
        const string VersusPath = "Assets/Scenes/Versus.unity";
        const string TrainingPath = "Assets/Scenes/Arena.unity";
        const string PrefabDir = "Assets/Resources/Net";
        const string GreyKnightPrefab = PrefabDir + "/Player_GreyKnight.prefab";
        const string BerserkerPrefab = PrefabDir + "/Player_KhorneBerserker.prefab";
        const string PlagueSurgeonPrefab = PrefabDir + "/Player_PlagueSurgeon.prefab";

        static NetSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                bool missing = !File.Exists(MenuPath) || !File.Exists(VersusPath) || !File.Exists(GreyKnightPrefab) || !File.Exists(BerserkerPrefab) || !File.Exists(PlagueSurgeonPrefab);
                if (missing && File.Exists("Assets/Models/GreyKnight.fbx")) BuildAll();
            };
        }

        [MenuItem("Anathema/Rebuild Online Setup")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(Path.GetDirectoryName(MenuPath));

            // Build in additive scenes so whatever the user has open is left untouched.
            Scene previous = SceneManager.GetActiveScene();

            BuildPlayerPrefab(ArenaSceneBuilder.Character.GreyKnight, GreyKnightPrefab);
            BuildPlayerPrefab(ArenaSceneBuilder.Character.KhorneBerserker, BerserkerPrefab);
            BuildPlayerPrefab(ArenaSceneBuilder.Character.PlagueSurgeon, PlagueSurgeonPrefab);
            BuildVersusScene();
            BuildMenuScene();

            if (previous.IsValid()) SceneManager.SetActiveScene(previous);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(VersusPath, true),
                new EditorBuildSettingsScene(TrainingPath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Online setup built: Menu + Versus scenes and player prefabs. Open Assets/Scenes/Menu.unity and press Play.");
        }

        static Scene NewScratchScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            return scene;
        }

        static void BuildPlayerPrefab(ArenaSceneBuilder.Character character, string path)
        {
            Scene scratch = NewScratchScene();

            GameObject root = ArenaSceneBuilder.CreatePlayer(character);
            root.name = Path.GetFileNameWithoutExtension(path);

            // Replicas (other players' characters) use a plain capsule instead of the CharacterController.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.enabled = false;

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetPlayer>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            EditorSceneManager.CloseScene(scratch, true);

            // NetworkObject normally stamps its prefab hash while the asset is validated; make sure it has happened.
            var networkObject = saved.GetComponent<NetworkObject>();
            MethodInfo onValidate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            onValidate?.Invoke(networkObject, null);
            EditorUtility.SetDirty(saved);
            PrefabUtility.SavePrefabAsset(saved);
        }

        static void BuildVersusScene()
        {
            Scene scene = NewScratchScene();

            ArenaSceneBuilder.CreateLight();
            ArenaSceneBuilder.CreateArena();
            new GameObject("Gas Zone").AddComponent<GasZone>();
            new GameObject("Orb Spawner").AddComponent<OrbSpawner>();
            ArenaSceneBuilder.CreateCamera(null);

            // Opposite sides of the pit, facing the centre (NetGame turns them towards it).
            CreateSpawn(0, new Vector3(-20f, 0.1f, 0f));
            CreateSpawn(1, new Vector3(20f, 0.1f, 0f));

            EditorSceneManager.SaveScene(scene, VersusPath);
            EditorSceneManager.CloseScene(scene, true);
        }

        static void CreateSpawn(int index, Vector3 position)
        {
            var go = new GameObject("SpawnPoint_" + index);
            go.transform.position = position;
        }

        static void BuildMenuScene()
        {
            Scene scene = NewScratchScene();

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.03f, 0.03f);
            cameraObject.AddComponent<AudioListener>();

            new GameObject("Menu").AddComponent<MenuBootstrap>();

            EditorSceneManager.SaveScene(scene, MenuPath);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
