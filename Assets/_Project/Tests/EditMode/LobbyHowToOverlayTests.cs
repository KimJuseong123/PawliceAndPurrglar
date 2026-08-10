using System;
using System.IO;
using NUnit.Framework;
using PawliceAndPurrglar.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// What the builder has to have produced for the how-to modal.
    ///
    /// The paging rules are <c>LobbyHowToPanelTests</c>' job and the runtime
    /// wiring is the Play Mode test's. This reads the saved prefab, because the
    /// three things that can go wrong here all go wrong silently:
    ///
    /// - the overlay shipping switched **on**, which is the thing that was
    ///   asked to change and which every other check would still pass
    /// - the close region drifting off the X painted into the pages, which
    ///   leaves a modal that opens, pages, and cannot be closed
    /// - the button losing its caption, which is how this lobby's buttons were
    ///   invisible the first time round (`ISSUE-046`)
    /// </summary>
    public sealed class LobbyHowToOverlayTests
    {
        private const string PrefabPath =
            "Assets/_Project/UI/Prefabs/LobbyCanvas.prefab";

        private const string HotspotPath =
            "Assets/_Project/UI/Lobby/HowTo/howto_hotspots.json";

        private const string PageFolder = "Assets/_Project/UI/Lobby/HowTo";

        [Serializable]
        private struct Hotspots
        {
            public int canvasWidth;
            public int canvasHeight;
            public float closeCenterX;
            public float closeCenterY;
            public float closeWidth;
            public float closeHeight;
        }

        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(
                prefab,
                Is.Not.Null,
                $"The lobby prefab is missing: {PrefabPath}. Run "
                + "'PawliceAndPurrglar/UI/Rebuild Lobby (Art, Prefab, Scene)'.");
        }

        private Transform Find(string name)
        {
            foreach (Transform candidate in
                     prefab.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        private Transform Require(string name)
        {
            Transform found = Find(name);
            Assert.That(
                found,
                Is.Not.Null,
                $"The lobby prefab has no '{name}'. The builder and the runtime "
                + "components agree on names and nothing else, so a rename here "
                + "is a silent disconnection.");
            return found;
        }

        [Test]
        public void TheLobbyOpensWithTheHowToModalClosed()
        {
            Transform overlay = Require(LobbyHowToLauncher.OverlayName);

            Assert.That(
                overlay.gameObject.activeSelf,
                Is.False,
                "The how-to modal must not be on screen until it is asked for. "
                + "It used to be part of the lobby itself, which is exactly what "
                + "was asked to change.");
            Assert.That(
                overlay.GetComponent<LobbyHowToOverlay>(),
                Is.Not.Null);
        }

        [Test]
        public void TheOpenButtonSaysWhatItOpens()
        {
            Transform button = Require("HowToButton");

            Assert.That(
                button.GetComponent<Button>(),
                Is.Not.Null,
                "Nothing else on the screen opens the modal, so this being a "
                + "button is the whole feature.");
            Assert.That(
                button.GetComponent<LobbyHowToLauncher>(),
                Is.Not.Null,
                "Without the launcher the button is pressable and inert.");

            var caption = button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(caption, Is.Not.Null);
            Assert.That(
                caption.text,
                Is.EqualTo("게임 방법"),
                "Captioned, not an 'i'. The reader who needs this button most "
                + "is the one least likely to recognise an icon for it.");
        }

        [Test]
        public void TheScrimCoversTheLobbyAndSwallowsClicks()
        {
            var scrim = Require(LobbyHowToOverlay.ScrimName)
                .GetComponent<Image>();

            Assert.That(scrim, Is.Not.Null);
            Assert.That(
                scrim.raycastTarget,
                Is.True,
                "A click that misses the panel by a few pixels would otherwise "
                + "land on the lobby control underneath, and those host rooms "
                + "and start matches.");
            Assert.That(
                scrim.color.a,
                Is.GreaterThan(0.4f),
                "The pages were authored as screenshots over a dimmed lobby; a "
                + "faint scrim leaves them sitting on the wrong colour.");

            var rect = (RectTransform)scrim.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void EveryImportedPageIsOnThePanel()
        {
            int imported = 0;
            while (AssetDatabase.LoadAssetAtPath<Sprite>(
                       $"{PageFolder}/howto_page{imported + 1}.png") != null)
            {
                imported++;
            }

            Assert.That(
                imported,
                Is.GreaterThan(0),
                "No how-to pages are imported at all.");

            Transform pages = Require(LobbyHowToPanel.PagesNodeName);
            Assert.That(
                pages.childCount,
                Is.EqualTo(imported),
                "A page that exists as an asset but not on the panel is a page "
                + "nobody can reach, and the counter drawn into the art would "
                + "promise it anyway.");
        }

        /// <summary>
        /// The close region has to be over the X that is painted into every
        /// page, and this is the only check that can say so — the button is
        /// deliberately invisible, so a screenshot shows nothing either way.
        ///
        /// It compares against the same measurements the builder read, which
        /// makes it a check that the builder applied them, not that they are
        /// right. That they are right is the normalizer's job: it finds the X on
        /// each page and refuses to write the file when the pages disagree.
        /// </summary>
        [Test]
        public void TheCloseButtonSitsOnTheXDrawnIntoThePages()
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(HotspotPath);
            Assert.That(
                file,
                Is.Not.Null,
                $"'{HotspotPath}' is missing. Run "
                + "'python Tools/normalize_howto_pages.py' then "
                + "'PawliceAndPurrglar/UI/Import Lobby How-To Art'.");

            Hotspots spot = JsonUtility.FromJson<Hotspots>(file.text);
            var panel = (RectTransform)Require(
                LobbyHowToOverlay.PanelNodeName);
            var close = (RectTransform)Require(
                LobbyHowToPanel.CloseButtonName);

            Assert.That(
                close.GetComponent<Button>(),
                Is.Not.Null,
                "Without a Button the X is a picture of a close button.");
            Assert.That(
                close.GetComponentInChildren<Image>(true).raycastTarget,
                Is.True,
                "A transparent Image still takes clicks — but only if it is "
                + "asked to.");

            Assert.That(
                panel.rect.size,
                Is.EqualTo(new Vector2(spot.canvasWidth, spot.canvasHeight)),
                "The offsets below are fractions of the page canvas, so the "
                + "panel has to be that canvas. This is the single assumption "
                + "that keeps an invisible control over its artwork at every "
                + "resolution instead of beside it (`ISSUE-046`).");

            Vector2 centre = close.anchoredPosition;
            Assert.That(
                centre.x / panel.rect.width,
                Is.EqualTo(spot.closeCenterX).Within(0.002f));
            Assert.That(
                -centre.y / panel.rect.height,
                Is.EqualTo(spot.closeCenterY).Within(0.002f));

            // Bigger than the drawn plate, never smaller: a hit region inside
            // the picture of a button is a button with a dead border.
            Assert.That(
                close.rect.width,
                Is.GreaterThanOrEqualTo(spot.closeWidth * panel.rect.width));
            Assert.That(
                close.rect.height,
                Is.GreaterThanOrEqualTo(spot.closeHeight * panel.rect.height));

            Assert.That(
                spot.closeCenterX,
                Is.GreaterThan(0.5f),
                "The X is drawn in the top-right corner of every page.");
            Assert.That(spot.closeCenterY, Is.LessThan(0.5f));
        }

        /// <summary>
        /// The normalizer's output and the imported sprites have to be the same
        /// set. A stale hotspot file measured on a previous page set would place
        /// the close region somewhere plausible and wrong.
        /// </summary>
        [Test]
        public void TheMeasurementsMatchTheImportedPages()
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(HotspotPath);
            Assert.That(file, Is.Not.Null);
            Hotspots spot = JsonUtility.FromJson<Hotspots>(file.text);

            foreach (string path in Directory.GetFiles(
                         PageFolder,
                         "howto_page*.png"))
            {
                var page = AssetDatabase.LoadAssetAtPath<Sprite>(
                    path.Replace('\\', '/'));
                Assert.That(page, Is.Not.Null);
                Assert.That(
                    new Vector2(page.rect.width, page.rect.height),
                    Is.EqualTo(
                        new Vector2(spot.canvasWidth, spot.canvasHeight)),
                    $"'{Path.GetFileName(path)}' is not on the canvas the close "
                    + "button was measured on. Pages of different sizes also "
                    + "make the panel change size as it is paged through, "
                    + "because Preserve Aspect shrinks to the narrower axis.");
            }
        }
    }
}
