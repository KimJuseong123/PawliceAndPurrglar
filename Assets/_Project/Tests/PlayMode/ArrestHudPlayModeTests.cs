using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class ArrestHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator HudShowsProgressInterruptionCompletionAndThiefWarning()
        {
            ArrestFixture fixture = CreateFixture();
            float progressBeforeRefresh =
                fixture.Progress.ProgressSeconds;
            fixture.Presenter.Refresh();
            Assert.That(
                fixture.Progress.ProgressSeconds,
                Is.EqualTo(progressBeforeRefresh));
            Assert.That(fixture.Panel.activeSelf, Is.True);
            Assert.That(
                fixture.Status.text,
                Is.EqualTo("ARREST READY"));
            Assert.That(fixture.Fill.fillAmount, Is.Zero);
            Assert.That(fixture.Warning.gameObject.activeSelf, Is.False);

            fixture.Progress.Tick(
                fixture.ArrestConfig.ArrestDurationSeconds * 0.5f);
            fixture.Presenter.Refresh();
            Assert.That(fixture.Fill.fillAmount, Is.EqualTo(0.5f));
            Assert.That(
                fixture.Status.text,
                Is.EqualTo("ARRESTING  50%"));

            fixture.Selector.SelectRole(PlayerRole.Thief);
            fixture.Presenter.Refresh();
            Assert.That(fixture.Panel.activeSelf, Is.True);
            Assert.That(
                fixture.Status.text,
                Is.EqualTo("DANGER  50%"));
            Assert.That(fixture.Warning.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Warning.text, Is.EqualTo("RUN!"));

            fixture.Thief.transform.position =
                new Vector3(3f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            fixture.Presenter.Refresh();
            Assert.That(fixture.Fill.fillAmount, Is.Zero);
            Assert.That(
                fixture.Status.text,
                Is.EqualTo("ARREST ESCAPED"));
            Assert.That(fixture.Warning.gameObject.activeSelf, Is.False);

            fixture.Thief.transform.position =
                new Vector3(1f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            fixture.Progress.Tick(
                fixture.ArrestConfig.ArrestDurationSeconds);
            Assert.That(
                fixture.Completion.TryCompleteArrest(),
                Is.True);
            Assert.That(fixture.Completion.CurrentCatchCount, Is.EqualTo(1));
            Assert.That(fixture.Completion.IsCompleted, Is.False);

            fixture.Progress.Tick(
                fixture.ArrestConfig.ArrestDurationSeconds);
            Assert.That(
                fixture.Completion.TryCompleteArrest(),
                Is.True);
            Assert.That(fixture.Completion.CurrentCatchCount, Is.EqualTo(2));
            Assert.That(fixture.Completion.IsCompleted, Is.False);

            fixture.Progress.Tick(
                fixture.ArrestConfig.ArrestDurationSeconds);
            Assert.That(
                fixture.Completion.TryCompleteArrest(),
                Is.True);
            Assert.That(fixture.Completion.CurrentCatchCount, Is.EqualTo(3));
            Assert.That(fixture.Completion.IsCompleted, Is.True);
            fixture.Presenter.Refresh();
            Assert.That(fixture.Fill.fillAmount, Is.EqualTo(1f));
            Assert.That(
                fixture.Status.text,
                Is.EqualTo("YOU WERE CAUGHT"));
            Assert.That(fixture.Warning.text, Is.EqualTo("CAUGHT"));

            fixture.Selector.SelectRole(PlayerRole.Police);
            fixture.Presenter.Refresh();
            Assert.That(
                fixture.Status.text,
                Is.EqualTo("ARREST COMPLETE"));
            Assert.That(fixture.Warning.gameObject.activeSelf, Is.False);

            DestroyFixture(fixture);
            yield return null;
        }

        private static ArrestFixture CreateFixture()
        {
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.SetActive(false);
            MatchRuntimeState matchRuntime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            matchRuntime.Configure(matchConfig, false);
            runtimeObject.SetActive(true);
            matchRuntime.BeginCountdown();
            matchRuntime.Tick(matchConfig.ReadyCountdownSeconds);

            GameObject police = CreatePlayer(
                "Police",
                PlayerRole.Police);
            GameObject thief = CreatePlayer(
                "Thief",
                PlayerRole.Thief);
            thief.transform.position = new Vector3(1f, 0f, 0f);
            ArrestConfig arrestConfig =
                ScriptableObject.CreateInstance<ArrestConfig>();

            police.SetActive(false);
            ArrestRangeSensor sensor =
                police.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                police.GetComponent<PlayerRoleIdentity>(),
                thief.GetComponent<PlayerRoleIdentity>(),
                arrestConfig,
                Physics.AllLayers);
            ArrestProgressController progress =
                police.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, matchRuntime, arrestConfig);
            ArrestCompletionController completion =
                police.AddComponent<ArrestCompletionController>();
            completion.Configure(progress, matchRuntime);
            police.SetActive(true);
            Physics.SyncTransforms();
            sensor.Evaluate();

            LocalPlayerRoleSelector selector =
                CreateSelector(police, thief);
            var panel = new GameObject("Arrest HUD Panel");
            Image fill = CreateImage("Progress Fill");
            Text status = CreateLabel("Status");
            Text warning = CreateLabel("Warning");
            var presenterObject =
                new GameObject("Arrest HUD Presenter");
            presenterObject.SetActive(false);
            ArrestHudPresenter presenter =
                presenterObject.AddComponent<ArrestHudPresenter>();
            presenter.Configure(
                selector,
                progress,
                completion,
                panel,
                fill,
                status,
                warning);
            presenterObject.SetActive(true);

            return new ArrestFixture(
                runtimeObject,
                police,
                thief,
                panel,
                presenterObject,
                matchConfig,
                arrestConfig,
                selector,
                sensor,
                progress,
                completion,
                presenter,
                fill,
                status,
                warning);
        }

        private static GameObject CreatePlayer(
            string name,
            PlayerRole role)
        {
            var player = new GameObject(name);
            player.SetActive(false);
            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.up;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            player.AddComponent<PlayerKeyboardInput>();
            player.SetActive(true);
            return player;
        }

        private static LocalPlayerRoleSelector CreateSelector(
            GameObject police,
            GameObject thief)
        {
            var selectorObject = new GameObject("Role Selector");
            selectorObject.SetActive(false);
            LocalPlayerRoleSelector selector =
                selectorObject.AddComponent<LocalPlayerRoleSelector>();
            selector.Configure(
                new[]
                {
                    CreateBinding(police),
                    CreateBinding(thief)
                },
                null,
                PlayerRole.Police);
            selector.SelectRole(PlayerRole.Police);
            selectorObject.SetActive(true);
            return selector;
        }

        private static PlayerRoleControlBinding CreateBinding(
            GameObject player)
        {
            return new PlayerRoleControlBinding(
                player.GetComponent<PlayerRoleIdentity>(),
                player.GetComponent<PlayerKeyboardInput>());
        }

        private static Image CreateImage(string name)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            return imageObject.GetComponent<Image>();
        }

        private static Text CreateLabel(string name)
        {
            var labelObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            return labelObject.GetComponent<Text>();
        }

        private static void DestroyFixture(ArrestFixture fixture)
        {
            Object.Destroy(fixture.MatchConfig);
            Object.Destroy(fixture.ArrestConfig);
            Object.Destroy(fixture.MatchRuntimeObject);
            Object.Destroy(fixture.Police);
            Object.Destroy(fixture.Thief);
            Object.Destroy(fixture.Selector.gameObject);
            Object.Destroy(fixture.PresenterObject);
            Object.Destroy(fixture.Panel);
            Object.Destroy(fixture.Fill.gameObject);
            Object.Destroy(fixture.Status.gameObject);
            Object.Destroy(fixture.Warning.gameObject);
        }

        private readonly struct ArrestFixture
        {
            public ArrestFixture(
                GameObject matchRuntimeObject,
                GameObject police,
                GameObject thief,
                GameObject panel,
                GameObject presenterObject,
                MatchConfig matchConfig,
                ArrestConfig arrestConfig,
                LocalPlayerRoleSelector selector,
                ArrestRangeSensor sensor,
                ArrestProgressController progress,
                ArrestCompletionController completion,
                ArrestHudPresenter presenter,
                Image fill,
                Text status,
                Text warning)
            {
                MatchRuntimeObject = matchRuntimeObject;
                Police = police;
                Thief = thief;
                Panel = panel;
                PresenterObject = presenterObject;
                MatchConfig = matchConfig;
                ArrestConfig = arrestConfig;
                Selector = selector;
                Sensor = sensor;
                Progress = progress;
                Completion = completion;
                Presenter = presenter;
                Fill = fill;
                Status = status;
                Warning = warning;
            }

            public GameObject MatchRuntimeObject { get; }
            public GameObject Police { get; }
            public GameObject Thief { get; }
            public GameObject Panel { get; }
            public GameObject PresenterObject { get; }
            public MatchConfig MatchConfig { get; }
            public ArrestConfig ArrestConfig { get; }
            public LocalPlayerRoleSelector Selector { get; }
            public ArrestRangeSensor Sensor { get; }
            public ArrestProgressController Progress { get; }
            public ArrestCompletionController Completion { get; }
            public ArrestHudPresenter Presenter { get; }
            public Image Fill { get; }
            public Text Status { get; }
            public Text Warning { get; }
        }
    }
}
