using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Throwaway diagnostic: what the house models are made of, so the door and
    /// the interior are built against the real asset rather than a guess.
    /// </summary>
    internal static class TempHouseModelProbe
    {
        [MenuItem("Paws & Loot/Temp/Probe House Model")]
        public static void Probe()
        {
            foreach (string stem in new[]
            {
                "building_house_1f",
                "building_house_1f_with_interior"
            })
            {
                string path =
                    $"Assets/_Project/Art/Buildings/{stem}.fbx";
                var source =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null)
                {
                    Debug.Log($"[HOUSEMODEL] {stem}: 없음");
                    continue;
                }

                GameObject instance = Object.Instantiate(source);
                Transform[] all =
                    instance.GetComponentsInChildren<Transform>(true);
                Renderer[] renderers =
                    instance.GetComponentsInChildren<Renderer>(true);

                Debug.Log(
                    $"[HOUSEMODEL] {stem}: nodes={all.Length} "
                    + $"renderers={renderers.Length}");

                // Everything whose name hints at a door, window or floor.
                string[] interesting = all
                    .Select(t => t.name)
                    .Where(n =>
                    {
                        string l = n.ToLowerInvariant();
                        return l.Contains("door") || l.Contains("floor")
                            || l.Contains("window") || l.Contains("wall")
                            || l.Contains("room") || l.Contains("interior")
                            || l.Contains("entrance") || l.Contains("gate");
                    })
                    .Distinct()
                    .ToArray();
                Debug.Log(
                    $"[HOUSEMODEL] {stem} 관심 노드 "
                    + $"({interesting.Length}): "
                    + string.Join(" | ", interesting.Take(40)));

                // The first two levels of hierarchy, to see how it is split.
                foreach (Transform child in instance.transform)
                {
                    Debug.Log(
                        $"[HOUSEMODEL] {stem} 자식: {child.name} "
                        + $"(그 아래 {child.childCount}개)");
                }

                Object.DestroyImmediate(instance);
            }
        }
    }
}
