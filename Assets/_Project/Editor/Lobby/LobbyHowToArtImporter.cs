using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Brings the lobby's how-to pages in as sprites.
    ///
    /// Source PNGs go in <see cref="SourceFolder"/> named so they sort into
    /// reading order; they are copied to the UI folder as
    /// <c>howto_page1.png</c>, <c>howto_page2.png</c>, ... which is the order
    /// the panel pages through.
    ///
    /// The import settings are the point of this existing at all. Unity's
    /// default <c>npotScale</c> is <c>ToNearest</c>, which quietly rescales a
    /// non-power-of-two image — a 1672x941 mockup became 2048x1024 here once,
    /// and every coordinate measured against the original then pointed
    /// somewhere else. Nothing reports it; the picture simply is not the
    /// picture that was measured.
    /// </summary>
    public static class LobbyHowToArtImporter
    {
        public const string SourceFolder = "ArtSource/Lobby/HowTo";

        private const string TargetFolder = LobbyCanvasBuilder.HowToFolder;

        [MenuItem("PawliceAndPurrglar/UI/Import Lobby How-To Art", priority = 46)]
        public static void Import()
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string source = Path.Combine(root, SourceFolder);

            // The normalized copies win when they exist.
            //
            // The authored crops do not share a canvas — different sizes,
            // different aspect ratios, an opaque background a shade off the
            // lobby's own, and slivers of the arrow buttons and the title logo
            // along the edges. `preserveAspect` centres inside its rect, so
            // three different aspects means the panel changes size as the
            // player pages through it. `Tools/normalize_howto_pages.py` puts
            // them on one canvas; this prefers its output so a re-import cannot
            // silently fall back to the raw crops.
            string normalized = Path.Combine(source, "normalized");
            if (Directory.Exists(normalized)
                && Directory.GetFiles(normalized, "*.png").Length > 0)
            {
                source = normalized;
                Debug.Log(
                    "[UI] Using the normalized how-to pages. Re-run "
                    + "Tools/normalize_howto_pages.py after changing the "
                    + "authored crops.");
            }
            else
            {
                Debug.LogWarning(
                    "[UI] No normalized how-to pages found, so the authored "
                    + "crops are being imported as they are. If they do not "
                    + "share one canvas the panel will change size between "
                    + "pages. Run Tools/normalize_howto_pages.py.");
            }

            if (!Directory.Exists(source))
            {
                Debug.LogError(
                    $"[UI] No how-to source folder at '{SourceFolder}'. Put one "
                    + "PNG per page in it, named so they sort in reading order "
                    + "(page1.png, page2.png, page3.png).");
                return;
            }

            var files = new List<string>(
                Directory.GetFiles(source, "*.png", SearchOption.TopDirectoryOnly));
            files.Sort(string.CompareOrdinal);

            if (files.Count == 0)
            {
                Debug.LogError(
                    $"[UI] '{SourceFolder}' has no PNG in it, so there are no "
                    + "pages to import.");
                return;
            }

            Directory.CreateDirectory(TargetFolder);

            // Cleared first. Leaving an old page4 behind when the source drops
            // to three would page into a picture nobody meant to ship, and the
            // builder counts pages by asking which files exist.
            foreach (string stale in Directory.GetFiles(
                TargetFolder,
                "howto_page*.png"))
            {
                AssetDatabase.DeleteAsset(
                    stale.Replace('\\', '/')
                        .Substring(stale.Replace('\\', '/')
                            .IndexOf("Assets/", System.StringComparison.Ordinal)));
            }

            for (int index = 0; index < files.Count; index++)
            {
                string target = $"{TargetFolder}/howto_page{index + 1}.png";
                File.Copy(files[index], target, true);
                AssetDatabase.ImportAsset(
                    target,
                    ImportAssetOptions.ForceUpdate);
                ApplySpriteSettings(target);
                Debug.Log(
                    $"[UI] How-to page {index + 1} <- "
                    + Path.GetFileName(files[index]));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[UI] Imported {files.Count} how-to page(s). Run "
                + "'Rebuild Lobby (Art, Prefab, Scene)' to put them on screen.");
        }

        private static void ApplySpriteSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            // The one setting this method exists for. See the class comment.
            importer.npotScale = TextureImporterNPOTScale.None;

            // These pages are read at close to their authored size on a 1920
            // reference, so a compressed one shows blocking on the thin outlines
            // and on Korean text. They are three images.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            importer.SaveAndReimport();
        }
    }
}
