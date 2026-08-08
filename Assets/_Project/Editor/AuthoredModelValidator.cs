using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// ART-002. Checks imported models against the MODEL-001 spec.
    ///
    /// Reports rather than throws, because a model that breaks a guideline
    /// should still be playable while the artist iterates. Only hard breakages
    /// that would produce an invisible or unusable asset are errors.
    ///
    /// The rules it enforces are written down in docs/16_MODEL_SPEC.md.
    /// </summary>
    public static class AuthoredModelValidator
    {
        private sealed class Spec
        {
            public string Path;
            public float TargetHeight;
            public bool ExpectHumanoid;
            public int TriangleBudget;
            public int MaterialBudget;
        }

        private const float HeightTolerance = 0.45f;
        private const float OriginTolerance = 0.06f;

        private static readonly Spec[] CharacterSpecs =
        {
            new()
            {
                Path = "Assets/_Project/Art/Characters/police.fbx",
                TargetHeight = 1.70f,
                ExpectHumanoid = true,
                TriangleBudget = 6000,
                MaterialBudget = 1
            },
            new()
            {
                Path = "Assets/_Project/Art/Characters/thief.fbx",
                TargetHeight = 1.70f,
                ExpectHumanoid = true,
                TriangleBudget = 6000,
                MaterialBudget = 1
            },
            new()
            {
                Path = "Assets/_Project/Art/Characters/dog.fbx",
                TargetHeight = 0.75f,
                ExpectHumanoid = false,
                TriangleBudget = 4000,
                MaterialBudget = 1
            },
            new()
            {
                Path = "Assets/_Project/Art/Characters/cat.fbx",
                TargetHeight = 0.45f,
                ExpectHumanoid = false,
                TriangleBudget = 4000,
                MaterialBudget = 1
            },
            new()
            {
                Path = "Assets/_Project/Art/Characters/raccoon.fbx",
                TargetHeight = 0.70f,
                ExpectHumanoid = false,
                TriangleBudget = 4000,
                MaterialBudget = 1
            }
        };

        private static readonly string[] RequiredClipNames =
        {
            "Idle",
            "Walk",
            "Run",
            "Command"
        };

        [MenuItem("PawliceAndPurrglar/Setup/Validate Authored Models")]
        public static void Validate()
        {
            int problems = 0;
            foreach (Spec spec in CharacterSpecs)
            {
                problems += ValidateCharacter(spec);
            }

            problems += ValidateMaterialLocations();

            Debug.Log(
                problems == 0
                    ? "[ART-002] All authored models match the MODEL-001 spec."
                    : $"[ART-002] {problems} spec deviations reported above. "
                      + "See docs/16_MODEL_SPEC.md.");
        }

        private static int ValidateCharacter(Spec spec)
        {
            var importer = AssetImporter.GetAtPath(spec.Path)
                as ModelImporter;
            GameObject asset =
                AssetDatabase.LoadAssetAtPath<GameObject>(spec.Path);
            if (importer == null || asset == null)
            {
                Debug.LogError($"[ART-002] Missing model: {spec.Path}");
                return 1;
            }

            int problems = 0;
            string name = System.IO.Path.GetFileName(spec.Path);

            // Rig kind. A quadruped imported as Humanoid would stand upright
            // the moment any humanoid clip played on it.
            bool isHumanoid = importer.animationType
                == ModelImporterAnimationType.Human;
            if (isHumanoid != spec.ExpectHumanoid)
            {
                Debug.LogWarning(
                    $"[ART-002] {name}: rig is "
                    + $"{importer.animationType} but the spec expects "
                    + $"{(spec.ExpectHumanoid ? "Human" : "Generic")}.");
                problems++;
            }

            if (!Mathf.Approximately(importer.globalScale, 1f))
            {
                Debug.LogWarning(
                    $"[ART-002] {name}: import scale is "
                    + $"{importer.globalScale:0.###}, expected 1. Author at "
                    + "1 unit = 1m instead of rescaling on import.");
                problems++;
            }

            GameObject instance = Object.Instantiate(asset);
            try
            {
                problems += ValidateGeometry(name, instance, spec);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            problems += ValidateClips(name, spec.Path);
            return problems;
        }

        private static int ValidateGeometry(
            string name,
            GameObject instance,
            Spec spec)
        {
            int problems = 0;
            var renderers = new List<Renderer>(
                instance.GetComponentsInChildren<Renderer>(true));
            if (renderers.Count == 0)
            {
                Debug.LogError(
                    $"[ART-002] {name}: no renderer, so it would be "
                    + "invisible in game.");
                return 1;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Count; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            // Height is a guideline: the scene normalises it anyway, but a big
            // gap means a big rescale and more skinning distortion.
            float ratio = bounds.size.y / spec.TargetHeight;
            if (ratio < 1f - HeightTolerance || ratio > 1f + HeightTolerance)
            {
                Debug.LogWarning(
                    $"[ART-002] {name}: authored height "
                    + $"{bounds.size.y:0.00}m against a target of "
                    + $"{spec.TargetHeight:0.00}m, so the scene rescales it by "
                    + $"{1f / ratio:0.00}x.");
                problems++;
            }

            // Origin at the feet. An off-origin model floats or sinks.
            if (Mathf.Abs(bounds.min.y) > OriginTolerance)
            {
                Debug.LogWarning(
                    $"[ART-002] {name}: base sits at y="
                    + $"{bounds.min.y:0.000}, expected 0. Set the origin to "
                    + "the centre of the feet.");
                problems++;
            }

            var materials = new HashSet<Material>();
            int triangles = 0;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }

                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                {
                    triangles += mesh.triangles.Length / 3;
                }
            }

            if (materials.Count > spec.MaterialBudget)
            {
                Debug.LogWarning(
                    $"[ART-002] {name}: {materials.Count} materials against a "
                    + $"budget of {spec.MaterialBudget}. Each extra material "
                    + "costs a draw call.");
                problems++;
            }

            if (triangles > spec.TriangleBudget)
            {
                Debug.LogWarning(
                    $"[ART-002] {name}: {triangles} triangles against a budget "
                    + $"of {spec.TriangleBudget}.");
                problems++;
            }

            return problems;
        }

        private static int ValidateClips(string name, string path)
        {
            var found = new List<string>();
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is AnimationClip clip
                    && !clip.name.StartsWith("__preview__"))
                {
                    found.Add(clip.name);
                }
            }

            if (found.Count == 0)
            {
                Debug.LogWarning(
                    $"[ART-002] {name}: no animation clips. Movement falls "
                    + "back to procedural animation until MODEL-002 ships "
                    + $"{string.Join(", ", RequiredClipNames)}.");
                return 1;
            }

            int problems = 0;
            foreach (string required in RequiredClipNames)
            {
                if (!found.Contains(required))
                {
                    Debug.LogWarning(
                        $"[ART-002] {name}: missing required clip "
                        + $"'{required}'. Found: {string.Join(", ", found)}.");
                    problems++;
                }
            }

            return problems;
        }

        /// <summary>
        /// Imported materials must end up inside the project art folder, not
        /// beside an external package, or a teammate without that package sees
        /// magenta.
        /// </summary>
        private static int ValidateMaterialLocations()
        {
            int problems = 0;
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:Material",
                         new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                {
                    continue;
                }

                if (!material.shader.name.StartsWith(
                        "Universal Render Pipeline"))
                {
                    Debug.LogWarning(
                        $"[ART-002] {path}: shader '{material.shader.name}' is "
                        + "not a URP shader and will render magenta.");
                    problems++;
                }
            }

            return problems;
        }
    }
}
