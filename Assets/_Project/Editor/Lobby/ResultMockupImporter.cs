using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Brings the authored result mockups into the project under names that say
    /// what they are.
    ///
    /// The delivered files are named for the situation rather than the axes -
    /// "cops win cops view", "cops win theif loose" - which reads fine until you
    /// have to say which of the four a crop came from. There are two axes and the
    /// names now carry both: who won, and whose screen it is.
    ///
    /// That pair is the point of this art. A result screen says 승리 or 패배 from
    /// the *viewer's* side, and the illustration behind it shows whoever actually
    /// won, so the two are chosen independently. The old mockups could not express
    /// that: their titles read "경찰 승리!" and "도둑 승리!", which is the winner
    /// twice over and leaves the loser reading a title about somebody else.
    ///
    /// Copied rather than read in place because <see cref="MockupCutter"/> asks the
    /// asset database for its source, and the asset database only knows about
    /// Assets.
    /// </summary>
    public static class ResultMockupImporter
    {
        private const string SourceFolder = "ArtSource/Result";

        internal const string OutputFolder = "Assets/_Project/UI/Result";

        /// <summary>
        /// Delivered name to the name it gets here, as
        /// <c>result_{winner}_win_{viewer}_view</c>.
        /// </summary>
        private static readonly (string Source, string Target)[] Mockups =
        {
            ("cops win cops view", "result_police_win_police_view"),
            ("cops win theif loose", "result_police_win_thief_view"),
            ("theif win cops loose", "result_thief_win_police_view"),
            ("thief win theif view", "result_thief_win_thief_view")
        };

        [MenuItem("PawliceAndPurrglar/UI/Import Result Mockups")]
        public static void Import()
        {
            MockupCutter.EnsureFolder(OutputFolder);

            var written = new List<string>();
            foreach ((string source, string target) in Mockups)
            {
                string from = Path.GetFullPath($"{SourceFolder}/{source}.png");
                if (!File.Exists(from))
                {
                    throw new FileNotFoundException(
                        $"Result mockup is missing: {SourceFolder}/{source}.png. "
                        + "All four are needed: the title comes from the viewer's "
                        + "outcome and the illustration from the winner, so three "
                        + "of them cannot cover the fourth case.",
                        from);
                }

                string to = $"{OutputFolder}/{target}.png";
                File.Copy(from, Path.GetFullPath(to), true);
                written.Add(to);
            }

            AssetDatabase.Refresh();
            foreach (string path in written)
            {
                ApplyImportSettings(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Imported {written.Count} result mockups into '{OutputFolder}'. "
                + "Run 'Extract Result Art From Mockups' next.");
        }

        /// <summary>
        /// Readable at full size and uncompressed, because crops are taken off
        /// these pixels. Compression artefacts survive a crop and show up as fringe
        /// on a title that is meant to have hard pixel edges.
        /// </summary>
        private static void ApplyImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException(
                    $"Imported mockup has no importer: {path}",
                    path);
            }

            importer.textureType = TextureImporterType.Default;

            // The one that matters. These mockups are 1672x941, and the default
            // NPOT rule scales a non-power-of-two texture to the nearest one — so
            // the cutter was handed a 2048x1024 image and every window measured off
            // the file landed somewhere else. The 패배 title came out as a
            // six-kilobyte sliver of nothing, which is the only reason it was
            // noticed: a wrong crop that still contains pixels looks deliberate.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
