using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Reports where a room's parts actually ended up, in the saved scene.
    ///
    /// Two symptoms — nothing is solid, and the player stands with their legs
    /// through the floor — have the same shape of cause: something is not where
    /// the thing it belongs to is. Which something, and by how much, is not
    /// answerable by reading the code that placed them, because the code
    /// reports success either way.
    ///
    /// So it is measured off the scene: the visible room, the collision copy,
    /// the floor plate and the point a player is put down on, all in the same
    /// coordinates, side by side.
    /// </summary>
    internal static class InteriorGeometryProbe
    {
        [MenuItem("Paws & Loot/Setup/Probe Interior Geometry")]
        public static void Report()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Game.unity",
                OpenSceneMode.Single);

            Transform interiors = scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(part => part.name == "House Interiors");
            if (interiors == null)
            {
                Debug.LogError("[PROBE] No House Interiors in the scene.");
                return;
            }

            int reported = 0;
            foreach (Transform room in interiors)
            {
                if (reported >= 3)
                {
                    break;
                }

                reported++;
                Debug.Log($"[PROBE] === {room.name} ===");

                // What is drawn, ignoring the collision copy, which has had its
                // renderers stripped and would not be in here anyway.
                Report("visible", Measure(room, drawn: true));
                Report("collision", MeasureColliders(room));

                foreach (Transform part in
                    room.GetComponentsInChildren<Transform>(true))
                {
                    if (part.name.Contains("Entry")
                        || part.name.Contains("Floor")
                        || part.name.Contains("Plate"))
                    {
                        Debug.Log(
                            $"[PROBE]   {part.name}: y {part.position.y:0.00} "
                            + $"at ({part.position.x:0.0}, "
                            + $"{part.position.z:0.0})");
                    }
                }
            }
        }

        private static void Report(string label, Bounds? bounds)
        {
            if (bounds == null)
            {
                Debug.Log($"[PROBE]   {label}: none");
                return;
            }

            Bounds value = bounds.Value;
            Debug.Log(
                $"[PROBE]   {label,-9}: centre ({value.center.x:0.0}, "
                + $"{value.center.y:0.0}, {value.center.z:0.0})  size "
                + $"({value.size.x:0.0}, {value.size.y:0.0}, "
                + $"{value.size.z:0.0})  top of floor y "
                + $"{value.min.y:0.00}..{value.max.y:0.00}");
        }

        private static Bounds? Measure(Transform room, bool drawn)
        {
            Bounds? total = null;
            foreach (Renderer part in
                room.GetComponentsInChildren<Renderer>(true))
            {
                total = total == null
                    ? part.bounds
                    : Grow(total.Value, part.bounds);
            }

            return total;
        }

        private static Bounds? MeasureColliders(Transform room)
        {
            Bounds? total = null;
            foreach (Collider part in
                room.GetComponentsInChildren<Collider>(true))
            {
                if (part.isTrigger)
                {
                    continue;
                }

                total = total == null
                    ? part.bounds
                    : Grow(total.Value, part.bounds);
            }

            return total;
        }

        private static Bounds Grow(Bounds bounds, Bounds other)
        {
            bounds.Encapsulate(other);
            return bounds;
        }
    }
}
