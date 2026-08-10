using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// The lobby's "게임 방법" button, and the only thing that opens the how-to
    /// overlay.
    ///
    /// It finds the overlay by name instead of holding a reference to it. A
    /// serialized reference would be fine, but the overlay starts switched off
    /// and the pair have to agree on a name anyway — <see cref="OverlayName"/>
    /// is that agreement, and it is read from one place by both this and the
    /// builder. <c>Transform.Find</c> reaches inactive children, which is the
    /// whole point here.
    ///
    /// The click is wired in <c>OnEnable</c> rather than by the builder because
    /// a listener added from an editor script disappears when the scene is
    /// saved, leaving a button that works in the editor and does nothing in the
    /// build. Six lobby buttons shipped that way once (`ISSUE-017`).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LobbyHowToLauncher : MonoBehaviour
    {
        /// <summary>The name the builder must give the overlay root.</summary>
        public const string OverlayName = "HowToOverlay";

        private Button button;
        private LobbyHowToOverlay overlay;
        private bool wired;

        private void OnEnable()
        {
            Resolve();

            if (wired || button == null)
            {
                return;
            }

            button.onClick.AddListener(Toggle);
            wired = true;
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Toggle);
            }

            wired = false;
        }

        /// <summary>
        /// Opens the overlay, or closes it if the button is pressed again while
        /// it is up. Public so the tests can press it without an EventSystem.
        /// </summary>
        public void Toggle()
        {
            Resolve();
            if (overlay == null)
            {
                return;
            }

            if (overlay.IsOpen)
            {
                overlay.Close();
            }
            else
            {
                overlay.Open();
            }
        }

        private void Resolve()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (overlay == null)
            {
                Transform found = FindDeep(transform.root, OverlayName);
                overlay = found != null
                    ? found.GetComponent<LobbyHowToOverlay>()
                    : null;
            }
        }

        /// <summary>
        /// The named descendant, inactive ones included.
        ///
        /// By name and by walk rather than <c>FindObjectsByType</c>: that
        /// returns things in instance-id order, which is an accident of what was
        /// created when and has broken a check in this project before
        /// (`ISSUE-041`). It would also reach into other canvases.
        /// </summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int child = 0; child < root.childCount; child++)
            {
                Transform found = FindDeep(root.GetChild(child), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
