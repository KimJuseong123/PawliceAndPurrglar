using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Puts an authored model under something, at a size somebody asked for.
    ///
    /// One copy of the fitting arithmetic, used by the prop prefabs and by the
    /// loot in the map. Two copies is how the animals ended up following their
    /// owners differently from the players: the same sum, written twice, fixed
    /// once.
    ///
    /// **Measured, not typed.** These models are generated and arrive at sizes
    /// with no relation to each other — the props imported this week ranged from
    /// four millimetres to a centimetre, and the town is in metres. A multiplier
    /// per model is a number somebody has to keep true as models are replaced; a
    /// target size is a number the player can see.
    ///
    /// **Sat on the ground, not centred on it.** Half of a centred model is
    /// under the road, which on anything flat is most of the model.
    /// </summary>
    internal static class AuthoredModelPlacer
    {
        public static GameObject TryPlace(
            string stem,
            float targetSize,
            Transform parent,
            out string report)
        {
            report = null;
            if (string.IsNullOrEmpty(stem))
            {
                return null;
            }

            string path = $"{PlaceholderModelLibrary.PropDirectory}/{stem}.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = $"{stem}_Model";
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (!TryMeasure(instance, out float size, out Bounds bounds))
            {
                Object.DestroyImmediate(instance);
                return null;
            }

            float scale = targetSize / size;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = new Vector3(
                -bounds.center.x * scale,
                -bounds.min.y * scale,
                -bounds.center.z * scale);

            foreach (Collider collider in
                instance.GetComponentsInChildren<Collider>(true))
            {
                // The interaction trigger on the parent decides everything. A
                // solid piece of loot in a shop would block the aisle it is
                // standing in, and stop thrown props besides.
                Object.DestroyImmediate(collider);
            }

            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            report =
                $"{stem} measured {size:0.000} m, scaled x{scale:0.000} to "
                + $"{targetSize:0.00} m";
            return instance;
        }

        /// <summary>
        /// The longest of the model's three extents, in its own metres.
        ///
        /// Not the footprint. Scaling by footprint leaves a tall model as tall as
        /// it likes — the rubber chicken stands upright, and pinning its width
        /// made it over a metre high.
        /// </summary>
        public static bool TryMeasure(
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
    }
}
