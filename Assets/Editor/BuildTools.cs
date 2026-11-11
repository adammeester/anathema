using System.IO;
using Anathema.Net;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anathema.EditorTools
{
    // One-click Windows build: regenerates the online scenes/prefabs (so the latest scripts are baked in), then
    // builds Builds/Anathema/Anathema.exe with the Menu, Versus and Arena scenes.
    public static class BuildTools
    {
        const string OutputExe = "Builds/Anathema/Anathema.exe";
        const string ServerExe = "Builds/AnathemaServer/AnathemaServer.exe";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/Menu.unity", "Assets/Scenes/Versus.unity", "Assets/Scenes/Versus_Shrine.unity", "Assets/Scenes/Arena.unity"
        };

        [MenuItem("Anathema/Build Windows Game")]
        public static void BuildWindows()
        {
            NetSceneBuilder.BuildAll();
            Build(OutputExe, (int)StandaloneBuildSubtarget.Player);
        }

        // For command-line builds: Unity.exe -batchmode -quit -projectPath <path> -executeMethod Anathema.EditorTools.BuildTools.BuildWindowsBatch
        public static void BuildWindowsBatch()
        {
            BuildWindows();
            EditorApplication.Exit(File.Exists(OutputExe) ? 0 : 1);
        }

        // The dedicated server: a true headless Dedicated Server build when Unity's "Dedicated Server Build Support"
        // module is installed, otherwise the normal player (run headless by the StartServer.bat written next to it).
        [MenuItem("Anathema/Build Dedicated Server")]
        public static void BuildServer()
        {
            NetSceneBuilder.BuildAll();
            bool serverModule = HasDedicatedServerModule();
            if (!serverModule)
                Debug.LogWarning("Dedicated Server Build Support is not installed for this Unity version; building the normal " +
                                 "player instead. It runs headless through StartServer.bat. Add the module in Unity Hub for a leaner server.");

            if (!Build(ServerExe, (int)(serverModule ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player))) return;

            string exeName = Path.GetFileName(ServerExe);
            string bat = "@echo off\r\n" +
                         "rem Starts the Anathema dedicated server. Players join with this machine's IP (port " + NetGame.DefaultPort + ", UDP).\r\n" +
                         "rem Change the port with -port <number>; forward that UDP port on your router to play over the internet.\r\n" +
                         $"\"%~dp0{exeName}\" -batchmode -nographics -server -port {NetGame.DefaultPort} -logFile -\r\n";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(ServerExe), "StartServer.bat"), bat);
            Debug.Log($"Server ready: run {Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ServerExe), "StartServer.bat"))}");
        }

        public static void BuildServerBatch()
        {
            BuildServer();
            EditorApplication.Exit(File.Exists(ServerExe) ? 0 : 1);
        }

        static bool Build(string exe, int subtarget)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exe));
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                subtarget = subtarget,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {Path.GetFullPath(exe)} ({report.summary.totalSize / (1024 * 1024)} MB)");
                return true;
            }

            Debug.LogError($"Build failed: {report.summary.result} ({report.summary.totalErrors} errors). See the Console.");
            return false;
        }

        static bool HasDedicatedServerModule()
        {
            string engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.StandaloneWindows64, BuildOptions.None);
            string variations = Path.Combine(engine, "Variations");
            if (!Directory.Exists(variations)) return false;
            foreach (string dir in Directory.GetDirectories(variations))
                if (Path.GetFileName(dir).ToLowerInvariant().Contains("server")) return true;
            return false;
        }

        // Runs the Menu scene in the editor as a dedicated server, so built clients can join 127.0.0.1 to test it.
        [MenuItem("Anathema/Play as Dedicated Server")]
        public static void PlayAsServer()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.Environment.SetEnvironmentVariable(ServerLaunch.EnvironmentFlag, "1");
            EditorSceneManager.OpenScene(Scenes[0]);
            EditorApplication.isPlaying = true;
        }

        // The flag only lasts for the one play session.
        [InitializeOnLoadMethod]
        static void ClearServerFlagOnStop()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    System.Environment.SetEnvironmentVariable(ServerLaunch.EnvironmentFlag, null);
            };
        }
    }
}
