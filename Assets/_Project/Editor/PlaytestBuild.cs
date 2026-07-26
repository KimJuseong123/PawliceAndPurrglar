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
