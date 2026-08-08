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

        [MenuItem("PawliceAndPurrglar/Setup/Bake Road Tile Textures")]
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

                // Written before anything touches it. When a tile comes out
                // wrong the first question is whether the render was wrong or
                // the correction was, and those two have very different fixes.
                //
                // Before *anything*: one correction used to run above this
                // line, so the file called "raw" was already a third of the way
                // corrected, and reading the crossing's asphalt off it gave a
                // number that was twenty points too high. A diagnostic that is
                // slightly not what it says it is is worse than none.
                picture.Apply();
                Directory.CreateDirectory(RawDirectory);
                File.WriteAllBytes(
                    $"{RawDirectory}/{stem}.png",
                    picture.EncodeToPNG());

                if (Roads.Contains(stem))
                {
                    MatchSurfaces(picture);
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
        /// What one tile turned out to be made of.
        ///
        /// Measured from the tile rather than compared against written-down
        /// brightnesses. Absolute thresholds were what broke this before: they
        /// were calibrated against a lit bake, the bake became unlit, every
        /// tile dropped forty points, and the bands stopped landing on the
        /// things they were named after. The kerb test silently matched nothing
        /// at all on any tile, and nobody noticed because "the kerbs disagree"
        /// looks exactly like "the kerbs were never corrected".
        /// </summary>
        private readonly struct Surfaces
        {
            public Surfaces(
                float roadLevel,
                Color road,
                float roadSpread,
                Color kerb,
                bool hasKerb,
                Color paint,
                bool hasPaint)
            {
                RoadLevel = roadLevel;
                Road = road;
                RoadSpread = roadSpread;
                Kerb = kerb;
                HasKerb = hasKerb;
                Paint = paint;
                HasPaint = hasPaint;
            }

            /// <summary>Luminance of this tile's carriageway.</summary>
            public float RoadLevel { get; }

            public Color Road { get; }

            /// <summary>
            /// How mottled the carriageway is. The thing that made the crossing
            /// read as a lighter tile even after its average was matched: its
            /// asphalt varies twelve times as much as a straight's, and a
            /// mottled grey beside a flat grey looks like a different grey.
            /// </summary>
            public float RoadSpread { get; }

            public Color Kerb { get; }
            public bool HasKerb { get; }

            /// <summary>
            /// The lane markings, which disagree as much as anything else:
            /// measured across the set they run from 127 to 181, and the
            /// crossing's stripes are 112 — the same brightness as a kerb,
            /// which is why they read as dirty rather than painted.
            /// </summary>
            public Color Paint { get; }

            public bool HasPaint { get; }
        }

        /// <summary>
        /// Where the carriageway ends, as a distance above its own level.
        ///
        /// Everything within this of the road level is road. Relative, so it
        /// means the same thing on a tile whose asphalt is nearly black as on
        /// one whose asphalt is mid grey.
        /// </summary>
        private const float RoadBand = 0.06f;

        /// <summary>Where the correction has faded to nothing.</summary>
        private const float RoadFade = 0.20f;

        /// <summary>
        /// Above the road and below the paint. Kerbstone lives here, and on
        /// these tiles nothing else does.
        /// </summary>
        private const float KerbCeiling = 0.55f;

        /// <summary>
        /// How mottled the carriageway is allowed to be, in luminance.
        ///
        /// The median of the set, measured: five of the six sit between 0.0011
        /// and 0.0058 and the crossing sits at 0.0464.
        /// </summary>
        private const float TargetSpread = 0.0040f;

        /// <summary>
        /// What a kerb should look like, measured across the set rather than
        /// chosen. Four of the six cluster here; the crossing's is greyer and
        /// the dead end's is half as bright.
        /// </summary>
        private static readonly Color Kerb =
            new(112f / 255f, 96f / 255f, 80f / 255f, 1f);

        /// <summary>
        /// How far above its own road a marking starts.
        ///
        /// Low enough to catch the crossing's stripes, which sit only 0.15
        /// above its asphalt — right where a kerb sits. Brightness alone cannot
        /// separate those two on this tile and never could; what separates them
        /// is that a kerb runs round the outside and a marking is painted
        /// inside.
        /// </summary>
        private const float PaintFloor = 0.12f;

        /// <summary>
        /// What painted lines should look like, measured across the set. The
        /// straight and the corner agree here; the T is fifty points darker and
        /// the crossing's zebra darker still.
        /// </summary>
        private static readonly Color Paint =
            new(178f / 255f, 173f / 255f, 165f / 255f, 1f);

        /// <summary>
        /// Reads a tile's carriageway, kerb and markings off the picture.
        /// </summary>
        private static Surfaces Measure(Color[] pixels)
        {
            // Only well inside the tile, so kerbstone is not mistaken for a
            // dark road on the tiles whose kerb runs wide.
            int margin = Mathf.RoundToInt(Size * 0.26f);
            var inner = new List<Color>();
            for (int y = margin; y < Size - margin; y++)
            {
                for (int x = margin; x < Size - margin; x++)
                {
                    inner.Add(pixels[y * Size + x]);
                }
            }

            inner.Sort((left, right) =>
                Luminance(left).CompareTo(Luminance(right)));

            // The darker half is carriageway on every one of these, including
            // the crossing, where the stripes take up rather less than half.
            int roadCount = Mathf.Max(1, (int)(inner.Count * 0.55f));
            var road = inner.GetRange(0, roadCount);

            float level = Luminance(road[road.Count / 2]);
            var mean = new Color(
                Mean(road, 0),
                Mean(road, 1),
                Mean(road, 2),
                1f);

            double sum = 0d;
            foreach (Color pixel in road)
            {
                float delta = Luminance(pixel) - level;
                sum += delta * delta;
            }

            float spread = Mathf.Sqrt((float)(sum / road.Count));

            var stone = new List<Color>();
            for (int index = 0; index < pixels.Length; index++)
            {
                if (BorderWeight(index) <= 0f)
                {
                    continue;
                }

                float lum = Luminance(pixels[index]);
                if (lum > level + RoadBand && lum < level + KerbCeiling)
                {
                    stone.Add(pixels[index]);
                }
            }

            // A tile with no kerb — the crossroads has none along its edges —
            // has nothing to match, and a median of a handful of anti-aliased
            // pixels would move it somewhere arbitrary.
            bool hasKerb = stone.Count > pixels.Length / 40;
            var kerb = hasKerb
                ? new Color(
                    Median(stone, 0),
                    Median(stone, 1),
                    Median(stone, 2),
                    1f)
                : Color.black;

            var markings = new List<Color>();
            for (int index = 0; index < pixels.Length; index++)
            {
                if (PaintWeight(pixels[index], index, level) > 0.9f)
                {
                    markings.Add(pixels[index]);
                }
            }

            // A tile with no markings inside it — the dead end has only its
            // dashes and the crossroads only its cross — still has enough. Far
            // fewer than that and it is anti-aliasing rather than paint.
            bool hasPaint = markings.Count > pixels.Length / 400;
            var paint = hasPaint
                ? new Color(
                    Median(markings, 0),
                    Median(markings, 1),
                    Median(markings, 2),
                    1f)
                : Color.black;

            return new Surfaces(
                level,
                mean,
                spread,
                kerb,
                hasKerb,
                paint,
                hasPaint);
        }

        /// <summary>
        /// Brings one tile's carriageway and kerb onto the set's.
        ///
        /// Both are done as an affine remap per channel rather than a flat
        /// offset: the offset moves the colour and the scale flattens the
        /// mottling. Matching only the average is what let the crossing pass
        /// every measurement and still read as a lighter square in the street.
        ///
        /// The scale never rises above one. Squeezing a mottled tile onto a
        /// calm one is wanted; stretching a calm tile up to the set's average
        /// mottle would be inventing noise, and on the dead end — whose asphalt
        /// varies by a thousandth — it would multiply what little it has by
        /// four.
        ///
        /// Weights come from the *original* pixel, so correcting the road does
        /// not move the kerb out from under its own test.
        /// </summary>
        private static void MatchSurfaces(Texture2D picture)
        {
            Color[] pixels = picture.GetPixels();
            Surfaces measured = Measure(pixels);

            float scale = Mathf.Clamp01(
                TargetSpread / Mathf.Max(1e-5f, measured.RoadSpread));

            for (int index = 0; index < pixels.Length; index++)
            {
                Color pixel = pixels[index];
                float lum = Luminance(pixel);
                Color moved = pixel;

                float road = Mathf.Clamp01(
                    (measured.RoadLevel + RoadFade - lum)
                    / (RoadFade - RoadBand));
                if (road > 0f)
                {
                    moved = Blend(
                        moved,
                        Remap(pixel, measured.Road, Tarmac, scale),
                        road);
                }

                if (measured.HasKerb)
                {
                    float kerb = KerbWeight(pixel, index, measured.RoadLevel);
                    if (kerb > 0f)
                    {
                        // Offset only. The stone's grain is the one thing on
                        // these tiles worth keeping, and flattening it would
                        // turn every kerb into a painted strip.
                        moved = Blend(
                            moved,
                            Remap(pixel, measured.Kerb, Kerb, 1f),
                            kerb);
                    }
                }

                if (measured.HasPaint)
                {
                    float paint = PaintWeight(pixel, index, measured.RoadLevel);
                    if (paint > 0f)
                    {
                        moved = Blend(
                            moved,
                            Remap(pixel, measured.Paint, Paint, 1f),
                            paint);
                    }
                }

                pixels[index] = new Color(
                    Mathf.Clamp01(moved.r),
                    Mathf.Clamp01(moved.g),
                    Mathf.Clamp01(moved.b),
                    1f);
            }

            picture.SetPixels(pixels);
        }

        private static Color Remap(Color pixel, Color from, Color to, float scale)
        {
            return new Color(
                to.r + (pixel.r - from.r) * scale,
                to.g + (pixel.g - from.g) * scale,
                to.b + (pixel.b - from.b) * scale,
                1f);
        }

        private static Color Blend(Color from, Color to, float amount)
        {
            return new Color(
                Mathf.Lerp(from.r, to.r, amount),
                Mathf.Lerp(from.g, to.g, amount),
                Mathf.Lerp(from.b, to.b, amount),
                1f);
        }

        /// <summary>
        /// How much a pixel counts as kerb: near the edge of the tile, and in
        /// the band above this tile's own road and below its paint.
        ///
        /// Position matters as much as colour. The crossing's stripes are not
        /// white — they sit at the same brightness a kerb does — so a
        /// colour-only test grabbed them and painted the zebra tan. Warmth does
        /// not settle it either: the crossing's kerb is warm and the dead end's
        /// is very nearly grey.
        ///
        /// A kerb runs along the outside of a tile by definition, and every
        /// marking on these pieces is drawn inside. The dead end's kerb curves,
        /// but it curves around the perimeter and stays in the band.
        /// </summary>
        private static float KerbWeight(Color pixel, int index, float roadLevel)
        {
            float border = BorderWeight(index);
            if (border <= 0f)
            {
                return 0f;
            }

            float lum = Luminance(pixel);

            // Ramped at both ends so the anti-aliased pixels where a kerb meets
            // the road are carried across smoothly instead of forming a line of
            // their own.
            float rising = Mathf.Clamp01((lum - roadLevel - RoadBand) / 0.06f);
            float falling = Mathf.Clamp01(
                (roadLevel + KerbCeiling - lum) / 0.10f);
            return Mathf.Min(Mathf.Min(rising, falling), border);
        }

        /// <summary>
        /// How much a pixel counts as a painted marking: inside the tile, and
        /// brighter than this tile's own asphalt.
        ///
        /// The mirror image of the kerb test, and deliberately so. The two
        /// overlap completely in brightness — the crossing's zebra is exactly
        /// as bright as its kerb — and not at all in position. Gating each on
        /// the side of the tile it belongs to keeps them apart without either
        /// needing to know about the other.
        /// </summary>
        private static float PaintWeight(Color pixel, int index, float roadLevel)
        {
            float inside = 1f - BorderWeight(index);
            if (inside <= 0f)
            {
                return 0f;
            }

            float lum = Luminance(pixel);
            return Mathf.Min(
                inside,
                Mathf.Clamp01((lum - roadLevel - PaintFloor) / 0.05f));
        }

        private static float BorderWeight(int index)
        {
            int x = index % Size;
            int y = index / Size;
            int fromEdge = Mathf.Min(
                Mathf.Min(x, Size - 1 - x),
                Mathf.Min(y, Size - 1 - y));
            return Mathf.Clamp01(
                (Size * 0.26f - fromEdge) / (Size * 0.06f));
        }

        private static float Luminance(Color pixel)
        {
            return 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
        }

        private static float Mean(List<Color> samples, int channel)
        {
            double total = 0d;
            foreach (Color sample in samples)
            {
                total += channel switch
                {
                    0 => sample.r,
                    1 => sample.g,
                    _ => sample.b
                };
            }

            return (float)(total / samples.Count);
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
