using System;
using PawsAndLoot.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
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

        [SerializeField]
        private Sprite policeTitleSprite;

        [SerializeField]
        private Sprite thiefTitleSprite;

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
        private TMP_Text reasonLabel;

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

        public string ReasonText => Read(reasonLabel);
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
        public Sprite TitleSprite =>
            titleImage != null ? titleImage.sprite : null;

        public Sprite VersusSprite =>
            versusImage != null ? versusImage.sprite : null;

        public void ConfigureArt(
            Image configuredTitleImage,
            Sprite configuredPoliceTitle,
            Sprite configuredThiefTitle,
            Image configuredVersusImage,
            Sprite configuredPoliceVersus,
            Sprite configuredThiefVersus)
        {
            titleImage = configuredTitleImage;
            policeTitleSprite = configuredPoliceTitle;
            thiefTitleSprite = configuredThiefTitle;
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
            TMP_Text configuredReason,
            TMP_Text configuredElapsedValue,
            Image configuredMiddleIcon,
            Sprite configuredArrestIcon,
            Sprite configuredLootIcon,
            TMP_Text configuredMiddleCaption,
            TMP_Text configuredMiddleValue,
            TMP_Text configuredGoldCaption,
            TMP_Text configuredGoldValue)
        {
            reasonLabel = configuredReason;
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

            SetSprite(
                titleImage,
                policeWon ? policeTitleSprite : thiefTitleSprite);
            SetSprite(
                versusImage,
                policeWon ? policeVersusSprite : thiefVersusSprite);

            SetText(policeBadgeLabel, policeWon ? "승리" : "패배");
            SetText(thiefBadgeLabel, policeWon ? "패배" : "승리");
            SetText(reasonLabel, DescribeReason(result.Reason, summary));

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
            SetSprite(titleImage, policeTitleSprite);
            SetSprite(versusImage, policeVersusSprite);
            SetText(policeBadgeLabel, "대기");
            SetText(thiefBadgeLabel, "대기");
            SetText(reasonLabel, "경기를 한 번 진행하면 결과가 표시됩니다.");
            SetText(elapsedValueLabel, "--:--");
            SetSprite(middleIconImage, arrestIconSprite);
            SetText(middleCaptionLabel, "경찰 체포");
            SetText(middleValueLabel, "-");
            SetText(goldCaptionLabel, "도둑 골드");
            SetText(goldValueLabel, "-");
        }

        public void ValidateOrThrow()
        {
            if (titleImage == null
                || versusImage == null
                || reasonLabel == null
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
                    + "'Paws & Loot/UI/Rebuild Result Canvas Prefab'.");
            }
        }

        public static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        /// <summary>
        /// The sentence under the title. It quotes the count that actually
        /// ended the match, which is the one number a player wants first.
        /// </summary>
        private static string DescribeReason(
            MatchEndReason reason,
            MatchSummary summary)
        {
            return reason switch
            {
                MatchEndReason.ThiefArrested =>
                    $"도둑을 {summary.CatchCount}번 체포했습니다",
                MatchEndReason.SaleTargetReached =>
                    "목표 골드를 모아 탈출에 성공했습니다",
                MatchEndReason.TimeExpiredBelowTarget =>
                    "제한 시간 안에 목표 골드를 모으지 못했습니다",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "Unknown match end reason.")
            };
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

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image != null && sprite != null && image.sprite != sprite)
            {
                image.sprite = sprite;
            }
        }

        private void OnEnable()
        {
            Refresh();
        }
    }
}
