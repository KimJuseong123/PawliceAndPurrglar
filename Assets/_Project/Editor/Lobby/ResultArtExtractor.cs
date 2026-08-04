using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Cuts the result screen's artwork out of the authored mockups.
    ///
    /// The result screen had the same problem as the lobby, and one more: its four
    /// labels were built at font size one, fully transparent and switched off, so
    /// the numbers a player read — the clock, the arrests, the gold — were painted
    /// into the picture and identical whatever had just happened.
    ///
    /// Two axes, cut separately. The title says 승리 or 패배 from the *viewer's*
    /// side; the illustration shows whoever actually won. Cutting them from one
    /// image would tie them together, and they are not tied: the losing player sees
    /// 패배 over a picture of the winner celebrating.
    ///
    /// The versus illustration is cut as one piece per outcome. That is not the
    /// shortcut the lobby's single image was: it holds no controls and no figures to
    /// press, it is one drawing, and it changes as a unit when the winner changes.
    ///
    /// The 승리 and 패배 badges are not cut. They sit across the corner of the
    /// illustration, so neither a rectangle nor a colour key separates them;
    /// <see cref="ResultCanvasBuilder"/> draws them from the same plate the buttons
    /// use, which also lets one sprite serve both outcomes.
    ///
    /// Windows measured off the delivered files rather than read off a preview.
    /// Eyeballing them is what caught a neighbour's hat twice in the lobby cut-outs,
    /// and the mockups changed shape here — 1672x941 against the old 1448x1086 — so
    /// every old number was wrong by a different amount.
    /// </summary>
    public static class ResultArtExtractor
    {
        private const string ViewerWonMockup =
            ResultMockupImporter.OutputFolder
            + "/result_police_win_police_view.png";

        private const string ViewerLostMockup =
            ResultMockupImporter.OutputFolder
            + "/result_police_win_thief_view.png";

        private const string ThiefWonMockup =
            ResultMockupImporter.OutputFolder
            + "/result_thief_win_thief_view.png";

        /// <summary>
        /// The old 4:3 mockups, kept for the three stat icons and the header.
        ///
        /// Those five pieces are pixel-identical in the new art, and re-cutting
        /// them would be four more windows to measure for no visible change. The
        /// title and the illustration are what actually changed.
        /// </summary>
        private const string LegacyPoliceMockup =
            "Assets/_Project/UI/Result/result_police_win.png";

        private const string LegacyThiefMockup =
            "Assets/_Project/UI/Result/result_thief_win.png";

        internal const string OutputFolder =
            "Assets/_Project/UI/Result/Elements";

        /// <summary>
        /// The illustration window, shared by both outcomes.
        ///
        /// One window because the band is drawn in the same place in all four
        /// mockups — measured at x 193-1477, y 256-683 in every one of them. A
        /// per-file window would drift and the two illustrations would not line up
        /// when the screen swapped them.
        /// </summary>
        private const int BandX = 193;
        private const int BandY = 256;
        private const int BandWidth = 1285;
        private const int BandHeight = 428;

        private static readonly MockupCutter.Element[] ViewerWonElements =
        {
            // 승리. Generous rather than measured to the pixel: the row scan that
            // found the title reported it ending at y 246, but a scan counts a row
            // as content only once enough of it is ink, and the last rows of a
            // glyph are one thin stroke. 패배 lost the bottom of both its vowels
            // that way. The cutter trims to content anyway, so the only thing a
            // window has to do is miss the neighbours — GAME OVER ends at y 53 and
            // the illustration starts at y 256.
            new(
                "result_title_win",
                565,
                60,
                540,
                192,
                MockupCutter.BlobFilter.DropEdgeTouching),
            // Plain rectangle: the panels carry their own painted backgrounds, and
            // keying would punch holes straight through the artwork.
            new(
                "result_versus_police",
                BandX,
                BandY,
                BandWidth,
                BandHeight,
                MockupCutter.BlobFilter.None)
        };

        private static readonly MockupCutter.Element[] ViewerLostElements =
        {
            // 패배. Its own window because it is a different word in a different
            // place, and equally generous for the same reason 승리 is. Here GAME
            // OVER ends at y 45 and the illustration starts at y 258.
            new(
                "result_title_lose",
                565,
                56,
                540,
                196,
                MockupCutter.BlobFilter.DropEdgeTouching)
        };

        private static readonly MockupCutter.Element[] ThiefWonElements =
        {
            new(
                "result_versus_thief",
                BandX,
                BandY,
                BandWidth,
                BandHeight,
                MockupCutter.BlobFilter.None)
        };

        /// <summary>
        /// Header and stat icons, still off the old mockups. Windows unchanged
        /// because the images they refer to are unchanged.
        /// </summary>
        private static readonly MockupCutter.Element[] LegacyPoliceElements =
        {
            new(
                "result_header",
                460,
                22,
                530,
                78,
                MockupCutter.BlobFilter.DropEdgeTouching),
            new(
                "icon_clock",
                70,
                795,
                115,
                120,
                MockupCutter.BlobFilter.CoreOverlap),
            new(
                "icon_cuffs",
                410,
                800,
                125,
                110,
                MockupCutter.BlobFilter.CoreOverlap),
            new(
                "icon_coin",
                760,
                792,
                115,
                120,
                MockupCutter.BlobFilter.CoreOverlap)
        };

        private static readonly MockupCutter.Element[] LegacyThiefElements =
        {
            new(
                "icon_bag",
                412,
                798,
                120,
                118,
                MockupCutter.BlobFilter.CoreOverlap)
        };

        [MenuItem("Paws & Loot/UI/Extract Result Art From Mockups")]
        public static void Extract()
        {
            MockupCutter.Cut(ViewerWonMockup, OutputFolder, ViewerWonElements);
            MockupCutter.Cut(ViewerLostMockup, OutputFolder, ViewerLostElements);
            MockupCutter.Cut(ThiefWonMockup, OutputFolder, ThiefWonElements);

            Color32 backdrop = MockupCutter.Cut(
                LegacyPoliceMockup,
                OutputFolder,
                LegacyPoliceElements);
            MockupCutter.Cut(
                LegacyThiefMockup,
                OutputFolder,
                LegacyThiefElements);

            Debug.Log(
                "Result art extracted. Titles come from the viewer's outcome and "
                + "the illustration from the winner. Backdrop sample "
                + $"#{backdrop.r:X2}{backdrop.g:X2}{backdrop.b:X2}.");
        }
    }
}
