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
    public sealed class LootHidingSpot : MonoBehaviour, IPlayerInteractable,
        PawsAndLoot.Gameplay.Players.IRoleAwareInteractable
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
        /// <summary>
        /// Generic, not Loot, since the officer gained a move here.
        ///
        /// The permission table answers per type and <c>Loot</c> means thief-only,
        /// so an officer's scanner never offered the crate as a target and the
        /// key had nothing to act on — the same shape as the market being shut to
        /// them for a whole release. The role rules that matter are written out in
        /// <see cref="TryInteract"/>, which can say which role it wants and for
        /// what; a type cannot.
        /// </summary>
        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Generic;
        public string Prompt => HasStoredLoot
            ? "숨긴 보물 꺼내기"
            : IsSheltering
                ? "끌어내기"
                : "숨기거나 보물 넣기";
        /// <summary>
        /// The thief's alone.
        ///
        /// <see cref="TryInteract"/> already refused anybody else, but only
        /// after the press — so the officer was offered "숨기기" on every crate
        /// and bin and got nothing for it. Stashing loot is a thief verb; the
        /// officer's business with a container is the raccoon's market, which is
        /// a different component.
        /// </summary>
        public bool IsAvailableFor(PawsAndLoot.Gameplay.Players.PlayerRole role)
        {
            return role == PawsAndLoot.Gameplay.Players.PlayerRole.Thief;
        }

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
        /// Hides the carried loot, recovers what is already stored, or hides the
        /// thief themselves. One key, and which of the three it does depends on
        /// what there is to do.
        ///
        /// The box is a crate in the street and the player reads it as somewhere
        /// to get into. Two components each claiming E would be a press whose
        /// outcome nobody can predict — the scanner ranks one target and the
        /// player cannot see which — so the order is written out here instead:
        /// something stored comes back, something carried goes in, and an empty
        /// box with an empty-handed thief is a place to hide.
        /// </summary>
        public bool TryInteract(PlayerInteractionContext context)
        {
            if (!IsAvailable
                || context.Player == null
                || !Contains(context.Player.transform.position))
            {
                return false;
            }

            // The officer's one move here: turf out whoever is inside. Without it
            // a thief who reached a crate would sit in it for the rest of the
            // match, which ends the officer's half of the game.
            if (context.Player.Role != PlayerRole.Thief)
            {
                return IsSheltering && TryClimbIn(context);
            }

            LootCarrier carrier =
                context.Player.GetComponent<LootCarrier>();
            if (carrier == null)
            {
                return false;
            }

            if (HasStoredLoot)
            {
                return TryRecover(carrier);
            }

            return carrier.HasLoot
                ? TryHide(carrier)
                : TryClimbIn(context);
        }

        /// <summary>
        /// Puts the thief in the box, when there is nothing to stash.
        ///
        /// Delegated to the same <see cref="PawsAndLoot.Gameplay.Players.PlayerHidingSpot"/>
        /// the bins use, sitting on this object. Reimplementing it would be a
        /// second place for "hidden" to mean something slightly different, and
        /// the officer's counter-press has to reach both.
        /// </summary>
        private bool TryClimbIn(PlayerInteractionContext context)
        {
            var berth =
                GetComponent<PawsAndLoot.Gameplay.Players.PlayerHidingSpot>();
            return berth != null && berth.TryInteract(context);
        }

        /// <summary>
        /// Whether somebody is inside, so the prompt can say so.
        /// </summary>
        private bool IsSheltering
        {
            get
            {
                var berth =
                    GetComponent<PawsAndLoot.Gameplay.Players.PlayerHidingSpot>();
                return berth != null && berth.IsOccupied;
            }
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
