using PawsAndLoot.Config;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    public static class GameConfigSetup
    {
        public const string ConfigRoot = "Assets/_Project/Settings/Configs";
        public const string DefaultSetPath = ConfigRoot + "/DefaultGameConfigSet.asset";

        private const string MatchPath = ConfigRoot + "/MatchConfig.asset";
        private const string PlayerPath = ConfigRoot + "/PlayerConfig.asset";
        private const string LootPath = ConfigRoot + "/LootConfig.asset";
        private const string ArrestPath = ConfigRoot + "/ArrestConfig.asset";
        private const string CompanionPath = ConfigRoot + "/CompanionConfig.asset";
        private const string VoicePath = ConfigRoot + "/VoiceConfig.asset";

        [MenuItem("Paws & Loot/Setup/Create Default Config Assets")]
        public static void CreateDefaultConfigAssets()
        {
            EnsureConfigFolder();

            MatchConfig match = LoadOrCreate<MatchConfig>(MatchPath);
            PlayerConfig player = LoadOrCreate<PlayerConfig>(PlayerPath);
            LootConfig loot = LoadOrCreate<LootConfig>(LootPath);
            ArrestConfig arrest = LoadOrCreate<ArrestConfig>(ArrestPath);
            CompanionConfig companion = LoadOrCreate<CompanionConfig>(CompanionPath);
            VoiceConfig voice = LoadOrCreate<VoiceConfig>(VoicePath);
            GameConfigSet configSet = LoadOrCreate<GameConfigSet>(DefaultSetPath);

            configSet.Configure(match, player, loot, arrest, companion, voice);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(configSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            configSet.ValidateOrThrow();
            BasicSceneSetup.EnsureBootstrapServices();
            Debug.Log("BASE-004 default configuration assets created and validated.");
        }

        [MenuItem("Paws & Loot/Setup/Validate Default Config Assets")]
        public static void ValidateDefaultConfigAssets()
        {
            GameConfigSet configSet = AssetDatabase.LoadAssetAtPath<GameConfigSet>(DefaultSetPath);
            if (configSet == null)
            {
                throw new GameConfigurationException(
                    $"Default GameConfigSet is missing at '{DefaultSetPath}'.");
            }

            configSet.ValidateOrThrow();
            ValidateMissingReferenceMessage();
            Debug.Log("BASE-004 configuration validation passed.");
        }

        private static void ValidateMissingReferenceMessage()
        {
            GameConfigSet probe = ScriptableObject.CreateInstance<GameConfigSet>();
            probe.name = "MissingReferenceProbe";

            try
            {
                probe.ValidateOrThrow();
                throw new GameConfigurationException(
                    "Missing-reference validation probe unexpectedly succeeded.");
            }
            catch (GameConfigurationException exception)
            {
                if (!exception.Message.Contains("matchConfig"))
                {
                    throw new GameConfigurationException(
                        "Missing-reference validation did not identify the missing matchConfig field.",
                        exception);
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static void EnsureConfigFolder()
        {
            if (!AssetDatabase.IsValidFolder(ConfigRoot))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Configs");
            }
        }

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
