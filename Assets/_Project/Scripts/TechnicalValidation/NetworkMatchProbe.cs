using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    /// <summary>
    /// Verifies NET-003 and NET-004 in the match scene without a human watching
    /// two windows.
    ///
    /// Activated by the same <c>-netLobby</c> run once the match scene has
    /// loaded. Each process records the match clock, both player positions and
    /// which role it controls, so comparing the two files shows whether the
    /// screens agree.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkMatchProbe : MonoBehaviour
    {
        private const string ModeArgument = "-netLobby";
        private const string SampleArgument = "-netMatchSeconds";

        [SerializeField, Min(1f)]
        private float sampleSeconds = 6f;

        private string _mode = string.Empty;
        private float _elapsed;
        private bool _written;
        private float _driveTimer;
        private Vector2 _driveInput = Vector2.right;

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

            string configured = ReadValue(args, SampleArgument);
            if (float.TryParse(
                    configured,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsed))
            {
                sampleSeconds = Mathf.Max(1f, parsed);
            }
        }

        private void Update()
        {
            if (_written)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            DriveMovement();

            if (_elapsed >= sampleSeconds)
            {
                Write();
            }
        }

        /// <summary>
        /// Feeds a changing input so the character actually moves, which is what
        /// makes a position comparison meaningful. Sent through the same bridge
        /// path a player's keys use.
        /// </summary>
        private void DriveMovement()
        {
            NetworkRoleBoard board =
                FindFirstObjectByType<NetworkRoleBoard>();
            if (board == null || !board.IsAssigned)
            {
                return;
            }

            _driveTimer += Time.unscaledDeltaTime;
            if (_driveTimer > 1.5f)
            {
                _driveTimer = 0f;
                _driveInput = new Vector2(
                    -_driveInput.y,
                    _driveInput.x);
            }

            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(
                    FindObjectsSortMode.None))
            {
                if (link.Role == board.LocalRole && link.IsSpawned)
                {
                    link.SubmitInputRpc(_driveInput, false);
                }
            }
        }

        private void Write()
        {
            _written = true;
            MatchRuntimeState runtime =
                FindFirstObjectByType<MatchRuntimeState>();
            NetworkRoleBoard board =
                FindFirstObjectByType<NetworkRoleBoard>();

            Vector3 police = Vector3.zero;
            Vector3 thief = Vector3.zero;
            int linkCount = 0;
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(
                    FindObjectsSortMode.None))
            {
                linkCount++;
                if (link.Role == PlayerRole.Police)
                {
                    police = link.transform.position;
                }
                else
                {
                    thief = link.transform.position;
                }
            }

            var json = new StringBuilder();
            json.AppendLine("{");
            Append(json, "task", "NET-003-004");
            Append(json, "instance", _mode);
            Append(
                json,
                "matchState",
                runtime != null
                    ? runtime.CurrentState.ToString()
                    : "None");
            AppendNumber(
                json,
                "remainingSeconds",
                runtime != null ? runtime.RemainingMatchSeconds : -1f);
            AppendBool(
                json,
                "matchRemoteControlled",
                runtime != null && runtime.IsRemoteControlled);
            Append(
                json,
                "localRole",
                board != null && board.IsAssigned
                    ? board.LocalRole.ToString()
                    : "Unassigned");
            AppendNumber(json, "playerLinks", linkCount);
            Append(json, "policePosition", Format(police));
            Append(json, "thiefPosition", Format(thief));
            AppendBool(
                json,
                "passed",
                runtime != null && linkCount == 2);
            json.AppendLine("  \"end\": true");
            json.AppendLine("}");

            string directory = Application.persistentDataPath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory,
                $"net-match-{_mode}-result.json");
            File.WriteAllText(path, json.ToString());
            GameLogger.Info(
                GameLogCategory.Network,
                $"Match probe wrote '{path}'.",
                this);
            Application.Quit(0);
        }

        private static string Format(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.00} {1:0.00} {2:0.00}",
                value.x,
                value.y,
                value.z);
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
            float value)
        {
            json.AppendLine(
                $"  \"{key}\": "
                + value.ToString("0.###", CultureInfo.InvariantCulture)
                + ",");
        }
    }
}
