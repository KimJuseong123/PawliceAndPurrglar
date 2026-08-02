using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Production asset gate. This validator deliberately fails before a
    /// production scene is created when an authored model is missing,
    /// unresolved, or structurally invalid.
    /// </summary>
    public static class RequiredAuthoredAssetValidator
    {
        public const string RequiredLfsAssetsError =
            "Required Git LFS assets are unavailable. Run git lfs pull and retry.";

        public const string GeneratedValidationRoot =
            "Assets/_Project/Art/Generated/Validation";

        private const string CharacterRoot =
            "Assets/_Project/Art/Characters";
        private const string BuildingRoot =
            "Assets/_Project/Art/Buildings";
        private const string ControllerPath =
            CharacterRoot + "/CharacterLocomotion.controller";
        private const string GameScenePath =
            "Assets/_Project/Scenes/Game.unity";

        private static readonly string[] CharacterStems =
        {
            "police",
            "dog",
            "cat",
            "raccoon",
            "thief"
        };

        private static readonly string[] BuildingStems =
        {
            "building_bookstore",
            "building_house_1f",
            "building_house_1f_with_interior",
            "building_police_station",
            "building_supermarket"
        };

        [MenuItem("Paws & Loot/Technical Validation/Validate Authored Production Assets")]
        public static void ValidateRequiredAuthoredAssets()
        {
            ValidateAllOrThrow();
            Debug.Log("Authored production asset validation passed.");
        }

        public static void ValidateAllOrThrow()
        {
            foreach (string stem in CharacterStems)
            {
                ValidateCharacter(stem);
            }

            foreach (string stem in BuildingStems)
            {
                ValidateBuilding(stem);
            }

            ValidateController();
            ValidateGameSceneFile();
        }

        public static void RequireCharacterSourceReady(string stem)
        {
            if (!CharacterStems.Contains(stem, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unknown production character stem '{stem}'.");
            }

            ValidateCharacter(stem);
        }

        public static void RequireBuildingSourceReady(string stem)
        {
            if (!BuildingStems.Contains(stem, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unknown production building stem '{stem}'.");
            }

            ValidateBuilding(stem);
        }

        public static void ValidateGameSceneFile()
        {
            if (!File.Exists(GameScenePath))
            {
                return;
            }

            string sceneText = File.ReadAllText(GameScenePath);
            if (sceneText.IndexOf(
                    GeneratedValidationRoot,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    $"Production scene references forbidden validation assets: {GameScenePath}");
            }

            foreach (string stem in CharacterStems)
            {
                RequireSceneGuid(sceneText, $"{CharacterRoot}/{stem}.fbx");
            }

            foreach (string stem in BuildingStems)
            {
                RequireSceneGuid(sceneText, $"{BuildingRoot}/{stem}.fbx");
            }
        }

        private static void ValidateCharacter(string stem)
        {
            string path = $"{CharacterRoot}/{stem}.fbx";
            GameObject model = LoadReadyModel(path);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            ModelImporterAnimationType expectedRig = stem == "police"
                || stem == "thief"
                ? ModelImporterAnimationType.Human
                : ModelImporterAnimationType.Generic;
            if (importer.animationType != expectedRig)
            {
                throw new InvalidOperationException(
                    $"Authored character '{path}' has importer rig '{importer.animationType}', "
                    + $"expected '{expectedRig}'.");
            }
            RequireRendererMaterials(model, path);

            Animator animator = model.GetComponent<Animator>()
                ?? model.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                throw new InvalidOperationException(
                    $"Authored character '{path}' has no Animator component.");
            }

            if ((stem == "police" || stem == "thief")
                && (animator.avatar == null || !animator.avatar.isHuman))
            {
                throw new InvalidOperationException(
                    $"Authored human '{path}' must import as a Humanoid Avatar.");
            }

            // Generic animal rigs may legitimately expose no Humanoid Avatar;
            // their locomotion is driven by the companion animation system.
            if (stem != "police" && stem != "thief"
                && animator.avatar != null
                && animator.avatar.isHuman)
            {
                throw new InvalidOperationException(
                    $"Authored animal '{path}' must not use a Humanoid Avatar.");
            }
        }

        private static void ValidateBuilding(string stem)
        {
            string path = $"{BuildingRoot}/{stem}.fbx";
            GameObject model = LoadReadyModel(path);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                throw new InvalidOperationException(
                    $"Authored building '{path}' must use the Generic importer rig.");
            }
            RequireRendererMaterials(model, path);

            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Authored building '{path}' has no renderers.");
            }

            if (stem.Contains("with_interior", StringComparison.Ordinal))
            {
                RequireRendererNamed(renderers, "BD_House1F_Foundation", path);
                RequireRendererNamed(renderers, "IN_Bedroom_Bed_Frame", path);
            }
        }

        private static GameObject LoadReadyModel(string assetPath)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath) || IsGitLfsPointer(absolutePath))
            {
                throw new InvalidOperationException(
                    $"{RequiredLfsAssetsError} Missing or unresolved asset: {assetPath}");
            }

            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            {
                throw new InvalidOperationException(
                    $"Required authored asset is not imported as a model: {assetPath}");
            }

            if (!importer.importAnimation)
            {
                throw new InvalidOperationException(
                    $"Required authored model has animation import disabled: {assetPath}");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                throw new InvalidOperationException(
                    $"Required authored model could not be loaded: {assetPath}");
            }

            return model;
        }

        private static void RequireRendererMaterials(
            GameObject model,
            string assetPath)
        {
            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Authored model has no renderers: {assetPath}");
            }

            foreach (Renderer renderer in renderers)
            {
                if (renderer.sharedMaterials.Length == 0
                    || renderer.sharedMaterials.Any(material => material == null))
                {
                    throw new InvalidOperationException(
                        $"Authored renderer '{renderer.name}' has an empty material reference: {assetPath}");
                }
            }
        }

        private static void RequireRendererNamed(
            IEnumerable<Renderer> renderers,
            string rendererName,
            string assetPath)
        {
            if (!renderers.Any(renderer => renderer.name == rendererName))
            {
                throw new InvalidOperationException(
                    $"Authored building '{assetPath}' is missing renderer '{rendererName}'.");
            }
        }

        private static void ValidateController()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"Character locomotion controller is missing: {ControllerPath}");
            }

            RequireParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            RequireParameter(controller, "Command", AnimatorControllerParameterType.Trigger);
            RequireParameter(controller, "Win", AnimatorControllerParameterType.Bool);
            RequireParameter(controller, "Lose", AnimatorControllerParameterType.Bool);
        }

        private static void RequireParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == name);
            if (parameter == null || parameter.type != type)
            {
                throw new InvalidOperationException(
                    $"Character locomotion controller parameter '{name}' is missing or has the wrong type.");
            }
        }

        private static void RequireSceneGuid(string sceneText, string assetPath)
        {
            string metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
            {
                throw new InvalidOperationException(
                    $"Authored asset meta file is missing: {metaPath}");
            }

            string guid = File.ReadLines(metaPath)
                .FirstOrDefault(line => line.StartsWith("guid:", StringComparison.Ordinal))
                ?.Substring("guid:".Length)
                .Trim();
            if (string.IsNullOrWhiteSpace(guid)
                || sceneText.IndexOf(guid, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    $"Production Game.unity does not reference authored asset '{assetPath}'.");
            }
        }

        private static bool IsGitLfsPointer(string absolutePath)
        {
            using StreamReader reader = File.OpenText(absolutePath);
            string firstLine = reader.ReadLine();
            return firstLine != null
                && firstLine.StartsWith(
                    "version https://git-lfs.github.com/spec/v1",
                    StringComparison.Ordinal);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
