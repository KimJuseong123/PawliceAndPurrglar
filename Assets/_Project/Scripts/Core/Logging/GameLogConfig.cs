using System;
using PawsAndLoot.Config;
using UnityEngine;

namespace PawsAndLoot.Logging
{
    [CreateAssetMenu(menuName = "Paws & Loot/Logging/Game Log Config", fileName = "GameLogConfig")]
    public sealed class GameLogConfig : GameConfigAsset
    {
        [Header("Minimum Level")]
        [SerializeField]
        private GameLogLevel developmentMinimumLevel = GameLogLevel.Debug;

        [SerializeField]
        private GameLogLevel releaseMinimumLevel = GameLogLevel.Warning;

        public GameLogLevel DevelopmentMinimumLevel => developmentMinimumLevel;
        public GameLogLevel ReleaseMinimumLevel => releaseMinimumLevel;

        public GameLogLevel GetMinimumLevel(bool isDevelopmentBuild)
        {
            return isDevelopmentBuild ? developmentMinimumLevel : releaseMinimumLevel;
        }

        public override void ValidateOrThrow()
        {
            ValidateLevel(developmentMinimumLevel, nameof(developmentMinimumLevel));
            ValidateLevel(releaseMinimumLevel, nameof(releaseMinimumLevel));

            if (developmentMinimumLevel >= releaseMinimumLevel)
            {
                throw GameConfigValidation.CreateException(
                    this,
                    nameof(developmentMinimumLevel),
                    "development logging must be more verbose than release logging");
            }
        }

        private void ValidateLevel(GameLogLevel level, string fieldName)
        {
            if (!Enum.IsDefined(typeof(GameLogLevel), level))
            {
                throw GameConfigValidation.CreateException(
                    this,
                    fieldName,
                    $"unknown log level value {(int)level}");
            }
        }
    }
}
