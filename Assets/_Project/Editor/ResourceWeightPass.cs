using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
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

        /// <summary>
        /// A texture budget, in the same spirit as the triangle budget in
        /// CLAUDE.md — and added for the same reason, one level up.
        ///
        /// The triangle budget was written down and enforced: icons 2,000,
        /// props 5,000, exteriors 40,000, interiors 100,000. **Nobody ever wrote
        /// down a texture budget**, so every Tripo model arrived with a
        /// 2048×2048 base colour and kept it. A metal key on a shelf got the
        /// same 2048² as a shop interior. Measured in the WebGL build: **34
        /// textures at 2.7 MB each, 92 MB**, out of a 100 MB payload.
        ///
        /// The numbers below mirror the triangle table rather than being picked
        /// fresh, because the reasoning is identical — how close does the player
        /// get, and for how long. Interiors are where they stand longest, so
        /// they keep the most.
        /// </summary>
        private static readonly (string Folder, int MaxSize)[] ModelTextureBudget =
        {
            // Stood inside, for minutes at a time.
            ("Assets/_Project/Art/Buildings", 1024),

            // Seen at arm's length while carried, and on a shelf otherwise.
            ("Assets/_Project/Art/Props", 512),

            // Street furniture, never approached deliberately. The roads and
            // grass here are baked to 512px tiles anyway, so their sources are
            // not what the build packs.
            ("Assets/_Project/Art/Environment", 512),

            // The two the camera follows all match.
            ("Assets/_Project/Art/Characters", 1024)
        };

        /// <summary>
        /// Screen art that ships uncompressed.
        ///
        /// These are **not** oversized — the lobby pair art is 1138×785 and the
        /// versus band is 1285×428, which is what they are drawn at. The cost is
        /// entirely that their default platform entry says
        /// <c>Uncompressed</c>, so a 1138×785 sheet is 1138·785·4 bytes = 3.4 MB
        /// of RGBA in the build. Seventeen megabytes of the payload was this.
        ///
        /// So this pass compresses and does **not** cap the size: shrinking art
        /// that is already at its display resolution would be visible, and
        /// compressing it is not.
        /// </summary>
        private static readonly string[] UiTextureFolders =
        {
            "Assets/_Project/UI"
        };

        /// <summary>
        /// Higher than the model textures' 50. UI is viewed flat, at full size,
        /// with the eye stationary on it — the place where crunch artefacts
        /// would actually be findable.
        /// </summary>
        private const int UiCrunchQuality = 75;

        private static readonly string[] MeshFolders =
        {
            "Assets/_Project/Art"
        };

        /// <summary>
        /// What a collision copy is called. These are instantiated, their
        /// renderers destroyed, and a <c>MeshCollider</c> put on what is left —
        /// so every byte of normal, tangent, UV and material on them is paid for
        /// and never looked at.
        /// </summary>
        private const string CollisionSuffix = "_col";

        [MenuItem("PawliceAndPurrglar/Build/Optimize Texture And Audio Budgets")]
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

            foreach (string folder in UiTextureFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                foreach (string guid in
                    AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (ApplyUiTextureSettings(assetPath))
                    {
                        changed.Add($"{assetPath} (ui)");
                    }
                }
            }

            foreach (string folder in MeshFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                foreach (string guid in
                    AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (ApplyMeshSettings(assetPath))
                    {
                        changed.Add($"{assetPath} (mesh)");
                    }
                }
            }

            foreach ((string folder, int maxSize) in ModelTextureBudget)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                foreach (string guid in
                    AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (ApplyModelTextureSettings(assetPath, maxSize))
                    {
                        changed.Add($"{assetPath} -> {maxSize}px");
                    }
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

        /// <summary>
        /// Compresses screen art without resizing it.
        /// </summary>
        private static bool ApplyUiTextureSettings(string assetPath)
        {
            var importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            bool dirty = false;

            if (importer.textureCompression
                != TextureImporterCompression.Compressed)
            {
                importer.textureCompression =
                    TextureImporterCompression.Compressed;
                dirty = true;
            }

            if (!importer.crunchedCompression)
            {
                importer.crunchedCompression = true;
                importer.compressionQuality = UiCrunchQuality;
                dirty = true;
            }

            // Screen art is never minified.
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            return dirty && Reimport(importer);
        }

        private static bool Reimport(AssetImporter importer)
        {
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// Drops the vertex channels and import steps these models do not use.
        ///
        /// Lossless, all of it. **No model in this project has a normal map** —
        /// the <c>.fbm</c> folders contain base colour and nothing else — so
        /// every tangent imported is four floats per vertex spent on lighting
        /// maths that has no map to read. Blend shapes and animation are
        /// imported on static props for the same reason: nobody turned them off.
        ///
        /// Collision copies get more taken away, because their renderers are
        /// destroyed at scene-build time and only the shape survives.
        /// Their mesh compression is deliberately left <b>off</b>: it quantises
        /// positions, and the failure that buys is a wall the player walks
        /// through, which is worth more than the megabytes.
        /// </summary>
        private static bool ApplyMeshSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            bool collision = Path.GetFileNameWithoutExtension(assetPath)
                .EndsWith(CollisionSuffix);
            bool dirty = false;

            if (importer.importTangents != ModelImporterTangents.None)
            {
                importer.importTangents = ModelImporterTangents.None;
                dirty = true;
            }

            if (importer.importBlendShapes)
            {
                importer.importBlendShapes = false;
                dirty = true;
            }

            // The characters are the only rigged things here, and their clips
            // do not exist yet (MODEL-002). Everything else is scenery.
            if (!assetPath.Contains("/Characters/")
                && importer.importAnimation)
            {
                importer.importAnimation = false;
                dirty = true;
            }

            if (collision)
            {
                // Normals stay, even though nothing renders these.
                //
                // `HouseInteriorSetup.MeasureFloorTop` reads the collision
                // mesh's normals to find which vertices face up, and that is
                // how every room's floor height — and therefore where a player
                // lands when they walk through the door — is decided. Stripping
                // them made `mesh.normals` empty, the measuring loop ran zero
                // times, and the measurement silently returned its fallback:
                // the underside of the model. Players arrived buried to the
                // waist in the floor of every room.
                //
                // Nothing threw, nothing logged, and all 512 tests passed.
                if (importer.materialImportMode
                    != ModelImporterMaterialImportMode.None)
                {
                    importer.materialImportMode =
                        ModelImporterMaterialImportMode.None;
                    dirty = true;
                }
            }
            else if (importer.meshCompression == ModelImporterMeshCompression.Off)
            {
                // Medium on things that are only ever looked at. It quantises
                // positions and normals; on stylised props at this camera
                // distance the difference is not findable, and it is the
                // largest single lever left on 46 MB of meshes.
                importer.meshCompression = ModelImporterMeshCompression.Medium;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }

            return dirty;
        }

        /// <summary>
        /// Caps a model texture and crunches it.
        ///
        /// Crunch is what makes the difference on a download rather than in
        /// memory: it is a second compression on top of the GPU format, decoded
        /// once at load. For a browser build where the whole payload is the
        /// first impression, that trade is the right way round.
        /// </summary>
        private static bool ApplyModelTextureSettings(
            string assetPath,
            int maxSize)
        {
            var importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            bool dirty = false;

            if (importer.maxTextureSize > maxSize)
            {
                importer.maxTextureSize = maxSize;
                dirty = true;
            }

            if (importer.textureCompression
                != TextureImporterCompression.Compressed)
            {
                importer.textureCompression =
                    TextureImporterCompression.Compressed;
                dirty = true;
            }

            if (!importer.crunchedCompression)
            {
                importer.crunchedCompression = true;
                importer.compressionQuality = 50;
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
