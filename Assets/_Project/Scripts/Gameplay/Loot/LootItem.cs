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

        private LootStateMachine _stateMachine;

        public event Action<LootStateChanged> StateChanged;

        public LootDefinition Definition => definition;
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
            LootState configuredInitialState = LootState.Available)
        {
            definition = configuredDefinition;
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

        internal bool TryAcquire(LootCarrier carrier)
        {
            if (carrier == null
                || CurrentCarrier != null
                || !IsPickupState(CurrentState))
            {
                return false;
            }

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
            EnsureStateMachine();
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
