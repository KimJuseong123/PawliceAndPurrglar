using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// A prop lying in the world that a player can pick up with the interact key.
    ///
    /// Rides the existing interaction system rather than a new one, so range,
    /// match-state gating and the on-screen prompt all behave the way loot and
    /// ladders already do.
    ///
    /// <see cref="restrictedTo"/> is what makes a pickup belong to one side. A
    /// rock in the street is fair game for either player; a banana taken off a
    /// shop shelf is the thief's, and letting the police help themselves to it
    /// would remove the point of stealing it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowablePickup : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private ThrowableKind kind = ThrowableKind.Rock;

        /// <summary>
        /// Leave unset for a pickup either side may take.
        /// </summary>
        [SerializeField]
        private bool roleRestricted;

        [SerializeField]
        private PlayerRole restrictedTo = PlayerRole.Thief;

        /// <summary>
        /// Seconds before the prop comes back after being taken. Zero removes it
        /// for the rest of the match.
        /// </summary>
        [SerializeField, Min(0f)]
        private float respawnSeconds;

        [SerializeField]
        private Transform presentationRoot;

        private float _hiddenFor;
        private bool _taken;

        public ThrowableKind Kind => kind;
        public bool IsAvailable => !_taken && isActiveAndEnabled;
        public Transform InteractionTransform => transform;

        /// <summary>
        /// Generic, not Loot.
        ///
        /// <c>PlayerRolePermissions</c> restricts Loot to the thief, so a rock
        /// reported as Loot would be invisible to the police — and a rock the
        /// police cannot pick up is not a rock. Generic is the type both roles
        /// may touch; which side may take a particular prop is decided by
        /// <see cref="roleRestricted"/> here instead, which is what lets the
        /// shop banana stay the thief's.
        /// </summary>
        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Generic;

        public string Prompt => kind == ThrowableKind.Banana
            ? "바나나 챙기기"
            : "돌 줍기";

        public void Configure(
            ThrowableKind configuredKind,
            Transform configuredPresentationRoot,
            bool configuredRoleRestricted = false,
            PlayerRole configuredRestrictedTo = PlayerRole.Thief,
            float configuredRespawnSeconds = 0f)
        {
            kind = configuredKind;
            presentationRoot = configuredPresentationRoot;
            roleRestricted = configuredRoleRestricted;
            restrictedTo = configuredRestrictedTo;
            respawnSeconds = configuredRespawnSeconds;
            _taken = false;
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null || _taken)
            {
                return false;
            }

            if (roleRestricted && context.Player.Role != restrictedTo)
            {
                return false;
            }

            ToolCarrier carrier =
                context.Player.GetComponent<ToolCarrier>();
            if (carrier == null || !carrier.TryPickUp(kind))
            {
                return false;
            }

            _taken = true;
            _hiddenFor = 0f;
            SetVisible(false);
            return true;
        }

        private void SetVisible(bool visible)
        {
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(visible);
            }
        }

        private void Update()
        {
            if (!_taken || respawnSeconds <= 0f)
            {
                return;
            }

            _hiddenFor += Time.deltaTime;
            if (_hiddenFor < respawnSeconds)
            {
                return;
            }

            _taken = false;
            SetVisible(true);
        }
    }
}
