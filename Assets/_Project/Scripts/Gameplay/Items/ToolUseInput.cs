using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Reads the throw key for a locally controlled player.
    ///
    /// Mirrors <c>PlayerInteractionInput</c> and <c>LootDropInput</c> exactly,
    /// including <see cref="IsLocallyControlled"/>. In a session the network
    /// input bridge switches every one of these off and sends the press to the
    /// host instead, so a client cannot use a prop on its own authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolUseInput : MonoBehaviour
    {
        [SerializeField]
        private ToolUseAction action;

        [SerializeField]
        private bool isLocallyControlled;

        public bool IsLocallyControlled
        {
            get => isLocallyControlled;
            set => isLocallyControlled = value;
        }

        public void Configure(
            ToolUseAction configuredAction,
            bool locallyControlled)
        {
            action = configuredAction;
            isLocallyControlled = locallyControlled;
        }

        private void Update()
        {
            if (!isLocallyControlled
                || action == null
                || Keyboard.current == null)
            {
                return;
            }

            // F, chosen because E is interact, Q is drop and Space is dash. One
            // key covers throwing and placing: which one happens is decided by
            // what is held, not by the player remembering two bindings.
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                action.TryUse();
            }
        }
    }
}
