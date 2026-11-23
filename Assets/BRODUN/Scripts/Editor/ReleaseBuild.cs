using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Brodun.Editor
{
    public static class ReleaseBuild
    {
        public static void Windows()
        {
            var output = Environment.GetEnvironmentVariable("BRODUN_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Set BRODUN_BUILD_OUTPUT to the release folder.");
            Directory.CreateDirectory(output);
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && scene.path.StartsWith("Assets/BRODUN/Scenes/", StringComparison.Ordinal))
                .Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].EndsWith("/BrodunBoot.unity", StringComparison.Ordinal))
                throw new InvalidOperationException("BrodunBoot must be the first release scene.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "BRODUN.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            UnityEngine.Debug.Log("BRODUN release built: " + report.summary.totalSize + " bytes");
        }
    }
}
