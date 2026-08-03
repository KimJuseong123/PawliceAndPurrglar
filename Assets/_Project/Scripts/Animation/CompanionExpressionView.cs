using System.Collections.Generic;
using PawsAndLoot.Companions;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Shows one icon above the animal's head and turns it to face the camera.
    ///
    /// Four models are parented once and switched by enabling one of them.
    /// Instantiating on demand would be simpler to read and worse to run: these
    /// fire several times a second during a chase, and the first one would
    /// arrive a frame late while Unity loaded the mesh.
    ///
    /// Facing is done by rotating to the camera's forward rather than by
    /// looking at the camera's position. Looking at the position makes icons on
    /// the edge of the screen lean noticeably, and with a chase camera that
    /// pans constantly the lean reads as the icon wobbling.
    /// </summary>
    public sealed class CompanionExpressionView : MonoBehaviour
    {
        [SerializeField]
        private Transform anchor;

        [SerializeField, Min(0.1f)]
        private float displaySeconds = 1.6f;

        [SerializeField]
        private float bobHeight = 0.08f;

        [SerializeField, Min(0.1f)]
        private float bobSpeed = 2.4f;

        private readonly Dictionary<CompanionExpression, GameObject> _icons =
            new();

        private float _remainingSeconds;
        private Vector3 _restLocalPosition;
        private Camera _camera;

        public CompanionExpression Current { get; private set; }

        public bool IsShowing => _remainingSeconds > 0f;

        /// <summary>
        /// Registers one of the four models. Called by the scene builder, but
        /// the lookup it fills is rebuilt from the children on load — an
        /// editor-populated collection does not survive being saved into a
        /// scene, and a dictionary that is empty at runtime looks exactly like
        /// an animal that never has anything to say.
        /// </summary>
        public void Register(CompanionExpression expression, GameObject icon)
        {
            if (icon == null || expression == CompanionExpression.None)
            {
                return;
            }

            _icons[expression] = icon;
            icon.SetActive(false);
        }

        public void Configure(Transform iconAnchor, float seconds)
        {
            anchor = iconAnchor;
            displaySeconds = Mathf.Max(0.1f, seconds);
        }

        public void Show(CompanionExpression expression)
        {
            if (expression == CompanionExpression.None)
            {
                Hide();
                return;
            }

            ResolveIcons();
            foreach (KeyValuePair<CompanionExpression, GameObject> pair
                in _icons)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(pair.Key == expression);
                }
            }

            Current = expression;
            _remainingSeconds = displaySeconds;
        }

        public void Hide()
        {
            foreach (GameObject icon in _icons.Values)
            {
                if (icon != null)
                {
                    icon.SetActive(false);
                }
            }

            Current = CompanionExpression.None;
            _remainingSeconds = 0f;
        }

        /// <summary>
        /// Finds the icon children by name.
        ///
        /// The scene builder calls <see cref="Register"/>, and that list is gone
        /// by the time the scene is loaded — the same way an
        /// <c>onClick.AddListener</c> added from an editor script is gone. So
        /// the view finds its own children instead of trusting what it was
        /// handed.
        /// </summary>
        private void ResolveIcons()
        {
            if (_icons.Count > 0 || anchor == null)
            {
                return;
            }

            foreach (Transform child in anchor)
            {
                if (System.Enum.TryParse(
                        child.name,
                        out CompanionExpression parsed)
                    && parsed != CompanionExpression.None)
                {
                    _icons[parsed] = child.gameObject;
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void Awake()
        {
            if (anchor != null)
            {
                _restLocalPosition = anchor.localPosition;
            }

            ResolveIcons();
            Hide();
        }

        private void LateUpdate()
        {
            if (!IsShowing || anchor == null)
            {
                return;
            }

            _remainingSeconds -= Time.deltaTime;
            if (_remainingSeconds <= 0f)
            {
                Hide();
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                anchor.rotation = Quaternion.LookRotation(
                    _camera.transform.forward,
                    Vector3.up);
            }

            // A small bob, because a static icon on a moving animal reads as a
            // decal stuck to the screen rather than something the animal is
            // doing.
            float phase = Mathf.Sin(
                (displaySeconds - _remainingSeconds) * bobSpeed * Mathf.PI);
            anchor.localPosition =
                _restLocalPosition + new Vector3(0f, phase * bobHeight, 0f);
        }
    }
}
