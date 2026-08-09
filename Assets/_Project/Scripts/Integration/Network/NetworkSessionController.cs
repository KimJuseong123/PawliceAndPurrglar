using System;
using System.Threading.Tasks;
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
    /// Hosts or joins a two-player session, by invite code or by address.
    ///
    /// Host authority is still the model: one of the two players is the server.
    /// What changed is how the other one reaches them. Direct IP (DEC-027) was
    /// right while this was a desktop build on one wifi, and is unreachable
    /// from a browser — Unity Transport refuses a WebGL server outright unless
    /// the protocol is Relay, and no home connection behind CGNAT has an
    /// address to type anyway. So the browser path is
    /// <see cref="TryCreateRoomAsync"/> and <see cref="TryJoinRoomAsync"/>, and
    /// the address pair stays for the desktop two-process regression, which is
    /// the one place a real listening socket still exists.
    ///
    /// This lives in the Integration layer so no rule depends on it; the match
    /// itself still runs on the same local systems whether or not a session was
    /// ever started.
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
        /// The code this machine is showing or used, empty when there is none.
        ///
        /// Held here rather than in the lobby because it outlives the screen
        /// that shows it: a player who alt-tabs away and comes back needs to
        /// read it out again, and the lobby is rebuilt from this on every
        /// refresh.
        /// </summary>
        public string InviteCode { get; private set; } = string.Empty;

        /// <summary>
        /// True while a Relay call is in flight.
        ///
        /// Relay is a round trip to a server, so unlike every other entry point
        /// here there is a window in which the session is neither offline nor
        /// started. Without a name for it, the buttons stay enabled through the
        /// wait and a second press starts a second allocation — two rooms, one
        /// of which nobody will ever join.
        /// </summary>
        public bool IsBusy { get; private set; }

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

        /// <summary>
        /// Does the part of hosting that does not depend on the player, before
        /// they ask for it.
        ///
        /// Safe to call repeatedly and safe to ignore: it signs in and stops,
        /// and if that fails nothing is reported, because nobody has asked for
        /// anything yet.
        /// </summary>
        public void Prewarm()
        {
            _ = RelaySessionService.PrewarmAsync();
        }

        /// <summary>
        /// Opens a room on Relay and starts hosting it, returning the invite
        /// code through <see cref="InviteCode"/>.
        ///
        /// This is the browser's only way to host. Unity Transport refuses a
        /// WebGL server outright unless the protocol is Relay, so the direct-IP
        /// pair below cannot be reached from a browser at all — they stay for
        /// the desktop two-process regression, which is the one place a real
        /// listening socket still exists.
        /// </summary>
        public async Task<bool> TryCreateRoomAsync()
        {
            if (_mode != SessionMode.Offline)
            {
                RefuseHosting(
                    "이미 세션이 실행 중입니다.",
                    $"the session is still in mode {_mode}");
                return false;
            }

            if (IsBusy)
            {
                RefuseHosting(
                    "아직 처리 중입니다. 잠시 후 다시 누르세요.",
                    "a previous request has not finished");
                return false;
            }

            UnityTransport transport = ResolveTransport();
            if (transport == null)
            {
                RefuseHosting(
                    "UnityTransport 컴포넌트를 찾을 수 없습니다.",
                    networkManager == null
                        ? "this controller has no NetworkManager"
                        : "the NetworkManager has no UnityTransport");
                return false;
            }

            // The manager has to be finished with the last match before it can
            // host the next one. Shutdown is not instant, and StartHost on a
            // manager that is still listening or still closing returns false
            // with nothing said — which is indistinguishable, on screen, from
            // the button not being wired.
            if (networkManager.IsListening
                || networkManager.ShutdownInProgress)
            {
                RefuseHosting(
                    "이전 세션을 정리하는 중입니다. 잠시 후 다시 누르세요.",
                    $"listening={networkManager.IsListening}, "
                    + $"shuttingDown={networkManager.ShutdownInProgress}");
                return false;
            }

            SetBusy(true, "방을 만드는 중입니다...");
            RelayOutcome outcome =
                await RelaySessionService.CreateRoomAsync(transport);

            // The await outlives the object when a player leaves the lobby mid
            // request. Touching networkManager after that throws inside a Task,
            // where nothing surfaces it.
            if (this == null)
            {
                return false;
            }

            if (!outcome.Ok)
            {
                SetBusy(false, outcome.Error);
                return false;
            }

            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;

            if (!networkManager.StartHost())
            {
                Cleanup();
                SetBusy(false, "호스트 시작에 실패했습니다.");
                GameLogger.Error(
                    GameLogCategory.Network,
                    "StartHost refused after Relay handed us an allocation. "
                    + $"singletonIsUs={NetworkManager.Singleton == networkManager}, "
                    + $"listening={networkManager.IsListening}, "
                    + $"shuttingDown={networkManager.ShutdownInProgress}.",
                    this);
                return false;
            }

            InviteCode = outcome.InviteCode;
            SpawnRoleBoard();
            ConfigureVoiceCapability();
            SetMode(SessionMode.Host);
            SetBusy(
                false,
                $"초대코드 {InviteCode} · 상대에게 알려주세요.");
            GameLogger.Info(
                GameLogCategory.Network,
                $"Hosting a Relay room with invite code {InviteCode}.",
                this);
            return true;
        }

        /// <summary>
        /// Joins the room an invite code names.
        /// </summary>
        public async Task<bool> TryJoinRoomAsync(string rawInviteCode)
        {
            if (_mode != SessionMode.Offline)
            {
                SetStatus("이미 세션이 실행 중입니다.");
                return false;
            }

            if (IsBusy)
            {
                return false;
            }

            UnityTransport transport = ResolveTransport();
            if (transport == null)
            {
                SetStatus("UnityTransport 컴포넌트를 찾을 수 없습니다.");
                return false;
            }

            // Validated before the spinner rather than after the round trip: a
            // five-character code is answerable here, and sending it would buy
            // the player a wait to be told what was already knowable.
            // Fully qualified: inside this class the bare name is the property
            // holding this session's code, not the type that validates one.
            string problem =
                PawsAndLoot.Integration.Network.InviteCode.DescribeProblem(
                    rawInviteCode);
            if (problem != null)
            {
                SetStatus(problem);
                return false;
            }

            SetBusy(true, "방을 찾는 중입니다...");
            RelayOutcome outcome =
                await RelaySessionService.JoinRoomAsync(transport, rawInviteCode);

            if (this == null)
            {
                return false;
            }

            if (!outcome.Ok)
            {
                SetBusy(false, outcome.Error);
                return false;
            }

            networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;

            if (!networkManager.StartClient())
            {
                Cleanup();
                SetBusy(false, "접속 시작에 실패했습니다.");
                return false;
            }

            InviteCode = outcome.InviteCode;
            SetMode(SessionMode.Client);
            SetBusy(false, $"초대코드 {InviteCode} 방에 접속 중...");
            GameLogger.Info(
                GameLogCategory.Network,
                $"Joining a Relay room with invite code {InviteCode}.",
                this);
            return true;
        }

        /// <summary>
        /// Starts a session this machine does not play in.
        /// </summary>
        /// <remarks>
        /// The difference from a host is one call and one absence: no local
        /// player. Both characters belong to clients, both are driven by input
        /// arriving over the wire, and the authority that decides everything
        /// sits on a machine with no screen — which is what a deployed server
        /// is and what a host can never be.
        ///
        /// Everything else is deliberately identical, approval included. The
        /// cap counts connected clients, and on a host one of those is the host
        /// itself; with nobody local the same arithmetic admits two players
        /// instead of one, which is the answer both cases want.
        /// </remarks>
        public bool TryStartServer(string port)
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
            if (!TryApplyTransport("0.0.0.0", parsed))
            {
                return false;
            }

            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;

            if (!networkManager.StartServer())
            {
                Cleanup();
                SetStatus("서버 시작에 실패했습니다.");
                return false;
            }

            _mode = SessionMode.Host;
            SetStatus($"서버가 포트 {parsed}에서 대기 중입니다.");
            SpawnRoleBoard();
            return true;
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
            ConfigureVoiceCapability();
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
            InviteCode = string.Empty;
            IsBusy = false;
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
            InviteCode = string.Empty;
            IsBusy = false;
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

        private UnityTransport ResolveTransport() =>
            networkManager != null
                ? networkManager.GetComponent<UnityTransport>()
                : null;

        /// <summary>
        /// Gives the host the voice capability component the session needs.
        /// Host only: the component answers for the machine that owns the
        /// match, and a client asking itself would answer for nobody.
        /// </summary>
        private void ConfigureVoiceCapability()
        {
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
        }

        private void SetBusy(bool busy, string status)
        {
            IsBusy = busy;
            SetStatus(status);
        }

        /// <summary>
        /// Says no to hosting, on screen and in the log, with the reason.
        ///
        /// The screen gets a sentence the player can act on and the log gets the
        /// state that produced it. Without the second half every one of these
        /// looks the same from outside — the button was pressed and the lobby
        /// did not open a room — and the first thing anybody suspects is the
        /// wiring, which this project has had wrong before and would go looking
        /// for again.
        /// </summary>
        private void RefuseHosting(string status, string reason)
        {
            SetStatus(status);
            GameLogger.Warning(
                GameLogCategory.Network,
                $"방 만들기 refused: {reason}.",
                this);
        }

        private bool TryApplyTransport(string address, ushort port)
        {
            var transport = ResolveTransport();
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
                // The room may well still be open — this machine simply is not
                // in it any more, and a code shown next to "연결이 끊어졌습니다"
                // reads as though it were.
                InviteCode = string.Empty;
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
