using System;
using PawsAndLoot.Integration.Voice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Input
{
    /// <summary>
    /// Single keyboard edge router for the production gameplay mapping.
    /// NetworkInputBridge remains authoritative in network sessions, while
    /// local components consume these same events in offline play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayInputRouter : MonoBehaviour
    {
        private static readonly string[] DefaultQuickSlotLabels = { "1", "2", "3", "4" };
        private static readonly string[] DefaultAnimalCommandLabels = { "1", "2", "3", "4" };

        private static string[] quickSlotLabels = (string[])DefaultQuickSlotLabels.Clone();
        private static string[] animalCommandLabels = (string[])DefaultAnimalCommandLabels.Clone();
        public static string VoiceBindingLabel { get; private set; } = "V";
        public static string InventoryBindingLabel { get; private set; } = "TAB";
        public static string InteractionBindingLabel { get; private set; } = "E";
        public static bool GameplayInputSuppressed { get; private set; }

        public static event Action<int> QuickSlotPressed;
        public static event Action<int> AnimalCommandPressed;
        public static event Action ContextInteractionPressed;
        public static event Action InventoryTogglePressed;
        public static event Action VoicePressed;
        public static event Action EscapePressed;
        public static event Action BindingDisplayChanged;

        public static void SetGameplayInputSuppressed(bool suppressed)
        {
            if (GameplayInputSuppressed == suppressed)
            {
                return;
            }

            GameplayInputSuppressed = suppressed;
            if (suppressed)
            {
                CancelVoiceCapture();
            }
        }

        public static string GetQuickSlotLabel(int slot) =>
            slot >= 0 && slot < quickSlotLabels.Length ? quickSlotLabels[slot] : string.Empty;

        public static string GetAnimalCommandLabel(int command) =>
            command > 0 && command <= animalCommandLabels.Length
                ? $"CTRL + {animalCommandLabels[command - 1]}"
                : string.Empty;

        public static void SetBindingDisplayLabels(
            string[] configuredQuickSlotLabels,
            string[] configuredAnimalCommandLabels,
            string voiceLabel = "V",
            string inventoryLabel = "TAB",
            string interactionLabel = "E")
        {
            if (configuredQuickSlotLabels != null
                && configuredQuickSlotLabels.Length == 4)
            {
                quickSlotLabels = (string[])configuredQuickSlotLabels.Clone();
            }

            if (configuredAnimalCommandLabels != null
                && configuredAnimalCommandLabels.Length == 4)
            {
                animalCommandLabels = (string[])configuredAnimalCommandLabels.Clone();
            }

            VoiceBindingLabel = string.IsNullOrWhiteSpace(voiceLabel) ? "V" : voiceLabel;
            InventoryBindingLabel = string.IsNullOrWhiteSpace(inventoryLabel) ? "TAB" : inventoryLabel;
            InteractionBindingLabel = string.IsNullOrWhiteSpace(interactionLabel) ? "E" : interactionLabel;

            BindingDisplayChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<GameplayInputRouter>() != null)
            {
                return;
            }

            var root = new GameObject("Gameplay Input Router");
            DontDestroyOnLoad(root);
            root.AddComponent<GameplayInputRouter>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                InventoryTogglePressed?.Invoke();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelVoiceCapture();
                EscapePressed?.Invoke();
            }

            if (GameplayInputSuppressed)
            {
                return;
            }

            bool ctrl = keyboard.leftCtrlKey.isPressed
                || keyboard.rightCtrlKey.isPressed;
            int number = ReadNumberKey(keyboard);
            if (number > 0)
            {
                if (ctrl)
                {
                    AnimalCommandPressed?.Invoke(number);
                }
                else
                {
                    QuickSlotPressed?.Invoke(number - 1);
                }
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                ContextInteractionPressed?.Invoke();
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                VoicePressed?.Invoke();
                foreach (VoiceCommandInput voice in
                    FindObjectsByType<VoiceCommandInput>(FindObjectsSortMode.None))
                {
                    voice.StartListening();
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelVoiceCapture();
            }
        }

        private static void CancelVoiceCapture()
        {
            foreach (VoiceCommandInput voice in
                FindObjectsByType<VoiceCommandInput>(FindObjectsSortMode.None))
            {
                voice.CancelListening();
            }
        }

        private static int ReadNumberKey(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) return 1;
            if (keyboard.digit2Key.wasPressedThisFrame) return 2;
            if (keyboard.digit3Key.wasPressedThisFrame) return 3;
            if (keyboard.digit4Key.wasPressedThisFrame) return 4;
            return 0;
        }
    }
}
