using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Logging;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Network
{
    /// <summary>
    /// NET-008. Lets either player ask for a rematch and makes both start the
    /// same one.
    ///
    /// A named message rather than an RPC on purpose: rematch is pressed on the
    /// result screen, and a NetworkObject placed in a scene does not survive the
    /// scene change (ISSUE-016). Named messages belong to the NetworkManager, so
    /// they work in every scene for as long as the session lives.
    ///
    /// The host is the only machine that loads. A client's press turns into a
    /// request, and the host's scene load carries the client with it, so there is
    /// no way for one side to restart alone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkRematchCoordinator : MonoBehaviour
    {
        public const string RematchMessageName = "PawliceAndPurrglar.Rematch";

        private static NetworkRematchCoordinator _instance;

        [SerializeField]
        private NetworkManager networkManager;

        private bool _registered;

        public int ReceivedRequestCount { get; private set; }

        public void Configure(NetworkManager manager)
        {
            networkManager = manager;
        }

        /// <summary>
        /// Routes one rematch press.
        ///
        /// Returns false when there is no session, so the caller falls back to
        /// its normal local scene load and the offline playtest is unaffected.
        /// </summary>
        public static bool TryRequestRematch()
        {
            return _instance != null && _instance.RequestRematch();
        }

        private bool RequestRematch()
        {
            NetworkManager manager = ResolveManager();
            if (manager == null || !manager.IsListening)
            {
                return false;
            }

            if (manager.IsServer)
            {
                // The host reloads for everyone through the scene coordinator.
                GameSceneLoader.Load(GameSceneId.Game);
                return true;
            }

            EnsureRegistered(manager);
            using var writer = new FastBufferWriter(
                sizeof(byte),
                Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            manager.CustomMessagingManager.SendNamedMessage(
                RematchMessageName,
                NetworkManager.ServerClientId,
                writer);
            GameLogger.Info(
                GameLogCategory.Network,
                "Asked the host for a rematch.",
                this);
            return true;
        }

        private void HandleRematchRequest(
            ulong senderClientId,
            FastBufferReader reader)
        {
            NetworkManager manager = ResolveManager();
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            ReceivedRequestCount++;
            GameLogger.Info(
                GameLogCategory.Network,
                $"Client {senderClientId} asked for a rematch.",
                this);
            GameSceneLoader.Load(GameSceneId.Game);
        }

        private NetworkManager ResolveManager()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            return networkManager;
        }

        private void EnsureRegistered(NetworkManager manager)
        {
            if (_registered
                || manager == null
                || manager.CustomMessagingManager == null)
            {
                return;
            }

            _registered = true;
            manager.CustomMessagingManager
                .RegisterNamedMessageHandler(
                    RematchMessageName,
                    HandleRematchRequest);
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable()
        {
            NetworkSceneBridge.SetRematchHandler(RequestRematch);
        }

        private void OnDisable()
        {
            NetworkSceneBridge.ClearRematchHandler();
        }

        private void Update()
        {
            NetworkManager manager = ResolveManager();
            if (manager == null || !manager.IsListening)
            {
                _registered = false;
                return;
            }

            // Registered lazily: the messaging manager only exists once the
            // session is running, and the handler has to outlive scene loads.
            EnsureRegistered(manager);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (!_registered || networkManager == null)
            {
                return;
            }

            networkManager.CustomMessagingManager
                ?.UnregisterNamedMessageHandler(RematchMessageName);
            _registered = false;
        }
    }
}
