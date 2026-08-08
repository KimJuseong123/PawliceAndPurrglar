using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Puts import settings on the things that ship whether or not anybody
    /// references them.
    ///
    /// <b>Everything under a <c>Resources</c> folder is in the build.</b> Not
    /// "if a prefab points at it" — always, at full weight, because the whole
    /// point of the folder is that code can ask for a path at runtime and Unity
    /// cannot know in advance which paths those are. That makes it the one place
    /// where an oversized source file is a shipping cost rather than a disk
    /// cost, and it is exactly where the oversized files were.
    ///
    /// The HUD's hand-drawn item icons are <b>1254×1254</b>. The icons baked
    /// from the loot models, which sit in a subfolder and do the same job in the
    /// same slot, are <b>192×192</b>. Nothing asked for the difference; the
    /// drawn ones were simply imported at whatever the artist exported, and the
    /// 2048 default cap never bit because they were already under it.
    ///
    /// Re-runnable and idempotent: it reports what it changed and reimports only
    /// those. Run it after adding anything to a Resources folder.
    /// </summary>
    public static class ResourceWeightPass
    {
        /// <summary>
        /// The widest a HUD icon is ever drawn is a quick-slot at roughly 96
        /// screen pixels on a 1080p canvas. 256 leaves room for a larger canvas
        /// scale and for the merchant window's bigger cells, and is still above
        /// the 192 the baked icons have always used.
        /// </summary>
        private const int IconMaxSize = 256;

        /// <summary>
        /// Half. Vorbis at quality 1 spends its whole budget on a looping lobby
        /// track that plays under a menu; at 0.5 the difference is not audible
        /// through the compression the browser applies on top.
        /// </summary>
        private const float MusicQuality = 0.5f;

        private static readonly string[] IconFolders =
        {
            "Assets/_Project/Resources/UI/ItemIcons"
        };

        private static readonly string[] AudioFolders =
        {
            "Assets/_Project/Resources/Audio"
        };

        [MenuItem("Paws & Loot/Build/Optimize Resource Import Settings")]
        public static void Run()
        {
            var changed = new List<string>();
            long before = 0;
            long after = 0;

            foreach (string folder in IconFolders)
            {
                foreach (string path in
                    AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(path);
                    before += FileBytes(assetPath);
                    if (ApplyIconSettings(assetPath))
                    {
                        changed.Add(assetPath);
                    }

                    after += FileBytes(assetPath);
                }
            }

            foreach (string folder in AudioFolders)
            {
                foreach (string guid in
                    AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (ApplyMusicSettings(assetPath))
                    {
                        changed.Add(assetPath);
                    }
                }
            }

            Debug.Log(
                $"[ResourceWeight] {changed.Count} asset(s) re-imported.\n  "
                + string.Join("\n  ", changed));

            if (before > 0)
            {
                Debug.Log(
                    "[ResourceWeight] Source bytes are unchanged on disk "
                    + $"({before / 1024}KB); what shrinks is the imported "
                    + "texture the build packs, which is capped at "
                    + $"{IconMaxSize}px instead of the source resolution.");
            }

            AssetDatabase.SaveAssets();
        }

        private static long FileBytes(string assetPath)
        {
            return File.Exists(assetPath)
                ? new FileInfo(assetPath).Length
                : 0L;
        }

        /// <summary>
        /// Returns true when something actually changed, so a second run is
        /// silent rather than reimporting the whole folder again.
        /// </summary>
        private static bool ApplyIconSettings(string assetPath)
        {
            var importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            bool dirty = false;

            if (importer.maxTextureSize > IconMaxSize)
            {
                importer.maxTextureSize = IconMaxSize;
                dirty = true;
            }

            if (importer.textureCompression
                == TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression =
                    TextureImporterCompression.Compressed;
                dirty = true;
            }

            // A HUD sprite is never minified, so the mip chain is a third of the
            // texture spent on sizes nothing samples.
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }

            return dirty;
        }

        private static bool ApplyMusicSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                return false;
            }

            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;

            bool dirty = false;
            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                dirty = true;
            }

            if (settings.quality > MusicQuality + 0.001f)
            {
                settings.quality = MusicQuality;
                dirty = true;
            }

            // Decompressed on load meant the whole lobby track sat in memory as
            // PCM before the menu had drawn. Compressed in memory keeps it
            // encoded and costs a decode per play, which for one looping track
            // is nothing.
            if (settings.loadType != AudioClipLoadType.CompressedInMemory)
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                dirty = true;
            }

            if (dirty)
            {
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            return dirty;
        }
    }
}
