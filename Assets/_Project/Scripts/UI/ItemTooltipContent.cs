using UnityEngine;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// The five things a hover tooltip says about one item.
    ///
    /// Deliberately five strings and a sprite rather than the item itself. The
    /// tooltip must not be able to ask what a banana does — the whole point of
    /// this shape is that the answer is decided where the item is defined
    /// (<c>ThrowableCatalog</c>, <c>LootDefinition</c>) and carried here as
    /// finished words. A view that can see the item is a view that will
    /// eventually branch on it, and then the same prop reads two ways depending
    /// on which panel you hovered.
    ///
    /// A struct with no allocation, because the HUD rebinds every cell every
    /// frame: this is built twenty-five times a frame whether or not anybody is
    /// hovering. The strings it holds are all cached constants for the same
    /// reason.
    /// </summary>
    public readonly struct ItemTooltipContent
    {
        public ItemTooltipContent(
            Sprite icon,
            string itemName,
            string category,
            string description,
            string usageHint = "")
        {
            Icon = icon;
            ItemName = itemName ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            UsageHint = usageHint ?? string.Empty;
        }

        public Sprite Icon { get; }
        public string ItemName { get; }
        public string Category { get; }
        public string Description { get; }
        public string UsageHint { get; }

        /// <summary>
        /// Whether there is anything worth putting on screen.
        ///
        /// Keyed on the name rather than on the icon. Most of the thirty-odd
        /// loot definitions have no artwork yet and are drawn as a letter, and
        /// deciding by the sprite would have meant the items that most need
        /// explaining were the ones with no tooltip.
        /// </summary>
        public bool HasContent => !string.IsNullOrWhiteSpace(ItemName);

        /// <summary>
        /// Whether this says the same thing as <paramref name="other"/>.
        ///
        /// Used to keep a showing tooltip from re-assigning four TMP strings
        /// every frame while the cursor sits still. The strings on both sides
        /// are cached, so this is usually a reference comparison.
        /// </summary>
        public bool SaysTheSameAs(in ItemTooltipContent other)
        {
            return Icon == other.Icon
                && string.Equals(ItemName, other.ItemName)
                && string.Equals(Category, other.Category)
                && string.Equals(Description, other.Description)
                && string.Equals(UsageHint, other.UsageHint);
        }
    }

    /// <summary>
    /// A slot view that knows what its own contents should say.
    ///
    /// Implemented by the slot views rather than read off them by the tooltip,
    /// so the path stays one-way: item data → slot binding → hover → tooltip.
    /// </summary>
    public interface IItemTooltipContentSource
    {
        /// <summary>
        /// False when the slot is empty, unusable, or holding something with
        /// nothing to say. An empty cell has no tooltip at all.
        /// </summary>
        bool TryGetTooltipContent(out ItemTooltipContent content);
    }

    /// <summary>
    /// Something that has asked for the tooltip and can still be asked about it.
    ///
    /// Separate from <see cref="IItemTooltipContentSource"/> so the tooltip does
    /// not depend on slots at all: anything that can answer "am I still there?"
    /// and "what do you say?" can raise one. That is what keeps a future gamepad
    /// focus or a world-space label from needing a slot to hang off.
    /// </summary>
    public interface IItemTooltipRequester
    {
        /// <summary>
        /// Whether this is still on screen and still wants the tooltip.
        ///
        /// Asked every frame while the tooltip is up, because the thing that
        /// closes a panel does not send a pointer-exit: closing the bag with the
        /// cursor over a cell leaves the tooltip floating over the world with no
        /// event coming to take it down.
        /// </summary>
        bool IsTooltipRequesterAlive { get; }

        bool TryGetTooltipContent(out ItemTooltipContent content);
    }
}
