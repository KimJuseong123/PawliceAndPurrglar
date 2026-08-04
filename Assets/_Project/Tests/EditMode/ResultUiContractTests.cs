using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// Guards the properties the previous result screen lost silently.
    ///
    /// It was the authored mockup behind three transparent, captionless
    /// buttons, and its four labels were created at font size one, fully
    /// transparent and switched off. It built, it loaded, it logged nothing,
    /// and every match ended with the same painted numbers.
    /// </summary>
    public sealed class ResultUiContractTests
    {
        private const string PrefabPath =
            "Assets/_Project/UI/Prefabs/ResultCanvas.prefab";

        private const string FontPath =
            "Assets/Resources/PawsAndLootDefaultFont.asset";

        private const string ResultScenePath =
            "Assets/_Project/Scenes/Result.unity";

        private static readonly string[] ActionButtons =
        {
            "Retry Button",
            "Lobby Button",
            "Quit Button"
        };

        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(
                _prefab,
                Is.Not.Null,
                $"The result prefab is missing: {PrefabPath}. Run "
                + "'Paws & Loot/UI/Rebuild Result (Art, Prefab, Scene)'.");
        }

        [Test]
        public void ResultScalesFromTheReferenceResolution()
        {
            var scaler = _prefab.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(
                scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(
                scaler.referenceResolution,
                Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void PresenterHoldsEveryReference()
        {
            var presenter = _prefab.GetComponent<ResultScreenPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.DoesNotThrow(presenter.ValidateOrThrow);

            foreach (FieldInfo field in presenter.GetType()
                         .GetFields(
                             BindingFlags.Instance | BindingFlags.NonPublic)
                         .Where(f =>
                             f.GetCustomAttribute<SerializeField>() != null))
            {
                Assert.That(
                    field.GetValue(presenter) as Object,
                    Is.Not.Null,
                    $"ResultScreenPresenter.{field.Name} is unassigned, so "
                    + "that part of the report would silently stay blank.");
            }
        }

        /// <summary>
        /// Both destinations and the quit action, which is what the scene
        /// contract for the result screen requires.
        /// </summary>
        [Test]
        public void EveryWayOutOfTheResultScreenExists()
        {
            SceneNavigationButton[] navigation =
                _prefab.GetComponentsInChildren<SceneNavigationButton>(true);
            Assert.That(
                navigation.Select(button => button.TargetScene),
                Is.EquivalentTo(new[]
                {
                    GameSceneId.Game,
                    GameSceneId.Bootstrap
                }));
            Assert.That(
                _prefab.GetComponentsInChildren<ApplicationQuitButton>(true),
                Has.Length.EqualTo(1));

            foreach (string name in ActionButtons)
            {
                Assert.That(
                    Find(name).GetComponent<Button>(),
                    Is.Not.Null,
                    $"'{name}' is not a button.");
            }
        }

        /// <summary>
        /// Every button has a caption. The screen this replaces had three with
        /// none, because the words were painted into the picture behind them.
        /// </summary>
        [Test]
        public void EveryButtonIsCaptionedAndBigEnough()
        {
            foreach (string name in ActionButtons)
            {
                Transform label = Find(name).transform.Find("Label");
                Assert.That(label, Is.Not.Null, $"'{name}' has no caption.");

                var text = label.GetComponent<TMP_Text>();
                Assert.That(
                    text.text,
                    Is.Not.Empty,
                    $"'{name}' has an empty caption.");
                Assert.That(
                    text.fontSize,
                    Is.GreaterThanOrEqualTo(38f),
                    $"'{name}' caption is below the readable floor.");

                var element = Find(name).GetComponent<LayoutElement>();
                Assert.That(element, Is.Not.Null);
                Assert.That(
                    element.preferredHeight,
                    Is.GreaterThanOrEqualTo(92f),
                    $"'{name}' is shorter than the floor for a main button.");
            }
        }

        /// <summary>
        /// No button is invisible. A fully transparent plate is exactly what
        /// made the old screen's controls impossible to find.
        /// </summary>
        [Test]
        public void NoButtonIsTransparent()
        {
            foreach (Button button in
                     _prefab.GetComponentsInChildren<Button>(true))
            {
                var fill = button.targetGraphic as Image;
                Assert.That(
                    fill,
                    Is.Not.Null,
                    $"'{button.name}' has no target graphic.");
                Assert.That(
                    fill.color.a,
                    Is.GreaterThan(0.9f),
                    $"'{button.name}' is drawn transparent.");
                Assert.That(
                    fill.sprite,
                    Is.Not.Null,
                    $"'{button.name}' has no plate sprite.");
            }
        }

        [Test]
        public void EveryCaptionUsesTheProjectFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Assert.That(font, Is.Not.Null, FontPath);

            TMP_Text[] labels =
                _prefab.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(labels.Length, Is.GreaterThan(8));
            foreach (TMP_Text label in labels)
            {
                Assert.That(
                    label.font,
                    Is.EqualTo(font),
                    $"'{Path(label.transform)}' does not use the project font.");
            }
        }

        [Test]
        public void NoLegacyTextSurvives()
        {
            Assert.That(
                _prefab.GetComponentsInChildren<Text>(true),
                Is.Empty,
                "Legacy UI.Text cannot render the project font asset.");
        }

        /// <summary>
        /// No label may be a hidden placeholder. The old screen's four result
        /// labels were size one, transparent and switched off — present to any
        /// check that only looked for them, invisible to a player.
        /// </summary>
        [Test]
        public void NoLabelIsHiddenOrUnreadable()
        {
            foreach (TMP_Text label in
                     _prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(
                    label.gameObject.activeSelf,
                    Is.True,
                    $"'{Path(label.transform)}' is switched off.");
                Assert.That(
                    label.fontSize,
                    Is.GreaterThanOrEqualTo(20f),
                    $"'{Path(label.transform)}' is too small to read.");
                Assert.That(
                    label.color.a,
                    Is.GreaterThan(0.5f),
                    $"'{Path(label.transform)}' is transparent.");
            }
        }

        [Test]
        public void NoLayoutGroupForcesItsChildrenToExpand()
        {
            HorizontalOrVerticalLayoutGroup[] groups =
                _prefab.GetComponentsInChildren<
                    HorizontalOrVerticalLayoutGroup>(true);
            Assert.That(groups.Length, Is.GreaterThan(0));
            foreach (HorizontalOrVerticalLayoutGroup group in groups)
            {
                Assert.That(group.childForceExpandWidth, Is.False);
                Assert.That(group.childForceExpandHeight, Is.False);
            }
        }

        [Test]
        public void EveryImageHasItsSprite()
        {
            foreach (Image image in _prefab.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "BackgroundFill")
                {
                    continue;
                }

                if (image.name == "TitleArt")
                {
                    // The one image that is meant to be empty in the prefab. It
                    // holds 승리 or 패배, chosen from whether the local player won,
                    // and a prefab that baked either would be claiming a result
                    // before a match had been played. What matters instead is that
                    // it is switched off while it is empty — an Image with no
                    // sprite draws a white quad, which is what it did.
                    Assert.That(
                        image.enabled,
                        Is.False,
                        "The title has no sprite and is still enabled, so it draws "
                        + "a white rectangle where the verdict belongs.");
                    continue;
                }

                Assert.That(
                    image.sprite,
                    Is.Not.Null,
                    $"'{Path(image.transform)}' has no sprite.");
            }
        }

        [Test]
        public void SlicedPlatesCarryANineSliceBorder()
        {
            Image[] sliced = _prefab.GetComponentsInChildren<Image>(true)
                .Where(image => image.type == Image.Type.Sliced)
                .ToArray();
            Assert.That(sliced.Length, Is.GreaterThan(0));
            foreach (Image image in sliced)
            {
                Assert.That(
                    image.sprite.border,
                    Is.Not.EqualTo(Vector4.zero),
                    $"'{Path(image.transform)}' is sliced but its sprite has "
                    + "no border, so its corners stretch with the plate.");
            }
        }

        [Test]
        public void ArtworkKeepsItsAspect()
        {
            foreach (string name in
                     new[] { "GameOverHeader", "TitleArt", "VersusArt" })
            {
                Assert.That(
                    Find(name).GetComponent<Image>().preserveAspect,
                    Is.True,
                    $"'{name}' may be stretched out of proportion.");
            }
        }

        [Test]
        public void ResultSceneUsesThePrefab()
        {
            Assert.That(File.Exists(ResultScenePath), Is.True, ResultScenePath);
            Scene scene = EditorSceneManager.OpenScene(
                ResultScenePath,
                OpenSceneMode.Additive);
            try
            {
                GameObject root = scene.GetRootGameObjects()
                    .FirstOrDefault(item => item.name == "ResultCanvas");
                Assert.That(
                    root,
                    Is.Not.Null,
                    "The result screen is not in Result.unity.");
                Assert.That(
                    PrefabUtility.GetCorrespondingObjectFromSource(root),
                    Is.EqualTo(_prefab),
                    "The result screen in the scene is not an instance of the "
                    + "prefab.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private GameObject Find(string name)
        {
            foreach (Transform transform in
                     _prefab.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform.gameObject;
                }
            }

            Assert.Fail($"The result prefab has no '{name}'.");
            return null;
        }

        private static string Path(Transform transform)
        {
            var parts = new List<string>();
            for (Transform step = transform; step != null; step = step.parent)
            {
                parts.Add(step.name);
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
