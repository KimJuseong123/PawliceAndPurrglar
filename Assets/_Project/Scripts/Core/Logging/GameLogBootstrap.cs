using UnityEngine;

namespace PawliceAndPurrglar.Logging
{
    [DefaultExecutionOrder(-11000)]
    public sealed class GameLogBootstrap : MonoBehaviour
    {
        [SerializeField]
        private GameLogConfig config;

        public GameLogConfig Config
        {
            get => config;
            set => config = value;
        }

        private void Awake()
        {
            GameLogger.Configure(config);
            GameLogger.InfoOnce(
                GameLogCategory.Match,
                "logging-initialized",
                $"Logging initialized from '{config.name}' with " +
                $"development level {config.DevelopmentMinimumLevel} and " +
                $"release level {config.ReleaseMinimumLevel}.",
                this);
        }
    }
}
