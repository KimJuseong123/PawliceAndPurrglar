using System;
using System.IO;
using PawsAndLoot.Core;
using PawsAndLoot.TechnicalValidation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    public static class TechnicalValidationSetup
    {
        public const string WindowsBuildPath =
            "Builds/TechnicalValidation/Windows/PawsAndLootTech.exe";

        private const string MaterialRoot =
            "Assets/_Project/Materials/TechnicalValidation";
        private const string FloorMaterialPath = MaterialRoot + "/Floor.mat";
        private const string CubeMaterialPath = MaterialRoot + "/PlayerCube.mat";

        [MenuItem("PawliceAndPurrglar/Technical Validation/Create TECH-001 Scene")]
        public static void CreateTechnicalTestScene()
        {
            EnsureMaterialFolder();

            Material floorMaterial = LoadOrCreateMaterial(
                FloorMaterialPath,
                new Color(0.16f, 0.19f, 0.22f));
            Material cubeMaterial = LoadOrCreateMaterial(
                CubeMaterialPath,
                new Color(0.08f, 0.32f, 0.82f));

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CreateCamera();
            CreateLight();
            CreateFloor(floorMaterial);
            Transform cube = CreateMovementCube(cubeMaterial);

            var reporterObject = new GameObject("TECH-001 Reporter");
            TechnicalValidationReporter reporter =
                reporterObject.AddComponent<TechnicalValidationReporter>();
            reporter.MovementTarget = cube;

            string scenePath = GameSceneCatalog.GetPath(GameSceneId.TechnicalTest);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save technical validation scene: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            ValidateTechnicalTestScene();
            Debug.Log("TECH-001 scene created and validated.");
        }

        [MenuItem("PawliceAndPurrglar/Technical Validation/Validate TECH-001 Scene")]
        public static void ValidateTechnicalTestScene()
        {
            string scenePath = GameSceneCatalog.GetPath(GameSceneId.TechnicalTest);
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    $"Technical validation scene is missing: {scenePath}",
                    scenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            KeyboardCubeMover mover = FindInScene<KeyboardCubeMover>(scene);
            TechnicalValidationReporter reporter =
                FindInScene<TechnicalValidationReporter>(scene);

            if (scene.name != "TechnicalTest"
                || Camera.main == null
                || mover == null
                || reporter == null
                || reporter.MovementTarget != mover.transform)
            {
                throw new InvalidOperationException(
                    "TECH-001 scene is missing its camera, movement cube, or reporter connection.");
            }

            Debug.Log("TECH-001 scene validation passed.");
        }

        [MenuItem("PawliceAndPurrglar/Technical Validation/Build Windows TECH-001")]
        public static void BuildWindowsTechnicalValidation()
        {
            CreateTechnicalTestScene();

            PlayerSettings.companyName = "PawsAndLoot";
            PlayerSettings.productName = "PawsAndLoot";

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
                        GameSceneCatalog.GetPath(GameSceneId.TechnicalTest)
                    },
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"TECH-001 Windows build failed with result {report.summary.result} " +
                    $"and {report.summary.totalErrors} errors.");
            }

            Debug.Log(
                $"TECH-001 Windows build succeeded: '{absoluteBuildPath}', " +
                $"{report.summary.totalSize} bytes.");
        }

        private static void CreateCamera()
        {
            var cameraObject =
                new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 8f, -8f);
            cameraObject.transform.LookAt(Vector3.zero);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
            camera.fieldOfView = 48f;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static void CreateFloor(Material material)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Validation Floor";
            floor.transform.position = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(14f, 0.3f, 14f);
            floor.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform CreateMovementCube(Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Keyboard Movement Cube";
            cube.transform.position = new Vector3(0f, 0.6f, 0f);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            cube.AddComponent<KeyboardCubeMover>();
            return cube.transform;
        }

        private static Material LoadOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "URP Lit shader is unavailable for technical validation materials.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialRoot))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Project/Materials",
                    "TechnicalValidation");
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
