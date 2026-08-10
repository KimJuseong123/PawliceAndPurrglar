using System.Collections.Generic;
using System.Linq;
using System.Text;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Interiors;
using PawliceAndPurrglar.Gameplay.Loot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Says, for every piece of treasure in every room, whether the player will
    /// actually see the thing it is supposed to be.
    ///
    /// Three ways a piece can be in the scene and wrong, and **none of them logs
    /// anything**:
    ///
    /// 1. The authored model was not found, so the builder quietly fell back to
    ///    a tinted cube. The room still has four pieces, the tally still counts
    ///    four, and the log still says "4 pieces over 7 places".
    /// 2. The model is there and its importer material remap is empty, so it
    ///    renders as a white lump (`ISSUE-058`). The scene saves, the validator
    ///    passes, and `Capture Model Sheet` shows it correctly — the contact
    ///    sheet instantiates the model fresh and the scene holds the saved
    ///    reference, so the same asset looks different in two tools.
    /// 3. There is no renderer under the presentation root at all, so the piece
    ///    is a collider and a prompt standing in an empty room.
    ///
    /// Counting per room rather than in total, because a total hides the case
    /// that matters most: one shop furnished and twelve rooms of cubes.
    /// </summary>
    public static class InteriorLootModelReport
    {
        [MenuItem("PawliceAndPurrglar/Setup/Report Interior Loot Models")]
        public static void Report()
        {
            EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);

            HouseInterior[] rooms = Object
                .FindObjectsByType<HouseInterior>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderBy(room => room.InteriorId)
                .ToArray();

            // Every piece in the scene, not the ones under a room.
            //
            // A room's stock is *positioned* at its shelves and never
            // reparented — `StockRoom` writes `transform.position` and leaves
            // the object where the map staged it. Walking the room's children
            // therefore finds nothing, and the first version of this report
            // cheerfully announced "14 rooms, 0 pieces" for a town holding
            // sixty-two of them.
            LootItem[] pieces = Object
                .FindObjectsByType<LootItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderBy(piece => piece.name)
                .ToArray();

            var report = new StringBuilder();
            int total = 0;
            int placeholders = 0;
            int missingRenderer = 0;
            int suspectMaterial = 0;

            {
                var lines = new List<string>();
                foreach (LootItem piece in pieces)
                {
                    total++;
                    Transform presentation = piece.PresentationRoot != null
                        ? piece.PresentationRoot
                        : piece.transform;

                    Renderer[] renderers = presentation
                        .GetComponentsInChildren<Renderer>(true);

                    if (renderers.Length == 0)
                    {
                        missingRenderer++;
                        lines.Add($"    {piece.name}: NO RENDERER");
                        continue;
                    }

                    bool placeholder = presentation
                        .GetComponentsInChildren<Transform>(true)
                        .Any(child => child.name == "PlaceholderModel");

                    if (placeholder)
                    {
                        placeholders++;
                    }

                    // Unity's built-in primitive material is what a cube gets
                    // when nothing assigned one, and it is also what a model
                    // with an empty remap ends up drawing. Either way the piece
                    // is a white or grey blob.
                    string materials = string.Join(
                        "/",
                        renderers
                            .Select(r => r.sharedMaterial != null
                                ? r.sharedMaterial.name
                                : "<none>")
                            .Distinct());

                    bool suspect = renderers.Any(r =>
                        r.sharedMaterial == null
                        || r.sharedMaterial.name == "Default-Material"
                        || r.sharedMaterial.name.StartsWith("Lit"));

                    if (suspect)
                    {
                        suspectMaterial++;
                    }

                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    lines.Add(
                        $"    {piece.name}: "
                        + $"{(placeholder ? "PLACEHOLDER" : "model")}, "
                        + $"{renderers.Length} renderer(s), "
                        + $"mat {materials}, "
                        + $"size {bounds.size.magnitude:0.00}m"
                        + (suspect ? "  <-- SUSPECT MATERIAL" : string.Empty));
                }

                foreach (string line in lines.OrderBy(line => line))
                {
                    report.AppendLine(line);
                }
            }

            // The rooms separately, because "sixty-two pieces exist" and "every
            // room has some" are different facts and only the second one means
            // a player walking into a house finds anything.
            report.AppendLine("  rooms:");
            foreach (HouseInterior room in rooms)
            {
                LootSpotDraw draw = room.GetComponent<LootSpotDraw>();
                report.AppendLine(
                    $"    interior {room.InteriorId} ({room.name}): "
                    + (draw == null
                        ? "NO DRAW"
                        : $"{draw.PieceCount} piece(s) over "
                          + $"{draw.SpotCount} place(s)"));
            }

            Debug.Log(
                $"[LOOT-MODELS] {rooms.Length} rooms, {total} pieces, "
                + $"{placeholders} placeholder, "
                + $"{missingRenderer} with no renderer, "
                + $"{suspectMaterial} with a default/missing material.\n"
                + report);

            if (placeholders > 0 || missingRenderer > 0 || suspectMaterial > 0)
            {
                Debug.LogWarning(
                    "[LOOT-MODELS] Some pieces will not read as what they are. "
                    + "Run 'Repair Model Textures' for white models, and check "
                    + "the LootDefinition ModelStem for placeholders.");
            }
        }
    }
}
