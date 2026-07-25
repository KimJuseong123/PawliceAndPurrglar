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

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null)
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
