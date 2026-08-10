using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    /// <summary>
    /// A bin or a crate the thief can climb into with the interact key.
    ///
    /// The thief presses E to get in and E again to get out. The officer presses
    /// E at an occupied one to turf them out — without that, a thief who found a
    /// bin could sit in it for the rest of the match and the officer's whole half
    /// of the game would be over. It is one press against one press, and the
    /// officer has to guess which of the town's bins.
    ///
    /// <see cref="PlayerInteractionType.Generic"/>, because both roles have
    /// business here and the role check is written out in
    /// <see cref="TryInteract"/> rather than delegated to the permission table —
    /// the table answers per type, and this type means two different things
    /// depending on who is standing there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHidingSpot :
        MonoBehaviour,
        IPlayerInteractable,
        IRoleAwareInteractable
    {
        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Transform occupantAnchor;

        [SerializeField, Min(0.1f)]
        private float interactionRadius = 1.4f;

        private IMatchStateReader _matchState;

        /// <summary>Who is inside, or null.</summary>
        public ThiefHidingState Occupant { get; private set; }

        public bool IsOccupied => Occupant != null && Occupant.IsHiding;

        /// <summary>
        /// Where the hidden character is parked — a point on the floor for their
        /// **feet**, not a position for their transform.
        ///
        /// It matters which, and it is written down here because getting it wrong
        /// is silent: a character's transform is the middle of their capsule, so
        /// treating this as a transform position drops the soles a metre through
        /// the ground. <see cref="ThiefHidingState"/> did exactly that, and the
        /// controller quietly undid it on most frames.
        ///
        /// Sunk below the bin's own origin rather than balanced on the lid. The
        /// drop is a request, not a guarantee — the state clamps it to the ground
        /// the thief stepped in from, because two of these berths are authored
        /// under the pavement.
        /// </summary>
        public Vector3 OccupantPosition =>
            occupantAnchor != null
                ? occupantAnchor.position
                : transform.position;

        public Transform InteractionTransform => transform;

        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Generic;

        public string Prompt => IsOccupied ? "끌어내기" : "숨기";

        public bool IsAvailable =>
            isActiveAndEnabled
            && ResolveMatchState()?.IsGameplayActive == true;

        /// <summary>
        /// The officer is only offered an occupied one.
        ///
        /// <see cref="TryInteract"/> has always refused an empty box for the
        /// police — it returns <c>IsOccupied &amp;&amp; Turf()</c> — but the
        /// prompt did not know that, so the officer was told "숨기" at every
        /// empty box in the town and pressing did nothing. Hiding is the thief's
        /// alone, and turfing out only exists when there is somebody to turf.
        /// </summary>
        public bool IsAvailableFor(PlayerRole role)
        {
            return role != PlayerRole.Police || IsOccupied;
        }

        public void Configure(
            IMatchStateReader configuredMatchState,
            Transform configuredAnchor,
            float configuredRadius = 1.4f)
        {
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            occupantAnchor = configuredAnchor;
            interactionRadius = Mathf.Max(0.1f, configuredRadius);
            EnsureInteractionCollider();
        }

        private void Awake()
        {
            EnsureInteractionCollider();
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null || !IsAvailable)
            {
                return false;
            }

            if (context.Role == PlayerRole.Police)
            {
                // Nothing to say when it is empty. An officer who got a message
                // for every empty bin would learn to ignore the message, which is
                // the one they need when a bin is not empty.
                return IsOccupied && Turf();
            }

            var hiding = context.Player.GetComponent<ThiefHidingState>();
            if (hiding == null || !hiding.HasAuthority)
            {
                return false;
            }

            if (hiding.IsHiding)
            {
                return ReferenceEquals(hiding.CurrentSpot, this) && Turf();
            }

            if (IsOccupied || !hiding.TryEnter(this))
            {
                return false;
            }

            Occupant = hiding;
            return true;
        }

        private bool Turf()
        {
            ThiefHidingState occupant = Occupant;
            Occupant = null;
            return occupant != null && occupant.TryLeave();
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            _matchState ??= FindFirstObjectByType<MatchRuntimeState>();
            return _matchState;
        }

        /// <summary>
        /// A trigger the scanner can find, added rather than authored.
        ///
        /// The bins are a solid cube with a model fitted inside it, and a solid
        /// collider is not what the scanner ranks — it ranks anything it overlaps,
        /// and a body cannot overlap the inside of a closed box. Standing next to
        /// one has to be enough.
        /// </summary>
        private void EnsureInteractionCollider()
        {
            foreach (Collider existing in GetComponents<Collider>())
            {
                if (existing.isTrigger)
                {
                    return;
                }
            }

            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = interactionRadius;
        }
    }
}
