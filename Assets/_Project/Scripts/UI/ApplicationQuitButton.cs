using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Ends the game.
    ///
    /// Hides itself in the browser. <c>Application.Quit</c> cannot close a tab
    /// the player opened — the browser will not let a page do that — so the
    /// button would sit there doing nothing, which is exactly the failure this
    /// project keeps finding in its own lobby. Nothing to quit, so nothing to
    /// press.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ApplicationQuitButton : MonoBehaviour
    {
        private Button _button;

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnEnable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            gameObject.SetActive(false);
            return;
#else
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Quit);
#endif
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(Quit);
            }
        }
    }
}
