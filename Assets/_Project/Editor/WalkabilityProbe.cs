using System.Collections.Generic;
using System.Linq;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Says why a player cannot get onto something.
    ///
    /// "It looks walkable and I bounce off it" has three different causes that
    /// look identical from the game: the surface is higher than the controller's
    /// step offset, something invisible is standing in front of it, or the
    /// collider is not where the model is. Guessing between them from a
    /// screenshot is how the wrong one gets fixed.
    ///
    /// Measures instead. For a point on the map it reports every collider a
    /// character-sized capsule would meet on the way in, how tall the step onto
    /// each is, and whether the controller could climb it.
    /// </summary>
    public static class WalkabilityProbe
    {
        [MenuItem("PawliceAndPurrglar/Setup/Report Walkability Around Plaza")]
        public static void ReportPlaza()
        {
            Report(new Vector3(0f, 0f, 0f), 14f);
        }

        /// <summary>
        /// Everything solid in the town, with its footprint. Used to work out
        /// which object a screenshot is pointing at — a shape and a position
        /// identify it where a name alone does not.
        /// </summary>
        /// <summary>
        /// The lake garden, which had the same solid-box problem the square did.
        /// </summary>
        [MenuItem("PawliceAndPurrglar/Setup/Report Walkability Around Lake Garden")]
        public static void ReportLakeGarden()
        {
            Report(new Vector3(-20f, 0f, -14f), 12f);
        }

        private static void Report(Vector3 centre, float radius)
        {
            EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);

            // The step the character can actually take, read from the character
            // rather than assumed. The two roles are different models and the
            // number is not written down anywhere else.
            var steps = new List<string>();
            foreach (CharacterController controller in
                Object.FindObjectsByType<CharacterController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                steps.Add(
                    $"{controller.name}: stepOffset={controller.stepOffset:0.00} "
                    + $"radius={controller.radius:0.00} "
                    + $"height={controller.height:0.00} "
                    + $"slopeLimit={controller.slopeLimit:0}");
            }

            var rows = new List<string>();
            foreach (Collider collider in
                Object.FindObjectsByType<Collider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                Bounds b = collider.bounds;
                Vector2 flat = new(b.center.x - centre.x, b.center.z - centre.z);
                if (flat.magnitude > radius)
                {
                    continue;
                }

                rows.Add(
                    $"  top y={b.max.y:0.00} bottom y={b.min.y:0.00} "
                    + $"size=({b.size.x:0.0},{b.size.y:0.0},{b.size.z:0.0}) "
                    + $"at ({b.center.x:0.0},{b.center.z:0.0}) "
                    + $"{collider.GetType().Name} '{Path(collider.transform)}'");
            }

            // The number that actually decides it: how high the surface is that
            // a player walking in from the street would have to step onto.
            // Bounds cannot answer this — a mesh collider's bounds include the
            // fountain sticking up in the middle.
            var surface = new List<string>();
            for (float x = -5f; x <= 5.001f; x += 2.5f)
            {
                for (float z = -5f; z <= 5.001f; z += 2.5f)
                {
                    var at = new Vector3(centre.x + x, 0f, centre.z + z);
                    if (Physics.Raycast(
                            at + Vector3.up * 12f,
                            Vector3.down,
                            out RaycastHit hit,
                            24f,
                            ~0,
                            QueryTriggerInteraction.Ignore))
                    {
                        surface.Add(
                            $"({x:+0.0;-0.0},{z:+0.0;-0.0}) y={hit.point.y:0.00} "
                            + $"{hit.collider.name}");
                    }
                    else
                    {
                        surface.Add($"({x:+0.0;-0.0},{z:+0.0;-0.0}) NOTHING");
                    }
                }
            }

            Debug.Log(
                "[WALK] plaza surface heights (step onto these):\n  "
                + string.Join("\n  ", surface));

            Debug.Log(
                $"[WALK] characters:\n  {string.Join("\n  ", steps)}\n"
                + $"[WALK] {rows.Count} solid collider(s) within {radius}m of "
                + $"{centre}, sorted by top height:\n"
                + string.Join("\n", rows.OrderByDescending(r => r)));
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            while (t != null && parts.Count < 6)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }

            return string.Join("/", parts);
        }
    }
}
