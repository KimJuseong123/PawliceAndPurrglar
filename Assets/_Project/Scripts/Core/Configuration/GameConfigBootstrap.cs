using UnityEngine;
using PawliceAndPurrglar.Logging;

namespace PawliceAndPurrglar.Config
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GameConfigBootstrap : MonoBehaviour
    {
        [SerializeField]
        private GameConfigSet configSet;

        public GameConfigSet ConfigSet
        {
            get => configSet;
            set => configSet = value;
        }

        private void Awake()
        {
            if (GameConfigService.IsInitialized)
            {
                GameConfigService.Initialize(configSet);
                Destroy(gameObject);
                return;
            }

            GameConfigService.Initialize(configSet);
            GameLogger.InfoOnce(
                GameLogCategory.Match,
                "game-config-initialized",
                $"Game configuration initialized from '{configSet.name}'.",
                this);
            DontDestroyOnLoad(gameObject);
        }
    }
}
