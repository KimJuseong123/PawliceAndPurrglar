using System.Collections.Generic;
using System.Text;
using PawliceAndPurrglar.Audio;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Integration.Network;
using PawliceAndPurrglar.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// The invite-code lobby: one player makes a room and reads out the six
    /// characters it produces, the other types them in, and once both are
    /// present they split the roles.
    ///
    /// It used to show this machine's IP address and take one to join. That
    /// works on one wifi and nowhere else — the browser this game now ships in
    /// cannot open a listening socket at all, and a home connection behind
    /// CGNAT has no address worth reading out even on the desktop build.
    ///
    /// Read only with respect to rules. It starts a session and reads the role
    /// board; it never assigns a role itself, because only the server may do
    /// that.
    ///
    /// The view is wired at prefab build time and the session at scene build
    /// time, so the prefab can be opened and inspected on its own.
    /// </summary>
    public sealed class NetworkLobbyPresenter : MonoBehaviour
    {
        /// <summary>
        /// How often the lobby re-reads the session while nothing has raised an
        /// event. Polled rather than driven purely by events because the LAN
        /// directory and the role board both change without notifying anyone,
        /// but at a fixed interval instead of every frame.
        /// </summary>
        private const float PollInterval = 0.15f;

        [SerializeField]
        private NetworkSessionController session;

        [SerializeField]
        private TMP_Text inviteNoteLabel;

        [SerializeField]
        private TMP_Text statusLabel;

        [SerializeField]
        private TMP_Text roleLabel;

        /// <summary>
        /// Shows the code when hosting, takes one when joining.
        ///
        /// One field for both because there is no state in which a player holds
        /// two codes, and two fields side by side invite the one mistake this
        /// screen can make — typing the other player's code into your own box
        /// and waiting for somebody who was never told where to go.
        /// </summary>
        [SerializeField]
        private TMP_InputField inviteCodeField;

        [SerializeField]
        private Button copyCodeButton;

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
        private TMP_Text roomListLabel;

        [SerializeField]
        private LobbyCharacterView characterView;

        /// <summary>
        /// Fixed slots rather than instantiated rows: the match is two players,
        /// so a handful of rooms is all a LAN will ever usefully show, and a
        /// fixed set costs no allocation on the refresh.
        /// </summary>
        [SerializeField]
        private Button[] roomButtons = new Button[0];

        [SerializeField]
        private TMP_Text[] roomLabels = new TMP_Text[0];

        private readonly List<LanRoom> _boundRooms = new();

        private NetworkRoleBoard _roleBoard;
        private float _nextPoll;
        private string _roomSignature = string.Empty;

        /// <summary>
        /// How long "복사했습니다" stays up.
        ///
        /// A copy leaves no trace anywhere on screen, so without a line saying
        /// it happened the button is indistinguishable from a dead one — which
        /// this lobby has shipped before. It expires because the message
        /// describes an event, and a permanent one would still be claiming a
        /// copy long after the player moved on.
        /// </summary>
        private const float CopyNoticeSeconds = 2.5f;

        private string _copyNotice = string.Empty;
        private float _copyNoticeUntil;

        /// <summary>
        /// Set while the presenter is rewriting the code box itself.
        ///
        /// Assigning <c>text</c> raises <c>onValueChanged</c> again, and the
        /// handler that folds the case assigns <c>text</c> — which is an
        /// infinite loop inside one keystroke, not a slow one.
        /// </summary>
        private bool _foldingCode;

        /// <summary>
        /// What the last refresh saw, so the three lobby sounds are raised on the
        /// change rather than on the state.
        ///
        /// <see cref="Refresh"/> runs about seven times a second and none of the
        /// things it reads are events — the role board and the session are both
        /// polled — so without an edge here the connect beep would repeat for as
        /// long as anybody sat in the lobby.
        ///
        /// The role is nullable rather than defaulted so that being handed
        /// <see cref="PlayerRole.Police"/> is distinguishable from not having been
        /// handed anything yet. Defaulted, the officer would never hear theirs.
        /// </summary>
        private bool _wasSessionReady;
        private PlayerRole? _announcedRole;

        /// <summary>
        /// The session this lobby actually talks to.
        ///
        /// Exposed so a caller can read the same object a button reached rather
        /// than whichever <see cref="NetworkSessionController"/> a scene-wide
        /// search turns up first — which, with a leftover from an earlier scene
        /// still alive, is not necessarily this one.
        /// </summary>
        public NetworkSessionController Session => session;

        public string InviteNoteText =>
            inviteNoteLabel != null ? inviteNoteLabel.text : string.Empty;
        public string StatusText =>
            statusLabel != null ? statusLabel.text : string.Empty;
        public string RoleText =>
            roleLabel != null ? roleLabel.text : string.Empty;

        /// <summary>
        /// What is in the code box right now — the code this machine is showing
        /// when hosting, or what the player has typed when joining.
        /// </summary>
        public string InviteCodeText =>
            inviteCodeField != null ? inviteCodeField.text : string.Empty;

        /// <summary>
        /// Wires everything that lives inside the lobby prefab. Called once when
        /// the prefab is built, so the references are serialised into the asset
        /// rather than reconstructed in the scene.
        /// </summary>
        public void ConfigureView(
            TMP_Text configuredInviteNote,
            TMP_Text configuredStatus,
            TMP_Text configuredRole,
            TMP_InputField configuredInviteCode,
            Button configuredCopyCode,
            Button configuredHost,
            Button configuredJoin,
            Button configuredSwap,
            Button configuredStart,
            Button configuredLeave)
        {
            inviteNoteLabel = configuredInviteNote;
            statusLabel = configuredStatus;
            roleLabel = configuredRole;
            inviteCodeField = configuredInviteCode;
            copyCodeButton = configuredCopyCode;
            hostButton = configuredHost;
            joinButton = configuredJoin;
            swapRoleButton = configuredSwap;
            startMatchButton = configuredStart;
            leaveButton = configuredLeave;
        }

        public void ConfigureRoomListView(
            TMP_Text configuredRoomListLabel,
            Button[] configuredRoomButtons,
            TMP_Text[] configuredRoomLabels)
        {
            roomListLabel = configuredRoomListLabel;
            roomButtons = configuredRoomButtons ?? new Button[0];
            roomLabels = configuredRoomLabels ?? new TMP_Text[0];
        }

        public void ConfigureCharacterView(LobbyCharacterView configuredView)
        {
            characterView = configuredView;
        }

        /// <summary>
        /// Supplies the scene's services. Separate from the view because the
        /// session lives in the scene and cannot be referenced from a prefab
        /// asset.
        /// </summary>
        public void ConfigureSession(
            NetworkSessionController configuredSession,
            LanRoomDirectory configuredDirectory)
        {
            session = configuredSession;
            roomDirectory = configuredDirectory;
            Refresh();
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

            string signature = BuildRoomSignature(offline);
            if (signature == _roomSignature)
            {
                return;
            }

            _roomSignature = signature;

            for (int index = 0; index < roomButtons.Length; index++)
            {
                Button button = roomButtons[index];
                if (button == null)
                {
                    continue;
                }

                bool used = index < _boundRooms.Count;
                if (button.gameObject.activeSelf != used)
                {
                    button.gameObject.SetActive(used);
                }

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
                    // Numbered rather than named. The advert carries the host's
                    // machine name and address, and neither belongs on screen;
                    // they go to the log instead, where they are still useful
                    // when a join fails.
                    roomLabels[index].text = room.IsFull
                        ? $"방 {index + 1} · 가득 참"
                        : $"방 {index + 1} · 참가 가능 "
                          + $"({room.PlayerCount}/"
                          + $"{NetworkSessionController.MaximumPlayers})";
                    GameLogger.Debug(
                        GameLogCategory.Network,
                        $"Lobby room {index + 1}: {room.Label} at "
                        + $"{room.Address}:{room.Port}, "
                        + $"{room.PlayerCount} player(s).");
                }
            }

            if (roomListLabel == null)
            {
                return;
            }

            if (!offline)
            {
                SetText(roomListLabel, "세션 진행 중");
                return;
            }

            // The empty case says nothing about the LAN on purpose. Discovery is
            // a shortcut for two desktops on one wifi and it is off entirely in
            // a browser; telling a player who is about to type a code that no
            // rooms were found on their network describes a mechanism they are
            // not using and reads as a failure.
            if (_boundRooms.Count == 0)
            {
                SetText(roomListLabel, "초대코드로 만나세요.");
                return;
            }

            SetText(
                roomListLabel,
                $"같은 네트워크에서 방 {_boundRooms.Count}개를 찾았습니다. 눌러서 참가하세요.");
        }

        private string BuildRoomSignature(bool offline)
        {
            var signature = new StringBuilder(offline ? "off" : "on");
            signature.Append(
                roomDirectory != null && roomDirectory.IsListening ? '+' : '-');
            foreach (LanRoom room in _boundRooms)
            {
                signature.Append('|');
                signature.Append(room.Address);
                signature.Append(':');
                signature.Append(room.Port);
                signature.Append('/');
                signature.Append(room.PlayerCount);
            }

            return signature.ToString();
        }

        /// <summary>
        /// Opens a room and shows the code it produced.
        ///
        /// Fire-and-forget rather than awaited, because a Unity button hands
        /// back nothing to await with. The work is still awaited inside
        /// <see cref="CreateRoomAsync"/>, where an exception can be caught —
        /// an <c>async void</c> that faults takes the exception somewhere
        /// nothing is listening, and the player sees a button that did nothing.
        /// </summary>
        public void OnHostPressed()
        {
            if (session == null || session.IsBusy)
            {
                return;
            }

            _ = CreateRoomAsync();
        }

        public void OnJoinPressed()
        {
            if (session == null || session.IsBusy)
            {
                return;
            }

            _ = JoinRoomAsync();
        }

        /// <summary>
        /// Puts the code on the clipboard so it can be pasted into whatever the
        /// two players are talking through.
        ///
        /// Runs inside the click on purpose: a browser refuses a clipboard
        /// write it cannot attribute to a user gesture, and refuses it without
        /// raising anything, so deferring this by even one frame would leave
        /// the lobby claiming a copy that never happened.
        /// </summary>
        public void OnCopyCodePressed()
        {
            string code = session != null ? session.InviteCode : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            _copyNotice = ClipboardBridge.Copy(code)
                ? "코드를 복사했습니다."
                : "복사 실패 · 직접 읽으세요";
            _copyNoticeUntil = Time.unscaledTime + CopyNoticeSeconds;
            Refresh();
        }

        private async System.Threading.Tasks.Task CreateRoomAsync()
        {
            try
            {
                await session.TryCreateRoomAsync();
            }
            catch (System.Exception exception)
            {
                GameLogger.Exception(
                    GameLogCategory.Network,
                    exception,
                    "Creating a room threw.");
            }

            if (this != null)
            {
                Refresh();
            }
        }

        private async System.Threading.Tasks.Task JoinRoomAsync()
        {
            try
            {
                await session.TryJoinRoomAsync(InviteCodeText);
            }
            catch (System.Exception exception)
            {
                GameLogger.Exception(
                    GameLogCategory.Network,
                    exception,
                    "Joining a room threw.");
            }

            if (this != null)
            {
                Refresh();
            }
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
            bool isHost = session.Mode
                == NetworkSessionController.SessionMode.Host;
            NetworkRoleBoard board = ResolveRoleBoard();
            bool ready = session.IsSessionReady
                && board != null
                && board.IsAssigned;
            bool localIsPolice = ready && board.LocalRole == PlayerRole.Police;

            AnnounceLobbyChanges(offline, ready, board);

            SetText(statusLabel, DescribeStatus(offline, ready, isHost));
            SetText(
                roleLabel,
                ready
                    ? localIsPolice ? "내 역할: 경찰" : "내 역할: 도둑"
                    : "내 역할: 대기 중");

            if (characterView != null)
            {
                characterView.Apply(ready, localIsPolice);
            }

            bool busy = session.IsBusy;
            SetInteractable(hostButton, offline && !busy);
            SetInteractable(joinButton, offline && !busy);
            SetInteractable(leaveButton, !offline);
            SetInteractable(swapRoleButton, ready);
            // Only the host may start, so a client cannot pull the other player
            // into a match they have not agreed to.
            SetInteractable(startMatchButton, ready && isHost);

            RefreshInviteCode(offline, isHost, busy);
            RefreshRoomList(offline);
        }

        /// <summary>
        /// Keeps the code box and the line above it agreeing with the session.
        ///
        /// The box is the same control in both directions, so which way it is
        /// pointing has to be visible without reading the label: hosting fills
        /// it and locks it, joining leaves it open and empty, and only a host
        /// with a code has anything to copy.
        /// </summary>
        private void RefreshInviteCode(bool offline, bool isHost, bool busy)
        {
            string sessionCode = session.InviteCode ?? string.Empty;
            bool showsOwnCode = !offline && sessionCode.Length > 0;

            if (inviteCodeField != null)
            {
                if (showsOwnCode && inviteCodeField.text != sessionCode)
                {
                    inviteCodeField.text = sessionCode;
                }

                bool editable = offline && !busy;
                if (inviteCodeField.interactable != editable)
                {
                    inviteCodeField.interactable = editable;
                }
            }

            SetInteractable(copyCodeButton, showsOwnCode && isHost);

            if (inviteNoteLabel == null)
            {
                return;
            }

            if (Time.unscaledTime < _copyNoticeUntil
                && _copyNotice.Length > 0)
            {
                SetText(inviteNoteLabel, _copyNotice);
                return;
            }

            _copyNotice = string.Empty;
            SetText(
                inviteNoteLabel,
                showsOwnCode && isHost
                    ? $"내 초대코드: {sessionCode}"
                    : showsOwnCode
                        ? $"{sessionCode} 방에 참가"
                        : "받은 코드를 입력하세요.");
        }

        /// <summary>
        /// The three things in the lobby worth hearing: the other player
        /// arriving, the other player going, and which side you are on.
        ///
        /// Here rather than in the network layer because the lobby is the only
        /// place that knows all three at once, and because the rest of this
        /// screen is already driven by reading the same two objects. The role
        /// pair repeats on a swap, which is the point — the swap button gives no
        /// other confirmation that it worked.
        ///
        /// Nothing plays while offline. Leaving a session drops the ready flag
        /// exactly the way losing the other player does, and a farewell beep for
        /// your own decision to leave reads as an error.
        /// </summary>
        private void AnnounceLobbyChanges(
            bool offline,
            bool ready,
            NetworkRoleBoard board)
        {
            bool sessionReady = !offline && session.IsSessionReady;
            if (sessionReady && !_wasSessionReady)
            {
                GameSoundService.Request(GameSoundId.PeerJoined);
            }
            else if (!sessionReady && _wasSessionReady && !offline)
            {
                GameSoundService.Request(GameSoundId.PeerLeft);
            }

            _wasSessionReady = sessionReady;

            if (!ready || board == null)
            {
                // Forgotten on the way out, so rejoining announces the role again
                // instead of treating the old one as still current.
                _announcedRole = null;
                return;
            }

            PlayerRole role = board.LocalRole;
            if (_announcedRole == role)
            {
                return;
            }

            _announcedRole = role;
            GameSoundService.Request(
                role == PlayerRole.Police
                    ? GameSoundId.RoleAssignedPolice
                    : GameSoundId.RoleAssignedThief);
        }

        /// <summary>
        /// One line that always says what to do next.
        ///
        /// A disabled button with no explanation is the most common way a lobby
        /// strands someone, so the reason a control is unavailable is the
        /// message rather than a footnote under it.
        /// </summary>
        private string DescribeStatus(bool offline, bool ready, bool isHost)
        {
            if (offline)
            {
                return string.IsNullOrEmpty(session.LastStatus)
                    ? "방을 만들거나 받은 코드로 입장하세요."
                    : session.LastStatus;
            }

            if (!ready)
            {
                return isHost
                    ? "상대 플레이어를 기다리고 있습니다."
                    : "호스트에 연결하는 중입니다.";
            }

            return isHost
                ? "상대 플레이어와 연결되었습니다. 게임 시작을 누르세요."
                : "상대 플레이어와 연결되었습니다. 호스트가 시작하기를 기다립니다.";
        }

        private NetworkRoleBoard ResolveRoleBoard()
        {
            if (_roleBoard == null)
            {
                _roleBoard = FindFirstObjectByType<NetworkRoleBoard>();
            }

            return _roleBoard;
        }

        private static void SetText(TMP_Text label, string value)
        {
            // Compared before assigning: the lobby polls, and handing TMP the
            // same string still marks the canvas dirty and re-lays the text out.
            if (label != null && label.text != value)
            {
                label.text = value;
            }
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null && button.interactable != value)
            {
                button.interactable = value;
            }
        }

        /// <summary>
        /// Buttons are wired here, at runtime, not by the prefab builder.
        ///
        /// <c>Button.onClick.AddListener</c> from an editor script registers a
        /// non-persistent listener, which is dropped when the asset is saved.
        /// The built player then showed a lobby whose buttons did nothing, so no
        /// session was ever created no matter what IP was typed. Wiring in
        /// OnEnable is what <see cref="SceneNavigationButton"/> already does and
        /// it survives into the build.
        /// </summary>
        private void WireButtons(bool add)
        {
            Bind(hostButton, OnHostPressed, add);
            Bind(joinButton, OnJoinPressed, add);
            Bind(copyCodeButton, OnCopyCodePressed, add);
            Bind(swapRoleButton, OnSwapRolePressed, add);

            // On the change rather than on the poll. The refresh runs about
            // seven times a second, so folding the case there means a lower
            // case letter is visible for up to 150ms after it is typed and,
            // worse, a player who presses 방 입장 inside that window sends the
            // unfolded text. Bound here so what is on screen and what is sent
            // are the same string at every instant.
            if (inviteCodeField != null)
            {
                inviteCodeField.onValueChanged.RemoveListener(
                    OnInviteCodeChanged);
                if (add)
                {
                    inviteCodeField.onValueChanged.AddListener(
                        OnInviteCodeChanged);
                }
            }
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

        /// <summary>
        /// Folds what was typed to upper case, in place.
        ///
        /// Relay issues upper case; a phone keyboard offers lower, and a code
        /// that is right but rejected for its case is a failure the player has
        /// no way to see. Done here rather than only when 방 입장 is pressed so
        /// the box shows the player the exact string that will be sent.
        /// </summary>
        private void OnInviteCodeChanged(string value)
        {
            if (_foldingCode || inviteCodeField == null)
            {
                return;
            }

            string raised = value.ToUpperInvariant();
            if (raised == value)
            {
                return;
            }

            _foldingCode = true;
            try
            {
                int caret = inviteCodeField.caretPosition;
                inviteCodeField.text = raised;
                inviteCodeField.caretPosition = caret;
            }
            finally
            {
                _foldingCode = false;
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
            // The scene wires these; falling back to a search keeps a lobby that
            // was dropped into a scene by hand from failing silently, and the
            // warning says which case happened.
            if (session == null)
            {
                session = FindFirstObjectByType<NetworkSessionController>();
                if (session == null)
                {
                    GameLogger.Error(
                        GameLogCategory.Network,
                        "Lobby has no NetworkSessionController. Nothing in the "
                        + "lobby can start or join a session.");
                }
                else
                {
                    GameLogger.Warning(
                        GameLogCategory.Network,
                        "Lobby session reference was not serialised; found one "
                        + "in the scene instead.");
                }
            }

            if (roomDirectory == null)
            {
                roomDirectory = FindFirstObjectByType<LanRoomDirectory>();
            }

            if (session != null)
            {
                session.StatusChanged += HandleStatusChanged;
            }

            WireButtons(true);
            _roomSignature = string.Empty;

            // Started as the lobby opens, not when 방 만들기 is pressed. Signing
            // in is two round trips that do not depend on which button the
            // player chooses, and doing them while they are still reading the
            // screen is time nobody spends waiting.
            if (session != null)
            {
                session.Prewarm();
            }

            Refresh();
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
            // Polled on an interval rather than every frame. The session and the
            // room directory both change without raising anything, but a lobby
            // that rebuilds its strings sixty times a second allocates for no
            // reason.
            if (Time.unscaledTime < _nextPoll)
            {
                return;
            }

            _nextPoll = Time.unscaledTime + PollInterval;
            Refresh();
        }
    }
}
