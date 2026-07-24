using UnityEngine;

namespace PawsAndLoot.Config
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
            DontDestroyOnLoad(gameObject);
        }
    }
}
