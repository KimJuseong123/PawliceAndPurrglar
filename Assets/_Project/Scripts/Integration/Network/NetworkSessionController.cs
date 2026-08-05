using System;
using PawsAndLoot.Logging;
using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Integration.Voice;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Hosts or joins a two-player session over direct IP.
    ///
    /// Host authority with direct IP is the adopted model (DEC-027): the host
    /// is the server, no Relay and no dedicated build. This lives in the
    /// Integration layer so no rule depends on it; the match itself still runs
    /// on the same local systems whether or not a session was ever started.
    ///
    /// Exactly two players are allowed. A third is refused during approval
    /// rather than after spawning, so it never briefly appears in the world.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSessionController : MonoBehaviour
    {
        public const int MaximumPlayers = 2;
        public const ushort DefaultPort = 7979;

        public enum SessionMode
        {
            Offline = 0,
            Host = 1,
            Client = 2
        }

        [SerializeField]
        private NetworkManager networkManager;

        /// <summary>
        /// Spawned by the server once hosting starts.
        ///
        /// A registered prefab rather than a scene object: with scene
        /// management disabled, NGO asks the client to create in-scene
        /// NetworkObjects from the prefab list and drops the connection when it
        /// cannot find one.
        /// </summary>
        [SerializeField]
        private GameObject roleBoardPrefab;

        private SessionMode _mode = SessionMode.Offline;
        private NetworkObject _spawnedRoleBoard;

        public event Action<SessionMode> ModeChanged;
        public event Action<string> StatusChanged;

        public SessionMode Mode => _mode;
        public string LastStatus { get; private set; } = string.Empty;
        public ushort Port { get; private set; } = DefaultPort;
        public string JoinAddress { get; private set; } =
            LocalAddressProvider.LoopbackAddress;

        /// <summary>
        /// Number of connected players. Only the server can know this:
        /// <c>ConnectedClientsIds</c> is server-only in NGO and reads as empty
        /// on a client, so a client must never gate on it.
        /// </summary>
        public int ConnectedPlayerCount =>
            networkManager != null
            && networkManager.IsListening
            && networkManager.IsServer
                ? networkManager.ConnectedClientsIds.Count
                : 0;

        /// <summary>
        /// Whether this machine has everything it needs to start.
        ///
        /// The host counts its clients. A client cannot count anything, so it
        /// relies on being connected, and on the replicated role board for the
        /// actual assignment.
        /// </summary>
        public bool IsSessionReady =>
            _mode switch
            {
                SessionMode.Host =>
                    ConnectedPlayerCount >= MaximumPlayers,
                SessionMode.Client =>
                    networkManager != null
                    && networkManager.IsConnectedClient,
                _ => false
            };

        public void Configure(
            NetworkManager manager,
            GameObject configuredRoleBoardPrefab = null)
        {
            networkManager = manager;
            roleBoardPrefab = configuredRoleBoardPrefab;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (networkManager == null)
            {
                throw new InvalidOperationException(
                    $"NetworkSessionController '{name}' requires a "
                    + "NetworkManager.");
            }
        }

        public bool TryStartHost(string port)
        {
            if (_mode != SessionMode.Offline)
            {
                SetStatus("이미 세션이 실행 중입니다.");
                return false;
            }

            if (!LocalAddressProvider.IsValidPort(port, out ushort parsed))
            {
                SetStatus("포트는 1024 이상의 숫자여야 합니다.");
                return false;
            }

            Port = parsed;
            // Listen on any interface so a second machine on the LAN can reach
            // us, while the address shown on screen is the one to type in.
            if (!TryApplyTransport("0.0.0.0", parsed))
            {
                return false;
            }

            // The approval flag is already on in the serialized config so both
            // sides hash the same. Only the callback is host side.
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;

            if (!networkManager.StartHost())
            {
                Cleanup();
                SetStatus("호스트 시작에 실패했습니다.");
                return false;
            }

            SpawnRoleBoard();
            VoiceSessionCapabilityClient voiceClient =
                GetComponent<VoiceSessionCapabilityClient>();
            if (voiceClient == null)
            {
                voiceClient = gameObject.AddComponent<
                    VoiceSessionCapabilityClient>();
            }
            voiceClient.Configure(
                GameConfigService.IsInitialized
                    ? GameConfigService.Current.Voice
                    : null);
            SetMode(SessionMode.Host);
            SetStatus(
                $"호스트 대기 중 · 포트 {parsed} · 상대에게 내 IP를 알려주세요.");
            GameLogger.Info(
                GameLogCategory.Network,
                $"Hosting on port {parsed}.",
                this);
            return true;
        }

        public bool TryJoin(string address, string port)
        {
            if (_mode != SessionMode.Offline)
            {
                SetStatus("이미 세션이 실행 중입니다.");
                return false;
            }

            // A name is as good as a number here.
            //
            // This took an IPv4 address and nothing else, which is right for a
            // room on the same wifi and wrong for everywhere this is going: a
            // tunnel hands out a hostname, and so will the server. Rejecting
            // them meant the only way to reach a remote host was to look up its
            // address by hand and hope it had not moved.
            string trimmed =
                LocalAddressProvider.NormaliseHost(address);
            if (!LocalAddressProvider.IsValidHost(trimmed))
            {
                SetStatus(
                    "주소 형식이 올바르지 않습니다. "
                    + "예: 192.168.0.10 또는 0.tcp.ngrok.io");
                return false;
            }

            if (!LocalAddressProvider.IsValidPort(port, out ushort parsed))
            {
                SetStatus("포트는 1024 이상의 숫자여야 합니다.");
                return false;
            }

            JoinAddress = trimmed;
            Port = parsed;
            if (!TryApplyTransport(trimmed, parsed))
            {
                return false;
            }

            networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;

            if (!networkManager.StartClient())
            {
                Cleanup();
                SetStatus("접속 시작에 실패했습니다.");
                return false;
            }

            SetMode(SessionMode.Client);
            SetStatus($"{trimmed}:{parsed} 로 접속 중...");
            GameLogger.Info(
                GameLogCategory.Network,
                $"Joining {trimmed}:{parsed}.",
                this);
            return true;
        }

        /// <summary>
        /// Forces this controller back to a fresh lobby state.
        ///
        /// Unlike <see cref="Leave"/> this does not require a session to be
        /// running and never touches the transport: it is for the case where the
        /// session belonged to a manager that has just been destroyed, so there
        /// is nothing left to shut down and the only work is dropping references
        /// to it.
        /// </summary>
        public void ResetForLobby()
        {
            Cleanup();
            // Dropped rather than despawned. The board belonged to the previous
            // session; despawning through a manager that is gone would throw,
            // and holding the reference would make SpawnRoleBoard skip the new
            // one and leave the roles unassigned.
            _spawnedRoleBoard = null;
            SetMode(SessionMode.Offline);
            LastStatus = string.Empty;
            StatusChanged?.Invoke(LastStatus);
        }

        public void Leave()
        {
            if (_mode == SessionMode.Offline)
            {
                return;
            }

            DespawnRoleBoard();
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            Cleanup();
            SetMode(SessionMode.Offline);
            SetStatus("세션을 종료했습니다.");
        }

        /// <summary>
        /// Server side only. The client receives the board through the normal
        /// spawn message instead of finding it in its own scene.
        /// </summary>
        private void SpawnRoleBoard()
        {
            if (roleBoardPrefab == null || _spawnedRoleBoard != null)
            {
                return;
            }

            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsServer)
            {
                GameLogger.Error(
                    GameLogCategory.Network,
                    "Refusing to spawn the role board: this manager is not "
                    + $"listening as a server (listening="
                    + $"{networkManager != null && networkManager.IsListening}, "
                    + $"server={networkManager != null && networkManager.IsServer}).",
                    this);
                return;
            }

            var prefabObject = roleBoardPrefab.GetComponent<NetworkObject>();
            if (prefabObject == null)
            {
                GameLogger.Warning(
                    GameLogCategory.Network,
                    "Role board prefab has no NetworkObject.",
                    this);
                return;
            }

            // Spawned through this manager by name rather than Instantiate plus
            // Spawn(). A NetworkObject with no owner set resolves its manager as
            // NetworkManager.Singleton, and Netcode leaves a manager from a
            // previous match holding that singleton — the spawn then went to
            // something that was not listening and said so.
            //
            // Lives only for the lobby: the role is committed to a local value
            // before the match loads, so nothing has to survive the scene change.
            _spawnedRoleBoard = prefabObject.InstantiateAndSpawn(networkManager);
            if (_spawnedRoleBoard == null)
            {
                GameLogger.Error(
                    GameLogCategory.Network,
                    "Netcode refused to spawn the role board; roles cannot be "
                    + "assigned.",
                    this);
            }
        }

        private void DespawnRoleBoard()
        {
            if (_spawnedRoleBoard == null)
            {
                return;
            }

            if (_spawnedRoleBoard.IsSpawned
                && networkManager != null
                && networkManager.IsServer)
            {
                _spawnedRoleBoard.Despawn();
            }

            _spawnedRoleBoard = null;
        }

        private bool TryApplyTransport(string address, ushort port)
        {
            var transport =
                networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                SetStatus("UnityTransport 컴포넌트를 찾을 수 없습니다.");
                return false;
            }

            // WebSocket rather than UDP, on both sides.
            //
            // A browser cannot open a UDP socket, so the moment this game is
            // meant to be played in one — which is where it is going — UDP stops
            // being an option. Switching now means the transport under the
            // desktop build and the transport under the WebGL build are the same
            // one, and a bug found in either is a bug found in both.
            //
            // It also makes the session tunnellable. UDP needs a port forwarded
            // and a public address, which half of Korean home connections cannot
            // give you; a TCP-shaped protocol goes through any of the free
            // tunnels, which is how two people on different networks can play
            // before there is a server to play on.
            transport.UseWebSockets = true;

            transport.SetConnectionData(address, port);
            return true;
        }

        /// <summary>
        /// Refuses a third player before it spawns. The host itself is already
        /// counted, so the limit is checked against connected clients.
        /// </summary>
        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool isHostItself =
                request.ClientNetworkId == networkManager.LocalClientId;
            int wouldBe = networkManager.ConnectedClientsIds.Count
                + (isHostItself ? 0 : 1);

            response.Approved = isHostItself || wouldBe <= MaximumPlayers;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (!response.Approved)
            {
                response.Reason = "This match already has two players.";
                GameLogger.Warning(
                    GameLogCategory.Network,
                    "Refused an extra player: the match is full.",
                    this);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_mode == SessionMode.Client)
            {
                SetStatus("호스트에 접속했습니다. 역할을 기다립니다.");
                return;
            }

            SetStatus(
                ConnectedPlayerCount >= MaximumPlayers
                    ? "두 명이 모였습니다. 역할을 정하세요."
                    : $"접속 {ConnectedPlayerCount}/{MaximumPlayers}");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            // A client losing the host, or a host losing its client, must both
            // land somewhere the player can act, never an endless wait.
            if (_mode == SessionMode.Client)
            {
                Cleanup();
                SetMode(SessionMode.Offline);
                SetStatus("호스트와 연결이 끊어졌습니다.");
                return;
            }

            SetStatus(
                $"상대가 나갔습니다. 접속 "
                + $"{ConnectedPlayerCount}/{MaximumPlayers}");
        }

        private void Cleanup()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback = null;
            networkManager.OnClientConnectedCallback -=
                HandleClientConnected;
            networkManager.OnClientDisconnectCallback -=
                HandleClientDisconnected;
        }

        private void SetMode(SessionMode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            ModeChanged?.Invoke(mode);
        }

        private void SetStatus(string status)
        {
            LastStatus = status;
            StatusChanged?.Invoke(status);
        }

        /// <summary>
        /// Offers the session-ending hook the result screen's "로비로" needs.
        /// Installed here rather than called directly so the UI layer keeps not
        /// referencing the network layer.
        /// </summary>
        private void OnEnable()
        {
            NetworkSceneBridge.SetLeaveHandler(Leave);
        }

        private void OnDisable()
        {
            NetworkSceneBridge.ClearLeaveHandler();
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
