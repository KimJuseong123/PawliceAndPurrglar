using PawsAndLoot.Config;
using PawsAndLoot.TechnicalValidation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Creates only the isolated throw/interaction validation scene. It never
    /// calls a production scene builder and never edits build settings.
    /// </summary>
    public static class ThrowInteractionValidationSetup
    {
        private const string ScenePath =
            "Assets/_Project/Scenes/Validation/ThrowInteractionValidation.unity";
        private const string PlayerConfigPath =
            "Assets/_Project/Settings/Configs/PlayerConfig.asset";

        [MenuItem("PawliceAndPurrglar/Validation/Build Throw Interaction POC")]
        public static void BuildScene()
        {
            EnsureValidationFolder();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "ThrowInteractionValidation";

            GameObject root = new("Throw Interaction Validation");
            ValidationMatchState matchState =
                root.AddComponent<ValidationMatchState>();
            ThrowInteractionValidationBootstrap bootstrap =
                root.AddComponent<ThrowInteractionValidationBootstrap>();

            CreateLighting();
            CreateFloor();
            CreateObstacle();
            GameObject player = CreatePlayer();
            CreateInteractionTargets();
            CreateCamera();
            CreateCanvas();
            CreateEventSystem();

            PlayerConfig playerConfig =
                AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerConfigPath);
            bootstrap.Configure(playerConfig, player);

            if (playerConfig == null)
            {
                Debug.LogError(
                    $"[ThrowInteractionValidation] Missing PlayerConfig at " +
                    $"'{PlayerConfigPath}'.");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Selection.activeObject = root;
            Debug.Log(
                $"[ThrowInteractionValidation] Saved isolated POC scene at " +
                $"'{ScenePath}'. It is not added to build settings.");
        }

        private static void EnsureValidationFolder()
        {
            const string scenesPath = "Assets/_Project/Scenes";
            const string validationPath = scenesPath + "/Validation";
            if (!AssetDatabase.IsValidFolder(validationPath))
            {
                AssetDatabase.CreateFolder(scenesPath, "Validation");
            }
        }

        private static GameObject CreatePlayer()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Validation Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;

            player.AddComponent<PawsAndLoot.Gameplay.Players.PlayerRoleIdentity>();
            player.AddComponent<PawsAndLoot.Gameplay.Items.ToolCarrier>();
            player.AddComponent<PawsAndLoot.Gameplay.Players.PlayerInteractionScanner>();
            player.AddComponent<ThrowInteractionValidationController>();
            player.AddComponent<ValidationInteractionInput>();
            player.AddComponent<ValidationPlayerMover>();
            return player;
        }

        private static void CreateInteractionTargets()
        {
            CreateTarget(
                "Validation Item",
                new Vector3(3f, 0.5f, 0f),
                new Vector3(1f, 1f, 1f),
                ValidationInteractionKind.Item,
                new Color(0.95f, 0.75f, 0.18f));
            CreateTarget(
                "Validation Drawer",
                new Vector3(0f, 0.5f, 3f),
                new Vector3(1.2f, 1f, 0.8f),
                ValidationInteractionKind.Drawer,
                new Color(0.55f, 0.3f, 0.12f));
            CreateTarget(
                "Validation Locked Door",
                new Vector3(-3f, 1f, 0f),
                new Vector3(0.8f, 2f, 0.25f),
                ValidationInteractionKind.LockedDoor,
                new Color(0.65f, 0.2f, 0.2f));
            CreateTarget(
                "Validation Animal",
                new Vector3(0f, 0.5f, -3f),
                new Vector3(1f, 1f, 1f),
                ValidationInteractionKind.Animal,
                new Color(0.35f, 0.8f, 0.45f));
            CreateTarget(
                "Validation Ladder",
                new Vector3(3f, 0.75f, 3f),
                new Vector3(0.8f, 1.5f, 0.35f),
                ValidationInteractionKind.Ladder,
                new Color(0.35f, 0.65f, 0.95f));
        }

        private static void CreateTarget(
            string name,
            Vector3 position,
            Vector3 scale,
            ValidationInteractionKind kind,
            Color color)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = position;
            target.transform.localScale = scale;
            target.AddComponent<ValidationInteractionTarget>()
                .Configure(kind);
            SetRendererColor(target, color);
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Validation Floor";
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(24f, 0.5f, 24f);
            SetRendererColor(floor, new Color(0.16f, 0.19f, 0.23f));
        }

        private static void CreateObstacle()
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Trajectory Obstacle";
            obstacle.transform.position = new Vector3(0f, 1f, 6f);
            obstacle.transform.localScale = new Vector3(3f, 2f, 0.7f);
            SetRendererColor(obstacle, new Color(0.5f, 0.18f, 0.65f));
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new(
                "Validation Directional Light",
                typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.38f);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new(
                "Main Camera",
                typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 12f, -13f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 3f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.07f, 0.11f);
        }

        private static void CreateCanvas()
        {
            GameObject canvasObject = new(
                "Throw Interaction Validation HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(ThrowInteractionValidationHud));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void CreateEventSystem()
        {
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
        }

        private static void SetRendererColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            MaterialPropertyBlock block = new();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
