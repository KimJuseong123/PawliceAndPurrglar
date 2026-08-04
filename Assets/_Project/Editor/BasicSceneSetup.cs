using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    public static class BasicSceneSetup
    {
        private static readonly Color BackgroundColor = new(0.025f, 0.032f, 0.05f, 1f);
        private static readonly Color PanelColor = new(0.055f, 0.065f, 0.09f, 0.98f);
        private static readonly Color PoliceBlue = new(0.08f, 0.28f, 0.62f, 1f);
        private static readonly Color ThiefRed = new(0.62f, 0.12f, 0.09f, 1f);
        private static readonly Color MerchantGold = new(0.95f, 0.64f, 0.15f, 1f);
        private static readonly Color MutedText = new(0.7f, 0.75f, 0.82f, 1f);
        private static readonly Color Transparent = new(1f, 1f, 1f, 0f);

        /// <summary>
        /// Root name of the result prefab instance in the scene. Kept distinct
        /// from the other scenes' "Scene UI" so a rebuild can tell the new
        /// screen from whatever an older version of this file left behind.
        /// </summary>
        private const string ResultRootName = "ResultCanvas";

        [MenuItem("Paws & Loot/Setup/Rebuild Basic Scenes")]
        public static void CreateBasicScenes()
        {
            EnsureSceneDirectory();

            CreateScene(
                GameSceneId.Bootstrap,
                "PAWS & LOOT",
                "POLICE + DOG   VS   THIEF + CAT",
                new[]
                {
                    new ButtonSpec("START GAME", GameSceneId.Game, PoliceBlue)
                });

            GreyboxMapSetup.CreateGameScene();

            CreateScene(
                GameSceneId.Result,
                "NO MATCH RESULT",
                "PLAY A MATCH TO VIEW THE RESULT",
                new[]
                {
                    new ButtonSpec("REMATCH", GameSceneId.Game, PoliceBlue),
                    new ButtonSpec("MAIN MENU", GameSceneId.Bootstrap, MerchantGold)
                });

            ApplyBuildSettings();
            ValidateBasicScenes();
            EditorSceneManager.OpenScene(GameSceneCatalog.GetPath(GameSceneId.Bootstrap), OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("BASE-003 basic scenes created and validated.");
        }

        [MenuItem("Paws & Loot/Setup/Validate Basic Scenes")]
        public static void ValidateBasicScenes()
        {
            ValidateBuildSettings();

            foreach (GameSceneId sceneId in GameSceneCatalog.BuildOrder)
            {
                string path = GameSceneCatalog.GetPath(sceneId);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Required scene is missing: {path}", path);
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ValidateSceneContents(sceneId, scene);
            }

            EditorSceneManager.OpenScene(GameSceneCatalog.GetPath(GameSceneId.Bootstrap), OpenSceneMode.Single);
            Debug.Log("BASE-003 scene validation passed.");
        }

        [MenuItem("Paws & Loot/Setup/Rebuild Result UI")]
        public static void RebuildResultUi()
        {
            EnsureSceneDirectory();

            string path = GameSceneCatalog.GetPath(GameSceneId.Result);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Required scene is missing: {path}",
                    path);
            }

            Scene scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Single);
            DestroyRoot(scene, ResultRootName);
            // The name the inline builder used, so rebuilding an older scene
            // does not leave two result screens stacked on each other.
            DestroyRoot(scene, "Scene UI");
            if (!SceneHasComponent<Camera>(scene))
            {
                CreateCamera();
            }

            if (!SceneHasComponent<EventSystem>(scene))
            {
                CreateEventSystem();
            }

            CreateResultInterface();
            NormalizeSceneCanvasScales();
            ProjectFontSweep.Apply(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"Failed to save scene: {path}");
            }

            ValidateSceneContents(GameSceneId.Result, scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Result UI rebuilt.");
        }

        [MenuItem("Paws & Loot/Setup/Ensure Bootstrap Services")]
        public static void EnsureBootstrapServices()
        {
            EnsureSceneDirectory();

            string path = GameSceneCatalog.GetPath(GameSceneId.Bootstrap);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Required scene is missing: {path}", path);
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameConfigSet configSet = LoadDefaultConfigSet();
            GameLogConfig logConfig = LoadDefaultLogConfig();
            GameConfigBootstrap configBootstrap = FindConfigBootstrap(scene);
            GameLogBootstrap logBootstrap = FindLogBootstrap(scene);
            GameObject servicesObject = configBootstrap != null
                ? configBootstrap.gameObject
                : logBootstrap != null
                    ? logBootstrap.gameObject
                    : FindRoot(scene, "Project Services") ?? new GameObject("Project Services");

            if (logBootstrap == null)
            {
                logBootstrap = servicesObject.AddComponent<GameLogBootstrap>();
            }

            if (configBootstrap == null)
            {
                configBootstrap = servicesObject.AddComponent<GameConfigBootstrap>();
            }

            logBootstrap.Config = logConfig;
            configBootstrap.ConfigSet = configSet;
            EditorUtility.SetDirty(logBootstrap);
            EditorUtility.SetDirty(configBootstrap);

            ProjectFontSweep.Apply(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save scene: {path}");
            }

            ValidateSceneContents(GameSceneId.Bootstrap, scene);
            Debug.Log("Bootstrap project services created and validated.");
        }

        private static void EnsureSceneDirectory()
        {
            if (!AssetDatabase.IsValidFolder(GameSceneCatalog.SceneRoot))
            {
                throw new DirectoryNotFoundException(
                    $"Scene root does not exist: {GameSceneCatalog.SceneRoot}");
            }
        }

        private static void CreateScene(
            GameSceneId sceneId,
            string title,
            string subtitle,
            IReadOnlyList<ButtonSpec> buttons)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateInterface(sceneId, title, subtitle, buttons);
            CreateEventSystem();
            CreateProjectServices(sceneId);
            NetworkLobbySetup.Build(sceneId);
            NormalizeSceneCanvasScales();

            string path = GameSceneCatalog.GetPath(sceneId);
            ProjectFontSweep.Apply(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"Failed to save scene: {path}");
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateInterface(
            GameSceneId sceneId,
            string title,
            string subtitle,
            IReadOnlyList<ButtonSpec> buttons)
        {
            if (sceneId == GameSceneId.Result)
            {
                CreateResultInterface();
                return;
            }

            var canvasObject = new GameObject(
                "Scene UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.localScale = Vector3.one;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one;
            CreateStretchImage("Background", canvasRect, BackgroundColor);

            RectTransform policeBar = CreateRect("Police Accent", canvasRect);
            SetRect(policeBar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(360f, 18f), new Vector2(220f, -44f));
            policeBar.gameObject.AddComponent<Image>().color = PoliceBlue;

            RectTransform thiefBar = CreateRect("Thief Accent", canvasRect);
            SetRect(thiefBar, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(360f, 18f), new Vector2(-220f, -44f));
            thiefBar.gameObject.AddComponent<Image>().color = ThiefRed;

            RectTransform panel = CreateRect("Flow Panel", canvasRect);
            SetRect(
                panel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(860f, 500f),
                Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            CreateText(
                "Scene Label",
                panel,
                GameSceneCatalog.GetName(sceneId).ToUpperInvariant(),
                20,
                FontStyle.Bold,
                MerchantGold,
                new Vector2(0f, 170f),
                new Vector2(720f, 32f));

            CreateText(
                "Title",
                panel,
                title,
                64,
                FontStyle.Bold,
                Color.white,
                new Vector2(0f, 95f),
                new Vector2(780f, 88f));

            CreateText(
                "Subtitle",
                panel,
                subtitle,
                22,
                FontStyle.Normal,
                MutedText,
                new Vector2(0f, 25f),
                new Vector2(760f, 40f));

            float firstButtonY = buttons.Count == 1 ? -105f : -70f;
            for (int index = 0; index < buttons.Count; index++)
            {
                ButtonSpec spec = buttons[index];
                CreateButton(
                    panel,
                    spec.Label,
                    spec.Target,
                    spec.Color,
                    new Vector2(0f, firstButtonY - index * 86f));
            }

            CreateText(
                "Footer",
                canvasRect,
                "POLICE BLUE   /   THIEF RED   /   RACCOON GOLD",
                16,
                FontStyle.Normal,
                MutedText,
                new Vector2(0f, -470f),
                new Vector2(900f, 30f));
        }

        /// <summary>
        /// Drops the result prefab into the scene.
        ///
        /// This used to build the screen inline: the authored mockup stretched
        /// across the canvas with three transparent, captionless buttons pinned
        /// over it, and four result labels created at font size one, fully
        /// transparent and switched off. The clock, the arrests and the gold a
        /// player read were painted into the picture, so they were the same
        /// after every match.
        /// </summary>
        private static void CreateResultInterface()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ResultCanvasBuilder.PrefabPath);
            if (prefab == null)
            {
                // Generated on demand so a fresh clone can rebuild the scenes
                // in one step rather than failing on a missing asset.
                ResultCanvasBuilder.Rebuild();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    ResultCanvasBuilder.PrefabPath);
            }

            if (prefab == null)
            {
                throw new FileNotFoundException(
                    "The result prefab is missing and could not be generated.",
                    ResultCanvasBuilder.PrefabPath);
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate the result prefab: "
                    + ResultCanvasBuilder.PrefabPath);
            }

            instance.name = ResultRootName;
            EditorUtility.SetDirty(instance);

            // The screen fills the canvas with its own cream, but a mismatched
            // clear colour shows for a frame on load and in every editor view
            // that is not playing.
            foreach (GameObject root in
                     SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var camera = root.GetComponentInChildren<Camera>(true);
                if (camera == null)
                {
                    continue;
                }

                camera.backgroundColor = new Color(0.988f, 0.941f, 0.886f, 1f);
                EditorUtility.SetDirty(camera);
                break;
            }
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static void CreateProjectServices(GameSceneId sceneId)
        {
            if (sceneId != GameSceneId.Bootstrap)
            {
                return;
            }

            var servicesObject = new GameObject("Project Services");
            GameLogBootstrap logBootstrap = servicesObject.AddComponent<GameLogBootstrap>();
            logBootstrap.Config = LoadDefaultLogConfig();

            GameConfigBootstrap configBootstrap =
                servicesObject.AddComponent<GameConfigBootstrap>();
            configBootstrap.ConfigSet = LoadDefaultConfigSet();
        }

        private static GameConfigSet LoadDefaultConfigSet()
        {
            GameConfigSet configSet =
                AssetDatabase.LoadAssetAtPath<GameConfigSet>(GameConfigSetup.DefaultSetPath);
            if (configSet == null)
            {
                throw new GameConfigurationException(
                    $"Required GameConfigSet is missing at '{GameConfigSetup.DefaultSetPath}'. " +
                    "Run 'Paws & Loot/Setup/Create Default Config Assets' first.");
            }

            return configSet;
        }

        private static GameLogConfig LoadDefaultLogConfig()
        {
            GameLogConfig logConfig =
                AssetDatabase.LoadAssetAtPath<GameLogConfig>(GameLogSetup.DefaultConfigPath);
            if (logConfig == null)
            {
                throw new GameConfigurationException(
                    $"Required GameLogConfig is missing at '{GameLogSetup.DefaultConfigPath}'. " +
                    "Run 'Paws & Loot/Setup/Create Default Log Config' first.");
            }

            return logConfig;
        }

        private static GameConfigBootstrap FindConfigBootstrap(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameConfigBootstrap bootstrap =
                    root.GetComponentInChildren<GameConfigBootstrap>(true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }

            return null;
        }

        private static GameLogBootstrap FindLogBootstrap(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameLogBootstrap bootstrap =
                    root.GetComponentInChildren<GameLogBootstrap>(true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static bool SceneHasComponent<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<T>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateButton(
            RectTransform parent,
            string label,
            GameSceneId target,
            Color color,
            Vector2 position)
        {
            RectTransform buttonRect = CreateRect($"{label} Button", parent);
            SetRect(buttonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 64f), position);

            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = color;

            Button button = buttonRect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            SceneNavigationButton navigation = buttonRect.gameObject.AddComponent<SceneNavigationButton>();
            navigation.TargetScene = target;

            CreateText(
                "Label",
                buttonRect,
                label,
                24,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                new Vector2(390f, 54f));
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 position,
            Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);

            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateStretchImage(string name, RectTransform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void NormalizeSceneCanvasScales()
        {
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                RectTransform rect = canvas.GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                EditorUtility.SetDirty(rect);
            }
        }

        private static void ApplyBuildSettings()
        {
            var buildScenes = new EditorBuildSettingsScene[GameSceneCatalog.BuildOrder.Count];

            for (int index = 0; index < GameSceneCatalog.BuildOrder.Count; index++)
            {
                GameSceneId sceneId = GameSceneCatalog.BuildOrder[index];
                buildScenes[index] = new EditorBuildSettingsScene(GameSceneCatalog.GetPath(sceneId), true);
            }

            EditorBuildSettings.scenes = buildScenes;
        }

        private static void ValidateBuildSettings()
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length != GameSceneCatalog.BuildOrder.Count)
            {
                throw new InvalidOperationException(
                    $"Expected {GameSceneCatalog.BuildOrder.Count} build scenes, found {buildScenes.Length}.");
            }

            for (int index = 0; index < GameSceneCatalog.BuildOrder.Count; index++)
            {
                string expectedPath = GameSceneCatalog.GetPath(GameSceneCatalog.BuildOrder[index]);
                EditorBuildSettingsScene actual = buildScenes[index];

                if (!actual.enabled || !string.Equals(actual.path, expectedPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Build scene {index} must be enabled and use path '{expectedPath}'.");
                }
            }
        }

        private static void ValidateSceneContents(GameSceneId sceneId, Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"Scene failed to load: {sceneId}");
            }

            bool hasCamera = false;
            bool hasCanvas = false;
            bool hasEventSystem = false;
            var navigationTargets = new List<GameSceneId>();
            GameConfigBootstrap configBootstrap = null;
            GameLogBootstrap logBootstrap = null;
            ResultScreenPresenter resultPresenter = null;
            ApplicationQuitButton quitButton = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                hasCamera |= root.GetComponentInChildren<Camera>(true) != null;
                hasCanvas |= root.GetComponentInChildren<Canvas>(true) != null;
                hasEventSystem |= root.GetComponentInChildren<EventSystem>(true) != null;
                configBootstrap ??= root.GetComponentInChildren<GameConfigBootstrap>(true);
                logBootstrap ??= root.GetComponentInChildren<GameLogBootstrap>(true);
                resultPresenter ??=
                    root.GetComponentInChildren<
                        ResultScreenPresenter>(true);
                quitButton ??=
                    root.GetComponentInChildren<
                        ApplicationQuitButton>(true);

                foreach (SceneNavigationButton button in root.GetComponentsInChildren<SceneNavigationButton>(true))
                {
                    navigationTargets.Add(button.TargetScene);
                }
            }

            if (!hasCamera || !hasCanvas || !hasEventSystem)
            {
                throw new InvalidOperationException(
                    $"Scene '{sceneId}' must contain a Camera, Canvas, and EventSystem.");
            }

            IReadOnlyList<GameSceneId> expectedTargets = GetExpectedNavigationTargets(sceneId);
            if (navigationTargets.Count != expectedTargets.Count)
            {
                throw new InvalidOperationException(
                    $"Scene '{sceneId}' has {navigationTargets.Count} navigation targets; expected {expectedTargets.Count}.");
            }

            foreach (GameSceneId expectedTarget in expectedTargets)
            {
                if (!navigationTargets.Contains(expectedTarget))
                {
                    throw new InvalidOperationException(
                        $"Scene '{sceneId}' is missing navigation to '{expectedTarget}'.");
                }
            }

            if (sceneId == GameSceneId.Bootstrap)
            {
                if (configBootstrap == null)
                {
                    throw new InvalidOperationException(
                        "Bootstrap scene must contain a GameConfigBootstrap component.");
                }

                if (configBootstrap.ConfigSet == null)
                {
                    throw new GameConfigurationException(
                        "Bootstrap GameConfigBootstrap is missing its required GameConfigSet reference.");
                }

                configBootstrap.ConfigSet.ValidateOrThrow();

                if (logBootstrap == null)
                {
                    throw new InvalidOperationException(
                        "Bootstrap scene must contain a GameLogBootstrap component.");
                }

                if (logBootstrap.Config == null)
                {
                    throw new GameConfigurationException(
                        "Bootstrap GameLogBootstrap is missing its required GameLogConfig reference.");
                }

                logBootstrap.Config.ValidateOrThrow();
            }
            else if (sceneId == GameSceneId.Result)
            {
                if (resultPresenter == null)
                {
                    throw new InvalidOperationException(
                        "Result scene must contain one ResultScreenPresenter.");
                }

                resultPresenter.ValidateOrThrow();
                if (quitButton == null)
                {
                    throw new InvalidOperationException(
                        "Result scene must contain one ApplicationQuitButton.");
                }
            }
        }

        private static IReadOnlyList<GameSceneId> GetExpectedNavigationTargets(GameSceneId sceneId)
        {
            return sceneId switch
            {
                GameSceneId.Bootstrap => new[] { GameSceneId.Game },
                GameSceneId.Game => Array.Empty<GameSceneId>(),
                GameSceneId.Result => new[] { GameSceneId.Game, GameSceneId.Bootstrap },
                _ => throw new ArgumentOutOfRangeException(nameof(sceneId), sceneId, "Unknown game scene.")
            };
        }

        private readonly struct ButtonSpec
        {
            public ButtonSpec(string label, GameSceneId target, Color color)
            {
                Label = label;
                Target = target;
                Color = color;
            }

            public string Label { get; }
            public GameSceneId Target { get; }
            public Color Color { get; }
        }
    }
}
