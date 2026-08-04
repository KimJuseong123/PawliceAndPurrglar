using System;
using System.Collections.Generic;
using System.Linq;
using PawsAndLoot.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Turns each prop's FBX into a prefab a running match can load.
    ///
    /// Props are created during play, so they cannot come from the asset
    /// database. They come from <c>Resources/Props</c> instead, and these
    /// prefabs are what goes there — one per kind that has art, holding the
    /// model already scaled, with colliders stripped and shadows off.
    ///
    /// A prefab is a reference, not a copy: the FBX stays in
    /// <c>Art/Props</c> and is pulled into the build because something in
    /// Resources points at it. Moving the art itself into Resources would work
    /// too and would put a second copy of every model in the build.
    ///
    /// The scale is **measured, not typed**. These models are generated and
    /// arrive at sizes that have nothing to do with each other — the five
    /// imported for this pass ranged over two orders of magnitude. A multiplier
    /// per model is a number somebody has to keep true; a target width is a
    /// number the player can see.
    /// </summary>
    internal static class ThrowablePropResources
    {
        private const string OutputFolder = "Assets/_Project/Resources/Props";

        [MenuItem("Paws & Loot/Setup/Sync Throwable Props To Resources")]
        public static void Sync()
        {
            EnsureFolder(OutputFolder);

            var made = new List<string>();
            var missing = new List<string>();
            var noArt = new List<string>();

            foreach (ThrowableKind kind in
                Enum.GetValues(typeof(ThrowableKind)).Cast<ThrowableKind>())
            {
                string stem = ThrowableCatalog.GetModelStem(kind);
                if (string.IsNullOrEmpty(stem))
                {
                    noArt.Add(kind.ToString());
                    continue;
                }

                string modelPath =
                    $"{PlaceholderModelLibrary.PropDirectory}/{stem}.fbx";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (source == null)
                {
                    missing.Add($"{kind} wants {modelPath}");
                    continue;
                }

                if (TryBuild(kind, stem, source, out string report))
                {
                    made.Add(report);
                }
                else
                {
                    missing.Add($"{kind} could not be measured");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[PROPS] {made.Count} prop prefabs written to {OutputFolder}:\n  "
                + string.Join("\n  ", made)
                + (noArt.Count > 0
                    ? $"\n  no art, drawn as shapes: {string.Join(", ", noArt)}"
                    : string.Empty));

            if (missing.Count > 0)
            {
                // Loud, because a prop with a stem and no file is the case that
                // silently falls back to a grey shape at runtime and looks like
                // the feature was never wired.
                Debug.LogError(
                    "[PROPS] Missing prop art:\n  "
                    + string.Join("\n  ", missing));
            }
        }

        /// <summary>
        /// Puts the generated prop model under a scene object, or returns null
        /// when that kind has no art.
        ///
        /// Reads the same prefab a running match reads, so a prop on a shelf and
        /// the prop the player throws are the same size. Re-deriving the scale
        /// here is what let the two drift the first time: the editor scaled a
        /// prop by a typed multiplier and the runtime had none, so the shelf and
        /// the street disagreed.
        /// </summary>
        public static GameObject TryPlace(
            ThrowableKind kind,
            Transform parent)
        {
            string stem = ThrowableCatalog.GetModelStem(kind);
            if (string.IsNullOrEmpty(stem))
            {
                return null;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{OutputFolder}/{stem}.prefab");
            if (prefab == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"{stem}_Model";
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        private static bool TryBuild(
            ThrowableKind kind,
            string stem,
            GameObject source,
            out string report)
        {
            report = null;
            var root = new GameObject(stem);
            try
            {
                var model =
                    (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                if (!TryMeasureSize(model, out float size, out Bounds bounds))
                {
                    return false;
                }

                float target = ThrowableCatalog.GetModelSize(kind);
                float scale = target / size;
                model.transform.localScale = Vector3.one * scale;

                // Sat on the ground rather than centred on it. Half of a
                // centred prop is under the road, which on a banana lying flat
                // is most of the banana.
                model.transform.localPosition =
                    new Vector3(
                        -bounds.center.x * scale,
                        -bounds.min.y * scale,
                        -bounds.center.z * scale);

                foreach (Collider collider in
                    root.GetComponentsInChildren<Collider>(true))
                {
                    // The prop's own trigger decides everything. A solid prop in
                    // the road would stop the runner it is meant to catch, and
                    // stop thrown rocks besides.
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                foreach (Renderer renderer in
                    root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                string path = $"{OutputFolder}/{stem}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report =
                    $"{kind}: {stem} measured {size:0.000} m, scaled "
                    + $"x{scale:0.000} to {target:0.00} m";
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The longest of the model's three extents, in its own metres.
        ///
        /// Not the footprint. Scaling by footprint leaves a tall prop as tall as
        /// it likes: the rubber chicken stands upright, and pinning its width
        /// made it over a metre high.
        /// </summary>
        private static bool TryMeasureSize(
            GameObject instance,
            out float size,
            out Bounds bounds)
        {
            size = 0f;
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer part in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = part.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(part.bounds);
                }
            }

            if (!any)
            {
                return false;
            }

            size = Mathf.Max(
                bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z));
            return size > 0.0001f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path[..path.LastIndexOf('/')];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path[(parent.Length + 1)..]);
        }
    }
}
