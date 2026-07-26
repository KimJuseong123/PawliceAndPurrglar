using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Companions
{
    public sealed class CompanionKeyboardInput : MonoBehaviour
    {
        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        public void Configure(
            LocalPlayerRoleSelector configuredRoleSelector,
            CompanionCommandDispatcher configuredDispatcher)
        {
            roleSelector = configuredRoleSelector;
            dispatcher = configuredDispatcher;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null
                || dispatcher == null
                || roleSelector == null
                || !roleSelector.IsGameplayInputEnabled)
            {
                return;
            }

            if (roleSelector.ActiveRole == PlayerRole.Police
                && keyboard.digit1Key.wasPressedThisFrame)
            {
                dispatcher.TryDispatch(
                    CompanionCommandId.Track,
                    CompanionCommandSource.Keyboard);
            }
            else if (roleSelector.ActiveRole == PlayerRole.Thief
                && keyboard.digit2Key.wasPressedThisFrame)
            {
                dispatcher.TryDispatch(
                    CompanionCommandId.Distract,
                    CompanionCommandSource.Keyboard);
            }
        }
    }
}
