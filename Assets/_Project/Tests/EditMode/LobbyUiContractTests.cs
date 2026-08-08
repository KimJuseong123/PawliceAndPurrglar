using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
    /// Guards the properties the previous lobby lost silently.
    ///
    /// That lobby was a mockup image with fully transparent, captionless
    /// buttons pinned over it at fixed offsets. It built, it loaded, it logged
    /// nothing, and the buttons simply were not where they looked. Nothing here
    /// asks a component for its own opinion of itself; every check reads the
    /// authored values a player would end up seeing.
    /// </summary>
    public sealed class LobbyUiContractTests
    {
        private const string PrefabPath =
            "Assets/_Project/UI/Prefabs/LobbyCanvas.prefab";

        private const string FontPath =
            "Assets/Resources/PawsAndLootDefaultFont.asset";

        private const string BootstrapPath =
            "Assets/_Project/Scenes/Bootstrap.unity";

        /// <summary>
        /// Names <c>NetworkLobbyProbe</c> looks up to drive the lobby from the
        /// command line. Renaming one of these leaves the probe passing every
        /// scenario it can still reach and silently skipping the rest.
        /// </summary>
        private static readonly string[] ProbeDrivenControls =
        {
            "Host Button",
            "Join Button",
            "Join Address",
            "Port",
            "Room Slot 0",
            "Room Slot 1",
            "Room Slot 2",
            "Room Slot 3"
        };

        private static readonly string[] ActionButtons =
        {
            "Swap Role Button",
            "Start Match Button",
            "Mic Test Button"
        };

        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(
                _prefab,
                Is.Not.Null,
                $"The lobby prefab is missing: {PrefabPath}. Run "
                + "'PawliceAndPurrglar/UI/Rebuild Lobby (Art, Prefab, Scene)'.");
        }

        [Test]
        public void LobbyScalesFromTheReferenceResolution()
        {
            var scaler = _prefab.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(
                scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(
                scaler.referenceResolution,
                Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(
                scaler.screenMatchMode,
                Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));
        }

        /// <summary>
        /// Every serialised view reference is present. A null one costs nothing
        /// at build time and shows up as a control that does nothing.
        /// </summary>
        [Test]
        public void PresenterHoldsEveryViewReference()
        {
            var presenter = _prefab.GetComponent<NetworkLobbyPresenter>();
            Assert.That(presenter, Is.Not.Null);

            // Supplied by the scene, not the prefab, because they live outside
            // the asset.
            var sceneSupplied = new HashSet<string>
            {
                "session", "roomDirectory", "fillEndpointFields"
            };

            foreach (FieldInfo field in SerializedFields(presenter, sceneSupplied))
            {
                object value = field.GetValue(presenter);
                if (value is System.Array array)
                {
                    Assert.That(
                        array.Length,
                        Is.GreaterThan(0),
                        $"NetworkLobbyPresenter.{field.Name} is empty.");
                    for (int index = 0; index < array.Length; index++)
                    {
                        Assert.That(
                            array.GetValue(index) as Object,
                            Is.Not.Null,
                            $"NetworkLobbyPresenter.{field.Name}[{index}] "
                            + "is unassigned.");
                    }

                    continue;
                }

                Assert.That(
                    value as Object,
                    Is.Not.Null,
                    $"NetworkLobbyPresenter.{field.Name} is unassigned.");
            }
        }

        [Test]
        public void MicrophoneCheckHoldsEveryViewReference()
        {
            var diagnostics =
                _prefab.GetComponent<LobbyMicrophoneDiagnostics>();
            Assert.That(diagnostics, Is.Not.Null);

            foreach (FieldInfo field in SerializedFields(
                         diagnostics,
                         new HashSet<string> { "idleButtonCaption" }))
            {
                Assert.That(
                    field.GetValue(diagnostics) as Object,
                    Is.Not.Null,
                    $"LobbyMicrophoneDiagnostics.{field.Name} is unassigned.");
            }
        }

        [Test]
        public void ControlsKeepTheNamesTheProbeDrivesThemBy()
        {
            foreach (string name in ProbeDrivenControls)
            {
                Assert.That(
                    Find(name),
                    Is.Not.Null,
                    $"NetworkLobbyProbe looks up '{name}' by name and would "
                    + "silently skip it.");
            }
        }

        /// <summary>
        /// Every caption uses the project font. A stray default font renders
        /// Korean as boxes, which is invisible to any test that only checks
        /// that a label exists.
        /// </summary>
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
        public void NoLegacyTextOrInputFieldSurvives()
        {
            Assert.That(
                _prefab.GetComponentsInChildren<Text>(true),
                Is.Empty,
                "Legacy UI.Text cannot render the project font asset.");
            Assert.That(
                _prefab.GetComponentsInChildren<InputField>(true),
                Is.Empty,
                "Legacy UI.InputField cannot render the project font asset.");
        }

        [Test]
        public void ControlsMeetTheMinimumSizes()
        {
            foreach (string name in ActionButtons)
            {
                Assert.That(
                    PreferredHeight(name),
                    Is.GreaterThanOrEqualTo(92f),
                    $"'{name}' is shorter than the floor for a main button.");
                Assert.That(
                    CaptionSize(name),
                    Is.GreaterThanOrEqualTo(38f),
                    $"'{name}' caption is below the readable floor.");
            }

            Assert.That(PreferredHeight("Host Button"), Is.GreaterThanOrEqualTo(58f));
            Assert.That(PreferredHeight("Join Button"), Is.GreaterThanOrEqualTo(58f));
            Assert.That(
                PreferredHeight("AddressPanel"),
                Is.GreaterThanOrEqualTo(125f));

            foreach (string field in new[] { "Join Address", "Port" })
            {
                Assert.That(
                    Rect(field).sizeDelta.y,
                    Is.GreaterThanOrEqualTo(64f),
                    $"'{field}' is shorter than the floor for an input field.");
                Assert.That(
                    Find(field).GetComponent<TMP_InputField>().textComponent
                        .fontSize,
                    Is.GreaterThanOrEqualTo(28f),
                    $"'{field}' text is below the readable floor.");
            }

            Assert.That(
                Rect("TitleLogo").sizeDelta.x,
                Is.GreaterThanOrEqualTo(420f));

            foreach (string status in
                     new[] { "ConnectionStatusText", "MicrophoneStatusText" })
            {
                Assert.That(
                    Find(status).GetComponent<TMP_Text>().fontSize,
                    Is.GreaterThanOrEqualTo(26f),
                    $"'{status}' is below the readable floor.");
            }
        }

        /// <summary>
        /// Force-expand is what turns a button into a flat bar and squeezes a
        /// field until its caption overlaps its own text.
        /// </summary>
        [Test]
        public void NoLayoutGroupForcesItsChildrenToExpand()
        {
            HorizontalOrVerticalLayoutGroup[] groups =
                _prefab.GetComponentsInChildren<
                    HorizontalOrVerticalLayoutGroup>(true);
            Assert.That(groups.Length, Is.GreaterThan(0));
            foreach (HorizontalOrVerticalLayoutGroup group in groups)
            {
                Assert.That(
                    group.childForceExpandWidth,
                    Is.False,
                    $"'{Path(group.transform)}' force-expands width.");
                Assert.That(
                    group.childForceExpandHeight,
                    Is.False,
                    $"'{Path(group.transform)}' force-expands height.");
            }
        }

        /// <summary>
        /// A missing sprite draws as a white box, which reads as a layout
        /// problem rather than a broken reference.
        /// </summary>
        [Test]
        public void EveryImageHasItsSprite()
        {
            foreach (Image image in _prefab.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "BackgroundFill")
                {
                    // One flat colour across the canvas, by design.
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
            var sliced = _prefab.GetComponentsInChildren<Image>(true)
                .Where(image => image.type == Image.Type.Sliced)
                .ToArray();
            Assert.That(sliced.Length, Is.GreaterThan(0));
            foreach (Image image in sliced)
            {
                Assert.That(
                    image.sprite.border,
                    Is.Not.EqualTo(Vector4.zero),
                    $"'{Path(image.transform)}' is sliced but its sprite has "
                    + "no border, so its corners stretch with the button.");
            }
        }

        [Test]
        public void CharactersAndLogoKeepTheirAspect()
        {
            foreach (string name in new[]
                     {
                         "TitleLogo",
                         "PoliceTeamArt",
                         "ThiefTeamArt"
                     })
            {
                Assert.That(
                    Find(name).GetComponent<Image>().preserveAspect,
                    Is.True,
                    $"'{name}' may be stretched out of proportion.");
            }
        }

        /// <summary>
        /// The two teams keep their sides. Swapping them on role change is
        /// exactly what the layout is not allowed to do.
        /// </summary>
        /// <summary>
        /// Four distinct pair sprites, all present.
        ///
        /// A missing one would leave that pose showing whatever it replaced, and
        /// two that are the same object make the pose change invisible — neither
        /// of which any test about references being non-null would notice.
        /// </summary>
        [Test]
        public void EachTeamHasBothOfItsPoses()
        {
            var view = _prefab.GetComponentInChildren<LobbyCharacterView>(true);
            Assert.That(view, Is.Not.Null);

            var sprites = new List<Sprite>();
            foreach (FieldInfo field in view.GetType()
                         .GetFields(
                             BindingFlags.Instance | BindingFlags.NonPublic)
                         .Where(f => f.FieldType == typeof(Sprite)))
            {
                var sprite = field.GetValue(view) as Sprite;
                Assert.That(
                    sprite,
                    Is.Not.Null,
                    $"LobbyCharacterView.{field.Name} is unassigned.");
                sprites.Add(sprite);
            }

            Assert.That(sprites, Has.Count.EqualTo(4));
            Assert.That(
                sprites.Distinct().Count(),
                Is.EqualTo(4),
                "Two poses share a sprite, so changing role would show no "
                + "difference.");
        }

        /// <summary>
        /// A team's artwork must not reach into the middle, where the room list
        /// floats. Preserve Aspect centres art inside its rect, so a rect wider
        /// than the widest pose pushes that pose inwards.
        /// </summary>
        [Test]
        public void TeamArtIsNoWiderThanItsWidestPose()
        {
            var view = _prefab.GetComponentInChildren<LobbyCharacterView>(true);
            Assert.That(view, Is.Not.Null);

            foreach (string name in new[] { "PoliceTeamArt", "ThiefTeamArt" })
            {
                RectTransform rect = Rect(name);
                var image = rect.GetComponent<Image>();
                float rectAspect = rect.sizeDelta.x / rect.sizeDelta.y;
                float spriteAspect =
                    image.sprite.rect.width / image.sprite.rect.height;
                Assert.That(
                    rectAspect,
                    Is.GreaterThanOrEqualTo(spriteAspect - 0.001f),
                    $"'{name}' is narrower than its own sprite, so width binds "
                    + "and the pair shrinks below the shared height.");
                Assert.That(
                    rect.anchorMin.x,
                    Is.EqualTo(rect.anchorMax.x),
                    $"'{name}' must be pinned to one edge, not stretched.");
            }
        }

        /// <summary>
        /// The background is one flat opaque colour across the whole canvas, and
        /// the text on it is dark enough to read.
        ///
        /// Both halves matter together. A painted plate was tried here and taken
        /// back out (`ISSUE-053`); going back to flat means the text has to go
        /// back to dark in the same change, and a partly transparent fill would
        /// let whatever is behind the canvas show through — which is what put the
        /// black margins on this screen in the first place.
        /// </summary>
        [Test]
        public void BackgroundIsOneFlatColourAndTheTextOnItIsDark()
        {
            RectTransform fill = Rect("BackgroundFill");
            var image = fill.GetComponent<Image>();
            Assert.That(
                image.color.a,
                Is.EqualTo(1f).Within(0.001f),
                "The background fill is not opaque, so whatever sits behind the "
                + "canvas shows through it.");
            Assert.That(fill.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(fill.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(
                fill.sizeDelta,
                Is.EqualTo(Vector2.zero),
                "The fill is inset from the canvas, so its edges are uncovered.");

            float background = image.color.r * 0.299f
                + image.color.g * 0.587f
                + image.color.b * 0.114f;
            foreach (string name in
                     new[] { "ConnectionStatusText", "MicrophoneStatusText" })
            {
                var text = Find(name).GetComponent<TMP_Text>();
                float ink = text.color.r * 0.299f
                    + text.color.g * 0.587f
                    + text.color.b * 0.114f;
                Assert.That(
                    background - ink,
                    Is.GreaterThan(0.25f),
                    $"'{name}' is only {background - ink:0.00} darker than the "
                    + "background it sits on.");
            }
        }

        [Test]
        public void PoliceStayLeftAndThievesStayRight()
        {
            RectTransform police = Rect("PoliceTeamGroup");
            RectTransform thief = Rect("ThiefTeamGroup");
            Assert.That(police.anchorMin.x, Is.EqualTo(0f));
            Assert.That(thief.anchorMin.x, Is.EqualTo(1f));
        }

        /// <summary>
        /// The characters' band must clear the control band. They overlapped in
        /// the old lobby and the network status sat across the officer's feet.
        /// </summary>
        [Test]
        public void CharacterBandClearsTheControlBandAtItsTallest()
        {
            RectTransform characters = Rect("CharacterArea");
            RectTransform controls = Rect("LobbyControlArea");

            // Measured from the authored child sizes rather than the control
            // area's own sizeDelta: a ContentSizeFitter has not run on a prefab
            // asset, so its stored height means nothing. The tallest case is
            // every room row showing at once.
            var column = controls.GetComponent<VerticalLayoutGroup>();
            Assert.That(column, Is.Not.Null);
            float content = TallestContent(controls) + column.spacing
                * (controls.childCount - 1);

            float controlsTop = controls.anchoredPosition.y + content;
            float charactersBottom = 1080f + characters.offsetMin.y;

            Assert.That(
                charactersBottom,
                Is.GreaterThanOrEqualTo(controlsTop),
                $"The characters reach down to {charactersBottom:0} from the "
                + $"bottom and a full control band reaches up to "
                + $"{controlsTop:0}. They would overlap.");
        }

        /// <summary>
        /// The room list is the one part of the lobby whose height depends on
        /// what the network turns up. It is kept out of the control stack for
        /// that reason, and floats in the gap between the two teams — so what
        /// has to be proved is that a full list stays inside that gap.
        /// </summary>
        [Test]
        public void FullRoomListStaysInTheGapBetweenTheTeams()
        {
            RectTransform list = Rect("RoomListArea");
            var column = list.GetComponent<VerticalLayoutGroup>();
            Assert.That(column, Is.Not.Null);

            float height = TallestContent(list)
                + column.spacing * (list.childCount - 1);
            float top = list.anchoredPosition.y + height;

            // The gap the list grows into is between the two teams, so rising
            // level with them is fine and the ceiling is the title above them.
            // Reaching the logo is what there is no room for.
            RectTransform logo = Rect("TitleLogo");
            float logoBottom = 1080f + logo.anchoredPosition.y
                - logo.sizeDelta.y;
            Assert.That(
                top,
                Is.LessThanOrEqualTo(logoBottom),
                $"A full room list reaches up to {top:0} from the bottom and the "
                + $"title comes down to {logoBottom:0}. They would collide.");

            float halfWidth = list.sizeDelta.x * 0.5f;
            float policeRight = TeamContentEdge("PoliceTeamGroup", true);
            float thiefLeft = TeamContentEdge("ThiefTeamGroup", false);
            Assert.That(
                960f - halfWidth,
                Is.GreaterThanOrEqualTo(policeRight),
                "The room list overlaps the police team.");
            Assert.That(
                960f + halfWidth,
                Is.LessThanOrEqualTo(thiefLeft),
                "The room list overlaps the thief team.");
        }

        /// <summary>
        /// How far a team's artwork actually reaches, in screen coordinates.
        /// The group rect is wider than its contents, so measuring the group
        /// would report a collision that is not there.
        /// </summary>
        private float TeamContentEdge(string groupName, bool rightEdge)
        {
            RectTransform group = Rect(groupName);
            float extent = 0f;
            foreach (Transform child in group)
            {
                var image = child.GetComponent<Image>();
                if (image == null || child.name == "RoleBadge")
                {
                    continue;
                }

                var rect = (RectTransform)child;
                extent = Mathf.Max(
                    extent,
                    Mathf.Abs(rect.anchoredPosition.x) + rect.sizeDelta.x);
            }

            float margin = Mathf.Abs(group.anchoredPosition.x);
            return rightEdge ? margin + extent : 1920f - margin - extent;
        }

        private static float TallestContent(RectTransform area)
        {
            float total = 0f;
            foreach (Transform child in area)
            {
                var element = child.GetComponent<LayoutElement>();
                total += element != null
                    ? element.preferredHeight
                    : ((RectTransform)child).sizeDelta.y;
            }

            return total;
        }

        [Test]
        public void BootstrapUsesThePrefabAndHidesTheOldCanvas()
        {
            Assert.That(File.Exists(BootstrapPath), Is.True, BootstrapPath);
            Scene scene = EditorSceneManager.OpenScene(
                BootstrapPath,
                OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();

                GameObject legacy =
                    roots.FirstOrDefault(root => root.name == "Scene UI");
                Assert.That(
                    legacy,
                    Is.Not.Null,
                    "The scene contract still requires this canvas to exist.");
                Assert.That(
                    legacy.activeSelf,
                    Is.False,
                    "The old title canvas is still on; its dark backdrop is "
                    + "what filled the margins beside the lobby.");

                GameObject lobby =
                    roots.FirstOrDefault(root => root.name == "LobbyCanvas");
                Assert.That(lobby, Is.Not.Null, "The lobby is not in Bootstrap.");
                Assert.That(
                    PrefabUtility.GetCorrespondingObjectFromSource(lobby),
                    Is.EqualTo(_prefab),
                    "The lobby in the scene is not an instance of the prefab.");

                var presenter = lobby.GetComponent<NetworkLobbyPresenter>();
                Assert.That(presenter, Is.Not.Null);
                FieldInfo session = typeof(NetworkLobbyPresenter).GetField(
                    "session",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(
                    session.GetValue(presenter) as Object,
                    Is.Not.Null,
                    "The scene did not hand the lobby its session, so no "
                    + "button can start or join one.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static IEnumerable<FieldInfo> SerializedFields(
            Object component,
            ICollection<string> skip)
        {
            return component.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field =>
                    field.GetCustomAttribute<SerializeField>() != null)
                .Where(field => !skip.Contains(field.Name));
        }

        private float PreferredHeight(string name)
        {
            var element = Find(name).GetComponent<LayoutElement>();
            Assert.That(
                element,
                Is.Not.Null,
                $"'{name}' has no LayoutElement, so its layout group is free "
                + "to shrink it to nothing.");
            return element.preferredHeight;
        }

        private float CaptionSize(string name)
        {
            Transform label = Find(name).transform.Find("Label");
            Assert.That(label, Is.Not.Null, $"'{name}' has no caption.");
            return label.GetComponent<TMP_Text>().fontSize;
        }

        private RectTransform Rect(string name)
        {
            return (RectTransform)Find(name).transform;
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

            Assert.Fail($"The lobby prefab has no '{name}'.");
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
