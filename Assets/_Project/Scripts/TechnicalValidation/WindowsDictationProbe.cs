using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace PawliceAndPurrglar.TechnicalValidation
{
    public sealed class WindowsDictationProbe : MonoBehaviour
    {
        private const string ResultFileName = "tech-003-result.json";
        private const string ScreenshotFileName = "tech-003-screenshot.png";
        private const int SpeechPrivacyPolicyNotAccepted = unchecked((int)0x80045509);

        private VoiceValidationResult _result;
        private string _resultPath;
        private string _screenshotPath;
        private float _startedAt;
        private bool _autoStart;
        private bool _autoFallback;
        private bool _autoQuit;
        private bool _fallbackTriggered;
        private bool _screenshotCaptured;
        private bool _resultDirty;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private DictationRecognizer _recognizer;
#endif

        private IEnumerator Start()
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return null;

            string[] arguments = Environment.GetCommandLineArgs();
            _autoStart = HasArgument(arguments, "-voiceAutoStart");
            _autoFallback = HasArgument(arguments, "-voiceAutoFallback");
            _autoQuit = HasArgument(arguments, "-techAutoQuit");
            bool simulateDenied = HasArgument(arguments, "-voiceSimulateDenied");

            _resultPath = Path.Combine(Application.persistentDataPath, ResultFileName);
            _screenshotPath = Path.Combine(Application.persistentDataPath, ScreenshotFileName);
            _startedAt = Time.unscaledTime;
            _result = new VoiceValidationResult
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                sceneName = SceneManager.GetActiveScene().name,
                sceneLoaded = SceneManager.GetActiveScene().name == "VoiceTechnicalTest",
                microphoneDeviceCount = Microphone.devices.Length,
                keyboardFallbackAvailable = true,
                screenshotPath = _screenshotPath,
                status = "Initializing"
            };

            CreateRecognizer();

            if (simulateDenied)
            {
                ReportError(
                    "Simulated microphone privacy denial",
                    SpeechPrivacyPolicyNotAccepted);
            }
            else if (_autoStart)
            {
                yield return new WaitForSeconds(0.5f);
                StartListening();
            }

            WriteResult();
        }

        private void Update()
        {
            if (_result == null)
            {
                return;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer != null
                && _recognizer.Status == SpeechSystemStatus.Running
                && !_result.recognizerReachedRunning)
            {
                _result.recognizerReachedRunning = true;
                _result.status = "Listening";
                _resultDirty = true;
            }
#endif

            if (Keyboard.current?.spaceKey.wasPressedThisFrame == true)
            {
                UseKeyboardFallback();
            }

            float elapsed = Time.unscaledTime - _startedAt;
            if (_autoFallback && !_fallbackTriggered && elapsed >= 2.5f)
            {
                UseKeyboardFallback();
            }

            if (!_screenshotCaptured && elapsed >= 3.5f)
            {
                _screenshotCaptured = true;
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                _resultDirty = true;
            }

            if (_resultDirty)
            {
                WriteResult();
            }

            if (_autoQuit && elapsed >= 6f)
            {
                Application.Quit();
            }
        }

        public void StartListening()
        {
            if (_result == null)
            {
                return;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer == null)
            {
                if (!_result.errorReceived)
                {
                    ReportError("Windows DictationRecognizer is unavailable.", 0);
                }

                return;
            }

            try
            {
                if (_recognizer.Status != SpeechSystemStatus.Running)
                {
                    _recognizer.Start();
                    _result.startRequested = true;
                    _result.status = "Starting";
                    _resultDirty = true;
                }
            }
            catch (Exception exception)
            {
                ReportError(exception.Message, exception.HResult);
            }
#else
            ReportError("TECH-003 DictationRecognizer is Windows-only.", 0);
#endif
        }

        public void StopListening()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer == null)
            {
                return;
            }

            try
            {
                if (_recognizer.Status == SpeechSystemStatus.Running)
                {
                    _recognizer.Stop();
                }

                _result.status = "Stopped";
                _resultDirty = true;
            }
            catch (Exception exception)
            {
                ReportError(exception.Message, exception.HResult);
            }
#endif
        }

        public void UseKeyboardFallback()
        {
            if (_result == null)
            {
                return;
            }

            _fallbackTriggered = true;
            _result.keyboardFallbackUsed = true;
            _result.gameRemainedResponsive = true;
            _result.displayText = "Keyboard fallback accepted";
            _result.status = "Fallback ready";
            _resultDirty = true;
        }

        private void CreateRecognizer()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            _result.windowsSpeechApiAvailable = true;
            try
            {
                _recognizer = new DictationRecognizer(ConfidenceLevel.Low);
                _recognizer.InitialSilenceTimeoutSeconds = 8f;
                _recognizer.AutoSilenceTimeoutSeconds = 3f;
                _recognizer.DictationHypothesis += OnHypothesis;
                _recognizer.DictationResult += OnResult;
                _recognizer.DictationComplete += OnComplete;
                _recognizer.DictationError += ReportError;
                _result.recognizerCreated = true;
                _result.status = "Ready";
            }
            catch (Exception exception)
            {
                ReportError(exception.Message, exception.HResult);
            }
#else
            _result.windowsSpeechApiAvailable = false;
            ReportError("TECH-003 DictationRecognizer is Windows-only.", 0);
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void OnHypothesis(string text)
        {
            _result.hypothesisText = text;
            _result.status = "Hearing";
            _resultDirty = true;
        }

        private void OnResult(string text, ConfidenceLevel confidence)
        {
            _result.recognitionReceived = !string.IsNullOrWhiteSpace(text);
            _result.recognizedText = text;
            _result.displayText = text;
            _result.confidence = confidence.ToString();
            _result.status = "Text received";
            _resultDirty = true;
        }

        private void OnComplete(DictationCompletionCause cause)
        {
            _result.completionCause = cause.ToString();
            _result.status = cause == DictationCompletionCause.Complete
                ? "Complete"
                : $"Completed: {cause}";
            _resultDirty = true;
        }
#endif

        private void ReportError(string error, int hresult)
        {
            _result.errorReceived = true;
            _result.errorMessage = error;
            _result.errorHResult = $"0x{hresult:X8}";
            _result.privacyPolicyDenied =
                hresult == SpeechPrivacyPolicyNotAccepted;
            _result.status = "Voice unavailable - fallback ready";
            _resultDirty = true;
        }

        private void WriteResult()
        {
            _result.harnessPassed =
                _result.sceneLoaded
                && _result.windowsSpeechApiAvailable
                && _result.recognizerCreated
                && _result.keyboardFallbackAvailable
                && (!_result.keyboardFallbackUsed || _result.gameRemainedResponsive);
            _result.manualSpeechPassed = _result.recognitionReceived;
            File.WriteAllText(_resultPath, JsonUtility.ToJson(_result, true));
            _resultDirty = false;
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer == null)
            {
                return;
            }

            _recognizer.DictationHypothesis -= OnHypothesis;
            _recognizer.DictationResult -= OnResult;
            _recognizer.DictationComplete -= OnComplete;
            _recognizer.DictationError -= ReportError;

            try
            {
                if (_recognizer.Status == SpeechSystemStatus.Running)
                {
                    _recognizer.Stop();
                }
            }
            catch (Exception exception)
            {
                if (_result != null)
                {
                    ReportError(exception.Message, exception.HResult);
                }
            }
            finally
            {
                _recognizer.Dispose();
                _recognizer = null;
            }
#endif
        }

        private void OnGUI()
        {
            const float panelWidth = 820f;
            Rect panel = new(32f, 28f, panelWidth, 560f);
            GUI.Box(panel, GUIContent.none);

            GUIStyle titleStyle = new(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
            GUIStyle headingStyle = new(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold
            };
            GUIStyle bodyStyle = new(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = true
            };

            GUI.Label(new Rect(58f, 48f, 760f, 40f), "TECH-003 WINDOWS SPEECH TO TEXT", titleStyle);
            GUI.Label(
                new Rect(58f, 94f, 760f, 28f),
                "Isolated microphone probe. No AI dialogue or gameplay command is executed.",
                bodyStyle);

            string devices = _result == null
                ? "Microphones: checking"
                : $"Microphones: {_result.microphoneDeviceCount}";
            string status = _result == null
                ? "Status: initializing"
                : $"Status: {_result.status}";
            GUI.Label(new Rect(58f, 138f, 760f, 28f), devices, headingStyle);
            GUI.Label(new Rect(58f, 170f, 760f, 28f), status, bodyStyle);

            if (GUI.Button(new Rect(58f, 214f, 210f, 48f), "Start listening"))
            {
                StartListening();
            }

            if (GUI.Button(new Rect(284f, 214f, 170f, 48f), "Stop"))
            {
                StopListening();
            }

            if (GUI.Button(new Rect(470f, 214f, 320f, 48f), "Continue with keyboard (Space)"))
            {
                UseKeyboardFallback();
            }

            GUI.Label(new Rect(58f, 292f, 760f, 28f), "Recognized text", headingStyle);
            string displayText = string.IsNullOrWhiteSpace(_result?.displayText)
                ? "Speak after pressing Start listening."
                : _result.displayText;
            GUI.Box(new Rect(58f, 326f, 732f, 72f), GUIContent.none);
            GUI.Label(new Rect(72f, 338f, 704f, 48f), displayText, bodyStyle);

            GUI.Label(new Rect(58f, 420f, 760f, 28f), "Error and fallback", headingStyle);
            string error = string.IsNullOrWhiteSpace(_result?.errorMessage)
                ? "No voice error reported. Space remains available."
                : $"{_result.errorHResult}: {_result.errorMessage}";
            GUI.Label(new Rect(58f, 452f, 732f, 54f), error, bodyStyle);

            string manual = _result?.manualSpeechPassed == true
                ? "Manual speech check: TEXT RECEIVED"
                : "Manual speech check: waiting for spoken phrase";
            GUI.Label(new Rect(58f, 520f, 732f, 28f), manual, headingStyle);
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            return Array.Exists(
                arguments,
                argument => string.Equals(
                    argument,
                    expected,
                    StringComparison.OrdinalIgnoreCase));
        }

        [Serializable]
        private sealed class VoiceValidationResult
        {
            public string utcTimestamp;
            public string platform;
            public string sceneName;
            public bool sceneLoaded;
            public bool harnessPassed;
            public bool manualSpeechPassed;
            public bool windowsSpeechApiAvailable;
            public int microphoneDeviceCount;
            public bool recognizerCreated;
            public bool startRequested;
            public bool recognizerReachedRunning;
            public bool recognitionReceived;
            public string hypothesisText;
            public string recognizedText;
            public string confidence;
            public string completionCause;
            public bool errorReceived;
            public string errorMessage;
            public string errorHResult;
            public bool privacyPolicyDenied;
            public bool keyboardFallbackAvailable;
            public bool keyboardFallbackUsed;
            public bool gameRemainedResponsive;
            public string displayText;
            public string status;
            public string screenshotPath;
        }
    }
}
