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
            // Carry weight is not rarity. The watch is the second dearest
            // piece and the easiest to run with, and the trinket is the
            // cheapest and awkward — that mismatch is the whole choice at the
            // shelf. Making the expensive things uniformly heavy would collapse
            // it back into "take the dearest one you can reach".
            CreateOrUpdate(
                "common-trinket",
                "Common Trinket",
                LootRarity.Common,
                LootCarryType.TwoHand);
            CreateOrUpdate(
                "uncommon-watch",
                "Fine Watch",
                LootRarity.Uncommon,
                LootCarryType.Pocket);
            CreateOrUpdate(
                "rare-jewel",
                "Rare Jewel",
                LootRarity.Rare,
                LootCarryType.Bulky);
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
            LootRarity rarity,
            LootCarryType carryType)
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

            definition.Configure(stableId, displayName, rarity, carryType);
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
