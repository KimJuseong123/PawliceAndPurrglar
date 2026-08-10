using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Loot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// The shop pieces have to be twelve different things, drawn as themselves,
    /// standing where a thief can reach them.
    ///
    /// Every piece of loot in this game used to be the same jewellery box tinted
    /// purple, so the supermarket, the bookshop and the jeweller's all sold one
    /// object at three prices and nothing on screen said which was which. The
    /// data was already correct — the rarity, the carry cost and the alarm were
    /// all authored per piece — and none of it was visible.
    ///
    /// Measured off the saved scene, because that is what ships. A generator that
    /// reports "12 shop pieces" has said what it tried to do, not what came out.
    /// </summary>
    public sealed class ShopLootModelTests
    {
        private const string ScenePath =
            "Assets/_Project/Scenes/Game.unity";
        private const string LootRoot = "Assets/_Project/Data/Loot";

        private static readonly string[] ShopIds =
        {
            "market-bread", "market-liquor", "market-cash-drawer",
            "market-beef-set", "market-cheese", "market-watermelon",
            "market-ginseng",
            "book-plain", "book-figure", "book-pen", "book-laptop",
            "book-wallet", "book-magic",
            "jewel-ruby", "jewel-watch", "jewel-gold-bar", "jewel-sapphire",
            "jewel-ring"
        };

        /// <summary>
        /// The houses' stock, kept apart from the shops' for one reason: these
        /// share models with the shops on purpose, so they cannot join the
        /// distinctness check above.
        ///
        /// A house having cheese in it is the same cheese. What separates the
        /// two is price and place, and asserting a second model would only force
        /// a second cheese to be modelled for nothing.
        /// </summary>
        private static readonly string[] HouseIds =
        {
            "house1-sausage", "house1-cheese", "house1-headphones",
            "house1-laptop",
            "house2-popcorn", "house2-notes", "house2-wallet",
            "house2-liquor"
        };

        /// <summary>
        /// Every house piece is authored and draws as itself.
        ///
        /// Same check as the shops minus the distinctness, and here for the same
        /// reason: a piece with no model falls back to the shared jewellery box,
        /// which makes a sausage and a laptop the same object on screen. Nothing
        /// in the game says so — the price is right, the carry cost is right,
        /// and the thief simply cannot tell what they picked up.
        /// </summary>
        [Test]
        public void EveryHousePieceIsAuthoredAndDrawsAsItself()
        {
            foreach (string id in HouseIds)
            {
                var definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                    $"{LootRoot}/{id}.asset");
                Assert.That(
                    definition,
                    Is.Not.Null,
                    $"No loot definition '{id}'. Run 'Create Default Loot "
                    + "Data'.");
                Assert.That(
                    definition.ModelStem,
                    Is.Not.Empty,
                    $"'{id}' names no model, so it falls back to the shared "
                    + "jewellery box and looks like every other piece.");
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"Assets/_Project/Art/Props/{definition.ModelStem}.fbx"),
                    Is.Not.Null,
                    $"'{id}' names model '{definition.ModelStem}' and there is "
                    + "no FBX by that name.");
            }
        }

        /// <summary>
        /// Each room deals three fewer pieces than it has places.
        ///
        /// This is the rule the whole indoor draw rests on. Fewer pieces than
        /// places is what makes two of them land differently every match, and
        /// it is also what guarantees no two pieces share a spot — the draw
        /// removes a place as it uses it, so it can only run out if the counts
        /// are equal. Fill a room exactly and the draw still succeeds, still
        /// logs a cheerful number, and quietly becomes a fixed layout.
        ///
        /// Counted off the definitions and the marked places rather than off
        /// either generator's log, because a generator reports what it meant.
        /// </summary>
        [Test]
        public void EveryRoomHasThreeMorePlacesThanPieces()
        {
            (string Prefix, int Places, string Room)[] rooms =
            {
                ("market-", 10, "supermarket"),
                ("book-", 9, "bookshop"),
                ("house1-", 7, "one-storey house"),
                ("house2-", 7, "two-storey house")
            };

            foreach ((string prefix, int places, string room) in rooms)
            {
                int pieces = AssetDatabase
                    .FindAssets("t:LootDefinition", new[] { LootRoot })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<LootDefinition>(
                        AssetDatabase.GUIDToAssetPath(guid)))
                    .Count(definition => definition != null
                        && definition.StableId.StartsWith(prefix)
                        && !definition.RaisesAlarm);

                Assert.That(
                    pieces,
                    Is.EqualTo(places - 3),
                    $"The {room} deals {pieces} pieces over {places} places. "
                    + "Three places have to stay empty, or the room is the same "
                    + "room every match.");
            }
        }

        [Test]
        public void EveryShopPieceHasItsOwnDefinitionAndModel()
        {
            var stems = new List<string>();
            foreach (string id in ShopIds)
            {
                var definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                    $"{LootRoot}/{id}.asset");
                Assert.That(
                    definition,
                    Is.Not.Null,
                    $"No loot definition '{id}'. Run 'Create Default Loot Data'.");
                Assert.That(
                    definition.ModelStem,
                    Is.Not.Empty,
                    $"'{id}' names no model, so it falls back to the shared "
                    + "jewellery box and looks like every other piece.");

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/_Project/Art/Props/{definition.ModelStem}.fbx");
                Assert.That(
                    model,
                    Is.Not.Null,
                    $"'{id}' names model '{definition.ModelStem}' and there is "
                    + "no FBX by that name.");
                stems.Add(definition.ModelStem);
            }

            Assert.That(
                stems.Distinct().Count(),
                Is.EqualTo(stems.Count),
                "Two shop pieces share a model: "
                + string.Join(", ", stems.GroupBy(s => s)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)));
        }

        /// <summary>
        /// A pocket piece must be smaller than a two-hander on screen.
        ///
        /// The carry cost is the choice the thief makes at the shelf, and it is
        /// only a choice if it can be seen before committing to the pickup. Sizes
        /// come from the carry type for exactly this reason, so this pins that
        /// the rule survived into the models.
        /// </summary>
        [Test]
        public void SizeFollowsHowThePieceIsCarried()
        {
            Assert.That(
                LootDefinition.GetModelSize(LootCarryType.Pocket),
                Is.LessThan(LootDefinition.GetModelSize(LootCarryType.OneHand)));
            Assert.That(
                LootDefinition.GetModelSize(LootCarryType.OneHand),
                Is.LessThan(LootDefinition.GetModelSize(LootCarryType.TwoHand)));
            Assert.That(
                LootDefinition.GetModelSize(LootCarryType.TwoHand),
                Is.LessThan(LootDefinition.GetModelSize(LootCarryType.Bulky)));
        }

        [Test]
        public void TheSceneDrawsEachShopPieceAsItself()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            try
            {
                var drawn = new Dictionary<string, string>();
                foreach (LootItem item in Object
                    .FindObjectsByType<LootItem>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None))
                {
                    LootDefinition definition = item.Definition;
                    if (definition == null
                        || string.IsNullOrEmpty(definition.ModelStem))
                    {
                        continue;
                    }

                    Transform model = FindByName(
                        item.transform,
                        $"{definition.ModelStem}_Model");
                    if (model != null)
                    {
                        drawn[definition.StableId] = definition.ModelStem;
                    }
                }

                foreach (string id in ShopIds)
                {
                    Assert.That(
                        drawn.ContainsKey(id),
                        Is.True,
                        $"'{id}' is not drawn with its own model anywhere in the "
                        + "scene. Either it was never placed or it fell back to "
                        + "the shared placeholder. Rebuild the greybox village.");
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        /// <summary>
        /// Two pieces on the same spot make one of them impossible to take: the
        /// scanner picks a winner and the loser is never offered.
        /// </summary>
        [Test]
        public void NoTwoShopPiecesStandOnTheSameSpot()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            try
            {
                var spots = new List<(string Id, Vector3 At)>();
                foreach (LootItem item in Object
                    .FindObjectsByType<LootItem>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None))
                {
                    if (item.Definition != null
                        && ShopIds.Contains(item.Definition.StableId))
                    {
                        spots.Add((
                            item.Definition.StableId,
                            item.transform.position));
                    }
                }

                // Every kind is somewhere, rather than exactly one of each.
                //
                // Each interior stocks itself now, so a town with two
                // supermarkets has two of every supermarket piece — and that is
                // the point of the change: one room per shop kind left eight of
                // the thirteen marked interiors with nothing in them. What still
                // has to hold is that the kinds all exist and that no two pieces
                // share a spot, which the loop below checks across every copy in
                // the town and not merely within a room.
                foreach (string id in ShopIds)
                {
                    Assert.That(
                        spots.Any(spot => spot.Id == id),
                        Is.True,
                        $"No '{id}' anywhere in the town.");
                }

                for (int a = 0; a < spots.Count; a++)
                {
                    for (int b = a + 1; b < spots.Count; b++)
                    {
                        float gap = Vector3.Distance(spots[a].At, spots[b].At);
                        Assert.That(
                            gap,
                            Is.GreaterThan(0.9f),
                            $"'{spots[a].Id}' and '{spots[b].Id}' are "
                            + $"{gap:0.00} m apart. Overlapping pickups mean "
                            + "one of them can never be chosen.");
                    }
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (Transform child in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
