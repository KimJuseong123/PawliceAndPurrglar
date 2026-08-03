using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Four prop slots per player, separate from the loot slot.
    ///
    /// It has to be separate. "The thief carries one piece of loot" is a
    /// confirmed rule in <c>docs/03_GAME_RULES.md</c>, and sharing that slot
    /// would mean picking up a banana makes you drop the jewels — which turns
    /// every item into a punishment rather than a choice.
    ///
    /// This class only tracks what is held and hands out the decision to use it.
    /// It never applies a stun and never moves anyone: the host resolves the
    /// throw, so a client cannot stun anybody by holding a rock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolCarrier : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private bool grantDefaultLoadoutOnGameplayStart;

        [SerializeField, Min(1)]
        private int maximumStackSize = 9;

        private readonly QuickSlotController _slots = new();
        private IMatchStateReader _matchState;
        private MatchRuntimeState _subscribedRuntime;
        private bool _defaultLoadoutGranted;

        public event Action<bool> HeldToolChanged;
        public event Action InventoryChanged;

        public bool HasTool => _slots.HasSelectedItem;
        public bool HasAnyTool => _slots.HasAnyItem;
        public ThrowableKind HeldKind =>
            _slots.TryGet(_slots.SelectedSlot, out ThrowableKind kind)
                ? kind
                : ThrowableKind.Rock;
        public int HeldQuantity => GetSlotQuantity(SelectedSlot);
        public PlayerRole Role =>
            identity != null ? identity.Role : PlayerRole.Police;

        public ThrowableUse HeldUse => ThrowableCatalog.GetUse(HeldKind);

        public int SelectedSlot => _slots.SelectedSlot;
        public int EncodedSlots => _slots.EncodeSlots();
        public int EncodedQuantities => _slots.EncodeQuantities();

        public bool TryGetSlot(int index, out ThrowableKind kind)
        {
            return _slots.TryGet(index, out kind);
        }

        public int GetSlotQuantity(int index)
        {
            return _slots.GetQuantity(index);
        }

        public bool CanStore(ThrowableKind kind)
        {
            return ThrowableCatalog.CanUseInQuickSlot(kind)
                && _slots.CanStore(kind, maximumStackSize);
        }

        public bool CanStore(ThrowableKind kind, int quantity)
        {
            return ThrowableCatalog.CanUseInQuickSlot(kind)
                && _slots.CanStore(kind, quantity, maximumStackSize);
        }

        public bool SelectSlot(int index)
        {
            if (!_slots.SelectSlot(index))
            {
                return false;
            }

            PublishChanged();
            return true;
        }

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            IMatchStateReader configuredMatchState)
        {
            UnsubscribeRuntime();
            identity = configuredIdentity;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            _slots.Clear();
            _defaultLoadoutGranted = false;
            SubscribeRuntime();
            TryGrantDefaultLoadoutIfNeeded();
            PublishChanged();
        }

        public void ConfigureDefaultLoadout(
            bool grantOnGameplayStart,
            int configuredMaximumStackSize = 9)
        {
            grantDefaultLoadoutOnGameplayStart = grantOnGameplayStart;
            maximumStackSize = Mathf.Max(1, configuredMaximumStackSize);
            TryGrantDefaultLoadoutIfNeeded();
        }

        /// <summary>
        /// Takes a prop. Refused when a match is not running or the quick slots
        /// have no room for this kind.
        /// </summary>
        public bool TryPickUp(ThrowableKind kind)
        {
            if (ResolveMatchState()?.IsGameplayActive != true
                || !ThrowableCatalog.CanUseInQuickSlot(kind)
                || !_slots.TryStore(
                    kind,
                    1,
                    maximumStackSize,
                    out _))
            {
                return false;
            }

            GameLogger.Info(
                GameLogCategory.Player,
                $"{Role} picked up {kind}.",
                this);
            PublishChanged();
            return true;
        }

        public bool TryStore(ThrowableKind kind, int quantity)
        {
            if (ResolveMatchState()?.IsGameplayActive != true
                || !ThrowableCatalog.CanUseInQuickSlot(kind)
                || !_slots.CanStore(kind, quantity, maximumStackSize)
                || !_slots.TryStore(
                    kind,
                    quantity,
                    maximumStackSize,
                    out _))
            {
                return false;
            }

            PublishChanged();
            return true;
        }

        public bool TryTakeSlot(
            int index,
            out ThrowableKind kind,
            out int quantity)
        {
            if (!_slots.TryTakeSlot(index, out kind, out quantity))
            {
                return false;
            }

            PublishChanged();
            return true;
        }

        public bool TryTakeOne(
            int index,
            out ThrowableKind kind)
        {
            if (!_slots.TryTakeOne(index, out kind))
            {
                return false;
            }

            PublishChanged();
            return true;
        }

        /// <summary>
        /// Spends one prop from the selected slot.
        /// </summary>
        public bool TryConsume(out ThrowableKind kind)
        {
            kind = HeldKind;
            if (ResolveMatchState()?.IsGameplayActive != true
                || !_slots.TryConsumeSelected(out kind))
            {
                return false;
            }

            PublishChanged();
            return true;
        }

        /// <summary>
        /// Sets the slot to what the host says it is, on a machine that does not
        /// decide.
        /// </summary>
        /// <remarks>
        /// Deliberately skips the match-state gate that <see cref="TryPickUp"/>
        /// applies. This is not a request and cannot be refused — the host has
        /// already decided, and a client that quietly declined would show an
        /// empty hand for a rock it is really holding. That was the bug: picking
        /// a rock up worked on the host and the other player's HUD kept saying
        /// they had nothing, which is indistinguishable from the pickup being
        /// broken.
        /// </remarks>
        public void ApplyReplicated(bool hasTool, ThrowableKind kind)
        {
            if (MatchesReplicatedState(hasTool, kind))
            {
                return;
            }

            _slots.Clear();
            if (hasTool)
            {
                _slots.TrySetSlot(0, kind, 1);
                _slots.SelectSlot(0);
            }

            PublishChanged();
        }

        public void ApplyReplicatedSlots(
            int packedSlots,
            int packedQuantities,
            int selectedSlot)
        {
            int clampedSelected = Mathf.Clamp(
                selectedSlot,
                0,
                QuickSlotController.SlotCount - 1);
            if (_slots.EncodeSlots() == packedSlots
                && _slots.EncodeQuantities() == packedQuantities
                && _slots.SelectedSlot == clampedSelected)
            {
                return;
            }

            _slots.ApplyEncodedSlots(
                packedSlots,
                packedQuantities,
                clampedSelected);
            PublishChanged();
        }

        /// <summary>
        /// Drops the prop without using it, for match end and restarts.
        /// </summary>
        public void Clear()
        {
            _defaultLoadoutGranted = false;
            if (!_slots.HasAnyItem)
            {
                return;
            }

            _slots.Clear();
            PublishChanged();
        }

        private void OnEnable()
        {
            SubscribeRuntime();
            TryGrantDefaultLoadoutIfNeeded();
        }

        private void Start()
        {
            SubscribeRuntime();
            TryGrantDefaultLoadoutIfNeeded();
        }

        private void OnDisable()
        {
            UnsubscribeRuntime();
        }

        private bool MatchesReplicatedState(
            bool hasTool,
            ThrowableKind kind)
        {
            if (!hasTool)
            {
                return !_slots.HasAnyItem;
            }

            return _slots.OccupiedSlotCount == 1
                && _slots.SelectedSlot == 0
                && _slots.GetQuantity(0) == 1
                && _slots.TryGet(0, out ThrowableKind current)
                && current == kind;
        }

        private void TryGrantDefaultLoadoutIfNeeded()
        {
            if (!grantDefaultLoadoutOnGameplayStart
                || _defaultLoadoutGranted
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                return;
            }

            bool changed = false;
            for (int slot = 0; slot < QuickSlotController.SlotCount; slot++)
            {
                if (!ThrowableCatalog.TryGetStartingLoadout(
                        Role,
                        slot,
                        out ThrowableLoadoutItem item))
                {
                    continue;
                }

                changed |= _slots.TrySetSlot(
                    slot,
                    item.Kind,
                    Mathf.Min(
                        Mathf.Max(1, item.Quantity),
                        maximumStackSize));
            }

            _slots.SelectSlot(0);
            _defaultLoadoutGranted = true;
            if (changed)
            {
                GameLogger.Info(
                    GameLogCategory.Player,
                    $"{Role} received starting tools.",
                    this);
                PublishChanged();
            }
        }

        private void PublishChanged()
        {
            HeldToolChanged?.Invoke(HasTool);
            InventoryChanged?.Invoke();
        }

        private void SubscribeRuntime()
        {
            if (_subscribedRuntime != null)
            {
                return;
            }

            if (matchStateSource is MatchRuntimeState runtime)
            {
                _subscribedRuntime = runtime;
                _subscribedRuntime.StateChanged += HandleMatchStateChanged;
            }
        }

        private void UnsubscribeRuntime()
        {
            if (_subscribedRuntime == null)
            {
                return;
            }

            _subscribedRuntime.StateChanged -= HandleMatchStateChanged;
            _subscribedRuntime = null;
        }

        private void HandleMatchStateChanged(MatchStateChanged change)
        {
            if (change.CurrentState == MatchState.Playing)
            {
                TryGrantDefaultLoadoutIfNeeded();
            }
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }
    }
}
