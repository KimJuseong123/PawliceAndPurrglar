using System;
using System.IO;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.TechnicalValidation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Editor
{
    public static class VoiceTechnicalValidationSetup
    {
        public const string WindowsBuildPath =
            "Builds/TechnicalValidation/Windows/PawliceAndPurrglarVoiceTech.exe";

        [MenuItem("PawliceAndPurrglar/Technical Validation/Create TECH-003 Scene")]
        public static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var cameraObject =
                new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);

            var probeObject = new GameObject(
                "TECH-003 Windows Dictation Probe",
                typeof(WindowsDictationProbe));

            string scenePath =
                GameSceneCatalog.GetPath(GameSceneId.VoiceTechnicalTest);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save voice validation scene: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            ValidateScene();
        }

        [MenuItem("PawliceAndPurrglar/Technical Validation/Validate TECH-003 Scene")]
        public static void ValidateScene()
        {
            string scenePath =
                GameSceneCatalog.GetPath(GameSceneId.VoiceTechnicalTest);
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    $"Voice validation scene is missing: {scenePath}",
                    scenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            WindowsDictationProbe probe = FindInScene<WindowsDictationProbe>(scene);
            if (scene.name != "VoiceTechnicalTest"
                || Camera.main == null
                || probe == null)
            {
                throw new InvalidOperationException(
                    "TECH-003 scene is missing its camera or Windows dictation probe.");
            }
        }

        [MenuItem("PawliceAndPurrglar/Technical Validation/Build Windows TECH-003")]
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
                        GameSceneCatalog.GetPath(GameSceneId.VoiceTechnicalTest)
                    },
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"TECH-003 Windows build failed with result {report.summary.result} " +
                    $"and {report.summary.totalErrors} errors.");
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
