using PawsAndLoot.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// A way out of a building that does not depend on the door working.
    ///
    /// Rooms are scanned models with collision cut from a coarse copy of
    /// themselves, and a player can end up somewhere neither the model nor the
    /// generator expected: wedged behind a partition, on the wrong side of a
    /// counter, boxed in by their own animal. Without this the only answer is
    /// to leave the match.
    ///
    /// **Ten seconds, not instantly.** An instant exit is not a safety valve,
    /// it is a movement ability: a thief cornered in the back of the jeweller's
    /// would press it and be in the street before the officer reached them, and
    /// every chase indoors would end that way. Ten seconds is longer than it
    /// takes to be caught, so it rescues the stuck without rescuing the
    /// cornered.
    ///
    /// Cancelled by leaving the building by any other means, which includes
    /// being arrested — the countdown belongs to being in a room, not to the
    /// player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorEscapeHatch : MonoBehaviour
    {
        /// <summary>
        /// How long the wait is. Long enough that it cannot win a chase.
        /// </summary>
        public const float EscapeSeconds = 10f;

        [SerializeField]
        private PlayerInteriorState interiorState;

        private float _remaining = -1f;

        /// <summary>
        /// Seconds left, or a negative number when nothing is counting. Read by
        /// the screen so the player knows the press landed.
        /// </summary>
        public float Remaining => _remaining;

        public bool IsCounting => _remaining >= 0f;

        public void Configure(PlayerInteriorState configuredState)
        {
            interiorState = configuredState;
        }

        private void Awake()
        {
            interiorState ??= GetComponent<PlayerInteriorState>();
        }

        private void Update()
        {
            if (interiorState == null || !interiorState.IsIndoors)
            {
                // Out of the building by some other route, so there is nothing
                // left to escape from.
                _remaining = -1f;
                return;
            }

            if (!IsCounting && WasRequested())
            {
                _remaining = EscapeSeconds;
                GameLogger.Info(
                    GameLogCategory.Player,
                    $"Emergency exit asked for; {EscapeSeconds:0} seconds.",
                    this);
                return;
            }

            if (!IsCounting)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
            {
                return;
            }

            _remaining = -1f;
            Escape();
        }

        /// <summary>
        /// T or G. Two keys because this is the one thing a stuck player has to
        /// be able to find, and one of them being taken by something else on
        /// somebody's layout should not leave them in the room.
        /// </summary>
        private static bool WasRequested()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.tKey.wasPressedThisFrame
                    || keyboard.gKey.wasPressedThisFrame);
        }

        private void Escape()
        {
            HouseInterior room = FindRoom(interiorState.CurrentInteriorId);
            if (room == null)
            {
                GameLogger.Warning(
                    GameLogCategory.Player,
                    "Emergency exit found no room to leave, so the player was "
                    + "left where they are.",
                    this);
                return;
            }

            InteriorTravel.Place(
                gameObject,
                room.ExitPositionFor(HouseDoorSide.Front),
                null);
            interiorState.SetInterior(PlayerInteriorState.Outside);

            GameLogger.Info(
                GameLogCategory.Player,
                $"Emergency exit from house {room.InteriorId}.",
                this);
        }

        private static HouseInterior FindRoom(int interiorId)
        {
            foreach (HouseInterior room in
                FindObjectsByType<HouseInterior>(FindObjectsSortMode.None))
            {
                if (room.InteriorId == interiorId)
                {
                    return room;
                }
            }

            return null;
        }
    }
}
