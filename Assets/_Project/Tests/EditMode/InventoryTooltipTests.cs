using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// The hover window over a bag cell: that it opens with the right words, and
    /// that the words can actually be read.
    ///
    /// Every assertion here is about something a screenshot would have passed.
    /// A cell that holds a name and never shows it, a window with a rect too
    /// short for its own line — TMP draws **nothing** in that case rather than
    /// clipping (<c>ISSUE-047</c>) — and a window beside the last column that
    /// sits off the edge of the screen all look fine from the one place anybody
    /// looks: an open bag in the middle of a 4:3 editor view.
    /// </summary>
    public sealed class InventoryTooltipTests
    {
        private const string HudPrefabPath = "Assets/Resources/HudCanvas.prefab";
        private const float RootWidth = 1920f;
        private const float RootHeight = 1080f;

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                // DestroyImmediate, not Destroy: a plain Destroy in a test runs at
                // the end of the frame, so the next test finds the previous
                // window still parented under a root that was supposed to be gone.
                Object.DestroyImmediate(root);
                root = null;
            }
        }

        [Test]
        public void HoveringAPropCellNamesItAndSaysWhatItDoes()
        {
            InventorySlotView slot = BuildSlot(Vector2.zero);
            slot.Bind(Model(
                ThrowableCatalog.GetDisplayName(ThrowableKind.Rock),
                ThrowableCatalog.GetEffectSummary(ThrowableKind.Rock)));
            Hover(slot);

            InventoryTooltipView tooltip = Tooltip();
            Assert.That(tooltip, Is.Not.Null, "No tooltip was created.");
            Assert.That(tooltip.gameObject.activeSelf, Is.True);
            Assert.That(tooltip.Title, Is.EqualTo("돌"));

            // The number, not just some text. The sentence is composed from
            // ThrowableCatalog's constants so that a balance change moves it, and
            // this is the assertion that keeps it that way.
            Assert.That(tooltip.Body, Does.Contain("1.2"));
            Assert.That(Label(tooltip, "Body").enabled, Is.True);
        }

        [Test]
        public void HoveringTreasureNamesItAndLeavesTheEffectLineOut()
        {
            InventorySlotView slot = BuildSlot(Vector2.zero);
            slot.Bind(Model("금목걸이", string.Empty));
            Hover(slot);

            InventoryTooltipView tooltip = Tooltip();
            Assert.That(tooltip.gameObject.activeSelf, Is.True);
            Assert.That(tooltip.Title, Is.EqualTo("금목걸이"));

            // Disabled rather than blank. An enabled empty label still takes its
            // share of the height, which would open a window with a dead half —
            // the same "present but drawing nothing" shape as ISSUE-050.
            Assert.That(Label(tooltip, "Body").enabled, Is.False);
        }

        [Test]
        public void HoveringAnEmptyCellOpensNothing()
        {
            InventorySlotView slot = BuildSlot(Vector2.zero);
            slot.Bind(Model(string.Empty, string.Empty));
            Hover(slot);

            InventoryTooltipView tooltip = Tooltip();
            Assert.That(
                tooltip == null || !tooltip.gameObject.activeSelf,
                Is.True,
                "An empty cell opened a window.");
        }

        [Test]
        public void LeavingTheCellClosesTheWindow()
        {
            InventorySlotView slot = BuildSlot(Vector2.zero);
            slot.Bind(Model("돌", ThrowableCatalog.GetEffectSummary(ThrowableKind.Rock)));
            Hover(slot);
            slot.OnPointerExit(new PointerEventData(EventSystem.current));

            Assert.That(Tooltip().gameObject.activeSelf, Is.False);
        }

        // Closing the bag while the cursor is still on a cell is
        // InventoryTooltipPlayModeTests' job. It rests on OnDisable, and edit
        // mode does not deliver that for an object built inside a test — the
        // assertion passed for the wrong reason here and said nothing about the
        // game.

        /// <summary>
        /// A cell that empties under the cursor — dragged away, sold, handed to
        /// the cat — must stop describing what used to be in it.
        /// </summary>
        [Test]
        public void ACellThatEmptiesUnderTheCursorStopsDescribingItself()
        {
            InventorySlotView slot = BuildSlot(Vector2.zero);
            slot.Bind(Model("돌", ThrowableCatalog.GetEffectSummary(ThrowableKind.Rock)));
            Hover(slot);
            slot.Bind(Model(string.Empty, string.Empty));

            Assert.That(Tooltip().gameObject.activeSelf, Is.False);
        }

        [Test]
        public void TheWindowSitsBesideTheCellAndStaysOnScreen()
        {
            // Left of centre, where the bag panel is: the window goes to the
            // right of the cell.
            InventorySlotView left = BuildSlot(new Vector2(-700f, 0f));
            left.Bind(Model("돌", ThrowableCatalog.GetEffectSummary(ThrowableKind.Rock)));
            Hover(left);

            Rect window = WorldRect((RectTransform)Tooltip().transform);
            Rect cell = WorldRect((RectTransform)left.transform);
            Assert.That(
                window.xMin,
                Is.GreaterThanOrEqualTo(cell.xMax),
                "The window covered the cell it describes.");
            AssertInsideRoot(window);
        }

        [Test]
        public void TheWindowFlipsRatherThanRunningOffTheEdge()
        {
            InventorySlotView right = BuildSlot(new Vector2(910f, -480f));
            right.Bind(Model("돌", ThrowableCatalog.GetEffectSummary(ThrowableKind.Rock)));
            Hover(right);

            Rect window = WorldRect((RectTransform)Tooltip().transform);
            Rect cell = WorldRect((RectTransform)right.transform);
            Assert.That(
                window.xMax,
                Is.LessThanOrEqualTo(cell.xMin + 0.01f),
                "The window did not flip to the left of the cell.");
            AssertInsideRoot(window);
        }

        /// <summary>
        /// Both labels have to fit the rect they were given. TMP draws nothing at
        /// all when a rect is shorter than one line, so a window whose text is a
        /// few pixels too tall is an empty window rather than a clipped one
        /// (<c>ISSUE-047</c>). Checked with the longest effect line in the set.
        /// </summary>
        [Test]
        public void NeitherLabelIsTallerThanItsRect()
        {
            ThrowableKind longest = ThrowableKind.Rock;
            foreach (ThrowableKind kind in System.Enum.GetValues(typeof(ThrowableKind)))
            {
                if (ThrowableCatalog.GetEffectSummary(kind).Length
                    > ThrowableCatalog.GetEffectSummary(longest).Length)
                {
                    longest = kind;
                }
            }

            InventorySlotView slot = BuildSlot(Vector2.zero);
            slot.Bind(Model(
                ThrowableCatalog.GetDisplayName(longest),
                ThrowableCatalog.GetEffectSummary(longest)));
            Hover(slot);

            InventoryTooltipView tooltip = Tooltip();
            foreach (string name in new[] { "Title", "Body" })
            {
                TMP_Text label = Label(tooltip, name);
                Assert.That(
                    label.preferredHeight,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.5f),
                    $"{name} needs more height than its rect has ({longest}).");
            }
        }

        /// <summary>
        /// Every prop the player can hold explains itself, and the sentence
        /// carries the same number the rules layer uses.
        ///
        /// A hand-typed duration beside a constant that has moved is worse than
        /// no text: the player plans around the number they were shown.
        /// </summary>
        [Test]
        public void EveryPropExplainsItselfWithTheNumbersTheRulesUse()
        {
            foreach (ThrowableKind kind in System.Enum.GetValues(typeof(ThrowableKind)))
            {
                if (!ThrowableCatalog.CanUseInQuickSlot(kind))
                {
                    continue;
                }

                string summary = ThrowableCatalog.GetEffectSummary(kind);
                Assert.That(summary, Is.Not.Empty, kind.ToString());

                float stun = ThrowableCatalog.GetStunSeconds(kind);
                if (stun > 0f)
                {
                    Assert.That(summary, Does.Contain(Number(stun)), kind.ToString());
                }

                float blind = ThrowableCatalog.GetBlindSeconds(kind);
                if (blind > 0f)
                {
                    Assert.That(summary, Does.Contain(Number(blind)), kind.ToString());
                }

                float noise = ThrowableCatalog.GetNoiseRadius(kind);
                if (noise > 0f)
                {
                    Assert.That(summary, Does.Contain(Number(noise)), kind.ToString());
                }

                if (ThrowableCatalog.GetEffect(kind) == TrapEffect.Lure)
                {
                    Assert.That(
                        summary,
                        Does.Contain(Number(ThrowableCatalog.LureSeconds)),
                        kind.ToString());
                }
            }
        }

        /// <summary>
        /// The shipped cell has to be able to receive a pointer at all.
        ///
        /// Asserted against the prefab rather than the hand-built slot above: the
        /// hover behaviour can be perfect and reach nothing if the cell that is
        /// actually on screen has its raycast target switched off, and that is a
        /// one-line change in the builder that no other test would notice.
        /// </summary>
        [Test]
        public void TheShippedBagCellCanBeHovered()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null, HudPrefabPath);

            Transform cell = prefab.transform.Find("Inventory/Grid/Inventory Slot 1");
            Assert.That(cell, Is.Not.Null, "Inventory/Grid/Inventory Slot 1");
            Assert.That(cell.GetComponent<InventorySlotView>(), Is.Not.Null);

            Image background = cell.GetComponent<Image>();
            Assert.That(background, Is.Not.Null);
            Assert.That(
                background.raycastTarget,
                Is.True,
                "The cell cannot be hovered, so it can never open a tooltip.");
        }

        private static string Number(float value)
        {
            return value.ToString(
                "0.#",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static InventorySlotViewModel Model(string title, string body)
        {
            return new InventorySlotViewModel(
                "1",
                null,
                1,
                false,
                string.IsNullOrEmpty(title),
                tooltipTitle: title,
                tooltipBody: body);
        }

        /// <summary>
        /// A root of a known size, so the edge cases are arithmetic rather than
        /// whatever resolution the test runner happens to have.
        ///
        /// No <c>Canvas</c> component on purpose: a live canvas rewrites its own
        /// rect to the screen when it updates, and a test that measures the
        /// screen measures the machine it ran on. The tooltip walks up to the
        /// root when there is no canvas, which is the same transform either way.
        /// </summary>
        private InventorySlotView BuildSlot(Vector2 position)
        {
            if (root == null)
            {
                root = new GameObject("Fake Canvas", typeof(RectTransform));
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(RootWidth, RootHeight);
            }

            var cell = new GameObject("Inventory Slot", typeof(RectTransform));
            cell.transform.SetParent(root.transform, false);
            var rect = cell.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(70f, 70f);
            rect.anchoredPosition = position;
            return cell.AddComponent<InventorySlotView>();
        }

        private static void Hover(InventorySlotView slot)
        {
            slot.OnPointerEnter(new PointerEventData(EventSystem.current));
        }

        private InventoryTooltipView Tooltip()
        {
            return root == null
                ? null
                : root.GetComponentInChildren<InventoryTooltipView>(true);
        }

        private static TMP_Text Label(InventoryTooltipView tooltip, string name)
        {
            Transform label = tooltip.transform.Find(name);
            Assert.That(label, Is.Not.Null, name);
            return label.GetComponent<TMP_Text>();
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(
                corners[0].x,
                corners[0].y,
                corners[2].x - corners[0].x,
                corners[2].y - corners[0].y);
        }

        private void AssertInsideRoot(Rect window)
        {
            Rect bounds = WorldRect(root.GetComponent<RectTransform>());
            Assert.That(window.xMin, Is.GreaterThanOrEqualTo(bounds.xMin - 0.01f));
            Assert.That(window.xMax, Is.LessThanOrEqualTo(bounds.xMax + 0.01f));
            Assert.That(window.yMin, Is.GreaterThanOrEqualTo(bounds.yMin - 0.01f));
            Assert.That(window.yMax, Is.LessThanOrEqualTo(bounds.yMax + 0.01f));
        }
    }
}
