using System;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Loot
{
    public sealed class LootSaleZone :
        MonoBehaviour,
        IPlayerInteractable,
        IScreenAnsweredInteractable
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
        /// <summary>
        /// What the key does now, which is open the ledger — not sell.
        ///
        /// It used to read "Sell carried loot" and that was true when the key sold
        /// the piece in your hands. The key opens a screen now, and a prompt that
        /// promises a sale on a press that does not make one is worse than no
        /// prompt: the player presses it, nothing they were told about happens,
        /// and the window that did open reads as something that went wrong.
        /// </summary>
        public string Prompt => "너구리 암시장 열기";
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
