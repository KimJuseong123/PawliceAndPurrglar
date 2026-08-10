using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PawliceAndPurrglar.TechnicalValidation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Editor
{
    public static class CharacterTechnicalValidationSetup
    {
        private const string ScenePath =
            "Assets/_Project/Scenes/CharacterTechnicalTest.unity";
        private const string AssetRoot =
            "Assets/_Project/Art/Characters";
        private const string ControllerRoot =
            AssetRoot + "/GeneratedControllers";
        private const string WindowsBuildPath =
            "Builds/TechnicalValidation/Windows/PawliceAndPurrglarCharacterTech.exe";

        private static readonly CharacterSource[] CharacterSources =
        {
            new(
                "police",
                "Police",
                "ArtSource/Blender/Characters/police+officer+3d+model/tripo_convert_2e70f84e-28fa-4956-a8e1-1624e84b9a83.fbx",
                "Assets/_Project/Art/Characters/Police/police.fbx",
                new Vector3(-6f, 0f, 0f),
                true),
            new(
                "thief",
                "Thief",
                "ArtSource/Blender/Characters/theif+3d+model/tripo_convert_ddd97310-d16a-4698-bd8a-d7dbe976dce5.fbx",
                "Assets/_Project/Art/Characters/Thief/thief.fbx",
                new Vector3(-2f, 0f, 0f),
                false),
            new(
                "dog",
                "Dog",
                "ArtSource/Blender/Animals/dog/tripo_convert_eafe761a-3586-4647-91bb-a6f1bad8aa50.fbx",
                "Assets/_Project/Art/Characters/Dog/dog.fbx",
                new Vector3(2f, 0f, 0f),
                false),
            new(
                "cat",
                "Cat",
                "ArtSource/Blender/Animals/cat/tripo_convert_6214ab8d-ecd4-4b9a-9b9f-88c9d8c18057.fbx",
                "Assets/_Project/Art/Characters/Cat/cat.fbx",
                new Vector3(6f, 0f, 0f),
                false)
        };

        [MenuItem("PawliceAndPurrglar/Technical Validation/Create CHAR-001 Scene")]
        public static void CreateScene()
        {
            SyncCharacterAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);

            List<CharacterAssetInfo> importedAssets = CharacterSources
                .Select(PrepareCharacterAsset)
                .ToList();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CreateCamera();
            CreateLight();
            CreateFloor();

            var previews = new List<CharacterTechnicalPreview>();
            foreach (CharacterAssetInfo asset in importedAssets)
            {
                previews.Add(CreatePreview(asset));
            }

            var reporterObject = new GameObject("CHAR-001 Reporter");
            CharacterTechnicalValidationReporter reporter =
                reporterObject.AddComponent<CharacterTechnicalValidationReporter>();
            reporter.Characters = previews.ToArray();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save character validation scene: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            ValidateScene();
        }

        [MenuItem("PawliceAndPurrglar/Technical Validation/Validate CHAR-001 Scene")]
        public static void ValidateScene()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException(
                    $"Character validation scene is missing: {ScenePath}",
                    ScenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CharacterTechnicalPreview[] previews = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterTechnicalPreview>(true))
                .ToArray();
            CharacterTechnicalValidationReporter reporter = scene
                .GetRootGameObjects()
                .Select(root => root.GetComponentInChildren<CharacterTechnicalValidationReporter>(true))
                .FirstOrDefault(component => component != null);

            if (scene.name != "CharacterTechnicalTest"
                || Camera.main == null
                || previews.Length != CharacterSources.Length
                || reporter == null)
            {
                throw new InvalidOperationException(
                    "CHAR-001 scene is missing its camera, character previews, or reporter.");
            }

            foreach (CharacterTechnicalPreview preview in previews)
            {
                if (preview.VisualRoot == null)
                {
                    throw new InvalidOperationException(
                        $"Character preview '{preview.name}' is missing its VisualRoot.");
                }

                if (preview.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Character preview '{preview.name}' has no renderers.");
                }
            }
        }

        [MenuItem("PawliceAndPurrglar/Technical Validation/Build Windows CHAR-001")]
        public static void BuildWindows()
        {
            CreateScene();

            string absoluteBuildPath = Path.GetFullPath(WindowsBuildPath);
            string buildDirectory = Path.GetDirectoryName(absoluteBuildPath);
            if (string.IsNullOrWhiteSpace(buildDirectory))
            {
                throw new InvalidOperationException(
                    $"Could not determine build directory for '{absoluteBuildPath}'.");
            }

            Directory.CreateDirectory(buildDirectory);
            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"CHAR-001 Windows build failed with result {report.summary.result} " +
                    $"and {report.summary.totalErrors} errors.");
            }
        }

        private static void SyncCharacterAssets()
        {
            foreach (CharacterSource source in CharacterSources)
            {
                string targetDirectory = Path.GetDirectoryName(source.TargetFbxPath);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    throw new InvalidOperationException(
                        $"Could not resolve target directory for '{source.TargetFbxPath}'.");
                }

                Directory.CreateDirectory(targetDirectory);
                CopyIfDifferent(source.SourceFbxPath, source.TargetFbxPath);

                string sourceTextureDirectory = Path.ChangeExtension(source.SourceFbxPath, ".fbm");
                if (!Directory.Exists(sourceTextureDirectory))
                {
                    continue;
                }

                string targetTextureDirectory = Path.ChangeExtension(source.TargetFbxPath, ".fbm");
                Directory.CreateDirectory(targetTextureDirectory);
                foreach (string sourceFile in Directory.GetFiles(sourceTextureDirectory))
                {
                    string targetFile = Path.Combine(
                        targetTextureDirectory,
                        Path.GetFileName(sourceFile));
                    CopyIfDifferent(sourceFile, targetFile);
                }
            }
        }

        private static CharacterAssetInfo PrepareCharacterAsset(CharacterSource source)
        {
            AssetDatabase.ImportAsset(
                source.TargetFbxPath,
                ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(source.TargetFbxPath) is not ModelImporter importer)
            {
                throw new InvalidOperationException(
                    $"Could not load ModelImporter for '{source.TargetFbxPath}'.");
            }

            bool changed =
                importer.animationType != ModelImporterAnimationType.Generic
                || !importer.importAnimation
                || importer.optimizeGameObjects
                || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.bakeAxisConversion = false;
            importer.preserveHierarchy = true;

            ModelImporterClipAnimation[] clips =
                importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;
            for (int index = 0; index < clips.Length; index++)
            {
                clips[index].loopTime = true;
            }

            importer.clipAnimations = clips;
            if (changed)
            {
                importer.SaveAndReimport();
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(source.TargetFbxPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException(
                    $"Could not load imported FBX '{source.TargetFbxPath}'.");
            }

            List<AnimationClip> importedClips = LoadAnimationClips(source.TargetFbxPath);
            AnimatorController controller = CreateAnimatorController(source, importedClips);
            string defaultClipName = ResolveDefaultClipName(importedClips);

            return new CharacterAssetInfo(
                source,
                modelAsset,
                controller,
                importedClips,
                defaultClipName);
        }

        private static CharacterTechnicalPreview CreatePreview(CharacterAssetInfo asset)
        {
            var root = new GameObject(asset.Source.DisplayName);
            root.transform.position = asset.Source.ScenePosition;

            if (asset.Source.MovementDriven)
            {
                root.AddComponent<KeyboardCubeMover>();
            }

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            GameObject instance = PrefabUtility.InstantiatePrefab(
                asset.ModelAsset,
                visualRoot.transform) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate model '{asset.Source.TargetFbxPath}'.");
            }

            instance.name = asset.Source.Id + "_model";
            instance.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = asset.Controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var preview = root.AddComponent<CharacterTechnicalPreview>();
            preview.Initialize(
                asset.Source.Id,
                asset.Source.SourceFbxPath,
                asset.Source.TargetFbxPath,
                visualRoot.transform,
                animator,
                asset.Source.MovementDriven,
                asset.DefaultClipName,
                asset.Clips);
            return preview;
        }

        private static List<AnimationClip> LoadAnimationClips(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static AnimatorController CreateAnimatorController(
            CharacterSource source,
            IReadOnlyList<AnimationClip> clips)
        {
            string controllerPath = $"{ControllerRoot}/{source.Id}.controller";
            EnsureAssetFolder(AssetRoot);
            EnsureAssetFolder(ControllerRoot);

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            if (clips.Count == 0)
            {
                return controller;
            }

            AnimationClip idleClip = FindClipByAlias(clips, "idle");
            AnimationClip walkClip = FindClipByAlias(clips, "walk");
            AnimationClip defaultClip = idleClip ?? walkClip ?? clips[0];

            var states = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);
            foreach (AnimationClip clip in clips)
            {
                AnimatorState state = stateMachine.AddState(clip.name);
                state.motion = clip;
                states[clip.name] = state;
            }

            stateMachine.defaultState = states[defaultClip.name];

            if (idleClip != null && walkClip != null)
            {
                controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

                AnimatorState idleState = states[idleClip.name];
                AnimatorState walkState = states[walkClip.name];

                AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
                idleToWalk.hasExitTime = false;
                idleToWalk.duration = 0.12f;
                idleToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

                AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
                walkToIdle.hasExitTime = false;
                walkToIdle.duration = 0.12f;
                walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            }

            return controller;
        }

        private static AnimationClip FindClipByAlias(
            IReadOnlyList<AnimationClip> clips,
            string alias)
        {
            return clips.FirstOrDefault(
                clip => clip.name.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string ResolveDefaultClipName(IReadOnlyList<AnimationClip> clips)
        {
            if (clips.Count == 0)
            {
                return string.Empty;
            }

            return FindClipByAlias(clips, "idle")?.name
                ?? FindClipByAlias(clips, "walk")?.name
                ?? clips[0].name;
        }

        private static void CreateCamera()
        {
            var cameraObject =
                new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 6.5f, -12f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.25f, 0f));

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
            camera.fieldOfView = 40f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Validation Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(18f, 0.2f, 10f);
            floor.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Materials/TechnicalValidation/Floor.mat");
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static void CopyIfDifferent(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"Required source asset is missing: {sourcePath}",
                    sourcePath);
            }

            bool shouldCopy = !File.Exists(targetPath);
            if (!shouldCopy)
            {
                var sourceInfo = new FileInfo(sourcePath);
                var targetInfo = new FileInfo(targetPath);
                shouldCopy =
                    sourceInfo.Length != targetInfo.Length
                    || sourceInfo.LastWriteTimeUtc != targetInfo.LastWriteTimeUtc;
            }

            if (!shouldCopy)
            {
                return;
            }

            File.Copy(sourcePath, targetPath, true);
            File.SetLastWriteTimeUtc(targetPath, File.GetLastWriteTimeUtc(sourcePath));
        }

        private readonly struct CharacterSource
        {
            public CharacterSource(
                string id,
                string displayName,
                string sourceFbxPath,
                string targetFbxPath,
                Vector3 scenePosition,
                bool movementDriven)
            {
                Id = id;
                DisplayName = displayName;
                SourceFbxPath = sourceFbxPath;
                TargetFbxPath = targetFbxPath;
                ScenePosition = scenePosition;
                MovementDriven = movementDriven;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string SourceFbxPath { get; }
            public string TargetFbxPath { get; }
            public Vector3 ScenePosition { get; }
            public bool MovementDriven { get; }
        }

        private readonly struct CharacterAssetInfo
        {
            public CharacterAssetInfo(
                CharacterSource source,
                GameObject modelAsset,
                AnimatorController controller,
                IReadOnlyList<AnimationClip> clips,
                string defaultClipName)
            {
                Source = source;
                ModelAsset = modelAsset;
                Controller = controller;
                Clips = clips;
                DefaultClipName = defaultClipName;
            }

            public CharacterSource Source { get; }
            public GameObject ModelAsset { get; }
            public AnimatorController Controller { get; }
            public IReadOnlyList<AnimationClip> Clips { get; }
            public string DefaultClipName { get; }
        }
    }
}
