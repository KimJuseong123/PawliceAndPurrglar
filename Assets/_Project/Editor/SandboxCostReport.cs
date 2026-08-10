using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Counts what the sandbox scene actually asks the graphics card to draw.
    ///
    /// "It lags" is a symptom with three unrelated causes — too many objects,
    /// too many triangles, too many shadow casters — and guessing between them
    /// wastes a rebuild each time. This reports all three, and the worst
    /// offenders by name, so the fix goes where the cost is.
    /// </summary>
    internal static class SandboxCostReport
    {
        private const string ScenePath =
            "Assets/_Project/Sandbox/MapSandbox.unity";

        [MenuItem("PawliceAndPurrglar/Setup/Report Sandbox Cost")]
        public static void Report()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);

            var byModel = new Dictionary<string, (int Count, long Triangles)>();
            long triangles = 0;
            int renderers = 0;
            int casters = 0;
            int dynamic = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter filter in
                    root.GetComponentsInChildren<MeshFilter>(false))
                {
                    Mesh mesh = filter.sharedMesh;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (mesh == null || renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    long count = mesh.triangles.Length / 3;
                    triangles += count;
                    renderers++;
                    if (renderer.shadowCastingMode
                        != UnityEngine.Rendering.ShadowCastingMode.Off)
                    {
                        casters++;
                    }

                    if ((GameObjectUtility.GetStaticEditorFlags(filter.gameObject)
                            & StaticEditorFlags.BatchingStatic) == 0)
                    {
                        dynamic++;
                    }

                    string key = mesh.name;
                    byModel.TryGetValue(key, out var tally);
                    byModel[key] = (tally.Count + 1, tally.Triangles + count);
                }
            }

            string worst = string.Join(
                "\n  ",
                byModel
                    .OrderByDescending(entry => entry.Value.Triangles)
                    .Take(8)
                    .Select(entry =>
                        $"{entry.Key}: {entry.Value.Count} copies, "
                        + $"{entry.Value.Triangles:N0} triangles "
                        + $"({entry.Value.Triangles / entry.Value.Count:N0} each)"));

            Debug.Log(
                $"[COST] {renderers} renderers, {triangles:N0} triangles, "
                + $"{casters} casting shadows, {dynamic} not batched.\n"
                + $"  {worst}");
        }
    }
}
