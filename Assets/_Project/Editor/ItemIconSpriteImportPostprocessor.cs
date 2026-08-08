using UnityEditor;

namespace PawsAndLoot.Editor
{
    public sealed class ItemIconSpriteImportPostprocessor : AssetPostprocessor
    {
        private const string ItemIconRoot =
            "Assets/_Project/Resources/UI/ItemIcons/";
        private const string CurrencyCoinPath =
            "Assets/_Project/Resources/UI/CurrencyCoin.png";

        private void OnPreprocessTexture()
        {
            if (!IsRuntimeHudIcon(assetPath)
                || assetImporter is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }

        [MenuItem("PawliceAndPurrglar/Setup/Ensure HUD Icon Sprites")]
        public static void EnsureHudIconSprites()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { "Assets/_Project/Resources/UI" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsRuntimeHudIcon(path)
                    || AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.mipmapEnabled
                    || !importer.alphaIsTransparency;
                if (!changed)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static bool IsRuntimeHudIcon(string path)
        {
            return path == CurrencyCoinPath
                || path.StartsWith(ItemIconRoot, System.StringComparison.Ordinal);
        }
    }
}
