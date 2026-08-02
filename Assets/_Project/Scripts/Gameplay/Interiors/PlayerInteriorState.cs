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
        /// How high a step the character will walk up indoors.
        ///
        /// Small. Outdoors a generous step is what gets a player over kerbs and
        /// doorsteps without jumping. Indoors it is what let them walk up onto
        /// a door threshold, from there onto a partition, and over it into a
        /// back room with no way out — a room the model draws as sealed and the
        /// collision therefore seals.
        ///
        /// A quarter of a metre still clears anything a room has on its floor.
        /// </summary>
        private const float IndoorStepOffset = 0.25f;

        /// <summary>
        /// How wide the character is indoors, as a radius in metres.
        ///
        /// Outdoors they are 0.45 across the radius, which is nine tenths of a
        /// metre of shoulder plus the controller's skin. Interior doorways are
        /// drawn about a metre and a quarter wide and the coarse collision copy
        /// of a wall bulges into that, so a doorway that is plainly open on
        /// screen is one nobody can walk through — which is exactly what was
        /// reported of the bookstore.
        ///
        /// Narrowing indoors is the honest fix for a character built for
        /// streets being asked to use domestic doors. Nothing outdoors changes.
        ///
        /// Down to 0.24 after 0.3 still would not fit the bookstore's inner
        /// doorway. Under half a metre across the shoulders is not a shape
        /// anybody reads off the screen, and the alternative is cutting
        /// triangles out of a wall that is drawn with a hole already in it.
        /// </summary>
        private const float IndoorRadius = 0.24f;

        private CharacterController _controller;
        private float _outdoorStepOffset = -1f;
        private float _outdoorRadius = -1f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_controller != null)
            {
                _outdoorStepOffset = _controller.stepOffset;
                _outdoorRadius = _controller.radius;
            }
        }

        /// <summary>
        /// Applies the step height that belongs to where the player now is.
        ///
        /// The outdoor value is remembered rather than written down, because it
        /// comes from the player config and this is not the place that decides
        /// it.
        /// </summary>
        private void ApplyStepOffset()
        {
            if (_controller == null || _outdoorStepOffset < 0f)
            {
                return;
            }

            _controller.stepOffset = IsIndoors
                ? Mathf.Min(IndoorStepOffset, _outdoorStepOffset)
                : _outdoorStepOffset;

            if (_outdoorRadius > 0f)
            {
                _controller.radius = IsIndoors
                    ? Mathf.Min(IndoorRadius, _outdoorRadius)
                    : _outdoorRadius;
            }
        }

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
            ApplyStepOffset();
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
