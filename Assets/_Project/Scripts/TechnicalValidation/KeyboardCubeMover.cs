using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PawsAndLoot.TechnicalValidation
{
    public sealed class KeyboardCubeMover : MonoBehaviour
    {
        [SerializeField, Min(0.01f)]
        private float movementSpeed = 4f;

        public float MovementSpeed
        {
            get => movementSpeed;
            set => movementSpeed = Mathf.Max(0.01f, value);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float horizontal =
                ReadAxis(keyboard.aKey, keyboard.leftArrowKey, keyboard.dKey, keyboard.rightArrowKey);
            float vertical =
                ReadAxis(keyboard.sKey, keyboard.downArrowKey, keyboard.wKey, keyboard.upArrowKey);
            Vector3 direction = new(horizontal, 0f, vertical);

            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            transform.position += direction * (movementSpeed * Time.deltaTime);
        }

        private static float ReadAxis(
            KeyControl negativePrimary,
            KeyControl negativeSecondary,
            KeyControl positivePrimary,
            KeyControl positiveSecondary)
        {
            float negative =
                negativePrimary.isPressed || negativeSecondary.isPressed ? 1f : 0f;
            float positive =
                positivePrimary.isPressed || positiveSecondary.isPressed ? 1f : 0f;
            return positive - negative;
        }
    }
}
