using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Counts the emergency exit down on screen.
    ///
    /// Without it the key is a press that appears to do nothing for ten
    /// seconds, which a stuck player reads as the key not working — so they
    /// press it again, and again, and then leave the match, which is the exact
    /// outcome the exit exists to prevent.
    ///
    /// Builds its own label rather than being handed one. A list filled by an
    /// editor script does not survive being saved, and this project has lost
    /// three playtests to that (`ISSUE-031`).
    /// </summary>
    public sealed class InteriorEscapePresenter : MonoBehaviour
    {
        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        private Text _label;
        private InteriorEscapeHatch _hatch;

        public string CurrentText => _label != null ? _label.text : string.Empty;

        public bool IsShowing =>
            _label != null && _label.gameObject.activeSelf;

        public void Configure(LocalPlayerRoleSelector configuredSelector)
        {
            roleSelector = configuredSelector;
        }

        private void OnEnable()
        {
            EnsureLabel();
        }

        private void EnsureLabel()
        {
            if (_label != null)
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var host = new GameObject("Interior Escape Countdown");
            host.transform.SetParent(canvas.transform, false);

            var rect = host.AddComponent<RectTransform>();

            // Under the middle of the screen, where a player who cannot move is
            // already looking at their own character.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 150f);
            rect.sizeDelta = new Vector2(520f, 44f);

            _label = host.AddComponent<Text>();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.fontSize = 26;
            _label.color = new Color(1f, 0.85f, 0.35f);
            _label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            _label.raycastTarget = false;
            host.SetActive(false);
        }

        private void Update()
        {
            EnsureLabel();
            if (_label == null)
            {
                return;
            }

            ResolveHatch();
            bool counting = _hatch != null && _hatch.IsCounting;
            if (_label.gameObject.activeSelf != counting)
            {
                _label.gameObject.SetActive(counting);
            }

            if (counting)
            {
                _label.text =
                    $"비상 탈출까지 {Mathf.CeilToInt(_hatch.Remaining)}초";
            }
        }

        /// <summary>
        /// Finds the hatch on whichever character this screen is driving.
        ///
        /// Re-asked while it is null rather than once at startup: the role this
        /// machine controls is handed out by the host after the lobby, so at
        /// startup there is nobody to ask.
        /// </summary>
        private void ResolveHatch()
        {
            if (_hatch != null)
            {
                return;
            }

            if (roleSelector == null)
            {
                roleSelector =
                    FindFirstObjectByType<LocalPlayerRoleSelector>();
            }

            PlayerRoleIdentity identity =
                roleSelector != null && roleSelector.ActiveBinding != null
                    ? roleSelector.ActiveBinding.Identity
                    : null;
            if (identity != null)
            {
                _hatch = identity.GetComponentInChildren<InteriorEscapeHatch>();
            }
        }
    }
}
