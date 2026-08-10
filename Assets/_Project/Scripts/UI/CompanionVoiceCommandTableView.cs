using System.Collections.Generic;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// What this player can say to their animal, in the top-left corner.
    ///
    /// It replaces the panel that listed `CTRL + 1..4`. Those keys were the
    /// stand-in for voice while there was no microphone and were removed on
    /// 2026-08-10, and a table describing them would have been a table of a
    /// control that no longer exists. This one is the thing it describes: the
    /// phrases here are the stems the server matches without a model, so
    /// reading a row out loud is guaranteed to resolve.
    ///
    /// Role-aware. The two animals take different orders, and printing the
    /// other side's is worse than printing nothing — a player who tries "짖어"
    /// as the thief gets silence and no reason for it.
    ///
    /// Installs itself, like <see cref="PlayerStatusBannerView"/> and
    /// <see cref="InkBlindOverlayView"/>. The alternative is a serialised field
    /// on the HUD, which means regenerating `Game.unity` to populate it — and
    /// that rewrites the `GlobalObjectIdHash` of all 130 in-scene
    /// NetworkObjects, which makes every existing build incompatible with every
    /// new one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionVoiceCommandTableView : MonoBehaviour
    {
        /// <summary>
        /// Panel geometry, in the reference resolution the scaler works in.
        ///
        /// Nine rows is the whole vocabulary for one role: four that belong to
        /// this animal and five either animal obeys.
        /// </summary>
        private const float PanelWidth = 340f;
        private const float RowHeight = 30f;
        private const float HeaderHeight = 46f;
        private const float DividerHeight = 10f;
        private const float NameColumnWidth = 96f;

        /// <summary>
        /// TMP draws nothing at all — not an ellipsis, nothing — when a rect is
        /// shorter than one line, so every text rect here is at least this much
        /// taller than its type size (`ISSUE-047`).
        /// </summary>
        private const float LineHeightFactor = 1.45f;

        private const float NameTypeSize = 15f;
        private const float PhraseTypeSize = 13f;

        /// <summary>
        /// How often the role is re-read. A rebuild is a dozen GameObjects, so
        /// it is not done per frame; the role changes once per match, on a
        /// rematch that swapped sides.
        /// </summary>
        private const float RoleCheckSeconds = 0.5f;

        private RectTransform _panel;
        private CanvasGroup _group;
        private TMP_FontAsset _font;
        private readonly List<GameObject> _rows = new();

        private bool _hasBuiltRole;
        private PlayerRole _builtRole;
        private float _nextRoleCheck;

        /// <summary>
        /// The role the table is currently drawn for, and how many rows it has.
        /// Exposed so a test can assert what is on screen rather than the
        /// component's opinion of itself.
        /// </summary>
        public PlayerRole? ShowingRole => _hasBuiltRole ? _builtRole : null;

        public int RowCount => _rows.Count;

        public float Alpha => _group != null ? _group.alpha : 0f;

        /// <summary>
        /// Every line of text on the panel, in order. A test reads this to
        /// check the thief is not being shown the dog's orders.
        /// </summary>
        public IReadOnlyList<string> Lines
        {
            get
            {
                var lines = new List<string>();
                if (_panel == null)
                {
                    return lines;
                }

                foreach (TMP_Text text in
                    _panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    lines.Add(text.text);
                }

                return lines;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Only where there is a match. Bootstrap and Result have no animal
            // to talk to, and a command table over the lobby would be the same
            // class of mistake as the panel this replaces.
            if (FindFirstObjectByType<CompanionCommandDispatcher>() == null
                || FindFirstObjectByType<CompanionVoiceCommandTableView>()
                    != null)
            {
                return;
            }

            new GameObject("Companion Voice Command Table")
                .AddComponent<CompanionVoiceCommandTableView>();
        }

        private void Awake()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Below the stuck banner and the ink. It is a reference card, not a
            // warning; anything that needs to interrupt should cover it.
            canvas.sortingOrder = 320;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _font = Resources.Load<TMP_FontAsset>("PawliceAndPurrglarDefaultFont");
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRoleCheck)
            {
                return;
            }

            _nextRoleCheck = Time.unscaledTime + RoleCheckSeconds;
            Refresh();
        }

        /// <summary>
        /// Rebuilds if the role changed, and does nothing otherwise. Public so
        /// a test can drive it without waiting half a second.
        /// </summary>
        public void Refresh()
        {
            if (_group == null)
            {
                // Called before Awake — a test that adds the component and asks
                // in the same frame. Nothing to draw on yet.
                return;
            }

            if (!LocalPlayerRoleSelector.TryResolveLocalRole(
                out PlayerRole role))
            {
                // No role yet. Blank rather than a guess: showing the police
                // table to a thief for the first frames of a match teaches the
                // wrong words at exactly the moment somebody is reading it.
                _group.alpha = 0f;
                return;
            }

            if (_hasBuiltRole && _builtRole == role)
            {
                _group.alpha = 1f;
                return;
            }

            Build(role);
            _hasBuiltRole = true;
            _builtRole = role;
            _group.alpha = 1f;
        }

        private void Build(PlayerRole role)
        {
            // Cleared immediately rather than at the end of the frame. A
            // rebuild happens when the role becomes known, and `Refresh` is
            // called again on the next line — a panel still holding the
            // previous role's rows would show both for a frame and would report
            // both to anything reading `Lines`.
            if (_panel != null)
            {
                for (int index = _panel.childCount - 1; index >= 0; index--)
                {
                    DestroyImmediate(_panel.GetChild(index).gameObject);
                }
            }

            _rows.Clear();

            CompanionCommandId[] commands =
                CompanionCommandCatalog.GetCommandsFor(role);

            // This animal's own commands, a gap, then the five either animal
            // obeys.
            //
            // Asked of the catalog rather than counted as four. The two animals
            // had four each until CAT-010 gave the cat "물기", and a constant
            // would have drawn the cat's divider one row early — filing a
            // cat-only order under "either animal obeys these", which is the
            // exact confusion this table exists to prevent.
            int ownCommandCount = 0;
            foreach (CompanionCommandId candidate in commands)
            {
                if (CompanionCommandCatalog.IsSharedByBothAnimals(candidate))
                {
                    break;
                }

                ownCommandCount++;
            }

            float height = HeaderHeight
                + commands.Length * RowHeight
                + DividerHeight;

            if (_panel == null)
            {
                var panelObject = new GameObject("Panel");
                panelObject.transform.SetParent(transform, false);
                Image background = panelObject.AddComponent<Image>();
                background.color = new Color(0.02f, 0.07f, 0.09f, 0.62f);
                background.raycastTarget = false;
                _panel = background.rectTransform;
            }

            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(24f, -24f);
            _panel.sizeDelta = new Vector2(PanelWidth, height);

            bool police = role == PlayerRole.Police;
            AddLabel(
                "Title",
                police ? "강아지에게 말하기" : "고양이에게 말하기",
                17f,
                new Color(0.05f, 0.95f, 1f, 1f),
                new Vector2(14f, -10f),
                new Vector2(PanelWidth - 28f, 17f * LineHeightFactor),
                TextAlignmentOptions.Left);

            AddLabel(
                "Hint",
                "V를 누른 채 말한다",
                12f,
                new Color(0.75f, 0.88f, 0.92f, 1f),
                new Vector2(14f, -28f),
                new Vector2(PanelWidth - 28f, 12f * LineHeightFactor),
                TextAlignmentOptions.Left);

            float y = -HeaderHeight;
            for (int index = 0; index < commands.Length; index++)
            {
                if (index == ownCommandCount)
                {
                    // The five below the line work for either animal, which is
                    // worth showing: they are also the only ones that resolve
                    // with the speech model unreachable.
                    y -= DividerHeight;
                }

                AddRow(commands[index], y, index >= ownCommandCount);
                y -= RowHeight;
            }
        }

        private void AddRow(
            CompanionCommandId command,
            float y,
            bool shared)
        {
            var row = new GameObject(command.ToString());
            row.transform.SetParent(_panel, false);
            var rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(PanelWidth, RowHeight);
            _rows.Add(row);

            Color nameColor = shared
                ? new Color(0.72f, 0.88f, 0.94f, 1f)
                : new Color(1f, 0.86f, 0.32f, 1f);

            AddLabel(
                "Name",
                CompanionCommandCatalog.GetKoreanName(command),
                NameTypeSize,
                nameColor,
                new Vector2(14f, -4f),
                new Vector2(NameColumnWidth, NameTypeSize * LineHeightFactor),
                TextAlignmentOptions.Left,
                rect);

            AddLabel(
                "Phrases",
                CompanionCommandCatalog.GetSpokenExamples(command),
                PhraseTypeSize,
                new Color(0.88f, 0.94f, 0.96f, 1f),
                new Vector2(14f + NameColumnWidth, -5f),
                new Vector2(
                    PanelWidth - NameColumnWidth - 28f,
                    PhraseTypeSize * LineHeightFactor),
                TextAlignmentOptions.Left,
                rect);
        }

        private void AddLabel(
            string name,
            string text,
            float size,
            Color color,
            Vector2 position,
            Vector2 sizeDelta,
            TextAlignmentOptions alignment,
            RectTransform parent = null)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent != null ? parent : _panel, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                label.font = _font;
                label.fontSharedMaterial = _font.material;
            }

            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;

            // Never `Ellipsis`. TMP draws nothing at all when the rect is
            // shorter than a line, and a phrase list that vanishes is worse
            // than one that runs on.
            label.overflowMode = TextOverflowModes.Overflow;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
        }
    }
}
