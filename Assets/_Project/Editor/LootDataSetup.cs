using System.Collections.Generic;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Loot;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    public static class LootDataSetup
    {
        public const string LootDataRoot =
            "Assets/_Project/Data/Loot";

        [MenuItem("Paws & Loot/Setup/Create Default Loot Data")]
        public static void CreateDefaultLootData()
        {
            EnsureFolder();
            CreateOrUpdate(
                "common-trinket",
                "Common Trinket",
                LootRarity.Common);
            CreateOrUpdate(
                "uncommon-watch",
                "Fine Watch",
                LootRarity.Uncommon);
            CreateOrUpdate(
                "rare-jewel",
                "Rare Jewel",
                LootRarity.Rare);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateDefaultLootData();
            Debug.Log("LOOT-008 default loot data created and validated.");
        }

        [MenuItem("Paws & Loot/Setup/Validate Default Loot Data")]
        public static void ValidateDefaultLootData()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:LootDefinition",
                new[] { LootDataRoot });
            if (guids.Length != 3)
            {
                throw new System.InvalidOperationException(
                    $"Expected 3 default loot definitions, found {guids.Length}.");
            }

            var stableIds = new HashSet<string>();
            foreach (string guid in guids)
            {
                LootDefinition definition =
                    AssetDatabase.LoadAssetAtPath<LootDefinition>(
                        AssetDatabase.GUIDToAssetPath(guid));
                definition.ValidateOrThrow();
                if (!stableIds.Add(definition.StableId))
                {
                    throw new System.InvalidOperationException(
                        $"Duplicate loot stable ID '{definition.StableId}'.");
                }
            }
        }

        private static void CreateOrUpdate(
            string stableId,
            string displayName,
            LootRarity rarity)
        {
            string path =
                $"{LootDataRoot}/{stableId}.asset";
            LootDefinition definition =
                AssetDatabase.LoadAssetAtPath<LootDefinition>(path);
            if (definition == null)
            {
                definition =
                    ScriptableObject.CreateInstance<LootDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(stableId, displayName, rarity);
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(LootDataRoot))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Project/Data",
                    "Loot");
            }
        }
    }
}
