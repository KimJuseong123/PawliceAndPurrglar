using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// The jeweller's key, lying where the thief has to go and get it.
    ///
    /// Its whole value is the walk. Opening a case quietly is worth a great deal
    /// — the smash carries thirty metres and is the one sound in this game the
    /// officer cannot have made himself — so the key has to cost something, and
    /// the only currency the thief has is time and exposure. Sitting it next to
    /// the case it opens would make the quiet option free and the loud one
    /// pointless.
    ///
    /// Thief-only. The officer picking it up would be denial rather than
    /// pursuit, and this game's answer to a hiding thief is to go and look.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisplayCaseKeyPickup : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private Transform presentationRoot;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;
        private bool _taken;

        public bool Taken => _taken;

        public Transform InteractionTransform => transform;

        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Loot;

        public string Prompt => "Take the key";

        public bool IsAvailable =>
            isActiveAndEnabled
            && !_taken
            && ResolveMatchState()?.IsGameplayActive == true;

        public void Configure(
            Transform configuredPresentationRoot,
            IMatchStateReader configuredMatchState)
        {
            presentationRoot = configuredPresentationRoot;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (_taken || context.Player == null)
            {
                return false;
            }

            var identity = context.Player.GetComponent<PlayerRoleIdentity>();
            if (identity == null || identity.Role != PlayerRole.Thief)
            {
                return false;
            }

            var holder =
                context.Player.GetComponent<DisplayCaseKeyHolder>();
            if (holder == null || !holder.TryTake())
            {
                return false;
            }

            _taken = true;
            ApplyTaken();
            GameLogger.Info(
                GameLogCategory.Loot,
                $"The key was taken from {transform.position}.",
                this);
            return true;
        }

        /// <summary>
        /// Puts it back for a fresh match, in the same place. A key that stayed
        /// taken would make every match after the first one loud.
        /// </summary>
        public void Reset()
        {
            _taken = false;
            ApplyTaken();
        }

        private void ApplyTaken()
        {
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(!_taken);
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
