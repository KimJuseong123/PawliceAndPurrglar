using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.TechnicalValidation
{
    /// <summary>
    /// Small local-only mover for the validation scene. It deliberately does
    /// not replace or modify the production movement motor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ValidationPlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float moveSpeed = 4f;

        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private UnityEngine.Camera viewCamera;

        private float verticalVelocity;

        private void Awake()
        {
            characterController ??= GetComponent<CharacterController>();
            viewCamera ??= UnityEngine.Camera.main;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = viewCamera != null
                ? viewCamera.transform.forward
                : Vector3.forward;
            Vector3 right = viewCamera != null
                ? viewCamera.transform.right
                : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 movement = (forward * input.y + right * input.x)
                * moveSpeed;

            if (characterController != null)
            {
                if (characterController.isGrounded && verticalVelocity < 0f)
                {
                    verticalVelocity = -0.5f;
                }

                verticalVelocity += Physics.gravity.y * Time.deltaTime;
                movement.y = verticalVelocity;
                characterController.Move(movement * Time.deltaTime);
            }
            else
            {
                transform.position += movement * Time.deltaTime;
            }
        }
    }
}
