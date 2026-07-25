using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    public sealed class LootSaleZone : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private BoxCollider saleArea;

        [SerializeField]
        private LootConfig lootConfig;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;

        public Transform InteractionTransform => transform;
        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Sale;
        public string Prompt => "Sell carried loot";
        public bool IsAvailable =>
            isActiveAndEnabled
            && saleArea != null
            && IsGameplayActive();

        public void Configure(
            BoxCollider configuredSaleArea,
            LootConfig configuredLootConfig,
            IMatchStateReader configuredMatchState)
        {
            saleArea = configuredSaleArea;
            lootConfig = configuredLootConfig;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        public bool Contains(Vector3 worldPosition)
        {
            return saleArea != null
                && saleArea.bounds.Contains(worldPosition);
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null
                || context.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || !Contains(context.PlayerTransform.position))
            {
                return false;
            }

            LootCarrier carrier =
                context.Player.GetComponent<LootCarrier>();
            ThiefLootWallet wallet =
                context.Player.GetComponent<ThiefLootWallet>();
            return carrier != null
                && wallet != null
                && carrier.TrySell(wallet, lootConfig);
        }

        public void ValidateOrThrow()
        {
            if (saleArea == null
                || saleArea.transform != transform
                || !saleArea.isTrigger)
            {
                throw new InvalidOperationException(
                    $"LootSaleZone '{name}' requires a trigger BoxCollider on its root.");
            }

            if (lootConfig == null)
            {
                throw new InvalidOperationException(
                    $"LootSaleZone '{name}' requires LootConfig.");
            }

            lootConfig.ValidateOrThrow();
            if (_matchState == null && matchStateSource == null)
            {
                throw new InvalidOperationException(
                    $"LootSaleZone '{name}' requires a match state source.");
            }
        }

        private void Awake()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            ValidateOrThrow();
        }

        private bool IsGameplayActive()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            return _matchState?.IsGameplayActive == true;
        }
    }
}
