using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// Lifts the night on one player's screen. The thief's is lifted further.
    ///
    /// Two problems, one fix. The night was legible but oppressive to actually
    /// play in, and the thief — who is the one being hunted in the dark — had no
    /// compensation for it. Giving the thief better dark adaptation is the
    /// cheapest counterweight there is: it costs the police nothing they can
    /// see, needs no new rule, and it is what a cat burglar would plausibly
    /// have.
    ///
    /// A directional light, so it fills the whole map rather than following the
    /// character around like a second torch. Direction is what a directional
    /// light uses, so being parented to a moving player changes nothing.
    ///
    /// Presentation only, and per screen. The component lives on both machines
    /// because the player object does, but the light is only switched on for
    /// whoever is actually playing that role — that is what keeps the police
    /// from getting the thief's night vision. Nothing here is replicated and no
    /// rule reads it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NightVisionFill : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity viewer;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Light fill;

        private IMatchStateReader _matchState;

        public bool IsLit => fill != null && fill.enabled;

        public void Configure(
            PlayerRoleIdentity configuredViewer,
            IMatchStateReader configuredMatchState,
            Light configuredFill)
        {
            viewer = configuredViewer;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            fill = configuredFill;
        }

        /// <summary>
        /// True only on the machine playing this player's role, the same gate
        /// the torch cone and the footprints use. Without it both fills would be
        /// on at once and the police would see the thief's brighter night.
        /// </summary>
        private bool ViewerIsLocal()
        {
            if (viewer == null)
            {
                return false;
            }

            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (assigned.HasValue)
            {
                return assigned.Value == viewer.Role;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null
                && selector.ActiveRole == viewer.Role;
        }

        private void LateUpdate()
        {
            if (fill == null)
            {
                return;
            }

            // Off outside a match. The lobby and the result screen are lit
            // normally and would end up washed out by two fills.
            fill.enabled = ViewerIsLocal()
                && ResolveMatchState()?.IsGameplayActive == true;
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
