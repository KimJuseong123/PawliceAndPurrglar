using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// NET-009. Gets both machines out of a broken session instead of leaving
    /// one waiting.
    ///
    /// A 1v1 chase cannot continue with one player, so any peer loss ends the
    /// session and returns to the lobby. The same path covers every case the
    /// task lists — police quits, thief quits, host quits, someone quits on the
    /// result screen, the link drops mid-match — because they all arrive as the
    /// same disconnect callback.
    ///
    /// It runs once per session. Repeating the teardown is what produces the
    /// "error message repeating forever" failure, so the handled flag is the
    /// point of this class as much as the teardown itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkDisconnectHandler : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager networkManager;

        [SerializeField]
        private NetworkSessionController session;

        [SerializeField]
        private bool returnToLobby = true;

        private bool _subscribed;
        private bool _handled;

        public string LastReason { get; private set; } = string.Empty;
        public int HandledCount { get; private set; }

        public void Configure(
            NetworkManager manager,
            NetworkSessionController configuredSession)
        {
            networkManager = manager;
            session = configuredSession;
        }

        /// <summary>
        /// Exposed so a test or probe can exercise the teardown without
        /// physically dropping a socket.
        /// </summary>
        public void HandlePeerLost(string reason)
        {
            if (_handled)
            {
                return;
            }

            _handled = true;
            HandledCount++;
            LastReason = reason;
            GameLogger.Warning(
                GameLogCategory.Network,
                $"Session ended: {reason}",
                this);

            // The role came from the host. Once the host is gone it means
            // nothing, and leaving it set would carry a stale role into the next
            // session.
            LocalPlayerRoleSelector.ClearOverriddenRole();

            if (session != null)
            {
                session.Leave();
            }
            else if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            if (returnToLobby && !IsAlreadyInLobby())
            {
                // Loaded locally: the session is already down, so there is no
                // host left to drive a networked load.
                GameSceneLoader.Load(GameSceneId.Bootstrap);
            }
        }

        private static bool IsAlreadyInLobby()
        {
            return UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene().name
                == GameSceneCatalog.GetName(GameSceneId.Bootstrap);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            NetworkManager manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            // The host hears about its own client leaving; a client hears about
            // itself when the host is gone. Its own id arriving on a client is
            // the "host quit" case.
            bool isSelf = clientId == manager.LocalClientId;
            if (manager.IsServer && !isSelf)
            {
                HandlePeerLost("상대가 접속을 종료했습니다.");
                return;
            }

            if (!manager.IsServer)
            {
                HandlePeerLost("호스트와 연결이 끊어졌습니다.");
            }
        }

        private NetworkManager ResolveManager()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            return networkManager;
        }

        private void Update()
        {
            NetworkManager manager = ResolveManager();
            if (manager == null)
            {
                return;
            }

            if (manager.IsListening && !_subscribed)
            {
                _subscribed = true;
                // Armed only once a session exists, and re-armed for the next
                // one, so a fresh session is not treated as already broken.
                _handled = false;
                manager.OnClientDisconnectCallback +=
                    HandleClientDisconnected;
                return;
            }

            if (!manager.IsListening && _subscribed)
            {
                _subscribed = false;
                manager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (!_subscribed || networkManager == null)
            {
                return;
            }

            networkManager.OnClientDisconnectCallback -=
                HandleClientDisconnected;
            _subscribed = false;
        }
    }
}
