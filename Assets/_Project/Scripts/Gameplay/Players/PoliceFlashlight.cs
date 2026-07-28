using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// The cone of light the police carries at night.
    ///
    /// Presentation only, and deliberately so. It lights the ground ahead of the
    /// officer and nothing reads it: the thief is not hidden by being outside
    /// the beam, and no rule consults it. Restricting what the police can
    /// actually see is a separate decision that waits on a playtest, because
    /// "the police is too strong on a small map" has not been measured in this
    /// game yet.
    ///
    /// Keeping the two apart means the atmosphere can ship now and be kept even
    /// if the vision restriction is rejected.
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
        private float pitchDegrees = 24f;

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
