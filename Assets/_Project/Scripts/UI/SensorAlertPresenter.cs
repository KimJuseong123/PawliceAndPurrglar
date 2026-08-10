using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Tells the officer a sensor went off, on their screen only.
    ///
    /// The reveal itself is the real payoff — the thief simply becomes visible —
    /// and that was the whole design: show them rather than point at them. But a
    /// thief revealed behind a building, or off the edge of the view, is a reveal
    /// the officer never notices, and a light flashing somewhere off screen is
    /// not something they can act on. One line of text is what turns "something
    /// happened" into "look now".
    ///
    /// Read only, and only while the reveal is running. It reports what the
    /// visibility rule already decided and cannot cause a reveal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SensorAlertPresenter : MonoBehaviour
    {
        [SerializeField]
        private Text label;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        private FlashlightVisibility _visibility;

        public bool IsShowing { get; private set; }
        public string AlertText =>
            label != null ? label.text : string.Empty;

        public void Configure(
            Text configuredLabel,
            LocalPlayerRoleSelector configuredRoleSelector)
        {
            label = configuredLabel;
            roleSelector = configuredRoleSelector;
        }

        /// <summary>
        /// The visibility rule lives on the officer, so finding it also answers
        /// "is this the officer's screen".
        /// </summary>
        private FlashlightVisibility ResolveVisibility()
        {
            if (_visibility == null)
            {
                _visibility =
                    FindFirstObjectByType<FlashlightVisibility>();
            }

            return _visibility;
        }

        private bool ViewerIsPolice()
        {
            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (assigned.HasValue)
            {
                return assigned.Value == PlayerRole.Police;
            }

            return roleSelector != null
                && roleSelector.ActiveRole == PlayerRole.Police;
        }

        public void Refresh()
        {
            if (label == null)
            {
                return;
            }

            bool showing = ViewerIsPolice()
                && ResolveVisibility()?.IsRevealed == true;
            if (showing == IsShowing)
            {
                return;
            }

            IsShowing = showing;
            label.text = showing
                ? $"센서등 작동! {ThrowableCatalog.RevealSeconds:0.#}초간 "
                  + "도둑이 보입니다"
                : string.Empty;
            label.color = new Color(1f, 0.88f, 0.4f);
        }

        private void Update()
        {
            Refresh();
        }
    }
}
