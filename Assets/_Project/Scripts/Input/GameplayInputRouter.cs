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

        /// <summary>
        /// The interact key, raised whether or not a panel is open.
        ///
        /// Only for interactions a screen answers — the cat's bag. Everything the
        /// key does to the world stays behind the suppression gate.
        /// </summary>
        public static event Action ScreenInteractionPressed;
        public static event Action InventoryTogglePressed;
        public static event Action VoicePressed;
        public static event Action EscapePressed;

        /// <summary>
        /// Empty the open container into the bag. Raised whether or not a panel is
        /// open — the HUD is the one that knows, and an input router that had to be
        /// told which screens exist would have to be told again for the next one.
        /// </summary>
        public static event Action TakeAllPressed;

        /// <summary>
        /// The bag screen asking for two prop slots to trade places.
        ///
        /// A static event rather than the HUD calling the network link directly:
        /// the UI layer does not reference <c>Integration.Network</c>, and the
        /// request has to reach the host on a machine that is only a client. The
        /// bridge that already forwards key presses forwards this the same way.
        /// </summary>
        public static event Action<int, int> QuickSlotSwapRequested;

        public static void RequestQuickSlotSwap(int left, int right)
        {
            QuickSlotSwapRequested?.Invoke(left, right);
        }

        /// <summary>
        /// The merchant screen asking the host to sell some of one kind.
        ///
        /// Raised only by a machine that is not the authority. Where the loot is
        /// this machine's own to sell, the screen sells it directly — going out
        /// and back would put a round trip between the click and the coins.
        /// </summary>
        public static event Action<int, int> LootSaleRequested;

        public static void RequestLootSale(int definitionIdHash, int count)
        {
            LootSaleRequested?.Invoke(definitionIdHash, count);
        }

        /// <summary>
        /// The officer's shop screen asking the host to buy one prop.
        ///
        /// Raised only by a machine that is not the authority, for the same reason
        /// the sale is: the host owns the quick slots and the purse, and a purchase
        /// applied locally would be overwritten by the next update — the officer
        /// would see the trap appear and vanish.
        /// </summary>
        public static event Action<int> PropPurchaseRequested;

        public static void RequestPropPurchase(int throwableKindValue)
        {
            PropPurchaseRequested?.Invoke(throwableKindValue);
        }

        /// <summary>
        /// The cat's bag screen asking the host to move one prop in or out.
        ///
        /// The bag itself is the thief's own machine's business — nothing else in
        /// the game reads it and the officer never sees it — but the quick slots
        /// on the other side of the exchange belong to the host. Applying only
        /// locally would put the banana in the cat's bag *and* leave it in the
        /// slot on the host's next update, which is the item-in-two-places bug
        /// <c>ContainerTransfer</c> exists to prevent.
        ///
        /// <paramref name="toCat"/> says which way: true takes the prop out of the
        /// quick slot, false hands one back.
        /// </summary>
        public static event Action<bool, int, int> CatBagTransferRequested;

        public static void RequestCatBagTransfer(
            bool toCat,
            int slotIndex,
            int throwableKindValue)
        {
            CatBagTransferRequested?.Invoke(
                toCat,
                slotIndex,
                throwableKindValue);
        }

        /// <summary>
        /// Whether a shop screen is open in front of the player.
        ///
        /// Separate from <see cref="GameplayInputSuppressed"/> on purpose. The
        /// raccoon's ledger deliberately leaves movement alone — its pitch is the
        /// one spot on the map the officer most wants to stand on, so a modal
        /// window would pin the thief there — but the same left button that
        /// presses BUY also throws whatever is in the quick slot, so a player
        /// shopping threw a rock at the raccoon with every click.
        /// </summary>
        public static bool ToolUseSuppressed { get; private set; }

        public static void SetToolUseSuppressed(bool suppressed)
        {
            ToolUseSuppressed = suppressed;
        }

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

            // Above the suppression gate on purpose, and the same key as throwing.
            //
            // F is the throw key, but throwing already stops the moment a panel
            // opens (`ToolUseInput` checks the same flag), so while a container is
            // on screen the key is free. Sharing it is better than finding a
            // seventh letter: the hand is already on F to throw, and "empty the
            // cupboard" is the throw of the searching half of the game.
            if (keyboard.fKey.wasPressedThisFrame)
            {
                TakeAllPressed?.Invoke();
            }

            // Above the gate, like the take-all key.
            //
            // The cat's bag is opened from this event, and opening the thief's own
            // bag suppresses gameplay input — so pressing E at the cat with the
            // bag already open reached nothing at all. The suppression is there to
            // stop the world being acted on through an open panel; a screen the
            // HUD opens beside that panel is not the world.
            if (keyboard.eKey.wasPressedThisFrame)
            {
                ScreenInteractionPressed?.Invoke();
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
