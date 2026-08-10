using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// The one thing about the hover window that edit mode cannot answer: what
    /// happens when the bag closes with the cursor still on a cell.
    ///
    /// The window closes on the cell's <c>OnDisable</c>, because no exit event
    /// arrives — the bag is closed with a key and the whole panel is switched off
    /// from under the pointer. Edit mode does not deliver <c>OnDisable</c> for an
    /// object a test built, so the same assertion there passed without exercising
    /// anything. The failure it is guarding against is a window left hanging over
    /// the game with the bag gone from behind it.
    /// </summary>
    public sealed class InventoryTooltipPlayModeTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                // DestroyImmediate: a plain Destroy lands at the end of the
                // frame, and the next test would find this run's window still
                // parented under a root that was supposed to be gone.
                Object.DestroyImmediate(root);
                root = null;
            }
        }

        [UnityTest]
        public IEnumerator ClosingTheBagClosesTheWindow()
        {
            InventorySlotView slot = BuildSlot(out GameObject panel);
            slot.Bind(new InventorySlotViewModel(
                "1",
                null,
                1,
                false,
                false,
                tooltipTitle: ThrowableCatalog.GetDisplayName(ThrowableKind.Rock),
                tooltipBody: ThrowableCatalog.GetEffectSummary(ThrowableKind.Rock)));
            slot.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return null;

            InventoryTooltipView tooltip =
                root.GetComponentInChildren<InventoryTooltipView>(true);
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(
                tooltip.gameObject.activeSelf,
                Is.True,
                "The window never opened, so this test proves nothing about closing it.");

            panel.SetActive(false);
            yield return null;

            Assert.That(
                tooltip.gameObject.activeSelf,
                Is.False,
                "The window outlived the bag it was describing.");
        }

        private InventorySlotView BuildSlot(out GameObject panel)
        {
            root = new GameObject("Fake Canvas", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);

            panel = new GameObject("Inventory", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);

            var cell = new GameObject("Inventory Slot", typeof(RectTransform));
            cell.transform.SetParent(panel.transform, false);
            var rect = cell.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(70f, 70f);
            return cell.AddComponent<InventorySlotView>();
        }
    }
}
