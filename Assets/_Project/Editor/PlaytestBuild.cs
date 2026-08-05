using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Builds the playable vertical slice with every scene of the build
    /// order so the Bootstrap -> Game -> Result -> rematch loop works in a
    /// standalone player. The technical validation builds ship single
    /// scenes and cannot reach the result screen.
    /// </summary>
    public static class PlaytestBuild
    {
        private const string WindowsBuildPath =
            "Builds/Playtest/Windows/PawsAndLoot.exe";

        private const string WebGlBuildPath = "Builds/Playtest/WebGL";

        /// <summary>
        /// ART-005 measures the WebGL payload without development metadata.
        /// </summary>
        [MenuItem("Paws & Loot/Build/Measure WebGL Build Size")]
        public static void BuildWebGl()
        {
            BuildWebGl(BuildOptions.None, true);
        }

        /// <summary>
        /// Builds the browser playtest used by the local launcher. Keeping this
        /// separate from the measurement command makes the batch entry point
        /// explicit without changing the existing validation workflow.
        /// </summary>
        [MenuItem("Paws & Loot/Build/Build WebGL Playtest")]
        public static void BuildWebGlPlaytest()
        {
            BuildWebGl(BuildOptions.Development, false);
        }

        private static void BuildWebGl(
            BuildOptions buildOptions,
            bool measurePayload)
        {
            string[] scenePaths = ResolveScenePaths();
            string absolute = Path.GetFullPath(WebGlBuildPath);
            Directory.CreateDirectory(absolute);

            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = absolute,
                    target = BuildTarget.WebGL,
                    options = buildOptions
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"WebGL build failed with "
                    + $"{report.summary.totalErrors} errors.");
            }

            if (!measurePayload)
            {
                Debug.Log(
                    $"WebGL playtest build succeeded at '{absolute}'.");
                return;
            }

            long totalBytes = 0;
            long buildFolderBytes = 0;
            foreach (string file in Directory.GetFiles(
                         absolute,
                         "*",
                         SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                totalBytes += info.Length;
                if (file.Replace('\\', '/').Contains("/Build/"))
                {
                    buildFolderBytes += info.Length;
                }
            }

            Debug.Log(
                $"[ART-005] WebGL total {totalBytes / (1024f * 1024f):0.00}MB, "
                + $"payload {buildFolderBytes / (1024f * 1024f):0.00}MB at "
                + $"'{absolute}'.");
        }

        /// <summary>
        /// The headless Linux build that goes on the server.
        /// </summary>
        /// <remarks>
        /// A server subtarget rather than a normal player with the window
        /// hidden. The subtarget strips the renderer, the audio and the input
        /// stack out of the build, which is most of its size and all of the
        /// parts that would be looking for a display that is not there.
        ///
        /// Needs the Linux module installed in this editor. Without it the build
        /// fails with a message about the target being unsupported, which is
        /// worth saying here rather than leaving somebody to read it out of a
        /// build report: Unity Hub &gt; Installs &gt; Add modules &gt; Linux
        /// Build Support (IL2CPP).
        /// </remarks>
        [MenuItem("Paws & Loot/Build/Build Linux Dedicated Server")]
        public static void BuildLinuxServer()
        {
            const string ServerBuildPath =
                "Builds/Server/Linux/PawsAndLoot.x86_64";

            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Standalone,
                    BuildTarget.StandaloneLinux64))
            {
                throw new InvalidOperationException(
                    "Linux Build Support is not installed in this editor. "
                    + "Unity Hub > Installs > the 6000.5.4f1 gear > Add "
                    + "modules > Linux Build Support (IL2CPP).");
            }

            string[] scenePaths = ResolveScenePaths();
            string absoluteBuildPath = Path.GetFullPath(ServerBuildPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(absoluteBuildPath));

            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneLinux64,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    options = BuildOptions.None
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Linux server build failed with "
                    + $"{report.summary.totalErrors} errors.");
            }

            Debug.Log(
                $"Linux dedicated server built at '{absoluteBuildPath}'. Run "
                + "it with: ./PawsAndLoot.x86_64 -dedicatedServer -netPort 7979");
        }

        [MenuItem("Paws & Loot/Build/Build Windows Playtest")]
        public static void BuildWindows()
        {
            string[] scenePaths = ResolveScenePaths();
            string absoluteBuildPath = Path.GetFullPath(WindowsBuildPath);
            string directory = Path.GetDirectoryName(absoluteBuildPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    $"Could not resolve build directory for "
                    + $"'{absoluteBuildPath}'.");
            }

            Directory.CreateDirectory(directory);
            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Playtest Windows build failed with "
                    + $"{report.summary.totalErrors} errors.");
            }

            Debug.Log(
                $"Playtest Windows build succeeded with "
                + $"{scenePaths.Length} scenes at '{absoluteBuildPath}'.");
        }

        private static string[] ResolveScenePaths()
        {
            var paths = new List<string>();
            foreach (GameSceneId sceneId in GameSceneCatalog.BuildOrder)
            {
                string path = GameSceneCatalog.GetPath(sceneId);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        $"Required scene is missing: {path}",
                        path);
                }

                paths.Add(path);
            }

            if (paths.Count == 0)
            {
                throw new InvalidOperationException(
                    "The scene build order is empty.");
            }

            return paths.ToArray();
        }
    }
}
