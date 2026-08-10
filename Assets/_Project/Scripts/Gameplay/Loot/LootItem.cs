using System;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Loot
{
    /// <summary>
    /// A piece of treasure lying in the world.
    ///
    /// One press takes it. <c>LootPickupProgress</c> is no longer on the path.
    ///
    /// A committed moment beside the treasure is a good rule and this is not a
    /// rejection of it — it is a rejection of a rule the player cannot see. The
    /// wait was charged by counting the frames the thief kept *asking*, one press
    /// asks once, and in a session the local input is switched off so the progress
    /// ring that was meant to signpost the hold never ran at all. Every version of
    /// it read as "the key does nothing".
    /// </summary>
    public sealed class LootItem :
        MonoBehaviour,
        IPlayerInteractable,
        IHoldInteractable
    {
        [SerializeField]
        private LootDefinition definition;

        [SerializeField]
        private LootState initialState = LootState.Available;

        [SerializeField]
        private Transform presentationRoot;

        private LootStateMachine _stateMachine;
        private Collider[] _worldColliders;
        private float _worldClearance;

        public event Action<LootStateChanged> StateChanged;

        public LootDefinition Definition => definition;
        public Transform PresentationRoot => presentationRoot;
        public float WorldClearance => GetWorldClearance();
        public LootState CurrentState =>
            EnsureStateMachine().CurrentState;
        public LootCarrier CurrentCarrier { get; private set; }
        public Transform InteractionTransform => transform;
        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Loot;
        public string Prompt => definition != null
            ? $"Pick up {definition.DisplayName}"
            : "Pick up loot";
        public bool IsAvailable =>
            isActiveAndEnabled
            && CurrentCarrier == null
            && IsPickupState(CurrentState);

        public void Configure(
            LootDefinition configuredDefinition,
            Transform configuredPresentationRoot,
            LootState configuredInitialState = LootState.Available)
        {
            definition = configuredDefinition;
            presentationRoot = configuredPresentationRoot;
            initialState = configuredInitialState;
            _stateMachine = null;
        }

        /// <summary>
        /// True when another machine owns this item's state. A remote-controlled
        /// item never runs its own transitions; it only reflects the authority,
        /// which is what stops two machines disagreeing about who holds it.
        /// </summary>
        public bool IsRemoteControlled { get; private set; }

        public void SetRemoteControlled(bool remoteControlled)
        {
            IsRemoteControlled = remoteControlled;
        }

        /// <summary>
        /// NET-005. Applies the authority's view of this item.
        ///
        /// Transitions still pass through the state machine so the legal-move
        /// rules hold, and the presentation is re-parented to match the carrier
        /// so a client sees the loot in the right hands.
        /// </summary>
        public void ApplyRemoteState(
            LootState state,
            Vector3 worldPosition,
            LootCarrier carrier)
        {
            LootStateMachine machine = EnsureStateMachine();
            if (machine.CurrentState != state)
            {
                // A rejected transition means the authority took a path this
                // machine has not seen; force it rather than drift apart.
                if (!machine.TryTransitionTo(state))
                {
                    machine.ResetTo(state);
                }
            }

            LootCarrier previousCarrier = CurrentCarrier;
            CurrentCarrier = carrier;
            if (previousCarrier != null && previousCarrier != carrier)
            {
                previousCarrier.ForgetReplicated(this);
            }

            if (presentationRoot == null)
            {
                return;
            }

            if (carrier != null && carrier.CarryPoint != null)
            {
                presentationRoot.SetParent(carrier.CarryPoint, false);
                presentationRoot.localPosition = Vector3.zero;
                presentationRoot.localRotation = Quaternion.identity;
                presentationRoot.gameObject.SetActive(true);

                // The world collider goes with it. Only the host runs
                // AttachPresentation, so on a client a carried piece left a solid
                // box standing where it was picked up — invisible, and in the way
                // of the thief who had just taken it.
                CacheWorldColliders();
                SetWorldCollidersEnabled(false);

                // After the attach, not before. The carrier decides which one
                // piece is in the hands and stows the rest; telling it first
                // would let the line above un-stow whatever it had just hidden.
                carrier.AdoptReplicated(this);
                return;
            }

            presentationRoot.SetParent(transform, false);
            presentationRoot.position = worldPosition;
            bool onTheGround =
                state != LootState.Sold && state != LootState.Hidden;
            presentationRoot.gameObject.SetActive(onTheGround);
            CacheWorldColliders();
            SetWorldCollidersEnabled(onTheGround);
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null
                || context.Role != PlayerRole.Thief
                || IsRemoteControlled)
            {
                return false;
            }

            LootCarrier carrier =
                context.Player.GetComponent<LootCarrier>();
            if (carrier == null)
            {
                return false;
            }

            // Straight into the bag on one press.
            //
            // It used to go through LootPickupProgress, which charges a wait by
            // counting the frames the thief keeps asking. That design is sound and
            // the input was not: one press asks once, so a tap never finished and
            // holding was never signposted — in a session the local input is off,
            // so the progress ring the hold was supposed to fill does not even
            // run. Two attempts at making the hold legible both landed on "the key
            // does nothing", which is the worst reading a key can have.
            return carrier.TryAcquire(this);
        }

        /// <summary>
        /// Zero: one press takes it.
        ///
        /// Still an <see cref="IHoldInteractable"/> rather than dropping back to a
        /// plain interactable, and that is what makes the press work offline —
        /// <c>PlayerInteractionInput</c> reads a zero duration as "complete it
        /// now" and calls <see cref="CompleteHold"/> on the press frame. The route
        /// through <c>TryInteract</c> stays for the host, which is handed requests
        /// rather than pressing keys.
        /// </summary>
        public float HoldDurationSeconds => 0f;

        /// <summary>
        /// Whether this player may start taking it.
        ///
        /// The role is checked here and not only in
        /// <c>PlayerRolePermissions</c>. That table is consulted by the scanner
        /// when it ranks what is in range, and the host runs interactions the
        /// scanner never ranked — it is handed a request and acts on it. And every
        /// player carries a <see cref="LootCarrier"/>, officer included, so
        /// "has somewhere to put it" is not the question it looks like.
        /// </summary>
        public bool CanBeginHold(PlayerInteractionContext context)
        {
            return context.Player != null
                && context.Role == PlayerRole.Thief
                && !IsRemoteControlled
                && IsAvailable
                && context.Player.GetComponent<LootCarrier>() != null;
        }

        /// <summary>
        /// Takes it, at the end of the hold.
        ///
        /// The same route <see cref="TryInteract"/> takes. Two entry points and
        /// one rule: a press from this machine and a request from the host both
        /// end at <c>LootCarrier.TryAcquire</c>.
        /// </summary>
        public bool CompleteHold(PlayerInteractionContext context)
        {
            if (!CanBeginHold(context))
            {
                return false;
            }

            return context.Player.GetComponent<LootCarrier>().TryAcquire(this);
        }

        public void CancelHold(PlayerInteractionContext context)
        {
        }

        internal bool TryAcquire(
            LootCarrier carrier,
            Transform carryPoint)
        {
            if (carrier == null
                || carryPoint == null
                || CurrentCarrier != null
                || !IsPickupState(CurrentState))
            {
                return false;
            }

            ValidatePresentationOrThrow();
            LootStateMachine stateMachine = EnsureStateMachine();
            if (!stateMachine.TryTransitionTo(LootState.Reserved))
            {
                return false;
            }

            CurrentCarrier = carrier;
            if (!stateMachine.TryTransitionTo(LootState.Carried))
            {
                CurrentCarrier = null;
                throw new InvalidOperationException(
                    $"Loot '{name}' could not complete RESERVED -> CARRIED.");
            }

            AttachPresentation(carryPoint);
            return true;
        }

        /// <summary>
        /// Hides a carried piece that is in the bag rather than in the hands.
        ///
        /// Every carried piece parents its model to the same carry point, so the
        /// bag needs somewhere for the ones that are not on show to go, and
        /// "nowhere" is the only place that cannot end up behind a wall or inside
        /// the thief's head. Driven only by <c>LootCarrier</c>: a piece that
        /// decided this for itself would fight the carrier over which one is in
        /// hand.
        /// </summary>
        internal void SetStowed(bool stowed)
        {
            if (presentationRoot == null)
            {
                return;
            }

            IsStowed = stowed;
            presentationRoot.gameObject.SetActive(!stowed);
        }

        public bool IsStowed { get; private set; }

        internal bool TryReleaseFromUnavailableCarrier(
            LootCarrier carrier)
        {
            if (carrier == null
                || CurrentCarrier != carrier
                || CurrentState != LootState.Carried)
            {
                return false;
            }

            if (!EnsureStateMachine().TryTransitionTo(LootState.Dropped))
            {
                return false;
            }

            CurrentCarrier = null;
            RestorePresentationToWorld(presentationRoot.position);
            return true;
        }

        internal bool TryDrop(
            LootCarrier carrier,
            Vector3 worldPosition)
        {
            if (carrier == null
                || CurrentCarrier != carrier
                || CurrentState != LootState.Carried)
            {
                return false;
            }

            if (!EnsureStateMachine().TryTransitionTo(LootState.Dropped))
            {
                return false;
            }

            CurrentCarrier = null;
            RestorePresentationToWorld(worldPosition);
            return true;
        }

        /// <summary>
        /// LOOT-005. Moves carried loot into a hiding spot's stash.
        ///
        /// The presentation is parented to the stash rather than left in the
        /// world, so a hidden item is not visible lying on the ground, and the
        /// state machine records HIDDEN so it can be recovered later. Sold loot
        /// is terminal and is rejected by the state machine.
        /// </summary>
        internal bool TryHide(
            LootCarrier carrier,
            Transform stashRoot)
        {
            if (carrier == null
                || stashRoot == null
                || CurrentCarrier != carrier
                || CurrentState != LootState.Carried)
            {
                return false;
            }

            if (!EnsureStateMachine().TryTransitionTo(LootState.Hidden))
            {
                return false;
            }

            CurrentCarrier = null;
            presentationRoot.SetParent(stashRoot, false);
            presentationRoot.localPosition = Vector3.zero;
            presentationRoot.localRotation = Quaternion.identity;
            presentationRoot.gameObject.SetActive(false);
            return true;
        }

        internal bool TrySell(LootCarrier carrier)
        {
            if (carrier == null
                || CurrentCarrier != carrier
                || CurrentState != LootState.Carried)
            {
                return false;
            }

            if (!EnsureStateMachine().TryTransitionTo(LootState.Sold))
            {
                return false;
            }

            CurrentCarrier = null;
            presentationRoot.SetParent(transform, false);
            presentationRoot.localPosition = Vector3.zero;
            presentationRoot.localRotation = Quaternion.identity;
            presentationRoot.gameObject.SetActive(false);
            SetWorldCollidersEnabled(false);
            return true;
        }

        private void Awake()
        {
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"LootItem '{name}' requires a LootDefinition.");
            }

            definition.ValidateOrThrow();
            ValidatePresentationOrThrow();
            EnsureStateMachine();
        }

        private void OnDestroy()
        {
            LootCarrier carrier = CurrentCarrier;
            CurrentCarrier = null;
            if (carrier != null)
            {
                carrier.HandleLootUnavailable(this);
            }
        }

        private void CacheWorldColliders()
        {
            if (_worldColliders != null)
            {
                return;
            }

            _worldColliders = GetComponentsInChildren<Collider>(true);
            _worldClearance = CalculateWorldClearance(_worldColliders);
        }

        private void AttachPresentation(Transform carryPoint)
        {
            _worldColliders =
                GetComponentsInChildren<Collider>(true);
            _worldClearance =
                CalculateWorldClearance(_worldColliders);
            SetWorldCollidersEnabled(false);
            presentationRoot.SetParent(carryPoint, false);
            presentationRoot.localPosition = Vector3.zero;
            presentationRoot.localRotation = Quaternion.identity;
        }

        private void RestorePresentationToWorld(Vector3 worldPosition)
        {
            presentationRoot.SetParent(transform, false);
            transform.position = worldPosition;
            presentationRoot.localPosition = Vector3.zero;
            presentationRoot.localRotation = Quaternion.identity;

            // Un-stowed on the way out. A piece dropped straight from the bag has
            // its model switched off, and leaving it that way puts an invisible,
            // solid, pickable object on the pavement — which reads as the drop
            // key deleting loot.
            IsStowed = false;
            presentationRoot.gameObject.SetActive(true);
            SetWorldCollidersEnabled(true);
        }

        private float GetWorldClearance()
        {
            if (_worldClearance > 0f)
            {
                return _worldClearance;
            }

            Collider[] colliders =
                GetComponentsInChildren<Collider>(true);
            _worldClearance = CalculateWorldClearance(colliders);
            return _worldClearance;
        }

        private static float CalculateWorldClearance(
            Collider[] colliders)
        {
            float clearance = 0f;
            foreach (Collider worldCollider in colliders)
            {
                if (worldCollider != null)
                {
                    clearance = Mathf.Max(
                        clearance,
                        worldCollider.bounds.extents.y);
                }
            }

            return clearance;
        }

        private void SetWorldCollidersEnabled(bool enabled)
        {
            if (_worldColliders == null)
            {
                return;
            }

            foreach (Collider worldCollider in _worldColliders)
            {
                if (worldCollider != null)
                {
                    worldCollider.enabled = enabled;
                }
            }
        }

        private void ValidatePresentationOrThrow()
        {
            if (presentationRoot == null
                || presentationRoot.parent != transform)
            {
                throw new InvalidOperationException(
                    $"LootItem '{name}' requires a direct PresentationRoot child.");
            }
        }

        private LootStateMachine EnsureStateMachine()
        {
            if (_stateMachine != null)
            {
                return _stateMachine;
            }

            _stateMachine = new LootStateMachine(initialState);
            _stateMachine.StateChanged += change =>
                StateChanged?.Invoke(change);
            return _stateMachine;
        }

        private static bool IsPickupState(LootState state)
        {
            return state == LootState.Available
                || state == LootState.Dropped
                || state == LootState.Hidden;
        }
    }
}
