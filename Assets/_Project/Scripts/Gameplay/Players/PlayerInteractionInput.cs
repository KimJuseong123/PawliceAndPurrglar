using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class PlayerInteractionInput : MonoBehaviour
    {
        [SerializeField]
        private PlayerInteractionScanner scanner;

        public bool IsLocallyControlled { get; set; }

        public void Configure(
            PlayerInteractionScanner configuredScanner,
            bool locallyControlled)
        {
            scanner = configuredScanner;
            IsLocallyControlled = locallyControlled;
        }

        private void Update()
        {
            if (!IsLocallyControlled
                || scanner == null
                || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                scanner.TryInteractCurrent();
            }
        }
    }
}
