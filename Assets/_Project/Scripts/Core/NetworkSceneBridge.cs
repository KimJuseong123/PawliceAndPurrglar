using System;

namespace PawliceAndPurrglar.Core
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

        /// <summary>
        /// NET-008. Rematch is not an ordinary load: a client may not load at
        /// all, it has to ask the host. Kept as its own handler so the plain
        /// load path stays unchanged.
        /// </summary>
        private static Func<bool> _rematchHandler;

        public static bool HasRematchHandler => _rematchHandler != null;

        public static void SetRematchHandler(Func<bool> handler)
        {
            _rematchHandler = handler;
        }

        public static void ClearRematchHandler()
        {
            _rematchHandler = null;
        }

        public static bool TryRequestRematch()
        {
            return _rematchHandler != null && _rematchHandler();
        }

        /// <summary>
        /// Leaving the match for the lobby. Its own handler because going back
        /// to the lobby has to end the session first: the lobby that loads is a
        /// fresh scene with its own NetworkManager, and a session still running
        /// on the old one leaves two of them alive with the stale one owning the
        /// singleton.
        /// </summary>
        private static Action _leaveHandler;

        public static bool HasLeaveHandler => _leaveHandler != null;

        public static void SetLeaveHandler(Action handler)
        {
            _leaveHandler = handler;
        }

        public static void ClearLeaveHandler()
        {
            _leaveHandler = null;
        }

        /// <summary>
        /// Ends any running session. Safe to call offline, where it does
        /// nothing.
        /// </summary>
        public static void LeaveSession()
        {
            _leaveHandler?.Invoke();
        }
    }
}
