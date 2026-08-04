using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class PoliceHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator HudShowsPoliceMatchLootArrestAndAlerts()
        {
            PoliceHudFixture fixture = CreateFixture();
            fixture.Presenter.Refresh();

            Assert.That(fixture.Panel.activeSelf, Is.True);
            Assert.That(fixture.Time.text, Is.EqualTo("TIME  03:50"));
            Assert.That(
                fixture.ThiefGold.text,
                Is.EqualTo("THIEF GOLD  0 / 1000"));
            Assert.That(
                fixture.Arrest.text,
                Is.EqualTo("ARREST  0%"));
            Assert.That(
                fixture.TheftAlert.text,
                Is.EqualTo("THEFT ALERT  CLEAR"));
            Assert.That(
                fixture.Goal.text,
                Is.EqualTo(
                    "GOAL  ARREST THIEF OR STOP 1000 GOLD"));

            Assert.That(
                fixture.Carrier.TryAcquire(fixture.Loot),
                Is.True);
            fixture.Progress.Tick(
                fixture.ArrestConfig.ArrestDurationSeconds * 0.5f);
            fixture.Presenter.Refresh();
            Assert.That(
                fixture.Arrest.text,
                Is.EqualTo("ARREST  50%"));
            Assert.That(
                fixture.TheftAlert.text,
                Is.EqualTo("THEFT ALERT  LOOT IN TRANSIT"));

            Assert.That(
                fixture.Carrier.TrySell(
                    fixture.Wallet,
                    fixture.LootConfig),
                Is.True);
            fixture.Presenter.Refresh();
            Assert.That(
                fixture.ThiefGold.text,
                Is.EqualTo("THIEF GOLD  200 / 1000"));
            Assert.That(
                fixture.TheftAlert.text,
                Is.EqualTo("THEFT ALERT  SALE +200"));

            float timeBeforeRefresh =
                fixture.MatchRuntime.RemainingMatchSeconds;
            float arrestBeforeRefresh =
                fixture.Progress.ProgressSeconds;
            int goldBeforeRefresh = fixture.Wallet.SoldAmount;
            fixture.Presenter.Refresh();
            Assert.That(
                fixture.MatchRuntime.RemainingMatchSeconds,
                Is.EqualTo(timeBeforeRefresh));
            Assert.That(
                fixture.Progress.ProgressSeconds,
                Is.EqualTo(arrestBeforeRefresh));
            Assert.That(
                fixture.Wallet.SoldAmount,
                Is.EqualTo(goldBeforeRefresh));

            fixture.Selector.SelectRole(PlayerRole.Thief);
            fixture.Presenter.Refresh();
            Assert.That(fixture.Panel.activeSelf, Is.False);

            DestroyFixture(fixture);
            yield return null;
        }

        private static PoliceHudFixture CreateFixture()
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
            matchRuntime.Tick(10f);

            GameObject police = CreateBasicPlayer(
                "Police",
                PlayerRole.Police);
            GameObject thief = CreateBasicPlayer(
                "Thief",
                PlayerRole.Thief);
            thief.transform.position = new Vector3(1f, 0f, 0f);

            LootConfig lootConfig =
                ScriptableObject.CreateInstance<LootConfig>();
            ArrestConfig arrestConfig =
                ScriptableObject.CreateInstance<ArrestConfig>();
            thief.SetActive(false);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(thief.transform, false);
            PlayerRoleIdentity thiefIdentity =
                thief.GetComponent<PlayerRoleIdentity>();
            LootCarrier carrier = thief.AddComponent<LootCarrier>();
            carrier.Configure(
                thiefIdentity,
                matchRuntime,
                carryPoint.transform);
            ThiefLootWallet wallet =
                thief.AddComponent<ThiefLootWallet>();
            wallet.Configure(thiefIdentity, matchConfig);
            thief.SetActive(true);

            police.SetActive(false);
            ArrestRangeSensor sensor =
                police.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                police.GetComponent<PlayerRoleIdentity>(),
                thiefIdentity,
                arrestConfig,
                Physics.AllLayers);
            ArrestProgressController progress =
                police.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, matchRuntime, arrestConfig);
            police.SetActive(true);
            Physics.SyncTransforms();
            sensor.Evaluate();

            LocalPlayerRoleSelector selector =
                CreateSelector(police, thief);
            LootItem loot = CreateLoot();
            var panel = new GameObject("Police HUD Panel");
            Text time = CreateLabel("Time");
            Text thiefGold = CreateLabel("Thief Gold");
            Text arrest = CreateLabel("Arrest");
            Text theftAlert = CreateLabel("Theft Alert");
            Text goal = CreateLabel("Goal");
            var presenterObject =
                new GameObject("Police HUD Presenter");
            presenterObject.SetActive(false);
            PoliceHudPresenter presenter =
                presenterObject.AddComponent<PoliceHudPresenter>();
            presenter.Configure(
                selector,
                matchRuntime,
                wallet,
                carrier,
                progress,
                panel,
                time,
                thiefGold,
                arrest,
                theftAlert,
                goal);
            presenterObject.SetActive(true);

            return new PoliceHudFixture(
                runtimeObject,
                police,
                thief,
                panel,
                presenterObject,
                matchConfig,
                lootConfig,
                arrestConfig,
                matchRuntime,
                selector,
                carrier,
                wallet,
                progress,
                loot,
                presenter,
                time,
                thiefGold,
                arrest,
                theftAlert,
                goal);
        }

        private static GameObject CreateBasicPlayer(
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
            // Activated first, then told which role.
            //
            // SelectRole was called while the object was still switched off, so
            // whatever Awake sets up had not run and the choice did not take.
            // The selector then reported the enum's default — the thief — and the
            // police panel switched itself off, which is exactly what a correctly
            // working presenter does when it is not the officer's screen.
            selectorObject.SetActive(true);
            selector.SelectRole(PlayerRole.Police);
            return selector;
        }

        private static PlayerRoleControlBinding CreateBinding(
            GameObject player)
        {
            return new PlayerRoleControlBinding(
                player.GetComponent<PlayerRoleIdentity>(),
                player.GetComponent<PlayerKeyboardInput>());
        }

        private static LootItem CreateLoot()
        {
            var lootObject = new GameObject("Loot");
            lootObject.SetActive(false);
            lootObject.AddComponent<BoxCollider>();
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(
                lootObject.transform,
                false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                "police-hud-loot",
                "Police HUD Loot",
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
        }

        private static Text CreateLabel(string name)
        {
            var labelObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            return labelObject.GetComponent<Text>();
        }

        private static void DestroyFixture(
            PoliceHudFixture fixture)
        {
            Object.Destroy(fixture.Loot.Definition);
            Object.Destroy(fixture.Loot.gameObject);
            Object.Destroy(fixture.MatchConfig);
            Object.Destroy(fixture.LootConfig);
            Object.Destroy(fixture.ArrestConfig);
            Object.Destroy(fixture.MatchRuntimeObject);
            Object.Destroy(fixture.Police);
            Object.Destroy(fixture.Thief);
            Object.Destroy(fixture.Selector.gameObject);
            Object.Destroy(fixture.PresenterObject);
            Object.Destroy(fixture.Panel);
            Object.Destroy(fixture.Time.gameObject);
            Object.Destroy(fixture.ThiefGold.gameObject);
            Object.Destroy(fixture.Arrest.gameObject);
            Object.Destroy(fixture.TheftAlert.gameObject);
            Object.Destroy(fixture.Goal.gameObject);
        }

        private readonly struct PoliceHudFixture
        {
            public PoliceHudFixture(
                GameObject matchRuntimeObject,
                GameObject police,
                GameObject thief,
                GameObject panel,
                GameObject presenterObject,
                MatchConfig matchConfig,
                LootConfig lootConfig,
                ArrestConfig arrestConfig,
                MatchRuntimeState matchRuntime,
                LocalPlayerRoleSelector selector,
                LootCarrier carrier,
                ThiefLootWallet wallet,
                ArrestProgressController progress,
                LootItem loot,
                PoliceHudPresenter presenter,
                Text time,
                Text thiefGold,
                Text arrest,
                Text theftAlert,
                Text goal)
            {
                MatchRuntimeObject = matchRuntimeObject;
                Police = police;
                Thief = thief;
                Panel = panel;
                PresenterObject = presenterObject;
                MatchConfig = matchConfig;
                LootConfig = lootConfig;
                ArrestConfig = arrestConfig;
                MatchRuntime = matchRuntime;
                Selector = selector;
                Carrier = carrier;
                Wallet = wallet;
                Progress = progress;
                Loot = loot;
                Presenter = presenter;
                Time = time;
                ThiefGold = thiefGold;
                Arrest = arrest;
                TheftAlert = theftAlert;
                Goal = goal;
            }

            public GameObject MatchRuntimeObject { get; }
            public GameObject Police { get; }
            public GameObject Thief { get; }
            public GameObject Panel { get; }
            public GameObject PresenterObject { get; }
            public MatchConfig MatchConfig { get; }
            public LootConfig LootConfig { get; }
            public ArrestConfig ArrestConfig { get; }
            public MatchRuntimeState MatchRuntime { get; }
            public LocalPlayerRoleSelector Selector { get; }
            public LootCarrier Carrier { get; }
            public ThiefLootWallet Wallet { get; }
            public ArrestProgressController Progress { get; }
            public LootItem Loot { get; }
            public PoliceHudPresenter Presenter { get; }
            public Text Time { get; }
            public Text ThiefGold { get; }
            public Text Arrest { get; }
            public Text TheftAlert { get; }
            public Text Goal { get; }
        }
    }
}
