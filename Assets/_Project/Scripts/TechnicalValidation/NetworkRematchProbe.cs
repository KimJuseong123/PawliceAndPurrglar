using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Integration.Network;
using PawliceAndPurrglar.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.TechnicalValidation
{
    /// <summary>
    /// NET-008. Checks that one player's rematch press restarts the match on both
    /// machines.
    ///
    /// Lives on the persistent NetworkManager object rather than in a scene,
    /// because the press happens on the result screen and the restart lands in
    /// the match scene: no single scene sees the whole thing.
    ///
    /// Only runs for <c>-netScenario rematch</c>, so every other verification run
    /// and every normal launch are unaffected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkRematchProbe : MonoBehaviour
    {
        private const string ModeArgument = "-netLobby";
        private const string ScenarioArgument = "-netScenario";

        /// <summary>
        /// Time on the result screen before the client asks. Long enough for the
        /// networked scene load to have settled on both sides.
        /// </summary>
        private const float RequestAfterSeconds = 2.5f;

        /// <summary>
        /// Time after the request to confirm where both machines ended up.
        /// </summary>
        private const float ConfirmAfterSeconds = 6f;

        private string _mode = string.Empty;
        private bool _active;
        private float _onResultFor;
        private float _afterRequest;
        private bool _requested;
        private bool _written;
        private bool _reachedResult;
        private bool _returnedToGame;

        private void Awake()
        {
            IReadOnlyList<string> args =
                Environment.GetCommandLineArgs();
            _mode = ReadValue(args, ModeArgument)?.ToLowerInvariant()
                ?? string.Empty;
            string scenario =
                ReadValue(args, ScenarioArgument)?.ToLowerInvariant()
                ?? string.Empty;
            _active = !string.IsNullOrEmpty(_mode)
                && scenario == "rematch";
            enabled = _active;
        }

        private void Update()
        {
            if (!_active || _written)
            {
                return;
            }

            string active = SceneManager.GetActiveScene().name;
            bool onResult =
                active == GameSceneCatalog.GetName(GameSceneId.Result);
            bool onGame =
                active == GameSceneCatalog.GetName(GameSceneId.Game);

            if (onResult)
            {
                _reachedResult = true;
                _onResultFor += Time.unscaledDeltaTime;
            }

            // Returning to the match after the press is the whole point, so it
            // is latched rather than sampled at the end.
            if (_reachedResult && _requested && onGame)
            {
                _returnedToGame = true;
            }

            if (!_requested
                && onResult
                && _onResultFor >= RequestAfterSeconds)
            {
                _requested = true;
                // Only the client presses. If the host pressed too, its own
                // return to the match would prove nothing about the request
                // having travelled; this way the host restarting can only be
                // caused by the client's message.
                if (_mode == "client")
                {
                    NetworkSceneBridge.TryRequestRematch();
                    GameLogger.Info(
                        GameLogCategory.Network,
                        "Rematch probe asked for a rematch.",
                        this);
                }
            }

            if (!_requested)
            {
                return;
            }

            _afterRequest += Time.unscaledDeltaTime;
            if (_afterRequest >= ConfirmAfterSeconds)
            {
                Write();
            }
        }

        private void Write()
        {
            _written = true;
            NetworkRematchCoordinator coordinator =
                FindFirstObjectByType<NetworkRematchCoordinator>();

            var json = new StringBuilder();
            json.AppendLine("{");
            Append(json, "task", "NET-008");
            Append(json, "instance", _mode);
            Append(
                json,
                "activeScene",
                SceneManager.GetActiveScene().name);
            AppendBool(json, "reachedResult", _reachedResult);
            AppendBool(json, "requested", _requested);
            AppendBool(json, "returnedToGame", _returnedToGame);
            AppendNumber(
                json,
                "hostReceivedRequests",
                coordinator != null
                    ? coordinator.ReceivedRequestCount
                    : -1);
            json.AppendLine(
                "  \"passed\": "
                + (_reachedResult && _returnedToGame
                    ? "true"
                    : "false"));
            json.AppendLine("}");

            string directory = Application.persistentDataPath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory,
                $"net-rematch-{_mode}-result.json");
            File.WriteAllText(path, json.ToString());
            GameLogger.Info(
                GameLogCategory.Network,
                $"Rematch probe wrote '{path}'.",
                this);
            Application.Quit(0);
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
