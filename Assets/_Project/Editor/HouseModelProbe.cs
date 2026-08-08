using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Prints what the house models are actually made of, in their own space.
    ///
    /// The interiors were built from guessed dimensions once already and it cost a
    /// playtest, so before reshaping them the model gets measured: how big the
    /// shell is, where its walls and floor sit, and which of the front and back
    /// doors exist on which variant.
    /// </summary>
    internal static class HouseModelProbe
    {
        [MenuItem("Pawlice and Purrglar/Setup/Report House Model Layout")]
        public static void Report()
        {
            foreach (string stem in new[]
            {
                "building_house_1f",
                "building_house_1f_with_interior"
            })
            {
                ReportOne(stem);
            }
        }

        private static void ReportOne(string stem)
        {
            string path = $"Assets/_Project/Art/Buildings/{stem}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogError($"[PROBE] {path} not found.");
                return;
            }

            GameObject instance = Object.Instantiate(asset);
            instance.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            Renderer[] renderers =
                instance.GetComponentsInChildren<Renderer>(true);
            var shell = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (first)
                {
                    shell = renderer.bounds;
                    first = false;
                }
                else
                {
                    shell.Encapsulate(renderer.bounds);
                }
            }

            var report = new StringBuilder();
            report.AppendLine($"[PROBE] {stem}: {renderers.Length} renderers");
            report.AppendLine(
                $"  shell size {shell.size:F2} centre {shell.center:F2}");

            // The pieces the inside of the room is actually made of.
            string[] interesting =
            {
                "BD_House1F_Foundation",
                "BD_House1F_Wall_Front",
                "BD_House1F_Wall_Back",
                "BD_House1F_Wall_Left",
                "BD_House1F_Wall_Right",
                "BD_House1F_Door_Front_Leaf",
                "BD_House1F_Door_Back_Leaf",
                "BD_House1F_Porch_Floor",
                "BD_House1F_BackStep_Upper"
            };
            foreach (string name in interesting)
            {
                Renderer found = renderers.FirstOrDefault(
                    r => r.name == name);
                report.AppendLine(found == null
                    ? $"  {name}: MISSING"
                    : $"  {name}: centre {found.bounds.center:F2} "
                        + $"size {found.bounds.size:F2}");
            }

            // Anything that is neither shell nor trim, i.e. would count as
            // furniture. Reported as a count so "the model has an interior" can be
            // confirmed or denied rather than assumed from its file name.
            var shellWords = new[]
            {
                "Wall", "Siding", "Roof", "Gable", "Trim", "Porch", "Window",
                "Door", "Shutter", "Foundation", "Chimney", "BackStep",
                "Corner", "Base", "Vent", "Rail", "Baluster", "Column"
            };
            List<string> other = renderers
                .Select(r => r.name)
                .Where(n => !shellWords.Any(w => n.Contains(w)))
                .Distinct()
                .ToList();
            report.AppendLine(
                $"  non-shell parts: {other.Count}"
                + (other.Count == 0
                    ? " — the model has no furniture inside it"
                    : ": " + string.Join(", ", other.Take(12))));

            // Every interior part by size, so the rule for "worth bumping into"
            // can be a measurement instead of a keyword list. The first version
            // matched names and produced 87 colliders per room — one per chair
            // slat and drawer front.
            Renderer[] interiorParts = renderers
                .Where(r => r.name.StartsWith("IN_"))
                .ToArray();
            if (interiorParts.Length > 0)
            {
                report.AppendLine(
                    $"  --- IN_ parts by size: {interiorParts.Length}");
                foreach (Renderer part in interiorParts
                    .OrderByDescending(r =>
                        Mathf.Min(r.bounds.size.x, r.bounds.size.z))
                    .Take(60))
                {
                    Vector3 s2 = part.bounds.size;
                    report.AppendLine(
                        $"    {s2.x:0.00} x {s2.y:0.00} x {s2.z:0.00}  "
                        + part.name);
                }

                int blocking = interiorParts.Count(r =>
                    r.bounds.size.y >= 0.45f
                    && Mathf.Min(r.bounds.size.x, r.bounds.size.z) >= 0.35f);
                report.AppendLine(
                    $"  parts at least 0.45 tall and 0.35 wide: {blocking}");
            }

            // The partition walls and their openings, individually. A box collider
            // per wall would seal every room off if the walls are single meshes
            // with holes in them, and be exactly right if they are already split
            // into segments either side of each doorway. Which it is decides how
            // the room can be made solid at all.
            foreach (string prefix in new[]
            {
                "IN_House1F_Wall",
                "IN_House1F_Door",
                "IN_House1F_Floor"
            })
            {
                Renderer[] group = renderers
                    .Where(r => r.name.StartsWith(prefix))
                    .OrderBy(r => r.name)
                    .ToArray();
                report.AppendLine($"  --- {prefix}: {group.Length}");
                foreach (Renderer part in group)
                {
                    report.AppendLine(
                        $"    {part.name}: c {part.bounds.center:F2} "
                        + $"s {part.bounds.size:F2}");
                }
            }

            Debug.Log(report.ToString());
            Object.DestroyImmediate(instance);
        }
    }
}
