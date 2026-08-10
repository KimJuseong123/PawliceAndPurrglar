using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using PawliceAndPurrglar.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class ThiefHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator HudReadsThiefLootLoopAndHidesForPolice()
        {
            var state = new ActiveMatchState();
            PlayerFixture thief = CreateThief(state);
            PlayerRoleControlBinding police =
                CreatePoliceBinding();
            LocalPlayerRoleSelector selector =
                CreateSelector(police, thief.Binding);
            LootItem loot = CreateLoot();
            LootSaleZone zone =
                CreateSaleZone(state, out LootConfig lootConfig);
            Text amount = CreateLabel("Amount");
            Text held = CreateLabel("Held");
            Text price = CreateLabel("Price");
            Text penalty = CreateLabel("Penalty");
            Text sale = CreateLabel("Sale");
            var panel = new GameObject("Thief HUD Panel");
            var presenterObject = new GameObject("Thief HUD Presenter");
            presenterObject.SetActive(false);
            ThiefHudPresenter presenter =
                presenterObject.AddComponent<ThiefHudPresenter>();
            presenter.Configure(
                selector,
                thief.Wallet,
                thief.Carrier,
                thief.Motor,
                thief.Scanner,
                lootConfig,
                panel,
                amount,
                held,
                price,
                penalty,
                sale);
            presenterObject.SetActive(true);
            selector.SelectRole(PlayerRole.Thief);
            presenter.Refresh();

            Assert.That(panel.activeSelf, Is.True);
            Assert.That(amount.text, Is.EqualTo("GOLD  0 / 1000"));
            Assert.That(held.text, Is.EqualTo("LOOT  EMPTY"));
            Assert.That(price.text, Is.EqualTo("VALUE  -"));
            Assert.That(penalty.text, Is.EqualTo("MOVE  NORMAL"));
            Assert.That(sale.text, Is.EqualTo("SALE  UNAVAILABLE"));

            Assert.That(thief.Carrier.TryAcquire(loot), Is.True);
            Physics.SyncTransforms();
            thief.Scanner.RefreshTarget();
            presenter.Refresh();
            Assert.That(
                held.text,
                Is.EqualTo("LOOT  HUD Test Loot"));
            Assert.That(price.text, Is.EqualTo("VALUE  40"));
            Assert.That(penalty.text, Is.EqualTo("MOVE  -10%"));
            Assert.That(sale.text, Is.EqualTo("SALE  READY"));

            Assert.That(
                zone.TryInteract(
                    new PlayerInteractionContext(thief.Identity)),
                Is.True);
            int amountBeforeRefresh = thief.Wallet.SoldAmount;
            presenter.Refresh();
            Assert.That(thief.Wallet.SoldAmount, Is.EqualTo(amountBeforeRefresh));
            Assert.That(amount.text, Is.EqualTo("GOLD  40 / 1000"));
            Assert.That(held.text, Is.EqualTo("LOOT  EMPTY"));
            Assert.That(price.text, Is.EqualTo("VALUE  -"));
            Assert.That(penalty.text, Is.EqualTo("MOVE  NORMAL"));
            Assert.That(sale.text, Is.EqualTo("SALE  UNAVAILABLE"));

            selector.SelectRole(PlayerRole.Police);
            presenter.Refresh();
            Assert.That(panel.activeSelf, Is.False);

            DestroyTestObjects(
                thief,
                police,
                selector,
                loot,
                zone,
                lootConfig,
                presenterObject,
                panel,
                amount,
                held,
                price,
                penalty,
                sale);
            yield return null;
        }

        private static PlayerFixture CreateThief(
            IMatchStateReader state)
        {
            var root = new GameObject("Thief Player");
            root.SetActive(false);
            CharacterController controller =
                root.AddComponent<CharacterController>();
            PlayerConfig playerConfig =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerMovementMotor motor =
                root.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, playerConfig, state, null);
            PlayerRoleIdentity identity =
                root.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Thief);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(root.transform, false);
            LootCarrier carrier = root.AddComponent<LootCarrier>();
            carrier.Configure(identity, state, carryPoint.transform);
            LootCarryMovementPenalty carryPenalty =
                root.AddComponent<LootCarryMovementPenalty>();
            carryPenalty.Configure(carrier, motor);
            PlayerInteractionScanner scanner =
                root.AddComponent<PlayerInteractionScanner>();
            scanner.Configure(identity, playerConfig, state);
            PlayerKeyboardInput input =
                root.AddComponent<PlayerKeyboardInput>();
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            ThiefLootWallet wallet =
                root.AddComponent<ThiefLootWallet>();
            wallet.Configure(identity, matchConfig);
            root.SetActive(true);
            var binding = new PlayerRoleControlBinding(
                identity,
                input,
                scanner);
            return new PlayerFixture(
                root,
                playerConfig,
                matchConfig,
                identity,
                motor,
                carrier,
                scanner,
                wallet,
                binding);
        }

        private static PlayerRoleControlBinding CreatePoliceBinding()
        {
            var police = new GameObject("Police Player");
            PlayerRoleIdentity identity =
                police.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Police);
            PlayerKeyboardInput input =
                police.AddComponent<PlayerKeyboardInput>();
            return new PlayerRoleControlBinding(identity, input);
        }

        private static LocalPlayerRoleSelector CreateSelector(
            params PlayerRoleControlBinding[] bindings)
        {
            var selectorObject = new GameObject("Role Selector");
            selectorObject.SetActive(false);
            LocalPlayerRoleSelector selector =
                selectorObject.AddComponent<LocalPlayerRoleSelector>();
            selector.Configure(bindings, null, PlayerRole.Police);
            selector.SelectRole(PlayerRole.Police);
            selectorObject.SetActive(true);
            return selector;
        }

        private static LootItem CreateLoot()
        {
            var lootObject = new GameObject("Loot");
            lootObject.SetActive(false);
            lootObject.AddComponent<BoxCollider>();
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                "hud-test-loot",
                "HUD Test Loot",
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
        }

        private static LootSaleZone CreateSaleZone(
            IMatchStateReader state,
            out LootConfig lootConfig)
        {
            var zoneObject = new GameObject("Sale Zone");
            zoneObject.SetActive(false);
            BoxCollider area = zoneObject.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.size = new Vector3(4f, 4f, 4f);
            lootConfig = ScriptableObject.CreateInstance<LootConfig>();
            LootSaleZone zone =
                zoneObject.AddComponent<LootSaleZone>();
            zone.Configure(area, lootConfig, state);
            zoneObject.SetActive(true);
            return zone;
        }

        private static Text CreateLabel(string name)
        {
            var labelObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            return labelObject.GetComponent<Text>();
        }

        private static void DestroyTestObjects(
            PlayerFixture thief,
            PlayerRoleControlBinding police,
            LocalPlayerRoleSelector selector,
            LootItem loot,
            LootSaleZone zone,
            LootConfig lootConfig,
            GameObject presenterObject,
            GameObject panel,
            params Text[] labels)
        {
            Object.Destroy(loot.Definition);
            Object.Destroy(loot.gameObject);
            Object.Destroy(zone.gameObject);
            Object.Destroy(lootConfig);
            Object.Destroy(thief.PlayerConfig);
            Object.Destroy(thief.MatchConfig);
            Object.Destroy(thief.Root);
            Object.Destroy(police.Identity.gameObject);
            Object.Destroy(selector.gameObject);
            Object.Destroy(presenterObject);
            Object.Destroy(panel);
            foreach (Text label in labels)
            {
                Object.Destroy(label.gameObject);
            }
        }

        private sealed class ActiveMatchState : IMatchStateReader
        {
            public MatchState CurrentState => MatchState.Playing;
            public bool IsGameplayActive => true;
        }

        private readonly struct PlayerFixture
        {
            public PlayerFixture(
                GameObject root,
                PlayerConfig playerConfig,
                MatchConfig matchConfig,
                PlayerRoleIdentity identity,
                PlayerMovementMotor motor,
                LootCarrier carrier,
                PlayerInteractionScanner scanner,
                ThiefLootWallet wallet,
                PlayerRoleControlBinding binding)
            {
                Root = root;
                PlayerConfig = playerConfig;
                MatchConfig = matchConfig;
                Identity = identity;
                Motor = motor;
                Carrier = carrier;
                Scanner = scanner;
                Wallet = wallet;
                Binding = binding;
            }

            public GameObject Root { get; }
            public PlayerConfig PlayerConfig { get; }
            public MatchConfig MatchConfig { get; }
            public PlayerRoleIdentity Identity { get; }
            public PlayerMovementMotor Motor { get; }
            public LootCarrier Carrier { get; }
            public PlayerInteractionScanner Scanner { get; }
            public ThiefLootWallet Wallet { get; }
            public PlayerRoleControlBinding Binding { get; }
        }
    }
}
