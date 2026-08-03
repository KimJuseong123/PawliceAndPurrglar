using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// A small valuable lying in a house. Picked up with the same key as anything
    /// else and turned straight into money.
    ///
    /// No carrying and no trip to the merchant, unlike a treasure. That is the
    /// whole shape of the choice: a room is a quick, small, certain gain, and a
    /// treasure is a slow, large, risky one that has to be hauled across town past
    /// somebody hunting you. If pocketing paid as much as selling, nobody would
    /// ever carry anything again.
    ///
    /// <see cref="PlayerInteractionType.Loot"/>, which
    /// <c>PlayerRolePermissions</c> already restricts to the thief — the officer
    /// following them into a room is there to arrest them, not to loot the place.
    /// No role flag needed; the type does it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorValuablePickup
        : MonoBehaviour, IPlayerInteractable, IHoldInteractable
    {
        [SerializeField]
        private int sourceId;

        /// <summary>
        /// Small on purpose, and the total across every room is deliberately less
        /// than the target: a thief who empties every house still has to sell real
        /// treasure to win, so the rooms are a supplement rather than a way to skip
        /// the game.
        /// </summary>
        [SerializeField, Min(1)]
        private int value = 50;

        [SerializeField, Min(0.1f)]
        private float holdDurationSeconds = 1.15f;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Transform presentationRoot;

        private IMatchStateReader _matchState;
        private bool _taken;

        /// <summary>
        /// Raised on the machine that decided, so the network layer can tell the
        /// other one. The pickup itself knows nothing about sessions.
        /// </summary>
        public event System.Action<int> Taken;

        public int SourceId => sourceId;
        public int Value => value;
        public bool IsTaken => _taken;

        public bool IsAvailable =>
            !_taken
            && isActiveAndEnabled
            && ResolveMatchState()?.IsGameplayActive == true;

        public Transform InteractionTransform => transform;

        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Loot;

        public string Prompt => $"뒤지기 ({value}골드)";
        public float HoldDurationSeconds => holdDurationSeconds;

        public void Configure(
            int configuredSourceId,
            int configuredValue,
            IMatchStateReader configuredMatchState,
            Transform configuredPresentationRoot)
        {
            sourceId = configuredSourceId;
            value = Mathf.Max(1, configuredValue);
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            presentationRoot = configuredPresentationRoot;
            _taken = false;
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            return TryCompletePickup(context);
        }

        public bool CanBeginHold(PlayerInteractionContext context)
        {
            return CanCompletePickup(context);
        }

        public bool CompleteHold(PlayerInteractionContext context)
        {
            return TryCompletePickup(context);
        }

        public void CancelHold(PlayerInteractionContext context)
        {
        }

        private bool CanCompletePickup(PlayerInteractionContext context)
        {
            if (context.Player == null || !IsAvailable)
            {
                return false;
            }

            var wallet = context.Player.GetComponent<ThiefLootWallet>();
            if (wallet == null)
            {
                return false;
            }

            return true;
        }

        private bool TryCompletePickup(PlayerInteractionContext context)
        {
            if (!CanCompletePickup(context))
            {
                return false;
            }

            var wallet = context.Player.GetComponent<ThiefLootWallet>();

            // The wallet refuses a second credit for the same shelf, so a press
            // that arrives after the first one pays nothing and takes nothing.
            if (!wallet.TryCreditCash(value, sourceId))
            {
                return false;
            }

            SetTaken(true);
            Taken?.Invoke(sourceId);
            return true;
        }

        /// <summary>
        /// Applies what the host says, on a machine that does not decide.
        ///
        /// Without this the shelf stays full on the other screen while the money
        /// has already moved — the same split that made a picked-up rock look like
        /// a rock that would not pick up.
        /// </summary>
        public void ApplyReplicatedTaken(bool taken)
        {
            SetTaken(taken);
        }

        private void SetTaken(bool taken)
        {
            if (_taken == taken)
            {
                return;
            }

            _taken = taken;
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(!taken);
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
