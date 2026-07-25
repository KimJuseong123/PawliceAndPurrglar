using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class InteractionPromptPresenter : MonoBehaviour
    {
        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private Text promptLabel;

        public void Configure(
            LocalPlayerRoleSelector selector,
            Text label)
        {
            roleSelector = selector;
            promptLabel = label;
        }

        private void Update()
        {
            if (promptLabel == null)
            {
                return;
            }

            PlayerInteractionScanner scanner =
                roleSelector?.ActiveBinding?.InteractionScanner;
            promptLabel.text = scanner != null && scanner.HasTarget
                ? $"[E] {scanner.CurrentPrompt}"
                : string.Empty;
        }
    }
}
