using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using PawliceAndPurrglar.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class RoleObjectivePlayModeTests
    {
        [UnityTest]
        public IEnumerator ObjectiveFollowsRoleAndExpiresAfterStart()
        {
            MatchConfig config =
                ScriptableObject.CreateInstance<MatchConfig>();
            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.SetActive(false);
            MatchRuntimeState runtime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            runtime.Configure(config, false);
            runtimeObject.SetActive(true);

            LocalPlayerRoleSelector selector =
                CreateSelector(out GameObject selectorObject);
            var panel = new GameObject("Objective Panel");
            Text label = new GameObject(
                "Objective Label",
                typeof(RectTransform),
                typeof(Text)).GetComponent<Text>();
            var presenterObject = new GameObject("Objective Presenter");
            RoleObjectivePresenter presenter =
                presenterObject.AddComponent<RoleObjectivePresenter>();
            presenter.Configure(
                runtime,
                selector,
                panel,
                label,
                4f);

            MatchState beforeRefresh = runtime.CurrentState;
            presenter.Refresh(0f);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(runtime.CurrentState, Is.EqualTo(beforeRefresh));

            runtime.BeginCountdown();
            presenter.Refresh(0f);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(label.text, Does.StartWith("POLICE:"));

            selector.SelectRole(PlayerRole.Thief);
            presenter.Refresh(0f);
            Assert.That(label.text, Does.StartWith("THIEF:"));

            runtime.Tick(config.ReadyCountdownSeconds);
            presenter.Refresh(0f);
            Assert.That(panel.activeSelf, Is.True);
            presenter.Refresh(4.1f);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Playing));

            Object.Destroy(presenterObject);
            Object.Destroy(label.gameObject);
            Object.Destroy(panel);
            Object.Destroy(selectorObject);
            Object.Destroy(runtimeObject);
            Object.Destroy(config);
            yield return null;
        }

        [TestCase(PlayerRole.Police)]
        [TestCase(PlayerRole.Thief)]
        public void ObjectiveUsesAtMostThreeSentences(PlayerRole role)
        {
            string objective = RoleObjectivePresenter.GetObjective(role);
            int sentenceCount = objective.Split(
                new[] { '.', '!', '?' },
                System.StringSplitOptions.RemoveEmptyEntries).Length;

            Assert.That(sentenceCount, Is.LessThanOrEqualTo(3));
        }

        private static LocalPlayerRoleSelector CreateSelector(
            out GameObject selectorObject)
        {
            var police = new GameObject("Police");
            PlayerRoleIdentity policeIdentity =
                police.AddComponent<PlayerRoleIdentity>();
            policeIdentity.Configure(PlayerRole.Police);
            PlayerKeyboardInput policeInput =
                police.AddComponent<PlayerKeyboardInput>();

            var thief = new GameObject("Thief");
            PlayerRoleIdentity thiefIdentity =
                thief.AddComponent<PlayerRoleIdentity>();
            thiefIdentity.Configure(PlayerRole.Thief);
            PlayerKeyboardInput thiefInput =
                thief.AddComponent<PlayerKeyboardInput>();

            selectorObject = new GameObject("Selector");
            selectorObject.SetActive(false);
            LocalPlayerRoleSelector selector =
                selectorObject.AddComponent<LocalPlayerRoleSelector>();
            selector.Configure(
                new[]
                {
                    new PlayerRoleControlBinding(
                        policeIdentity,
                        policeInput),
                    new PlayerRoleControlBinding(
                        thiefIdentity,
                        thiefInput)
                },
                null,
                PlayerRole.Police);
            selector.SelectRole(PlayerRole.Police);
            police.transform.SetParent(selectorObject.transform);
            thief.transform.SetParent(selectorObject.transform);
            return selector;
        }
    }
}
