using System;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class ResultScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private Text winnerLabel;

        [SerializeField]
        private Text reasonLabel;

        [SerializeField]
        private Text soldAmountLabel;

        [SerializeField]
        private Text remainingTimeLabel;

        public string WinnerText => winnerLabel != null
            ? winnerLabel.text
            : string.Empty;
        public string ReasonText => reasonLabel != null
            ? reasonLabel.text
            : string.Empty;
        public string SoldAmountText => soldAmountLabel != null
            ? soldAmountLabel.text
            : string.Empty;
        public string RemainingTimeText => remainingTimeLabel != null
            ? remainingTimeLabel.text
            : string.Empty;

        public void Configure(
            Text configuredWinnerLabel,
            Text configuredReasonLabel,
            Text configuredSoldAmountLabel,
            Text configuredRemainingTimeLabel)
        {
            winnerLabel = configuredWinnerLabel;
            reasonLabel = configuredReasonLabel;
            soldAmountLabel = configuredSoldAmountLabel;
            remainingTimeLabel = configuredRemainingTimeLabel;
            ValidateOrThrow();
            Refresh();
        }

        public void Refresh()
        {
            ValidateOrThrow();
            if (!MatchResultSession.TryGet(out MatchResult result))
            {
                winnerLabel.text = "NO MATCH RESULT";
                reasonLabel.text = "PLAY A MATCH TO VIEW THE RESULT";
                soldAmountLabel.text = "SOLD 0 GOLD";
                remainingTimeLabel.text = "TIME 00:00";
                return;
            }

            winnerLabel.text = result.Winner switch
            {
                MatchWinner.Police => "POLICE WIN",
                MatchWinner.Thief => "THIEF WIN",
                _ => throw new ArgumentOutOfRangeException()
            };
            reasonLabel.text = GetReasonText(result.Reason);
            soldAmountLabel.text = $"SOLD {result.SoldAmount:N0} GOLD";
            remainingTimeLabel.text =
                $"TIME {FormatTime(result.RemainingSeconds)}";
        }

        public void ValidateOrThrow()
        {
            if (winnerLabel == null
                || reasonLabel == null
                || soldAmountLabel == null
                || remainingTimeLabel == null)
            {
                throw new InvalidOperationException(
                    $"ResultScreenPresenter '{name}' has missing labels.");
            }
        }

        public static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string GetReasonText(MatchEndReason reason)
        {
            return reason switch
            {
                MatchEndReason.ThiefArrested =>
                    "THIEF ARRESTED",
                MatchEndReason.TimeExpiredBelowTarget =>
                    "TIME EXPIRED / TARGET NOT REACHED",
                MatchEndReason.SaleTargetReached =>
                    "SALE TARGET REACHED",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "Unknown match end reason.")
            };
        }

        private void OnEnable()
        {
            Refresh();
        }
    }
}
