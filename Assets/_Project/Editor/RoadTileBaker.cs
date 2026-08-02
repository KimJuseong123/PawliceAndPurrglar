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
        /// Where the uncorrected renders go, for when a tile comes out wrong.
        ///
        /// Outside Assets on purpose. They are evidence, not art: putting them
        /// in the project would import seven textures nothing references on
        /// every bake.
        /// </summary>
        private const string RawDirectory = "Logs/baked-raw";

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
            "env_road_end",
            "env_grass_tile"
        };

        /// <summary>
        /// What tarmac should look like, taken from the pieces that got it
        /// right.
        /// </summary>
        private static readonly Color Tarmac =
            new(0.235f, 0.243f, 0.259f, 1f);

        /// <summary>
        /// Which tiles get their colours matched to each other.
        ///
        /// The roads only. They were sculpted one at a time and disagree about
        /// what tarmac and kerbstone look like, so they have to be brought
        /// together or the street reads as six different streets.
        ///
        /// The grass is one model with no such problem, and running the road
        /// correction over it did real damage: its border pixels fall in the
        /// same brightness band a kerb does, so every lawn came back with a tan
        /// frame around it and the town looked tiled in linoleum. A correction
        /// belongs only where the fault it corrects exists.
        /// </summary>
        private static readonly HashSet<string> Roads = new()
        {
            "env_road_straight2",
            "env_road_cross2",
            "env_road_corner2",
            "env_road_crossing2",
            "env_road_tee2",
            "env_road_end"
        };

        /// <summary>
        /// What a kerb should look like, taken the same way — the median of the
        /// three pieces that agree.
        /// </summary>
        private static readonly Color Kerb =
            new(0.635f, 0.592f, 0.537f, 1f);

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

                // No light at all. Each tile is drawn unlit below, so lighting
                // it would only be a second opinion about its colour.
                //
                // There used to be a directional light pointing straight down,
                // on the argument that a raking light bakes shadows in. It does
                // — but straight down is the worst case of the opposite
                // problem: every up-facing surface sits at exactly full
                // brightness, so the tiniest wobble in a normal falls off a
                // cliff. Three tiles came back as white noise with the kerbs
                // and the lane markings around them perfectly clean, because
                // the kerbs face sideways and the markings are separate
                // geometry. What a ground tile wants baked into it is its
                // colour, and nothing else.
                sun.enabled = false;

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
            var borrowed = new List<Material>();
            MakeUnlit(subject, borrowed);

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

                // Written before the corrections as well as after. When a tile
                // comes out wrong the first question is whether the render was
                // wrong or the correction was, and those two have very
                // different fixes. Guessing cost an afternoon once.
                picture.Apply();
                Directory.CreateDirectory(RawDirectory);
                File.WriteAllBytes(
                    $"{RawDirectory}/{stem}.png",
                    picture.EncodeToPNG());

                if (Roads.Contains(stem))
                {
                    MatchTarmac(picture);
                    MatchKerb(picture);
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
                foreach (Material material in borrowed)
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        /// <summary>
        /// Redresses a tile in flat colour for the photograph.
        ///
        /// The picture wanted here is the tile's albedo — what colour is this
        /// square of ground — and a lit shader answers a different question,
        /// one whose answer depends on how the mesh happens to be tessellated.
        /// Unlit takes the lighting out of it entirely, so a tile decimated to
        /// forty thousand triangles bakes to the same picture as the million
        /// it was exported at.
        ///
        /// The stand-in materials are made here and destroyed with the subject.
        /// Leaving them behind would put an untracked material in the project
        /// for every tile, every bake.
        /// </summary>
        private static void MakeUnlit(GameObject subject, List<Material> made)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogWarning(
                    "[ROAD] No URP unlit shader, so the tiles are being lit "
                    + "after all. Expect them to disagree about brightness.");
                return;
            }

            foreach (Renderer renderer in
                subject.GetComponentsInChildren<Renderer>(true))
            {
                Material[] originals = renderer.sharedMaterials;
                var swapped = new Material[originals.Length];
                for (int index = 0; index < originals.Length; index++)
                {
                    var flat = new Material(unlit);
                    if (originals[index] != null)
                    {
                        flat.mainTexture = originals[index].mainTexture;
                        flat.color = originals[index].HasProperty("_BaseColor")
                            ? originals[index].GetColor("_BaseColor")
                            : Color.white;
                    }

                    made.Add(flat);
                    swapped[index] = flat;
                }

                renderer.sharedMaterials = swapped;
            }
        }

        /// <summary>
        /// Shifts a tile's road surface onto the same grey as every other
        /// tile's.
        ///
        /// The pieces were sculpted and textured one at a time, so each one has
        /// its own idea of what tarmac looks like. Measured across the set: the
        /// T-junction reads (50, 49, 51) and the dead end (79, 84, 100), a fifth
        /// darker and a third lighter than the four that agree — and the dead
        /// end is visibly blue on top of that. Laid side by side they look like
        /// six different roads, which is exactly what a player sees.
        ///
        /// The correction is measured per tile rather than written down: find
        /// what this tile calls tarmac, and move it to what the set calls
        /// tarmac. Nothing is assumed about how far off any piece is.
        ///
        /// Applied as an offset that fades out with brightness. The markings and
        /// the kerbs are not the thing that disagrees, and a flat gain would
        /// drag the white lines grey along with the road. Full strength on the
        /// road, none on anything as bright as a painted line.
        /// </summary>
        private static void MatchTarmac(Texture2D picture)
        {
            Color[] pixels = picture.GetPixels();

            // The median of the dark half, not the mean. A mean is pulled about
            // by however much white paint a tile happens to carry, and the
            // crossing carries a great deal.
            var dark = new List<Color>();
            foreach (Color pixel in pixels)
            {
                if (Luminance(pixel) < 0.43f)
                {
                    dark.Add(pixel);
                }
            }

            if (dark.Count < pixels.Length / 20)
            {
                // Almost no road in this picture. Nothing to match, and
                // guessing from a handful of pixels would move it wrongly.
                return;
            }

            var measured = new Color(
                Median(dark, 0),
                Median(dark, 1),
                Median(dark, 2),
                1f);
            var shift = new Color(
                Tarmac.r - measured.r,
                Tarmac.g - measured.g,
                Tarmac.b - measured.b,
                0f);

            for (int index = 0; index < pixels.Length; index++)
            {
                Color pixel = pixels[index];

                // Anchored to this tile's own tarmac, not to a fixed
                // brightness. A tile whose road is already lighter than most
                // sits high in a fixed ramp and gets only part of the
                // correction it needs — the dead end came out three quarters
                // fixed and still visibly blue. Measured from the tile, the
                // road always gets all of it.
                float weight = Mathf.Clamp01(
                    (Luminance(measured) + 0.32f - Luminance(pixel)) / 0.32f);
                pixels[index] = new Color(
                    Mathf.Clamp01(pixel.r + shift.r * weight),
                    Mathf.Clamp01(pixel.g + shift.g * weight),
                    Mathf.Clamp01(pixel.b + shift.b * weight),
                    1f);
            }

            picture.SetPixels(pixels);
        }

        /// <summary>
        /// Shifts a tile's kerb onto the same stone as every other tile's.
        ///
        /// Same problem as the tarmac and the same answer, one band up. The
        /// pieces disagree about kerbs even more than they do about road:
        /// measured, three of them cluster near (160, 151, 137) while the
        /// crossing is (123, 111, 97) and the dead end (206, 208, 201) — dark
        /// tan against near-white grey, side by side along the same street.
        ///
        /// Kerbs are picked out by colour rather than by position. They do sit
        /// along the edges, but the dead end's curves inward and correcting
        /// only a border band would leave a seam halfway round it. The band
        /// they occupy — bright enough not to be tarmac, dark enough not to be
        /// paint — has nothing else in it.
        /// </summary>
        private static void MatchKerb(Texture2D picture)
        {
            Color[] pixels = picture.GetPixels();

            var stone = new List<Color>();
            for (int index = 0; index < pixels.Length; index++)
            {
                if (KerbWeight(pixels[index], index) > 0.9f)
                {
                    stone.Add(pixels[index]);
                }
            }

            // A tile with no kerb at all — the crossroads has none — has
            // nothing to match, and a median of a handful of anti-aliased edge
            // pixels would move it somewhere arbitrary.
            if (stone.Count < pixels.Length / 25)
            {
                return;
            }

            var measured = new Color(
                Median(stone, 0),
                Median(stone, 1),
                Median(stone, 2),
                1f);
            var shift = new Color(
                Kerb.r - measured.r,
                Kerb.g - measured.g,
                Kerb.b - measured.b,
                0f);

            for (int index = 0; index < pixels.Length; index++)
            {
                Color pixel = pixels[index];
                float weight = KerbWeight(pixel, index);
                if (weight <= 0f)
                {
                    continue;
                }

                pixels[index] = new Color(
                    Mathf.Clamp01(pixel.r + shift.r * weight),
                    Mathf.Clamp01(pixel.g + shift.g * weight),
                    Mathf.Clamp01(pixel.b + shift.b * weight),
                    1f);
            }

            picture.SetPixels(pixels);
        }

        /// <summary>
        /// How much a pixel counts as kerb: the right brightness, and near the
        /// edge of the tile.
        ///
        /// Brightness alone is not enough. The crossing's stripes are not white
        /// — they sit at the same brightness a kerb does — so a colour-only
        /// test grabbed them and painted the zebra tan. Warmth does not settle
        /// it either: the crossing's kerb is warm and the dead end's is very
        /// nearly grey.
        ///
        /// Where they are does settle it. A kerb runs along the outside of a
        /// tile by definition, and every marking on these pieces is drawn
        /// inside. The dead end's kerb curves, but it curves around the
        /// perimeter and stays in the band.
        ///
        /// Ramped on both, so the anti-aliased pixels where a kerb meets the
        /// road are carried across smoothly instead of forming a line of their
        /// own.
        /// </summary>
        private static float KerbWeight(Color pixel, int index)
        {
            float lum = Luminance(pixel);
            if (lum < 0.35f || lum > 0.92f)
            {
                return 0f;
            }

            int x = index % Size;
            int y = index / Size;
            int fromEdge = Mathf.Min(
                Mathf.Min(x, Size - 1 - x),
                Mathf.Min(y, Size - 1 - y));
            float border = Mathf.Clamp01(
                (Size * 0.26f - fromEdge) / (Size * 0.06f));
            if (border <= 0f)
            {
                return 0f;
            }

            float rising = Mathf.Clamp01((lum - 0.35f) / 0.08f);
            float falling = Mathf.Clamp01((0.92f - lum) / 0.10f);
            return Mathf.Min(Mathf.Min(rising, falling), border);
        }

        private static float Luminance(Color pixel)
        {
            return 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
        }

        private static float Median(List<Color> samples, int channel)
        {
            var values = new List<float>(samples.Count);
            foreach (Color sample in samples)
            {
                values.Add(channel switch
                {
                    0 => sample.r,
                    1 => sample.g,
                    _ => sample.b
                });
            }

            values.Sort();
            return values[values.Count / 2];
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

                // Clamped. Ground tiles are placed one per cell, and a
                // wrapping tile bleeds its far edge into its near one at every
                // seam.
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
