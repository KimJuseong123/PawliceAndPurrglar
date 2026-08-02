using System;
using System.Collections.Generic;
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
        private readonly HashSet<LootRequestId> _completedRequests =
            new();
        private ulong _nextLocalRequestValue = 1;

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
            _completedRequests.Clear();
            _nextLocalRequestValue = 1;
        }

        public bool TryAcquire(LootItem loot)
        {
            return TryAcquire(loot, CreateLocalRequestId());
        }

        public bool TryAcquire(
            LootItem loot,
            LootRequestId requestId)
        {
            ValidateOrThrow();
            if (!requestId.IsValid
                || _completedRequests.Contains(requestId)
                || loot == null
                || identity == null
                || identity.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || HeldLoot != null
                || !loot.TryAcquire(this, carryPoint))
            {
                return false;
            }

            LootItem previous = HeldLoot;
            Vector3 liftedFrom = loot.transform.position;
            HeldLoot = loot;
            _completedRequests.Add(requestId);
            HeldLootChanged?.Invoke(previous, HeldLoot);
            RaiseAlarmIfWatched(loot, liftedFrom);
            return true;
        }

        /// <summary>
        /// Sounds the shop's alarm if this piece is one of the watched ones.
        ///
        /// Done on acquisition rather than at the case, because the two are not
        /// the same moment: the glass going is loud, and the ring leaving its
        /// cushion is what the shop is actually wired to notice. A thief who
        /// breaks a case and takes nothing has made a noise; a thief who takes
        /// the ring has set off an alarm.
        ///
        /// The position is where the piece was, not where the thief is. The
        /// mark on the officer's screen should point at the empty cushion —
        /// pointing it at the thief would make the alarm a tracker, and there
        /// is a separate, shorter reveal for that.
        /// </summary>
        private void RaiseAlarmIfWatched(LootItem loot, Vector3 liftedFrom)
        {
            if (loot.Definition == null || !loot.Definition.RaisesAlarm)
            {
                return;
            }

            FindFirstObjectByType<LootAlarm>()?.Raise(liftedFrom, identity);
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

        /// <summary>
        /// LOOT-005. Hands the carried loot to a hiding spot's stash.
        /// Rejected outside a match, for the wrong role, or with empty hands.
        /// </summary>
        public bool TryHide(LootItem loot, Transform stashRoot)
        {
            ValidateOrThrow();
            if (identity.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || HeldLoot == null
                || loot != HeldLoot
                || stashRoot == null)
            {
                return false;
            }

            LootItem previous = HeldLoot;
            if (!previous.TryHide(this, stashRoot))
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
            return TrySell(
                wallet,
                lootConfig,
                CreateLocalRequestId());
        }

        public bool TrySell(
            ThiefLootWallet wallet,
            LootConfig lootConfig,
            LootRequestId requestId)
        {
            ValidateOrThrow();
            if (!requestId.IsValid
                || _completedRequests.Contains(requestId)
                || identity.Role != PlayerRole.Thief
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
            if (!wallet.CanRecordSale(
                    soldLoot,
                    price,
                    requestId)
                || !soldLoot.TrySell(this))
            {
                return false;
            }

            HeldLoot = null;
            _completedRequests.Add(requestId);
            HeldLootChanged?.Invoke(soldLoot, null);
            wallet.RecordSale(soldLoot, price, requestId);
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

        private LootRequestId CreateLocalRequestId()
        {
            ulong value = _nextLocalRequestValue++;
            if (_nextLocalRequestValue == 0)
            {
                _nextLocalRequestValue = 1;
            }

            return new LootRequestId(value);
        }
    }
}
