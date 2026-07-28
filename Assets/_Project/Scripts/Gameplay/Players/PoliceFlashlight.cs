using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// The cone of light the police carries at night.
    ///
    /// Still just a light — no rule reads this component. What the officer can
    /// see is decided by <c>FlashlightVisibility</c> and drawn by
    /// <c>FlashlightConeView</c>; all three now take their shape from
    /// <see cref="FlashlightCone"/> so the lit floor and the rule agree instead
    /// of contradicting each other.
    ///
    /// Deleting this would leave the night unlit and the rule intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceFlashlight : MonoBehaviour
    {
        [SerializeField]
        private Light beam;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        /// <summary>
        /// Tilted down so the cone lands on the road a few metres ahead rather
        /// than shining out at the horizon, which from a fixed overhead camera
        /// would light nothing the player can see.
        /// </summary>
        [SerializeField]
        private float pitchDegrees = FlashlightCone.PitchDegrees;

        private IMatchStateReader _matchState;

        public bool IsLit => beam != null && beam.enabled;

        public void Configure(
            Light configuredBeam,
            IMatchStateReader configuredMatchState)
        {
            beam = configuredBeam;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        private void LateUpdate()
        {
            if (beam == null)
            {
                return;
            }

            // Off outside a match so the lobby and the result screen are not lit
            // by a stray torch.
            beam.enabled = ResolveMatchState()?.IsGameplayActive == true;
            if (!beam.enabled)
            {
                return;
            }

            // Follows the body's facing rather than the camera: the officer
            // looks where they run, and the fixed camera never turns.
            beam.transform.rotation = Quaternion.Euler(
                pitchDegrees,
                transform.eulerAngles.y,
                0f);
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }
    }
}
