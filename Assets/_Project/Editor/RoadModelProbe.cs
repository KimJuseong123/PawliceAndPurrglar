using PawsAndLoot.Gameplay.Map;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Reports the road model's authored size and what the town asks of it.
    ///
    /// The generator scales the model to whatever width a road spec names, so
    /// the question "does the model have to be remade for a wider street" has a
    /// measurable answer rather than a guessed one: it depends on how far the
    /// texture is being stretched, not on whether the mesh fits.
    /// </summary>
    internal static class RoadModelProbe
    {
        private const string RoadModelPath =
            "Assets/_Project/Art/Environment/env_road_section.fbx";

        [MenuItem("PawliceAndPurrglar/Setup/Report Road Model")]
        public static void Report()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                RoadModelPath);
            if (source == null)
            {
                Debug.LogError($"[ROAD] No model at {RoadModelPath}");
                return;
            }

            GameObject instance = Object.Instantiate(source);
            try
            {
                if (!PlaceholderModelLibrary.TryGetWorldBounds(
                        instance,
                        out Bounds bounds))
                {
                    Debug.LogError("[ROAD] The model has no renderers.");
                    return;
                }

                Vector3 size = bounds.size;
                Debug.Log(
                    $"[ROAD] Authored size: {size.x:0.###} x {size.y:0.###} "
                    + $"x {size.z:0.###} m (width x height x length).");

                // What the town currently asks for, and the stretch each one
                // implies along the short axis.
                foreach ((string name, float width) in
                    new[]
                    {
                        ("alley 3m", 3f),
                        ("street 4m", 4f),
                        ("reference-image street 6m", 6f)
                    })
                {
                    float factor = width / size.x;
                    Debug.Log(
                        $"[ROAD] {name}: scale x{factor:0.00} across. "
                        + (factor > 1.6f || factor < 0.6f
                            ? "The surface is stretched enough to see."
                            : "Within what a tiled surface hides."));
                }

                Debug.Log(
                    "[ROAD] Minimum clear width a route needs: "
                    + $"{GreyboxMapDefinition.RequiredMinimumClearWidth:0.##}m.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
