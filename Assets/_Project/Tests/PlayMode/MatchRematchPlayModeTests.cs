using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class MatchRematchPlayModeTests
    {
        private const float PositionTolerance = 0.01f;

        [UnityTest]
        public IEnumerator RematchResetsMatchStateAndSecondMatchEnds()
        {
            MatchResultSession.Clear();
            yield return LoadGameScene();

            MatchSceneRefs first = MatchSceneRefs.Collect();
            float fullMatchSeconds = first.Runtime.RemainingMatchSeconds;
            Vector3 policeSpawn = first.Police.transform.position;
            Vector3 thiefSpawn = first.Thief.transform.position;
            Vector3 lootSpawn = first.Loot.transform.position;

            Assert.That(fullMatchSeconds, Is.GreaterThan(0f));
            Assert.That(
                first.Loot.CurrentState,
                Is.EqualTo(LootState.Available));
            Assert.That(first.Wallet.SoldAmount, Is.Zero);

            // Match 1: dirty every value the rematch has to clear.
            first.Runtime.Tick(first.Runtime.ReadyCountdownRemainingSeconds);
            Assert.That(
                first.Runtime.CurrentState,
                Is.EqualTo(MatchState.Playing));

            first.Thief.transform.position =
                first.SaleZone.transform.position;
            Physics.SyncTransforms();
            Assert.That(first.Carrier.TryAcquire(first.Loot), Is.True);
            Assert.That(
                first.SaleZone.TryInteract(
                    new PlayerInteractionContext(first.Thief)),
                Is.True);
            Assert.That(first.Wallet.SoldAmount, Is.GreaterThan(0));
            Assert.That(
                first.Loot.CurrentState,
                Is.EqualTo(LootState.Sold));

            first.Police.transform.position =
                first.Thief.transform.position + new Vector3(0.5f, 0f, 0f);
            Physics.SyncTransforms();
            first.Sensor.Evaluate();
            Assert.That(first.Sensor.IsTargetDetected, Is.True);
            first.Progress.Tick(0.5f);
            Assert.That(first.Progress.ProgressSeconds, Is.GreaterThan(0f));

            first.Runtime.Tick(30f);
            Assert.That(
                first.Runtime.RemainingMatchSeconds,
                Is.LessThan(fullMatchSeconds));
            Assert.That(
                Vector3.Distance(
                    first.Thief.transform.position,
                    thiefSpawn),
                Is.GreaterThan(1f));

            Assert.That(
                first.EndController.TryEndMatch(
                    new MatchResult(
                        MatchWinner.Police,
                        MatchEndReason.ThiefArrested,
                        first.Wallet.SoldAmount,
                        first.Runtime.RemainingMatchSeconds)),
                Is.True);
            yield return null;

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(GameSceneCatalog.GetName(GameSceneId.Result)));

            // Rematch: the REMATCH button reloads the Game scene.
            yield return LoadGameScene();

            MatchSceneRefs second = MatchSceneRefs.Collect();

            // 타이머 초기화
            Assert.That(
                second.Runtime.RemainingMatchSeconds,
                Is.EqualTo(fullMatchSeconds));
            Assert.That(
                second.Runtime.CurrentState,
                Is.EqualTo(MatchState.Ready));

            // 점수 초기화
            Assert.That(second.Wallet.SoldAmount, Is.Zero);
            Assert.That(second.Wallet.CreditedSaleCount, Is.Zero);
            Assert.That(second.Wallet.LastSoldLoot, Is.Null);

            // 보물 초기화
            Assert.That(
                second.Loot.CurrentState,
                Is.EqualTo(LootState.Available));
            Assert.That(second.Loot.CurrentCarrier, Is.Null);
            Assert.That(second.Carrier.HasLoot, Is.False);
            Assert.That(
                Vector3.Distance(
                    second.Loot.transform.position,
                    lootSpawn),
                Is.LessThan(PositionTolerance));

            // 플레이어 위치 초기화
            Assert.That(
                Vector3.Distance(
                    second.Police.transform.position,
                    policeSpawn),
                Is.LessThan(PositionTolerance));
            Assert.That(
                Vector3.Distance(
                    second.Thief.transform.position,
                    thiefSpawn),
                Is.LessThan(PositionTolerance));

            // 체포 상태 초기화
            Assert.That(second.Progress.ProgressSeconds, Is.Zero);
            Assert.That(second.Progress.ProgressNormalized, Is.Zero);
            Assert.That(second.Progress.IsCompleted, Is.False);

            // 중복 HUD 없음
            Assert.That(
                Object.FindObjectsByType<RoleAwareHudController>(
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<CommonHudPresenter>(
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(0));
            Assert.That(
                Object.FindObjectsByType<MatchRuntimeState>(
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<MatchResultFlowController>(
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(1));

            // 이전 경기 결과가 남지 않는다.
            Assert.That(MatchResultSession.HasResult, Is.False);
            Assert.That(second.EndController.HasEnded, Is.False);

            // 두 번째 경기도 정상 종료
            second.Runtime.Tick(
                second.Runtime.ReadyCountdownRemainingSeconds);
            Assert.That(
                second.Runtime.CurrentState,
                Is.EqualTo(MatchState.Playing));
            Assert.That(
                second.EndController.TryEndMatch(
                    new MatchResult(
                        MatchWinner.Thief,
                        MatchEndReason.SaleTargetReached,
                        1000,
                        90f)),
                Is.True);
            yield return null;

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(GameSceneCatalog.GetName(GameSceneId.Result)));
            ResultScreenPresenter presenter =
                Object.FindFirstObjectByType<ResultScreenPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.PoliceBadgeText, Is.EqualTo("패배"));
            Assert.That(presenter.ThiefBadgeText, Is.EqualTo("승리"));
            Assert.That(
                presenter.ReasonText,
                Is.EqualTo("목표 골드를 모아 탈출에 성공했습니다"));
            Assert.That(presenter.GoldValueText, Is.EqualTo("1,000"));

            MatchResultSession.Clear();
        }

        private static IEnumerator LoadGameScene()
        {
            SceneManager.LoadScene(
                GameSceneCatalog.GetName(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;
        }

        private sealed class MatchSceneRefs
        {
            public MatchRuntimeState Runtime;
            public MatchEndController EndController;
            public ArrestRangeSensor Sensor;
            public ArrestProgressController Progress;
            public LootItem Loot;
            public LootSaleZone SaleZone;
            public PlayerRoleIdentity Police;
            public PlayerRoleIdentity Thief;
            public LootCarrier Carrier;
            public ThiefLootWallet Wallet;

            public static MatchSceneRefs Collect()
            {
                var refs = new MatchSceneRefs
                {
                    Runtime =
                        Object.FindFirstObjectByType<MatchRuntimeState>(),
                    EndController =
                        Object.FindFirstObjectByType<MatchEndController>(),
                    Sensor =
                        Object.FindFirstObjectByType<ArrestRangeSensor>(),
                    Progress =
                        Object.FindFirstObjectByType<
                            ArrestProgressController>(),
                    Loot = Object.FindFirstObjectByType<LootItem>(),
                    SaleZone =
                        Object.FindFirstObjectByType<LootSaleZone>()
                };

                foreach (PlayerRoleIdentity identity in
                    Object.FindObjectsByType<PlayerRoleIdentity>(
                        FindObjectsSortMode.None))
                {
                    if (identity.Role == PlayerRole.Police)
                    {
                        refs.Police = identity;
                    }
                    else
                    {
                        refs.Thief = identity;
                    }
                }

                Assert.That(refs.Runtime, Is.Not.Null);
                Assert.That(refs.EndController, Is.Not.Null);
                Assert.That(refs.Sensor, Is.Not.Null);
                Assert.That(refs.Progress, Is.Not.Null);
                Assert.That(refs.Loot, Is.Not.Null);
                Assert.That(refs.SaleZone, Is.Not.Null);
                Assert.That(refs.Police, Is.Not.Null);
                Assert.That(refs.Thief, Is.Not.Null);

                refs.Carrier = refs.Thief.GetComponent<LootCarrier>();
                refs.Wallet = refs.Thief.GetComponent<ThiefLootWallet>();
                Assert.That(refs.Carrier, Is.Not.Null);
                Assert.That(refs.Wallet, Is.Not.Null);
                return refs;
            }
        }
    }
}
