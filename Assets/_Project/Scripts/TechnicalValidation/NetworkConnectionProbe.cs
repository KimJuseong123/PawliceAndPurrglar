using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    public sealed class NetworkConnectionProbe : MonoBehaviour
    {
        private const float ScreenshotAtSeconds = 5f;

        [SerializeField]
        private NetworkManager networkManager;

        private readonly Dictionary<ulong, Vector3> _firstPositions = new();
        private NetworkValidationResult _result;
        private string _mode;
        private string _instanceName;
        private string _resultPath;
        private string _screenshotPath;
        private float _startedAt;
        private float _disconnectAfter;
        private float _quitAfter;
        private bool _disconnectRequested;
        private bool _screenshotCaptured;
        private bool _resultDirty;

        public NetworkManager NetworkManager
        {
            get => networkManager;
            set => networkManager = value;
        }

        private void Start()
        {
            if (networkManager == null)
            {
                throw new InvalidOperationException(
                    "NetworkConnectionProbe requires a NetworkManager.");
            }

            Application.runInBackground = true;
            Screen.SetResolution(960, 540, FullScreenMode.Windowed);

            string[] arguments = Environment.GetCommandLineArgs();
            _mode = GetArgument(arguments, "-netMode", "host").ToLowerInvariant();
            _instanceName = GetArgument(arguments, "-netInstance", _mode);
            string address = GetArgument(arguments, "-netAddress", "127.0.0.1");
            ushort port = ParseUShort(
                GetArgument(arguments, "-netPort", "7777"),
                7777);
            _disconnectAfter = ParseFloat(
                GetArgument(arguments, "-netDisconnectAfter", "-1"),
                -1f);
            _quitAfter = ParseFloat(
                GetArgument(
                    arguments,
                    "-netQuitAfter",
                    _mode == "client" ? "8" : "11"),
                _mode == "client" ? 8f : 11f);

            _resultPath = Path.Combine(
                Application.persistentDataPath,
                $"net-001-{_instanceName}-result.json");
            _screenshotPath = Path.Combine(
                Application.persistentDataPath,
                $"net-001-{_instanceName}-screenshot.png");
            _startedAt = Time.unscaledTime;
            _result = new NetworkValidationResult
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                sceneName = UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene()
                    .name,
                mode = _mode,
                instanceName = _instanceName,
                address = address,
                port = port,
                screenshotPath = _screenshotPath,
                status = "Starting"
            };

            UnityTransport transport =
                networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                throw new InvalidOperationException(
                    "NET-001 requires UnityTransport on NetworkManager.");
            }

            transport.SetConnectionData(
                address,
                port,
                _mode == "host" ? "0.0.0.0" : null);

            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            bool started = _mode switch
            {
                "host" => networkManager.StartHost(),
                "client" => networkManager.StartClient(),
                _ => false
            };

            _result.startSucceeded = started;
            _result.status = started
                ? $"{_mode} started"
                : $"failed to start {_mode}";
            _resultDirty = true;
            WriteResult();
        }

        private void Update()
        {
            if (_result == null)
            {
                return;
            }

            ObserveNetworkPlayers();

            float elapsed = Time.unscaledTime - _startedAt;
            if (!_screenshotCaptured && elapsed >= ScreenshotAtSeconds)
            {
                _screenshotCaptured = true;
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                _resultDirty = true;
            }

            if (!_disconnectRequested
                && _disconnectAfter >= 0f
                && elapsed >= _disconnectAfter)
            {
                _disconnectRequested = true;
                _result.localShutdownRequested = true;
                networkManager.Shutdown();
                _resultDirty = true;
            }

            if (_disconnectRequested
                && !networkManager.IsListening
                && !_result.localShutdownHandled)
            {
                _result.localShutdownHandled = true;
                _result.status = "Local shutdown handled";
                _resultDirty = true;
            }

            if (_resultDirty)
            {
                WriteResult();
            }

            if (elapsed >= _quitAfter)
            {
                if (networkManager.IsListening)
                {
                    _result.localShutdownRequested = true;
                    networkManager.Shutdown();
                    _result.localShutdownHandled = !networkManager.IsListening;
                }

                WriteResult();
                Application.Quit();
            }
        }

        private void ObserveNetworkPlayers()
        {
            TechnicalNetworkPlayer[] players =
                FindObjectsByType<TechnicalNetworkPlayer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            TechnicalNetworkPlayer[] spawned = players
                .Where(player => player.IsSpawned)
                .ToArray();

            _result.visiblePlayerCount =
                Mathf.Max(_result.visiblePlayerCount, spawned.Length);
            if (spawned.Length >= 2)
            {
                _result.sawTwoPlayers = true;
                _result.sawDistinctOwners =
                    spawned.Select(player => player.OwnerClientId)
                        .Distinct()
                        .Count() >= 2;

                float greatestSeparation = 0f;
                foreach (TechnicalNetworkPlayer player in spawned)
                {
                    greatestSeparation = Mathf.Max(
                        greatestSeparation,
                        Vector3.Distance(
                            spawned[0].NetworkPosition,
                            player.NetworkPosition));

                    if (!_firstPositions.TryGetValue(
                            player.OwnerClientId,
                            out Vector3 firstPosition))
                    {
                        _firstPositions[player.OwnerClientId] =
                            player.NetworkPosition;
                        continue;
                    }

                    if (Vector3.Distance(firstPosition, player.NetworkPosition) > 0.35f)
                    {
                        _result.observedSynchronizedMovement = true;
                    }
                }

                _result.positionsWereDistinct |= greatestSeparation > 2f;
                _resultDirty = true;
            }

            if (networkManager.IsClient && networkManager.IsConnectedClient)
            {
                _result.localClientConnected = true;
                _result.localClientId = networkManager.LocalClientId;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            _result.connectedCallbackCount++;
            if (clientId == networkManager.LocalClientId)
            {
                _result.localClientConnected = true;
                _result.localClientId = clientId;
            }

            _result.status = $"Client {clientId} connected";
            _resultDirty = true;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            _result.disconnectedCallbackCount++;
            if (_mode == "host" && clientId != networkManager.LocalClientId)
            {
                _result.remoteDisconnectObserved = true;
            }

            _result.disconnectReason = networkManager.DisconnectReason;
            _result.status = $"Client {clientId} disconnected";
            _resultDirty = true;
        }

        private void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        private void OnApplicationQuit()
        {
            if (_result != null)
            {
                WriteResult();
            }
        }

        private void WriteResult()
        {
            bool disconnectPassed = _mode == "host"
                ? _result.remoteDisconnectObserved
                : _result.localShutdownHandled;
            _result.passed =
                _result.startSucceeded
                && _result.localClientConnected
                && _result.sawTwoPlayers
                && _result.sawDistinctOwners
                && _result.positionsWereDistinct
                && _result.observedSynchronizedMovement
                && disconnectPassed;

            File.WriteAllText(_resultPath, JsonUtility.ToJson(_result, true));
            _resultDirty = false;
        }

        private void OnGUI()
        {
            Rect panel = new(20f, 20f, 470f, 310f);
            GUI.Box(panel, GUIContent.none);

            GUIStyle titleStyle = new(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            GUIStyle bodyStyle = new(GUI.skin.label)
            {
                fontSize = 17
            };

            GUI.Label(new Rect(40f, 38f, 430f, 34f), "NET-001 LOCAL CONNECTION", titleStyle);
            GUI.Label(
                new Rect(40f, 82f, 430f, 28f),
                $"Instance: {_instanceName} / Mode: {_mode}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 114f, 430f, 28f),
                $"Status: {_result?.status ?? "Initializing"}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 154f, 430f, 28f),
                $"Visible players: {_result?.visiblePlayerCount ?? 0} / 2",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 186f, 430f, 28f),
                $"Distinct network owners: {YesNo(_result?.sawDistinctOwners)}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 218f, 430f, 28f),
                $"Synchronized movement: {YesNo(_result?.observedSynchronizedMovement)}",
                bodyStyle);
            bool? disconnectHandled = _mode == "host"
                ? _result?.remoteDisconnectObserved
                : _result?.localShutdownHandled;
            GUI.Label(
                new Rect(40f, 250f, 430f, 28f),
                $"Disconnect handled: {YesNo(disconnectHandled)}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 282f, 430f, 28f),
                $"Validation: {(_result?.passed == true ? "PASSED" : "RUNNING")}",
                bodyStyle);
        }

        private static string YesNo(bool? value)
        {
            return value == true ? "YES" : "NO";
        }

        private static string GetArgument(
            string[] arguments,
            string key,
            string fallback)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(
                        arguments[index],
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return fallback;
        }

        private static ushort ParseUShort(string value, ushort fallback)
        {
            return ushort.TryParse(value, out ushort parsed)
                ? parsed
                : fallback;
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float parsed)
                ? parsed
                : fallback;
        }

        [Serializable]
        private sealed class NetworkValidationResult
        {
            public string utcTimestamp;
            public string platform;
            public string sceneName;
            public string mode;
            public string instanceName;
            public string address;
            public int port;
            public bool passed;
            public bool startSucceeded;
            public bool localClientConnected;
            public ulong localClientId;
            public int connectedCallbackCount;
            public int disconnectedCallbackCount;
            public int visiblePlayerCount;
            public bool sawTwoPlayers;
            public bool sawDistinctOwners;
            public bool positionsWereDistinct;
            public bool observedSynchronizedMovement;
            public bool localShutdownRequested;
            public bool localShutdownHandled;
            public bool remoteDisconnectObserved;
            public string disconnectReason;
            public string status;
            public string screenshotPath;
        }
    }
}
