using System;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class PoliceHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private ThiefLootWallet thiefWallet;

        [SerializeField]
        private LootCarrier thiefCarrier;

        [SerializeField]
        private ArrestProgressController arrestProgress;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Text remainingTimeLabel;

        [SerializeField]
        private Text thiefSaleAmountLabel;

        [SerializeField]
        private Text arrestProgressLabel;

        [SerializeField]
        private Text theftAlertLabel;

        [SerializeField]
        private Text currentGoalLabel;

        public GameObject Panel => panel;

        public void Configure(
            LocalPlayerRoleSelector selector,
            MatchRuntimeState configuredMatchRuntime,
            ThiefLootWallet configuredThiefWallet,
            LootCarrier configuredThiefCarrier,
            ArrestProgressController configuredArrestProgress,
            GameObject configuredPanel,
            Text configuredRemainingTimeLabel,
            Text configuredThiefSaleAmountLabel,
            Text configuredArrestProgressLabel,
            Text configuredTheftAlertLabel,
            Text configuredCurrentGoalLabel)
        {
            roleSelector = selector;
            matchRuntime = configuredMatchRuntime;
            thiefWallet = configuredThiefWallet;
            thiefCarrier = configuredThiefCarrier;
            arrestProgress = configuredArrestProgress;
            panel = configuredPanel;
            remainingTimeLabel = configuredRemainingTimeLabel;
            thiefSaleAmountLabel = configuredThiefSaleAmountLabel;
            arrestProgressLabel = configuredArrestProgressLabel;
            theftAlertLabel = configuredTheftAlertLabel;
            currentGoalLabel = configuredCurrentGoalLabel;
            ValidateOrThrow();
            Refresh();
        }

        public void Refresh()
        {
            if (panel == null || roleSelector == null)
            {
                return;
            }

            bool isPolice =
                roleSelector.ActiveRole == PlayerRole.Police;
            panel.SetActive(isPolice);
            if (!isPolice)
            {
                return;
            }

            remainingTimeLabel.text =
                $"TIME  {CommonHudPresenter.FormatTime(matchRuntime.RemainingMatchSeconds)}";
            thiefSaleAmountLabel.text =
                $"THIEF GOLD  {thiefWallet.SoldAmount} / {thiefWallet.TargetAmount}";
            arrestProgressLabel.text =
                $"ARREST  {Mathf.RoundToInt(arrestProgress.ProgressNormalized * 100f)}%";
            theftAlertLabel.text = ResolveTheftAlert();
            currentGoalLabel.text =
                $"GOAL  ARREST THIEF OR STOP {thiefWallet.TargetAmount} GOLD";
        }

        public void ValidateOrThrow()
        {
            if (roleSelector == null
                || matchRuntime == null
                || thiefWallet == null
                || thiefCarrier == null
                || arrestProgress == null
                || panel == null
                || remainingTimeLabel == null
                || thiefSaleAmountLabel == null
                || arrestProgressLabel == null
                || theftAlertLabel == null
                || currentGoalLabel == null)
            {
                throw new InvalidOperationException(
                    $"PoliceHudPresenter '{name}' has missing references.");
            }
        }

        private string ResolveTheftAlert()
        {
            if (thiefCarrier.HasLoot)
            {
                return "THEFT ALERT  LOOT IN TRANSIT";
            }

            return thiefWallet.LastSoldLoot != null
                ? $"THEFT ALERT  SALE +{thiefWallet.LastSalePrice}"
                : "THEFT ALERT  CLEAR";
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void Update()
        {
            Refresh();
        }
    }
}
