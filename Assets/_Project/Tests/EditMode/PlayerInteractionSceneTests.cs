using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class PlayerInteractionSceneTests
    {
        [Test]
        public void GameSceneContainsSharedInteractionInfrastructure()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            PlayerInteractionScanner[] scanners = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        PlayerInteractionScanner>(true))
                .ToArray();
            IPlayerInteractable[] targets = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        MonoBehaviour>(true))
                .OfType<IPlayerInteractable>()
                .ToArray();
            CommonHudPresenter[] presenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        CommonHudPresenter>(true))
                .ToArray();
            ThiefHudPresenter[] thiefPresenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ThiefHudPresenter>(true))
                .ToArray();
            PoliceHudPresenter[] policePresenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        PoliceHudPresenter>(true))
                .ToArray();
            ArrestHudPresenter[] arrestHudPresenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ArrestHudPresenter>(true))
                .ToArray();
            ArrestRangeSensor[] arrestSensors = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ArrestRangeSensor>(true))
                .ToArray();
            ArrestProgressController[] arrestProgressControllers = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ArrestProgressController>(true))
                .ToArray();
            ArrestCompletionController[] arrestCompletionControllers = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ArrestCompletionController>(true))
                .ToArray();
            MatchResultEvaluator[] resultEvaluators = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        MatchResultEvaluator>(true))
                .ToArray();
            MatchEndController[] endControllers = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        MatchEndController>(true))
                .ToArray();
            MatchResultFlowController[] resultFlows = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        MatchResultFlowController>(true))
                .ToArray();

            Assert.That(scanners, Has.Length.EqualTo(2));

            // Counted by category rather than listed one by one.
            //
            // The list used to be spelled out, and every feature that added an
            // interactable rewrote it — five rocks, two shop counters, then
            // sixteen house doorways. Counting says the same thing about the
            // parts that matter and stops the churn that made the assertion feel
            // like paperwork.
            var byType = targets
                .GroupBy(target => target.InteractionType)
                .ToDictionary(group => group.Key, group => group.Count());

            // Six ISSUE-011 loot pieces, the alarmed crown jewel and the case
            // it stands in, the two LOOT-005 hiding spots, and the twelve shop
            // pieces the three shops now hold (docs/17 section 7).
            //
            // Twenty-three rather than twenty-two: the jeweller's ring is both a
            // shop piece and alarmed, so it brings its own display case. Plus the
            // display case key, which is a thief-only pickup and so counts here
            // even though it is not worth any gold. Twenty-five since the
            // sapphire joined the jeweller's random draw.
            //
            // Thirty-eight since every room was filled to three fewer pieces
            // than it has marked places: three more in the supermarket, two in
            // the bookshop, and four in each of the two houses. The three empty
            // places are the point — a room the thief can learn is a room they
            // stop searching.
            //
            // Seventy-three since every *interior* stocks itself rather than one
            // room per shop kind claiming the town's whole supply. Thirteen rooms
            // are marked and five of them had anything in them, so eight houses
            // were seven bare shelves — indistinguishable from a house the thief
            // had already emptied, which is the one thing the officer walks in
            // there to find out. The extra thirty-five are copies: three more
            // house01 rooms and four more house02 at four pieces each, plus a
            // second supermarket at seven.
            Assert.That(
                byType[PlayerInteractionType.Loot],
                Is.EqualTo(73),
                "Loot is thief-only, and the count is the thief's whole "
                + "victory path.");
            Assert.That(
                byType[PlayerInteractionType.Sale],
                Is.EqualTo(5),
                "Five places to sell, of which a match opens two. One was a "
                + "place the officer could stand on, and standing on the "
                + "selling point is the strongest thing an officer can do here "
                + "and the least interesting — the thief has to come back to "
                + "sell, so the only defence a single point had was that nobody "
                + "had thought to camp it. All five are built; BlackMarketDraw "
                + "switches three off when a match starts, and the host's "
                + "choice is replicated because being switched off is not a "
                + "fact that travels on its own.");
            // At least three, not exactly three. It was one per store when the
            // stores were the only buildings with a roof worth reaching; the
            // town now puts one up the east face of every shop and every
            // single-storey house, and how many that is belongs to the town
            // rather than to this list.
            Assert.That(
                byType[PlayerInteractionType.Traversal],
                Is.GreaterThanOrEqualTo(3),
                "The MAP-003 climbable ladders.");

            // Everything either role may touch: the plaza marker, the rock
            // pickups, the shop counters and both sides of every house door.
            Assert.That(
                byType[PlayerInteractionType.Generic],
                Is.GreaterThanOrEqualTo(8),
                "The shared interactables are how both roles get props and get "
                + "through doors.");

            // Rocks have to be Generic, not Loot. PlayerRolePermissions gives
            // Loot to the thief alone, so a rock typed as Loot would be
            // invisible to the police — and a throwable the police cannot pick
            // up is not a throwable. This is the assertion that catches it.
            ThrowablePickup[] pickups = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<ThrowablePickup>(true))
                .ToArray();
            Assert.That(pickups, Is.Not.Empty);
            foreach (ThrowablePickup pickup in pickups)
            {
                Assert.That(
                    PlayerRolePermissions.CanInteract(
                        PlayerRole.Police,
                        pickup.InteractionType),
                    Is.True,
                    $"'{pickup.name}' cannot be picked up by the police.");
                Assert.That(
                    PlayerRolePermissions.CanInteract(
                        PlayerRole.Thief,
                        pickup.InteractionType),
                    Is.True,
                    $"'{pickup.name}' cannot be picked up by the thief.");
            }

            // Ownership is the pickup's own business, and it has to agree with
            // the catalog. A glue trap the thief can take, or a rock reserved for
            // one side, would quietly merge the two kits into one.
            foreach (ThrowablePickup pickup in pickups)
            {
                PlayerRole? owner =
                    ThrowableCatalog.GetOwner(pickup.Kind);
                Assert.That(
                    pickup.IsRoleRestricted,
                    Is.EqualTo(owner.HasValue),
                    $"'{pickup.name}' disagrees with the catalog about "
                    + "whether it belongs to one side.");
                if (owner.HasValue)
                {
                    Assert.That(
                        pickup.RestrictedTo,
                        Is.EqualTo(owner.Value),
                        $"'{pickup.name}' is reserved for the wrong side.");
                }
            }

            // THROW-011. The officer's props come from the shop, so none of
            // them lie about as free pickups.
            Assert.That(
                pickups.Where(pickup =>
                    ThrowableCatalog.GetOwner(pickup.Kind)
                    == PlayerRole.Police),
                Is.Empty);
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            LootHidingSpot>(true))
                    .ToArray(),
                Has.Length.EqualTo(2));

            // The thief's win condition is arithmetic, so it is asserted as
            // arithmetic: the loot on the map has to be worth more than the
            // target or the sale victory is unreachable no matter how well the
            // thief plays. That was ISSUE-011, and one spare piece is the
            // margin that keeps a single loss from ending the run.
            LootItem[] loot = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<LootItem>(true))
                .ToArray();
            // A floor rather than an exact number. What matters is stated as
            // arithmetic just below — the gold on the map against the target —
            // and a hard count says nothing extra while having to be edited
            // every time a piece is added. This one was 6 and became wrong the
            // day the crown jewel was placed, which is the whole argument.
            Assert.That(loot, Has.Length.AtLeast(6));

            MatchConfig matchConfig =
                UnityEditor.AssetDatabase.LoadAssetAtPath<MatchConfig>(
                    "Assets/_Project/Settings/Configs/MatchConfig.asset");
            LootConfig lootConfig =
                UnityEditor.AssetDatabase.LoadAssetAtPath<LootConfig>(
                    "Assets/_Project/Settings/Configs/LootConfig.asset");
            Assert.That(matchConfig, Is.Not.Null);
            Assert.That(lootConfig, Is.Not.Null);

            int onTheMap = loot.Sum(
                item => lootConfig.GetPrice(item.Definition.Rarity));
            Assert.That(
                onTheMap,
                Is.GreaterThan(matchConfig.TargetSaleAmount),
                $"The map holds {onTheMap} gold against a target of "
                + $"{matchConfig.TargetSaleAmount}. Without a margin the thief "
                + "has to sell every single piece to win.");

            LadderTraversal[] ladders = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<LadderTraversal>(true))
                .ToArray();
            // Three or more. How many the town puts up is the town's decision;
            // what this cares about is that climbing exists at all and that
            // every one of them is wired.
            Assert.That(ladders, Has.Length.GreaterThanOrEqualTo(3));
            foreach (LadderTraversal ladder in ladders)
            {
                Assert.DoesNotThrow(ladder.ValidateOrThrow);
                Assert.That(
                    ladder.TopPoint.position.y,
                    Is.GreaterThan(ladder.BottomPoint.position.y),
                    "A ladder must lead upward.");
            }
            Assert.That(presenters, Has.Length.EqualTo(1));
            Assert.That(thiefPresenters, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                thiefPresenters[0].ValidateOrThrow);
            Assert.That(policePresenters, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                policePresenters[0].ValidateOrThrow);
            Assert.That(arrestHudPresenters, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                arrestHudPresenters[0].ValidateOrThrow);
            Assert.That(arrestSensors, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(arrestSensors[0].ValidateOrThrow);
            Assert.That(
                arrestProgressControllers,
                Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                arrestProgressControllers[0].ValidateOrThrow);
            Assert.That(
                arrestCompletionControllers,
                Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                arrestCompletionControllers[0].ValidateOrThrow);
            Assert.That(resultEvaluators, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                resultEvaluators[0].ValidateOrThrow);
            Assert.That(endControllers, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                endControllers[0].ValidateOrThrow);
            Assert.That(resultFlows, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                resultFlows[0].ValidateOrThrow);
            // Five selling points authored, two open per match. See the count
            // by interaction type above for why one was not enough.
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            LootSaleZone>(true))
                    .ToArray(),
                Has.Length.EqualTo(5));
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            ThiefLootWallet>(true))
                    .ToArray(),
                Has.Length.EqualTo(1));
        }
    }
}
