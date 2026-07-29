using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// The front door swinging open.
    ///
    /// The house model is one flat list of 200-odd named parts, so the leaf, its
    /// handle, panels and glass are five separate objects that all have to turn
    /// together about one edge.
    ///
    /// The obvious way to do that is to gather them onto a hinge object, and that
    /// is how this started. It worked and it was expensive: a prefab instance
    /// refuses to have its children reparented, so every house had to be unpacked
    /// first, and unpacking writes all 186 of its parts into the scene as real
    /// objects instead of one reference. Eight houses took <c>Game.unity</c> from
    /// 3.2 MB to 9.2 MB, and because the scene is regenerated wholesale, every
    /// rebuild committed another 9 MB of it.
    ///
    /// So nothing is reparented. The parts stay where the prefab put them and this
    /// turns each one about a shared point instead, which is the same arithmetic a
    /// hinge would have done. The houses stay packed.
    ///
    /// Two things are deliberately resolved here rather than handed over:
    ///
    /// <list type="number">
    /// <item>The parts are found by name at load. A list filled by an editor
    /// script is the trap that has already killed the lobby buttons, the leg
    /// animator and the sensor arcs.</item>
    /// <item>The hinge point is measured from the leaf itself, so it is right for
    /// whatever rotation the house happens to have.</item>
    /// </list>
    ///
    /// <c>SceneOptimizationPass</c> still has to leave these five renderers out of
    /// static batching — a baked renderer does not move however far it is turned,
    /// with nothing in any log to say so. It asks <see cref="MovesTransform"/>
    /// rather than matching names.
    ///
    /// Presentation only. The doorway decides who goes where; this is the part
    /// that makes the door look like a door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseDoorLeaf : MonoBehaviour
    {
        /// <summary>
        /// The parts of the front door, in the model's own naming. In code because
        /// code survives into the build; a serialised list of transforms would not
        /// have to.
        /// </summary>
        private static readonly string[] PartNames =
        {
            "BD_House1F_Door_Front_Leaf",
            "BD_House1F_Door_Front_Handle",
            "BD_House1F_Door_Front_Panel_1",
            "BD_House1F_Door_Front_Panel_2",
            "BD_House1F_Door_Front_VerticalGlass"
        };

        /// <summary>
        /// The part whose edge the door turns on. The others follow it.
        /// </summary>
        private const string LeafName = "BD_House1F_Door_Front_Leaf";

        [SerializeField]
        private Transform houseRoot;

        /// <summary>
        /// Signed, because which way is "outward" depends on the wall the door is
        /// in. Negative opens away from the house: a positive turn about up carries
        /// the free edge from +X toward -Z, which for a door on the +Z wall is
        /// inward.
        /// </summary>
        [SerializeField]
        private float openDegrees = -95f;

        [SerializeField, Min(0.05f)]
        private float openSeconds = 0.35f;

        /// <summary>
        /// How long it stays open after somebody uses it. A door that shuts
        /// instantly never reads as having opened at all.
        /// </summary>
        [SerializeField, Min(0.1f)]
        private float holdSeconds = 1.4f;

        private readonly List<Transform> _parts = new();
        private readonly List<Vector3> _closedOffsets = new();
        private readonly List<Quaternion> _closedRotations = new();

        /// <summary>
        /// The hinge, in the house's own space rather than the world's.
        ///
        /// This is the part that was wrong before, and it was wrong before the
        /// parts stopped being reparented: the edge was picked with
        /// <c>bounds.min.x</c>, a world axis. On a house turned to face the other
        /// way that is the far edge of the leaf, so those doors hinged on the wrong
        /// side and swung into the building. Nothing said so — the door opened,
        /// just the wrong way, on some of the houses.
        /// </summary>
        private Vector3 _pivotLocal;
        private bool _resolved;
        private float _openUntil;
        private float _blend;

        public bool IsOpen => _blend > 0.05f;
        public float OpenAmount => _blend;

        /// <summary>
        /// The point the door turns on, in world space. Exposed so a test can check
        /// it sits at the leaf's edge rather than its middle.
        /// </summary>
        public Vector3 HingePoint
        {
            get
            {
                Resolve();
                return houseRoot != null
                    ? houseRoot.TransformPoint(_pivotLocal)
                    : _pivotLocal;
            }
        }

        /// <summary>
        /// How many parts were found. Zero means the door will not appear to open,
        /// which is worth being able to assert: the failure is silent otherwise.
        /// </summary>
        public int PartCount
        {
            get
            {
                Resolve();
                return _parts.Count;
            }
        }

        public void Configure(
            Transform configuredHouseRoot,
            float configuredOpenDegrees)
        {
            houseRoot = configuredHouseRoot;
            openDegrees = configuredOpenDegrees;
            _resolved = false;
        }

        /// <summary>
        /// True for a transform this moves, so the batching pass can exclude it by
        /// construction instead of by name.
        /// </summary>
        public bool MovesTransform(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            Resolve();
            return _parts.Contains(candidate);
        }

        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _parts.Clear();
            _closedOffsets.Clear();
            _closedRotations.Clear();
            if (houseRoot == null)
            {
                return;
            }

            Transform leaf = null;
            var found = new List<Transform>();
            foreach (Transform candidate in
                houseRoot.GetComponentsInChildren<Transform>(true))
            {
                foreach (string name in PartNames)
                {
                    if (candidate.name != name)
                    {
                        continue;
                    }

                    found.Add(candidate);
                    if (name == LeafName)
                    {
                        leaf = candidate;
                    }

                    break;
                }
            }

            Renderer leafRenderer = leaf != null
                ? leaf.GetComponent<Renderer>()
                : null;
            if (leafRenderer == null)
            {
                // Without the leaf there is no edge to turn on, and swinging the
                // handle on its own would look worse than a door that stays shut.
                return;
            }

            // The left edge, so it opens across the porch rather than through
            // whoever is standing at it.
            //
            // Measured in the house's space, which is the whole point. The mesh's
            // own box is put through the leaf and then back into the house, so
            // "left" means the left of the building however the building is turned.
            // Reading the world-space box instead picks the far edge on any house
            // facing the other way.
            Matrix4x4 leafToHouse = houseRoot.worldToLocalMatrix
                * leafRenderer.transform.localToWorldMatrix;
            Bounds mesh = leafRenderer.localBounds;
            var localBox = new Bounds(
                leafToHouse.MultiplyPoint3x4(mesh.center),
                Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);
                localBox.Encapsulate(
                    leafToHouse.MultiplyPoint3x4(
                        mesh.center
                        + Vector3.Scale(mesh.extents, sign)));
            }

            _pivotLocal = new Vector3(
                localBox.min.x,
                localBox.min.y,
                localBox.center.z);

            foreach (Transform part in found)
            {
                _parts.Add(part);
                _closedOffsets.Add(
                    houseRoot.InverseTransformPoint(part.position)
                    - _pivotLocal);
                _closedRotations.Add(
                    Quaternion.Inverse(houseRoot.rotation) * part.rotation);
            }
        }

        /// <summary>
        /// Called on every machine — the door opening is a local consequence of a
        /// move the host already decided, so there is nothing to replicate.
        /// </summary>
        public void Swing()
        {
            _openUntil = Time.time + holdSeconds;
        }

        private void Awake()
        {
            Resolve();
        }

        private void LateUpdate()
        {
            Resolve();
            if (_parts.Count == 0 || houseRoot == null)
            {
                return;
            }

            float target = Time.time < _openUntil ? 1f : 0f;
            float previous = _blend;
            _blend = Mathf.MoveTowards(
                _blend,
                target,
                Time.deltaTime / openSeconds);

            // Shut and staying shut. Writing the closed pose every frame would
            // fight anything else that ever wanted to move the house.
            if (_blend <= 0f && previous <= 0f)
            {
                return;
            }

            // Entirely in the house's space, then put back into the world. A door
            // opens away from its own building, so every term — the hinge, the
            // offsets and the axis — has to be the building's, not the world's.
            Quaternion swing = Quaternion.AngleAxis(
                openDegrees * _blend,
                Vector3.up);
            for (int index = 0; index < _parts.Count; index++)
            {
                if (_parts[index] == null)
                {
                    continue;
                }

                _parts[index].SetPositionAndRotation(
                    houseRoot.TransformPoint(
                        _pivotLocal + swing * _closedOffsets[index]),
                    houseRoot.rotation
                        * swing
                        * _closedRotations[index]);
            }
        }
    }
}
