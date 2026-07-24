using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.TechnicalValidation
{
    [DefaultExecutionOrder(100)]
    public sealed class BlenderValidationReporter : MonoBehaviour
    {
        private const string ResultFileName = "tech-002-result.json";
        private const string ScreenshotFileName = "tech-002-screenshot.png";
        private const float AutoQuitDelaySeconds = 5f;

        [SerializeField]
        private VisualRootContract player;

        [SerializeField]
        private SkinnedMeshRenderer skinnedMesh;

        private ValidationResult _result;
        private string _resultPath;
        private string _screenshotPath;
        private float _startedAt;
        private bool _autoQuit;

        public VisualRootContract Player
        {
            get => player;
            set => player = value;
        }

        public SkinnedMeshRenderer SkinnedMesh
        {
            get => skinnedMesh;
            set => skinnedMesh = value;
        }

        private IEnumerator Start()
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            if (player == null || skinnedMesh == null)
            {
                throw new InvalidOperationException(
                    "BlenderValidationReporter requires a player and skinned mesh.");
            }

            player.ValidateOrThrow();

            _resultPath = Path.Combine(Application.persistentDataPath, ResultFileName);
            _screenshotPath = Path.Combine(Application.persistentDataPath, ScreenshotFileName);
            _startedAt = Time.unscaledTime;
            _autoQuit = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(
                    argument,
                    "-techAutoQuit",
                    StringComparison.OrdinalIgnoreCase));

            AnimationClip[] clips = player.VisualAnimator.runtimeAnimatorController.animationClips;
            float modelHeight = skinnedMesh.bounds.size.y;
            bool scaleIsOne = Approximately(player.VisualRoot.lossyScale, Vector3.one);
            bool rotationIsIdentity =
                Quaternion.Angle(player.VisualRoot.rotation, Quaternion.identity) < 0.01f;

            _result = new ValidationResult
            {
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                sceneName = SceneManager.GetActiveScene().name,
                sceneLoaded = SceneManager.GetActiveScene().name == "BlenderTechnicalTest",
                boneCount = skinnedMesh.bones.Length,
                materialCount = skinnedMesh.sharedMaterials.Length,
                hasIdle = clips.Any(clip => clip.name == "Idle"),
                hasWalk = clips.Any(clip => clip.name == "Walk"),
                animatorPlayingWalk =
                    player.VisualAnimator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),
                rootHasCollider =
                    player.GameplayCollider != null
                    && player.GameplayCollider.transform == player.transform,
                rootHasMovement = player.GetComponent<KeyboardCubeMover>() != null,
                visualRootSeparated = player.VisualRoot.parent == player.transform,
                rootMotionDisabled = !player.VisualAnimator.applyRootMotion,
                scaleIsOne = scaleIsOne,
                rotationIsIdentity = rotationIsIdentity,
                modelHeightMeters = modelHeight,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenshotPath = _screenshotPath
            };

            _result.passed =
                _result.sceneLoaded
                && _result.boneCount == 5
                && _result.materialCount == 1
                && _result.hasIdle
                && _result.hasWalk
                && _result.animatorPlayingWalk
                && _result.rootHasCollider
                && _result.rootHasMovement
                && _result.visualRootSeparated
                && _result.rootMotionDisabled
                && _result.scaleIsOne
                && _result.rotationIsIdentity
                && modelHeight >= 1.8f
                && modelHeight <= 2.5f;

            ScreenCapture.CaptureScreenshot(_screenshotPath);
            WriteResult();
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
            const float width = 520f;
            Rect panel = new(24f, 24f, width, 286f);
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

            GUI.Label(new Rect(44f, 40f, 470f, 38f), "TECH-002 BLENDER TO UNITY", titleStyle);
            GUI.Label(new Rect(44f, 82f, 470f, 28f), "Generic rig: 5 bones / 1 material", bodyStyle);
            GUI.Label(new Rect(44f, 112f, 470f, 28f), "Animation clips: Idle + Walk", bodyStyle);
            GUI.Label(new Rect(44f, 142f, 470f, 28f), "Movement and Collider stay on PlayerRoot", bodyStyle);
            GUI.Label(new Rect(44f, 172f, 470f, 28f), "Model lives under replaceable VisualRoot", bodyStyle);

            string height = _result == null
                ? "Imported height: measuring"
                : $"Imported height: {_result.modelHeightMeters:F2} m";
            string result = _result == null
                ? "Validation: running"
                : $"Validation: {(_result.passed ? "PASSED" : "FAILED")}";
            GUI.Label(new Rect(44f, 214f, 470f, 28f), height, bodyStyle);
            GUI.Label(new Rect(44f, 246f, 470f, 28f), result, bodyStyle);
        }

        private void WriteResult()
        {
            File.WriteAllText(_resultPath, JsonUtility.ToJson(_result, true));
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Approximately(left.x, right.x)
                && Mathf.Approximately(left.y, right.y)
                && Mathf.Approximately(left.z, right.z);
        }

        [Serializable]
        private sealed class ValidationResult
        {
            public string utcTimestamp;
            public string platform;
            public string sceneName;
            public bool sceneLoaded;
            public bool passed;
            public int boneCount;
            public int materialCount;
            public bool hasIdle;
            public bool hasWalk;
            public bool animatorPlayingWalk;
            public bool rootHasCollider;
            public bool rootHasMovement;
            public bool visualRootSeparated;
            public bool rootMotionDisabled;
            public bool scaleIsOne;
            public bool rotationIsIdentity;
            public float modelHeightMeters;
            public int screenWidth;
            public int screenHeight;
            public string screenshotPath;
        }
    }
}
