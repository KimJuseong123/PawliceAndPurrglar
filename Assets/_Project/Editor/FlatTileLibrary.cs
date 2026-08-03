using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// A two-triangle square wearing a tile's baked top-down picture.
    ///
    /// The ground tiles are sculpted rather than modelled — roughly a million
    /// triangles each for a square of tarmac — so a hundred of them cannot be
    /// placed as models.
    ///
    /// This used to cut the model's top face out and hand the quad the model's
    /// own texture coordinates. That works only while the top face is one tidy
    /// island in the atlas, and it stopped working the moment a set arrived
    /// whose upward faces span the whole of it: a linear fit across scattered
    /// islands samples the gaps between them, and the streets came out as dark
    /// smears. Measuring said so plainly — u 0..1 and v 0..1 on every piece.
    ///
    /// So the picture is taken rather than reconstructed. `Bake Road Tile
    /// Textures` photographs each tile straight down at its own footprint and
    /// keeps the image; this puts that image on a plain square. What the player
    /// sees is exactly what the model looks like from the only angle this game
    /// is played at.
    /// </summary>
    internal static class FlatTileLibrary
    {
        private const string GeneratedDirectory =
            "Assets/_Project/Art/Generated";
        private const string MaterialDirectory =
            "Assets/_Project/Materials/Models";

        /// <summary>
        /// The unit square every ground tile is drawn on, and the material
        /// carrying that tile's baked picture.
        ///
        /// One mesh shared by all of them. It is a square with corner-to-corner
        /// texture coordinates and nothing about it varies per tile — only the
        /// material does — so making one per piece would be a hundred assets
        /// saying the same thing.
        /// </summary>
        public static Mesh TileFor(
            string stem,
            string directory,
            out Material paint,
            float inset = 0f)
        {
            _ = directory;
            _ = inset;

            paint = AssetDatabase.LoadAssetAtPath<Material>(
                $"{MaterialDirectory}/{stem}_baked.mat");
            if (paint == null)
            {
                Debug.LogWarning(
                    $"[ART] '{stem}' has no baked tile. Run "
                    + "Bake Road Tile Textures.");
                return null;
            }

            return UnitSquare();
        }

        private static Mesh UnitSquare()
        {
            string path = $"{GeneratedDirectory}/ground_tile.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached != null)
            {
                return cached;
            }

            var quad = new Mesh
            {
                name = "ground_tile",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, -0.5f)
                },

                // The bake is taken looking down with north up and east right,
                // so the picture's v runs with world z and its u with world x.
                // Getting this the other way round turns every tile a quarter
                // turn and nothing else about it looks wrong.
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                },
                normals = new[]
                {
                    Vector3.up, Vector3.up, Vector3.up, Vector3.up
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            quad.RecalculateTangents();
            quad.RecalculateBounds();

            Directory.CreateDirectory(GeneratedDirectory);
            AssetDatabase.CreateAsset(quad, path);
            AssetDatabase.SaveAssets();
            return quad;
        }
    }
}
