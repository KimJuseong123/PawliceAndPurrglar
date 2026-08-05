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
            // The one the whole town is wired to notice. Alarmed, and the
            // heaviest thing on the map, so taking it is a decision about the
            // rest of the match rather than about one room.
            CreateOrUpdate(
                "rare-jewel",
                "Rare Jewel",
                LootRarity.Rare,
                LootCarryType.Bulky,
                true);
            CreateShopLoot();
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
            // Three originals, the twelve shop pieces and the sapphire that
            // makes the jeweller's draw four kinds. Counted rather than
            // left open, because a definition that fails to write is invisible
            // otherwise — the map simply places one fewer thing to steal.
            const int Expected = 16;
            if (guids.Length != Expected)
            {
                throw new System.InvalidOperationException(
                    $"Expected {Expected} loot definitions, found "
                    + $"{guids.Length}.");
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

        /// <summary>
        /// The pieces the three shops hold, from
        /// <c>docs/17_게임_아이템_사용처_정리.md</c> section 7's first pass.
        ///
        /// **Four price grades onto three rarities.** The document grades pieces
        /// 저가 / 중가 / 고가 / 최고가 and this game has priced three tiers since
        /// the win condition was set, so 고가 and 최고가 both land on
        /// <c>Rare</c>. The top grade is told apart by consequence instead of by
        /// price: the ring is the piece the whole town is wired to notice.
        /// Inventing a fourth tier would move the gold target, which is verified
        /// working, for a distinction the player reads off the siren anyway.
        ///
        /// **사과 is not here.** The document's first pass asks for an apple and
        /// no apple was ever modelled, so the 저가 / <c>POCKET</c> slot it fills
        /// is taken by the loaf from the same table.
        ///
        /// The three original pieces above stay. They are what the greybox map
        /// and its tests were built on, and removing them to make room would be
        /// a second change wearing this one's clothes.
        /// </summary>
        private static void CreateShopLoot()
        {
            // Supermarket. The widest spread of prices in the town, which is
            // what makes its narrow aisles worth walking into.
            CreateOrUpdate(
                "market-bread",
                "빵",
                LootRarity.Common,
                LootCarryType.Pocket,
                false,
                "loot_bread");
            CreateOrUpdate(
                "market-liquor",
                "고급 양주병",
                LootRarity.Rare,
                LootCarryType.OneHand,
                false,
                "loot_liquor_bottle");
            CreateOrUpdate(
                "market-cash-drawer",
                "계산대 돈통",
                LootRarity.Rare,
                LootCarryType.TwoHand,
                false,
                "loot_cash_drawer");
            CreateOrUpdate(
                "market-beef-set",
                "한우 선물세트",
                LootRarity.Rare,
                LootCarryType.TwoHand,
                false,
                "loot_beef_gift_set");

            // Bookshop. Few pieces, and the gap between the cheapest and the
            // dearest is the whole reason to search the shelves.
            CreateOrUpdate(
                "book-plain",
                "일반 책",
                LootRarity.Common,
                LootCarryType.Pocket,
                false,
                "loot_book");
            CreateOrUpdate(
                "book-figure",
                "캐릭터 피규어",
                LootRarity.Uncommon,
                LootCarryType.OneHand,
                false,
                "loot_figure_knight");
            CreateOrUpdate(
                "book-pen",
                "만년필",
                LootRarity.Rare,
                LootCarryType.Pocket,
                false,
                "loot_fountain_pen");
            CreateOrUpdate(
                "book-laptop",
                "노트북",
                LootRarity.Rare,
                LootCarryType.TwoHand,
                false,
                "loot_laptop");

            // Jeweller's. Everything here is dear, so the choice is not what it
            // is worth but what it costs to carry and whether it screams.
            CreateOrUpdate(
                "jewel-ruby",
                "루비",
                LootRarity.Rare,
                LootCarryType.Pocket,
                false,
                "loot_ruby");
            CreateOrUpdate(
                "jewel-watch",
                "황금 손목시계",
                LootRarity.Rare,
                LootCarryType.Pocket,
                false,
                "loot_gold_watch");
            CreateOrUpdate(
                "jewel-gold-bar",
                "금괴",
                LootRarity.Rare,
                LootCarryType.TwoHand,
                false,
                "loot_gold_bar");
            // The fourth kind, so the jeweller's random draw has four to draw.
            //
            // The shop holds four sellables in docs/17's first pass and one of
            // them — the ring — is now fixed inside the case in the middle of
            // the room. That left three for the shelves and a spec asking for
            // four, so the sapphire comes forward from the same table: 고가,
            // POCKET, and art already in ArtSource. Same handling as the ruby,
            // which keeps it from being a new mechanic wearing a new colour.
            CreateOrUpdate(
                "jewel-sapphire",
                "사파이어",
                LootRarity.Rare,
                LootCarryType.Pocket,
                false,
                "loot_sapphire");

            // The only alarmed piece in the shop, and the document's whole
            // 대왕 반지 event hangs off it.
            CreateOrUpdate(
                "jewel-ring",
                "다이아몬드 반지",
                LootRarity.Rare,
                LootCarryType.OneHand,
                true,
                "loot_diamond_ring");
        }

        private static void CreateOrUpdate(
            string stableId,
            string displayName,
            LootRarity rarity,
            LootCarryType carryType,
            bool raisesAlarm = false,
            string modelStem = null)
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

            definition.Configure(
                stableId,
                displayName,
                rarity,
                carryType,
                raisesAlarm,
                modelStem);
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
