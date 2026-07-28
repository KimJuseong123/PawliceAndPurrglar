using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Reads the throw input for a locally controlled player, and where they
    /// aimed.
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

        /// <summary>
        /// Where the cursor is pointing, as a flat direction from a player.
        ///
        /// The camera is fixed and tilted, so a screen position is only
        /// meaningful once it is put back on the ground: the cursor ray is
        /// crossed with the horizontal plane through the player's feet, and the
        /// direction to that spot is the aim. Reading the ray's own direction
        /// instead would aim everything at the horizon.
        ///
        /// Returns null when there is no cursor, no camera, or the ray points
        /// away from the ground — callers fall back to the character's facing
        /// rather than throwing at nothing.
        /// </summary>
        public static Vector3? ReadAimDirection(Vector3 fromPosition)
        {
            Mouse mouse = Mouse.current;
            // Fully qualified: PawsAndLoot.Gameplay.Camera is a namespace, so a
            // bare Camera inside Gameplay resolves to it and not to the type.
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (mouse == null || view == null)
            {
                return null;
            }

            Ray ray = view.ScreenPointToRay(
                mouse.position.ReadValue());
            var ground = new Plane(Vector3.up, fromPosition);
            if (!ground.Raycast(ray, out float distance))
            {
                return null;
            }

            Vector3 aim = ray.GetPoint(distance) - fromPosition;
            aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f
                ? aim.normalized
                : null;
        }

        private void Update()
        {
            if (!isLocallyControlled || action == null)
            {
                return;
            }

            // Two bindings for one action: the mouse, because aiming and firing
            // with the same hand is what makes the cursor worth having, and F
            // because a player whose hand is on the keyboard should not have to
            // reach for the mouse to drop a banana at their own feet.
            bool pressed =
                (Mouse.current != null
                    && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null
                    && Keyboard.current.fKey.wasPressedThisFrame);

            if (pressed)
            {
                action.TryUse(
                    ReadAimDirection(transform.position));
            }
        }
    }
}
