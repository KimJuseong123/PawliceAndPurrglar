using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// The dimmed sheet the how-to panel sits on, and the two ways out of it.
    ///
    /// The panel used to be part of the lobby itself, always on screen between
    /// the two teams. It is a modal now: nothing until somebody asks for it,
    /// then everything behind it goes dark and stops taking clicks.
    ///
    /// The scrim is what makes the second half true. Without it a click meant
    /// for the panel that lands a few pixels outside would press whatever lobby
    /// control happens to be under there — and the lobby's controls host rooms
    /// and start matches.
    ///
    /// Like <see cref="LobbyHowToPanel"/>, everything is resolved by name and
    /// wired in <c>OnEnable</c> rather than serialized by the builder: a
    /// listener an editor script adds is non-persistent and does not survive the
    /// scene being saved (`ISSUE-017`).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyHowToOverlay : MonoBehaviour
    {
        /// <summary>Names the builder must use. Read, never written.</summary>
        public const string ScrimName = "Scrim";
        public const string PanelNodeName = "HowToPanel";

        private LobbyHowToPanel panel;
        private Button closeButton;
        private bool wired;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>
        /// The page currently on screen, or -1 when the panel is missing.
        /// Public for the tests and the layout capture.
        /// </summary>
        public LobbyHowToPanel Panel
        {
            get
            {
                Resolve();
                return panel;
            }
        }

        /// <summary>
        /// Shows the overlay from the first page.
        ///
        /// Reset rather than resumed: somebody who closed on page four and came
        /// back has a new question, and page four is an answer to the old one.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            // After the activation, so the panel's own OnEnable has already run
            // and resolved its pages.
            Resolve();
            panel?.Show(0);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Resolve();
            Wire();
        }

        private void OnDisable()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            wired = false;
        }

        /// <summary>
        /// Esc closes, because page one says it does.
        ///
        /// Only while the overlay is on screen — <c>Update</c> does not run on a
        /// disabled object, so this cannot swallow an Esc the lobby wants.
        /// </summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void Resolve()
        {
            if (panel == null)
            {
                Transform found = transform.Find(PanelNodeName);
                panel = found != null
                    ? found.GetComponent<LobbyHowToPanel>()
                    : null;
            }

            if (closeButton == null && panel != null)
            {
                Transform found = panel.transform.Find(
                    LobbyHowToPanel.CloseButtonName);
                closeButton = found != null ? found.GetComponent<Button>() : null;
            }
        }

        private void Wire()
        {
            if (wired)
            {
                return;
            }

            closeButton?.onClick.AddListener(Close);
            wired = true;
        }
    }
}
