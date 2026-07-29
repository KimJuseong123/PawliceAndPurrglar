using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// The front door swinging open.
    ///
    /// The house model is one flat list of 200-odd named parts, so the leaf, its
    /// handle, panels and glass are separate objects that have to be gathered onto
    /// a hinge before any of them can turn. Two traps apply, both of which have
    /// already cost a playtest on the bin lid:
    ///
    /// <list type="number">
    /// <item>A prefab instance refuses to have its children reparented, so the
    /// model has to be unpacked first.</item>
    /// <item><c>SceneOptimizationPass</c> bakes renderers <c>BatchingStatic</c>,
    /// and a baked renderer does not move however far its transform turns.</item>
    /// </list>
    ///
    /// Presentation only. The doorway decides who goes where; this is the part
    /// that makes the door look like a door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseDoorLeaf : MonoBehaviour
    {
        [SerializeField]
        private Transform hinge;

        /// <summary>
        /// Signed, because which way is "outward" depends on the wall the door is
        /// in. Measured by the builder rather than assumed.
        /// </summary>
        [SerializeField]
        private float openDegrees = 95f;

        [SerializeField, Min(0.05f)]
        private float openSeconds = 0.35f;

        /// <summary>
        /// How long it stays open after somebody uses it. A door that shuts
        /// instantly never reads as having opened at all.
        /// </summary>
        [SerializeField, Min(0.1f)]
        private float holdSeconds = 1.4f;

        private Quaternion _closed;
        private float _openUntil;
        private float _blend;

        /// <summary>
        /// Exposed so <c>SceneOptimizationPass</c> can exclude it from static
        /// batching by construction rather than by name.
        /// </summary>
        public Transform Hinge => hinge;

        public bool IsOpen => _blend > 0.05f;
        public float OpenAmount => _blend;

        public void Configure(
            Transform configuredHinge,
            float configuredOpenDegrees)
        {
            hinge = configuredHinge;
            openDegrees = configuredOpenDegrees;
            if (hinge != null)
            {
                _closed = hinge.localRotation;
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
            if (hinge != null)
            {
                _closed = hinge.localRotation;
            }
        }

        private void LateUpdate()
        {
            if (hinge == null)
            {
                return;
            }

            float target = Time.time < _openUntil ? 1f : 0f;
            _blend = Mathf.MoveTowards(
                _blend,
                target,
                Time.deltaTime / openSeconds);
            hinge.localRotation = _closed
                * Quaternion.Euler(0f, openDegrees * _blend, 0f);
        }
    }
}
