using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.TechnicalValidation
{
    [DefaultExecutionOrder(100)]
    public sealed class TechnicalValidationReporter : MonoBehaviour
    {
        private const string ResultFileName = "tech-001-result.json";
        private const string ScreenshotFileName = "tech-001-screenshot.png";
        private const float MovementThreshold = 0.25f;
        private const float AutoQuitDelaySeconds = 8f;

        [SerializeField]
        private Transform movementTarget;

        private ValidationResult _result;
        private Vector3 _initialPosition;
        private string _resultPath;
        private string _screenshotPath;
        private float _startedAt;
        private bool _autoQuit;

        public Transform MovementTarget
        {
            get => movementTarget;
            set => movementTarget = value;
        }

        private IEnumerator Start()
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return null;

            if (movementTarget == null)
            {
                throw new InvalidOperationException(
                    "TechnicalValidationReporter requires a movement target.");
            }

            _initialPosition = movementTarget.position;
            _resultPath = Path.Combine(Application.persistentDataPath, ResultFileName);
            _screenshotPath = Path.Combine(Application.persistentDataPath, ScreenshotFileName);
            _startedAt = Time.unscaledTime;
            _autoQuit = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(
                    argument,
                    "-techAutoQuit",
                    StringComparison.OrdinalIgnoreCase));

            _result = new ValidationResult
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                sceneName = SceneManager.GetActiveScene().name,
                sceneLoaded = SceneManager.GetActiveScene().name == "TechnicalTest",
                inputSystemAvailable = Keyboard.current != null,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                initialX = _initialPosition.x,
                initialY = _initialPosition.y,
                initialZ = _initialPosition.z,
                screenshotPath = _screenshotPath
            };

            WriteResult();
        }

        private void Update()
        {
            if (_result == null || movementTarget == null)
            {
                return;
            }

            Vector3 currentPosition = movementTarget.position;
            if (!_result.keyboardMovementDetected
                && Vector3.Distance(_initialPosition, currentPosition) >= MovementThreshold)
            {
                _result.keyboardMovementDetected = true;
                UpdateFinalPosition(currentPosition);
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                WriteResult();
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                Application.Quit();
            }

            if (_autoQuit && Time.unscaledTime - _startedAt >= AutoQuitDelaySeconds)
            {
                Application.Quit();
            }
        }

        private void OnApplicationQuit()
        {
            if (_result == null || movementTarget == null)
            {
                return;
            }

            UpdateFinalPosition(movementTarget.position);
            WriteResult();
        }

        private void OnGUI()
        {
            const float width = 460f;
            const float height = 220f;
            Rect panel = new(24f, 24f, width, height);
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

            GUI.Label(new Rect(44f, 40f, 420f, 38f), "TECH-001 WINDOWS BUILD", titleStyle);
            GUI.Label(new Rect(44f, 84f, 420f, 28f), "Move the cube with WASD or arrow keys.", bodyStyle);
            GUI.Label(new Rect(44f, 116f, 420f, 28f), "Press Esc to quit.", bodyStyle);

            string resolution = _result == null
                ? "Resolution: initializing"
                : $"Resolution: {_result.screenWidth} x {_result.screenHeight}";
            string input = _result?.keyboardMovementDetected == true
                ? "Keyboard movement: DETECTED"
                : "Keyboard movement: waiting";
            GUI.Label(new Rect(44f, 154f, 420f, 28f), resolution, bodyStyle);
            GUI.Label(new Rect(44f, 184f, 420f, 28f), input, bodyStyle);
        }

        private void UpdateFinalPosition(Vector3 position)
        {
            _result.finalX = position.x;
            _result.finalY = position.y;
            _result.finalZ = position.z;
        }

        private void WriteResult()
        {
            string json = JsonUtility.ToJson(_result, true);
            File.WriteAllText(_resultPath, json);
        }

        [Serializable]
        private sealed class ValidationResult
        {
            public string utcTimestamp;
            public string platform;
            public string sceneName;
            public bool sceneLoaded;
            public bool inputSystemAvailable;
            public bool keyboardMovementDetected;
            public int screenWidth;
            public int screenHeight;
            public float initialX;
            public float initialY;
            public float initialZ;
            public float finalX;
            public float finalY;
            public float finalZ;
            public string screenshotPath;
        }
    }
}
