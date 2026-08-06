using System;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// LOOT-005. A designated place where carried loot may be hidden.
    ///
    /// Hiding is only possible at one of these, never anywhere on the map, so
    /// the thief has to commit to a location the police can learn. Sold loot is
    /// terminal and can never be hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootHidingSpot : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private Collider area;

        [SerializeField]
        private Transform storedRoot;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private Match.IMatchStateReader _matchState;

        public event Action<LootItem> LootHidden;
        public event Action<LootItem> LootRecovered;

        public LootItem StoredLoot { get; private set; }
        public bool HasStoredLoot => StoredLoot != null;

        public Transform InteractionTransform => transform;
        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Loot;
        public string Prompt => HasStoredLoot
            ? "Recover hidden loot"
            : "Hide carried loot";
        public bool IsAvailable =>
            isActiveAndEnabled
            && ResolveMatchState()?.IsGameplayActive == true;

        public void Configure(
            Collider configuredArea,
            Transform configuredStoredRoot,
            Match.IMatchStateReader matchStateReader)
        {
            area = configuredArea;
            storedRoot = configuredStoredRoot;
            _matchState = matchStateReader;
            matchStateSource = matchStateReader as MonoBehaviour;
            StoredLoot = null;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (area == null || storedRoot == null)
            {
                throw new InvalidOperationException(
                    $"LootHidingSpot '{name}' requires an area and a stored "
                    + "root.");
            }

            if (ResolveMatchState() == null)
            {
                throw new InvalidOperationException(
                    $"LootHidingSpot '{name}' requires a match state source.");
            }
        }

        public bool Contains(Vector3 worldPosition)
        {
            return area != null
                && area.bounds.Contains(
                    new Vector3(
                        worldPosition.x,
                        area.bounds.center.y,
                        worldPosition.z));
        }

        /// <summary>
        /// Hides the carried loot, or recovers what is already stored. One
        /// interact key serves both directions so the spot reads as a stash.
        /// </summary>
        public bool TryInteract(PlayerInteractionContext context)
        {
            if (!IsAvailable
                || context.Player == null
                || context.Player.Role != PlayerRole.Thief
                || !Contains(context.Player.transform.position))
            {
                return false;
            }

            LootCarrier carrier =
                context.Player.GetComponent<LootCarrier>();
            if (carrier == null)
            {
                return false;
            }

            return HasStoredLoot
                ? TryRecover(carrier)
                : TryHide(carrier);
        }

        public bool TryHide(LootCarrier carrier)
        {
            if (carrier == null
                || !carrier.HasLoot
                || HasStoredLoot)
            {
                return false;
            }

            LootItem loot = carrier.HeldLoot;
            if (loot.CurrentState == LootState.Sold)
            {
                return false;
            }

            if (!carrier.TryHide(loot, storedRoot))
            {
                return false;
            }

            StoredLoot = loot;
            LootHidden?.Invoke(loot);
            return true;
        }

        public bool TryRecover(LootCarrier carrier)
        {
            // Refused only when the bag is full. It used to be refused whenever
            // the thief held anything at all, which was the same sentence back
            // when the thief could hold one thing — now it would mean a stash can
            // never be emptied by somebody who is already carrying.
            if (carrier == null || !HasStoredLoot || !carrier.CanCarryMore)
            {
                return false;
            }

            LootItem loot = StoredLoot;
            if (!carrier.TryAcquire(loot))
            {
                return false;
            }

            StoredLoot = null;
            LootRecovered?.Invoke(loot);
            return true;
        }

        private Match.IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState =
                    matchStateSource as Match.IMatchStateReader;
            }

            return _matchState;
        }

        private void Awake()
        {
            ValidateOrThrow();
        }
    }
}
