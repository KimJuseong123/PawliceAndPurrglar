using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    public sealed class LootCarrier : MonoBehaviour
    {
        private const float DropForwardDistance = 1.25f;

        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Transform carryPoint;

        private IMatchStateReader _matchState;

        public event Action<LootItem, LootItem> HeldLootChanged;

        public LootItem HeldLoot { get; private set; }
        public bool HasLoot => HeldLoot != null;
        public Transform CarryPoint => carryPoint;

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            IMatchStateReader configuredMatchState,
            Transform configuredCarryPoint)
        {
            identity = configuredIdentity;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            carryPoint = configuredCarryPoint;
        }

        public bool TryAcquire(LootItem loot)
        {
            ValidateOrThrow();
            if (loot == null
                || identity == null
                || identity.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || HeldLoot != null
                || !loot.TryAcquire(this, carryPoint))
            {
                return false;
            }

            LootItem previous = HeldLoot;
            HeldLoot = loot;
            HeldLootChanged?.Invoke(previous, HeldLoot);
            return true;
        }

        public bool TryDrop()
        {
            ValidateOrThrow();
            if (identity.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || HeldLoot == null)
            {
                return false;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 requestedPosition =
                transform.position
                + forward.normalized * DropForwardDistance;
            if (!LootGroundPlacement.TryFindSurface(
                    requestedPosition,
                    transform,
                    out Vector3 surfacePosition))
            {
                return false;
            }

            LootItem previous = HeldLoot;
            Vector3 dropPosition = surfacePosition
                + Vector3.up * previous.WorldClearance;
            if (!previous.TryDrop(this, dropPosition))
            {
                return false;
            }

            HeldLoot = null;
            HeldLootChanged?.Invoke(previous, null);
            return true;
        }

        public bool TrySell(
            ThiefLootWallet wallet,
            LootConfig lootConfig)
        {
            ValidateOrThrow();
            if (identity.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || HeldLoot == null
                || wallet == null
                || lootConfig == null)
            {
                return false;
            }

            lootConfig.ValidateOrThrow();
            LootItem soldLoot = HeldLoot;
            int price = soldLoot.Definition.GetPrice(lootConfig);
            if (!wallet.CanRecordSale(soldLoot, price)
                || !soldLoot.TrySell(this))
            {
                return false;
            }

            HeldLoot = null;
            HeldLootChanged?.Invoke(soldLoot, null);
            wallet.RecordSale(soldLoot, price);
            return true;
        }

        public void ValidateOrThrow()
        {
            if (identity == null)
            {
                throw new InvalidOperationException(
                    $"LootCarrier '{name}' requires PlayerRoleIdentity.");
            }

            if (carryPoint == null || !carryPoint.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    $"LootCarrier '{name}' requires a CarryPoint under PlayerRoot.");
            }
        }

        internal void HandleLootUnavailable(LootItem loot)
        {
            if (loot == null || HeldLoot != loot)
            {
                return;
            }

            LootItem previous = HeldLoot;
            HeldLoot = null;
            HeldLootChanged?.Invoke(previous, null);
        }

        private void OnDisable()
        {
            if (HeldLoot == null)
            {
                return;
            }

            LootItem previous = HeldLoot;
            if (previous.TryReleaseFromUnavailableCarrier(this))
            {
                HeldLoot = null;
                HeldLootChanged?.Invoke(previous, null);
            }
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
