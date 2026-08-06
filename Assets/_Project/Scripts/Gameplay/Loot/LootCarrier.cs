using System;
using System.Collections.Generic;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    public sealed class LootCarrier : MonoBehaviour
    {
        private const float DropForwardDistance = 1.25f;

        /// <summary>
        /// How many pieces fit in the bag.
        ///
        /// Twenty-one rather than twenty-five: the bag screen draws twenty-five
        /// cells and the first four of them are the prop quick slots, which are a
        /// different store. Writing the number here rather than in the HUD keeps
        /// the refusal and the drawing from disagreeing about when the bag is
        /// full.
        /// </summary>
        public const int DefaultCarryCapacity = 21;

        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Transform carryPoint;

        [SerializeField, Min(1)]
        private int carryCapacity = DefaultCarryCapacity;

        private IMatchStateReader _matchState;
        private readonly HashSet<LootRequestId> _completedRequests =
            new();
        private LootBag _bag;
        private ulong _nextLocalRequestValue = 1;

        /// <summary>
        /// The numbered cells, made on first use.
        ///
        /// Lazily rather than in a field initialiser because the capacity is a
        /// serialized value and a field initialiser runs before deserialisation:
        /// a bag built there would always have the default number of cells no
        /// matter what the prefab says.
        /// </summary>
        private LootBag Bag => _bag ??= new LootBag(CarryCapacity);

        public event Action<LootItem, LootItem> HeldLootChanged;

        /// <summary>
        /// Raised whenever the bag's contents change, including a piece added
        /// underneath the one in hand.
        ///
        /// Separate from <see cref="HeldLootChanged"/> because that one answers
        /// "what is in the thief's hands" — the movement penalty and the carry
        /// sound both need exactly that and nothing else. The bag screen needs
        /// the other question, and firing the hands event for a change it did not
        /// describe would restart the carry sound every time a coin went in.
        /// </summary>
        public event Action CarriedLootChanged;

        /// <summary>
        /// The piece in the thief's hands: the most recently taken one.
        ///
        /// Most recent rather than heaviest, because the hands are also what
        /// <c>E</c> sells and what the drop key drops, and "the thing I just
        /// picked up" is the only answer a player can predict without opening
        /// the bag.
        /// </summary>
        public LootItem HeldLoot => Bag.Newest;

        public bool HasLoot => Bag.Count > 0;
        public IReadOnlyList<LootItem> CarriedLoot => Bag.InAcquisitionOrder;
        public int CarriedCount => Bag.Count;
        public int CarryCapacity => Mathf.Max(1, carryCapacity);
        public bool CanCarryMore => Bag.Count < CarryCapacity;
        public Transform CarryPoint => carryPoint;

        /// <summary>
        /// The bag's cells, for the bag screen and the merchant screen to read.
        /// </summary>
        public LootBag Cells => Bag;

        /// <summary>
        /// Rearranges two cells. Presentation only — it moves nothing between
        /// players and takes nothing out of the bag, so it needs no authority and
        /// no request id.
        /// </summary>
        public bool TryRearrange(int fromCell, int toCell)
        {
            if (!Bag.TryMove(fromCell, toCell))
            {
                return false;
            }

            CarriedLootChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// The heaviest thing in the bag, which is what the thief moves at.
        ///
        /// Taken over the whole bag rather than off the piece in hand, so filling
        /// the bag with gold bars cannot be made free by taking a ring last.
        /// </summary>
        public LootCarryType HeaviestCarryType
        {
            get
            {
                LootCarryType heaviest = LootCarryType.Pocket;
                foreach (LootItem item in Bag.InAcquisitionOrder)
                {
                    LootCarryType candidate =
                        item != null && item.Definition != null
                            ? item.Definition.CarryType
                            : LootCarryType.OneHand;
                    if (candidate > heaviest)
                    {
                        heaviest = candidate;
                    }
                }

                return heaviest;
            }
        }

        public bool IsCarrying(LootItem loot)
        {
            return loot != null && Bag.Contains(loot);
        }

        public void ConfigureCapacity(int configuredCapacity)
        {
            carryCapacity = Mathf.Max(1, configuredCapacity);
        }

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
            Bag.Clear();
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

            // Named refusals rather than one silent false. A pickup that does not
            // happen looks the same from outside whichever of the eight reasons it
            // was, and the two-process run reports "no sale" either way — which
            // says nothing about whether the bag was full, the match was over, or
            // the piece was already somebody's.
            string refusal = null;
            if (!requestId.IsValid) refusal = "request id invalid";
            else if (_completedRequests.Contains(requestId)) refusal = "duplicate request";
            else if (loot == null) refusal = "no loot";
            else if (identity == null) refusal = "no identity";
            else if (identity.Role != PlayerRole.Thief) refusal = "not the thief";
            else if (!IsGameplayActive()) refusal = "match not playing";
            else if (!CanCarryMore) refusal = $"bag full ({Bag.Count}/{CarryCapacity})";
            else if (Bag.Contains(loot)) refusal = "already in the bag";
            else if (!loot.TryAcquire(this, carryPoint))
            {
                refusal = $"loot refused (state {loot.CurrentState}, "
                    + $"carrier {(loot.CurrentCarrier == null ? "none" : "other")}, "
                    + $"remote {loot.IsRemoteControlled})";
            }

            if (refusal != null)
            {
                // Info rather than Debug: a build filters Debug out, and this is a
                // line somebody reads exactly when a pickup is not happening.
                // Repetition is handled by the logger's duplicate suppression.
                GameLogger.Info(
                    GameLogCategory.Loot,
                    $"Pickup refused: {refusal}.",
                    this);
                return false;
            }

            LootItem previous = HeldLoot;
            Vector3 liftedFrom = loot.transform.position;
            Bag.TryAdd(loot);
            LastAcquired = loot;
            RefreshCarriedPresentation();
            _completedRequests.Add(requestId);
            HeldLootChanged?.Invoke(previous, HeldLoot);
            CarriedLootChanged?.Invoke();
            RaiseAlarmIfWatched(loot, liftedFrom);
            return true;
        }

        /// <summary>
        /// The last piece to go in, for the bag screen's NEW badge.
        ///
        /// Kept here rather than in the HUD because the client's bag is rebuilt
        /// from replicated state and the HUD has no other way to tell which cell
        /// is new — it would have to diff two frames of a grid that also reorders.
        /// </summary>
        public LootItem LastAcquired { get; private set; }

        /// <summary>
        /// Shows the piece in hand and hides the rest.
        ///
        /// Every carried piece parents its presentation to the same carry point,
        /// so without this a bag with six things in it draws six models inside
        /// each other at the thief's hip. Re-run from one place on every change so
        /// the host and the client reach the same answer.
        /// </summary>
        private void RefreshCarriedPresentation()
        {
            LootItem visible = HeldLoot;
            foreach (LootItem item in Bag.InAcquisitionOrder)
            {
                if (item != null)
                {
                    item.SetStowed(item != visible);
                }
            }
        }

        /// <summary>
        /// Adopts a piece the authority says this carrier is holding.
        ///
        /// Needed because the thief may be the client: their own bag is rebuilt
        /// from <see cref="LootItem.ApplyRemoteState"/> rather than from their own
        /// pickups, and a bag screen fed only by local acquisition would be empty
        /// on exactly one of the two machines.
        /// </summary>
        internal void AdoptReplicated(LootItem loot)
        {
            if (loot == null)
            {
                return;
            }

            if (Bag.Contains(loot))
            {
                // Already known. The refresh is not redundant: the caller has
                // just re-activated this piece's presentation to attach it, and
                // without stowing it again a bag with three things in it draws
                // all three every time the authority repeats itself.
                RefreshCarriedPresentation();
                return;
            }

            LootItem previous = HeldLoot;
            Bag.TryAdd(loot);
            LastAcquired = loot;
            RefreshCarriedPresentation();
            if (previous != HeldLoot)
            {
                HeldLootChanged?.Invoke(previous, HeldLoot);
            }

            CarriedLootChanged?.Invoke();
        }

        internal void ForgetReplicated(LootItem loot)
        {
            if (loot == null || !Bag.Remove(loot))
            {
                return;
            }

            if (LastAcquired == loot)
            {
                LastAcquired = null;
            }

            RefreshCarriedPresentation();
            HeldLootChanged?.Invoke(loot, HeldLoot);
            CarriedLootChanged?.Invoke();
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

            Release(previous);
            HeldLootChanged?.Invoke(previous, HeldLoot);
            CarriedLootChanged?.Invoke();
            ReportDropNoise(previous, dropPosition);
            return true;
        }

        /// <summary>
        /// Takes a piece out of the bag once the piece itself has agreed to go.
        ///
        /// Always after the item's own transition, never before: the transfer is
        /// "the item leaves, then the bag forgets it". Forgetting first is how a
        /// refused transition leaves a piece that belongs to nobody.
        /// </summary>
        private void Release(LootItem loot)
        {
            Bag.Remove(loot);
            if (LastAcquired == loot)
            {
                LastAcquired = null;
            }

            RefreshCarriedPresentation();
        }

        /// <summary>
        /// Tells the town that something heavy just hit the ground.
        ///
        /// Only for things heavy enough to be heard. A pocket piece gets a
        /// radius of zero and nothing is written down — an event nobody could
        /// act on is worse than no event, because it teaches the officer to
        /// ignore the one signal that matters.
        ///
        /// Reported on the drop rather than on the pickup, because dropping is
        /// the moment the thief chooses. Picking a thing up is something they
        /// did quietly on purpose; putting it down to run is a decision with a
        /// price, and this is the price.
        /// </summary>
        private void ReportDropNoise(LootItem dropped, Vector3 at)
        {
            if (dropped?.Definition == null)
            {
                return;
            }

            float radius = LootCarryRules.DropNoiseRadius(
                dropped.Definition.CarryType);
            if (radius <= 0f)
            {
                return;
            }

            FindFirstObjectByType<PawsAndLoot.Gameplay.Sensing.NoiseBoard>()
                ?.Report(at, radius, identity == null ? null : identity.Role);
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
                || loot == null
                || !Bag.Contains(loot)
                || stashRoot == null)
            {
                return false;
            }

            LootItem previous = HeldLoot;
            if (!loot.TryHide(this, stashRoot))
            {
                return false;
            }

            Release(loot);
            HeldLootChanged?.Invoke(previous, HeldLoot);
            CarriedLootChanged?.Invoke();
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
            return TrySellItem(HeldLoot, wallet, lootConfig, requestId);
        }

        /// <summary>
        /// Sells one named piece out of the bag.
        ///
        /// The merchant screen needs this: the thief ticks a gemstone in a bag
        /// that also holds a watch, and "sell what is in your hands" would sell
        /// the wrong one. The hands version above is now this one aimed at
        /// <see cref="HeldLoot"/>, so both paths run the same duplicate-sale
        /// guard rather than two copies of it.
        /// </summary>
        public bool TrySellItem(
            LootItem loot,
            ThiefLootWallet wallet,
            LootConfig lootConfig,
            LootRequestId requestId)
        {
            ValidateOrThrow();
            if (!requestId.IsValid
                || _completedRequests.Contains(requestId)
                || identity.Role != PlayerRole.Thief
                || !IsGameplayActive()
                || loot == null
                || !Bag.Contains(loot)
                || wallet == null
                || lootConfig == null)
            {
                return false;
            }

            lootConfig.ValidateOrThrow();
            LootItem previous = HeldLoot;
            int price = loot.Definition.GetPrice(lootConfig);
            if (!wallet.CanRecordSale(
                    loot,
                    price,
                    requestId)
                || !loot.TrySell(this))
            {
                return false;
            }

            Release(loot);
            _completedRequests.Add(requestId);
            HeldLootChanged?.Invoke(previous, HeldLoot);
            CarriedLootChanged?.Invoke();
            wallet.RecordSale(loot, price, requestId);
            return true;
        }

        /// <summary>
        /// Sells every piece of one kind and reports how many went.
        ///
        /// By definition rather than by object reference because the request
        /// crosses the wire: the client's bag screen knows it ticked "Blue Gem
        /// x5", and which five objects those are is the host's business. Sending
        /// object ids instead would make a sale fail whenever the two machines
        /// disagreed by one frame about which gem is which.
        /// </summary>
        public int SellAllOfKind(
            LootDefinition definition,
            ThiefLootWallet wallet,
            LootConfig lootConfig,
            int maximumCount = int.MaxValue)
        {
            if (definition == null || maximumCount <= 0)
            {
                return 0;
            }

            // Over a copy, because selling removes from the bag as we go and
            // walking the live list would skip every second piece.
            var candidates = new List<LootItem>(Bag.InAcquisitionOrder);
            int sold = 0;
            for (int index = candidates.Count - 1; index >= 0 && sold < maximumCount; index--)
            {
                LootItem candidate = candidates[index];
                if (candidate == null || candidate.Definition != definition)
                {
                    continue;
                }

                if (TrySellItem(
                        candidate,
                        wallet,
                        lootConfig,
                        CreateLocalRequestId()))
                {
                    sold++;
                }
            }

            return sold;
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
            if (loot == null || !Bag.Contains(loot))
            {
                return;
            }

            LootItem previous = HeldLoot;
            Release(loot);
            HeldLootChanged?.Invoke(previous, HeldLoot);
            CarriedLootChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (Bag.Count == 0)
            {
                return;
            }

            LootItem previous = HeldLoot;
            bool changed = false;
            var carried = new List<LootItem>(Bag.InAcquisitionOrder);
            foreach (LootItem item in carried)
            {
                if (item == null
                    || item.TryReleaseFromUnavailableCarrier(this))
                {
                    Bag.Remove(item);
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            LastAcquired = null;
            RefreshCarriedPresentation();
            HeldLootChanged?.Invoke(previous, HeldLoot);
            CarriedLootChanged?.Invoke();
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
