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
        [MenuItem("PawliceAndPurrglar/Build/Measure WebGL Build Size")]
        public static void BuildWebGl()
        {
            BuildWebGl(BuildOptions.None, true);
        }

        /// <summary>
        /// Builds the browser playtest used by the local launcher. Keeping this
        /// separate from the measurement command makes the batch entry point
        /// explicit without changing the existing validation workflow.
        /// </summary>
        [MenuItem("PawliceAndPurrglar/Build/Build WebGL Playtest")]
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

            using BuildStamp stamp = BuildStamp.Apply();
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
        [MenuItem("PawliceAndPurrglar/Build/Build Linux Dedicated Server")]
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

            using BuildStamp stamp = BuildStamp.Apply();
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

        /// <summary>
        /// Stamps the current commit into the player's version, for the duration
        /// of one build.
        ///
        /// Two machines running builds from different commits disagree about the
        /// <c>GlobalObjectIdHash</c> of every <c>NetworkObject</c> placed in the
        /// match scene — regenerating `Game.unity` renumbers all 130 of them — and
        /// NGO reports that as a wall of "soft synchronization failure" lines
        /// followed by a <c>NullReferenceException</c>. What the player sees is
        /// that they cannot move while the bag still opens. Nothing in that says
        /// "different build", so <c>NetworkSceneFingerprint</c> compares this
        /// string at connect time and says it.
        ///
        /// Restored afterwards, so running a build does not leave the repository
        /// dirty. The stamp lives in the built player, which is where it is
        /// needed, and nowhere else.
        /// </summary>
        private readonly struct BuildStamp : IDisposable
        {
            private readonly string _previous;

            private BuildStamp(string previous)
            {
                _previous = previous;
            }

            public static BuildStamp Apply()
            {
                string previous = PlayerSettings.bundleVersion;
                string commit = ReadCommit();
                if (!string.IsNullOrEmpty(commit))
                {
                    // Kept on the base version rather than replacing it, so a
                    // released version number survives and the commit is
                    // additional. `+` is the build-metadata separator in semver
                    // and is what NetworkSceneFingerprint looks for.
                    string baseVersion = previous.Split('+')[0];
                    string dirty = HasUncommittedGameFiles()
                        ? "-dirty"
                        : string.Empty;
                    PlayerSettings.bundleVersion =
                        $"{baseVersion}+{commit}{dirty}";
                    Debug.Log(
                        $"[BUILD] Stamped version '{PlayerSettings.bundleVersion}'. "
                        + "Both machines must run a build with the same stamp.");
                    if (dirty.Length > 0)
                    {
                        Debug.LogWarning(
                            "[BUILD] This build contains uncommitted changes to "
                            + "Assets/ProjectSettings/Packages. Another machine "
                            + "cloning the same commit will NOT get them, and a "
                            + "regenerated Game.unity renumbers every in-scene "
                            + "NetworkObject — which shows up as a player who "
                            + "cannot move.");
                    }
                }
                else
                {
                    Debug.LogWarning(
                        "[BUILD] Could not read the git commit, so this build "
                        + "carries no stamp and the connect-time build check "
                        + "will not be able to compare it.");
                }

                return new BuildStamp(previous);
            }

            public void Dispose()
            {
                PlayerSettings.bundleVersion = _previous;
            }

            /// <summary>
            /// Whether anything that goes into the build is uncommitted.
            ///
            /// The commit alone is not the build. A machine with a regenerated
            /// <c>Game.unity</c> sitting unstaged produces a player whose 130
            /// in-scene <c>NetworkObject</c> hashes match nothing another machine
            /// can clone — and both builds were stamped with the same commit, so
            /// the connect-time check said they matched. It reported "same build"
            /// for the one case it exists to catch.
            ///
            /// Scoped to <c>Assets</c>, <c>ProjectSettings</c> and
            /// <c>Packages</c> on purpose. Marking a build dirty because a
            /// document or a server file changed would put the warning on almost
            /// every build, and a warning that is always there is one nobody
            /// reads.
            /// </summary>
            private static bool HasUncommittedGameFiles()
            {
                string status = RunGit(
                    "status --porcelain -- Assets ProjectSettings Packages");
                return !string.IsNullOrWhiteSpace(status);
            }

            private static string ReadCommit()
            {
                return RunGit("rev-parse --short HEAD");
            }

            /// <summary>
            /// Runs git in the project folder and returns its output, or an empty
            /// string if it could not be run at all.
            ///
            /// Empty means "could not ask", which both callers treat as "do not
            /// claim anything" rather than as an answer.
            /// </summary>
            private static string RunGit(string arguments)
            {
                try
                {
                    var info = new System.Diagnostics.ProcessStartInfo(
                        "git",
                        arguments)
                    {
                        WorkingDirectory =
                            Path.GetDirectoryName(Application.dataPath),
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using System.Diagnostics.Process process =
                        System.Diagnostics.Process.Start(info);
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);
                    return output;
                }
                catch (System.Exception error)
                {
                    Debug.LogWarning(
                        $"[BUILD] 'git {arguments}' failed: {error.Message}");
                    return string.Empty;
                }
            }
        }

        [MenuItem("PawliceAndPurrglar/Build/Build Windows Playtest")]
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
            using BuildStamp stamp = BuildStamp.Apply();
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
