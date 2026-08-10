using System;
using System.IO;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Voice
{
    [Serializable]
    public sealed class LocalAiGatewaySettings
    {
        public string host = "127.0.0.1";
        public int port = 8765;
        public int startupTimeoutSeconds = 120;
        public int requestTimeoutSeconds = 30;
        public int portScanCount = 16;
    }

    [Serializable]
    public sealed class LocalAiOllamaSettings
    {
        public string host = "127.0.0.1";
        public int port = 11434;
        public string model = "qwen3:4b-instruct";
    }

    [Serializable]
    public sealed class LocalAiSttSettings
    {
        public string modelPath = "models/faster-whisper-small";
        public string language = "ko";
        public string preferredDevice = "cuda";
        public string gpuComputeType = "int8_float16";
        public string cpuComputeType = "int8";
    }

    [Serializable]
    public sealed class LocalAiRecordingSettings
    {
        public int sampleRate = 16000;
        public int maxSeconds = 5;
        public int minimumMilliseconds = 250;
    }

    [Serializable]
    public sealed class LocalAiConfigurationData
    {
        public LocalAiGatewaySettings gateway = new();
        public LocalAiOllamaSettings ollama = new();
        public LocalAiSttSettings stt = new();
        public LocalAiRecordingSettings recording = new();
    }

    public sealed class LocalAiConfiguration
    {
        public LocalAiConfigurationData Data { get; }
        public string ProjectRoot { get; }
        public string ConfigPath { get; }
        public string GatewayDirectory => Path.Combine(
            ProjectRoot,
            "LocalAI",
            "runtime",
            "gateway");
        public string GatewayExecutable => Path.Combine(
            GatewayDirectory,
            "paws-local-ai.exe");
        public string OllamaDirectory => Path.Combine(
            ProjectRoot,
            "LocalAI",
            "runtime",
            "ollama");
        public string OllamaExecutable => Path.Combine(
            OllamaDirectory,
            "ollama.exe");
        public string LogsDirectory => Path.Combine(
            ProjectRoot,
            "LocalAI",
            "logs");

        private LocalAiConfiguration(
            LocalAiConfigurationData data,
            string projectRoot,
            string configPath)
        {
            Data = data ?? new LocalAiConfigurationData();
            ProjectRoot = projectRoot;
            ConfigPath = configPath;
        }

        public static LocalAiConfiguration Load()
        {
            string root = ResolveProjectRoot();
            string path = Path.Combine(root, "LocalAI", "config", "local-ai.json");
            if (!File.Exists(path))
            {
                return new LocalAiConfiguration(
                    new LocalAiConfigurationData(),
                    root,
                    path);
            }

            try
            {
                LocalAiConfigurationData data = JsonUtility.FromJson<LocalAiConfigurationData>(
                    File.ReadAllText(path));
                return new LocalAiConfiguration(data, root, path);
            }
            catch (Exception exception)
            {
                GameLogger.Warning(
                    GameLogCategory.Voice,
                    $"Local AI config could not be loaded: {exception.Message}");
                return new LocalAiConfiguration(
                    new LocalAiConfigurationData(),
                    root,
                    path);
            }
        }

        public string ResolvePath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return ProjectRoot;
            }

            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(ProjectRoot, "LocalAI", configuredPath);
        }

        /// <summary>
        /// Marker files that identify the folder holding the Local AI stack.
        /// Either one is enough: a machine that has only been configured has the
        /// json, and a machine that only has the packaged runtime has the exe.
        /// </summary>
        private static bool HoldsLocalAiStack(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string localAi = Path.Combine(candidate, "LocalAI");
            return File.Exists(Path.Combine(localAi, "config", "local-ai.json"))
                || File.Exists(Path.Combine(
                    localAi,
                    "runtime",
                    "gateway",
                    "paws-local-ai.exe"));
        }

        /// <summary>
        /// Finds the folder that owns <c>LocalAI/</c>.
        ///
        /// The previous version returned the parent of <c>Application.dataPath</c>
        /// and stopped there. In the editor that is the project root and the
        /// stack is found. **In a build it is the folder holding the exe** —
        /// `Builds/Playtest/Windows` — which has no `LocalAI/`, so the gateway
        /// executable never existed, `StartGateway` returned false for all
        /// sixteen scanned ports, and voice failed with `GATEWAY_NOT_READY`
        /// before a single sample was recorded. The only trace was one
        /// `LogWarning` at startup, so the build looked like a microphone
        /// problem (`ISSUE-069`).
        ///
        /// So walk up. The stack is over a gigabyte of models and must not be
        /// copied next to every build; finding the repository above the build
        /// folder is what makes a Windows build and the editor use the same one.
        /// </summary>
        private static string ResolveProjectRoot()
        {
            string near = Directory.GetParent(Application.dataPath)?.FullName
                ?? Directory.GetCurrentDirectory();

            string overridden = ReadConfiguredRoot();
            if (HoldsLocalAiStack(overridden))
            {
                return overridden;
            }

            foreach (string start in new[] { near, Directory.GetCurrentDirectory() })
            {
                DirectoryInfo directory = SafeDirectory(start);
                while (directory != null)
                {
                    if (HoldsLocalAiStack(directory.FullName))
                    {
                        if (!string.Equals(
                                directory.FullName,
                                near,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            GameLogger.InfoOnce(
                                GameLogCategory.Voice,
                                "local-ai-root",
                                "Local AI stack found above the build folder at "
                                + $"'{directory.FullName}'.");
                        }

                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            // Unchanged fallback so a machine without the stack behaves exactly
            // as before, but say so once instead of failing silently.
            GameLogger.WarningOnce(
                GameLogCategory.Voice,
                "local-ai-root-missing",
                $"No LocalAI folder found at or above '{near}'. Windows voice "
                + "commands cannot start the local gateway. Pass "
                + "-localAiRoot <path> or set PAWS_LOCAL_AI_ROOT.");
            return near;
        }

        /// <summary>
        /// Explicit override, checked before the search: a copied build or a
        /// second checkout can point at one shared stack.
        /// </summary>
        private static string ReadConfiguredRoot()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(
                        arguments[index],
                        "-localAiRoot",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return Environment.GetEnvironmentVariable("PAWS_LOCAL_AI_ROOT");
        }

        private static DirectoryInfo SafeDirectory(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path)
                    ? null
                    : new DirectoryInfo(path);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
