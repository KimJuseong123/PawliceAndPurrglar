using System;

namespace PawsAndLoot.Core
{
    /// <summary>
    /// Lets <see cref="GameSceneLoader"/> hand a scene load to the network
    /// layer without the Core layer referencing it.
    ///
    /// The Core scene loader must stay usable offline, and rules must not depend
    /// on networking, so the network layer installs a handler here instead of
    /// the loader knowing about NGO.
    /// </summary>
    public static class NetworkSceneBridge
    {
        /// <summary>
        /// Returns true when the handler took responsibility for the load.
        /// </summary>
        private static Func<string, bool> _handler;

        public static bool HasHandler => _handler != null;

        public static void SetHandler(Func<string, bool> handler)
        {
            _handler = handler;
        }

        public static void ClearHandler()
        {
            _handler = null;
        }

        public static bool TryLoad(string sceneName)
        {
            return _handler != null && _handler(sceneName);
        }
    }
}
