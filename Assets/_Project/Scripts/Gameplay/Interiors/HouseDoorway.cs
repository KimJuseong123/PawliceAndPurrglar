using PawsAndLoot.Animation;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// A door that takes a player between the street and a house's inside.
    ///
    /// Rides the interaction system rather than a walk-through trigger. Three
    /// reasons, in order of weight: interaction is already routed through the host
    /// so the move is authoritative for free; a press cannot be triggered by
    /// accident while running past, which matters when the wrong side of a door is
    /// a death sentence in a chase; and the prompt tells the player the door is a
    /// door without a tutorial.
    ///
    /// One of these sits outside each enterable house and one inside each room, so
    /// the same component handles both directions — the difference is only which
    /// position it sends you to.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseDoorway : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private HouseInterior interior;

        /// <summary>
        /// True for the door in the street, false for the one in the room.
        /// </summary>
        [SerializeField]
        private bool leadsInside = true;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private HouseDoorLeaf leaf;

        private IMatchStateReader _matchState;

        public bool LeadsInside => leadsInside;
        public HouseInterior Interior => interior;

        public bool IsAvailable =>
            isActiveAndEnabled
            && interior != null
            && ResolveMatchState()?.IsGameplayActive == true;

        public Transform InteractionTransform => transform;

        /// <summary>
        /// Generic, so both roles may use it.
        ///
        /// Loot is thief-only and a door typed as Loot would be invisible to the
        /// officer — who has to be able to follow the thief inside, or the room is
        /// a hiding place with no risk attached.
        /// </summary>
        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Generic;

        public string Prompt =>
            leadsInside ? "집에 들어가기" : "집에서 나가기";

        public void Configure(
            HouseInterior configuredInterior,
            bool configuredLeadsInside,
            IMatchStateReader configuredMatchState,
            HouseDoorLeaf configuredLeaf = null)
        {
            interior = configuredInterior;
            leadsInside = configuredLeadsInside;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            leaf = configuredLeaf;
        }

        /// <summary>
        /// Host side. Moves the player and records where they now are.
        ///
        /// The <see cref="CharacterController"/> is switched off around the move:
        /// a controller resists being placed and would spend the next frames
        /// walking back toward where it thought it was.
        /// </summary>
        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null || !IsAvailable)
            {
                return false;
            }

            var state =
                context.Player.GetComponent<PlayerInteriorState>();
            if (state == null)
            {
                return false;
            }

            // Refuse the door that does not apply. Pressing the street door while
            // already indoors would teleport somebody from one room into the same
            // room, which reads as the press doing nothing.
            if (leadsInside == state.IsIndoors)
            {
                return false;
            }

            Vector3 destination = leadsInside
                ? interior.EntryPosition
                : interior.ExitPosition;

            Transform player = context.Player.transform;
            CharacterController controller =
                context.Player.GetComponent<CharacterController>();
            bool hadController = controller != null && controller.enabled;
            if (hadController)
            {
                controller.enabled = false;
            }

            // Feet on the floor, not the pivot on the floor.
            //
            // A character's transform sits about 0.9 m above their soles, so
            // putting the transform on the surface buries the whole capsule in it.
            // That is nearly a metre of penetration for the controller to sort out
            // on the frame it wakes up, and it sorts it out downward: the player
            // dropped straight through the floor of every room, immediately. The
            // earlier accumulated-fall-speed fix was a real bug and a different
            // one, which is why this survived it.
            //
            // From the capsule's own numbers, not its bounds: the controller has
            // just been switched off and a disabled collider's bounds are not
            // reliable. Read rather than assumed either way, because the two
            // characters are different models and neither figure belongs here.
            float feetToPivot = controller != null
                ? (controller.height * 0.5f - controller.center.y)
                    * Mathf.Abs(player.lossyScale.y)
                : 0f;
            player.position = destination
                + Vector3.up * (feetToPivot + 0.06f);
            if (hadController)
            {
                controller.enabled = true;
            }

            Physics.SyncTransforms();

            // The fall speed has to go with the old position.
            //
            // Gravity accumulates every frame the controller is not grounded, and
            // a teleport leaves it briefly airborne. Going in and out of a door a
            // few times built that speed up until it carried the capsule through
            // the floor between two frames — which is exactly the sinking that
            // was reported after using a door repeatedly.
            context.Player.GetComponent<PlayerMovementMotor>()
                ?.ResetVerticalVelocity();

            state.SetInterior(
                leadsInside ? interior.InteriorId : PlayerInteriorState.Outside);
            leaf?.Swing();

            GameLogger.Info(
                GameLogCategory.Player,
                $"{context.Player.Role} went "
                + $"{(leadsInside ? "into" : "out of")} house "
                + $"{interior.InteriorId}.",
                this);
            return true;
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
