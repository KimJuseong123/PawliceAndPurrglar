using System.Collections.Generic;
using PawsAndLoot.Gameplay.Players;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Resolves the temporary art used by the greybox Game scene.
    ///
    /// Props come from the authored FBX in Assets/_Project/Art/Props. The
    /// character meshes are borrowed from the external TopDown Engine demos
    /// because no character model has been authored yet; those demo
    /// materials target the Built-in pipeline, so every character renderer
    /// is re-assigned a project URP material to avoid magenta output.
    ///
    /// Every lookup is optional. A missing asset returns null so the caller
    /// keeps its greybox primitive instead of failing the scene rebuild.
    /// </summary>
    internal static class PlaceholderModelLibrary
    {
        public const string PropDirectory = "Assets/_Project/Art/Props";

        // The external package ships no two-heads-tall character, so the
        // roundest cartoon body it has is reused for both roles and separated
        // by colour. Its head bone is enlarged to approach the intended
        // chibi silhouette.
        private const string CharacterModelPath =
            "Assets/TopDownEngine/Demos/Explodudes/Animations/Characters/"
            + "MM/MMExplodude.fbx";

        private const string CharacterAnimatorPath =
            "Assets/TopDownEngine/Demos/Explodudes/Animations/Characters/"
            + "MM/MMExplodudeAnimatorController.controller";

        private const string HeadBoneName = "MM:Head";

        /// <summary>
        /// The source mesh is a realistic seven-heads build. Scaling the head
        /// bone by this factor brings the silhouette to roughly two and a
        /// quarter heads, which reads as the intended cartoon proportion at
        /// the top-down camera distance.
        /// </summary>
        private const float HeadBoneScale = 2.2f;

        /// <summary>
        /// Standing height the placeholder is normalised to, chosen to sit
        /// inside the 2m CharacterController without dwarfing the props.
        /// </summary>
        private const float TargetCharacterHeight = 1.7f;

        /// <summary>
        /// PlayerRoot sits at the character's centre because its
        /// CharacterController is 2m tall with a zero centre, while the
        /// borrowed meshes are pivoted at the feet.
        /// </summary>
        private const float CharacterFootOffset = -1f;

        private static readonly List<string> MissingAssets = new();

        public static IReadOnlyList<string> MissingAssetPaths =>
            MissingAssets;

        public static void ResetMissingAssetLog()
        {
            MissingAssets.Clear();
        }

        public static string GetPropPath(string stem)
        {
            return $"{PropDirectory}/{stem}.fbx";
        }

        /// <summary>
        /// Instantiates an authored prop FBX under <paramref name="parent"/>.
        /// Model origins sit at the base of the mesh, so a local position of
        /// zero places the prop standing on the parent's pivot.
        /// </summary>
        public static GameObject TryInstantiateProp(
            string stem,
            Transform parent,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            float uniformScale = 1f,
            Material overrideMaterial = null)
        {
            GameObject instance = TryInstantiate(
                GetPropPath(stem),
                parent,
                $"{stem}_Model");
            if (instance == null)
            {
                return null;
            }

            instance.transform.localPosition = localPosition;
            instance.transform.localEulerAngles = localEulerAngles;
            instance.transform.localScale = Vector3.one * uniformScale;
            StripColliders(instance);
            ApplyPropMaterial(instance, overrideMaterial);
            return instance;
        }

        /// <summary>
        /// Instantiates the temporary character mesh for a role and forces a
        /// readable single-colour project material on it.
        /// </summary>
        public static GameObject TryInstantiateCharacter(
            PlayerRole role,
            Transform parent,
            Material bodyMaterial,
            Material headMaterial)
        {
            GameObject instance = TryInstantiate(
                CharacterModelPath,
                parent,
                $"{role}PlaceholderModel");
            if (instance == null)
            {
                return null;
            }

            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            StripColliders(instance);
            ApplyRoleMaterials(instance, bodyMaterial, headMaterial);
            EnlargeHeadBone(instance);
            ApplyIdleAnimator(instance);
            NormaliseHeight(instance, parent);
            return instance;
        }

        /// <summary>
        /// Head-to-total ratio of the instance, expressed in heads. Reported
        /// after a rebuild so the placeholder proportion is measurable.
        /// </summary>
        public static bool TryGetHeadRatio(
            GameObject instance,
            out float heads,
            out float totalHeight)
        {
            heads = 0f;
            totalHeight = 0f;
            if (!TryGetWorldBounds(instance, out Bounds total))
            {
                return false;
            }

            totalHeight = total.size.y;
            float headHeight = 0f;
            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (IsHeadMaterial(material))
                    {
                        headHeight = Mathf.Max(
                            headHeight,
                            renderer.bounds.size.y);
                    }
                }
            }

            if (headHeight <= 0.001f)
            {
                return false;
            }

            heads = totalHeight / headHeight;
            return true;
        }

        private static void EnlargeHeadBone(GameObject instance)
        {
            foreach (Transform child in
                instance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != HeadBoneName)
                {
                    continue;
                }

                child.localScale *= HeadBoneScale;
                Debug.Log(
                    $"[Placeholder] head bone '{HeadBoneName}' scaled to "
                    + $"{child.localScale.x:0.00}. Skinned mesh bounds are "
                    + "not re-baked in the editor, so the reported head "
                    + "ratio still reflects the source proportions.");
                return;
            }

            Debug.LogWarning(
                $"[Placeholder] head bone '{HeadBoneName}' not found; "
                + "character keeps its source proportions.");
        }

        /// <summary>
        /// Scales the instance so it stands at the target height and rests its
        /// feet on the ground, whatever authored size the source mesh uses.
        /// </summary>
        private static void NormaliseHeight(
            GameObject instance,
            Transform parent)
        {
            instance.transform.localPosition = Vector3.zero;
            if (!TryGetWorldBounds(instance, out Bounds bounds)
                || bounds.size.y <= 0.001f)
            {
                instance.transform.localPosition =
                    new Vector3(0f, CharacterFootOffset, 0f);
                return;
            }

            float scale = TargetCharacterHeight / bounds.size.y;
            instance.transform.localScale = Vector3.one * scale;

            // Re-measure after scaling, then lift the model so its lowest
            // point lands on the ground plane under PlayerRoot.
            if (!TryGetWorldBounds(instance, out Bounds scaled))
            {
                return;
            }

            float groundY = parent.position.y + CharacterFootOffset;
            float delta = groundY - scaled.min.y;
            instance.transform.localPosition += new Vector3(0f, delta, 0f);
        }

        private static void ApplyIdleAnimator(GameObject instance)
        {
            var controller =
                AssetDatabase.LoadAssetAtPath<
                    UnityEditor.Animations.AnimatorController>(
                    CharacterAnimatorPath);
            if (controller == null)
            {
                MissingAssets.Add(CharacterAnimatorPath);
                return;
            }

            Animator animator = instance.GetComponent<Animator>()
                ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private static void ApplyRoleMaterials(
            GameObject instance,
            Material bodyMaterial,
            Material headMaterial)
        {
            if (bodyMaterial == null)
            {
                return;
            }

            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                var replacement = new Material[
                    source.Length == 0 ? 1 : source.Length];
                for (int index = 0; index < replacement.Length; index++)
                {
                    bool isHead = index < source.Length
                        && IsHeadMaterial(source[index]);
                    replacement[index] = isHead && headMaterial != null
                        ? headMaterial
                        : bodyMaterial;
                }

                renderer.sharedMaterials = replacement;
            }
        }

        private static bool IsHeadMaterial(Material material)
        {
            return material != null
                && material.name.IndexOf(
                    "head",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Local-space bounds of every renderer under the instance. Used to
        /// report placeholder scale in the rebuild log.
        /// </summary>
        public static bool TryGetWorldBounds(
            GameObject instance,
            out Bounds bounds)
        {
            bounds = default;
            bool initialised = false;
            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!initialised)
                {
                    bounds = renderer.bounds;
                    initialised = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return initialised;
        }

        private static GameObject TryInstantiate(
            string assetPath,
            Transform parent,
            string instanceName)
        {
            GameObject asset =
                AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                if (!MissingAssets.Contains(assetPath))
                {
                    MissingAssets.Add(assetPath);
                }

                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                asset,
                parent);
            instance.name = instanceName;
            return instance;
        }

        private static void ApplyPropMaterial(
            GameObject instance,
            Material material)
        {
            if (material == null)
            {
                return;
            }

            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials =
                    new Material[renderer.sharedMaterials.Length == 0
                        ? 1
                        : renderer.sharedMaterials.Length];
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void StripColliders(GameObject instance)
        {
            foreach (Collider collider in
                instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
