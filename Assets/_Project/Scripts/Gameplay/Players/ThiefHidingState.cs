using System;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    /// <summary>
    /// Whether the thief is inside a bin or a crate, and out of sight.
    ///
    /// Hiding is a place, not a stance: the thief can only do it at a
    /// <see cref="PlayerHidingSpot"/>, which is a thing the officer can learn the
    /// position of. A thief who could vanish anywhere would simply never be
    /// caught, and the map's four verge bins and the crates are exactly the sort
    /// of landmark a chase can be built around.
    ///
    /// While hidden the character does not move, is not drawn, and cannot be
    /// arrested. All three matter and none of them is enough alone — a thief who
    /// is invisible but still arrestable is caught by an officer waving at
    /// nothing, and one who is unarrestable but still drawn is a picture of a
    /// bug.
    ///
    /// Host-authoritative like every other rule. The interact key travels to the
    /// host and is applied there; this replicates back so the officer's screen
    /// stops drawing a thief the host says is inside a bin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThiefHidingState : MonoBehaviour
    {
        [SerializeField]
        private PlayerVisualRoot visualRoot;

        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Vector3 _returnTo;
        private bool _hasReturn;

        /// <summary>Raised on the machine that decided, so the link can tell the other.</summary>
        public event Action<bool> HidingChanged;

        public bool IsHiding { get; private set; }

        /// <summary>
        /// Which spot they are in, so a second player pressing E at the same bin
        /// knows it is occupied and the spot knows who to turf out.
        /// </summary>
        public PlayerHidingSpot CurrentSpot { get; private set; }

        /// <summary>
        /// Whether this machine decides. Same reasoning as
        /// <c>PlayerInteriorState.HasAuthority</c>: true offline so the editor
        /// scene works, set false on a client by the network link.
        /// </summary>
        public bool HasAuthority { get; private set; } = true;

        public void SetAuthority(bool authoritative)
        {
            HasAuthority = authoritative;
        }

        public void Configure(PlayerVisualRoot configuredVisualRoot)
        {
            visualRoot = configuredVisualRoot;
            _renderers = Array.Empty<Renderer>();
        }

        public bool TryEnter(PlayerHidingSpot spot)
        {
            if (IsHiding || spot == null)
            {
                return false;
            }

            _returnTo = transform.position;
            _hasReturn = true;
            CurrentSpot = spot;
            Apply(true);
            HidingChanged?.Invoke(true);
            return true;
        }

        public bool TryLeave()
        {
            if (!IsHiding)
            {
                return false;
            }

            CurrentSpot = null;
            Apply(false);
            HidingChanged?.Invoke(false);
            return true;
        }

        /// <summary>
        /// Applies what the host says, on a machine that does not decide.
        ///
        /// The position is left alone here. A client's character is moved by
        /// writing its position from the host anyway, so putting it back would
        /// fight the very stream that already says where it is.
        /// </summary>
        public void ApplyReplicated(bool hiding)
        {
            if (IsHiding == hiding)
            {
                return;
            }

            SetDrawn(!hiding);
            IsHiding = hiding;
        }

        private void Apply(bool hiding)
        {
            SetDrawn(!hiding);
            IsHiding = hiding;

            // Moved onto the bin going in, and put back on the ground coming out.
            //
            // Not left where they stood: a thief standing beside a bin they are
            // supposedly inside is the same picture as the hiding not working,
            // and the officer would still walk into them.
            if (hiding)
            {
                if (CurrentSpot != null)
                {
                    Park(CurrentSpot.OccupantPosition);
                }

                return;
            }

            // The capsule comes back on whether or not there is a point to put
            // them back at, and before anything that could return early. A thief
            // whose return point went missing walking out of a bin is a thief
            // standing in the wrong place; one whose controller stayed off cannot
            // move for the rest of the match, and would read as the game having
            // frozen.
            RestoreController();
            if (_hasReturn)
            {
                PlaceBack(_returnTo);
                _hasReturn = false;
            }
        }

        /// <summary>
        /// Puts the thief in the box, and then stops simulating them.
        ///
        /// This used to be one line — <c>transform.position = berth</c> — and
        /// every part of what replaced it is a defect that line had.
        ///
        /// **The berth is a floor, not a pivot.** A character's transform is the
        /// middle of their capsule, a metre above the soles, so assigning the
        /// berth to it put the soles a metre *under the ground*: −1.05 m at the
        /// crates, −0.69 m at the bins, measured. On an ordinary frame the
        /// controller resolves that overlap upward and nobody sees it, which is
        /// why it survived so long — but the resolution is not owed to anybody.
        /// It needs the controller to be running and the capsule to still
        /// overlap the ground, and where either is untrue the thief is simply
        /// under the world. This is the third time the same arithmetic has cost
        /// a bug (<c>ISSUE-040</c>, <c>ISSUE-044</c>), so it is asked of
        /// <see cref="InteriorTravel"/> rather than written out again.
        ///
        /// **Never below the ground they stepped in from.** The berths are
        /// authored as a drop below the bin's own origin, and one of them lands
        /// under the pavement on its own. The thief was standing within reach a
        /// moment ago, so that surface is a known-good floor height and no
        /// authored offset gets to go under it.
        ///
        /// **And the capsule goes off for the duration.** Hiding means the
        /// character does not move, and until now that was only true of the
        /// input: the motor kept applying gravity and calling
        /// <c>CharacterController.Move</c> every frame of it, because
        /// <c>CanMove</c> gates the direction and not the fall. Switching the
        /// controller off is how everything else in this project stops a
        /// character being simulated — the ladder does it while carrying
        /// somebody, the motor documents the case — and it also means there is no
        /// invisible body in the road for the officer to walk into.
        /// </summary>
        private void Park(Vector3 berth)
        {
            CharacterController controller = ResolveController();
            float feetToPivot =
                Interiors.InteriorTravel.FeetToPivot(controller, transform);
            float floor = Mathf.Max(berth.y, _returnTo.y - feetToPivot);
            Interiors.InteriorTravel.Place(
                gameObject,
                new Vector3(berth.x, floor, berth.z),
                null);

            _controllerWasEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        /// <summary>
        /// Back out onto the ground they left.
        ///
        /// The stored point is a pivot — it was read off the transform when they
        /// climbed in — so it is converted back to a floor for the placement
        /// rather than assigned raw. Going through the same helper as the way in
        /// is what clears the fall speed, which a teleport has to do: gravity
        /// keeps accumulating while the controller is off, and carried across it
        /// eventually takes the capsule through a floor between two frames.
        /// </summary>
        private void PlaceBack(Vector3 pivot)
        {
            CharacterController controller = ResolveController();
            float feetToPivot =
                Interiors.InteriorTravel.FeetToPivot(controller, transform);
            Interiors.InteriorTravel.Place(
                gameObject,
                new Vector3(pivot.x, pivot.y - feetToPivot, pivot.z),
                null,

                // No clearance on the way out. This point came off the transform
                // when they climbed in, so it is already a pivot that was
                // standing on something; six centimetres added per hide would
                // have a thief who uses the same crate twenty times climb out a
                // metre above the road.
                0f);
        }

        /// <summary>
        /// Back to what it was rather than switched on: on a machine that only
        /// displays this character the controller was already off, and turning it
        /// on there would start local physics fighting the replicated position.
        /// </summary>
        private void RestoreController()
        {
            CharacterController controller = ResolveController();
            if (controller != null)
            {
                controller.enabled = _controllerWasEnabled;
            }
        }

        private CharacterController ResolveController()
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            return _controller;
        }

        private CharacterController _controller;
        private bool _controllerWasEnabled = true;

        private void SetDrawn(bool drawn)
        {
            if (_renderers.Length == 0)
            {
                Transform root = visualRoot != null && visualRoot.VisualRoot != null
                    ? visualRoot.VisualRoot
                    : transform;
                _renderers = root.GetComponentsInChildren<Renderer>(true);
            }

            foreach (Renderer piece in _renderers)
            {
                if (piece != null)
                {
                    piece.enabled = drawn;
                }
            }

            SetCompanionDrawn(drawn);
        }

        /// <summary>
        /// The cat goes in with them.
        ///
        /// It follows at the thief's feet and never leaves, so a hidden thief with
        /// a cat sitting on the dustbin is an arrow pointing at the dustbin — the
        /// hiding would be worse than useless, because the officer would learn to
        /// read the cat rather than look for the thief.
        ///
        /// Found rather than assigned. The companion is spawned by a different
        /// part of the scene builder and paired with the player at runtime, and a
        /// serialized reference across that boundary is the kind that comes back
        /// from a prefab as null.
        /// </summary>
        private void SetCompanionDrawn(bool drawn)
        {
            if (_companion == null)
            {
                var identity = GetComponent<PlayerRoleIdentity>();
                foreach (Companions.CompanionAgent candidate in
                    FindObjectsByType<Companions.CompanionAgent>(
                        FindObjectsSortMode.None))
                {
                    bool isCat = candidate.CompanionKind
                        == Companions.CompanionKind.Cat;
                    if (identity != null
                        && (identity.Role == PlayerRole.Thief) == isCat)
                    {
                        _companion = candidate.transform;
                        break;
                    }
                }
            }

            if (_companion == null)
            {
                return;
            }

            if (_companionRenderers.Length == 0)
            {
                _companionRenderers =
                    _companion.GetComponentsInChildren<Renderer>(true);
            }

            foreach (Renderer piece in _companionRenderers)
            {
                if (piece != null)
                {
                    piece.enabled = drawn;
                }
            }
        }

        private Transform _companion;
        private Renderer[] _companionRenderers = Array.Empty<Renderer>();
    }
}
