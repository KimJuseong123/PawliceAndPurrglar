using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Which side the local player has been given, as far as the lobby art is
    /// concerned.
    /// </summary>
    public enum RoleSelectionState
    {
        None,
        Police,
        Thief
    }

    /// <summary>
    /// Shows which side the local player is on by swapping each team's artwork
    /// between a standing pose and a high five.
    ///
    /// The police keep the left and the thief the right whatever the
    /// assignment. Swapping their positions would make the role button read as
    /// "move the characters" rather than "change my role", and the eye has to
    /// re-find both teams every time.
    ///
    /// Two images and four authored sprites, rather than the four separate
    /// character cut-outs this replaced. Those were keyed out of a mockup by
    /// brightness and hue, which left ragged edges and ate the whites of an eye
    /// wherever they sat against the ivory background.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyCharacterView : MonoBehaviour
    {
        [SerializeField]
        private Image policeTeamImage;

        [SerializeField]
        private Image thiefTeamImage;

        [SerializeField]
        private Sprite policeIdleSprite;

        [SerializeField]
        private Sprite policeSelectedSprite;

        [SerializeField]
        private Sprite thiefIdleSprite;

        [SerializeField]
        private Sprite thiefSelectedSprite;

        [SerializeField]
        private GameObject policeBadge;

        [SerializeField]
        private GameObject thiefBadge;

        /// <summary>
        /// The unchosen team dims but stays clearly readable. Losing it further
        /// would make the screen look like the other side is unavailable.
        /// </summary>
        private const float DimmedBrightness = 0.78f;

        /// <summary>
        /// Just enough to lift the chosen pair off the background. More reads as
        /// the layout moving rather than a selection.
        /// </summary>
        private const float SelectedScale = 1.04f;

        private const float Response = 12f;

        private RoleSelectionState _state = RoleSelectionState.None;
        private bool _applied;

        public RoleSelectionState State => _state;

        /// <summary>What each side is currently showing, for tests.</summary>
        public Sprite PoliceSprite =>
            policeTeamImage != null ? policeTeamImage.sprite : null;

        public Sprite ThiefSprite =>
            thiefTeamImage != null ? thiefTeamImage.sprite : null;

        public void Configure(
            Image configuredPoliceImage,
            Image configuredThiefImage,
            Sprite configuredPoliceIdle,
            Sprite configuredPoliceSelected,
            Sprite configuredThiefIdle,
            Sprite configuredThiefSelected,
            GameObject configuredPoliceBadge,
            GameObject configuredThiefBadge)
        {
            policeTeamImage = configuredPoliceImage;
            thiefTeamImage = configuredThiefImage;
            policeIdleSprite = configuredPoliceIdle;
            policeSelectedSprite = configuredPoliceSelected;
            thiefIdleSprite = configuredThiefIdle;
            thiefSelectedSprite = configuredThiefSelected;
            policeBadge = configuredPoliceBadge;
            thiefBadge = configuredThiefBadge;
            _applied = false;
            Apply(false, false);
        }

        /// <summary>
        /// Applied only on change: the lobby polls, and assigning the same
        /// sprite every time still dirties the canvas.
        /// </summary>
        public void Apply(bool assigned, bool localIsPolice)
        {
            RoleSelectionState wanted = !assigned
                ? RoleSelectionState.None
                : localIsPolice
                    ? RoleSelectionState.Police
                    : RoleSelectionState.Thief;

            if (_applied && wanted == _state)
            {
                return;
            }

            _applied = true;
            _state = wanted;

            SetSprite(
                policeTeamImage,
                wanted == RoleSelectionState.Police
                    ? policeSelectedSprite
                    : policeIdleSprite);
            SetSprite(
                thiefTeamImage,
                wanted == RoleSelectionState.Thief
                    ? thiefSelectedSprite
                    : thiefIdleSprite);

            SetActive(policeBadge, wanted == RoleSelectionState.Police);
            SetActive(thiefBadge, wanted == RoleSelectionState.Thief);

            bool policeChosen = wanted == RoleSelectionState.Police;
            bool thiefChosen = wanted == RoleSelectionState.Thief;
            bool nobodyChosen = wanted == RoleSelectionState.None;

            // Tinted rather than faded: dropping a pair's alpha would show the
            // background through the characters.
            SetBrightness(policeTeamImage, nobodyChosen || policeChosen);
            SetBrightness(thiefTeamImage, nobodyChosen || thiefChosen);
        }

        /// <summary>
        /// Eases the chosen pair up to its selected size. Framerate-independent
        /// so the step is the same on any machine.
        /// </summary>
        private void Update()
        {
            Lerp(policeTeamImage, _state == RoleSelectionState.Police);
            Lerp(thiefTeamImage, _state == RoleSelectionState.Thief);
        }

        private static void Lerp(Image image, bool chosen)
        {
            if (image == null)
            {
                return;
            }

            float wanted = chosen ? SelectedScale : 1f;
            float current = Mathf.Lerp(
                image.rectTransform.localScale.x,
                wanted,
                1f - Mathf.Exp(-Response * Time.unscaledDeltaTime));
            image.rectTransform.localScale =
                new Vector3(current, current, 1f);
        }

        private static void SetBrightness(Image image, bool lit)
        {
            if (image == null)
            {
                return;
            }

            float level = lit ? 1f : DimmedBrightness;
            image.color = new Color(level, level, level, 1f);
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image != null && sprite != null && image.sprite != sprite)
            {
                image.sprite = sprite;
            }
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }
}
