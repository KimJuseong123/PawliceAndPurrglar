using PawsAndLoot.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
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
            GameSceneLoader.Load(targetScene);
        }
    }
}
