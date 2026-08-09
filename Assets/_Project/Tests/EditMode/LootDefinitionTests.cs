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
            // Three originals the greybox map was built on, then the shop and
            // house stock: supermarket seven, bookshop six, jeweller's five,
            // and four in each of the two houses. Every room deals three fewer
            // pieces than it has places, so three stand empty each match and an
            // empty shelf tells the thief nothing.
            //
            // Counted so a definition that fails to write is visible: the map
            // would simply place one fewer thing to steal and say nothing.
            Assert.That(definitions, Has.Length.EqualTo(29));
            Assert.That(
                definitions.Select(definition => definition.StableId),
                Is.Unique);
            // Grouped rather than keyed by rarity. One definition per tier was
            // true while there were three of them, and keying by rarity threw
            // the moment two pieces shared one — which is now every tier, since
            // the shops hold four pieces each.
            //
            // What is being pinned is that price comes from the tier and only
            // from the tier: two Rare pieces are worth the same whatever they
            // are, whatever they are called, and however hard they are to carry.
            foreach (System.Linq.IGrouping<LootRarity, LootDefinition> tier in
                definitions.GroupBy(definition => definition.Rarity))
            {
                int expected = tier.Key switch
                {
                    LootRarity.Common => 40,
                    LootRarity.Uncommon => 70,
                    _ => 100
                };

                Assert.That(
                    tier.Select(definition => definition.GetPrice(config)),
                    Is.All.EqualTo(expected),
                    $"{tier.Key} pieces must all be worth {expected}: "
                    + string.Join(
                        ", ",
                        tier.Select(d => $"{d.StableId}={d.GetPrice(config)}")));
            }

            // And every tier is actually represented, so a run where the shop
            // data failed to write cannot pass by having nothing to check.
            Assert.That(
                definitions.Select(definition => definition.Rarity).Distinct(),
                Is.EquivalentTo(new[]
                {
                    LootRarity.Common,
                    LootRarity.Uncommon,
                    LootRarity.Rare
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

            Assert.That(definition.GetPrice(config), Is.EqualTo(40));

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
