using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Makes the scenery cost less to ship without changing what it looks like.
    ///
    /// The Windows build's asset list came to 140 MB, and three models were
    /// fifty-six per cent of it: the jeweller's, the fountain and the
    /// two-storey house are roughly a million triangles each, straight out of
    /// the generator. On WebGL that is a download nobody waits for.
    ///
    /// Two settings do most of the work and neither touches a single vertex
    /// position on screen:
    ///
    /// - **Mesh compression** quantises the numbers a mesh is stored with. The
    ///   triangle count does not change, so nothing about how it draws changes;
    ///   only how many bytes describe it.
    /// - **A texture size cap.** The atlases are 2048 across and every one of
    ///   them is seen from a top-down camera at ten metres. Half that is more
    ///   than the screen can show.
    ///
    /// Both are importer settings, so both are undone by changing a number
    /// here and reimporting. Nothing is baked and nothing is destroyed.
    ///
    /// What this does not do is reduce the triangle count. That is the real
    /// fix, it belongs in Blender, and it is worth about seventy megabytes on
    /// its own — this is what can be had without waiting for it.
    /// </summary>
    internal static class BuildSizeTrim
    {
        private static readonly string[] SceneryFolders =
        {
            "Assets/_Project/Art/Environment",
            "Assets/_Project/Art/Buildings",
            "Assets/_Project/Art/Props"
        };

        private const string ArtRoot = "Assets/_Project/Art";

        /// <summary>
        /// The most the eye can use at this camera distance.
        ///
        /// The town is seen from about ten metres up. A building fills perhaps
        /// a fifth of a 1080p screen, so a 2048 atlas is delivering four times
        /// the detail that can land on a pixel.
        /// </summary>
        private const int TextureCap = 1024;

        [MenuItem("Pawlice and Purrglar/Setup/Trim Build Size")]
        public static void Trim()
        {
            int meshes = 0;
            int textures = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string path in AssetDatabase
                    .FindAssets("t:Model", SceneryFolders)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct())
                {
                    if (AssetImporter.GetAtPath(path)
                        is not ModelImporter model)
                    {
                        continue;
                    }

                    if (model.meshCompression == ModelImporterMeshCompression.High)
                    {
                        continue;
                    }

                    model.meshCompression = ModelImporterMeshCompression.High;

                    // Nothing reads these meshes back at runtime, and a
                    // readable mesh keeps a second copy in memory for the
                    // whole session.
                    model.isReadable = false;
                    model.SaveAndReimport();
                    meshes++;
                }

                foreach (string path in AssetDatabase
                    .FindAssets("t:Texture2D", new[] { ArtRoot })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct())
                {
                    if (AssetImporter.GetAtPath(path)
                        is not TextureImporter texture)
                    {
                        continue;
                    }

                    if (texture.maxTextureSize <= TextureCap)
                    {
                        continue;
                    }

                    texture.maxTextureSize = TextureCap;
                    texture.textureCompression =
                        TextureImporterCompression.Compressed;
                    texture.SaveAndReimport();
                    textures++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[SIZE] {meshes} meshes compressed, {textures} textures "
                + $"capped at {TextureCap}.");
        }

        /// <summary>
        /// Reports what has and has not been trimmed, so the state is
        /// checkable rather than assumed.
        /// </summary>
        [MenuItem("Pawlice and Purrglar/Setup/Report Build Size Settings")]
        public static void ReportSettings()
        {
            var loose = new List<string>();

            foreach (string path in AssetDatabase
                .FindAssets("t:Model", SceneryFolders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct())
            {
                if (AssetImporter.GetAtPath(path) is ModelImporter model
                    && model.meshCompression
                        != ModelImporterMeshCompression.High)
                {
                    loose.Add($"mesh {System.IO.Path.GetFileName(path)}");
                }
            }

            foreach (string path in AssetDatabase
                .FindAssets("t:Texture2D", new[] { ArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct())
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter texture
                    && texture.maxTextureSize > TextureCap)
                {
                    loose.Add(
                        $"texture {System.IO.Path.GetFileName(path)} "
                        + $"({texture.maxTextureSize})");
                }
            }

            if (loose.Count == 0)
            {
                Debug.Log("[SIZE] Everything under Art is trimmed.");
                return;
            }

            Debug.Log(
                $"[SIZE] {loose.Count} assets still untrimmed:\n  "
                + string.Join("\n  ", loose.Take(20)));
        }
    }
}
