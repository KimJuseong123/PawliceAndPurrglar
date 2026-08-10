using UnityEngine;
using UnityEngine.EventSystems;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Raises the shared tooltip for the slot it is sitting on.
    ///
    /// Added to every cell by the HUD builders and given nothing to configure —
    /// it finds the tooltip on its own canvas and the contents on its own
    /// GameObject. That is deliberate: an editor script cannot hand a component
    /// a reference to a delegate or fill a serialized list that survives being
    /// written to a prefab (<c>ISSUE-017</c>, <c>ISSUE-031</c>), so anything the
    /// build needs has to be found at runtime.
    ///
    /// It holds no words of its own. What the tooltip says comes from whatever
    /// <see cref="IItemTooltipContentSource"/> is beside it, which in turn got it
    /// from the item data when the slot was bound.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemSlotTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IItemTooltipRequester
    {
        private ItemTooltipView tooltip;
        private IItemTooltipContentSource source;
        private bool sourceResolved;

        public bool IsTooltipRequesterAlive =>
            this != null && isActiveAndEnabled && gameObject.activeInHierarchy;

        public bool TryGetTooltipContent(out ItemTooltipContent content)
        {
            content = default;
            IItemTooltipContentSource resolved = ResolveSource();
            return resolved != null
                && resolved.TryGetTooltipContent(out content)
                && content.HasContent;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ResolveTooltip()?.RequestShow(
                this,
                eventData != null ? eventData.position : Vector2.zero);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResolveTooltip()?.CancelShow(this);
        }

        /// <summary>
        /// A cell that goes away takes its tooltip with it.
        ///
        /// Closing the bag disables the whole grid without the pointer ever
        /// leaving the cell, so this is the only notice the tooltip gets on that
        /// path. The tooltip also checks for itself, and both are worth having:
        /// this one is immediate, and the other one catches the cell being
        /// destroyed rather than switched off.
        /// </summary>
        private void OnDisable()
        {
            if (tooltip != null)
            {
                tooltip.CancelShow(this);
            }
        }

        private ItemTooltipView ResolveTooltip()
        {
            if (tooltip == null)
            {
                tooltip = ItemTooltipView.Find(this);
            }

            return tooltip;
        }

        private IItemTooltipContentSource ResolveSource()
        {
            if (!sourceResolved)
            {
                source = GetComponent<IItemTooltipContentSource>();
                sourceResolved = true;
            }

            return source;
        }
    }
}
