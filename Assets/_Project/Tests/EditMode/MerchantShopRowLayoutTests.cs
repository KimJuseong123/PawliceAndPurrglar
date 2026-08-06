using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// The officer's shop has to show what a thing costs.
    ///
    /// It did not. The raccoon listed three goods, each with a coin and a BUY
    /// button and no number between them, and the shop read as a shelf of free
    /// samples. Nothing said so: the figure was written every frame, the label
    /// existed, and every test that asked "is there a price label" passed. It was
    /// laid out at a hand-tuned offset from the right edge that the 86px BUY
    /// button reached across, so it was drawn underneath the button.
    ///
    /// Checked as geometry rather than as existence, because existence is exactly
    /// what was true the whole time. Same family as <c>ISSUE-050</c>, where four
    /// result labels existed at <c>fontSize 1</c> and full transparency.
    /// </summary>
    public sealed class MerchantShopRowLayoutTests
    {
        private GameObject _canvas;
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Shop Canvas", typeof(Canvas));
            _player = new GameObject("Officer");
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate, because Destroy does not take effect until the end
            // of a frame and an edit-mode test has no frames — the window would
            // survive into the next test and its rows would be found twice.
            if (_canvas != null)
            {
                Object.DestroyImmediate(_canvas);
            }

            if (_player != null)
            {
                Object.DestroyImmediate(_player);
            }
        }

        [Test]
        public void EveryShopRowShowsItsPriceClearOfTheBuyButton()
        {
            var carrier = _player.AddComponent<ToolCarrier>();
            var presenter = _canvas.AddComponent<MerchantTradePresenter>();

            // Opening runs a Refresh of its own, so the figures are written
            // without needing a frame to tick.
            presenter.OpenShop(carrier, null);

            Assert.That(presenter.IsOpen, Is.True, "The stall did not open.");

            List<Transform> rows = _canvas
                .GetComponentsInChildren<Transform>(true)
                .Where(child => child.name.StartsWith("Sell Row")
                    && child.gameObject.activeInHierarchy)
                .ToList();

            Assert.That(
                rows,
                Is.Not.Empty,
                "The stall opened with no goods on the shelf at all.");

            foreach (Transform row in rows)
            {
                TMP_Text price = FindText(row, "Price");
                Assert.That(price, Is.Not.Null, $"'{row.name}' has no price label.");

                Assert.That(
                    price.text,
                    Is.Not.Empty,
                    $"'{row.name}' shows a coin and no figure.");
                Assert.That(
                    int.TryParse(price.text.Replace(",", string.Empty), out int gold)
                        && gold > 0,
                    Is.True,
                    $"'{row.name}' prices its goods at '{price.text}'.");

                // TMP draws nothing at all when the rect is shorter than one line
                // (ISSUE-047), so a label that fits by a pixel today is a label
                // that vanishes on the next font change.
                Assert.That(
                    price.rectTransform.rect.height,
                    Is.GreaterThanOrEqualTo(price.fontSize * 1.45f),
                    $"'{row.name}' gives a {price.fontSize}pt price "
                    + $"{price.rectTransform.rect.height}px of height.");

                Rect priceRect = WorldRect(price.rectTransform);

                var action = row.GetComponentsInChildren<Button>(true)
                    .Select(button => button.GetComponent<RectTransform>())
                    .FirstOrDefault();
                Assert.That(action, Is.Not.Null, $"'{row.name}' has no button.");
                Assert.That(
                    priceRect.Overlaps(WorldRect(action)),
                    Is.False,
                    $"'{row.name}' draws its price underneath the "
                    + $"{action.name} button, where nobody can read it.");

                // The coin reads as part of the figure, so it goes immediately to
                // its left rather than anywhere else on the row.
                Image coin = row.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(image => image.name == "Coin");
                Assert.That(coin, Is.Not.Null, $"'{row.name}' has no coin.");
                Rect coinRect = WorldRect(coin.rectTransform);
                Assert.That(
                    coinRect.xMax,
                    Is.LessThanOrEqualTo(priceRect.xMin),
                    $"'{row.name}' puts its coin at {coinRect.xMax:0} and its "
                    + $"price at {priceRect.xMin:0}. The coin belongs on the "
                    + "left of the figure.");
            }
        }

        private static TMP_Text FindText(Transform row, string name)
        {
            return row.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == name);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                corners[0].x,
                corners[0].y,
                corners[2].x,
                corners[2].y);
        }
    }
}
