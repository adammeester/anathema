using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Anathema.EditorTools
{
    // One-click Windows build: regenerates the online scenes/prefabs (so the latest scripts are baked in), then
    // builds Builds/Anathema/Anathema.exe with the Menu, Versus and Arena scenes.
    public static class BuildTools
    {
        const string OutputExe = "Builds/Anathema/Anathema.exe";

        [MenuItem("Anathema/Build Windows Game")]
        public static void BuildWindows()
        {
            NetSceneBuilder.BuildAll();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputExe));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Menu.unity", "Assets/Scenes/Versus.unity", "Assets/Scenes/Arena.unity" },
                locationPathName = OutputExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"Build succeeded: {Path.GetFullPath(OutputExe)} ({report.summary.totalSize / (1024 * 1024)} MB)");
            else
                Debug.LogError($"Build failed: {report.summary.result} ({report.summary.totalErrors} errors). See the Console.");
        }

        // For command-line builds: Unity.exe -batchmode -quit -projectPath <path> -executeMethod Anathema.EditorTools.BuildTools.BuildWindowsBatch
        public static void BuildWindowsBatch()
        {
            BuildWindows();
            EditorApplication.Exit(File.Exists(OutputExe) ? 0 : 1);
        }
    }
}
