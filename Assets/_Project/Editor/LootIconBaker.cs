using System.Collections.Generic;
using System.IO;
using PawsAndLoot.Gameplay.Loot;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Renders every piece of loot's own model into a bag icon.
    ///
    /// The bag drew a letter for most of what it held. Twenty-nine loot kinds
    /// exist and eleven icon PNGs do, so the cell for a wheel of cheese said "치"
    /// — and a cell with a count in the corner and a letter in the middle reads as
    /// a broken cell rather than an unfinished one. Two of the same first letter
    /// read as the same thing.
    ///
    /// Rendered rather than drawn by hand, because the models are already here and
    /// already decimated, and an icon that comes from the model can never show
    /// something the player will not find in the room. The alternative — waiting
    /// for twenty-nine pieces of artwork — leaves the bag unreadable until they
    /// arrive.
    ///
    /// Needs a real graphics device, like the map overview and the road tiles:
    ///
    ///     Unity.exe -batchmode -projectPath . -quit \
    ///       -executeMethod PawsAndLoot.Editor.LootIconBaker.BakeAll
    ///
    /// Note the absent <c>-nographics</c>. With it the camera renders nothing and
    /// writes twenty-nine transparent squares, which is worse than the letters
    /// because it looks finished.
    /// </summary>
    internal static class LootIconBaker
    {
        private const int Size = 192;

        /// <summary>
        /// Where the icons land. Under Resources because the HUD loads them by
        /// path at runtime, and in their own folder so a hand-authored icon in the
        /// parent folder always wins by being looked up first.
        /// </summary>
        private const string OutputDirectory =
            "Assets/_Project/Resources/UI/ItemIcons/Loot";

        [MenuItem("PawliceAndPurrglar/UI/Bake Loot Icons")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(OutputDirectory);

            List<LootDefinition> definitions = new();
            foreach (string guid in AssetDatabase.FindAssets(
                $"t:{nameof(LootDefinition)}"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }

            if (definitions.Count == 0)
            {
                Debug.LogError(
                    "[LOOT-ICON] No LootDefinition assets found. Run 'Create "
                    + "Default Loot Data' first.");
                return;
            }

            var stage = new GameObject("Loot Icon Stage");
            Camera camera = new GameObject("Loot Icon Camera")
                .AddComponent<Camera>();
            Light key = new GameObject("Loot Icon Key").AddComponent<Light>();
            Light fill = new GameObject("Loot Icon Fill").AddComponent<Light>();
            int baked = 0;
            var missing = new List<string>();

            try
            {
                camera.transform.SetParent(stage.transform);
                key.transform.SetParent(stage.transform);
                fill.transform.SetParent(stage.transform);

                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;

                // Fully transparent, so the cell's own frame shows through. A
                // solid background turns every icon into a square tile and the
                // grid stops reading as slots.
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.transform.rotation = Quaternion.Euler(18f, 34f, 0f);

                // Two lights, neither of them straight on. A single frontal light
                // puts every upward face at the top of the brightness curve and
                // flattens the piece into a silhouette — the same mistake that
                // blew out the baked road tiles (ISSUE-059).
                key.type = LightType.Directional;
                key.intensity = 1.15f;
                key.transform.rotation = Quaternion.Euler(38f, 22f, 0f);
                fill.type = LightType.Directional;
                fill.intensity = 0.45f;
                fill.transform.rotation = Quaternion.Euler(12f, -140f, 0f);

                foreach (LootDefinition definition in definitions)
                {
                    if (TryBake(camera, stage.transform, definition))
                    {
                        baked++;
                        continue;
                    }

                    missing.Add(definition.StableId);
                }
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }

            AssetDatabase.Refresh();
            foreach (LootDefinition definition in definitions)
            {
                ImportAsSprite($"{OutputDirectory}/{definition.StableId}.png");
            }

            AssetDatabase.SaveAssets();

            // Said out loud rather than counted as a success. A definition with no
            // model still shows a letter, and the whole point of this pass is that
            // nobody has to guess which ones those are.
            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    $"[LOOT-ICON] {missing.Count} pieces have no model to render "
                    + $"and keep their letter: {string.Join(", ", missing)}");
            }

            Debug.Log($"[LOOT-ICON] {baked} icons baked into {OutputDirectory}.");
        }

        private static bool TryBake(
            Camera camera,
            Transform stage,
            LootDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.ModelStem))
            {
                return false;
            }

            var holder = new GameObject($"Subject {definition.StableId}");
            holder.transform.SetParent(stage, false);

            // Parked far from the origin so nothing else in the loaded scene ends
            // up inside the frame.
            holder.transform.position = new Vector3(0f, 1000f, 0f);

            try
            {
                if (AuthoredModelPlacer.TryPlace(
                        definition.ModelStem,
                        1f,
                        holder.transform,
                        out string _) == null)
                {
                    return false;
                }

                if (!TryFrame(camera, holder))
                {
                    return false;
                }

                byte[] png = Render(camera);
                File.WriteAllBytes(
                    $"{OutputDirectory}/{definition.StableId}.png",
                    png);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// Points the camera at the piece and sizes the frame to it.
        ///
        /// Framed on the renderers rather than on the requested size, because a
        /// model's own bounds and the size it was scaled to are different numbers
        /// — a long thin sausage scaled to "one metre" is one metre on its longest
        /// axis and a few centimetres on the others.
        /// </summary>
        private static bool TryFrame(Camera camera, GameObject subject)
        {
            Renderer[] renderers =
                subject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            float reach = Mathf.Max(
                0.05f,
                Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));

            // A tenth of margin, so nothing touches the edge of the cell.
            camera.orthographicSize = reach * 1.35f;
            camera.transform.position = bounds.center
                - (camera.transform.forward * (reach * 4f + 10f));
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = reach * 12f + 40f;
            return true;
        }

        private static byte[] Render(Camera camera)
        {
            var target = new RenderTexture(Size, Size, 24)
            {
                antiAliasing = 8
            };
            RenderTexture previous = RenderTexture.active;
            var frame = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
                frame.Apply();
                return frame.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(frame);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Imports the written file as a sprite with its own size preserved.
        ///
        /// <c>npotScale</c> is forced off. Unity's default rounds a texture to the
        /// nearest power of two, and a 192px icon quietly becoming 256 is the
        /// class of failure that cost this project two afternoons on the lobby
        /// cutter — it does not error, it just answers a different question.
        /// </summary>
        private static void ImportAsSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
    }
}
