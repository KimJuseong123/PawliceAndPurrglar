using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Cuts the result screen's artwork out of the two authored mockups.
    ///
    /// The result screen had the same problem as the lobby, and one more: its
    /// four labels were built at font size one, fully transparent and switched
    /// off, so the numbers a player read — the clock, the arrests, the gold —
    /// were painted into the picture and identical whatever had just happened.
    ///
    /// The versus illustration is cut as one piece per outcome. That is not the
    /// shortcut the lobby's single image was: it holds no controls and no
    /// figures, it is one drawing, and it changes as a unit when the winner
    /// changes. What comes out separately is everything a player reads or
    /// presses.
    ///
    /// The 승리 and 패배 badges are not cut at all. They sit across the corner
    /// of the illustration, so neither a rectangle nor a colour key separates
    /// them; <see cref="ResultCanvasBuilder"/> draws them from the same plate
    /// the buttons use, which also lets one sprite serve both outcomes.
    /// </summary>
    public static class ResultArtExtractor
    {
        private const string PoliceMockupPath =
            "Assets/_Project/UI/Result/result_police_win.png";

        private const string ThiefMockupPath =
            "Assets/_Project/UI/Result/result_thief_win.png";

        internal const string OutputFolder =
            "Assets/_Project/UI/Result/Elements";

        /// <summary>
        /// From the police mockup. "GAME OVER", the clock, the handcuffs and
        /// the coin are identical in both, so they are only taken once.
        /// </summary>
        private static readonly MockupCutter.Element[] PoliceElements =
        {
            new(
                "result_header",
                460,
                22,
                530,
                78,
                MockupCutter.BlobFilter.DropEdgeTouching),
            // Ends at y=286: below the laurel tips at 285, above the subtitle at
            // 288. The subtitle is dynamic and must not be baked in, and the
            // only reliable way to keep it out is to leave it outside the
            // window entirely — relying on the edge filter to drop it works
            // only when the glyphs happen to straddle the boundary.
            new(
                "result_title_police",
                225,
                78,
                1030,
                208,
                MockupCutter.BlobFilter.DropEdgeTouching),
            // Plain rectangle: the panels carry their own painted backgrounds,
            // and keying would punch holes straight through the artwork.
            new(
                "result_versus_police",
                50,
                326,
                1350,
                448,
                MockupCutter.BlobFilter.None),
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

        private static readonly MockupCutter.Element[] ThiefElements =
        {
            // Ends at y=256. The thief mockup sets its subtitle 26 pixels
            // higher than the police one, so the two windows cannot share a
            // height.
            new(
                "result_title_thief",
                225,
                78,
                1030,
                178,
                MockupCutter.BlobFilter.DropEdgeTouching),
            new(
                "result_versus_thief",
                50,
                326,
                1350,
                448,
                MockupCutter.BlobFilter.None),
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
            Color32 backdrop = MockupCutter.Cut(
                PoliceMockupPath,
                OutputFolder,
                PoliceElements);
            MockupCutter.Cut(ThiefMockupPath, OutputFolder, ThiefElements);
            Debug.Log(
                "Result art extracted. Backdrop sample "
                + $"#{backdrop.r:X2}{backdrop.g:X2}{backdrop.b:X2}.");
        }
    }
}
