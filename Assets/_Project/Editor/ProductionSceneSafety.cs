using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Prevents the production Game scene from being replaced silently or
    /// saved with validation-only assets.
    /// </summary>
    [InitializeOnLoad]
    public static class ProductionSceneSafety
    {
        private const string GameScenePath =
            "Assets/_Project/Scenes/Game.unity";
        private const string BackupRoot =
            "Library/PawliceAndPurrglar/SceneBackups";

        static ProductionSceneSafety()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        public static void RequireProductionSceneCreationApproval()
        {
            RequiredAuthoredAssetValidator.ValidateAllOrThrow();

            if (!File.Exists(GameScenePath))
            {
                return;
            }

            if (Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "Refusing to replace existing Game.unity in batch mode. "
                    + "Run the explicit editor rebuild command after confirmation.");
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Replace production Game scene?",
                "This will create a new Game.unity after an automatic backup. "
                + "Continue only after confirming that authored Git LFS assets are available.",
                "Backup and rebuild",
                "Cancel");
            if (!confirmed)
            {
                throw new OperationCanceledException(
                    "Production Game scene rebuild was canceled; the existing scene was preserved.");
            }

            BackupExistingGameScene();
        }

        public static void RejectValidationReferences(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                InspectGameObject(root);
            }
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (!string.Equals(path, GameScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RejectValidationReferences(scene);
        }

        private static void InspectGameObject(GameObject gameObject)
        {
            RejectAssetPath(
                GetPrefabSourcePath(gameObject),
                gameObject.name);

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                InspectSerializedReferences(component);
            }

            foreach (Transform child in gameObject.transform)
            {
                InspectGameObject(child.gameObject);
            }
        }

        private static void InspectSerializedReferences(Component component)
        {
            try
            {
                var serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.objectReferenceValue == null)
                    {
                        continue;
                    }

                    RejectAssetPath(
                        AssetDatabase.GetAssetPath(property.objectReferenceValue),
                        $"{component.GetType().Name}.{property.name}");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not inspect production scene references on '{component.name}': "
                    + exception.Message);
            }
        }

        private static string GetPrefabSourcePath(GameObject gameObject)
        {
            UnityEngine.Object source =
                PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            return source == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(source);
        }

        private static void RejectAssetPath(string path, string owner)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string normalised = path.Replace('\\', '/');
            string forbidden = RequiredAuthoredAssetValidator.GeneratedValidationRoot;
            if (string.Equals(normalised, forbidden, StringComparison.OrdinalIgnoreCase)
                || normalised.StartsWith(forbidden + "/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Production Game.unity save rejected: '{owner}' references "
                    + $"validation-only asset '{normalised}'.");
            }
        }

        private static void BackupExistingGameScene()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(
                projectRoot,
                GameScenePath.Replace('/', Path.DirectorySeparatorChar));
            string backupDirectory = Path.Combine(
                projectRoot,
                BackupRoot.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(backupDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupPath = Path.Combine(
                backupDirectory,
                $"Game.unity.{timestamp}.bak");
            File.Copy(source, backupPath, false);

            string metaSource = source + ".meta";
            if (File.Exists(metaSource))
            {
                File.Copy(metaSource, backupPath + ".meta", false);
            }

            Debug.Log($"Backed up existing production Game scene to '{backupPath}'.");
        }
    }
}
