using PawsAndLoot.Core;
using PawsAndLoot.Logging;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Routes scene loads through NGO while a session is running.
    ///
    /// Scene management has to be on for the match: the players, loot and arrest
    /// objects live in the scene, and without it a client cannot resolve them
    /// and is disconnected. With it on, the server loads and every client
    /// follows, so both machines agree on the object list.
    ///
    /// Offline the handler declines and the plain loader runs, which keeps the
    /// single-player playtest path working unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSceneCoordinator : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager networkManager;

        public void Configure(NetworkManager manager)
        {
            networkManager = manager;
        }

        private bool HandleLoad(string sceneName)
        {
            if (networkManager == null || !networkManager.IsListening)
            {
                return false;
            }

            if (!networkManager.NetworkConfig.EnableSceneManagement)
            {
                return false;
            }

            // Only the server may start a networked load. A client asking for
            // one is a bug, so it is refused loudly rather than silently
            // desynchronising.
            if (!networkManager.IsServer)
            {
                GameLogger.Warning(
                    GameLogCategory.Network,
                    $"Ignored a client-side load of '{sceneName}'. "
                    + "The host drives scene changes.",
                    this);
                return true;
            }

            SceneEventProgressStatus status =
                networkManager.SceneManager.LoadScene(
                    sceneName,
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started)
            {
                GameLogger.Info(
                    GameLogCategory.Network,
                    $"Server is loading '{sceneName}' for everyone.",
                    this);
                return true;
            }

            GameLogger.Warning(
                GameLogCategory.Network,
                $"Networked load of '{sceneName}' returned {status}; "
                + "falling back to a local load.",
                this);
            return false;
        }

        private void OnEnable()
        {
            NetworkSceneBridge.SetHandler(HandleLoad);
        }

        private void OnDisable()
        {
            NetworkSceneBridge.ClearHandler();
        }
    }
}
