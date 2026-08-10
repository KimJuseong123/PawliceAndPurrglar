using System.Collections.Generic;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Interiors
{
    /// <summary>
    /// A room's four outside faces, each one removable as a whole.
    ///
    /// The first version registered the wall panel and nothing else, so hiding a side
    /// left its window frames, mullions, siding slats and shutters standing there —
    /// the player was looking at their character through a cage of bars. A side of a
    /// house is not one mesh; on this model it is the wall plus forty-odd pieces
    /// bolted to it.
    ///
    /// Which pieces belong to which side is worked out from where they are, not from
    /// what they are called. The room measures its own inside once, and anything
    /// beyond that box on an axis is on that face. A name list would have to be
    /// extended every time the model gains a part, and would fail silently when it
    /// was not — which is how the bars got there.
    ///
    /// Resolved at load rather than recorded at build time. The alternative is a
    /// serialised array of renderers per side, and lists filled by an editor script
    /// are the trap that has already killed the lobby buttons, the leg animator and
    /// the sensor arcs. Only two numbers are stored, and numbers always survive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorShellScreen : MonoBehaviour
    {
        /// <summary>
        /// The four faces, in a fixed order so a side can be named by an index.
        /// </summary>
        public static readonly Vector3[] Outward =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left
        };

        /// <summary>
        /// Where the middle of the room actually is, relative to this object.
        ///
        /// Not zero, and that was a bug worth the trouble it caused. The model is
        /// re-centred on its whole silhouette when it is placed, and that silhouette
        /// includes the porch sticking out of the front — so the walls sit about a
        /// metre behind this object's origin. Measuring from the origin put the front
        /// wall <b>inside</b> the room by that margin, and the entire front face,
        /// door and siding and shutters, belonged to no face at all. The back worked
        /// perfectly, which is exactly why it read as a door problem.
        /// </summary>
        [SerializeField]
        private Vector3 innerCentreOffset;

        /// <summary>
        /// Half the room's inside, measured by the builder from the wall positions.
        /// A part further out than this on an axis is part of that face.
        /// </summary>
        [SerializeField]
        private Vector2 innerHalfExtents = new(8f, 6f);

        /// <summary>
        /// Ignore anything below this. The floor slab and the foundation sit under the
        /// room and would otherwise be claimed by whichever face they reach past.
        /// </summary>
        [SerializeField]
        private float floorTop;

        private readonly List<Renderer>[] _faces =
        {
            new(), new(), new(), new()
        };

        private readonly bool[] _hidden = new bool[4];
        private bool _resolved;

        public int FaceCount => Outward.Length;

        /// <summary>
        /// The middle of the room in world space. Used for classifying parts and for
        /// deciding which face the camera is behind, so that both agree.
        /// </summary>
        public Vector3 Centre => transform.position + innerCentreOffset;

        public void Configure(
            Vector3 configuredCentreOffset,
            Vector2 configuredInner,
            float configuredFloorTop)
        {
            innerCentreOffset = configuredCentreOffset;
            innerHalfExtents = configuredInner;
            floorTop = configuredFloorTop;
            _resolved = false;
        }

        /// <summary>
        /// How many renderers a face is made of. Worth being able to read: a face of
        /// one is the bug this class exists for, and it looked fine in every log.
        /// </summary>
        public int PartsOn(int face)
        {
            Resolve();
            return face >= 0 && face < _faces.Length
                ? _faces[face].Count
                : 0;
        }

        /// <summary>
        /// The parts a face is made of, so a report can list what was assigned and,
        /// by subtraction, what was left behind. Exposed for measurement: the bug
        /// this class exists for is a part quietly not belonging to any face.
        /// </summary>
        public IReadOnlyList<Renderer> PartsOf(int face)
        {
            Resolve();
            return face >= 0 && face < _faces.Length
                ? _faces[face]
                : System.Array.Empty<Renderer>();
        }

        public bool IsHidden(int face)
        {
            return face >= 0 && face < _hidden.Length && _hidden[face];
        }

        /// <summary>
        /// Where a face is, for deciding which one the camera is behind. Averaged over
        /// its parts rather than taken from one of them.
        /// </summary>
        public Vector3 CentreOf(int face)
        {
            Resolve();
            if (face < 0 || face >= _faces.Length || _faces[face].Count == 0)
            {
                return Centre;
            }

            Vector3 total = Vector3.zero;
            foreach (Renderer part in _faces[face])
            {
                total += part.bounds.center;
            }

            return total / _faces[face].Count;
        }

        public void SetHidden(int face, bool hidden)
        {
            Resolve();
            if (face < 0 || face >= _faces.Length || _hidden[face] == hidden)
            {
                return;
            }

            _hidden[face] = hidden;
            foreach (Renderer part in _faces[face])
            {
                if (part != null)
                {
                    part.enabled = !hidden;
                }
            }
        }

        public void ShowEverything()
        {
            for (int face = 0; face < _faces.Length; face++)
            {
                SetHidden(face, false);
            }
        }

        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            foreach (List<Renderer> face in _faces)
            {
                face.Clear();
            }

            Vector3 centre = Centre;
            foreach (Renderer part in
                GetComponentsInChildren<Renderer>(true))
            {
                // Already switched off by the builder: the full-height partitions and
                // the interior door frames it replaced with 2 m greybox. Claiming
                // those would switch them back on when a face was revealed.
                if (!part.enabled)
                {
                    continue;
                }

                Bounds bounds = part.bounds;
                if (bounds.max.y <= floorTop + 0.05f)
                {
                    continue;
                }

                float outX = Mathf.Abs(bounds.center.x - centre.x)
                    - innerHalfExtents.x;
                float outZ = Mathf.Abs(bounds.center.z - centre.z)
                    - innerHalfExtents.y;
                if (outX <= 0f && outZ <= 0f)
                {
                    // Inside the room: furniture, the low partitions, the floors.
                    continue;
                }

                // A corner piece reaches past on both axes. It goes with whichever
                // face it reaches further past, so that it leaves with that face
                // rather than with neither.
                int face;
                if (outZ >= outX)
                {
                    face = bounds.center.z > centre.z ? 0 : 1;
                }
                else
                {
                    face = bounds.center.x > centre.x ? 2 : 3;
                }

                _faces[face].Add(part);
            }
        }
    }
}
