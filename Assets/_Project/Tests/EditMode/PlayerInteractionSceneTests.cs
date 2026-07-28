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

            // Six ISSUE-011 loot pieces plus the two LOOT-005 hiding spots all
            // use the Loot type, which keeps them thief only. The three
            // Traversal targets are the MAP-003 climbable ladders, one per
            // store.
            Assert.That(
                targets.Select(target => target.InteractionType),
                Is.EquivalentTo(new[]
                {
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Sale,
                    PlayerInteractionType.Traversal,
                    PlayerInteractionType.Traversal,
                    PlayerInteractionType.Traversal,
                    // The plaza marker, five THROW-005 rock pickups and the
                    // four THROW-009 police prop pickups.
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic,
                    PlayerInteractionType.Generic
                }));

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

            // THROW-009. The officer has both of their props on the map.
            Assert.That(
                pickups.Count(pickup =>
                    pickup.Kind == ThrowableKind.GlueTrap),
                Is.GreaterThan(0));
            Assert.That(
                pickups.Count(pickup =>
                    pickup.Kind == ThrowableKind.SensorLight),
                Is.GreaterThan(0));
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
            Assert.That(loot, Has.Length.EqualTo(6));

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
            Assert.That(ladders, Has.Length.EqualTo(3));
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
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            LootSaleZone>(true))
                    .ToArray(),
                Has.Length.EqualTo(1));
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
