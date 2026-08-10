using System;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// Which roles may search a container.
    ///
    /// Kept as its own field rather than as a bare interaction type, because
    /// "who is allowed to open the fridge" is a question about the fridge and
    /// should read like one in the Inspector. It is resolved to the permission
    /// table the rest of the game already uses, so there is one answer to "can
    /// this role interact with this" and not two that can disagree.
    /// </summary>
    public enum ContainerAccess
    {
        Thief = 0,
        Everyone = 1
    }

    /// <summary>
    /// Something a player can rummage through: a cupboard, a drawer, a till.
    ///
    /// An empty marker rather than a piece of the room's model. Every interior
    /// that arrives now is a single scanned mesh with no named parts in it, so
    /// there is no cupboard object to attach this to; the position comes from the
    /// interior plan captures instead, the same way the doorways and the arrival
    /// spots already do. When the models are eventually split, this component moves
    /// onto the real furniture and nothing else has to change.
    ///
    /// Contents are rolled once and then kept. A container that re-rolled on every
    /// open would let a thief stand at one cupboard and pull the whole table out of
    /// it, and the spec calls that out for good reason - the roll is the reward for
    /// having walked here, not for having pressed the key again.
    ///
    /// The contents live in a <c>QuickSlotController</c>, which is what the cat's
    /// bag uses. Stacking, capacity, and taking exactly one out are already right
    /// in there and getting them subtly wrong in a second place is how the same
    /// item ends up in two bags.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SearchableContainer : MonoBehaviour,
        IPlayerInteractable,
        IHoldInteractable,
        IInteractionPriority,
        ISlotContainer
    {
        /// <summary>
        /// Raised when a search finishes and the panel should open. The HUD listens
        /// rather than this reaching into it, which is what lets the cat's bag and a
        /// cupboard share one screen.
        /// </summary>
        public static event Action<ISlotContainer, ToolCarrier> SearchCompleted;

        [SerializeField]
        private string containerId = string.Empty;

        [SerializeField]
        private string containerDisplayName = "보관함";

        [SerializeField]
        private LootTable lootTable;

        [SerializeField]
        private ContainerAccess allowedRoles = ContainerAccess.Thief;

        /// <summary>
        /// How long the search takes. The default is short on purpose: this is a
        /// chase, and a two-second animation in front of a cupboard is two seconds
        /// the officer is walking towards you. Zero opens it on the press.
        /// </summary>
        [SerializeField, Min(0f)]
        private float searchDuration = 0.6f;

        [SerializeField, Min(0.2f)]
        private float interactionRadius = 1.1f;

        /// <summary>
        /// Whether an emptied container stays empty. On means the roll happens once
        /// per round, which is what stops a cupboard being a vending machine.
        /// </summary>
        [SerializeField]
        private bool searchOnceOnly = true;

        /// <summary>
        /// Whether an emptied container still offers a prompt. Off removes it from
        /// the scanner entirely, so a stripped room stops competing with the loot on
        /// its floor for the press.
        /// </summary>
        [SerializeField]
        private bool promptWhenEmpty = true;

        [SerializeField, Min(1)]
        private int maximumStackSize = 9;

        private readonly QuickSlotController contents = new();
        private IMatchStateReader matchState;
        private MonoBehaviour matchStateSource;
        private bool filled;

        public string ContainerId =>
            string.IsNullOrWhiteSpace(containerId) ? name : containerId;

        public LootTable Table => lootTable;
        public bool HasBeenSearched => filled;

        /// <summary>
        /// Whether anything is left in it. Asked of the slots rather than tracked
        /// alongside them, because a second copy of "is it empty" is a second thing
        /// that can be wrong after a transfer.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                for (int index = 0; index < QuickSlotController.SlotCount; index++)
                {
                    if (contents.GetQuantity(index) > 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Transform InteractionTransform => transform;

        /// <summary>
        /// Resolved to the permission table rather than checked here. <c>Loot</c> is
        /// the thief's, <c>Generic</c> is everybody's, and both already have a row
        /// in <c>PlayerRolePermissions</c>.
        /// </summary>
        public PlayerInteractionType InteractionType =>
            allowedRoles == ContainerAccess.Everyone
                ? PlayerInteractionType.Generic
                : PlayerInteractionType.Loot;

        public string Prompt => filled && IsEmpty
            ? $"빈 {containerDisplayName} 확인"
            : $"{containerDisplayName} 뒤지기";

        public bool IsAvailable =>
            ResolveMatchState()?.IsGameplayActive == true
            && (promptWhenEmpty || !(filled && IsEmpty));

        public float HoldDurationSeconds => searchDuration;

        /// <summary>
        /// Above the floor loot deliberately. A cupboard and the rock lying in front
        /// of it are both in reach, and the one the player walked over to is the
        /// cupboard: it is the bigger, more deliberate target and the rock can be
        /// picked up from anywhere. The cat sits below both at -1.
        /// </summary>
        public int InteractionPriority => 1;

        public int SlotCount => QuickSlotController.SlotCount;
        public string DisplayName => containerDisplayName;

        private void Awake()
        {
            EnsureInteractionCollider();
        }

        public void Configure(
            string configuredId,
            string configuredDisplayName,
            LootTable configuredTable,
            IMatchStateReader configuredMatchState,
            ContainerAccess configuredAccess = ContainerAccess.Thief,
            float configuredSearchDuration = 0.6f,
            bool configuredSearchOnceOnly = true)
        {
            containerId = configuredId;
            containerDisplayName = configuredDisplayName;
            lootTable = configuredTable;
            matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            allowedRoles = configuredAccess;
            searchDuration = Mathf.Max(0f, configuredSearchDuration);
            searchOnceOnly = configuredSearchOnceOnly;
            EnsureInteractionCollider();
        }

        /// <summary>
        /// Rolls the contents, once.
        ///
        /// Takes the generator from the caller so the host can drive every container
        /// in a match off the one match seed. A container that made its own would be
        /// unreproducible and, worse, would roll differently on each machine.
        /// </summary>
        public void Fill(System.Random random)
        {
            if (filled && searchOnceOnly)
            {
                return;
            }

            filled = true;
            if (lootTable == null)
            {
                GameLogger.Warning(
                    GameLogCategory.Loot,
                    $"'{ContainerId}' has no loot table, so it opens empty.");
                return;
            }

            foreach (LootRoll roll in LootRoller.Roll(lootTable, random))
            {
                if (!contents.TryStore(
                        roll.Kind,
                        roll.Quantity,
                        maximumStackSize,
                        out _))
                {
                    // The table asked for more than the container holds. Not an
                    // error: a wide table against four slots is a normal way to set
                    // one up, and the surplus is simply not there.
                    break;
                }
            }

            GameLogger.Debug(
                GameLogCategory.Loot,
                $"'{ContainerId}' filled from '{lootTable.name}'.");
        }

        public bool TryGetSlot(int index, out ThrowableKind kind)
        {
            return contents.TryGet(index, out kind);
        }

        public int GetSlotQuantity(int index)
        {
            return contents.GetQuantity(index);
        }

        public bool CanStore(ThrowableKind kind, int quantity)
        {
            return ThrowableCatalog.CanUseInQuickSlot(kind)
                && contents.CanStore(kind, quantity, maximumStackSize);
        }

        public bool TryStore(ThrowableKind kind, int quantity)
        {
            return ThrowableCatalog.CanUseInQuickSlot(kind)
                && contents.CanStore(kind, quantity, maximumStackSize)
                && contents.TryStore(kind, quantity, maximumStackSize, out _);
        }

        public bool TryTakeOne(int index, out ThrowableKind kind)
        {
            return contents.TryTakeOne(index, out kind);
        }

        /// <summary>
        /// A press with no hold behind it. Only reached when the search time is
        /// zero, because a container with a duration is driven through the hold
        /// interface instead.
        /// </summary>
        public bool TryInteract(PlayerInteractionContext context)
        {
            return searchDuration <= 0f && Open(context);
        }

        public bool CanBeginHold(PlayerInteractionContext context)
        {
            return IsAvailable && CanBeOpenedBy(context);
        }

        public bool CompleteHold(PlayerInteractionContext context)
        {
            return Open(context);
        }

        public void CancelHold(PlayerInteractionContext context)
        {
            // Nothing to undo. The roll happens on completion, not on the first
            // frame of the hold, so a search that was walked away from leaves the
            // container exactly as it was found.
        }

        private bool Open(PlayerInteractionContext context)
        {
            if (!IsAvailable || !CanBeOpenedBy(context))
            {
                return false;
            }

            ToolCarrier carrier = context.Player.GetComponent<ToolCarrier>();
            if (carrier == null)
            {
                return false;
            }

            if (!filled)
            {
                // Lazily, on the first search. Rolling every container in the town
                // at the start of the match would be work nobody asked for on
                // rooms nobody enters.
                Fill(new System.Random(ResolveSeed()));
            }

            SearchCompleted?.Invoke(this, carrier);
            return true;
        }

        private bool CanBeOpenedBy(PlayerInteractionContext context)
        {
            return context.Player != null
                && (allowedRoles == ContainerAccess.Everyone
                    || context.Role == PlayerRole.Thief);
        }

        /// <summary>
        /// The host's match seed, mixed with this container's id so two cupboards
        /// on the same table hold different things.
        ///
        /// It used to be <c>ContainerId.GetHashCode()</c> alone, which was wrong
        /// twice over. .NET randomises string hashing **per process**, so the host
        /// and the client rolled different contents for the same cupboard in the
        /// same match — the one thing the id was there to prevent. And a seed with
        /// no match in it makes every match identical, which the comment here
        /// described as a feature ("the same cupboard holds the same thing on a
        /// re-run") without noticing it also meant the second match is the first
        /// one.
        /// </summary>
        private int ResolveSeed()
        {
            return Loot.MatchDrawSeed.For(
                Loot.MatchDrawSeed.Current,
                ContainerId);
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                matchState = reader;
            }

            matchState ??= FindFirstObjectByType<MatchRuntimeState>();
            return matchState;
        }

        /// <summary>
        /// Its own trigger, so the scanner can find it without the room's mesh
        /// having anything to do with it. A trigger rather than a solid: the marker
        /// is not furniture and must not be something to walk into.
        /// </summary>
        private void EnsureInteractionCollider()
        {
            var reach = GetComponent<SphereCollider>();
            if (reach == null)
            {
                reach = gameObject.AddComponent<SphereCollider>();
            }

            reach.isTrigger = true;
            reach.radius = Mathf.Max(0.2f, interactionRadius);
        }
    }
}
