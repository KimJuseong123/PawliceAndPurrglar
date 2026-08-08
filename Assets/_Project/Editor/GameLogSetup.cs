using System;
using PawsAndLoot.Config;
using PawsAndLoot.Logging;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    public static class GameLogSetup
    {
        public const string LoggingRoot = "Assets/_Project/Settings/Logging";
        public const string DefaultConfigPath = LoggingRoot + "/DefaultGameLogConfig.asset";

        [MenuItem("Pawlice and Purrglar/Setup/Create Default Log Config")]
        public static void CreateDefaultLogConfig()
        {
            EnsureLoggingFolder();

            GameLogConfig config =
                AssetDatabase.LoadAssetAtPath<GameLogConfig>(DefaultConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameLogConfig>();
                AssetDatabase.CreateAsset(config, DefaultConfigPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            config.ValidateOrThrow();
            BasicSceneSetup.EnsureBootstrapServices();
            Debug.Log("BASE-005 default log configuration created and validated.");
        }

        [MenuItem("Pawlice and Purrglar/Setup/Validate Logging")]
        public static void ValidateLogging()
        {
            GameLogConfig config =
                AssetDatabase.LoadAssetAtPath<GameLogConfig>(DefaultConfigPath);
            if (config == null)
            {
                throw new GameConfigurationException(
                    $"Default GameLogConfig is missing at '{DefaultConfigPath}'.");
            }

            config.ValidateOrThrow();
            GameLogger.Configure(config);
            string validationRunId = Guid.NewGuid().ToString("N");

            foreach (GameLogCategory category in Enum.GetValues(typeof(GameLogCategory)))
            {
                GameLogger.InfoOnce(
                    category,
                    $"base-005-validation-{validationRunId}-{category}",
                    "BASE-005 category validation message.");
            }

            ValidateOnceSuppression(validationRunId);
            Debug.Log("BASE-005 logging validation passed.");
        }

        private static void ValidateOnceSuppression(string validationRunId)
        {
            const string probeMessage = "BASE-005 duplicate suppression probe.";
            int messageCount = 0;

            Application.LogCallback callback = (condition, _, logType) =>
            {
                if (logType == LogType.Warning && condition.Contains(probeMessage))
                {
                    messageCount++;
                }
            };

            Application.logMessageReceived += callback;
            try
            {
                GameLogger.WarningOnce(
                    GameLogCategory.Match,
                    $"base-005-duplicate-probe-{validationRunId}",
                    probeMessage);
                GameLogger.WarningOnce(
                    GameLogCategory.Match,
                    $"base-005-duplicate-probe-{validationRunId}",
                    probeMessage);
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }

            if (messageCount != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one warning from duplicate suppression probe, received {messageCount}.");
            }
        }

        private static void EnsureLoggingFolder()
        {
            if (!AssetDatabase.IsValidFolder(LoggingRoot))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Logging");
            }
        }
    }
}
