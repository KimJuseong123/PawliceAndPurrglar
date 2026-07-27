using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.Logging;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.TechnicalValidation
{
    /// <summary>
    /// Drives the lobby from the command line so the connection and the role
    /// split can be checked without a human watching two windows.
    ///
    ///     PawsAndLoot.exe -netLobby host   -netPort 7979
    ///     PawsAndLoot.exe -netLobby client -netAddress 127.0.0.1 -netPort 7979
    ///
    /// Each process writes its own JSON, so comparing the two files proves both
    /// machines agreed on who is police. Inert without the flag, so a normal
    /// launch still shows the lobby for a person to use.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkLobbyProbe : MonoBehaviour
    {
        private const string ModeArgument = "-netLobby";
        private const string AddressArgument = "-netAddress";
        private const string PortArgument = "-netPort";
        private const string SwapArgument = "-netSwapRoles";
        private const string MatchArgument = "-netMatchSeconds";

        /// <summary>
        /// How the session is started.
        ///
        /// <c>api</c> calls the session controller directly, which is what every
        /// earlier run did. That is exactly why a lobby whose buttons were wired
        /// to nothing passed every automated check and still failed for a real
        /// player: no probe had ever pressed a button.
        ///
        /// <c>ui</c> presses the host and join buttons. <c>room</c> presses host
        /// on one side and, on the other, the LAN room entry that appears once
        /// the host starts advertising.
        /// </summary>
        private const string JoinModeArgument = "-netJoinMode";

        [SerializeField]
        private NetworkSessionController session;

        [SerializeField, Min(2f)]
        private float timeoutSeconds = 20f;

        [SerializeField, Min(0.5f)]
        private float settleSeconds = 3f;

        private string _mode = string.Empty;
        private bool _requestSwap;
        private float _elapsed;
        private float _readyFor;
        private bool _written;

        private string _port = string.Empty;
        private string _address = string.Empty;
        private bool _startRequested;
        private bool _everReady;
        private bool _continueIntoMatch;
        private string _observedRole = "Unassigned";
        private ulong _observedPoliceId;
        private string _joinMode = "api";
        private string _pressedControl = string.Empty;
        private bool _sawRoomEntry;

        private void Awake()
        {
            IReadOnlyList<string> args =
                Environment.GetCommandLineArgs();
            _mode = ReadValue(args, ModeArgument)?.ToLowerInvariant()
                ?? string.Empty;
            if (string.IsNullOrEmpty(_mode))
            {
                enabled = false;
                return;
            }

            _requestSwap = HasFlag(args, SwapArgument);
            // With a match duration requested, the lobby hands over to the
            // match probe instead of quitting here.
            _continueIntoMatch =
                ReadValue(args, MatchArgument) != null;
            _port = ReadValue(args, PortArgument)
                ?? NetworkSessionController.DefaultPort.ToString();
            _address = ReadValue(args, AddressArgument)
                ?? LocalAddressProvider.LoopbackAddress;
            _joinMode =
                ReadValue(args, JoinModeArgument)?.ToLowerInvariant()
                ?? "api";
        }

        /// <summary>
        /// Starts through the lobby's own buttons, so the wiring a player
        /// depends on is the wiring under test.
        ///
        /// Returns false while the probe is still waiting — for a room press
        /// that means the host's advert has not arrived yet — so the caller
        /// simply tries again next frame until the timeout.
        /// </summary>
        private bool TryBeginThroughInterface()
        {
            NetworkLobbyPresenter presenter =
                FindFirstObjectByType<NetworkLobbyPresenter>();
            if (presenter == null)
            {
                Write(false, "No NetworkLobbyPresenter in the lobby scene.");
                return false;
            }

            if (_mode == "host")
            {
                Button hostButton = FindButton("Host Button");
                if (hostButton == null)
                {
                    Write(false, "Lobby has no reachable host button.");
                    return false;
                }

                _pressedControl = "Host Button";
                hostButton.onClick.Invoke();
                return true;
            }

            if (_joinMode == "room")
            {
                // The first active room slot is the host's advert. Waiting for
                // it to appear is the property under test.
                for (int index = 0; index < 4; index++)
                {
                    Button slot = FindButton($"Room Slot {index}");
                    if (slot == null
                        || !slot.gameObject.activeInHierarchy
                        || !slot.interactable)
                    {
                        continue;
                    }

                    _pressedControl = $"Room Slot {index}";
                    _sawRoomEntry = true;
                    slot.onClick.Invoke();
                    return true;
                }

                return false;
            }

            InputField addressField = FindField("Join Address");
            InputField portField = FindField("Port");
            if (addressField != null)
            {
                addressField.text = _address;
            }

            if (portField != null)
            {
                portField.text = _port;
            }

            Button joinButton = FindButton("Join Button");
            if (joinButton == null)
            {
                Write(false, "Lobby has no reachable join button.");
                return false;
            }

            _pressedControl = "Join Button";
            joinButton.onClick.Invoke();
            return true;
        }

        private static Button FindButton(string objectName)
        {
            foreach (Button button in
                FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == objectName)
                {
                    return button;
                }
            }

            return null;
        }

        private static InputField FindField(string objectName)
        {
            foreach (InputField field in
                FindObjectsByType<InputField>(FindObjectsSortMode.None))
            {
                if (field.name == objectName)
                {
                    return field;
                }
            }

            return null;
        }

        /// <summary>
        /// Starting is deferred out of Awake because NetworkManager sets itself
        /// up in its own Awake, and calling StartHost or StartClient before
        /// that throws inside the package.
        /// </summary>
        private bool TryBeginSession()
        {
            if (session == null)
            {
                session = FindFirstObjectByType<
                    NetworkSessionController>();
            }

            if (session == null)
            {
                Write(false, "No NetworkSessionController in the scene.");
                return false;
            }

            if (Unity.Netcode.NetworkManager.Singleton == null)
            {
                return false;
            }

            if (_joinMode != "api")
            {
                if (!TryBeginThroughInterface())
                {
                    // Not an error yet: a room press waits for the advert.
                    return false;
                }

                _startRequested = true;
                GameLogger.Info(
                    GameLogCategory.Network,
                    $"Lobby probe '{_mode}' pressed "
                    + $"'{_pressedControl}'.",
                    this);
                return true;
            }

            _startRequested = true;
            bool started = _mode == "host"
                ? session.TryStartHost(_port)
                : session.TryJoin(_address, _port);
            GameLogger.Info(
                GameLogCategory.Network,
                $"Lobby probe '{_mode}' start={started}.",
                this);
            if (!started)
            {
                Write(false, $"Failed to start as '{_mode}'.");
            }

            return started;
        }

        private void Update()
        {
            if (_written)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;

            if (!_startRequested)
            {
                // Give the client a moment so the host is listening first.
                float delay = _mode == "host" ? 0.5f : 1.5f;
                if (_elapsed < delay)
                {
                    return;
                }

                TryBeginSession();
                return;
            }

            NetworkRoleBoard board =
                FindFirstObjectByType<NetworkRoleBoard>();
            bool ready = session != null
                && session.IsSessionReady
                && board != null
                && board.IsAssigned;

            if (ready && !_everReady)
            {
                // Latched: the property under test is that the session reached
                // a ready state with roles assigned. Whichever process quits
                // first must not make the other one report a failure.
                _everReady = true;
                _observedRole = board.LocalRole.ToString();
                _observedPoliceId = board.PoliceClientId;
            }

            if (ready)
            {
                // Hold briefly so the role variable has certainly replicated
                // before either side records what it sees.
                _readyFor += Time.unscaledDeltaTime;
                if (_requestSwap
                    && _mode == "client"
                    && _readyFor > settleSeconds * 0.5f
                    && board.LocalRole == PlayerRole.Thief)
                {
                    board.RequestSwapRolesRpc();
                    _requestSwap = false;
                }

                if (_readyFor >= settleSeconds)
                {
                    if (_continueIntoMatch)
                    {
                        // Only the host may start, so the client simply waits
                        // to be brought along by the server's scene load.
                        if (_mode == "host")
                        {
                            board.TryCommitRoles();
                            Core.GameSceneLoader.Load(
                                Core.GameSceneId.Game);
                        }

                        enabled = false;
                        return;
                    }

                    Write(true, "Session ready and roles assigned.");
                    return;
                }
            }

            if (_elapsed >= timeoutSeconds)
            {
                Write(
                    _everReady,
                    _everReady
                        ? "Session was ready; the peer closed first."
                        : "Timed out before both players were ready.");
            }
        }

        private void Write(bool passed, string detail)
        {
            _written = true;
            NetworkRoleBoard board =
                FindFirstObjectByType<NetworkRoleBoard>();

            var json = new StringBuilder();
            json.AppendLine("{");
            Append(json, "task", "NET-LOBBY");
            Append(json, "instance", _mode);
            Append(json, "detail", detail);
            Append(
                json,
                "sessionMode",
                session != null
                    ? session.Mode.ToString()
                    : "None");
            Append(
                json,
                "status",
                session != null ? session.LastStatus : string.Empty);
            AppendNumber(
                json,
                "connectedPlayers",
                session != null ? session.ConnectedPlayerCount : 0);
            AppendBool(json, "rolesAssigned", _everReady);
            // Which path started the session, and what it actually pressed.
            // "api" here means the interface was never exercised.
            Append(json, "joinMode", _joinMode);
            Append(json, "pressedControl", _pressedControl);
            AppendBool(json, "sawRoomEntry", _sawRoomEntry);
            Append(json, "localRole", _observedRole);
            AppendNumber(json, "policeClientId", _observedPoliceId);
            Append(
                json,
                "localAddresses",
                string.Join(
                    " ",
                    LocalAddressProvider.GetIPv4Addresses()));
            json.AppendLine(
                $"  \"passed\": {(passed ? "true" : "false")}");
            json.AppendLine("}");

            string directory = Application.persistentDataPath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory,
                $"net-lobby-{_mode}-result.json");
            File.WriteAllText(path, json.ToString());
            GameLogger.Info(
                GameLogCategory.Network,
                $"Lobby probe wrote '{path}' passed={passed}.",
                this);
            Application.Quit(passed ? 0 : 1);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            string flag)
        {
            for (int index = 0; index < args.Count - 1; index++)
            {
                if (string.Equals(
                        args[index],
                        flag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static bool HasFlag(
            IReadOnlyList<string> args,
            string flag)
        {
            foreach (string arg in args)
            {
                if (string.Equals(
                        arg,
                        flag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Append(
            StringBuilder json,
            string key,
            string value)
        {
            json.AppendLine($"  \"{key}\": \"{value}\",");
        }

        private static void AppendBool(
            StringBuilder json,
            string key,
            bool value)
        {
            json.AppendLine(
                $"  \"{key}\": {(value ? "true" : "false")},");
        }

        private static void AppendNumber(
            StringBuilder json,
            string key,
            double value)
        {
            json.AppendLine(
                $"  \"{key}\": "
                + value.ToString("0.###", CultureInfo.InvariantCulture)
                + ",");
        }
    }
}
