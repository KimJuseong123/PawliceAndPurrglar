using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Hover and press feedback for a lobby button, plus the disabled look.
    ///
    /// Scale rather than colour for the hover, because the plate is already
    /// tinted to the button's own colour and a <see cref="ColorBlock"/> can only
    /// multiply — it can darken a blue button but never brighten one.
    ///
    /// The disabled look is applied here as well: multiplying a saturated plate
    /// by grey leaves a muddy version of the same colour, which does not read as
    /// "you cannot press this". Blending towards grey does.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LobbyButtonMotion : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        private const float HoverScale = 1.03f;
        private const float PressedScale = 0.97f;
        private const float Response = 16f;

        [SerializeField]
        private RectTransform target;

        /// <summary>
        /// The tinted plate. Kept separate from the outline so the disabled
        /// blend never touches the outline.
        /// </summary>
        [SerializeField]
        private Graphic fill;

        [SerializeField]
        private Graphic[] contents = new Graphic[0];

        [SerializeField]
        private Color enabledFill = Color.white;

        private static readonly Color DisabledFill =
            new(0.66f, 0.62f, 0.58f, 1f);

        private Button _button;
        private bool _hovered;
        private bool _pressed;
        private bool _lastInteractable = true;

        public void Configure(
            RectTransform configuredTarget,
            Graphic configuredFill,
            Color configuredEnabledFill,
            Graphic[] configuredContents)
        {
            target = configuredTarget;
            fill = configuredFill;
            enabledFill = configuredEnabledFill;
            contents = configuredContents ?? new Graphic[0];
            ApplyInteractable(true);
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (target == null)
            {
                target = transform as RectTransform;
            }
        }

        private void OnEnable()
        {
            _hovered = false;
            _pressed = false;
            if (target != null)
            {
                target.localScale = Vector3.one;
            }

            _lastInteractable = !IsInteractable();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        private bool IsInteractable()
        {
            return _button != null && _button.IsInteractable();
        }

        private void Update()
        {
            bool interactable = IsInteractable();
            if (interactable != _lastInteractable)
            {
                _lastInteractable = interactable;
                ApplyInteractable(interactable);
            }

            if (target == null)
            {
                return;
            }

            float wanted = 1f;
            if (interactable)
            {
                wanted = _pressed
                    ? PressedScale
                    : _hovered
                        ? HoverScale
                        : 1f;
            }

            // Framerate-independent ease rather than MoveTowards: the travel is
            // three hundredths of a unit, so a constant speed either snaps or
            // crawls depending on what number you pick.
            float current = Mathf.Lerp(
                target.localScale.x,
                wanted,
                1f - Mathf.Exp(-Response * Time.unscaledDeltaTime));
            target.localScale = new Vector3(current, current, 1f);
        }

        private void ApplyInteractable(bool interactable)
        {
            if (fill != null)
            {
                fill.color = interactable ? enabledFill : DisabledFill;
            }

            float contentAlpha = interactable ? 1f : 0.55f;
            foreach (Graphic graphic in contents)
            {
                if (graphic == null)
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = contentAlpha;
                graphic.color = color;
            }
        }
    }
}
