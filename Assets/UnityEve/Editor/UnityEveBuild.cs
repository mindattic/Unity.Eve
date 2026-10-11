using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityEve.Editor
{
    /// <summary>Batch-mode entry points: compile check and the Windows player build.</summary>
    public static class UnityEveBuild
    {
        public const string DemoScene = "Assets/UnityEve/Scenes/Demo.unity";

        /// <summary>Reaching this method at all means every assembly compiled.</summary>
        public static void CompileCheck()
        {
            Debug.Log("[Build] compile ok");
            EditorApplication.Exit(0);
        }

        [MenuItem("Unity Eve/Build/Windows Player")]
        public static void BuildWindows()
        {
            var versionFile = Path.Combine(Directory.GetCurrentDirectory(), "version.txt");
            if (File.Exists(versionFile)) PlayerSettings.bundleVersion = File.ReadAllText(versionFile).Trim();

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { DemoScene },
                locationPathName = "Build/Windows/UnityEve.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[Build] {report.summary.result} {report.summary.totalSize / (1024 * 1024)} MB, " +
                      $"{report.summary.totalErrors} errors -> {options.locationPathName}");
            if (!ok)
                foreach (var m in report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error))
                    Debug.Log($"[Build] {m.content}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
