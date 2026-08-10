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
            if (hiding && CurrentSpot != null)
            {
                transform.position = CurrentSpot.OccupantPosition;
                return;
            }

            if (!hiding && _hasReturn)
            {
                transform.position = _returnTo;
                _hasReturn = false;
            }
        }

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
