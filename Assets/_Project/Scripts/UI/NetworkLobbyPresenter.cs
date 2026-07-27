using System.Collections.Generic;
using System.Text;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// The direct-IP lobby: shows this machine's address, takes the address to
    /// join, and once two players are present lets them split the roles.
    ///
    /// Read only with respect to rules. It starts a session and reads the role
    /// board; it never assigns a role itself, because only the server may do
    /// that.
    /// </summary>
    public sealed class NetworkLobbyPresenter : MonoBehaviour
    {
        [SerializeField]
        private NetworkSessionController session;

        [SerializeField]
        private Text myAddressLabel;

        [SerializeField]
        private Text statusLabel;

        [SerializeField]
        private Text roleLabel;

        [SerializeField]
        private InputField joinAddressField;

        [SerializeField]
        private InputField portField;

        [SerializeField]
        private Button hostButton;

        [SerializeField]
        private Button joinButton;

        [SerializeField]
        private Button swapRoleButton;

        [SerializeField]
        private Button startMatchButton;

        [SerializeField]
        private Button leaveButton;

        [SerializeField]
        private LanRoomDirectory roomDirectory;

        [SerializeField]
        private Text roomListLabel;

        /// <summary>
        /// Fixed slots rather than instantiated rows: the match is two players,
        /// so a handful of rooms is all a LAN will ever usefully show, and a
        /// fixed set costs no allocation on the per-frame refresh.
        /// </summary>
        [SerializeField]
        private Button[] roomButtons = new Button[0];

        [SerializeField]
        private Text[] roomLabels = new Text[0];

        private readonly List<LanRoom> _boundRooms = new();

        private NetworkRoleBoard _roleBoard;

        public string MyAddressText =>
            myAddressLabel != null ? myAddressLabel.text : string.Empty;
        public string StatusText =>
            statusLabel != null ? statusLabel.text : string.Empty;
        public string RoleText =>
            roleLabel != null ? roleLabel.text : string.Empty;

        public void Configure(
            NetworkSessionController configuredSession,
            Text configuredMyAddress,
            Text configuredStatus,
            Text configuredRole,
            InputField configuredJoinAddress,
            InputField configuredPort,
            Button configuredHost,
            Button configuredJoin,
            Button configuredSwap,
            Button configuredStart,
            Button configuredLeave)
        {
            session = configuredSession;
            myAddressLabel = configuredMyAddress;
            statusLabel = configuredStatus;
            roleLabel = configuredRole;
            joinAddressField = configuredJoinAddress;
            portField = configuredPort;
            hostButton = configuredHost;
            joinButton = configuredJoin;
            swapRoleButton = configuredSwap;
            startMatchButton = configuredStart;
            leaveButton = configuredLeave;
            RefreshAddresses();
            Refresh();
        }

        /// <summary>
        /// Supplies the LAN room list. Separate from <see cref="Configure"/> so
        /// a lobby without discovery still works: without these the panel is
        /// just the original type-an-IP lobby.
        /// </summary>
        public void ConfigureRoomList(
            LanRoomDirectory configuredDirectory,
            Text configuredRoomListLabel,
            Button[] configuredRoomButtons,
            Text[] configuredRoomLabels)
        {
            roomDirectory = configuredDirectory;
            roomListLabel = configuredRoomListLabel;
            roomButtons = configuredRoomButtons ?? new Button[0];
            roomLabels = configuredRoomLabels ?? new Text[0];
        }

        /// <summary>
        /// Joins a room found on the LAN. The address comes from the datagram's
        /// sender, so clicking a room is the same code path as typing that IP.
        /// </summary>
        public void JoinRoom(LanRoom room)
        {
            if (session == null
                || session.Mode
                   != NetworkSessionController.SessionMode.Offline)
            {
                return;
            }

            if (joinAddressField != null)
            {
                // Mirrored into the field so the player can see what was used,
                // and can retry by hand if the join fails.
                joinAddressField.text = room.Address;
            }

            if (portField != null)
            {
                portField.text = room.Port.ToString();
            }

            session.TryJoin(room.Address, room.Port.ToString());
            Refresh();
        }

        private void RefreshRoomList(bool offline)
        {
            if (roomButtons.Length == 0)
            {
                return;
            }

            _boundRooms.Clear();
            if (offline && roomDirectory != null)
            {
                foreach (LanRoom room in roomDirectory.Rooms)
                {
                    if (_boundRooms.Count >= roomButtons.Length)
                    {
                        break;
                    }

                    _boundRooms.Add(room);
                }
            }

            for (int index = 0; index < roomButtons.Length; index++)
            {
                Button button = roomButtons[index];
                if (button == null)
                {
                    continue;
                }

                bool used = index < _boundRooms.Count;
                button.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                LanRoom room = _boundRooms[index];
                // A full room stays visible but unclickable, so the player can
                // tell "no games found" from "the game is already full".
                button.interactable = !room.IsFull;
                if (index < roomLabels.Length
                    && roomLabels[index] != null)
                {
                    roomLabels[index].text = room.IsFull
                        ? $"{room.Label}  ({room.Address}) · 가득 참"
                        : $"{room.Label}  ({room.Address}) · "
                          + $"{room.PlayerCount}/"
                          + $"{NetworkSessionController.MaximumPlayers}";
                }
            }

            if (roomListLabel == null)
            {
                return;
            }

            if (!offline)
            {
                roomListLabel.text = "세션 진행 중";
                return;
            }

            if (roomDirectory != null && !roomDirectory.IsListening)
            {
                roomListLabel.text =
                    "같은 네트워크 방 찾기 불가 · 아래에 IP를 직접 입력하세요";
                return;
            }

            roomListLabel.text = _boundRooms.Count == 0
                ? "같은 네트워크에서 방을 찾는 중... (호스트가 먼저 방을 열어야 합니다)"
                : $"같은 네트워크의 방 {_boundRooms.Count}개 · 눌러서 참가";
        }

        /// <summary>
        /// Lists every local IPv4 so the player can pick the one their partner
        /// can actually reach. Loopback is labelled because it only works for
        /// two processes on one machine.
        /// </summary>
        public void RefreshAddresses()
        {
            if (myAddressLabel == null)
            {
                return;
            }

            var text = new StringBuilder();
            text.Append("내 IP: ");
            bool first = true;
            foreach (string address in
                LocalAddressProvider.GetIPv4Addresses())
            {
                if (!first)
                {
                    text.Append("   ");
                }

                first = false;
                text.Append(address);
                if (address == LocalAddressProvider.LoopbackAddress)
                {
                    text.Append(" (같은 PC)");
                }
            }

            myAddressLabel.text = text.ToString();

            if (joinAddressField != null
                && string.IsNullOrWhiteSpace(joinAddressField.text))
            {
                joinAddressField.text =
                    LocalAddressProvider.LoopbackAddress;
            }

            if (portField != null
                && string.IsNullOrWhiteSpace(portField.text))
            {
                portField.text =
                    NetworkSessionController.DefaultPort.ToString();
            }
        }

        public void OnHostPressed()
        {
            if (session == null)
            {
                return;
            }

            session.TryStartHost(
                portField != null ? portField.text : string.Empty);
            Refresh();
        }

        public void OnJoinPressed()
        {
            if (session == null)
            {
                return;
            }

            session.TryJoin(
                joinAddressField != null
                    ? joinAddressField.text
                    : string.Empty,
                portField != null ? portField.text : string.Empty);
            Refresh();
        }

        /// <summary>
        /// Asks the server to swap. Both sides use the same call: on the host it
        /// applies directly, on a client it becomes an RPC.
        /// </summary>
        public void OnSwapRolePressed()
        {
            NetworkRoleBoard board = ResolveRoleBoard();
            if (board == null)
            {
                return;
            }

            if (board.IsServer)
            {
                board.ApplySwap();
            }
            else
            {
                board.RequestSwapRolesRpc();
            }

            Refresh();
        }

        /// <summary>
        /// Only the host starts the match, so both machines enter it from one
        /// decision rather than racing.
        /// </summary>
        public void OnStartMatchPressed()
        {
            NetworkRoleBoard board = ResolveRoleBoard();
            if (session == null
                || !session.IsSessionReady
                || board == null
                || !board.IsAssigned)
            {
                return;
            }

            // Hand every machine its role before the scene changes: the board
            // will not exist on a client once the match scene loads.
            board.TryCommitRoles();
            GameSceneLoader.Load(GameSceneId.Game);
        }

        public void OnLeavePressed()
        {
            if (session != null)
            {
                session.Leave();
            }

            _roleBoard = null;
            Refresh();
        }

        public void Refresh()
        {
            if (session == null)
            {
                return;
            }

            bool offline = session.Mode
                == NetworkSessionController.SessionMode.Offline;
            NetworkRoleBoard board = ResolveRoleBoard();
            bool ready = session.IsSessionReady
                && board != null
                && board.IsAssigned;

            if (statusLabel != null)
            {
                statusLabel.text = string.IsNullOrEmpty(session.LastStatus)
                    ? "호스트로 시작하거나 상대 IP로 접속하세요."
                    : session.LastStatus;
            }

            if (roleLabel != null)
            {
                roleLabel.text = ready
                    ? board.LocalRole == PlayerRole.Police
                        ? "내 역할: 👮 경찰"
                        : "내 역할: 🕵 도둑"
                    : "내 역할: 대기 중";
            }

            SetInteractable(hostButton, offline);
            SetInteractable(joinButton, offline);
            SetInteractable(leaveButton, !offline);
            SetInteractable(swapRoleButton, ready);
            // Only the host may start, so a client cannot pull the other player
            // into a match they have not agreed to.
            SetInteractable(
                startMatchButton,
                ready
                && session.Mode
                   == NetworkSessionController.SessionMode.Host);

            if (joinAddressField != null)
            {
                joinAddressField.interactable = offline;
            }

            if (portField != null)
            {
                portField.interactable = offline;
            }

            RefreshRoomList(offline);
        }

        private NetworkRoleBoard ResolveRoleBoard()
        {
            if (_roleBoard == null)
            {
                _roleBoard = Object.FindFirstObjectByType<
                    NetworkRoleBoard>();
            }

            return _roleBoard;
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null)
            {
                button.interactable = value;
            }
        }

        /// <summary>
        /// Buttons are wired here, at runtime, not by the scene builder.
        ///
        /// <c>Button.onClick.AddListener</c> from an editor script registers a
        /// non-persistent listener, which is dropped when the scene is saved.
        /// The built player then showed a lobby whose buttons did nothing, so no
        /// session was ever created no matter what IP was typed. Wiring in
        /// OnEnable is what <see cref="SceneNavigationButton"/> already does and
        /// it survives into the build.
        /// </summary>
        private void WireButtons(bool add)
        {
            Bind(hostButton, OnHostPressed, add);
            Bind(joinButton, OnJoinPressed, add);
            Bind(swapRoleButton, OnSwapRolePressed, add);
            Bind(startMatchButton, OnStartMatchPressed, add);
            Bind(leaveButton, OnLeavePressed, add);

            // Bound by slot, not by room: the slot's meaning is resolved at
            // click time from the same list the labels were drawn from, so a
            // room list that refreshed between draw and click cannot make a
            // button join a different host than the one it shows.
            //
            // RemoveAllListeners rather than the Bind helper above, because each
            // closure here is a distinct delegate instance that RemoveListener
            // could not match; binding twice would otherwise stack up a listener
            // per enable cycle. These buttons have no other listeners.
            for (int index = 0; index < roomButtons.Length; index++)
            {
                Button button = roomButtons[index];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                if (!add)
                {
                    continue;
                }

                int slot = index;
                button.onClick.AddListener(() => JoinRoomSlot(slot));
            }
        }

        private void JoinRoomSlot(int slot)
        {
            if (slot < 0 || slot >= _boundRooms.Count)
            {
                return;
            }

            JoinRoom(_boundRooms[slot]);
        }

        private static void Bind(
            Button button,
            UnityEngine.Events.UnityAction action,
            bool add)
        {
            if (button == null)
            {
                return;
            }

            // Removed first either way: a scene builder that ran in the editor
            // may have left a live listener behind in play mode, and binding
            // twice would fire every press twice.
            button.onClick.RemoveListener(action);
            if (add)
            {
                button.onClick.AddListener(action);
            }
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.StatusChanged += HandleStatusChanged;
            }

            WireButtons(true);
            RefreshAddresses();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StatusChanged -= HandleStatusChanged;
            }

            WireButtons(false);
        }

        private void HandleStatusChanged(string status)
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }
    }
}
