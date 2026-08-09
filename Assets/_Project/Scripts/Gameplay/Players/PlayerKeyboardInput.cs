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
            if (keyboard != null
                && (keyboard.leftShiftKey.wasPressedThisFrame
                    || keyboard.rightShiftKey.wasPressedThisFrame))
            {
                movementMotor.TryStartDash(input);
            }

            if (keyboard?.spaceKey.wasPressedThisFrame == true)
            {
                // The sound is raised here, on the press, rather than by the
                // observer watching `IsAirborne` go true. Leaving the ground is
                // not the same event as jumping: stepping off a kerb, walking
                // down the park steps and crossing any slope all raise it, and
                // the host simulates both characters, so an officer heard the
                // thief's kerbs across town. On the press it is once per press,
                // on this machine only (`ISSUE-072`).
                movementMotor.TryJump();
                Audio.GameSoundService.Request(Audio.GameSoundId.Jump);
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
