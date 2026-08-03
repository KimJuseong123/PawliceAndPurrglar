using System.Collections;
using System.Collections.Generic;
using PawsAndLoot.Core;
using PawsAndLoot.Logging;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Makes the lobby's own NetworkManager the only one, every time Bootstrap
    /// loads.
    ///
    /// Netcode's <c>NetworkManager.OnEnable</c> calls
    /// <c>DontDestroyOnLoad</c> on itself unconditionally — not once a session
    /// starts, but as soon as it is enabled — and claims the static
    /// <c>Singleton</c> only while that is still null. Loading Bootstrap a
    /// second time therefore leaves the first manager alive and still the
    /// singleton, beside the new scene's own.
    ///
    /// The lobby holds a serialised reference to the new one, so hosting
    /// appeared to work. But <c>NetworkObject.NetworkManager</c> falls back to
    /// the singleton whenever no owner is set, so spawning the role board went
    /// to the stale manager and logged "NetworkManagerOwner is not listening,
    /// start a server or host before spawning objects". No board meant no role
    /// assignment, and 게임 시작 stayed disabled forever.
    ///
    /// Destroying the survivor rather than adopting it: a manager that has
    /// hosted carries connection callbacks, a spawned object list and a session
    /// mode, and every one of those has to be unwound correctly. A scene that
    /// always brings its own manager has none of that to get wrong.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbySessionReset : MonoBehaviour
    {
        /// <summary>
        /// How long to wait for a stale manager to finish shutting down before
        /// destroying it anyway. Shutdown is not instant, and destroying a
        /// listening manager leaves its transport socket bound.
        /// </summary>
        private const float ShutdownTimeoutSeconds = 3f;

        [SerializeField]
        private NetworkSessionController session;

        public void Configure(NetworkSessionController configuredSession)
        {
            session = configuredSession;
        }

        /// <summary>
        /// Runs the reset immediately, for callers that need it done before they
        /// continue. The coroutine form is what the scene uses.
        /// </summary>
        public static void Run(NetworkSessionController lobbySession)
        {
            if (lobbySession == null)
            {
                return;
            }

            var keep = lobbySession.GetComponent<NetworkManager>();
            DestroyStaleManagers(keep);
            Reinstate(lobbySession, keep);
        }

        private void Start()
        {
            if (session == null)
            {
                session = GetComponent<NetworkSessionController>();
            }

            if (session == null)
            {
                GameLogger.Error(
                    GameLogCategory.Network,
                    "LobbySessionReset has no session, so a leftover "
                    + "NetworkManager from a previous match cannot be cleared.",
                    this);
                return;
            }

            StartCoroutine(ResetRoutine());
        }

        /// <summary>
        /// Started from <c>Start</c> rather than <c>Awake</c> on purpose. Unity
        /// gives no ordering guarantee between this component's Awake and the
        /// surviving NetworkManager's OnEnable, and the reinstatement below only
        /// makes sense once every component in the scene has had its turn.
        /// </summary>
        private IEnumerator ResetRoutine()
        {
            var keep = session.GetComponent<NetworkManager>();
            List<NetworkManager> stale = FindStale(keep);
            if (stale.Count == 0)
            {
                // First load of the session. Nothing to clear, but the singleton
                // still has to be this manager for spawning to resolve.
                Reinstate(session, keep);
                yield break;
            }

            GameLogger.Warning(
                GameLogCategory.Network,
                $"Found {stale.Count} leftover NetworkManager(s) from a "
                + "previous match. Shutting them down before the lobby opens.",
                this);

            foreach (NetworkManager manager in stale)
            {
                if (manager != null && manager.IsListening)
                {
                    manager.Shutdown();
                }
            }

            float deadline = Time.realtimeSinceStartup + ShutdownTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && StillClosing(stale))
            {
                yield return null;
            }

            if (StillClosing(stale))
            {
                GameLogger.Warning(
                    GameLogCategory.Network,
                    "A leftover NetworkManager did not finish shutting down in "
                    + $"{ShutdownTimeoutSeconds:0}s; destroying it anyway.",
                    this);
            }

            DestroyStaleManagers(keep);
            // One frame so the destroyed managers' OnDisable and OnDestroy have
            // run. That is where Netcode releases the singleton and where the
            // scene bridge handlers are cleared — reinstating before it would be
            // undone immediately.
            yield return null;

            Reinstate(session, keep);
        }

        private static bool StillClosing(IReadOnlyList<NetworkManager> stale)
        {
            foreach (NetworkManager manager in stale)
            {
                if (manager != null
                    && (manager.IsListening || manager.ShutdownInProgress))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<NetworkManager> FindStale(NetworkManager keep)
        {
            var stale = new List<NetworkManager>();
            foreach (NetworkManager manager in
                     FindObjectsByType<NetworkManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (manager != null && manager != keep)
                {
                    stale.Add(manager);
                }
            }

            return stale;
        }

        private static void DestroyStaleManagers(NetworkManager keep)
        {
            foreach (NetworkManager manager in FindStale(keep))
            {
                if (manager != null)
                {
                    DestroyImmediate(manager.gameObject);
                }
            }
        }

        /// <summary>
        /// Puts the surviving manager back in charge.
        ///
        /// The statics have to be re-established by hand: this manager's
        /// OnEnable has already run and declined the singleton because it was
        /// taken, and the destroyed manager's components cleared the scene
        /// bridge handlers on their way out — possibly after this scene's had
        /// installed theirs.
        /// </summary>
        private static void Reinstate(
            NetworkSessionController lobbySession,
            NetworkManager keep)
        {
            if (keep == null)
            {
                return;
            }

            if (NetworkManager.Singleton != keep)
            {
                keep.SetSingleton();
            }

            // Re-enabling is what re-registers them: each installs its handler
            // in OnEnable, and there is no separate entry point to call.
            Rebind(keep.GetComponent<NetworkSceneCoordinator>());
            Rebind(keep.GetComponent<NetworkRematchCoordinator>());
            Rebind(lobbySession);

            lobbySession.ResetForLobby();
        }

        private static void Rebind(MonoBehaviour component)
        {
            if (component == null || !component.enabled)
            {
                return;
            }

            component.enabled = false;
            component.enabled = true;
        }
    }
}
