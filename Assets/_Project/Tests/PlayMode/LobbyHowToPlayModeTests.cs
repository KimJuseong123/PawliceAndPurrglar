using NUnit.Framework;
using PawliceAndPurrglar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// That the buttons are actually connected to something.
    ///
    /// This has to run in Play Mode, and that is the entire point. The lobby is
    /// assembled by an editor script, and a listener an editor script adds with
    /// <c>onClick.AddListener</c> is non-persistent — it works in the editor and
    /// is gone from the saved scene, so the build gets a button that does
    /// nothing. Six lobby buttons shipped that way (`ISSUE-017`). The fix is to
    /// wire in <c>OnEnable</c>, and <c>OnEnable</c> does not run in Edit Mode
    /// without <c>[ExecuteAlways]</c> — so an Edit Mode test cannot tell a wired
    /// button from an inert one.
    ///
    /// The hierarchy is built here rather than loaded from the prefab because
    /// the Play Mode assembly cannot reach <c>AssetDatabase</c>. It is built out
    /// of the same name constants the builder uses, and
    /// <c>LobbyHowToOverlayTests</c> checks the prefab carries those names.
    /// </summary>
    public sealed class LobbyHowToPlayModeTests
    {
        private const int PageCount = 4;

        private GameObject root;
        private LobbyHowToLauncher launcher;
        private LobbyHowToOverlay overlay;
        private LobbyHowToPanel panel;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("LobbyCanvas", typeof(RectTransform));

            var overlayObject = new GameObject(
                LobbyHowToLauncher.OverlayName,
                typeof(RectTransform));
            overlayObject.transform.SetParent(root.transform, false);

            NewButton(LobbyHowToOverlay.ScrimName, overlayObject.transform);

            var panelObject = new GameObject(
                LobbyHowToOverlay.PanelNodeName,
                typeof(RectTransform));
            panelObject.transform.SetParent(overlayObject.transform, false);

            var pages = new GameObject(
                LobbyHowToPanel.PagesNodeName,
                typeof(RectTransform));
            pages.transform.SetParent(panelObject.transform, false);
            for (int page = 0; page < PageCount; page++)
            {
                var child = new GameObject(
                    $"Page {page + 1}",
                    typeof(RectTransform));
                child.transform.SetParent(pages.transform, false);
            }

            NewButton(LobbyHowToPanel.PreviousButtonName, panelObject.transform);
            NewButton(LobbyHowToPanel.NextButtonName, panelObject.transform);
            NewButton(LobbyHowToPanel.CloseButtonName, panelObject.transform);

            panel = panelObject.AddComponent<LobbyHowToPanel>();

            // Switched off before the overlay component goes on, so its
            // OnEnable has not run yet — the same state the saved prefab is in.
            overlayObject.SetActive(false);
            overlay = overlayObject.AddComponent<LobbyHowToOverlay>();

            Button open = NewButton("HowToButton", root.transform);
            launcher = open.gameObject.AddComponent<LobbyHowToLauncher>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button NewButton(string name, Transform parent)
        {
            var button = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            button.transform.SetParent(parent, false);
            return button.GetComponent<Button>();
        }

        private Button ButtonNamed(string name)
        {
            foreach (Button candidate in
                     root.GetComponentsInChildren<Button>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        [Test]
        public void TheModalStartsClosed()
        {
            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void PressingTheLobbyButtonOpensTheModal()
        {
            ButtonNamed("HowToButton").onClick.Invoke();

            Assert.That(
                overlay.IsOpen,
                Is.True,
                "The click reached nothing. That is what a listener added by an "
                + "editor script looks like in a build.");
        }

        [Test]
        public void PressingTheLobbyButtonAgainClosesTheModal()
        {
            Button open = ButtonNamed("HowToButton");
            open.onClick.Invoke();
            open.onClick.Invoke();

            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void TheXClosesTheModal()
        {
            launcher.Toggle();
            Assert.That(overlay.IsOpen, Is.True);

            ButtonNamed(LobbyHowToPanel.CloseButtonName).onClick.Invoke();

            Assert.That(
                overlay.IsOpen,
                Is.False,
                "A modal that opens and will not close is worse than one that "
                + "never opens.");
        }

        [Test]
        public void TheArrowsPageOnceTheModalIsOpen()
        {
            launcher.Toggle();

            ButtonNamed(LobbyHowToPanel.NextButtonName).onClick.Invoke();
            Assert.That(panel.PageIndex, Is.EqualTo(1));

            ButtonNamed(LobbyHowToPanel.NextButtonName).onClick.Invoke();
            Assert.That(panel.PageIndex, Is.EqualTo(2));

            ButtonNamed(LobbyHowToPanel.PreviousButtonName).onClick.Invoke();
            Assert.That(
                panel.PageIndex,
                Is.EqualTo(1),
                "The arrows are wired in the panel's own OnEnable, which only "
                + "runs once the overlay switches on.");
        }

        /// <summary>
        /// Somebody who closed on the last page and came back has a new
        /// question, and the last page is an answer to the old one.
        /// </summary>
        [Test]
        public void ReopeningStartsFromTheFirstPageAgain()
        {
            launcher.Toggle();
            panel.Show(PageCount - 1);
            launcher.Toggle();

            launcher.Toggle();

            Assert.That(overlay.IsOpen, Is.True);
            Assert.That(panel.PageIndex, Is.Zero);
        }
    }
}
