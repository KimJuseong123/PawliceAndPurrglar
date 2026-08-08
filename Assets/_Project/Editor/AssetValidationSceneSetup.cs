using System;
using System.IO;
using System.Linq;
using PawsAndLoot.TechnicalValidation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Explicit validation-only scene. Primitive fallback prefabs can be
    /// generated here for diagnostics, but this code never touches Game.unity
    /// and is not called by the production scene builder.
    /// </summary>
    public static class AssetValidationSceneSetup
    {
        private const string ScenePath =
            "Assets/_Project/Scenes/AssetValidation.unity";
        private const string ValidationRoot =
            RequiredAuthoredAssetValidator.GeneratedValidationRoot;

        [MenuItem("Pawlice and Purrglar/Technical Validation/Create Asset Validation Scene")]
        public static void CreateScene()
        {
            EnsureFolder("Assets/_Project/Art/Generated");
            EnsureFolder(ValidationRoot);
            EnsureFolder(ValidationRoot + "/Characters");
            EnsureFolder(ValidationRoot + "/Buildings");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject root = new GameObject("Asset Validation Only");

            string[] characters = { "dog", "cat", "raccoon", "thief" };
            foreach (string stem in characters)
            {
                CreatePreview(
                    root.transform,
                    stem,
                    $"Assets/_Project/Art/Characters/{stem}.fbx",
                    ValidationRoot + "/Characters/" + stem + ".prefab",
                    () => LocalValidationModelFactory.CreateCharacter(stem, null, 1.5f));
            }

            string[] buildings =
            {
                "building_bookstore",
                "building_house_1f",
                "building_house_1f_with_interior",
                "building_police_station",
                "building_supermarket"
            };
            foreach (string stem in buildings)
            {
                CreatePreview(
                    root.transform,
                    stem,
                    $"Assets/_Project/Art/Buildings/{stem}.fbx",
                    ValidationRoot + "/Buildings/" + stem + ".prefab",
                    () => LocalValidationModelFactory.CreateBuilding(stem, null));
            }

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save validation-only scene: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Created validation-only asset scene at {ScenePath}. "
                + "Generated assets are forbidden in Game.unity.");
        }

        private static void CreatePreview(
            Transform parent,
            string stem,
            string authoredPath,
            string fallbackPrefabPath,
            Func<GameObject> fallbackFactory)
        {
            GameObject model = null;
            if (IsHydrated(authoredPath))
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(authoredPath);
                model = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            }

            if (model == null)
            {
                EnsureFallbackPrefab(fallbackPrefabPath, fallbackFactory);
                GameObject fallback = AssetDatabase.LoadAssetAtPath<GameObject>(fallbackPrefabPath);
                model = PrefabUtility.InstantiatePrefab(fallback, parent) as GameObject;
            }

            if (model == null)
            {
                throw new InvalidOperationException(
                    $"Could not create validation preview for '{stem}'.");
            }

            model.name = "VALIDATION ONLY - " + stem;
            model.transform.position = new Vector3(
                parent.childCount % 5 * 5f,
                0f,
                parent.childCount / 5 * 5f);
        }

        private static void EnsureFallbackPrefab(
            string path,
            Func<GameObject> factory)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject source = factory();
            AssignValidationMaterial(source);
            PrefabUtility.SaveAsPrefabAsset(source, path);
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path);
        }

        private static void AssignValidationMaterial(GameObject source)
        {
            const string materialPath = ValidationRoot + "/ValidationFallback.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                material = new Material(shader)
                {
                    name = "ValidationFallback"
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, 1).ToArray();
            }
        }

        private static bool IsHydrated(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath)) return false;
            using StreamReader reader = File.OpenText(absolutePath);
            string firstLine = reader.ReadLine();
            return firstLine == null
                || !firstLine.StartsWith(
                    "version https://git-lfs.github.com/spec/v1",
                    StringComparison.Ordinal);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
