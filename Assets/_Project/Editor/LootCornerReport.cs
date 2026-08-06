using System.Linq;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Loot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>Where every piece of loot actually stands, for a look at the map.</summary>
    internal static class LootCornerReport
    {
        [MenuItem("Paws & Loot/Setup/Report Loot Placement")]
        public static void Report()
        {
            EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            foreach (LootItem item in Object
                .FindObjectsByType<LootItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderBy(i => i.transform.position.z)
                .ThenBy(i => i.transform.position.x))
            {
                Vector3 at = item.transform.position;
                bool inTown = Mathf.Abs(at.x) < 40f && Mathf.Abs(at.z) < 40f;
                Debug.Log(
                    $"[LOOT-WHERE] {(inTown ? "TOWN" : "away")} "
                    + $"{at.x:0.0} {at.y:0.0} {at.z:0.0}  {item.name}");
            }
        }
    }
}
