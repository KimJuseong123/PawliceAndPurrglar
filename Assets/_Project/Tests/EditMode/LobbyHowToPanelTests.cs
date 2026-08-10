using NUnit.Framework;
using PawliceAndPurrglar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// The paging rules, which are the whole of what was asked for: the left
    /// arrow is not there on the first page and the right arrow is not there on
    /// the last.
    ///
    /// Worth a test rather than an eye: the panel is assembled by an editor
    /// script and the arrows are wired at runtime, so the layout capture renders
    /// it with every page active and both arrows showing. Looking at a
    /// screenshot cannot tell you whether paging works — only that the pieces
    /// exist, which is the same trap that shipped a result screen whose labels
    /// were all present and drew nothing.
    /// </summary>
    public sealed class LobbyHowToPanelTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The same object names the builder uses. If the builder renames one,
        /// the panel silently stops resolving it — so the names are asserted
        /// through the constants rather than typed twice.
        /// </summary>
        private LobbyHowToPanel Build(int pageCount)
        {
            root = new GameObject("HowToPanel", typeof(RectTransform));

            var pages = new GameObject(
                LobbyHowToPanel.PagesNodeName,
                typeof(RectTransform));
            pages.transform.SetParent(root.transform, false);
            for (int page = 0; page < pageCount; page++)
            {
                var child = new GameObject(
                    $"Page {page + 1}",
                    typeof(RectTransform));
                child.transform.SetParent(pages.transform, false);
            }

            foreach (string name in new[]
            {
                LobbyHowToPanel.PreviousButtonName,
                LobbyHowToPanel.NextButtonName
            })
            {
                var button = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                button.transform.SetParent(root.transform, false);
            }

            var label = new GameObject(
                LobbyHowToPanel.PageLabelName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);

            var dots = new GameObject(
                LobbyHowToPanel.DotsNodeName,
                typeof(RectTransform));
            dots.transform.SetParent(root.transform, false);
            for (int page = 0; page < pageCount; page++)
            {
                var dot = new GameObject(
                    $"Dot {page + 1}",
                    typeof(RectTransform),
                    typeof(Image));
                dot.transform.SetParent(dots.transform, false);
            }

            // Added last so it resolves a finished hierarchy, then rebound
            // explicitly: `OnEnable` does not fire in the editor without
            // `[ExecuteAlways]`, so a test that relied on it would assert
            // against a panel that had never initialised.
            var panel = root.AddComponent<LobbyHowToPanel>();
            panel.Rebind();
            return panel;
        }

        private bool ArrowShown(string name) =>
            root.transform.Find(name).gameObject.activeSelf;

        [Test]
        public void TheFirstPageHasNoLeftArrow()
        {
            LobbyHowToPanel panel = Build(3);

            Assert.That(panel.PageIndex, Is.Zero);
            Assert.That(
                ArrowShown(LobbyHowToPanel.PreviousButtonName),
                Is.False,
                "There is nothing before the first page, so the left arrow must "
                + "not be on screen.");
            Assert.That(
                ArrowShown(LobbyHowToPanel.NextButtonName),
                Is.True);
        }

        [Test]
        public void TheLastPageHasNoRightArrow()
        {
            LobbyHowToPanel panel = Build(3);

            panel.ShowNext();
            panel.ShowNext();

            Assert.That(panel.PageIndex, Is.EqualTo(2));
            Assert.That(
                ArrowShown(LobbyHowToPanel.NextButtonName),
                Is.False,
                "There is nothing after the last page, so the right arrow must "
                + "not be on screen.");
            Assert.That(
                ArrowShown(LobbyHowToPanel.PreviousButtonName),
                Is.True);
        }

        [Test]
        public void OnlyTheCurrentPageIsOnScreen()
        {
            LobbyHowToPanel panel = Build(3);
            Transform pages = root.transform.Find(LobbyHowToPanel.PagesNodeName);

            for (int expected = 0; expected < 3; expected++)
            {
                panel.Show(expected);
                for (int page = 0; page < 3; page++)
                {
                    Assert.That(
                        pages.GetChild(page).gameObject.activeSelf,
                        Is.EqualTo(page == expected),
                        $"On page {expected + 1}, page {page + 1} should be "
                        + (page == expected ? "shown" : "hidden")
                        + ". Every page drawn at once reads as the last one, "
                        + "which is exactly how the editor capture renders it.");
                }
            }
        }

        [Test]
        public void PagingStopsAtTheEndsInsteadOfWrapping()
        {
            LobbyHowToPanel panel = Build(3);

            panel.ShowPrevious();
            Assert.That(
                panel.PageIndex,
                Is.Zero,
                "Wrapping would contradict the missing arrow.");

            panel.Show(99);
            Assert.That(panel.PageIndex, Is.EqualTo(2));
        }

        [Test]
        public void TheCounterCountsTheRealPages()
        {
            LobbyHowToPanel panel = Build(3);
            var label = root.transform
                .Find(LobbyHowToPanel.PageLabelName)
                .GetComponent<TMP_Text>();

            Assert.That(label.text, Is.EqualTo("1 / 3"));
            panel.ShowNext();
            Assert.That(label.text, Is.EqualTo("2 / 3"));
        }

        /// <summary>
        /// A single page is the shape a fourth page being added and removed
        /// leaves behind, and both arrows would be wrong.
        /// </summary>
        [Test]
        public void ASinglePageHasNoArrowsAtAll()
        {
            Build(1);

            Assert.That(
                ArrowShown(LobbyHowToPanel.PreviousButtonName),
                Is.False);
            Assert.That(
                ArrowShown(LobbyHowToPanel.NextButtonName),
                Is.False);
        }
    }
}
