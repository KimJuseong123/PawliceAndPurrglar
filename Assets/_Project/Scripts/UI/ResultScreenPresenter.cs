using System;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Reports how the match actually went.
    ///
    /// The screen this replaces built its four labels at font size one, fully
    /// transparent and switched off, and painted the numbers into the
    /// background picture instead. Every match therefore ended with the same
    /// clock, the same arrest count and the same gold, whatever had happened.
    ///
    /// Read only with respect to rules: the verdict comes from
    /// <see cref="MatchResultSession"/> and the counters from
    /// <see cref="MatchSummary"/>, and nothing here decides anything.
    /// </summary>
    public sealed class ResultScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private Image titleImage;

        /// <summary>
        /// 승리 and 패배, chosen by whether the local player won.
        ///
        /// Not by who won. The old pair read "경찰 승리!" and "도둑 승리!", which
        /// is the winner stated twice and leaves the loser reading a title about
        /// somebody else's match. The illustration below still shows whoever
        /// actually won, so the two are chosen from different questions.
        /// </summary>
        [SerializeField]
        private Sprite wonTitleSprite;

        [SerializeField]
        private Sprite lostTitleSprite;

        [SerializeField]
        private Image versusImage;

        [SerializeField]
        private Sprite policeVersusSprite;

        [SerializeField]
        private Sprite thiefVersusSprite;

        [SerializeField]
        private TMP_Text policeBadgeLabel;

        [SerializeField]
        private TMP_Text thiefBadgeLabel;

        [SerializeField]
        private TMP_Text elapsedValueLabel;

        [SerializeField]
        private Image middleIconImage;

        [SerializeField]
        private Sprite arrestIconSprite;

        [SerializeField]
        private Sprite lootIconSprite;

        [SerializeField]
        private TMP_Text middleCaptionLabel;

        [SerializeField]
        private TMP_Text middleValueLabel;

        [SerializeField]
        private TMP_Text goldCaptionLabel;

        [SerializeField]
        private TMP_Text goldValueLabel;

        public string ElapsedText => Read(elapsedValueLabel);
        public string MiddleCaptionText => Read(middleCaptionLabel);
        public string MiddleValueText => Read(middleValueLabel);
        public string GoldCaptionText => Read(goldCaptionLabel);
        public string GoldValueText => Read(goldValueLabel);
        public string PoliceBadgeText => Read(policeBadgeLabel);
        public string ThiefBadgeText => Read(thiefBadgeLabel);

        /// <summary>
        /// Which title art is showing. The winner is announced by artwork
        /// rather than a string, so this is what a test has to look at.
        /// </summary>
        /// <summary>
        /// Whether the title currently showing is the winning one, for tests.
        ///
        /// Exposed as a question rather than leaving them to compare sprites,
        /// because the thing worth asserting is "does this say I won", and a test
        /// that compares against <c>wonTitleSprite</c> passes just as happily when
        /// both fields hold the same sprite.
        /// </summary>
        public bool IsShowingWinTitle =>
            titleImage != null
            && wonTitleSprite != null
            && titleImage.sprite == wonTitleSprite;

        public Sprite TitleSprite =>
            titleImage != null ? titleImage.sprite : null;

        public Sprite VersusSprite =>
            versusImage != null ? versusImage.sprite : null;

        /// <summary>
        /// The titles are named for the viewer and the illustrations for the
        /// winner, because that is how they are chosen. Naming both pairs after the
        /// teams is what let the old screen show a loser a title about the winner.
        /// </summary>
        public void ConfigureArt(
            Image configuredTitleImage,
            Sprite configuredWonTitle,
            Sprite configuredLostTitle,
            Image configuredVersusImage,
            Sprite configuredPoliceVersus,
            Sprite configuredThiefVersus)
        {
            titleImage = configuredTitleImage;
            wonTitleSprite = configuredWonTitle;
            lostTitleSprite = configuredLostTitle;
            versusImage = configuredVersusImage;
            policeVersusSprite = configuredPoliceVersus;
            thiefVersusSprite = configuredThiefVersus;
        }

        public void ConfigureBadges(
            TMP_Text configuredPoliceBadge,
            TMP_Text configuredThiefBadge)
        {
            policeBadgeLabel = configuredPoliceBadge;
            thiefBadgeLabel = configuredThiefBadge;
        }

        public void ConfigureStats(
            TMP_Text configuredElapsedValue,
            Image configuredMiddleIcon,
            Sprite configuredArrestIcon,
            Sprite configuredLootIcon,
            TMP_Text configuredMiddleCaption,
            TMP_Text configuredMiddleValue,
            TMP_Text configuredGoldCaption,
            TMP_Text configuredGoldValue)
        {
            elapsedValueLabel = configuredElapsedValue;
            middleIconImage = configuredMiddleIcon;
            arrestIconSprite = configuredArrestIcon;
            lootIconSprite = configuredLootIcon;
            middleCaptionLabel = configuredMiddleCaption;
            middleValueLabel = configuredMiddleValue;
            goldCaptionLabel = configuredGoldCaption;
            goldValueLabel = configuredGoldValue;
        }

        public void Refresh()
        {
            ValidateOrThrow();

            if (!MatchResultSession.TryGet(out MatchResult result))
            {
                ShowNoResult();
                return;
            }

            MatchResultSession.TryGetSummary(out MatchSummary summary);
            bool policeWon = result.Winner == MatchWinner.Police;

            // Whose screen this is. Resolved from the role the lobby committed
            // before the scene changed, which survives the load as a static; the
            // selector component itself belongs to the match scene and is gone by
            // the time this runs.
            bool localIsPolice = ResolveLocalRole() == PlayerRole.Police;
            bool localWon = localIsPolice == policeWon;

            SetSprite(
                titleImage,
                localWon ? wonTitleSprite : lostTitleSprite);
            SetSprite(
                versusImage,
                policeWon ? policeVersusSprite : thiefVersusSprite);

            SetText(policeBadgeLabel, policeWon ? "승리" : "패배");
            SetText(thiefBadgeLabel, policeWon ? "패배" : "승리");

            // The middle card reports whichever side's effort decided the
            // match, so it changes with the winner rather than showing a zero.
            SetSprite(
                middleIconImage,
                policeWon ? arrestIconSprite : lootIconSprite);
            SetText(middleCaptionLabel, policeWon ? "경찰 체포" : "훔친 보물");
            SetText(goldCaptionLabel, policeWon ? "도둑 골드" : "획득 골드");

            // A verdict can be stored without a summary — a scene opened on its
            // own, or a caller that only had a result to hand. The gold is on
            // the verdict itself, so that much is always honest; the counters
            // that only the evaluator can see say nothing rather than zero.
            if (!summary.IsReported)
            {
                SetText(elapsedValueLabel, "--:--");
                SetText(middleValueLabel, "-");
                SetText(goldValueLabel, result.SoldAmount.ToString("N0"));
                return;
            }

            SetText(elapsedValueLabel, FormatTime(summary.ElapsedSeconds));
            SetText(
                middleValueLabel,
                policeWon
                    ? $"{summary.CatchCount} / {summary.RequiredCatchCount}"
                    : summary.SoldCount.ToString("N0"));
            SetText(
                goldValueLabel,
                $"{summary.SoldAmount:N0} / {summary.TargetAmount:N0}");
        }

        /// <summary>
        /// Opening the result scene without having played is a development
        /// path, so it says so rather than reporting a match of all zeroes as
        /// though it had happened.
        /// </summary>
        private void ShowNoResult()
        {
            // Nothing is claimed. No title, no badge, no verdict: a screen
            // opened without a match has nothing true to say, and the one thing it
            // must not do is show 승리 over a match that never happened. That is
            // what this screen used to do with painted numbers (ISSUE-050).
            SetSprite(titleImage, null);
            SetSprite(versusImage, policeVersusSprite);
            SetText(policeBadgeLabel, string.Empty);
            SetText(thiefBadgeLabel, string.Empty);
            SetText(elapsedValueLabel, "--:--");
            SetSprite(middleIconImage, arrestIconSprite);
            SetText(middleCaptionLabel, "경찰 체포");
            SetText(middleValueLabel, "-");
            SetText(goldCaptionLabel, "도둑 골드");
            SetText(goldValueLabel, "-");
        }

        /// <summary>
        /// Which side the local player was on.
        ///
        /// Asked of the static rather than of a component: the role is committed in
        /// the lobby and carried across the scene load as a static value, because a
        /// scene-placed <c>NetworkObject</c> does not survive one (ISSUE-016). By
        /// the time this screen exists the match scene and everything in it is gone.
        ///
        /// Falls back to police when nothing was committed, which is the same
        /// default the match scene uses. A wrong guess here swaps 승리 and 패배, so
        /// it is worth saying plainly that this is a guess only when no match was
        /// played through the lobby.
        /// </summary>
        private static PlayerRole ResolveLocalRole()
        {
            return LocalPlayerRoleSelector.OverriddenRole ?? PlayerRole.Police;
        }

        public void ValidateOrThrow()
        {
            if (titleImage == null
                || versusImage == null
                || elapsedValueLabel == null
                || middleCaptionLabel == null
                || middleValueLabel == null
                || goldCaptionLabel == null
                || goldValueLabel == null
                || policeBadgeLabel == null
                || thiefBadgeLabel == null)
            {
                throw new InvalidOperationException(
                    $"ResultScreenPresenter '{name}' is missing references. "
                    + "Rebuild the result prefab with "
                    + "'PawliceAndPurrglar/UI/Rebuild Result Canvas Prefab'.");
            }
        }

        public static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string Read(TMP_Text label)
        {
            return label != null ? label.text : string.Empty;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value)
            {
                label.text = value;
            }
        }

        /// <summary>
        /// Sets a sprite, and switches the graphic off when there is none.
        ///
        /// A null sprite is not "no picture": an <c>Image</c> with no sprite draws a
        /// white quad. Ignoring null — which this used to do — is what put a blank
        /// white rectangle where the title belongs on a screen with no result. The
        /// component has to go off, not merely be given nothing.
        /// </summary>
        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            if (image.sprite != sprite)
            {
                image.sprite = sprite;
            }

            image.enabled = sprite != null;
        }

        private void OnEnable()
        {
            Refresh();
        }
    }
}
