using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Hands out the authored model for a prop at runtime.
    ///
    /// Props are made during a match — a banana is put down where the thief
    /// chose, a rock flies from wherever it was thrown — so the editor's model
    /// library is no use: it reads the asset database, which does not exist in a
    /// build. The models are reachable instead through prefabs under
    /// <c>Resources/Props</c>, generated from the same FBX files by
    /// <c>Sync Throwable Props To Resources</c>.
    ///
    /// A prefab rather than the FBX itself, because the prefab is where the
    /// measured scale is baked. The models arrive at whatever size they were
    /// generated at and are normalised once, at import, rather than by every
    /// caller guessing a multiplier.
    ///
    /// Returns null for a prop with no art, and callers draw their own shape.
    /// That path is not a failure and is not logged as one — two of the nine
    /// props have never had models.
    /// </summary>
    public static class ThrowableModelLibrary
    {
        public const string ResourceFolder = "Props";

        private static readonly Dictionary<string, GameObject> Loaded = new();

        public static GameObject TryInstantiate(
            ThrowableKind kind,
            Transform parent)
        {
            string stem = ThrowableCatalog.GetModelStem(kind);
            if (string.IsNullOrEmpty(stem))
            {
                return null;
            }

            GameObject prefab = Load(stem);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = $"{stem}_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        /// <summary>
        /// Whether this kind has art. Asked by tests so that "the banana is a
        /// banana" can be pinned without loading a scene.
        /// </summary>
        public static bool HasModel(ThrowableKind kind)
        {
            string stem = ThrowableCatalog.GetModelStem(kind);
            return !string.IsNullOrEmpty(stem) && Load(stem) != null;
        }

        private static GameObject Load(string stem)
        {
            // Cached including the misses, so a prop with no prefab does not
            // hit Resources once per placement for the rest of the match.
            if (Loaded.TryGetValue(stem, out GameObject cached))
            {
                return cached;
            }

            var prefab = Resources.Load<GameObject>($"{ResourceFolder}/{stem}");
            Loaded[stem] = prefab;
            return prefab;
        }
    }
}
