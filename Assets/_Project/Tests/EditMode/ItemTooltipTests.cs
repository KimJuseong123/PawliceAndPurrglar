using System;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.UI;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// What the hover tooltip says, and where it goes.
    ///
    /// Both halves are checked without a screen. The words come from static
    /// catalogues, and the placement is a pure function precisely so the four
    /// corners can be tested here rather than by looking at a screenshot and
    /// deciding it seems fine — a tooltip clipped at the right edge is invisible
    /// in every log and obvious only to whoever happens to hover there.
    /// </summary>
    public sealed class ItemTooltipTests
    {
        private static readonly Vector2 Canvas1080 = new(1920f, 1080f);

        [Test]
        public void EveryPropSaysAName_ACategory_ADescriptionAndHowToUseIt()
        {
            foreach (ThrowableKind kind in Enum.GetValues(typeof(ThrowableKind)))
            {
                ItemTooltipContent content =
                    ItemTooltipCatalog.ForProp(kind, null);
                Assert.That(content.HasContent, Is.True, kind.ToString());
                Assert.That(
                    content.ItemName,
                    Is.EqualTo(ThrowableCatalog.GetDisplayName(kind)),
                    $"{kind} must be named the same here as everywhere else.");
                Assert.That(content.Category, Is.Not.Empty, kind.ToString());
                Assert.That(
                    content.Category,
                    Does.Contain("아이템"),
                    kind.ToString());
                Assert.That(content.Description, Is.Not.Empty, kind.ToString());
                Assert.That(content.UsageHint, Is.Not.Empty, kind.ToString());

                // A description that fits in a slot rather than one that fills
                // the screen. Two lines at this width is about ninety characters.
                Assert.That(
                    content.Description.Length,
                    Is.LessThanOrEqualTo(60),
                    $"{kind} description is too long for a hover panel.");
            }
        }

        /// <summary>
        /// The reason the sentences are built from the constants: a prop whose
        /// hold was retuned must not keep quoting the old number.
        /// </summary>
        [Test]
        public void PropDescriptionsQuoteTheDurationTheRulesActuallyUse()
        {
            foreach (ThrowableKind kind in Enum.GetValues(typeof(ThrowableKind)))
            {
                float seconds = ThrowableCatalog.GetStunSeconds(kind);
                if (seconds <= 0f)
                {
                    continue;
                }

                Assert.That(
                    ThrowableCatalog.GetShortDescription(kind),
                    Does.Contain(seconds.ToString("0.#")),
                    $"{kind} describes a duration other than its own.");
            }

            Assert.That(
                ThrowableCatalog.GetShortDescription(ThrowableKind.FrozenOctopus),
                Does.Contain(ThrowableCatalog.BlindSeconds.ToString("0.#")));
            Assert.That(
                ThrowableCatalog.GetShortDescription(ThrowableKind.Firework),
                Does.Contain(
                    ThrowableCatalog.FireworkFuseSeconds.ToString("0.#")));
            Assert.That(
                ThrowableCatalog.GetShortDescription(ThrowableKind.SensorLight),
                Does.Contain(ThrowableCatalog.RevealSeconds.ToString("0.#")));
        }

        [Test]
        public void PropCategoryNamesWhoseSideTheItemIsOn()
        {
            foreach (ThrowableKind kind in Enum.GetValues(typeof(ThrowableKind)))
            {
                string category = ThrowableCatalog.GetCategoryLabel(kind);
                PlayerRole? owner = ThrowableCatalog.GetOwner(kind);
                switch (owner)
                {
                    case PlayerRole.Police:
                        Assert.That(category, Does.Contain("경찰"), kind.ToString());
                        break;
                    case PlayerRole.Thief:
                        Assert.That(category, Does.Contain("도둑"), kind.ToString());
                        break;
                    default:
                        Assert.That(category, Does.Contain("공용"), kind.ToString());
                        break;
                }

                bool thrown = ThrowableCatalog.GetUse(kind) == ThrowableUse.Thrown;
                Assert.That(
                    category,
                    Does.Contain(thrown ? "투척" : "설치"),
                    kind.ToString());
            }
        }

        [Test]
        public void ThrownAndPlacedPropsTellThePlayerDifferentThings()
        {
            Assert.That(
                ThrowableCatalog.GetUsageHint(ThrowableKind.Rock),
                Is.Not.EqualTo(
                    ThrowableCatalog.GetUsageHint(ThrowableKind.GlueTrap)));

            // Both bindings, because ToolUseInput accepts both. A hint that named
            // only the mouse would be wrong for the hand on the keyboard.
            Assert.That(
                ThrowableCatalog.GetUsageHint(ThrowableKind.Rock),
                Does.Contain("좌클릭").And.Contain("F"));
        }

        [Test]
        public void TreasureIsDescribedByItsRarityAndWhatCarryingItCosts()
        {
            LootDefinition pocket = MakeLoot(
                "watch",
                "손목시계",
                LootRarity.Common,
                LootCarryType.Pocket,
                false);
            LootDefinition bulky = MakeLoot(
                "crown",
                "왕관",
                LootRarity.Rare,
                LootCarryType.Bulky,
                true);

            ItemTooltipContent pocketTooltip =
                ItemTooltipCatalog.ForLoot(pocket, null);
            ItemTooltipContent bulkyTooltip =
                ItemTooltipCatalog.ForLoot(bulky, null);

            Assert.That(pocketTooltip.ItemName, Is.EqualTo("손목시계"));
            Assert.That(bulkyTooltip.ItemName, Is.EqualTo("왕관"));
            Assert.That(
                pocketTooltip.Category,
                Is.Not.EqualTo(bulkyTooltip.Category),
                "Two rarities must not read as the same kind of thing.");
            Assert.That(bulkyTooltip.Category, Does.Contain("희귀"));
            Assert.That(
                pocketTooltip.Description,
                Is.Not.EqualTo(bulkyTooltip.Description),
                "A watch and a crown do not cost the same to carry.");

            // The alarm is on the piece, so it has to be on the piece's tooltip.
            // A thief deciding whether to lift the crown is deciding about this.
            Assert.That(bulkyTooltip.Description, Does.Contain("경보"));
            Assert.That(pocketTooltip.Description, Does.Not.Contain("경보"));

            Assert.That(pocketTooltip.UsageHint, Is.Not.Empty);

            UnityEngine.Object.DestroyImmediate(pocket);
            UnityEngine.Object.DestroyImmediate(bulky);
        }

        /// <summary>
        /// An empty cell has no tooltip, and neither does a missing definition.
        /// This is the check that keeps a panel from appearing over the twenty-one
        /// blank cells an officer's bag is made of.
        /// </summary>
        [Test]
        public void NothingToSayMeansNoPanel()
        {
            Assert.That(default(ItemTooltipContent).HasContent, Is.False);
            Assert.That(
                ItemTooltipCatalog.ForLoot(null, null).HasContent,
                Is.False);
            Assert.That(
                new ItemTooltipContent(null, "   ", "분류", "설명").HasContent,
                Is.False);
        }

        [Test]
        public void TooltipStaysWhollyInsideTheCanvasFromAnywhereOnScreen()
        {
            var size = new Vector2(ItemTooltipView.PanelWidth, 168f);
            for (float x = -960f; x <= 960f; x += 96f)
            {
                for (float y = -540f; y <= 540f; y += 60f)
                {
                    var cursor = new Vector2(x, y);
                    Vector2 corner = ItemTooltipView.ResolvePanelPosition(
                        cursor,
                        size,
                        Canvas1080,
                        ItemTooltipView.CursorOffset);
                    const float slack = 0.001f;
                    Assert.That(
                        corner.x,
                        Is.GreaterThanOrEqualTo(-960f - slack),
                        $"Cut off on the left at {cursor}.");
                    Assert.That(
                        corner.x + size.x,
                        Is.LessThanOrEqualTo(960f + slack),
                        $"Cut off on the right at {cursor}.");
                    Assert.That(
                        corner.y,
                        Is.LessThanOrEqualTo(540f + slack),
                        $"Cut off at the top at {cursor}.");
                    Assert.That(
                        corner.y - size.y,
                        Is.GreaterThanOrEqualTo(-540f - slack),
                        $"Cut off at the bottom at {cursor}.");
                }
            }
        }

        /// <summary>
        /// Offset, not centred. A panel under the arrow hides the first thing the
        /// player is trying to read, and — worse — a tooltip whose rect contains
        /// the cursor is one pointer-exit away from flickering.
        /// </summary>
        [Test]
        public void TooltipNeverSitsUnderTheCursor()
        {
            var size = new Vector2(ItemTooltipView.PanelWidth, 168f);
            for (float x = -960f; x <= 960f; x += 64f)
            {
                for (float y = -540f; y <= 540f; y += 45f)
                {
                    var cursor = new Vector2(x, y);
                    Vector2 corner = ItemTooltipView.ResolvePanelPosition(
                        cursor,
                        size,
                        Canvas1080,
                        ItemTooltipView.CursorOffset);
                    bool coversCursor = cursor.x >= corner.x
                        && cursor.x <= corner.x + size.x
                        && cursor.y <= corner.y
                        && cursor.y >= corner.y - size.y;
                    Assert.That(
                        coversCursor,
                        Is.False,
                        $"The panel is under the cursor at {cursor}.");
                }
            }
        }

        [Test]
        public void TooltipFlipsToTheOtherSideRatherThanBeingSquashed()
        {
            var size = new Vector2(ItemTooltipView.PanelWidth, 168f);

            // Hard against the right edge: the panel has to go left of the
            // cursor, and it must keep its full width doing it.
            Vector2 atRight = ItemTooltipView.ResolvePanelPosition(
                new Vector2(950f, 0f),
                size,
                Canvas1080,
                ItemTooltipView.CursorOffset);
            Assert.That(atRight.x + size.x, Is.LessThanOrEqualTo(950f));

            // Hard against the bottom: it has to go above the cursor.
            Vector2 atBottom = ItemTooltipView.ResolvePanelPosition(
                new Vector2(0f, -530f),
                size,
                Canvas1080,
                ItemTooltipView.CursorOffset);
            Assert.That(atBottom.y - size.y, Is.GreaterThanOrEqualTo(-530f));
        }

        /// <summary>
        /// A window narrower than the tooltip is still a window the tooltip has
        /// to fit in. Flipping cannot help here, so the clamp has to.
        /// </summary>
        [Test]
        public void TooltipWiderThanItsCanvasIsStillInsideIt()
        {
            var canvas = new Vector2(240f, 120f);
            var size = new Vector2(ItemTooltipView.PanelWidth, 168f);
            Vector2 corner = ItemTooltipView.ResolvePanelPosition(
                new Vector2(100f, -50f),
                size,
                canvas,
                ItemTooltipView.CursorOffset);
            Assert.That(corner.x, Is.EqualTo(-120f).Within(0.001f));
            Assert.That(corner.y, Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void HoverDelayIsShortEnoughToFeelImmediateAndLongEnoughToNotFlash()
        {
            Assert.That(
                ItemTooltipView.HoverDelaySeconds,
                Is.InRange(0.15f, 0.25f));
            Assert.That(ItemTooltipView.FadeSeconds, Is.InRange(0.06f, 0.14f));

            // The grace has to outlast one frame at any sane rate, or moving
            // between neighbouring cells waits out the delay again.
            Assert.That(
                ItemTooltipView.SwapGraceSeconds,
                Is.GreaterThan(1f / 30f));
        }

        private static LootDefinition MakeLoot(
            string id,
            string displayName,
            LootRarity rarity,
            LootCarryType carryType,
            bool raisesAlarm)
        {
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                id,
                displayName,
                rarity,
                carryType,
                raisesAlarm);
            return definition;
        }
    }
}
