using System.Collections.Generic;
using System.Linq;
using System.Text;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Interiors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Lists what each of a room's four faces is made of, and what belongs to none.
    ///
    /// A part on no face is the failure mode: hiding a side leaves it standing, and
    /// nothing in any log says so. This is how the window frames were found and it is
    /// how the next one will be.
    /// </summary>
    internal static class InteriorFaceReport
    {
        [MenuItem("Pawlice and Purrglar/Setup/Report Interior Faces")]
        public static void Report()
        {
            EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);

            InteriorShellScreen screen = Object
                .FindObjectsByType<InteriorShellScreen>(
                    FindObjectsSortMode.None)
                .OrderBy(s => s.name)
                .FirstOrDefault();
            if (screen == null)
            {
                Debug.LogError("[FACES] No interior screen in the scene.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine($"[FACES] {screen.name}");
            var assigned = new HashSet<Renderer>();
            for (int face = 0; face < screen.FaceCount; face++)
            {
                IReadOnlyList<Renderer> parts = screen.PartsOf(face);
                foreach (Renderer part in parts)
                {
                    assigned.Add(part);
                }

                report.AppendLine(
                    $"  face {face} ({InteriorShellScreen.Outward[face]}): "
                    + $"{parts.Count}");
            }

            Renderer[] left = screen
                .GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && !assigned.Contains(r))
                .ToArray();
            report.AppendLine($"  --- on no face: {left.Length}");
            foreach (Renderer part in left
                .OrderBy(r => r.name)
                .Take(70))
            {
                Vector3 offset =
                    part.bounds.center - screen.transform.position;
                report.AppendLine(
                    $"    {part.name}  off=({offset.x:0.00},{offset.y:0.00},"
                    + $"{offset.z:0.00}) size={part.bounds.size:F2}");
            }

            Debug.Log(report.ToString());
        }
    }
}
