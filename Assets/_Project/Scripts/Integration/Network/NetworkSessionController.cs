using System;
using PawsAndLoot.Logging;
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

            string trimmed = address?.Trim() ?? string.Empty;
            if (!LocalAddressProvider.IsValidIPv4(trimmed))
            {
                SetStatus("IP 형식이 올바르지 않습니다. 예: 192.168.0.10");
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

            GameObject instance = Instantiate(roleBoardPrefab);
            _spawnedRoleBoard = instance.GetComponent<NetworkObject>();
            if (_spawnedRoleBoard == null)
            {
                Destroy(instance);
                GameLogger.Warning(
                    GameLogCategory.Network,
                    "Role board prefab has no NetworkObject.",
                    this);
                return;
            }

            // The board has to outlive the lobby scene: the match scene reads
            // the role from it, and a Single-mode load would otherwise destroy
            // it and leave the client with no role at all.
            _spawnedRoleBoard.Spawn(false);
            _spawnedRoleBoard.DestroyWithScene = false;
            DontDestroyOnLoad(instance);
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

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
