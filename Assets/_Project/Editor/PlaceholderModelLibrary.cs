using System.Collections.Generic;
using PawsAndLoot.Animation;
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

        public const string CharacterDirectory =
            "Assets/_Project/Art/Characters";

        public const string BuildingDirectory =
            "Assets/_Project/Art/Buildings";

        public static string GetPropPath(string stem)
        {
            return $"{PropDirectory}/{stem}.fbx";
        }

        /// <summary>
        /// Instantiates an authored character mesh and normalises it to
        /// <paramref name="targetHeight"/> with its feet on the ground plane
        /// of <paramref name="parent"/>.
        ///
        /// The authored meshes arrive normalised to a roughly one-unit box, so
        /// their relative sizes are meaningless and every character needs an
        /// explicit target height. Their own textured material is kept.
        /// </summary>
        public static GameObject TryInstantiateAuthoredCharacter(
            string stem,
            Transform parent,
            float targetHeight,
            float groundOffset = CharacterFootOffset)
        {
            GameObject instance = TryInstantiate(
                $"{CharacterDirectory}/{stem}.fbx",
                parent,
                $"{stem}_Model");
            if (instance == null)
            {
                return null;
            }

            instance.transform.localRotation = Quaternion.identity;
            StripColliders(instance);
            NormaliseToHeight(instance, parent, targetHeight, groundOffset);
            ApplyLocomotionController(instance);
            DisableRootMotion(instance);
            return instance;
        }

        /// <summary>
        /// Assigns the shared locomotion controller when one exists, and always
        /// fits the guard.
        ///
        /// The guard used to be attached *inside* the "a controller was found"
        /// branch. That was fine while a controller was always found — and the
        /// moment the TopDownEngine-derived one was deleted it meant the
        /// characters shipped with an enabled Animator holding zero clips, which
        /// is the exact state the guard exists to prevent: the procedural walk
        /// defers to the Animator, the Animator has nothing to play, and no leg
        /// moves. Two Play Mode tests caught it, which is the only reason this
        /// paragraph is not a bug report.
        ///
        /// Quadrupeds and Generic rigs are left alone because humanoid clips
        /// cannot retarget onto them.
        /// </summary>
        private static void ApplyLocomotionController(GameObject instance)
        {
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null
                || animator.avatar == null
                || !animator.avatar.isHuman)
            {
                return;
            }

            UnityEditor.Animations.AnimatorController controller =
                CharacterAnimationSetup.LoadController();
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Height and foot placement were measured just above in the bind
            // pose, before any controller existed, so an Animator with no
            // usable clips must not be left running: it would retarget the rig
            // to a different rest pose, which is what sank the characters into
            // the ground. There are no clips at all today (`MODEL-002`), so the
            // guard is what is actually running.
            AnimatorClipGuard guard =
                instance.GetComponent<AnimatorClipGuard>()
                ?? instance.AddComponent<AnimatorClipGuard>();
            guard.Configure(animator);
        }

        /// <summary>
        /// Instantiates an authored building and scales it uniformly so its
        /// footprint fits the greybox slot. Returns the resulting world height
        /// so the caller can move the walkable rooftop collider onto the real
        /// roof, or -1 when the model is unavailable.
        /// </summary>
        /// <summary>
        /// Drops a building model onto a spot.
        ///
        /// <paramref name="uniformScale"/> above zero uses that scale directly and
        /// ignores the footprint for sizing. Fitting each model to its lot is what
        /// made one house model appear at half a dozen different sizes across the
        /// town — a narrow lot squeezed it, a wide one stretched it, and the same
        /// asset read as a different building each time.
        ///
        /// Returns the size the model actually ended up, so the caller can give it
        /// a collider that matches. The old version returned only the height and
        /// callers sized the box from the requested footprint, which on the
        /// shorter axis was always bigger than the model — an invisible wall
        /// standing off the side of every building.
        /// </summary>
        public static Vector3 TryInstantiateBuildingSized(
            string stem,
            Transform parent,
            Vector3 groundCenter,
            float footprintX,
            float footprintZ,
            float uniformScale = 0f)
        {
            GameObject instance = TryInstantiate(
                $"{BuildingDirectory}/{stem}.fbx",
                parent,
                $"{stem}_Model");
            if (instance == null)
            {
                return Vector3.zero;
            }

            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            StripColliders(instance);
            if (!TryGetWorldBounds(instance, out Bounds bounds)
                || bounds.size.x <= 0.001f
                || bounds.size.z <= 0.001f)
            {
                return Vector3.zero;
            }

            float scale = uniformScale > 0f
                ? uniformScale
                : Mathf.Min(
                    footprintX / bounds.size.x,
                    footprintZ / bounds.size.z);
            instance.transform.localScale = Vector3.one * scale;

            if (!TryGetWorldBounds(instance, out Bounds scaled))
            {
                return Vector3.zero;
            }

            // Re-centre on the slot and drop the base onto the ground.
            Vector3 delta = groundCenter - new Vector3(
                scaled.center.x,
                scaled.min.y,
                scaled.center.z);
            instance.transform.position += delta;
            return scaled.size;
        }

        /// <summary>
        /// The scale a model needs to fit a lot, without instantiating it into the
        /// scene for keeps.
        ///
        /// Used to work out one canonical house size from one canonical lot and
        /// then apply it everywhere.
        /// </summary>
        public static float MeasureFittingScale(
            string stem,
            float footprintX,
            float footprintZ)
        {
            GameObject probe = TryInstantiate(
                $"{BuildingDirectory}/{stem}.fbx",
                null,
                $"{stem}_ScaleProbe");
            if (probe == null)
            {
                return 0f;
            }

            probe.transform.localScale = Vector3.one;
            float scale = 0f;
            if (TryGetWorldBounds(probe, out Bounds bounds)
                && bounds.size.x > 0.001f
                && bounds.size.z > 0.001f)
            {
                scale = Mathf.Min(
                    footprintX / bounds.size.x,
                    footprintZ / bounds.size.z);
            }

            Object.DestroyImmediate(probe);
            return scale;
        }

        private static void NormaliseToHeight(
            GameObject instance,
            Transform parent,
            float targetHeight,
            float groundOffset)
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            if (!TryGetWorldBounds(instance, out Bounds bounds)
                || bounds.size.y <= 0.001f)
            {
                instance.transform.localPosition =
                    new Vector3(0f, groundOffset, 0f);
                return;
            }

            instance.transform.localScale =
                Vector3.one * (targetHeight / bounds.size.y);
            if (!TryGetWorldBounds(instance, out Bounds scaled))
            {
                return;
            }

            float groundY = parent.position.y + groundOffset;
            instance.transform.localPosition += new Vector3(
                parent.position.x - scaled.center.x,
                groundY - scaled.min.y,
                parent.position.z - scaled.center.z);
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

        // TryInstantiateCharacter lived here: it borrowed TopDownEngine's
        // MMExplodude mesh for both roles and separated them by colour, with
        // its head bone scaled 2.2x to fake a chibi silhouette. It was the
        // middle rung of authored -> borrowed -> capsule, and the authored
        // characters have been in place for weeks, so the borrowed one could
        // only ever have been reached on a machine that had the paid asset.
        // Nobody had one. Removed 2026-08-08 with the rest of the dependency.

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

        internal static GameObject TryInstantiate(
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

        private static void DisableRootMotion(GameObject instance)
        {
            foreach (Animator animator in
                instance.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
            }
        }
    }
}
