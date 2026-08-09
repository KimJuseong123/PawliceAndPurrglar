using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Assembles the lobby as a prefab of individually anchored controls.
    ///
    /// The lobby it replaces was the authored mockup stretched across the
    /// screen with captionless, fully transparent buttons pinned over it at
    /// fixed pixel offsets. That only lines up at the mockup's own 4:3, and the
    /// black bars either side of it were the scene's older canvas showing
    /// through. Everything here is anchored, nine-sliced and captioned, so the
    /// same layout holds from 1280x720 upwards.
    ///
    /// A prefab rather than nodes built straight into Bootstrap.unity: the
    /// references survive in one asset instead of a few hundred lines of scene
    /// YAML, and Play Mode can open the lobby on its own.
    /// </summary>
    public static class LobbyCanvasBuilder
    {
        public const string PrefabPath =
            "Assets/_Project/UI/Prefabs/LobbyCanvas.prefab";

        private const string ElementFolder = LobbyArtExtractor.OutputFolder;

        private const string FontAssetPath =
            "Assets/Resources/PawsAndLootDefaultFont.asset";

        /// <summary>
        /// Room rows. A LAN playtest shows one; four is headroom. Kept at four
        /// because <c>NetworkLobbyProbe</c> scans "Room Slot 0".."Room Slot 3".
        /// </summary>
        public const int RoomSlotCount = 4;

        private static readonly Vector2 Reference = new(1920f, 1080f);

        // Sampled from the mockup rather than taken from the palette table, so
        // the faint halo left around each cut-out silhouette lands on exactly
        // the colour it was cut from and disappears.
        private static readonly Color Backdrop = Hex("F2E5DA");

        private static readonly Color Ink = Hex("38271F");
        private static readonly Color PoliceBlue = Hex("3566BE");
        private static readonly Color ThiefRed = Hex("E63C35");
        private static readonly Color StartAmber = Hex("F5B53B");
        private static readonly Color MicPurple = Hex("8A4BA0");
        private static readonly Color PanelCream = Hex("FFF9F2");
        private static readonly Color Muted = Hex("A99B8F");
        private static readonly Color PawTint = Hex("DCCDBC");

        // Vertical plan at the reference height. The specified 22/45/16/11 split
        // does not survive giving the LAN room list and the two status lines
        // their own non-overlapping space, so the control band is wider and the
        // logo band narrower than the guide; the character band is what the
        // guide protects and it keeps its share.
        private const float LogoTop = 12f;
        private const float LogoWidth = 430f;
        private const float LogoHeight = 223f;
        private const float CharacterTop = 232f;
        private const float CharacterHeight = 432f;

        /// <summary>
        /// Height reserved under the characters' feet for the role badge, so a
        /// badge never lands on top of anybody.
        /// </summary>
        private const float BadgeBand = 48f;

        private const float TeamGroupWidth = 640f;

        /// <summary>
        /// Shared display height for both teams' artwork. Every pair sprite
        /// is cropped to its own opaque bounds on import, so one height here
        /// means one character height on screen.
        /// </summary>
        private const float PairArtHeight =
            CharacterHeight - BadgeBand - 8f;
        private const float TeamSideMargin = 64f;

        private const float ActionBarBottom = 28f;
        private const float ActionBarHeight = 100f;
        private const float ControlAreaBottom =
            ActionBarBottom + ActionBarHeight + 14f;
        private const float ControlAreaWidth = 1180f;

        // Three stacked rows inside the address panel: this machine's address
        // and role on top, the fields in the middle, their captions underneath.
        // Sized so no row's text has to be clipped to fit.
        private const float NetworkRowHeight = 150f;
        private const float FieldHeight = 68f;
        private const float PanelCaptionSize = 22f;
        private const float PanelNoteSize = 24f;
        private const float StatusRowHeight = 44f;
        private const float StatusSize = 28f;
        /// <summary>
        /// The invite code is read out loud over a call, so it is the largest
        /// text on the screen after the title. Bounded by the field it sits in:
        /// TMP draws nothing at all when a line does not fit, and the field is
        /// 68 tall with a 2px inset either side.
        /// </summary>
        private const float InviteCodeSize = 40f;

        private const float RoomRowHeight = 40f;
        private const float RoomTextSize = 25f;

        /// <summary>
        /// The control band is exactly the network row and the status line. It
        /// is a fixed height so the character band above it can be sized once
        /// and never has to move.
        /// </summary>
        private const float ControlBandSpacing = 8f;
        private const float ControlBandHeight =
            NetworkRowHeight + ControlBandSpacing + StatusRowHeight;

        /// <summary>
        /// The room list floats above the control band in the gap between the
        /// two teams rather than sitting in the stack with it. In the stack a
        /// single discovered room pushed the address panel up into the
        /// characters; here the list grows into space that is empty anyway.
        /// </summary>
        private const float RoomListBottom =
            ControlAreaBottom + ControlBandHeight + 10f;

        /// <summary>
        /// Narrow enough that a full room list clears the widest character
        /// pose on both sides. The list floats in the gap between the teams
        /// and the high-five poses reach further in than the standing ones.
        /// </summary>
        /// <summary>
        /// Sized to the gap the room list actually has, not to the text.
        ///
        /// The gap is what is left between the two teams' artwork, so it narrows
        /// whenever the characters grow. A room line reads "방 1 · 참가 가능 (1/2)",
        /// nowhere near this wide, so the gap is the binding constraint and this
        /// number belongs to it.
        /// </summary>
        private const float RoomRowWidth = 600f;

        /// <summary>
        /// Everything the lobby needs, in dependency order: cut the art, draw
        /// the chrome, build the prefab, then put it in the scene. One entry
        /// point because each step reads the previous step's output, and running
        /// them out of order fails on a missing asset.
        /// </summary>
        [MenuItem("PawliceAndPurrglar/UI/Rebuild Lobby (Art, Prefab, Scene)")]
        public static void RebuildAll()
        {
            LobbyArtExtractor.Extract();
            LobbyUiSpriteFactory.Generate();
            Rebuild();
            NetworkLobbySetup.RebuildBootstrapLobby();
        }

        [MenuItem("PawliceAndPurrglar/UI/Rebuild Lobby Canvas Prefab")]
        public static void Rebuild()
        {
            GameObject root = Build();
            try
            {
                string folder = Path.GetDirectoryName(PrefabPath)
                    ?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder))
                {
                    LobbyArtExtractor.EnsureFolder(folder);
                }

                ProjectFontSweep.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Lobby canvas prefab rebuilt at '{PrefabPath}'.");
        }

        /// <summary>
        /// Builds the hierarchy without saving it. Every reference that lives
        /// inside the prefab is wired here; the scene supplies only the session
        /// and the room directory afterwards.
        /// </summary>
        public static GameObject Build()
        {
            var canvasObject = new GameObject(
                "LobbyCanvas",
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
            Stretch(canvasRect);

            RectTransform safeArea = Node("SafeAreaRoot", canvasRect);
            Stretch(safeArea);

            BuildBackground(safeArea);
            BuildHeader(safeArea);
            LobbyCharacterView characterView = BuildCharacterArea(safeArea);
            LobbyControls controls = BuildControlArea(safeArea);
            LobbyActions actions = BuildActionBar(safeArea);
            Node("DebugLayer", safeArea).gameObject.SetActive(false);

            var presenter = canvasObject.AddComponent<NetworkLobbyPresenter>();
            presenter.ConfigureView(
                controls.InviteNote,
                controls.ConnectionStatus,
                controls.RoleStatus,
                controls.InviteCode,
                controls.CopyCode,
                controls.Host,
                controls.Join,
                actions.SwapRole,
                actions.StartMatch,
                actions.Leave);
            presenter.ConfigureRoomListView(
                controls.RoomListLabel,
                controls.RoomButtons,
                controls.RoomLabels);
            presenter.ConfigureCharacterView(characterView);

            var diagnostics =
                canvasObject.AddComponent<LobbyMicrophoneDiagnostics>();
            diagnostics.ConfigureView(
                controls.MicrophoneStatus,
                actions.MicrophoneTest,
                actions.MicrophoneTestLabel);

            return canvasObject;
        }

        /// <summary>
        /// One flat colour across the whole canvas.
        ///
        /// The colour is sampled from the mockup the cut-outs were taken from, so
        /// the faint halo left around each silhouette lands on exactly the colour
        /// it was cut from and disappears. A painted plate was tried here and
        /// taken back out — see `ISSUE-053`.
        ///
        /// It takes raycasts so a click on empty space lands on the background
        /// rather than falling through to whatever is behind the canvas.
        /// </summary>
        private static void BuildBackground(RectTransform parent)
        {
            RectTransform background = Node("BackgroundLayer", parent);
            Stretch(background);

            Image fill = NewGraphic<Image>("BackgroundFill", background);
            Stretch((RectTransform)fill.transform);
            fill.color = Backdrop;
            fill.raycastTarget = true;
        }

        private static void BuildHeader(RectTransform parent)
        {
            RectTransform header = Node("Header", parent);
            Stretch(header);

            Image logo = NewGraphic<Image>("TitleLogo", header);
            Anchor(
                (RectTransform)logo.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -LogoTop),
                new Vector2(LogoWidth, LogoHeight));
            logo.sprite = Sprite("lobby_logo");
            logo.preserveAspect = true;
            logo.raycastTarget = false;

            CreatePaw(header, "LeftPawDecoration", -287f);
            CreatePaw(header, "RightPawDecoration", 287f);
        }

        private static void CreatePaw(RectTransform parent, string name, float x)
        {
            Image paw = NewGraphic<Image>(name, parent);
            Anchor(
                (RectTransform)paw.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(x, -62f),
                new Vector2(66f, 66f));
            paw.sprite = Sprite("icon_paw");
            paw.color = PawTint;
            paw.preserveAspect = true;
            paw.raycastTarget = false;
            // Mirrored so the pair reads as a left and a right print.
            paw.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                x < 0f ? 14f : -14f);
        }

        /// <summary>
        /// The two teams keep fixed sides, each shown as one authored pair
        /// sprite that swaps between a standing pose and a high five.
        ///
        /// One image per team rather than four separate character cut-outs. The
        /// cut-outs were keyed out of a mockup by brightness and hue, which left
        /// ragged edges and ate the whites of an eye wherever a character sat
        /// against the ivory.
        /// </summary>
        private static LobbyCharacterView BuildCharacterArea(
            RectTransform parent)
        {
            RectTransform area = Node("CharacterArea", parent);
            // Full width, pinned under the header: the two groups sit at the
            // outer edges and the gap between them widens with the screen
            // instead of the characters growing.
            area.anchorMin = new Vector2(0f, 1f);
            area.anchorMax = new Vector2(1f, 1f);
            area.pivot = new Vector2(0.5f, 1f);
            area.offsetMin = new Vector2(0f, -CharacterTop - CharacterHeight);
            area.offsetMax = new Vector2(0f, -CharacterTop);

            RectTransform police = Node("PoliceTeamGroup", area);
            Anchor(
                police,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(TeamSideMargin, 0f),
                new Vector2(TeamGroupWidth, CharacterHeight));

            RectTransform thief = Node("ThiefTeamGroup", area);
            Anchor(
                thief,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-TeamSideMargin, 0f),
                new Vector2(TeamGroupWidth, CharacterHeight));

            // Sized to the widest pose each side can show and pushed against the
            // outer edge. Preserve Aspect centres art inside its rect, so a rect
            // wider than the art would float it towards the middle of the screen
            // and into the room list.
            Image policeArt = CreatePairArt(
                police,
                "PoliceTeamArt",
                WidestAspect("police_dog_idle", "police_dog_selected"),
                true);

            Image thiefArt = CreatePairArt(
                thief,
                "ThiefTeamArt",
                WidestAspect("thief_cat_idle", "thief_cat_selected"),
                false);

            GameObject policeBadge = CreateBadge(police, "경찰", PoliceBlue, true);
            GameObject thiefBadge = CreateBadge(thief, "도둑", ThiefRed, false);

            var view = area.gameObject.AddComponent<LobbyCharacterView>();
            view.Configure(
                policeArt,
                thiefArt,
                PairSprite("police_dog_idle"),
                PairSprite("police_dog_selected"),
                PairSprite("thief_cat_idle"),
                PairSprite("thief_cat_selected"),
                policeBadge,
                thiefBadge);
            return view;
        }

        /// <summary>
        /// One team's artwork, sitting above the badge band and hugging the
        /// screen edge.
        ///
        /// The rect is as wide as the widest pose this side can show, so the
        /// height is always what binds under Preserve Aspect and the characters
        /// keep their size when the pose changes. Anchored outwards rather than
        /// centred in the group, which is what keeps the wider high-five pose
        /// clear of the room list in the middle.
        /// </summary>
        private static Image CreatePairArt(
            RectTransform group,
            string name,
            float widestAspect,
            bool fromLeft)
        {
            Image image = NewGraphic<Image>(name, group);
            float edge = fromLeft ? 0f : 1f;
            Anchor(
                (RectTransform)image.transform,
                new Vector2(edge, 0f),
                new Vector2(edge, 0f),
                new Vector2(edge, 0f),
                new Vector2(0f, BadgeBand),
                new Vector2(PairArtHeight * widestAspect, PairArtHeight));
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static float WidestAspect(string first, string second)
        {
            return Mathf.Max(Aspect(first), Aspect(second));
        }

        private static float Aspect(string name)
        {
            Sprite sprite = PairSprite(name);
            return sprite.rect.width / sprite.rect.height;
        }

        private static Sprite PairSprite(string name)
        {
            return UiBuildKit.Sprite(
                LobbyPairArtImporter.OutputFolder,
                name);
        }

        private static GameObject CreateBadge(
            RectTransform group,
            string caption,
            Color tint,
            bool fromLeft)
        {
            RectTransform badge = Node("RoleBadge", group);
            float edge = fromLeft ? 0f : 1f;
            Anchor(
                badge,
                new Vector2(edge, 0f),
                new Vector2(edge, 0f),
                new Vector2(edge, 0f),
                new Vector2(fromLeft ? 96f : -96f, 0f),
                new Vector2(196f, 44f));

            Image outline = NewGraphic<Image>("Outline", badge);
            Stretch((RectTransform)outline.transform);
            Sliced(outline, "ui_plate_outline", 2.6f);
            outline.color = Color.white;
            outline.raycastTarget = false;

            Image fill = NewGraphic<Image>("Fill", badge);
            Stretch((RectTransform)fill.transform, 4f, 4f, 4f, 4f);
            Sliced(fill, "ui_plate_fill", 2.6f);
            fill.color = tint;
            fill.raycastTarget = false;

            TMP_Text label = Text("Label", badge, 25f, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            label.text = $"내 역할 · {caption}";
            label.color = Color.white;

            badge.gameObject.SetActive(false);
            return badge.gameObject;
        }

        private readonly struct LobbyControls
        {
            public LobbyControls(
                TMP_Text inviteNote,
                TMP_Text roleStatus,
                TMP_Text connectionStatus,
                TMP_Text microphoneStatus,
                TMP_InputField inviteCode,
                Button copyCode,
                Button host,
                Button join,
                TMP_Text roomListLabel,
                Button[] roomButtons,
                TMP_Text[] roomLabels)
            {
                InviteNote = inviteNote;
                RoleStatus = roleStatus;
                ConnectionStatus = connectionStatus;
                MicrophoneStatus = microphoneStatus;
                InviteCode = inviteCode;
                CopyCode = copyCode;
                Host = host;
                Join = join;
                RoomListLabel = roomListLabel;
                RoomButtons = roomButtons;
                RoomLabels = roomLabels;
            }

            public TMP_Text InviteNote { get; }
            public TMP_Text RoleStatus { get; }
            public TMP_Text ConnectionStatus { get; }
            public TMP_Text MicrophoneStatus { get; }
            public TMP_InputField InviteCode { get; }
            public Button CopyCode { get; }
            public Button Host { get; }
            public Button Join { get; }
            public TMP_Text RoomListLabel { get; }
            public Button[] RoomButtons { get; }
            public TMP_Text[] RoomLabels { get; }
        }

        /// <summary>
        /// The control band grows upwards from just above the action bar rather
        /// than downwards from a fixed top, so a room appearing in the list
        /// never pushes the buttons off the bottom of the screen.
        /// </summary>
        private static LobbyControls BuildControlArea(RectTransform parent)
        {
            RectTransform area = Node("LobbyControlArea", parent);
            Anchor(
                area,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, ControlAreaBottom),
                new Vector2(ControlAreaWidth, ControlBandHeight));

            var column = area.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.LowerCenter;
            column.spacing = ControlBandSpacing;
            column.childControlWidth = true;
            column.childControlHeight = true;
            // Off throughout: forcing expansion is what flattens a button into a
            // bar and squeezes a field until its caption overlaps its text.
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            column.childScaleWidth = false;
            column.childScaleHeight = false;

            var fitter = area.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Built against the control area's parent, not the control area, so
            // discovering a room cannot change where the address panel sits.
            (TMP_Text roomListLabel, Button[] roomButtons, TMP_Text[] roomLabels) =
                BuildRoomList(parent);

            (TMP_Text inviteNote,
                TMP_Text roleStatus,
                TMP_InputField inviteCode,
                Button copyCode,
                Button host,
                Button join) = BuildRoomRow(area);

            (TMP_Text connectionStatus, TMP_Text microphoneStatus) =
                BuildStatusRow(area);

            return new LobbyControls(
                inviteNote,
                roleStatus,
                connectionStatus,
                microphoneStatus,
                inviteCode,
                copyCode,
                host,
                join,
                roomListLabel,
                roomButtons,
                roomLabels);
        }

        private static (TMP_Text, Button[], TMP_Text[]) BuildRoomList(
            RectTransform parent)
        {
            RectTransform list = Node("RoomListArea", parent);
            Anchor(
                list,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, RoomListBottom),
                new Vector2(RoomRowWidth + 60f, LineBox(RoomTextSize)));

            var column = list.gameObject.AddComponent<VerticalLayoutGroup>();
            // Bottom aligned and grown upwards, into the empty middle rather
            // than down onto the address panel.
            column.childAlignment = TextAnchor.LowerCenter;
            column.spacing = 6f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;

            var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            // No LayoutElement on the list itself. A layout group already
            // reports its own preferred height from its children, and pinning
            // one here would hold the area at a single row's height however
            // many rooms turned up.

            TMP_Text label = Text(
                "Room List Label",
                list,
                RoomTextSize,
                TextAlignmentOptions.Center);
            label.color = Muted;
            label.text = "초대코드로 만나세요.";
            Fixed(
                (RectTransform)label.transform,
                RoomRowWidth + 60f,
                LineBox(RoomTextSize));

            var buttons = new Button[RoomSlotCount];
            var labels = new TMP_Text[RoomSlotCount];
            for (int index = 0; index < RoomSlotCount; index++)
            {
                Button slot = Plate(
                    $"Room Slot {index}",
                    list,
                    PanelCream,
                    Ink,
                    string.Empty,
                    RoomTextSize,
                    2.4f,
                    null,
                    RoomRowHeight);
                Fixed(
                    slot.GetComponent<RectTransform>(),
                    RoomRowWidth,
                    RoomRowHeight);
                buttons[index] = slot;
                labels[index] = slot.GetComponentInChildren<TMP_Text>(true);
                // Hidden until the directory reports a room, so an empty LAN
                // shows one line of guidance rather than four blank rows.
                slot.gameObject.SetActive(false);
            }

            return (label, buttons, labels);
        }

        /// <summary>
        /// One field, one code.
        ///
        /// The same box shows the code you were given and takes the code you
        /// were sent, because a lobby with a "your code" box and a "their code"
        /// box next to it invites exactly one mistake — typing theirs into
        /// yours — and there is no state in which both are filled. Hosting
        /// fills it and locks it; joining leaves it empty and open.
        ///
        /// It replaces the address and port pair, which a browser cannot use:
        /// there is no listening socket to name, and a player behind CGNAT has
        /// no address to read out even on the desktop build.
        /// </summary>
        private static (TMP_Text, TMP_Text, TMP_InputField, Button, Button,
            Button) BuildRoomRow(RectTransform parent)
        {
            RectTransform row = Node("RoomRow", parent);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.spacing = 18f;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
            Fixed(row, ControlAreaWidth, NetworkRowHeight, flexibleWidth: 1f);

            RectTransform panel = Node("RoomPanel", parent: row);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            Sliced(panelImage, "ui_panel", 1f);
            panelImage.color = Color.white;
            Fixed(panel, 930f, NetworkRowHeight, flexibleWidth: 1f);

            // Top row of the panel: what to tell the other player, and which
            // side this machine ended up on. Both are short, so they share one
            // line and cost the layout nothing.
            TMP_Text inviteNote = TopNote(
                panel,
                "Invite Note",
                TextAlignmentOptions.Left,
                0f,
                0.62f);
            inviteNote.text = "방을 만들면 초대코드가 나옵니다.";

            TMP_Text roleStatus = TopNote(
                panel,
                "Role",
                TextAlignmentOptions.Right,
                0.62f,
                1f);
            roleStatus.text = "내 역할: 대기 중";

            const float FieldRow = 40f;

            TMP_Text codeLabel = Text(
                "CodeLabel",
                panel,
                34f,
                TextAlignmentOptions.Center);
            Anchor(
                (RectTransform)codeLabel.transform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, FieldRow),
                new Vector2(180f, FieldHeight));
            codeLabel.text = "초대코드";
            codeLabel.color = Ink;

            // Larger than the other fields ever were. Six characters read out
            // over a call is the one string in this game that has to survive
            // being misheard, and the size is the only defence against that
            // which costs nothing.
            TMP_InputField inviteCode = Field(
                "Invite Code Field",
                panel,
                "예) ABC123",
                TextAlignmentOptions.Center,
                InviteCodeSize,
                verticalInset: 2f);
            Anchor(
                (RectTransform)inviteCode.transform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(216f, FieldRow),
                new Vector2(430f, FieldHeight));
            // Six is the length Relay issues, so a seventh keystroke is a typo
            // and the field simply refuses it.
            inviteCode.characterLimit = 6;
            inviteCode.characterValidation =
                TMP_InputField.CharacterValidation.Alphanumeric;

            Button copyCode = Plate(
                "Copy Code Button",
                panel,
                PanelCream,
                Ink,
                "코드 복사",
                28f,
                1.6f,
                null,
                FieldHeight);
            Anchor(
                copyCode.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(676f, FieldRow),
                new Vector2(230f, FieldHeight));

            Caption(panel, "받은 코드를 입력하세요", 220f, 430f);

            RectTransform column = Node("ConnectionButtonColumn", row);
            var vertical = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.childAlignment = TextAnchor.MiddleCenter;
            vertical.spacing = 10f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = false;
            vertical.childForceExpandHeight = false;
            Fixed(column, 216f, NetworkRowHeight);

            // The object names stay. NetworkLobbyProbe looks both of these up by
            // name to prove the buttons are wired at all, and a rename would
            // leave it passing every scenario it could still reach while
            // silently skipping this one.
            Button host = Plate(
                "Host Button",
                column,
                StartAmber,
                Ink,
                "방 만들기",
                30f,
                1.5f,
                null,
                62f);
            Fixed(host.GetComponent<RectTransform>(), 216f, 62f);

            Button join = Plate(
                "Join Button",
                column,
                PanelCream,
                Ink,
                "방 입장",
                30f,
                1.5f,
                null,
                62f);
            Fixed(join.GetComponent<RectTransform>(), 216f, 62f);

            return (inviteNote, roleStatus, inviteCode, copyCode, host, join);
        }

        /// <summary>
        /// A note on the panel's top row, anchored across a horizontal slice so
        /// the pair share the line without either being able to run into the
        /// other.
        /// </summary>
        private static TMP_Text TopNote(
            RectTransform panel,
            string name,
            TextAlignmentOptions alignment,
            float from,
            float to)
        {
            TMP_Text note = Text(name, panel, PanelNoteSize, alignment);
            var rect = (RectTransform)note.transform;
            rect.anchorMin = new Vector2(from, 1f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(26f, -4f - LineBox(PanelNoteSize));
            rect.offsetMax = new Vector2(-26f, -4f);
            note.color = Muted;
            return note;
        }

        private static void Caption(
            RectTransform panel,
            string text,
            float x,
            float width)
        {
            TMP_Text caption = Text(
                $"{text} Caption",
                panel,
                PanelCaptionSize,
                TextAlignmentOptions.Left);
            Anchor(
                (RectTransform)caption.transform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(x, 4f),
                new Vector2(width, LineBox(PanelCaptionSize)));
            caption.text = text;
            caption.color = Muted;
        }

        private static (TMP_Text, TMP_Text) BuildStatusRow(RectTransform parent)
        {
            RectTransform row = Node("StatusRow", parent);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.spacing = 20f;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
            Fixed(row, ControlAreaWidth, StatusRowHeight, flexibleWidth: 1f);

            TMP_Text connection = Text(
                "ConnectionStatusText",
                row,
                StatusSize,
                TextAlignmentOptions.Left);
            connection.color = Ink;
            // The same sentence the presenter writes on its first refresh. An
            // authored default that says something else is a string nobody ever
            // sees and nobody ever updates — this one still offered to take an
            // IP address months after the field for one was removed.
            connection.text = "방을 만들거나 받은 코드로 입장하세요.";
            // Overflow, like every other label here. Ellipsis blanked this line
            // twice: TMP refuses to draw at all when the rect is under one line
            // box, and the row is only a couple of pixels short of one. The
            // messages are short and the row is wide, so overflowing is both
            // safer and, when it happens at all, visible.
            Fixed(
                (RectTransform)connection.transform,
                740f,
                StatusRowHeight,
                flexibleWidth: 1f);

            TMP_Text microphone = Text(
                "MicrophoneStatusText",
                row,
                StatusSize,
                TextAlignmentOptions.Right);
            microphone.color = Muted;
            microphone.text = "마이크 확인 대기";
            Fixed((RectTransform)microphone.transform, 400f, StatusRowHeight);

            return (connection, microphone);
        }

        private readonly struct LobbyActions
        {
            public LobbyActions(
                Button swapRole,
                Button startMatch,
                Button microphoneTest,
                TMP_Text microphoneTestLabel,
                Button leave)
            {
                SwapRole = swapRole;
                StartMatch = startMatch;
                MicrophoneTest = microphoneTest;
                MicrophoneTestLabel = microphoneTestLabel;
                Leave = leave;
            }

            public Button SwapRole { get; }
            public Button StartMatch { get; }
            public Button MicrophoneTest { get; }
            public TMP_Text MicrophoneTestLabel { get; }
            public Button Leave { get; }
        }

        private static LobbyActions BuildActionBar(RectTransform parent)
        {
            RectTransform bar = Node("BottomActionBar", parent);
            Anchor(
                bar,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, ActionBarBottom),
                new Vector2(1156f, ActionBarHeight));

            var horizontal = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.spacing = 28f;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            Button swap = Plate(
                "Swap Role Button",
                bar,
                PoliceBlue,
                Color.white,
                "역할 바꾸기",
                40f,
                1f,
                "icon_paw",
                ActionBarHeight);
            Fixed(swap.GetComponent<RectTransform>(), 356f, ActionBarHeight);

            Button start = Plate(
                "Start Match Button",
                bar,
                StartAmber,
                Ink,
                "게임 시작",
                40f,
                1f,
                "icon_paw",
                ActionBarHeight);
            // The one call to action, so it is allowed to be the widest — within
            // the ten per cent the layout guide permits.
            Fixed(start.GetComponent<RectTransform>(), 388f, ActionBarHeight);

            Button microphone = Plate(
                "Mic Test Button",
                bar,
                MicPurple,
                Color.white,
                "마이크 확인",
                40f,
                1f,
                "icon_mic",
                ActionBarHeight);
            Fixed(
                microphone.GetComponent<RectTransform>(),
                356f,
                ActionBarHeight);

            // Small and off to the side: it only matters once a session is
            // running, and it must never compete with "게임 시작".
            Button leave = Plate(
                "Leave Button",
                parent,
                PanelCream,
                Ink,
                "나가기",
                26f,
                2f,
                null,
                52f);
            Anchor(
                leave.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(36f, ActionBarBottom + 24f),
                new Vector2(168f, 52f));

            return new LobbyActions(
                swap,
                start,
                microphone,
                microphone.transform.Find("Label")
                    .GetComponent<TMP_Text>(),
                leave);
        }

        /// <summary>
        /// A button: dark plate behind, tinted face in front, caption and marks
        /// on top. Two layers because tinting a single plate drags its outline
        /// towards the button colour and the disabled tint washes it out.
        /// </summary>
        private static Button Plate(
            string name,
            Transform parent,
            Color fillColor,
            Color textColor,
            string caption,
            float fontSize,
            float pixelsPerUnitMultiplier,
            string markSprite,
            float height)
        {
            return UiBuildKit.Plate(
                name,
                parent,
                fillColor,
                textColor,
                caption,
                fontSize,
                pixelsPerUnitMultiplier,
                markSprite,
                height);
        }

        private static Image CreateMark(
            RectTransform root,
            string spriteName,
            Color tint,
            float size,
            bool left)
        {
            return UiBuildKit.Mark(root, spriteName, tint, size, left);
        }

        private static TMP_InputField Field(
            string name,
            Transform parent,
            string placeholder,
            TextAlignmentOptions alignment,
            float fontSize = 30f,
            float verticalInset = 6f)
        {
            RectTransform root = Node(name, parent);
            var background = root.gameObject.AddComponent<Image>();
            Sliced(background, "ui_field", 1f);
            background.color = Color.white;

            RectTransform viewport = Node("Text Area", root);
            // The inset is what is left for the glyphs. A 40pt line needs
            // about 58px and the field is 68 tall, so six pixels top and bottom
            // would leave 56 — under one line box, which is where TMP stops
            // drawing altogether rather than clipping.
            Stretch(viewport, 20f, verticalInset, 20f, verticalInset);
            viewport.gameObject.AddComponent<RectMask2D>();

            TMP_Text placeholderText = Text(
                "Placeholder",
                viewport,
                fontSize,
                alignment);
            Stretch((RectTransform)placeholderText.transform);
            placeholderText.text = placeholder;
            placeholderText.color = new Color(
                Muted.r,
                Muted.g,
                Muted.b,
                0.75f);

            TMP_Text text = Text("Text", viewport, fontSize, alignment);
            Stretch((RectTransform)text.transform);
            text.color = Ink;
            text.richText = false;

            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.targetGraphic = background;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretWidth = 3;
            field.customCaretColor = true;
            field.caretColor = Ink;
            field.selectionColor = new Color(
                PoliceBlue.r,
                PoliceBlue.g,
                PoliceBlue.b,
                0.35f);
            return field;
        }

        private static TMP_Text Text(
            string name,
            Transform parent,
            float size,
            TextAlignmentOptions alignment)
        {
            return UiBuildKit.Text(name, parent, size, alignment);
        }

        /// <summary>
        /// Minimum rect height for a given font size. TMP needs more than the
        /// point size for a line, and a rect that is merely equal to it either
        /// clips or, under Ellipsis, blanks.
        /// </summary>
        private static float LineBox(float fontSize)
        {
            return UiBuildKit.LineBox(fontSize);
        }

        private static void Sliced(
            Image image,
            string spriteName,
            float pixelsPerUnitMultiplier)
        {
            UiBuildKit.Sliced(image, spriteName, pixelsPerUnitMultiplier);
        }

        private static Sprite Sprite(string name)
        {
            return UiBuildKit.Sprite(ElementFolder, name);
        }

        private static T NewGraphic<T>(string name, Transform parent)
            where T : Graphic
        {
            return UiBuildKit.NewGraphic<T>(name, parent);
        }

        private static RectTransform Node(string name, Transform parent)
        {
            return UiBuildKit.Node(name, parent);
        }

        private static void Stretch(
            RectTransform rect,
            float left = 0f,
            float bottom = 0f,
            float right = 0f,
            float top = 0f)
        {
            UiBuildKit.Stretch(rect, left, bottom, right, top);
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            UiBuildKit.Anchor(rect, anchorMin, anchorMax, pivot, position, size);
        }

        /// <summary>
        /// Every child of a layout group states its own minimum and preferred
        /// size. Without this the group is free to shrink a button below the
        /// height its caption needs.
        /// </summary>
        private static void Fixed(
            RectTransform rect,
            float width,
            float height,
            float flexibleWidth = 0f)
        {
            UiBuildKit.Fixed(rect, width, height, flexibleWidth);
        }

        private static Color Hex(string value)
        {
            return UiBuildKit.Hex(value);
        }
    }
}
