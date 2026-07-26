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

            LocalPlayerRoleSelector.OverrideRole(board.LocalRole);
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

        private void OnEnable()
        {
            if (session != null)
            {
                session.StatusChanged += HandleStatusChanged;
            }

            RefreshAddresses();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StatusChanged -= HandleStatusChanged;
            }
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
