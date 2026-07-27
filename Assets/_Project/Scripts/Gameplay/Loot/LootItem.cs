using System;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    public sealed class LootItem : MonoBehaviour, IPlayerInteractable
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

            CurrentCarrier = carrier;
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
                return;
            }

            presentationRoot.SetParent(transform, false);
            presentationRoot.position = worldPosition;
            presentationRoot.gameObject.SetActive(
                state != LootState.Sold && state != LootState.Hidden);
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null || IsRemoteControlled)
            {
                return false;
            }

            LootCarrier carrier =
                context.Player.GetComponent<LootCarrier>();
            return carrier != null && carrier.TryAcquire(this);
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
