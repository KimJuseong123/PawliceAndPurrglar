using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Photographs each ground tile from above, once, and keeps the picture.
    ///
    /// The tiles are sculpted, not modelled: roughly a million triangles each
    /// for a square of tarmac. A hundred of them on a map is a hundred million,
    /// so they cannot simply be placed.
    ///
    /// The previous answer was to cut the model's top face out as a quad and
    /// give it the model's own texture coordinates. That works only while the
    /// top face is one tidy island in the atlas, and the new set is unwrapped
    /// across the whole of it — measured: upward faces span u 0..1 and v 0..1
    /// on every one of them. A quad with a linear fit across that samples the
    /// gaps between islands, which is why the streets came out as dark smears.
    ///
    /// So the picture is taken rather than reconstructed. An orthographic
    /// camera framed exactly to the tile's footprint, straight down, into a
    /// small texture. What comes out is precisely what the player sees from the
    /// only angle this game is played at, and it costs two triangles to draw.
    ///
    /// Runs in graphics mode and dirties QualitySettings and GraphicsSettings
    /// as a side effect; both are restored, and
    /// `git status -- ProjectSettings/` should be clean afterwards.
    /// </summary>
    internal static class RoadTileBaker
    {
        private const string EnvironmentDirectory =
            "Assets/_Project/Art/Environment";
        private const string BakedDirectory =
            "Assets/_Project/Art/Generated/Baked";
        private const string MaterialDirectory =
            "Assets/_Project/Materials/Models";

        /// <summary>
        /// Big enough that a crossing's stripes stay crisp at the distance the
        /// camera sits, small enough that a hundred of them cost nothing.
        /// </summary>
        private const int Size = 512;

        /// <summary>
        /// Which models get baked. Everything laid flat on the ground and
        /// repeated — the things that stand up are placed as models.
        /// </summary>
        private static readonly string[] Stems =
        {
            "env_road_straight2",
            "env_road_cross2",
            "env_road_corner2",
            "env_road_crossing2",
            "env_road_tee2",
            "env_road_curve",
            "env_grass_tile"
        };

        /// <summary>
        /// What tarmac should look like, taken from the pieces that got it
        /// right.
        /// </summary>
        private static readonly Color Tarmac =
            new(0.235f, 0.243f, 0.259f, 1f);

        /// <summary>
        /// Tiles whose source texture paints the road pure black.
        ///
        /// The crossing arrived that way — measured, not guessed: its atlas has
        /// (0, 0, 0) everywhere the other pieces have a dark grey. Baked as-is
        /// it is a black hole in the middle of a grey street.
        ///
        /// Lifted here rather than left alone, because the alternative is a
        /// visibly broken town until the model is exported again. It is a
        /// patch on a picture and it is named so — **the fix belongs in the
        /// source texture**, and once that is re-exported this entry should go.
        /// </summary>
        private static readonly HashSet<string> BlackRoads = new()
        {
            "env_road_crossing2"
        };

        [MenuItem("Paws & Loot/Setup/Bake Road Tile Textures")]
        public static void Bake()
        {
            Directory.CreateDirectory(BakedDirectory);
            Directory.CreateDirectory(MaterialDirectory);
            AssetDatabase.Refresh();

            var stage = new GameObject("Bake Stage");
            var camera = new GameObject("Bake Camera").AddComponent<Camera>();
            var sun = new GameObject("Bake Sun").AddComponent<Light>();
            var written = new List<string>();

            try
            {
                camera.transform.SetParent(stage.transform);
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;

                // Opaque, and the same grey the tarmac is. Tiles meet edge to
                // edge, so nothing behind them is ever seen — but a stray
                // background pixel at a seam reads as a crack in the road.
                camera.backgroundColor = new Color(0.18f, 0.19f, 0.21f, 1f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                sun.transform.SetParent(stage.transform);
                sun.type = LightType.Directional;

                // Straight down and flat. A raking light bakes shadows into the
                // picture, and a baked shadow is one that points the wrong way
                // the moment the sun in the scene points anywhere else.
                sun.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                sun.intensity = 1f;
                sun.shadows = LightShadows.None;

                foreach (string stem in Stems)
                {
                    if (BakeOne(stem, camera, stage.transform))
                    {
                        written.Add(stem);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }

            AssetDatabase.Refresh();
            foreach (string stem in written)
            {
                MakeMaterial(stem);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[ROAD] Baked {written.Count} tiles to {BakedDirectory}: "
                + string.Join(", ", written));
        }

        private static bool BakeOne(
            string stem,
            Camera camera,
            Transform stage)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{EnvironmentDirectory}/{stem}.fbx");
            if (asset == null)
            {
                Debug.LogWarning($"[ROAD] '{stem}.fbx' not found.");
                return false;
            }

            var subject = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            subject.transform.SetParent(stage);
            subject.transform.position = Vector3.zero;

            var target = new RenderTexture(Size, Size, 24)
            {
                antiAliasing = 8
            };
            RenderTexture previous = RenderTexture.active;

            try
            {
                Bounds bounds = Extent(subject);

                // Framed to the footprint exactly, so the baked square is the
                // tile and nothing else. Anything looser leaves a margin that
                // shows up as a gap between every pair of tiles.
                camera.orthographicSize = bounds.extents.z;
                camera.aspect = bounds.size.x / Mathf.Max(0.0001f, bounds.size.z);
                camera.transform.position = new Vector3(
                    bounds.center.x,
                    bounds.max.y + 10f,
                    bounds.center.z);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 20f + bounds.size.y;

                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;

                var picture = new Texture2D(
                    Size,
                    Size,
                    TextureFormat.RGB24,
                    false);
                picture.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);

                if (BlackRoads.Contains(stem))
                {
                    LiftBlacks(picture);
                }

                picture.Apply();

                File.WriteAllBytes(
                    $"{BakedDirectory}/{stem}.png",
                    picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                return true;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(subject);
            }
        }

        /// <summary>
        /// Replaces near-black pixels with tarmac.
        ///
        /// Only the near-black ones, so the white markings and the tan kerb are
        /// untouched. A blanket brighten would wash out the whole tile; what is
        /// wrong here is one colour, and it is wrong by being absent.
        /// </summary>
        private static void LiftBlacks(Texture2D picture)
        {
            Color[] pixels = picture.GetPixels();
            for (int index = 0; index < pixels.Length; index++)
            {
                Color pixel = pixels[index];
                if (pixel.r < 0.08f && pixel.g < 0.08f && pixel.b < 0.08f)
                {
                    pixels[index] = Tarmac;
                }
            }

            picture.SetPixels(pixels);
        }

        /// <summary>
        /// A material wearing the baked picture, unlit.
        ///
        /// Unlit because the light is already in the picture. Lighting it again
        /// would darken every road in the shade of a building while the shadow
        /// baked into it stays where it was.
        /// </summary>
        private static void MakeMaterial(string stem)
        {
            string picturePath = $"{BakedDirectory}/{stem}.png";
            var importer = AssetImporter.GetAtPath(picturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            var picture = AssetDatabase.LoadAssetAtPath<Texture2D>(picturePath);
            if (picture == null)
            {
                return;
            }

            string path = $"{MaterialDirectory}/{stem}_baked.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture(Shader.PropertyToID("_BaseMap"), picture);
            material.SetColor(Shader.PropertyToID("_BaseColor"), Color.white);
            EditorUtility.SetDirty(material);
        }

        private static Bounds Extent(GameObject subject)
        {
            Renderer[] pieces =
                subject.GetComponentsInChildren<Renderer>(true);
            if (pieces.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = pieces[0].bounds;
            foreach (Renderer piece in pieces)
            {
                bounds.Encapsulate(piece.bounds);
            }

            return bounds;
        }
    }
}
