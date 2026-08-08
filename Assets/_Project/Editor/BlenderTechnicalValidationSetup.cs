using System;
using System.IO;
using System.Linq;
using PawsAndLoot.Core;
using PawsAndLoot.TechnicalValidation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    public static class BlenderTechnicalValidationSetup
    {
        public const string ModelPath =
            "Assets/_Project/Art/TechnicalValidation/TechRig.fbx";
        public const string ControllerPath =
            "Assets/_Project/Art/TechnicalValidation/TechRig.controller";
        public const string PrefabPath =
            "Assets/_Project/Prefabs/TechnicalValidation/TechRigPlayer.prefab";
        public const string WindowsBuildPath =
            "Builds/TechnicalValidation/Windows/PawsAndLootBlenderTech.exe";

        [MenuItem("Pawlice and Purrglar/Technical Validation/Create TECH-002 Scene")]
        public static void CreateScene()
        {
            ConfigureModelImporter();
            AnimationClip idle = LoadClip("Idle");
            AnimationClip walk = LoadClip("Walk");
            AnimatorController controller = CreateAnimatorController(idle, walk);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CreateCamera();
            CreateLight();
            CreateFloor();
            VisualRootContract player = CreatePlayer(controller);

            var reporterObject = new GameObject("TECH-002 Reporter");
            BlenderValidationReporter reporter =
                reporterObject.AddComponent<BlenderValidationReporter>();
            reporter.Player = player;
            reporter.SkinnedMesh =
                player.VisualAnimator.GetComponentInChildren<SkinnedMeshRenderer>(true);

            string scenePath =
                GameSceneCatalog.GetPath(GameSceneId.BlenderTechnicalTest);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save Blender validation scene: {scenePath}");
            }

            SavePlayerPrefab(player.gameObject);
            AssetDatabase.SaveAssets();
            ValidateScene();
        }

        [MenuItem("Pawlice and Purrglar/Technical Validation/Validate TECH-002 Scene")]
        public static void ValidateScene()
        {
            string scenePath =
                GameSceneCatalog.GetPath(GameSceneId.BlenderTechnicalTest);
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    $"Blender validation scene is missing: {scenePath}",
                    scenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            VisualRootContract player = FindInScene<VisualRootContract>(scene);
            BlenderValidationReporter reporter =
                FindInScene<BlenderValidationReporter>(scene);

            if (scene.name != "BlenderTechnicalTest"
                || Camera.main == null
                || player == null
                || reporter == null)
            {
                throw new InvalidOperationException(
                    "TECH-002 scene is missing its camera, player, or reporter.");
            }

            player.ValidateOrThrow();
            ValidateImportedModel(
                player.VisualAnimator.GetComponentInChildren<SkinnedMeshRenderer>(true),
                player.VisualAnimator.runtimeAnimatorController);
        }

        [MenuItem("Pawlice and Purrglar/Technical Validation/Build Windows TECH-002")]
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
                    scenes = new[]
                    {
                        GameSceneCatalog.GetPath(GameSceneId.BlenderTechnicalTest)
                    },
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"TECH-002 Windows build failed with result {report.summary.result} " +
                    $"and {report.summary.totalErrors} errors.");
            }
        }

        private static void ConfigureModelImporter()
        {
            if (!File.Exists(ModelPath))
            {
                throw new FileNotFoundException(
                    "Run the Blender TECH-002 generator before creating the Unity scene.",
                    ModelPath);
            }

            AssetDatabase.ImportAsset(
                ModelPath,
                ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
            {
                throw new InvalidOperationException(
                    $"Could not load ModelImporter for '{ModelPath}'.");
            }

            bool changed =
                importer.animationType != ModelImporterAnimationType.Generic
                || !importer.importAnimation
                || !Mathf.Approximately(importer.globalScale, 1f);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.globalScale = 1f;
            importer.bakeAxisConversion = false;
            importer.optimizeGameObjects = false;

            ModelImporterClipAnimation[] clips =
                importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip.name.EndsWith("Idle", StringComparison.OrdinalIgnoreCase))
                {
                    changed |= clip.name != "Idle";
                    clip.name = "Idle";
                }
                else if (clip.name.EndsWith("Walk", StringComparison.OrdinalIgnoreCase))
                {
                    changed |= clip.name != "Walk";
                    clip.name = "Walk";
                }
            }

            importer.clipAnimations = clips;

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip LoadClip(string clipName)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == clipName);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"TECH-002 FBX does not contain animation clip '{clipName}'.");
            }

            return clip;
        }

        private static AnimatorController CreateAnimatorController(
            AnimationClip idle,
            AnimationClip walk)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            stateMachine.defaultState = walkState;
            return controller;
        }

        private static VisualRootContract CreatePlayer(
            RuntimeAnimatorController controller)
        {
            var player = new GameObject("PlayerRoot");
            var collider = player.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.1f, 0f);
            collider.height = 2.2f;
            collider.radius = 0.48f;
            player.AddComponent<KeyboardCubeMover>();

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(player.transform, false);

            GameObject modelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject model = PrefabUtility.InstantiatePrefab(
                modelAsset,
                visualRoot.transform) as GameObject;
            if (model == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate TECH-002 model '{ModelPath}'.");
            }

            model.name = "TechRigModel";
            model.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            model.transform.localScale = Vector3.one;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var contract = player.AddComponent<VisualRootContract>();
            contract.Initialize(visualRoot.transform, collider, animator);
            return contract;
        }

        private static void SavePlayerPrefab(GameObject player)
        {
            EnsureFolder("Assets/_Project/Prefabs", "TechnicalValidation");
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
        }

        private static void ValidateImportedModel(
            SkinnedMeshRenderer renderer,
            RuntimeAnimatorController controller)
        {
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    "TECH-002 model must contain one SkinnedMeshRenderer.");
            }

            AnimationClip[] clips = controller.animationClips;
            if (renderer.bones.Length != 5
                || renderer.sharedMaterials.Length != 1
                || !clips.Any(clip => clip.name == "Idle")
                || !clips.Any(clip => clip.name == "Walk"))
            {
                throw new InvalidOperationException(
                    "TECH-002 model must have 5 bones, 1 material, Idle, and Walk.");
            }
        }

        private static void CreateCamera()
        {
            var cameraObject =
                new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(4.8f, 3.8f, -7f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.05f, 0f));

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
            camera.fieldOfView = 42f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Validation Floor";
            floor.transform.position = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(12f, 0.3f, 12f);
            floor.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Materials/TechnicalValidation/Floor.mat");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
