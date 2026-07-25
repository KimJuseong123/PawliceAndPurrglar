using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class RoleObjectivePresenter : MonoBehaviour
    {
        private const float DefaultPlayingDisplaySeconds = 4f;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private GameObject objectivePanel;

        [SerializeField]
        private Text objectiveLabel;

        [SerializeField, Min(0f)]
        private float playingDisplaySeconds =
            DefaultPlayingDisplaySeconds;

        private MatchState _lastState = (MatchState)(-1);
        private float _playingTimeRemaining;

        public void Configure(
            MatchRuntimeState runtime,
            LocalPlayerRoleSelector selector,
            GameObject panel,
            Text label,
            float displaySeconds =
                DefaultPlayingDisplaySeconds)
        {
            matchRuntime = runtime;
            roleSelector = selector;
            objectivePanel = panel;
            objectiveLabel = label;
            playingDisplaySeconds = Mathf.Max(0f, displaySeconds);
            Refresh(0f);
        }

        public void Refresh(float deltaTime)
        {
            if (matchRuntime == null
                || roleSelector == null
                || objectivePanel == null
                || objectiveLabel == null)
            {
                return;
            }

            MatchState state = matchRuntime.CurrentState;
            if (state != _lastState)
            {
                _lastState = state;
                _playingTimeRemaining =
                    state == MatchState.Playing
                        ? playingDisplaySeconds
                        : 0f;
            }

            if (state == MatchState.Playing)
            {
                _playingTimeRemaining = Mathf.Max(
                    0f,
                    _playingTimeRemaining
                        - Mathf.Max(0f, deltaTime));
            }

            bool shouldShow =
                state == MatchState.Ready
                || (state == MatchState.Playing
                    && _playingTimeRemaining > 0f);
            objectivePanel.SetActive(shouldShow);
            if (shouldShow)
            {
                objectiveLabel.text =
                    GetObjective(roleSelector.ActiveRole);
            }
        }

        public static string GetObjective(PlayerRole role)
        {
            return role == PlayerRole.Thief
                ? "THIEF: Steal and sell loot to reach the target amount before time runs out."
                : "POLICE: Catch the thief or stop them from reaching the target sale amount.";
        }

        private void Update()
        {
            Refresh(Time.deltaTime);
        }
    }
}
