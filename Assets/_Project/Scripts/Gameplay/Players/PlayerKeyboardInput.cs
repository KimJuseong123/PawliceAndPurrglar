using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class PlayerKeyboardInput : MonoBehaviour
    {
        [SerializeField]
        private PlayerMovementMotor movementMotor;

        [SerializeField]
        private bool isLocallyControlled;

        public bool IsLocallyControlled
        {
            get => isLocallyControlled;
            set => isLocallyControlled = value;
        }

        public void Configure(
            PlayerMovementMotor motor,
            bool locallyControlled)
        {
            movementMotor = motor;
            isLocallyControlled = locallyControlled;
        }

        private void Update()
        {
            if (!isLocallyControlled || movementMotor == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Vector2 input = keyboard == null
                ? Vector2.zero
                : new Vector2(
                    ReadAxis(keyboard.aKey, keyboard.dKey),
                    ReadAxis(keyboard.sKey, keyboard.wKey));
            if (keyboard?.spaceKey.wasPressedThisFrame == true)
            {
                movementMotor.TryStartDash(input);
            }

            movementMotor.Move(input, Time.deltaTime);
        }

        private static float ReadAxis(
            KeyControl negative,
            KeyControl positive)
        {
            return (positive.isPressed ? 1f : 0f)
                - (negative.isPressed ? 1f : 0f);
        }
    }
}
