using System;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    public sealed class ThiefHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private ThiefLootWallet wallet;

        [SerializeField]
        private LootCarrier carrier;

        [SerializeField]
        private PlayerMovementMotor movementMotor;

        [SerializeField]
        private PlayerInteractionScanner interactionScanner;

        [SerializeField]
        private LootConfig lootConfig;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Text amountLabel;

        [SerializeField]
        private Text heldLootLabel;

        [SerializeField]
        private Text lootPriceLabel;

        [SerializeField]
        private Text movementPenaltyLabel;

        [SerializeField]
        private Text saleAvailabilityLabel;

        public GameObject Panel => panel;

        public void Configure(
            LocalPlayerRoleSelector selector,
            ThiefLootWallet configuredWallet,
            LootCarrier configuredCarrier,
            PlayerMovementMotor configuredMovementMotor,
            PlayerInteractionScanner configuredInteractionScanner,
            LootConfig configuredLootConfig,
            GameObject configuredPanel,
            Text configuredAmountLabel,
            Text configuredHeldLootLabel,
            Text configuredLootPriceLabel,
            Text configuredMovementPenaltyLabel,
            Text configuredSaleAvailabilityLabel)
        {
            roleSelector = selector;
            wallet = configuredWallet;
            carrier = configuredCarrier;
            movementMotor = configuredMovementMotor;
            interactionScanner = configuredInteractionScanner;
            lootConfig = configuredLootConfig;
            panel = configuredPanel;
            amountLabel = configuredAmountLabel;
            heldLootLabel = configuredHeldLootLabel;
            lootPriceLabel = configuredLootPriceLabel;
            movementPenaltyLabel =
                configuredMovementPenaltyLabel;
            saleAvailabilityLabel =
                configuredSaleAvailabilityLabel;
            ValidateOrThrow();
            Refresh();
        }

        public void Refresh()
        {
            if (panel == null || roleSelector == null)
            {
                return;
            }

            bool isThief =
                roleSelector.ActiveRole == PlayerRole.Thief;
            panel.SetActive(isThief);
            if (!isThief)
            {
                return;
            }

            LootItem heldLoot = carrier.HeldLoot;
            amountLabel.text =
                $"GOLD  {wallet.SoldAmount} / {wallet.TargetAmount}";
            heldLootLabel.text = heldLoot != null
                ? $"LOOT  {heldLoot.Definition.DisplayName}"
                : "LOOT  EMPTY";
            lootPriceLabel.text = heldLoot != null
                ? $"VALUE  {heldLoot.Definition.GetPrice(lootConfig)}"
                : "VALUE  -";

            int penaltyPercent = Mathf.RoundToInt(
                (1f - movementMotor.MovementSpeedMultiplier)
                * 100f);
            movementPenaltyLabel.text = penaltyPercent > 0
                ? $"MOVE  -{penaltyPercent}%"
                : "MOVE  NORMAL";
            saleAvailabilityLabel.text = CanSellNow(heldLoot)
                ? "SALE  READY"
                : "SALE  UNAVAILABLE";
        }

        public void ValidateOrThrow()
        {
            if (roleSelector == null
                || wallet == null
                || carrier == null
                || movementMotor == null
                || interactionScanner == null
                || lootConfig == null
                || panel == null
                || amountLabel == null
                || heldLootLabel == null
                || lootPriceLabel == null
                || movementPenaltyLabel == null
                || saleAvailabilityLabel == null)
            {
                throw new InvalidOperationException(
                    $"ThiefHudPresenter '{name}' has missing references.");
            }

            lootConfig.ValidateOrThrow();
        }

        private bool CanSellNow(LootItem heldLoot)
        {
            return heldLoot != null
                && interactionScanner.CurrentTarget
                    is LootSaleZone saleZone
                && saleZone.Contains(carrier.transform.position);
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void Update()
        {
            Refresh();
        }
    }
}
