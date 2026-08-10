using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Lists where the town's landmarks and buildings actually are.
    ///
    /// Coordinates read off the saved scene rather than off the plan view. The
    /// plan says what is where relative to everything else and cannot be
    /// measured with a ruler; picking pickup spots off a picture is how five
    /// rocks ended up inside buildings the first time.
    /// </summary>
    internal static class TownLayoutReport
    {
        [MenuItem("PawliceAndPurrglar/Setup/Report Town Layout")]
        public static void Report()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Game.unity",
                OpenSceneMode.Single);

            foreach (Transform t in Object
                .FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Block ")
                    || t.name.Contains("Pickup")
                    || t.name.StartsWith("Loot ")
                    || t.name == "Prototype Loot"
                    || t.name == "Crown Jewel"
                    || t.name.Contains("Hiding Spot")
                    || t.name.Contains("Sale")
                    || t.name.Contains("Raccoon"))
                .OrderBy(t => t.name))
            {
                Renderer[] parts =
                    t.GetComponentsInChildren<Renderer>(true);
                if (parts.Length > 0 && t.name.StartsWith("Block "))
                {
                    Bounds b = parts[0].bounds;
                    foreach (Renderer part in parts.Skip(1))
                    {
                        b.Encapsulate(part.bounds);
                    }

                    Debug.Log(
                        $"[TOWN] {t.name}: centre "
                        + $"({b.center.x:0.0}, {b.center.z:0.0}) size "
                        + $"({b.size.x:0.0} x {b.size.z:0.0})");
                }
                else
                {
                    Debug.Log(
                        $"[TOWN] {t.name}: at "
                        + $"({t.position.x:0.0}, {t.position.z:0.0})");
                }
            }
        }
    }
}
