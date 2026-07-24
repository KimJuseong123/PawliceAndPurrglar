namespace PawsAndLoot.Config
{
    public static class GameConfigService
    {
        private static GameConfigSet _current;

        public static bool IsInitialized => _current != null;

        public static GameConfigSet Current =>
            _current ?? throw new GameConfigurationException(
                "Game configuration has not been initialized. Start from the Bootstrap scene.");

        public static void Initialize(GameConfigSet configSet)
        {
            if (configSet == null)
            {
                throw new GameConfigurationException(
                    "GameConfigBootstrap is missing its required GameConfigSet reference.");
            }

            configSet.ValidateOrThrow();

            if (_current != null && _current != configSet)
            {
                throw new GameConfigurationException(
                    $"Game configuration is already initialized with '{_current.name}' and cannot be replaced by '{configSet.name}'.");
            }

            _current = configSet;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _current = null;
        }
    }
}
