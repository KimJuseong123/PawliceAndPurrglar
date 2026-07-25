using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Loot;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class LootDefinitionTests
    {
        private const string LootDataRoot =
            "Assets/_Project/Data/Loot";
        private const string LootConfigPath =
            "Assets/_Project/Settings/Configs/LootConfig.asset";

        [Test]
        public void DefaultDefinitionsUseConfiguredRarityPrices()
        {
            LootConfig config =
                AssetDatabase.LoadAssetAtPath<LootConfig>(
                    LootConfigPath);
            LootDefinition[] definitions = LoadDefinitions();

            Assert.That(config, Is.Not.Null);
            Assert.That(definitions, Has.Length.EqualTo(3));
            Assert.That(
                definitions.Select(definition => definition.StableId),
                Is.Unique);
            Assert.That(
                definitions.ToDictionary(
                    definition => definition.Rarity,
                    definition => definition.GetPrice(config)),
                Is.EquivalentTo(new System.Collections.Generic.Dictionary<
                    LootRarity,
                    int>
                {
                    [LootRarity.Common] = 200,
                    [LootRarity.Uncommon] = 350,
                    [LootRarity.Rare] = 500
                }));
        }

        [Test]
        public void AssetNameDoesNotDeterminePrice()
        {
            LootConfig config =
                ScriptableObject.CreateInstance<LootConfig>();
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.name = "500";
            definition.Configure(
                "stable-test-id",
                "Price-looking name 9999",
                LootRarity.Common);

            Assert.That(definition.GetPrice(config), Is.EqualTo(200));

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(config);
        }

        private static LootDefinition[] LoadDefinitions()
        {
            return AssetDatabase.FindAssets(
                    "t:LootDefinition",
                    new[] { LootDataRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LootDefinition>)
                .ToArray();
        }
    }
}
