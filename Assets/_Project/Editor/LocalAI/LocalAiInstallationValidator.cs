using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    public static class LocalAiInstallationValidator
    {
        public static bool AllowMissingFromCommandLine()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "-allowMissingLocalAi", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName
            ?? Directory.GetCurrentDirectory();

        public static string LocalAiRoot => Path.Combine(ProjectRoot, "LocalAI");

        public static void ValidateOrThrow(bool allowMissing)
        {
            if (allowMissing)
            {
                Debug.LogWarning("Building with -allowMissingLocalAi; voice runtime will be unavailable.");
                return;
            }

            var required = new List<string>
            {
                Path.Combine(LocalAiRoot, "config", "local-ai.json"),
                Path.Combine(LocalAiRoot, "runtime", "gateway", "paws-local-ai.exe"),
                Path.Combine(LocalAiRoot, "runtime", "ollama", "ollama.exe"),
                Path.Combine(LocalAiRoot, "models", "faster-whisper-small"),
                Path.Combine(LocalAiRoot, "models", "ollama"),
                Path.Combine(ProjectRoot, "Licenses", "faster-whisper-MIT.txt"),
                Path.Combine(ProjectRoot, "Licenses", "faster-whisper-small-MIT.txt"),
                Path.Combine(ProjectRoot, "Licenses", "Qwen3-Apache-2.0.txt"),
                Path.Combine(ProjectRoot, "Licenses", "Ollama-license.txt")
            };

            var missing = new List<string>();
            foreach (string path in required)
            {
                bool exists = File.Exists(path) || Directory.Exists(path);
                if (exists && Directory.Exists(path)
                    && Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length == 0)
                {
                    exists = false;
                }

                if (!exists)
                {
                    missing.Add(path);
                }
            }

            if (missing.Count > 0)
            {
                throw new FileNotFoundException(
                    "LocalAI installation is incomplete. Missing:\n"
                    + string.Join("\n", missing));
            }
        }

        public static void CopyForBuild(string buildRoot)
        {
            string destination = Path.Combine(buildRoot, "LocalAI");
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            Directory.CreateDirectory(destination);
            CopyDirectory(
                Path.Combine(LocalAiRoot, "runtime"),
                Path.Combine(destination, "runtime"));
            CopyDirectory(
                Path.Combine(LocalAiRoot, "models"),
                Path.Combine(destination, "models"));
            CopyDirectory(
                Path.Combine(LocalAiRoot, "config"),
                Path.Combine(destination, "config"));
            CopyDirectory(
                Path.Combine(ProjectRoot, "Licenses"),
                Path.Combine(destination, "licenses"));
            Directory.CreateDirectory(Path.Combine(destination, "logs"));

            string manifest = Path.Combine(LocalAiRoot, "manifest.json");
            if (File.Exists(manifest))
            {
                File.Copy(manifest, Path.Combine(destination, "manifest.json"), true);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException(source);
            }

            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }
    }
}
