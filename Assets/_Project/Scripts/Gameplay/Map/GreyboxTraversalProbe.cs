using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Map
{
    public sealed class GreyboxTraversalProbe : MonoBehaviour
    {
        private const float ArrivalDistance = 0.12f;
        private const float ScreenshotDelaySeconds = 1.5f;
        private const float StuckTimeoutSeconds = 1.5f;

        [SerializeField]
        private GreyboxMapDefinition map;

        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private string routeId = GreyboxMapDefinition.CrossingRouteId;

        [SerializeField, Min(0.01f)]
        private float moveSpeedMetersPerSecond = 5f;

        private GreyboxRouteReference _route;
        private MapTraversalResult _result;
        private string _resultPath;
        private string _screenshotPath;
        private int _waypointIndex;
        private float _startedAt;
        private float _stuckSeconds;
        private float _quitAt = -1f;
        private bool _screenshotCaptured;
        private bool _autoQuit;

        public GreyboxMapDefinition Map
        {
            get => map;
            set => map = value;
        }

        public CharacterController CharacterController
        {
            get => characterController;
            set => characterController = value;
        }

        public string RouteId
        {
            get => routeId;
            set => routeId = value;
        }

        public float MoveSpeedMetersPerSecond
        {
            get => moveSpeedMetersPerSecond;
            set => moveSpeedMetersPerSecond = value;
        }

        private void Start()
        {
            _autoQuit = HasArgument("-mapAutoQuit");
            if (!_autoQuit)
            {
                gameObject.SetActive(false);
                return;
            }

            if (map == null || characterController == null)
            {
                throw new InvalidOperationException(
                    "GreyboxTraversalProbe requires a map and CharacterController.");
            }

            map.ValidateOrThrow();
            _route = map.GetRoute(routeId);
            if (moveSpeedMetersPerSecond <= 0f)
            {
                throw new InvalidOperationException(
                    "Greybox traversal speed must be positive.");
            }

            Application.runInBackground = true;
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            _resultPath = Path.Combine(
                Application.persistentDataPath,
                "map-001-result.json");
            _screenshotPath = Path.Combine(
                Application.persistentDataPath,
                "map-001-screenshot.png");

            characterController.enabled = false;
            transform.position = _route.Waypoints[0].position;
            characterController.enabled = true;

            _waypointIndex = 1;
            _startedAt = Time.unscaledTime;
            _result = new MapTraversalResult
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                sceneName = UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene()
                    .name,
                mapWidthMeters = map.MapWidthMeters,
                mapDepthMeters = map.MapDepthMeters,
                requiredLocationCount = map.Locations.Count,
                routeCount = map.Routes.Count,
                rooftopCount = map.Rooftops.Count,
                ladderCount = map.Ladders.Count,
                trashBinCount = map.TrashBins.Count,
                crossingRouteId = routeId,
                crossingRouteDistanceMeters = _route.CalculateDistance(),
                estimatedTraversalSeconds = map.EstimateCrossingSeconds(
                    moveSpeedMetersPerSecond),
                screenshotPath = _screenshotPath,
                status = "Traversing"
            };
            WriteResult();
        }

        private void Update()
        {
            if (_result == null)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _startedAt;
            if (!_screenshotCaptured && elapsed >= ScreenshotDelaySeconds)
            {
                _screenshotCaptured = true;
                ScreenCapture.CaptureScreenshot(_screenshotPath);
            }

            if (_result.completed)
            {
                if (_autoQuit && _quitAt > 0f && Time.unscaledTime >= _quitAt)
                {
                    Application.Quit();
                }

                return;
            }

            MoveToNextWaypoint();
        }

        private void MoveToNextWaypoint()
        {
            if (_waypointIndex >= _route.Waypoints.Count)
            {
                CompleteTraversal();
                return;
            }

            Vector3 target = _route.Waypoints[_waypointIndex].position;
            Vector3 offset = target - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= ArrivalDistance)
            {
                _waypointIndex++;
                _stuckSeconds = 0f;
                if (_waypointIndex >= _route.Waypoints.Count)
                {
                    CompleteTraversal();
                }

                return;
            }

            Vector3 before = transform.position;
            Vector3 displacement =
                offset.normalized * moveSpeedMetersPerSecond * Time.deltaTime;
            characterController.Move(displacement);
            float moved = Vector3.Distance(before, transform.position);
            _result.actualDistanceMeters += moved;

            if (moved < displacement.magnitude * 0.1f)
            {
                _stuckSeconds += Time.deltaTime;
                if (_stuckSeconds >= StuckTimeoutSeconds)
                {
                    _result.stuckCount++;
                    _result.status =
                        $"Stuck before waypoint {_waypointIndex}";
                    _result.completed = true;
                    _result.passed = false;
                    _result.actualTraversalSeconds =
                        Time.unscaledTime - _startedAt;
                    _quitAt = Time.unscaledTime + 1f;
                    WriteResult();
                }
            }
            else
            {
                _stuckSeconds = 0f;
            }
        }

        private void CompleteTraversal()
        {
            _result.completed = true;
            _result.actualTraversalSeconds = Time.unscaledTime - _startedAt;
            _result.passed = _result.stuckCount == 0;
            _result.status = _result.passed
                ? "Crossing completed without getting stuck"
                : "Crossing failed";
            _quitAt = Time.unscaledTime + 1f;
            WriteResult();
        }

        private void WriteResult()
        {
            File.WriteAllText(
                _resultPath,
                JsonUtility.ToJson(_result, true));
        }

        private void OnApplicationQuit()
        {
            if (_result != null)
            {
                WriteResult();
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 410f, 226f), GUIContent.none);
            GUIStyle titleStyle = new(GUI.skin.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold
            };
            GUIStyle bodyStyle = new(GUI.skin.label)
            {
                fontSize = 16
            };

            GUI.Label(
                new Rect(36f, 34f, 370f, 34f),
                "MAP-001 GREYBOX VILLAGE",
                titleStyle);
            GUI.Label(
                new Rect(36f, 78f, 370f, 26f),
                $"Map: {map?.MapWidthMeters:0} x {map?.MapDepthMeters:0} m",
                bodyStyle);
            GUI.Label(
                new Rect(36f, 108f, 370f, 26f),
                $"Required places: {map?.Locations.Count ?? 0} / 7",
                bodyStyle);
            GUI.Label(
                new Rect(36f, 138f, 370f, 26f),
                $"Routes: {map?.Routes.Count ?? 0}  |  Ladders: {map?.Ladders.Count ?? 0}",
                bodyStyle);
            GUI.Label(
                new Rect(36f, 168f, 370f, 26f),
                $"Crossing: {(_result?.actualDistanceMeters ?? 0f):0.0} / {(_result?.crossingRouteDistanceMeters ?? 0f):0.0} m",
                bodyStyle);
            GUI.Label(
                new Rect(36f, 198f, 370f, 26f),
                $"Status: {_result?.status ?? "Initializing"}",
                bodyStyle);
        }

        private static bool HasArgument(string expected)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(
                        argument,
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class MapTraversalResult
        {
            public string utcTimestamp;
            public string platform;
            public string sceneName;
            public bool passed;
            public bool completed;
            public string status;
            public float mapWidthMeters;
            public float mapDepthMeters;
            public int requiredLocationCount;
            public int routeCount;
            public int rooftopCount;
            public int ladderCount;
            public int trashBinCount;
            public string crossingRouteId;
            public float crossingRouteDistanceMeters;
            public float estimatedTraversalSeconds;
            public float actualTraversalSeconds;
            public float actualDistanceMeters;
            public int stuckCount;
            public string screenshotPath;
        }
    }
}
