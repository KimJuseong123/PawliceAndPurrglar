using System;
using System.Collections.Generic;
using PawsAndLoot.Config;
using UnityEngine;

namespace PawsAndLoot.Logging
{
    public static class GameLogger
    {
        private const string Prefix = "[PawsAndLoot]";

        private static readonly object Gate = new();
        private static readonly HashSet<string> OnceKeys = new(StringComparer.Ordinal);
        private static GameLogConfig _config;

        public static bool IsConfigured
        {
            get
            {
                lock (Gate)
                {
                    return _config != null;
                }
            }
        }

        public static void Configure(GameLogConfig config)
        {
            if (config == null)
            {
                throw new GameConfigurationException(
                    "GameLogBootstrap is missing its required GameLogConfig reference.");
            }

            config.ValidateOrThrow();

            lock (Gate)
            {
                if (_config != null && _config != config)
                {
                    throw new GameConfigurationException(
                        $"Game logging is already configured with '{_config.name}' and cannot be replaced by '{config.name}'.");
                }

                _config = config;
            }
        }

        public static void Debug(
            GameLogCategory category,
            string message,
            UnityEngine.Object context = null)
        {
            Write(GameLogLevel.Debug, category, message, context);
        }

        public static void Info(
            GameLogCategory category,
            string message,
            UnityEngine.Object context = null)
        {
            Write(GameLogLevel.Info, category, message, context);
        }

        public static void Warning(
            GameLogCategory category,
            string message,
            UnityEngine.Object context = null)
        {
            Write(GameLogLevel.Warning, category, message, context);
        }

        public static void Error(
            GameLogCategory category,
            string message,
            UnityEngine.Object context = null)
        {
            Write(GameLogLevel.Error, category, message, context);
        }

        public static void DebugOnce(
            GameLogCategory category,
            string key,
            string message,
            UnityEngine.Object context = null)
        {
            WriteOnce(GameLogLevel.Debug, category, key, message, context);
        }

        public static void InfoOnce(
            GameLogCategory category,
            string key,
            string message,
            UnityEngine.Object context = null)
        {
            WriteOnce(GameLogLevel.Info, category, key, message, context);
        }

        public static void WarningOnce(
            GameLogCategory category,
            string key,
            string message,
            UnityEngine.Object context = null)
        {
            WriteOnce(GameLogLevel.Warning, category, key, message, context);
        }

        public static void ErrorOnce(
            GameLogCategory category,
            string key,
            string message,
            UnityEngine.Object context = null)
        {
            WriteOnce(GameLogLevel.Error, category, key, message, context);
        }

        public static void Exception(
            GameLogCategory category,
            Exception exception,
            string message = null,
            UnityEngine.Object context = null)
        {
            ValidateCategory(category);
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            if (!ShouldWrite(GameLogLevel.Error))
            {
                return;
            }

            string summary = string.IsNullOrWhiteSpace(message)
                ? $"{exception.GetType().Name}: {exception.Message}"
                : message;
            UnityEngine.Debug.LogError(
                Format(GameLogLevel.Error, category, summary),
                context);

            UnityEngine.Debug.LogException(exception, context);
        }

        private static void WriteOnce(
            GameLogLevel level,
            GameLogCategory category,
            string key,
            string message,
            UnityEngine.Object context)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A non-empty deduplication key is required.", nameof(key));
            }

            ValidateCategory(category);
            if (!ShouldWrite(level))
            {
                return;
            }

            string qualifiedKey = $"{category}:{level}:{key}";
            lock (Gate)
            {
                if (!OnceKeys.Add(qualifiedKey))
                {
                    return;
                }
            }

            Write(level, category, message, context);
        }

        private static void Write(
            GameLogLevel level,
            GameLogCategory category,
            string message,
            UnityEngine.Object context)
        {
            ValidateCategory(category);
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A non-empty log message is required.", nameof(message));
            }

            if (!ShouldWrite(level))
            {
                return;
            }

            string formattedMessage = Format(level, category, message);
            switch (level)
            {
                case GameLogLevel.Warning:
                    UnityEngine.Debug.LogWarning(formattedMessage, context);
                    break;
                case GameLogLevel.Error:
                    UnityEngine.Debug.LogError(formattedMessage, context);
                    break;
                default:
                    UnityEngine.Debug.Log(formattedMessage, context);
                    break;
            }
        }

        private static bool ShouldWrite(GameLogLevel level)
        {
            GameLogConfig config;
            lock (Gate)
            {
                config = _config;
            }

            if (config == null)
            {
                return level >= GameLogLevel.Warning;
            }

            bool isDevelopmentBuild = Application.isEditor || UnityEngine.Debug.isDebugBuild;
            return level >= config.GetMinimumLevel(isDevelopmentBuild);
        }

        private static string Format(
            GameLogLevel level,
            GameLogCategory category,
            string message)
        {
            return $"{Prefix}[{category}][{level}] {message}";
        }

        private static void ValidateCategory(GameLogCategory category)
        {
            if (!Enum.IsDefined(typeof(GameLogCategory), category))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(category),
                    category,
                    "Unknown game log category.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (Gate)
            {
                _config = null;
                OnceKeys.Clear();
            }
        }
    }
}
