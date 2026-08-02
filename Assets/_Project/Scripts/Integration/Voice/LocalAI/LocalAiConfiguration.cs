using System;
using System.IO;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
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
                Debug.LogWarning($"Local AI config could not be loaded: {exception.Message}");
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

        private static string ResolveProjectRoot()
        {
#if UNITY_EDITOR
            return Directory.GetParent(Application.dataPath)?.FullName
                ?? Directory.GetCurrentDirectory();
#else
            return Directory.GetParent(Application.dataPath)?.FullName
                ?? Directory.GetCurrentDirectory();
#endif
        }
    }
}
