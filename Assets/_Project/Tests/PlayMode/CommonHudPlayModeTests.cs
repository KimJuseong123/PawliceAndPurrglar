using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class CommonHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator HudReflectsRuntimeWithoutChangingRules()
        {
            MatchConfig config =
                ScriptableObject.CreateInstance<MatchConfig>();
            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.SetActive(false);
            MatchRuntimeState runtime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            runtime.Configure(config, false);
            runtimeObject.SetActive(true);

            var player = new GameObject("Police");
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Police);
            PlayerKeyboardInput input =
                player.AddComponent<PlayerKeyboardInput>();
            var selectorObject = new GameObject("Selector");
            selectorObject.SetActive(false);
            LocalPlayerRoleSelector selector =
                selectorObject.AddComponent<LocalPlayerRoleSelector>();
            selector.Configure(
                new[] { new PlayerRoleControlBinding(identity, input) },
                null,
                PlayerRole.Police);
            selector.SelectRole(PlayerRole.Police);

            Text timer = CreateLabel("Timer");
            Text role = CreateLabel("Role");
            Text state = CreateLabel("State");
            Text interaction = CreateLabel("Interaction");
            var hudObject = new GameObject("HUD");
            CommonHudPresenter hud =
                hudObject.AddComponent<CommonHudPresenter>();
            hud.Configure(
                runtime,
                selector,
                timer,
                role,
                state,
                interaction);

            MatchState beforeRefresh = runtime.CurrentState;
            hud.Refresh();
            Assert.That(timer.text, Is.EqualTo("04:00"));
            Assert.That(role.text, Is.EqualTo("POLICE"));
            Assert.That(state.text, Is.EqualTo("LOBBY"));
            Assert.That(runtime.CurrentState, Is.EqualTo(beforeRefresh));

            runtime.BeginCountdown();
            runtime.Tick(1f);
            hud.Refresh();
            Assert.That(state.text, Does.StartWith("READY"));
            Assert.That(timer.text, Is.EqualTo("04:00"));

            Object.Destroy(hudObject);
            Object.Destroy(timer.gameObject);
            Object.Destroy(role.gameObject);
            Object.Destroy(state.gameObject);
            Object.Destroy(interaction.gameObject);
            Object.Destroy(selectorObject);
            Object.Destroy(player);
            Object.Destroy(runtimeObject);
            Object.Destroy(config);
            yield return null;
        }

        [TestCase(240f, "04:00")]
        [TestCase(61f, "01:01")]
        [TestCase(0f, "00:00")]
        [TestCase(-1f, "00:00")]
        public void TimeFormattingIsStable(
            float seconds,
            string expected)
        {
            Assert.That(
                CommonHudPresenter.FormatTime(seconds),
                Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator DuplicateHudDestroysSecondInstance()
        {
            var firstObject = new GameObject("First HUD");
            CommonHudPresenter first =
                firstObject.AddComponent<CommonHudPresenter>();
            var duplicateObject = new GameObject("Duplicate HUD");
            CommonHudPresenter duplicate =
                duplicateObject.AddComponent<CommonHudPresenter>();

            yield return null;

            Assert.That(CommonHudPresenter.Instance, Is.SameAs(first));
            Assert.That(duplicate == null, Is.True);

            Object.Destroy(firstObject);
            yield return null;
        }

        private static Text CreateLabel(string name)
        {
            var labelObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            return labelObject.GetComponent<Text>();
        }
    }
}
