using UnityEngine;
using UnityEngine.InputSystem;

namespace PawliceAndPurrglar.Gameplay.Loot
{
    public sealed class LootDropInput : MonoBehaviour
    {
        [SerializeField]
        private LootCarrier carrier;

        [SerializeField]
        private bool isLocallyControlled;

        public bool IsLocallyControlled
        {
            get => isLocallyControlled;
            set => isLocallyControlled = value;
        }

        public void Configure(
            LootCarrier configuredCarrier,
            bool locallyControlled)
        {
            carrier = configuredCarrier;
            isLocallyControlled = locallyControlled;
        }

        private void Update()
        {
            if (!isLocallyControlled
                || carrier == null
                || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                carrier.TryDrop();
            }
        }
    }
}
