using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.TechnicalValidation
{
    [DefaultExecutionOrder(110)]
    public sealed class CharacterTechnicalValidationReporter : MonoBehaviour
    {
        private const string ResultFileName = "character-tech-result.json";
        private const string ScreenshotFileName = "character-tech-screenshot.png";
        private const float AutoQuitDelaySeconds = 6f;

        [SerializeField]
        private CharacterTechnicalPreview[] characters = Array.Empty<CharacterTechnicalPreview>();

        private ValidationResult _result;
        private float _startedAt;
        private bool _autoQuit;
        private string _resultPath;
        private string _screenshotPath;

        public CharacterTechnicalPreview[] Characters
        {
            get => characters;
            set => characters = value ?? Array.Empty<CharacterTechnicalPreview>();
        }

        private IEnumerator Start()
        {
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForSeconds(0.75f);

            characters = characters
                .Where(character => character != null)
                .ToArray();

            if (characters.Length == 0)
            {
                throw new InvalidOperationException(
                    "CharacterTechnicalValidationReporter requires at least one preview.");
            }

            _startedAt = Time.unscaledTime;
            _autoQuit = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(
                    argument,
                    "-techAutoQuit",
                    StringComparison.OrdinalIgnoreCase));
            _resultPath = Path.Combine(Application.persistentDataPath, ResultFileName);
            _screenshotPath = Path.Combine(Application.persistentDataPath, ScreenshotFileName);

            _result = new ValidationResult
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                sceneName = SceneManager.GetActiveScene().name,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenshotPath = _screenshotPath,
                characters = characters
                    .Select(character => character.CaptureRuntimeInfo())
                    .ToArray()
            };

            _result.characterCount = _result.characters.Length;
            _result.charactersWithSkeleton =
                _result.characters.Count(character => character.hasSkinnedMesh);
            _result.charactersWithAnimations =
                _result.characters.Count(character => character.clipCount > 0);
            _result.charactersWithIdle =
                _result.characters.Count(character => character.hasIdle);
            _result.charactersWithWalk =
                _result.characters.Count(character => character.hasWalk);
            _result.passed =
                _result.sceneName == "CharacterTechnicalTest"
                && _result.characterCount >= 4
                && _result.characters.All(character => character.rendererCount > 0);

            File.WriteAllText(_resultPath, JsonUtility.ToJson(_result, true));
            ScreenCapture.CaptureScreenshot(_screenshotPath);
        }

        private void Update()
        {
            if (_autoQuit
                && _result != null
                && Time.unscaledTime - _startedAt >= AutoQuitDelaySeconds)
            {
                Application.Quit();
            }
        }

        private void OnGUI()
        {
            GUIStyle titleStyle = new(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            GUIStyle bodyStyle = new(GUI.skin.label)
            {
                fontSize = 16
            };

            GUI.Box(new Rect(20f, 20f, 480f, 250f), GUIContent.none);
            GUI.Label(new Rect(40f, 38f, 420f, 30f), "CHAR-001 FBX CHARACTER VALIDATION", titleStyle);
            GUI.Label(
                new Rect(40f, 82f, 420f, 24f),
                $"Scene: {SceneManager.GetActiveScene().name}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 112f, 420f, 24f),
                $"Characters: {characters.Length}",
                bodyStyle);

            if (_result == null)
            {
                GUI.Label(new Rect(40f, 142f, 420f, 24f), "Result: collecting data", bodyStyle);
                return;
            }

            GUI.Label(
                new Rect(40f, 142f, 420f, 24f),
                $"Skeletons: {_result.charactersWithSkeleton} / {_result.characterCount}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 172f, 420f, 24f),
                $"Animated assets: {_result.charactersWithAnimations} / {_result.characterCount}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 202f, 420f, 24f),
                $"Idle clips: {_result.charactersWithIdle}, Walk clips: {_result.charactersWithWalk}",
                bodyStyle);
            GUI.Label(
                new Rect(40f, 232f, 420f, 24f),
                $"Validation: {(_result.passed ? "PASSED" : "FAILED")}",
                bodyStyle);
        }

        [Serializable]
        private sealed class ValidationResult
        {
            public string utcTimestamp;
            public string platform;
            public string sceneName;
            public bool passed;
            public int screenWidth;
            public int screenHeight;
            public string screenshotPath;
            public int characterCount;
            public int charactersWithSkeleton;
            public int charactersWithAnimations;
            public int charactersWithIdle;
            public int charactersWithWalk;
            public CharacterTechnicalPreview.CharacterRuntimeInfo[] characters;
        }
    }
}
