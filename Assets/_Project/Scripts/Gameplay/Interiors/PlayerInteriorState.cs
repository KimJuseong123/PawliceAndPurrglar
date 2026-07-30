using System;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// Which house a player is inside, or none.
    ///
    /// Needed by three unrelated things, which is why it is a state rather than a
    /// flag hidden in the doorway: the camera switches to a free view indoors, the
    /// dog reports a house instead of a position, and the doorway itself has to
    /// know whether a press means in or out.
    ///
    /// Written by the host and replicated, because the host is the only machine
    /// that moves anybody. A client that decided its own location would have the
    /// camera indoors while the host still had the character in the street.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteriorState : MonoBehaviour
    {
        /// <summary>
        /// Not in any house. Chosen over zero so a default-constructed value does
        /// not read as "inside house 0".
        /// </summary>
        public const int Outside = -1;

        [SerializeField]
        private int currentInteriorId = Outside;

        public event Action<int> InteriorChanged;

        /// <summary>
        /// Whether this machine decides where this player is.
        ///
        /// True by default so the editor scene, which has no session, works: there is
        /// nobody else to defer to. In a match the link sets it, and it is only true
        /// on the host — a client's copy of a character is moved by writing its
        /// position, so acting on a trigger there would teleport somebody the host
        /// then drags straight back.
        ///
        /// It exists because a walk-through door is not a key press. A press travels
        /// to the host through the input bridge and is authoritative for free; a
        /// collision happens wherever the collider is, on every machine at once.
        /// </summary>
        public bool HasAuthority { get; private set; } = true;

        public void SetAuthority(bool authoritative)
        {
            HasAuthority = authoritative;
        }

        public int CurrentInteriorId => currentInteriorId;
        public bool IsIndoors => currentInteriorId != Outside;

        /// <summary>
        /// Host side, and the one place the value changes.
        /// </summary>
        public void SetInterior(int interiorId)
        {
            if (currentInteriorId == interiorId)
            {
                return;
            }

            currentInteriorId = interiorId;
            InteriorChanged?.Invoke(interiorId);
        }

        /// <summary>
        /// Adopts the host's answer. Cannot be refused: the character is already
        /// standing where the host put them, and a machine that disagreed would
        /// show the street while the player walked around a room.
        /// </summary>
        public void ApplyReplicated(int interiorId)
        {
            SetInterior(interiorId);
        }
    }
}
