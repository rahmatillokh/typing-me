using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TypingMe.EditorTools
{
    /// <summary>
    /// Player builds, callable from the menu or headless via
    /// <c>-executeMethod TypingMe.EditorTools.BuildTools.BuildMac</c>.
    /// </summary>
    public static class BuildTools
    {
        private static string[] Scenes =>
            EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

        [MenuItem("Typing Me/Build/macOS (for local playtesting)", false, 20)]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/TypingMe.app");

        /// <summary>
        /// Requires the "Windows Build Support (Mono)" module — Mono, not IL2CPP, because IL2CPP
        /// for Windows cannot cross-compile from macOS. Fails with a clear message when the
        /// module is missing rather than a cryptic build error.
        /// </summary>
        [MenuItem("Typing Me/Build/Windows x64", false, 21)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/TypingMe.exe");

        private static void Build(BuildTarget target, string outputPath)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                Debug.LogError($"[Typing Me] Build target {target} is not installed. " +
                               "Add the module in Unity Hub → Installs → Add modules.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            if (Scenes.Length == 0)
            {
                Debug.LogError("[Typing Me] No enabled scenes in Build Settings.");
                if (Application.isBatchMode) EditorApplication.Exit(3);
                return;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target);

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Typing Me] Build succeeded: {outputPath} " +
                          $"({summary.totalSize / 1024 / 1024} MB, {summary.totalTime.TotalSeconds:F0}s)");
                return;
            }

            Debug.LogError($"[Typing Me] Build {summary.result}: {summary.totalErrors} error(s).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
