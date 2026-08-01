using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    public static class LocalAiBuildMenu
    {
        private const string BuildPath = "Build/Windows/PawsAndLoot.exe";

        [MenuItem("Paws & Loot/Build/Build Windows Standalone with Local AI")]
        public static void BuildWindowsStandalone()
        {
            bool allowMissing = LocalAiInstallationValidator.AllowMissingFromCommandLine();
            LocalAiInstallationValidator.ValidateOrThrow(allowMissing);

            string[] scenes = ResolveScenePaths();
            string absolute = Path.GetFullPath(BuildPath);
            string buildDirectory = Path.GetDirectoryName(absolute);
            Directory.CreateDirectory(buildDirectory);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = absolute,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows Standalone build failed: {report.summary.totalErrors} errors.");
            }

            LocalAiInstallationValidator.CopyForBuild(buildDirectory);
            string reportPath = Path.Combine(buildDirectory, "build-report.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(new BuildReportSummary
            {
                result = report.summary.result.ToString(),
                totalErrors = report.summary.totalErrors,
                totalWarnings = report.summary.totalWarnings,
                totalSize = report.summary.totalSize,
                outputPath = absolute,
                unityVersion = Application.unityVersion
            }, true));

            Debug.Log($"Windows Standalone build succeeded at '{absolute}'.");
        }

        private static string[] ResolveScenePaths()
        {
            var paths = new List<string>();
            foreach (GameSceneId sceneId in GameSceneCatalog.BuildOrder)
            {
                string path = GameSceneCatalog.GetPath(sceneId);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("Required scene is missing.", path);
                }

                paths.Add(path);
            }

            if (paths.Count == 0)
            {
                throw new InvalidOperationException("No build scenes were resolved.");
            }

            return paths.ToArray();
        }

        [Serializable]
        private sealed class BuildReportSummary
        {
            public string result;
            public int totalErrors;
            public int totalWarnings;
            public ulong totalSize;
            public string outputPath;
            public string unityVersion;
        }
    }
}
