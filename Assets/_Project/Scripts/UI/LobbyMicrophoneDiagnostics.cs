using System.Collections;
using PawsAndLoot.Integration.Voice;
using PawsAndLoot.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// The lobby's microphone check: press once, speak, and find out before the
    /// match whether the voice commands will hear anything.
    ///
    /// It runs on a timer and stops itself. An open-ended toggle leaves the
    /// device recording if the player walks away, and the voice command capture
    /// cannot open a device this still holds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyMicrophoneDiagnostics : MonoBehaviour
    {
        private const int SampleRate = 16000;
        private const int SampleWindow = 512;

        /// <summary>
        /// How long a check listens before reporting. Long enough to say a
        /// short phrase, short enough that nobody wonders if it hung.
        /// </summary>
        private const float ListenSeconds = 5f;

        /// <summary>
        /// Loudness that counts as speech rather than room noise. Measured on
        /// the same scaled samples the voice capture uses, so a sensitivity
        /// that passes here passes there.
        /// </summary>
        private const float DetectionLevel = 0.035f;

        [SerializeField]
        private TMP_Text statusLabel;

        [SerializeField]
        private Button testButton;

        [SerializeField]
        private TMP_Text testButtonLabel;

        [SerializeField]
        private string idleButtonCaption = "마이크 확인";

        private AudioClip _clip;
        private string _deviceName;
        private bool _testing;
        private bool _detected;
        private float _peak;
        private float _level;

        public bool IsTesting => _testing;

        public float LastLevel => _level;

        /// <summary>
        /// Last message shown, so a test result survives the label being
        /// rewritten by a later refresh.
        /// </summary>
        public string StatusText =>
            statusLabel != null ? statusLabel.text : string.Empty;

        public void ConfigureView(
            TMP_Text configuredStatusLabel,
            Button configuredTestButton,
            TMP_Text configuredTestButtonLabel)
        {
            statusLabel = configuredStatusLabel;
            testButton = configuredTestButton;
            testButtonLabel = configuredTestButtonLabel;
            if (testButtonLabel != null)
            {
                idleButtonCaption = testButtonLabel.text;
            }

            SetStatus("마이크 확인 대기");
        }

        private void OnEnable()
        {
            BindControls();
            SetStatus(
                Microphone.devices.Length > 0
                    ? "마이크 확인 대기"
                    : "사용 가능한 마이크를 찾지 못했습니다.");
            SetButtonCaption(false);
        }

        private void OnDisable()
        {
            StopTest(null);
            if (testButton != null)
            {
                testButton.onClick.RemoveListener(ToggleTest);
            }
        }

        private void BindControls()
        {
            if (testButton == null)
            {
                GameLogger.Error(
                    GameLogCategory.Voice,
                    "Lobby microphone check has no button; the control cannot "
                    + "be pressed.");
                return;
            }

            // Removed first: the listener is added on every enable, and a scene
            // re-entry would otherwise start two checks per press.
            testButton.onClick.RemoveListener(ToggleTest);
            testButton.onClick.AddListener(ToggleTest);
        }

        public void ToggleTest()
        {
            if (_testing)
            {
                StopTest("마이크 확인 대기");
                return;
            }

            StartCoroutine(RunTest());
        }

        private IEnumerator RunTest()
        {
            SetStatus("마이크 권한을 확인하는 중입니다.");
            SetButtonCaption(true);

#if UNITY_2020_1_OR_NEWER
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                AsyncOperation request =
                    Application.RequestUserAuthorization(
                        UserAuthorization.Microphone);
                while (request != null && !request.isDone)
                {
                    yield return null;
                }

                if (!Application.HasUserAuthorization(
                        UserAuthorization.Microphone))
                {
                    StopTest("마이크 권한이 거부되었습니다.");
                    yield break;
                }
            }
#endif

            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                StopTest("사용 가능한 마이크를 찾지 못했습니다.");
                yield break;
            }

            _deviceName = devices[0];
            _clip = Microphone.Start(_deviceName, true, 1, SampleRate);
            if (_clip == null)
            {
                StopTest("마이크를 열 수 없습니다.");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 1f;
            while (!Microphone.IsRecording(_deviceName)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!Microphone.IsRecording(_deviceName))
            {
                StopTest("마이크가 다른 프로그램에서 사용 중입니다.");
                yield break;
            }

            _testing = true;
            _detected = false;
            _peak = 0f;
            SetStatus("마이크 입력을 듣고 있습니다. 말해 보세요.");
            GameLogger.Info(
                GameLogCategory.Voice,
                $"Lobby microphone check started on '{_deviceName}'.");

            float finish = Time.realtimeSinceStartup + ListenSeconds;
            while (_testing && Time.realtimeSinceStartup < finish)
            {
                yield return null;
            }

            if (!_testing)
            {
                yield break;
            }

            StopTest(
                _detected
                    ? $"마이크 입력이 정상적으로 감지되었습니다. (최대 {Percent(_peak)}%)"
                    : "입력이 감지되지 않았습니다. 마이크 볼륨을 확인하세요.");
        }

        private void Update()
        {
            if (!_testing || _clip == null || string.IsNullOrEmpty(_deviceName))
            {
                return;
            }

            int position = Microphone.GetPosition(_deviceName);
            if (position <= 0)
            {
                return;
            }

            int windowFrames = Mathf.Min(SampleWindow, position);
            int start = Mathf.Max(0, position - windowFrames);
            var samples = new float[windowFrames * _clip.channels];
            if (!_clip.GetData(samples, start))
            {
                return;
            }

            float sumSquares = 0f;
            for (int index = 0; index < samples.Length; index++)
            {
                float value = samples[index]
                    * VoiceCaptureSettings.MicrophoneSensitivity;
                sumSquares += value * value;
            }

            _level = Mathf.Sqrt(sumSquares / Mathf.Max(1, samples.Length));
            _peak = Mathf.Max(_peak, _level);
            _detected |= _level >= DetectionLevel;

            SetStatus(
                $"마이크 입력을 듣고 있습니다. (입력 {Percent(_level)}%)");
        }

        private void StopTest(string status)
        {
            if (!string.IsNullOrEmpty(_deviceName)
                && Microphone.IsRecording(_deviceName))
            {
                // Released rather than left running: the voice command capture
                // opens the same device, and Windows hands it to one client.
                Microphone.End(_deviceName);
            }

            _testing = false;
            _clip = null;
            _deviceName = string.Empty;
            _level = 0f;
            SetButtonCaption(false);
            if (!string.IsNullOrEmpty(status))
            {
                SetStatus(status);
            }
        }

        private static int Percent(float level)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(level * 20f) * 100f);
        }

        private void SetButtonCaption(bool testing)
        {
            if (testButtonLabel == null)
            {
                return;
            }

            string caption = testing ? "확인 중..." : idleButtonCaption;
            if (testButtonLabel.text != caption)
            {
                testButtonLabel.text = caption;
            }
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null && statusLabel.text != value)
            {
                statusLabel.text = value;
            }
        }
    }
}
