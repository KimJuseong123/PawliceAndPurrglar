using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    public sealed class CommonHudPresenter : MonoBehaviour
    {
        private static CommonHudPresenter instance;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private Text timerLabel;

        [SerializeField]
        private Text roleLabel;

        [SerializeField]
        private Text stateLabel;

        [SerializeField]
        private Text interactionLabel;

        public static CommonHudPresenter Instance => instance;

        public void Configure(
            MatchRuntimeState runtime,
            LocalPlayerRoleSelector selector,
            Text configuredTimerLabel,
            Text configuredRoleLabel,
            Text configuredStateLabel,
            Text configuredInteractionLabel)
        {
            matchRuntime = runtime;
            roleSelector = selector;
            timerLabel = configuredTimerLabel;
            roleLabel = configuredRoleLabel;
            stateLabel = configuredStateLabel;
            interactionLabel = configuredInteractionLabel;
            Refresh();
        }

        public void Refresh()
        {
            if (matchRuntime == null || roleSelector == null)
            {
                return;
            }

            if (timerLabel != null)
            {
                timerLabel.text = FormatTime(
                    matchRuntime.RemainingMatchSeconds);
            }

            if (roleLabel != null)
            {
                roleLabel.text = roleSelector.ActiveRole.ToString().ToUpperInvariant();
            }

            if (stateLabel != null)
            {
                stateLabel.text = FormatState(matchRuntime);
            }

            if (interactionLabel != null)
            {
                PlayerInteractionScanner scanner =
                    roleSelector.ActiveBinding?.InteractionScanner;
                interactionLabel.text =
                    scanner != null && scanner.HasTarget
                        ? $"[E] {scanner.CurrentPrompt}"
                        : string.Empty;
            }
        }

        public static string FormatTime(float remainingSeconds)
        {
            int totalSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(remainingSeconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string FormatState(MatchRuntimeState runtime)
        {
            if (runtime.CurrentState == MatchState.Ready
                && runtime.IsCountdownActive)
            {
                return $"READY  {Mathf.CeilToInt(runtime.ReadyCountdownRemainingSeconds)}";
            }

            return runtime.CurrentState.ToString().ToUpperInvariant();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetInstance()
        {
            instance = null;
        }
    }
}
