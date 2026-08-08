using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// A player who stands on a banana turns a full circle on the spot.
    ///
    /// The banana and the rock take the same second of control and were being
    /// drawn the same way — four stars over the head, which is the cartoon for
    /// "somebody hit you". Nobody is hit by a banana. The prop's entire joke is
    /// the fall, and a fall that is only reported by a HUD line is not a joke,
    /// it is a status effect.
    ///
    /// **The rule is untouched.** The second still comes from
    /// <see cref="StunState"/>, the re-stun guard is the same guard, and this
    /// component could be deleted without changing who wins. It reads the stun
    /// and turns a transform.
    ///
    /// Turns the *visual* root, not the player. The motor writes the root's
    /// rotation every frame to point the character where they are heading, so a
    /// spin applied there would be overwritten immediately and — worse — would
    /// leave the character facing a random direction when the stun ended, which
    /// the player would read as their input having been eaten. The visual root is
    /// a child nothing else rotates, so it can be returned to identity exactly.
    ///
    /// Both screens, like the stars. Watching the other player go over is most of
    /// the reward for throwing a banana, and <see cref="StunState.Cause"/> is
    /// replicated so both machines draw the same accident.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlipSpinView : MonoBehaviour
    {
        [SerializeField]
        private StunState stun;

        [SerializeField]
        private Transform visual;

        /// <summary>
        /// One turn. Not two: the stun is a second long, and two revolutions in
        /// a second reads as a glitch rather than a slip.
        /// </summary>
        [SerializeField]
        private float turnDegrees = 360f;

        /// <summary>
        /// How far the character leans out of vertical at the middle of the
        /// turn, and back to nothing by the end. Windmilling arms would be
        /// better and needs an animation clip; a lean needs a transform.
        /// </summary>
        [SerializeField, Range(0f, 60f)]
        private float leanDegrees = 26f;

        /// <summary>
        /// How fast the visual is put back upright if a spin is cut short — by a
        /// match ending, an arrest, or <see cref="StunState.Clear"/>. Snapping
        /// would leave a visible pop; leaving it crooked is worse.
        /// </summary>
        [SerializeField, Min(1f)]
        private float recoverDegreesPerSecond = 720f;

        private float _totalSeconds;
        private bool _spinning;

        /// <summary>True while the character is mid-turn.</summary>
        public bool IsSpinning => _spinning;

        /// <summary>
        /// How far through the turn, 0 to 1. Exposed because "is it spinning" is
        /// the kind of self-report that passed for the whole life of the stars
        /// while nothing was drawn; a test can read this and the transform and
        /// check they agree.
        /// </summary>
        public float Progress { get; private set; }

        public Transform Visual => ResolveVisual();

        public void Configure(StunState configuredStun, Transform configuredVisual)
        {
            stun = configuredStun;
            visual = configuredVisual;
        }

        private StunState ResolveStun()
        {
            if (stun == null)
            {
                stun = GetComponentInParent<StunState>();
            }

            return stun;
        }

        private Transform ResolveVisual()
        {
            if (visual != null)
            {
                return visual;
            }

            PlayerVisualRoot root = GetComponentInParent<PlayerVisualRoot>();
            if (root != null && root.VisualRoot != null)
            {
                visual = root.VisualRoot;
                return visual;
            }

            // Named lookup as the last resort, because the technical-validation
            // rigs carry the child without the component.
            visual = transform.Find("VisualRoot");
            return visual;
        }

        private void LateUpdate()
        {
            Transform target = ResolveVisual();
            if (target == null)
            {
                return;
            }

            StunState state = ResolveStun();
            bool slipping = state != null
                && state.IsStunned
                && state.Cause == StunCause.Slip;

            if (!slipping)
            {
                _spinning = false;
                _totalSeconds = 0f;
                Progress = 0f;
                if (target.localRotation != Quaternion.identity)
                {
                    target.localRotation = Quaternion.RotateTowards(
                        target.localRotation,
                        Quaternion.identity,
                        recoverDegreesPerSecond * Time.deltaTime);
                }

                return;
            }

            // Latched on the first frame of the slip. The remaining time is what
            // is replicated, so reading it every frame and dividing would make
            // the turn restart whenever a packet arrived.
            if (!_spinning)
            {
                _spinning = true;
                _totalSeconds = Mathf.Max(0.01f, state.RemainingSeconds);
            }

            Progress = Mathf.Clamp01(
                1f - (state.RemainingSeconds / _totalSeconds));

            // The lean goes out and comes back inside the one turn, so the
            // character ends the slip upright without a second animation to
            // straighten them.
            float lean = leanDegrees * Mathf.Sin(Progress * Mathf.PI);
            target.localRotation = Quaternion.Euler(
                0f,
                turnDegrees * Progress,
                lean);
        }
    }
}
