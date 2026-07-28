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

        /// <summary>
        /// Stable id, assigned by the scene builder. Used to name this pickup
        /// across machines: the pickups are scene objects, so the id is the same
        /// number on both without anything having to be spawned.
        /// </summary>
        [SerializeField]
        private int pickupId;

        private float _hiddenFor;
        private bool _taken;
        private bool _remoteDriven;

        /// <summary>
        /// Raised on the machine that decided, so the network layer can tell the
        /// other one. The pickup itself knows nothing about sessions.
        /// </summary>
        public event System.Action<int, bool> TakenChanged;

        public int PickupId => pickupId;
        public bool IsTaken => _taken;
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
            float configuredRespawnSeconds = 0f,
            int configuredPickupId = 0)
        {
            pickupId = configuredPickupId;
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

            SetTaken(true);
            return true;
        }

        /// <summary>
        /// Applies what the host says, on a machine that does not decide.
        ///
        /// A client that kept its own respawn clock would put the rock back at a
        /// slightly different moment and show one lying there that the host had
        /// already given away.
        /// </summary>
        public void ApplyReplicatedTaken(bool taken)
        {
            _remoteDriven = true;
            if (_taken == taken)
            {
                return;
            }

            _taken = taken;
            _hiddenFor = 0f;
            SetVisible(!taken);
        }

        private void SetTaken(bool taken)
        {
            _taken = taken;
            _hiddenFor = 0f;
            SetVisible(!taken);
            TakenChanged?.Invoke(pickupId, taken);
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
            // The respawn clock belongs to whoever decides. On a client the
            // rock comes back when the host says so.
            if (_remoteDriven || !_taken || respawnSeconds <= 0f)
            {
                return;
            }

            _hiddenFor += Time.deltaTime;
            if (_hiddenFor < respawnSeconds)
            {
                return;
            }

            SetTaken(false);
        }
    }
}
