using UnityEditor;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Cuts the lobby's logo and four characters out of the authored mockup.
    ///
    /// The lobby used to be that mockup, stretched behind invisible buttons
    /// pinned at hard-coded pixel offsets. Every element therefore moved as one
    /// picture and nothing lined up once the window stopped being the mockup's
    /// 4:3.
    ///
    /// Only the logo and the characters come from here. The paw and microphone
    /// marks are beige on ivory, too close to the background for any colour key
    /// to separate, so <see cref="LobbyUiSpriteFactory"/> draws those.
    /// </summary>
    public static class LobbyArtExtractor
    {
        private const string MockupPath =
            "Assets/_Project/UI/Lobby/lobby_default.png";

        internal const string OutputFolder =
            "Assets/_Project/UI/Lobby/Elements";

        /// <summary>
        /// Search windows, not final crops: each is trimmed to its own alpha
        /// bounding box afterwards, so they only have to separate one element
        /// from the next.
        /// </summary>
        private static readonly MockupCutter.Element[] Elements =
        {
            new(
                "lobby_logo",
                425,
                65,
                540,
                295,
                MockupCutter.BlobFilter.DropEdgeTouching),
            new(
                "lobby_police",
                95,
                240,
                265,
                485,
                MockupCutter.BlobFilter.CoreOverlap),
            new(
                "lobby_dog",
                360,
                375,
                232,
                335,
                MockupCutter.BlobFilter.CoreOverlap),
            new(
                "lobby_thief",
                872,
                248,
                285,
                452,
                MockupCutter.BlobFilter.CoreOverlap),
            new(
                "lobby_cat",
                1157,
                385,
                210,
                315,
                MockupCutter.BlobFilter.CoreOverlap)
        };

        [MenuItem("Paws & Loot/UI/Extract Lobby Art From Mockup")]
        public static void Extract()
        {
            MockupCutter.Cut(MockupPath, OutputFolder, Elements);
        }

        internal static void ApplySpriteImportSettings(string path)
        {
            MockupCutter.ApplySpriteImportSettings(path);
        }

        internal static void EnsureFolder(string folder)
        {
            MockupCutter.EnsureFolder(folder);
        }
    }
}
