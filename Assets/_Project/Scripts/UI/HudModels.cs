using UnityEngine;

namespace PawliceAndPurrglar.UI
{
    public enum HudRole
    {
        Police = 0,
        Thief = 1
    }

    public readonly struct QuickSlotViewModel
    {
        public QuickSlotViewModel(
            string keyLabel,
            Sprite icon,
            int quantity,
            bool selected,
            bool disabled,
            float cooldown01,
            string iconGlyph = "",
            ItemTooltipContent tooltip = default)
        {
            KeyLabel = keyLabel;
            Icon = icon;
            Quantity = quantity;
            Selected = selected;
            Disabled = disabled;
            Cooldown01 = Mathf.Clamp01(cooldown01);
            IconGlyph = iconGlyph ?? string.Empty;
            Tooltip = tooltip;
        }

        public string KeyLabel { get; }
        public Sprite Icon { get; }
        public int Quantity { get; }
        public bool Selected { get; }
        public bool Disabled { get; }
        public float Cooldown01 { get; }
        public string IconGlyph { get; }

        /// <summary>
        /// What the hover tooltip should say about whatever is in this slot.
        ///
        /// Carried with the rest of the binding rather than looked up by the
        /// tooltip, so the words come from the same pass that decided the icon
        /// and the count. Default for an empty slot, and an empty tooltip means
        /// no panel at all.
        /// </summary>
        public ItemTooltipContent Tooltip { get; }
    }

    public readonly struct AnimalCommandShortcutViewModel
    {
        public AnimalCommandShortcutViewModel(
            string modifier,
            string key,
            string command,
            bool disabled)
        {
            Modifier = modifier;
            Key = key;
            Command = command;
            Disabled = disabled;
        }

        public string Modifier { get; }
        public string Key { get; }
        public string Command { get; }
        public bool Disabled { get; }
    }

    public readonly struct MicrophoneStatusViewModel
    {
        public MicrophoneStatusViewModel(
            string stateLabel,
            bool recording,
            bool processing,
            bool permissionFailed,
            float recordingProgress01,
            float cooldownSeconds,
            float cooldownMaximumSeconds = 0f)
        {
            StateLabel = stateLabel;
            Recording = recording;
            Processing = processing;
            PermissionFailed = permissionFailed;
            RecordingProgress01 = Mathf.Clamp01(recordingProgress01);
            CooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            CooldownProgress01 = cooldownMaximumSeconds > 0f
                ? Mathf.Clamp01(CooldownSeconds / cooldownMaximumSeconds)
                : 0f;
        }

        public string StateLabel { get; }
        public bool Recording { get; }
        public bool Processing { get; }
        public bool PermissionFailed { get; }
        public float RecordingProgress01 { get; }
        public float CooldownSeconds { get; }
        public float CooldownProgress01 { get; }
    }

    public readonly struct ContextInteractionPromptViewModel
    {
        public ContextInteractionPromptViewModel(
            bool visible,
            string keyLabel,
            string actionLabel,
            bool holding,
            float holdProgress01)
        {
            Visible = visible;
            KeyLabel = keyLabel;
            ActionLabel = actionLabel;
            Holding = holding;
            HoldProgress01 = Mathf.Clamp01(holdProgress01);
        }

        public bool Visible { get; }
        public string KeyLabel { get; }
        public string ActionLabel { get; }
        public bool Holding { get; }
        public float HoldProgress01 { get; }
    }

    public readonly struct RoleStatusPanelViewModel
    {
        public RoleStatusPanelViewModel(
            HudRole role,
            string roleLabel,
            string objective,
            string status)
        {
            Role = role;
            RoleLabel = roleLabel;
            Objective = objective;
            Status = status;
        }

        public HudRole Role { get; }
        public string RoleLabel { get; }
        public string Objective { get; }
        public string Status { get; }
    }

    public readonly struct MinimapAlertViewModel
    {
        public MinimapAlertViewModel(Sprite icon, Color tint, bool visible)
        {
            Icon = icon;
            Tint = tint;
            Visible = visible;
        }

        public Sprite Icon { get; }
        public Color Tint { get; }
        public bool Visible { get; }
    }

    public readonly struct InventorySlotViewModel
    {
        public InventorySlotViewModel(
            string keyLabel,
            Sprite icon,
            int quantity,
            bool selected,
            bool disabled,
            string iconGlyph = "",
            string itemName = "",
            int price = 0,
            Sprite priceIcon = null,
            bool isNew = false,
            ItemTooltipContent tooltip = default)
        {
            Tooltip = tooltip;
            KeyLabel = keyLabel;
            Icon = icon;
            Quantity = quantity;
            Selected = selected;
            Disabled = disabled;
            IconGlyph = iconGlyph ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            Price = Mathf.Max(0, price);
            PriceIcon = priceIcon;
            IsNew = isNew;
        }

        public string KeyLabel { get; }
        public Sprite Icon { get; }
        public int Quantity { get; }
        public bool Selected { get; }
        public bool Disabled { get; }
        public string IconGlyph { get; }
        public string ItemName { get; }
        public int Price { get; }
        public Sprite PriceIcon { get; }

        /// <summary>
        /// Whether to flag this cell as the newest thing in the bag.
        /// </summary>
        public bool IsNew { get; }

        /// <summary>
        /// What the hover tooltip should say about whatever is in this cell.
        /// Default for an empty cell, and an empty tooltip means no panel.
        /// </summary>
        public ItemTooltipContent Tooltip { get; }

        public bool HasPrice => Price > 0;
    }
}
