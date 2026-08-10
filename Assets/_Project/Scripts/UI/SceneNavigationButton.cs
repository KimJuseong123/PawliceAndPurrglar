using PawliceAndPurrglar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SceneNavigationButton : MonoBehaviour
    {
        [SerializeField]
        private GameSceneId targetScene;

        public GameSceneId TargetScene
        {
            get => targetScene;
            set => targetScene = value;
        }

        private Button _button;

        private void OnEnable()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(LoadTargetScene);
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(LoadTargetScene);
            }
        }

        public void LoadTargetScene()
        {
            // NET-008. Going from the result screen back into the match is a
            // rematch. In a session a client cannot load it itself, so the press
            // becomes a request to the host; offline the bridge declines and the
            // ordinary load runs.
            if (targetScene == GameSceneId.Game
                && NetworkSceneBridge.TryRequestRematch())
            {
                return;
            }

            // Going to the lobby ends the session first. The lobby that loads
            // brings its own NetworkManager, and Netcode keeps the old one alive
            // across scenes while still holding the static singleton — so a
            // session left running made the second match unable to spawn
            // anything at all.
            if (targetScene == GameSceneId.Bootstrap)
            {
                NetworkSceneBridge.LeaveSession();
            }

            GameSceneLoader.Load(targetScene);
        }
    }
}
