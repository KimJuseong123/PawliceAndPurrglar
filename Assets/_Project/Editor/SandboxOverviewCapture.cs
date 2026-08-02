using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Photographs the sandbox town from directly above, framed to the map
    /// exactly, and writes down where everything currently stands.
    ///
    /// For deciding where props go. That is a question about space — is this
    /// corner bare, is that junction crowded — and space is the one thing a
    /// list of coordinates cannot answer. Two lamps eight metres apart read as
    /// a pair or as clutter depending entirely on what else is near them.
    ///
    /// Framed to the map rectangle and nothing else, so a pixel is a known
    /// number of metres and a spot picked off the picture can be given straight
    /// back as a coordinate. A shot framed "roughly" would be a picture you
    /// cannot measure.
    ///
    /// Runs in graphics mode and dirties QualitySettings and GraphicsSettings;
    /// both are restored, and `git status -- ProjectSettings/` should be clean
    /// afterwards.
    /// </summary>
    internal static class SandboxOverviewCapture
    {
        private const string ScenePath =
            "Assets/_Project/Sandbox/MapSandbox.unity";
        private const string ImagePath = "Logs/sandbox-overview.png";
        private const string PlacementPath = "Logs/sandbox-placements.txt";

        private const int PixelsPerMetre = 12;

        // The map, from MapSandboxSetup. Repeated rather than referenced
        // because a capture that framed itself from the scene's contents would
        // move whenever a tree was added, and then two pictures could not be
        // laid over each other.
        private const float MinX = -28f;
        private const float MaxX = 52f;
        private const float MinZ = -22f;
        private const float MaxZ = 50f;

        [MenuItem("Paws & Loot/Sandbox/Capture Sandbox Overview")]
        public static void Capture()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);

            int width = Mathf.RoundToInt((MaxX - MinX) * PixelsPerMetre);
            int height = Mathf.RoundToInt((MaxZ - MinZ) * PixelsPerMetre);

            var rig = new GameObject("Overview Rig");
            var camera = new GameObject("Overview Camera")
                .AddComponent<Camera>();
            var target = new RenderTexture(width, height, 24)
            {
                antiAliasing = 4
            };
            RenderTexture previous = RenderTexture.active;

            try
            {
                camera.transform.SetParent(rig.transform);
                camera.orthographic = true;
                camera.orthographicSize = (MaxZ - MinZ) * 0.5f;
                camera.aspect = (MaxX - MinX) / (MaxZ - MinZ);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.transform.position = new Vector3(
                    (MinX + MaxX) * 0.5f,
                    80f,
                    (MinZ + MaxZ) * 0.5f);
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.09f, 0.10f);

                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;

                var picture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGB24,
                    false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();

                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(ImagePath, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(rig);
            }

            WritePlacements(scene);
            Debug.Log(
                $"[SANDBOX] Overview written to {ImagePath} "
                + $"({width}x{height}, {PixelsPerMetre} px per metre, "
                + $"world x {MinX}..{MaxX}, z {MinZ}..{MaxZ}). "
                + $"Placements listed in {PlacementPath}.");
        }

        /// <summary>
        /// Writes down what stands where, so the picture can be argued with.
        ///
        /// Only the things somebody would move: the dressing and the buildings.
        /// Every road tile and every square of grass would bury the list under
        /// four hundred lines about the ground.
        /// </summary>
        private static void WritePlacements(Scene scene)
        {
            var rows = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform part in
                    root.GetComponentsInChildren<Transform>(true))
                {
                    string name = part.name;
                    bool interesting =
                        name.StartsWith("env_street_lamp")
                        || name.StartsWith("env_tree")
                        || name.StartsWith("env_fire_hydrant")
                        || name.StartsWith("Block ")
                        || name.EndsWith("Boundary");
                    if (!interesting)
                    {
                        continue;
                    }

                    Vector3 at = part.position;
                    rows.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0,-34} x {1,7:0.0}   z {2,7:0.0}",
                        name,
                        at.x,
                        at.z));
                }
            }

            rows.Sort();
            var text = new StringBuilder();
            text.AppendLine(
                $"# Sandbox placements. Map is x {MinX}..{MaxX}, "
                + $"z {MinZ}..{MaxZ}. North is +z.");
            text.AppendLine($"# {rows.Count} items.");
            foreach (string row in rows)
            {
                text.AppendLine(row);
            }

            File.WriteAllText(PlacementPath, text.ToString());
        }
    }
}
