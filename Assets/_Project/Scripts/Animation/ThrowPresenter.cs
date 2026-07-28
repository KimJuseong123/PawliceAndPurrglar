using PawsAndLoot.Gameplay.Items;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// What a throw looks like: the arm that swings and the prop that flies.
    ///
    /// Both halves of one moment, so they live together — splitting them would
    /// need a third object to keep the swing and the launch on the same frame.
    ///
    /// Cosmetic replay of a decision already made. The host resolved the throw
    /// before this runs and the outcome is fixed; the rock is a dumb child
    /// object flying from the reported origin to the reported landing, with no
    /// collider and nothing reading it. It cannot hit anybody, which is exactly
    /// why it is safe to run on both machines at slightly different times.
    ///
    /// Built at runtime, like the other views here, so
    /// <c>SceneOptimizationPass</c> has nothing to bake static.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowPresenter : MonoBehaviour
    {
        [SerializeField]
        private ToolUseAction source;

        [SerializeField]
        private Transform skeletonRoot;

        [SerializeField]
        private Material propMaterial;

        /// <summary>
        /// Arm timings. Short — a throw the player has to wait out would make
        /// the rock feel like a spell, and the host has already resolved the hit
        /// by the time the arm starts moving.
        /// </summary>
        [SerializeField, Min(0.01f)]
        private float windupSeconds = 0.11f;

        [SerializeField, Min(0.01f)]
        private float releaseSeconds = 0.09f;

        [SerializeField, Min(0.01f)]
        private float recoverSeconds = 0.22f;

        /// <summary>
        /// How far the upper arm rotates at full windup. Signed by measurement,
        /// not by guessing: the two arms on these rigs mirror each other, and
        /// assuming a direction is what once had the raccoon waving a hand it
        /// was holding at its knee.
        /// </summary>
        [SerializeField]
        private float windupDegrees = 95f;

        /// <summary>
        /// How far past rest the arm carries through on release. This overshoot
        /// is the part that reads as throwing rather than pointing.
        /// </summary>
        [SerializeField]
        private float followThroughDegrees = 55f;

        [SerializeField, Min(1f)]
        private float propSpeed = 24f;

        /// <summary>
        /// Taken from the catalog, which derives the hit corridor from the same
        /// number. A pea-sized pebble sweeping a character-wide corridor would
        /// make every graze look like a miss that somehow counted.
        /// </summary>
        [SerializeField, Min(0.05f)]
        private float propDiameter =
            ThrowableCatalog.PropDiameterMeters;

        private Transform _upperArm;
        private Transform _forearm;
        private Quaternion _upperRest;
        private Quaternion _forearmRest;
        private Vector3 _liftAxis = Vector3.right;
        private float _liftSign = 1f;

        private Transform _prop;
        private Vector3 _propFrom;
        private Vector3 _propTo;
        private float _propElapsed;
        private float _propDuration;
        private bool _propFlying;

        private float _armElapsed = -1f;

        public bool IsSwinging => _armElapsed >= 0f;
        public bool IsPropFlying => _propFlying;
        public bool HasArm => _upperArm != null;

        public void Configure(
            ToolUseAction configuredSource,
            Transform configuredSkeletonRoot,
            Material configuredPropMaterial)
        {
            source = configuredSource;
            skeletonRoot = configuredSkeletonRoot;
            propMaterial = configuredPropMaterial;
        }

        private void Awake()
        {
            // Collected here rather than in the scene builder because the bones
            // are not serialized and an editor-time lookup would not survive
            // into the build — the same way the companion leg animator silently
            // did nothing until it grew an Awake.
            CollectArm();
        }

        private void OnEnable()
        {
            if (source != null)
            {
                source.Thrown += HandleThrown;
            }
        }

        private void OnDisable()
        {
            if (source != null)
            {
                source.Thrown -= HandleThrown;
            }

            RestoreArm();
        }

        /// <summary>
        /// The throwing arm, preferring the right and falling back to the left,
        /// with the twist helpers skipped: they barely move and rotating one
        /// shears the mesh instead of swinging the limb.
        /// </summary>
        private void CollectArm()
        {
            if (skeletonRoot == null)
            {
                skeletonRoot = transform;
            }

            _upperArm = FindBone("R_Upperarm") ?? FindBone("L_Upperarm");
            _forearm = FindBone("R_Forearm") ?? FindBone("L_Forearm");
            if (_upperArm == null)
            {
                return;
            }

            _upperRest = _upperArm.localRotation;
            if (_forearm != null)
            {
                _forearmRest = _forearm.localRotation;
            }

            MeasureLiftAxis();
        }

        private Transform FindBone(string boneName)
        {
            foreach (Transform bone in
                skeletonRoot.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.IndexOf(
                        "Twist",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (bone.name == boneName)
                {
                    return bone;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds which local axis of the upper arm actually raises the hand, and
        /// the sign that raises rather than lowers it.
        ///
        /// Measured, because these rigs disagree about which axis is which and a
        /// wrong guess produces an arm that swings into the character's own hip.
        /// The bone is put straight back, so this leaves no trace.
        /// </summary>
        private void MeasureLiftAxis()
        {
            Transform tip = _forearm != null ? _forearm : _upperArm;
            Quaternion rest = _upperArm.localRotation;
            float restHeight = tip.position.y;
            float best = 0f;

            foreach (Vector3 candidate in
                new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                _upperArm.localRotation =
                    rest * Quaternion.AngleAxis(35f, candidate);
                float lift = tip.position.y - restHeight;
                _upperArm.localRotation = rest;

                if (Mathf.Abs(lift) <= Mathf.Abs(best))
                {
                    continue;
                }

                best = lift;
                _liftAxis = candidate;
            }

            _liftSign = best < 0f ? -1f : 1f;
        }

        private void HandleThrown(
            ThrowableKind kind,
            ThrowResolver.Result result)
        {
            Play(kind, result.Origin, result.Landing);
        }

        /// <summary>
        /// Starts the swing and launches the visual prop.
        ///
        /// Public because a client's throw is resolved on the host: the host
        /// tells the other machine what happened and the network layer calls
        /// this, since the local event never fires there.
        /// </summary>
        public void Play(
            ThrowableKind kind,
            Vector3 origin,
            Vector3 landing)
        {
            _armElapsed = 0f;
            if (ThrowableCatalog.GetUse(kind) != ThrowableUse.Thrown)
            {
                return;
            }

            EnsureProp();
            _propFrom = origin;
            _propTo = landing;
            _propElapsed = 0f;
            _propDuration = Mathf.Max(
                0.08f,
                Vector3.Distance(origin, landing) / propSpeed);
            _propFlying = true;
            if (_prop != null)
            {
                _prop.position = origin;
                _prop.gameObject.SetActive(true);
            }
        }

        private void EnsureProp()
        {
            if (_prop != null)
            {
                return;
            }

            GameObject prop = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            prop.name = "Thrown Prop";
            // No collider. The flight is a replay; letting it touch anything
            // would put a second opinion about the hit into the world.
            Destroy(prop.GetComponent<Collider>());
            // Not parented to the thrower: it has to fly away from them, and a
            // parent that keeps running would drag the rock along.
            prop.transform.localScale =
                Vector3.one * propDiameter;
            if (propMaterial != null)
            {
                prop.GetComponent<Renderer>().sharedMaterial =
                    propMaterial;
            }

            prop.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            prop.SetActive(false);
            _prop = prop.transform;
        }

        private void Update()
        {
            AdvanceProp();
        }

        private void AdvanceProp()
        {
            if (!_propFlying || _prop == null)
            {
                return;
            }

            _propElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_propElapsed / _propDuration);
            Vector3 position = Vector3.Lerp(_propFrom, _propTo, t);
            // A low arc. Enough to read as thrown rather than fired, not enough
            // to look lobbed over the obstacle the host said stopped it.
            position.y += Mathf.Sin(t * Mathf.PI)
                * Vector3.Distance(_propFrom, _propTo) * 0.07f;
            _prop.position = position;
            _prop.Rotate(
                new Vector3(520f, 240f, 0f) * Time.deltaTime,
                Space.Self);

            if (t < 1f)
            {
                return;
            }

            _propFlying = false;
            _prop.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            // After the animator, so the clip does not overwrite the swing.
            if (_upperArm == null)
            {
                return;
            }

            if (_armElapsed < 0f)
            {
                return;
            }

            _armElapsed += Time.deltaTime;
            float angle = EvaluateArmAngle(_armElapsed);
            _upperArm.localRotation = _upperRest
                * Quaternion.AngleAxis(
                    angle * _liftSign,
                    _liftAxis);

            if (_forearm != null)
            {
                // Trails the upper arm by about a third, which is what makes
                // the arm bend into the throw instead of staying a plank.
                _forearm.localRotation = _forearmRest
                    * Quaternion.AngleAxis(
                        angle * 0.35f * _liftSign,
                        _liftAxis);
            }

            if (_armElapsed
                >= windupSeconds + releaseSeconds + recoverSeconds)
            {
                _armElapsed = -1f;
                RestoreArm();
            }
        }

        /// <summary>
        /// Up behind the head, snap through rest, then settle back. Returning
        /// degrees rather than driving the bone keeps this testable without a
        /// rig.
        /// </summary>
        public float EvaluateArmAngle(float elapsed)
        {
            if (elapsed <= windupSeconds)
            {
                return Mathf.SmoothStep(
                    0f,
                    windupDegrees,
                    elapsed / windupSeconds);
            }

            float afterWindup = elapsed - windupSeconds;
            if (afterWindup <= releaseSeconds)
            {
                return Mathf.Lerp(
                    windupDegrees,
                    -followThroughDegrees,
                    afterWindup / releaseSeconds);
            }

            float afterRelease = afterWindup - releaseSeconds;
            return Mathf.Lerp(
                -followThroughDegrees,
                0f,
                Mathf.Clamp01(afterRelease / recoverSeconds));
        }

        private void RestoreArm()
        {
            if (_upperArm != null)
            {
                _upperArm.localRotation = _upperRest;
            }

            if (_forearm != null)
            {
                _forearm.localRotation = _forearmRest;
            }
        }
    }
}
