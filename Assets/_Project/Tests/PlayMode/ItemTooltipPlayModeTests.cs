using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// The tooltip through the pointer, not through its own API.
    ///
    /// Driven with <see cref="ExecuteEvents"/> against the cell, because the part
    /// that goes wrong is never the panel — it is whether a hover reaches
    /// anything at all. A test that called <c>RequestShow</c> would pass with the
    /// trigger missing from every cell in the build, which is exactly the shape of
    /// <c>ISSUE-017</c>.
    ///
    /// It also asserts the panel is *readable*: active, opaque, and with rects
    /// tall enough for the text. TMP draws nothing from a rect shorter than one
    /// line (<c>ISSUE-047</c>), and a test that only asked "is there a label"
    /// would pass over an empty box.
    /// </summary>
    public sealed class ItemTooltipPlayModeTests
    {
        private GameObject canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (canvasObject != null)
            {
                Object.DestroyImmediate(canvasObject);
                canvasObject = null;
            }

            PawliceAndPurrglar.Input.GameplayInputRouter
                .SetGameplayInputSuppressed(false);
        }

        [UnityTest]
        public IEnumerator HoveringACellShowsTheTooltipOnlyAfterTheHoverDelay()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();
            InventorySlotView cell = FindCells().First();
            BindProp(cell, ThrowableKind.Banana);

            Enter(cell, new Vector2(600f, 500f));
            Assert.That(
                tooltip.IsShowing,
                Is.False,
                "A tooltip that appears on the same frame as the hover flashes "
                + "at every cell the cursor crosses.");

            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 0.4f);
            Assert.That(tooltip.IsShowing, Is.False, "Shown before the delay.");

            yield return WaitFor(ItemTooltipView.HoverDelaySeconds);
            Assert.That(tooltip.IsShowing, Is.True, "Never shown.");

            yield return WaitFor(ItemTooltipView.FadeSeconds * 2f);
            AssertPanelIsReadable(tooltip, ThrowableKind.Banana);
        }

        [UnityTest]
        public IEnumerator LeavingTheCellTakesTheTooltipAway()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();
            InventorySlotView cell = FindCells().First();
            BindProp(cell, ThrowableKind.Rock);

            Enter(cell, new Vector2(600f, 500f));
            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 2f);
            Assert.That(tooltip.IsShowing, Is.True);

            Exit(cell);
            yield return null;
            Assert.That(tooltip.IsShowing, Is.False);
            Assert.That(
                PanelOf(tooltip).activeInHierarchy,
                Is.False,
                "The panel is still drawn after the cursor left.");
        }

        /// <summary>
        /// Moving between neighbouring cells swaps the words rather than waiting
        /// the delay out again — the exit and the enter arrive in the same frame,
        /// and a fresh delay per cell makes a row of items feel unresponsive.
        /// </summary>
        [UnityTest]
        public IEnumerator MovingToTheNextCellSwapsTheContentsImmediately()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();
            InventorySlotView[] cells = FindCells();
            BindProp(cells[0], ThrowableKind.Rock);
            BindProp(cells[1], ThrowableKind.Banana);

            Enter(cells[0], new Vector2(600f, 500f));
            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 2f);
            Assert.That(
                NameLabelOf(tooltip).text,
                Is.EqualTo(ThrowableCatalog.GetDisplayName(ThrowableKind.Rock)));

            Exit(cells[0]);
            Enter(cells[1], new Vector2(660f, 500f));
            Assert.That(
                tooltip.IsShowing,
                Is.True,
                "The neighbouring cell had to wait for the delay again.");
            Assert.That(
                NameLabelOf(tooltip).text,
                Is.EqualTo(
                    ThrowableCatalog.GetDisplayName(ThrowableKind.Banana)));
        }

        [UnityTest]
        public IEnumerator AnEmptyCellHasNoTooltipAtAll()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();

            // Bound as empty, which is what every cell of a bag with nothing in it
            // is. Twenty-one panels saying nothing would be worse than none.
            InventorySlotView cell = FindCells().First();
            cell.Bind(new InventorySlotViewModel("1", null, 0, false, true));

            Enter(cell, new Vector2(600f, 500f));
            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 3f);
            Assert.That(tooltip.IsShowing, Is.False);
        }

        /// <summary>
        /// A cell emptied under the cursor — sold, dropped, handed to the cat —
        /// takes its tooltip with it. No pointer event arrives to say so, so the
        /// panel has to notice on its own.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTooltipGoesWhenTheCellItDescribesEmpties()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();
            InventorySlotView cell = FindCells().First();
            BindProp(cell, ThrowableKind.Rock);

            Enter(cell, new Vector2(600f, 500f));
            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 2f);
            Assert.That(tooltip.IsShowing, Is.True);

            cell.Bind(new InventorySlotViewModel("1", null, 0, false, true));
            yield return null;
            Assert.That(tooltip.IsShowing, Is.False);
        }

        /// <summary>
        /// Closing the bag with the cursor over a cell sends no pointer-exit, so
        /// without this the panel is left floating over the town.
        /// </summary>
        [UnityTest]
        public IEnumerator ClosingTheBagTakesTheTooltipWithIt()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();
            InventorySlotView cell = FindCells().First();
            BindProp(cell, ThrowableKind.Rock);

            Enter(cell, new Vector2(600f, 500f));
            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 2f);
            Assert.That(tooltip.IsShowing, Is.True);

            // The panel switched off under the cursor, which is what closing the
            // bag does. No pointer event is raised for it.
            canvasObject.transform.Find("Inventory").gameObject.SetActive(false);
            yield return null;
            Assert.That(tooltip.IsShowing, Is.False);
            Assert.That(PanelOf(tooltip).activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator NothingInTheTooltipCanBeHitByThePointer()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();
            InventorySlotView cell = FindCells().First();
            BindProp(cell, ThrowableKind.Rock);
            Enter(cell, new Vector2(600f, 500f));
            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 2f);

            foreach (Graphic graphic in tooltip
                .GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(
                    graphic.raycastTarget,
                    Is.False,
                    $"{graphic.name} can be hit, which steals the cell's exit "
                    + "event and makes the panel flicker.");
            }

            var group = PanelOf(tooltip).GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
        }

        /// <summary>
        /// The panel stays on the canvas wherever the cursor is. Checked at the
        /// bottom-right corner, which is the one that used to clip: the offset
        /// pushes down and to the right, so that corner is where flipping has to
        /// happen in both directions at once.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTooltipStaysOnScreenInTheBottomRightCorner()
        {
            yield return BuildBag();
            ItemTooltipView tooltip = FindTooltip();
            InventorySlotView cell = FindCells().First();
            BindProp(cell, ThrowableKind.FrozenOctopus);

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            Enter(
                cell,
                new Vector2(Screen.width - 4f, 4f));
            yield return WaitFor(ItemTooltipView.HoverDelaySeconds * 2f);
            Assert.That(tooltip.IsShowing, Is.True);

            RectTransform panel = PanelOf(tooltip).GetComponent<RectTransform>();
            Vector2 corner = panel.anchoredPosition;
            Vector2 size = panel.rect.size;
            Vector2 half = canvasRect.rect.size * 0.5f;
            Assert.That(
                corner.x,
                Is.GreaterThanOrEqualTo(-half.x - 0.5f),
                "Off the left of the canvas.");
            Assert.That(
                corner.x + size.x,
                Is.LessThanOrEqualTo(half.x + 0.5f),
                "Off the right of the canvas.");
            Assert.That(
                corner.y,
                Is.LessThanOrEqualTo(half.y + 0.5f),
                "Off the top of the canvas.");
            Assert.That(
                corner.y - size.y,
                Is.GreaterThanOrEqualTo(-half.y - 0.5f),
                "Off the bottom of the canvas.");
        }

        /// <summary>
        /// Builds the HUD and opens the bag, with the controller switched off.
        ///
        /// The controller rebinds all twenty-five cells every frame from the
        /// player's carrier, and there is no player here — left enabled, it would
        /// wipe the cells this test binds by hand on the next frame and the
        /// tooltip would correctly refuse to describe an empty cell.
        /// </summary>
        private IEnumerator BuildBag()
        {
            canvasObject = HudRuntimeInstaller.BuildRuntimeCanvas();
            var hud = canvasObject.GetComponent<RoleAwareHudController>();
            hud.enabled = false;
            Transform inventory = canvasObject.transform.Find("Inventory");
            Assert.That(inventory, Is.Not.Null, "No bag panel on the HUD.");
            inventory.gameObject.SetActive(true);
            yield return null;
        }

        private static void BindProp(InventorySlotView cell, ThrowableKind kind)
        {
            cell.Bind(new InventorySlotViewModel(
                "1",
                null,
                1,
                false,
                false,
                iconGlyph: "?",
                tooltip: ItemTooltipCatalog.ForProp(kind, null)));
        }

        private InventorySlotView[] FindCells()
        {
            Transform grid = canvasObject.transform.Find("Inventory/Grid");
            Assert.That(grid, Is.Not.Null);
            InventorySlotView[] cells =
                grid.GetComponentsInChildren<InventorySlotView>(true);
            Assert.That(cells, Is.Not.Empty);
            foreach (InventorySlotView cell in cells)
            {
                Assert.That(
                    cell.GetComponent<ItemSlotTooltipTrigger>(),
                    Is.Not.Null,
                    $"{cell.name} has no hover trigger, so no tooltip can ever "
                    + "appear over it.");
            }

            return cells;
        }

        private ItemTooltipView FindTooltip()
        {
            ItemTooltipView[] found =
                canvasObject.GetComponentsInChildren<ItemTooltipView>(true);
            Assert.That(
                found,
                Has.Length.EqualTo(1),
                "One tooltip per canvas, shared by every slot.");
            Assert.That(
                found[0].IsShowing,
                Is.False,
                "The tooltip must start hidden.");
            return found[0];
        }

        private static GameObject PanelOf(ItemTooltipView tooltip)
        {
            Transform panel = tooltip.transform.Find("Panel");
            Assert.That(panel, Is.Not.Null);
            return panel.gameObject;
        }

        private static TMP_Text NameLabelOf(ItemTooltipView tooltip)
        {
            Transform label = tooltip.transform.Find("Panel/Header/Titles/Item Name");
            Assert.That(label, Is.Not.Null, "No name label in the tooltip.");
            return label.GetComponent<TMP_Text>();
        }

        private static void AssertPanelIsReadable(
            ItemTooltipView tooltip,
            ThrowableKind kind)
        {
            GameObject panel = PanelOf(tooltip);
            Assert.That(panel.activeInHierarchy, Is.True);
            var group = panel.GetComponent<CanvasGroup>();
            Assert.That(
                group.alpha,
                Is.EqualTo(1f).Within(0.01f),
                "The panel never finished fading in.");

            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!label.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Assert.That(
                    label.fontSize,
                    Is.GreaterThan(9f),
                    $"{label.name} is too small to read.");
                Assert.That(
                    label.color.a,
                    Is.GreaterThan(0.5f),
                    $"{label.name} is transparent.");
                Assert.That(
                    label.preferredHeight,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.5f),
                    $"{label.name} needs more height than its rect has, which is "
                    + "how TMP ends up drawing nothing at all.");
            }

            Assert.That(
                NameLabelOf(tooltip).text,
                Is.EqualTo(ThrowableCatalog.GetDisplayName(kind)));
            Assert.That(
                tooltip.transform
                    .Find("Panel/Header/Titles/Category")
                    .GetComponent<TMP_Text>()
                    .text,
                Is.EqualTo(ThrowableCatalog.GetCategoryLabel(kind)));
            Assert.That(
                tooltip.transform
                    .Find("Panel/Description")
                    .GetComponent<TMP_Text>()
                    .text,
                Is.EqualTo(ThrowableCatalog.GetShortDescription(kind)));
            Assert.That(
                tooltip.transform
                    .Find("Panel/Usage Hint")
                    .GetComponent<TMP_Text>()
                    .text,
                Is.EqualTo(ThrowableCatalog.GetUsageHint(kind)));
        }

        private static void Enter(Component target, Vector2 screenPosition)
        {
            var data = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
            ExecuteEvents.Execute(
                target.gameObject,
                data,
                ExecuteEvents.pointerEnterHandler);
        }

        private static void Exit(Component target)
        {
            ExecuteEvents.Execute(
                target.gameObject,
                new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerExitHandler);
        }

        private static IEnumerator WaitFor(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
