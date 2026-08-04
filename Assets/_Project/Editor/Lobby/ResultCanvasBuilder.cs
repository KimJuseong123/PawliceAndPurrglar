using System.IO;
using PawsAndLoot.Core;
using PawsAndLoot.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Assembles the result screen as a prefab of individually anchored parts.
    ///
    /// What it replaces was the lobby's problem twice over: an authored mockup
    /// stretched behind three transparent, captionless buttons, and four result
    /// labels built at font size one, fully transparent and switched off. The
    /// clock, the arrests and the gold a player read were painted into the
    /// picture, so every match ended with the same numbers.
    ///
    /// The versus illustration stays one sprite per outcome. It holds no
    /// controls and no figures, it is one drawing, and it changes as a unit
    /// when the winner changes.
    /// </summary>
    public static class ResultCanvasBuilder
    {
        public const string PrefabPath =
            "Assets/_Project/UI/Prefabs/ResultCanvas.prefab";

        private const string ArtFolder = ResultArtExtractor.OutputFolder;

        private static readonly Vector2 Reference = new(1920f, 1080f);

        /// <summary>
        /// Sampled from the mockups' card interior so the faint halo left
        /// around each keyed cut-out lands on the colour it was cut from.
        /// </summary>
        private static readonly Color Backdrop = UiBuildKit.Hex("FCF0E2");

        private const float HeaderTop = 12f;
        private const float HeaderHeight = 44f;
        private const float TitleTop = 62f;
        private const float TitleHeight = 178f;

        /// <summary>
        /// Width is derived from the illustration's own aspect at build time,
        /// so the panels are never stretched.
        /// </summary>
        private const float VersusTop = 302f;
        private const float VersusHeight = 490f;

        /// <summary>
        /// The illustration crop's own pixel size, used to place the badges on
        /// the corners the mockup painted them on whatever size it is drawn.
        /// </summary>
        // The new mockups are 1672x941 against the old 1448x1086, and the
        // illustration inside them measures 1285x428 rather than 1350x448. Every
        // fraction below is taken against these, so they have to change together.
        private const float SourceBandWidth = 1285f;
        private const float SourceBandHeight = 428f;

        private const float CardHeight = 124f;
        private const float CardWidth = 452f;
        private const float CardBottom = 150f;

        private const float ButtonHeight = 100f;
        private const float ButtonWidth = 420f;
        private const float ButtonBottom = 32f;

        /// <summary>
        /// Everything the result screen needs, in dependency order: cut the
        /// art, draw the chrome, build the prefab, then put it in the scene.
        /// One entry point because each step reads the previous step's output.
        /// </summary>
        [MenuItem("Paws & Loot/UI/Rebuild Result (Art, Prefab, Scene)")]
        public static void RebuildAll()
        {
            ResultArtExtractor.Extract();
            LobbyUiSpriteFactory.Generate();
            Rebuild();
            BasicSceneSetup.RebuildResultUi();
        }

        [MenuItem("Paws & Loot/UI/Rebuild Result Canvas Prefab")]
        public static void Rebuild()
        {
            GameObject root = Build();
            try
            {
                string folder = Path.GetDirectoryName(PrefabPath)
                    ?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder))
                {
                    MockupCutter.EnsureFolder(folder);
                }

                ProjectFontSweep.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Result canvas prefab rebuilt at '{PrefabPath}'.");
        }

        public static GameObject Build()
        {
            var canvasObject = new GameObject(
                "ResultCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            UiBuildKit.Stretch(canvasRect);

            RectTransform safeArea = UiBuildKit.Node("SafeAreaRoot", canvasRect);
            UiBuildKit.Stretch(safeArea);

            Image fill = UiBuildKit.NewGraphic<Image>("BackgroundFill", safeArea);
            UiBuildKit.Stretch((RectTransform)fill.transform);
            fill.color = Backdrop;
            fill.raycastTarget = true;

            BuildHeader(safeArea);
            Image title = BuildTitle(safeArea);
            (Image versus, TMP_Text policeBadge, TMP_Text thiefBadge) =
                BuildVersus(safeArea);
            (TMP_Text elapsed,
                Image middleIcon,
                TMP_Text middleCaption,
                TMP_Text middleValue,
                TMP_Text goldCaption,
                TMP_Text goldValue) = BuildCards(safeArea);
            BuildButtons(safeArea);

            var presenter = canvasObject.AddComponent<ResultScreenPresenter>();
            presenter.ConfigureArt(
                title,
                Art("result_title_win"),
                Art("result_title_lose"),
                versus,
                Art("result_versus_police"),
                Art("result_versus_thief"));
            presenter.ConfigureBadges(policeBadge, thiefBadge);
            presenter.ConfigureStats(
                elapsed,
                middleIcon,
                Art("icon_cuffs"),
                Art("icon_bag"),
                middleCaption,
                middleValue,
                goldCaption,
                goldValue);

            return canvasObject;
        }

        private static void BuildHeader(RectTransform parent)
        {
            Image header = UiBuildKit.NewGraphic<Image>("GameOverHeader", parent);
            UiBuildKit.Anchor(
                (RectTransform)header.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -HeaderTop),
                new Vector2(560f, HeaderHeight));
            header.sprite = Art("result_header");
            header.preserveAspect = true;
            header.raycastTarget = false;
        }

        private static Image BuildTitle(RectTransform parent)
        {
            Image title = UiBuildKit.NewGraphic<Image>("TitleArt", parent);
            UiBuildKit.Anchor(
                (RectTransform)title.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -TitleTop),
                new Vector2(860f, TitleHeight));
            // Off, not merely empty. An Image with no sprite draws a white
            // quad, and the prefab is what the capture tool and the scene show
            // before a match has been played.
            title.sprite = null;
            title.enabled = false;
            title.preserveAspect = true;
            title.raycastTarget = false;
            return title;
        }

        private static (Image, TMP_Text, TMP_Text) BuildVersus(
            RectTransform parent)
        {
            Sprite art = Art("result_versus_police");
            float width = VersusHeight * (art.rect.width / art.rect.height);

            Image versus = UiBuildKit.NewGraphic<Image>("VersusArt", parent);
            var rect = (RectTransform)versus.transform;
            UiBuildKit.Anchor(
                rect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -VersusTop),
                new Vector2(width, VersusHeight));
            versus.sprite = art;
            versus.preserveAspect = true;
            versus.raycastTarget = false;

            // Parented to the illustration and sized as a fraction of it, so
            // they sit exactly on top of the badges painted into the crop
            // whatever size the band ends up.
            //
            // The painted pair cannot be cut away: each lies across the corner
            // of the panel, where neither a rectangle nor a colour key
            // separates it from the artwork underneath. Covering them is what
            // lets the words change with the outcome.
            // Deliberately larger than the painted badge rather than matched to
            // it. In the new crop the plate runs off the top edge, so its full
            // height cannot be measured from the crop at all — and the two errors
            // are not symmetric. Covering a few pixels too much hides some sky;
            // covering a few too few leaves a sliver of the opposite word showing
            // under the badge, which on this screen tells the player they won when
            // they lost.
            var badge = new Vector2(
                width * (200f / SourceBandWidth),
                VersusHeight * (70f / SourceBandHeight));
            float inset = width * (3f / SourceBandWidth);

            TMP_Text police = BuildBadge(
                rect,
                "PoliceBadge",
                UiBuildKit.PoliceBlue,
                true,
                inset,
                badge);
            TMP_Text thief = BuildBadge(
                rect,
                "ThiefBadge",
                UiBuildKit.MicPurple,
                false,
                inset,
                badge);

            // Built and then switched off, because the drawn ones are already
            // right.
            //
            // Covering the painted badges made sense when there was one
            // illustration per winner and a title that named the winner. There are
            // now two illustrations chosen by who won, and each carries the badges
            // for that outcome — 승리 over the police when the police won, 패배 when
            // they did not. Drawing our own on top produced two plates a few pixels
            // apart, which is what the doubled edge in the screenshot was.
            //
            // Kept rather than deleted so the presenter and its tests still have
            // labels to write the verdict into; text on a disabled object is still
            // readable, and this becomes the way back if the art ever ships without
            // badges baked in.
            SetBadgeVisible(police, false);
            SetBadgeVisible(thief, false);
            return (versus, police, thief);
        }

        /// <summary>
        /// Shows or hides a badge by its plate, given the label inside it. The
        /// label's own object is the text; the plate is what is seen.
        /// </summary>
        private static void SetBadgeVisible(TMP_Text label, bool visible)
        {
            Transform plate = label != null ? label.transform.parent : null;
            if (plate != null)
            {
                plate.gameObject.SetActive(visible);
            }
        }

        private static TMP_Text BuildBadge(
            RectTransform versus,
            string name,
            Color tint,
            bool left,
            float inset,
            Vector2 size)
        {
            RectTransform badge = UiBuildKit.Node(name, versus);
            float edge = left ? 0f : 1f;
            UiBuildKit.Anchor(
                badge,
                new Vector2(edge, 1f),
                new Vector2(edge, 1f),
                new Vector2(edge, 1f),
                new Vector2(left ? inset : -inset, 0f),
                size);

            Image outline = UiBuildKit.NewGraphic<Image>("Outline", badge);
            UiBuildKit.Stretch((RectTransform)outline.transform);
            UiBuildKit.Sliced(outline, "ui_plate_outline", 2.2f);
            outline.color = Color.white;
            outline.raycastTarget = false;

            Image fill = UiBuildKit.NewGraphic<Image>("Fill", badge);
            UiBuildKit.Stretch((RectTransform)fill.transform, 5f, 5f, 5f, 5f);
            UiBuildKit.Sliced(fill, "ui_plate_fill", 2.2f);
            fill.color = tint;
            fill.raycastTarget = false;

            TMP_Text label = UiBuildKit.Text(
                "Label",
                badge,
                32f,
                TextAlignmentOptions.Center);
            UiBuildKit.Stretch((RectTransform)label.transform);
            // Blank in the prefab. The presenter fills these with 승리 and 패배 on
            // enable, and anything baked here is a claim about a match that has not
            // been played — which is what "대기" was doing on a screen whose whole
            // job is to report a verdict.
            label.text = string.Empty;
            label.color = Color.white;
            return label;
        }

        private static (TMP_Text, Image, TMP_Text, TMP_Text, TMP_Text, TMP_Text)
            BuildCards(RectTransform parent)
        {
            RectTransform row = UiBuildKit.Node("StatCardRow", parent);
            UiBuildKit.Anchor(
                row,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, CardBottom),
                new Vector2(CardWidth * 3f + 52f, CardHeight));
            UiBuildKit.Group<HorizontalLayoutGroup>(
                row,
                TextAnchor.MiddleCenter,
                26f);

            // Authored captions, not only runtime ones: the presenter fills
            // them on enable, but a prefab whose cards are blank in the editor
            // gives no way to see the layout without entering play mode.
            (TMP_Text elapsedCaption, TMP_Text elapsed, Image clockIcon) =
                BuildCard(row, "ElapsedCard", "icon_clock");
            elapsedCaption.text = "플레이 시간";

            (TMP_Text middleCaption, TMP_Text middleValue, Image middleIcon) =
                BuildCard(row, "MiddleCard", "icon_cuffs");
            middleCaption.text = "경찰 체포";

            (TMP_Text goldCaption, TMP_Text goldValue, Image coinIcon) =
                BuildCard(row, "GoldCard", "icon_coin");
            goldCaption.text = "도둑 골드";

            // The clock and the coin never change; only the middle card's mark
            // swaps with the winner, so only it is handed to the presenter.
            _ = clockIcon;
            _ = coinIcon;

            return (
                elapsed,
                middleIcon,
                middleCaption,
                middleValue,
                goldCaption,
                goldValue);
        }

        private static (TMP_Text, TMP_Text, Image) BuildCard(
            RectTransform row,
            string name,
            string iconSprite)
        {
            RectTransform card = UiBuildKit.Node(name, row);
            UiBuildKit.Fixed(card, CardWidth, CardHeight);

            Image panel = UiBuildKit.NewGraphic<Image>("Panel", card);
            UiBuildKit.Stretch((RectTransform)panel.transform);
            UiBuildKit.Sliced(panel, "ui_panel", 1.4f);
            panel.color = Color.white;
            panel.raycastTarget = false;

            Image icon = UiBuildKit.NewGraphic<Image>("Icon", card);
            UiBuildKit.Anchor(
                (RectTransform)icon.transform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(26f, 0f),
                new Vector2(86f, 86f));
            icon.sprite = Art(iconSprite);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text caption = UiBuildKit.Text(
                "Caption",
                card,
                26f,
                TextAlignmentOptions.Left);
            UiBuildKit.Anchor(
                (RectTransform)caption.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);
            var captionRect = (RectTransform)caption.transform;
            captionRect.offsetMin = new Vector2(126f, -14f - UiBuildKit.LineBox(26f));
            captionRect.offsetMax = new Vector2(-18f, -14f);
            caption.color = UiBuildKit.Muted;

            TMP_Text value = UiBuildKit.Text(
                "Value",
                card,
                42f,
                TextAlignmentOptions.Left);
            UiBuildKit.Anchor(
                (RectTransform)value.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                Vector2.zero);
            var valueRect = (RectTransform)value.transform;
            valueRect.offsetMin = new Vector2(126f, 12f);
            valueRect.offsetMax = new Vector2(-18f, 12f + UiBuildKit.LineBox(42f));
            value.color = UiBuildKit.Ink;
            value.text = "-";

            return (caption, value, icon);
        }

        private static void BuildButtons(RectTransform parent)
        {
            RectTransform row = UiBuildKit.Node("ActionRow", parent);
            UiBuildKit.Anchor(
                row,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, ButtonBottom),
                new Vector2(ButtonWidth * 3f + 60f, ButtonHeight));
            UiBuildKit.Group<HorizontalLayoutGroup>(
                row,
                TextAnchor.MiddleCenter,
                30f);

            Navigation(
                row,
                "Retry Button",
                "다시하기",
                UiBuildKit.PoliceBlue,
                Color.white,
                "icon_refresh",
                GameSceneId.Game);
            Navigation(
                row,
                "Lobby Button",
                "로비로",
                UiBuildKit.MicPurple,
                Color.white,
                "icon_home",
                GameSceneId.Bootstrap);

            Button quit = UiBuildKit.Plate(
                "Quit Button",
                row,
                UiBuildKit.QuitRed,
                Color.white,
                "종료",
                40f,
                1f,
                "icon_exit",
                ButtonHeight);
            UiBuildKit.Fixed(
                quit.GetComponent<RectTransform>(),
                ButtonWidth,
                ButtonHeight);
            quit.gameObject.AddComponent<ApplicationQuitButton>();
        }

        /// <summary>
        /// <see cref="SceneNavigationButton"/> rather than a direct scene load:
        /// it routes a rematch through the networked coordinator when a session
        /// is running, and the scene contract counts these to check that the
        /// result screen can reach both the match and the lobby.
        /// </summary>
        private static void Navigation(
            RectTransform row,
            string name,
            string caption,
            Color fill,
            Color textColor,
            string mark,
            GameSceneId target)
        {
            Button button = UiBuildKit.Plate(
                name,
                row,
                fill,
                textColor,
                caption,
                40f,
                1f,
                mark,
                ButtonHeight);
            UiBuildKit.Fixed(
                button.GetComponent<RectTransform>(),
                ButtonWidth,
                ButtonHeight);
            button.gameObject.AddComponent<SceneNavigationButton>().TargetScene =
                target;
        }

        private static Sprite Art(string name)
        {
            return UiBuildKit.Sprite(ArtFolder, name);
        }
    }
}
