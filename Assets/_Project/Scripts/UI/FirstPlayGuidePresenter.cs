using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// UX-001. Teaches the first match without a separate tutorial.
    ///
    /// Shows one instruction at a time, chosen from live state, and stops once
    /// the player has clearly done it. No scripted sequence and no modal step,
    /// so a player who already knows the game is never blocked.
    ///
    /// UX-002 applies here too: every line pairs an icon glyph with text so the
    /// meaning does not depend on colour alone.
    /// </summary>
    public sealed class FirstPlayGuidePresenter : MonoBehaviour
    {
        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private LootCarrier thiefCarrier;

        [SerializeField]
        private ThiefLootWallet thiefWallet;

        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        [SerializeField]
        private Text guideLabel;

        /// <summary>
        /// The guide retires after this long so it never clutters a match the
        /// player already understands.
        /// </summary>
        [SerializeField, Min(5f)]
        private float visibleSeconds = 45f;

        private float _elapsedSeconds;
        private bool _retired;

        public string GuideText =>
            guideLabel != null ? guideLabel.text : string.Empty;
        public bool IsRetired => _retired;

        public void Configure(
            LocalPlayerRoleSelector configuredRoleSelector,
            MatchRuntimeState configuredRuntime,
            LootCarrier configuredCarrier,
            ThiefLootWallet configuredWallet,
            CompanionCommandDispatcher configuredDispatcher,
            Text configuredLabel)
        {
            roleSelector = configuredRoleSelector;
            matchRuntime = configuredRuntime;
            thiefCarrier = configuredCarrier;
            thiefWallet = configuredWallet;
            dispatcher = configuredDispatcher;
            guideLabel = configuredLabel;
            _elapsedSeconds = 0f;
            _retired = false;
            Refresh(0f);
        }

        /// <summary>
        /// Picks the next thing worth saying. Ordered so the most immediate
        /// need wins, which is what keeps it to one line.
        /// </summary>
        public string ResolveGuide()
        {
            if (matchRuntime == null || roleSelector == null)
            {
                return string.Empty;
            }

            if (matchRuntime.CurrentState == MatchState.Ready)
            {
                return "⏳  곧 시작합니다";
            }

            if (!matchRuntime.IsGameplayActive)
            {
                return string.Empty;
            }

            bool isPolice = roleSelector.ActiveRole == PlayerRole.Police;

            // Movement first: nothing else is possible without it.
            if (_elapsedSeconds < 6f)
            {
                return "🎮  WASD 이동 · Space 대시";
            }

            // Then the one command this role has, until it is used once.
            if (dispatcher != null && dispatcher.AcceptedCount == 0)
            {
                return isPolice
                    ? "🐕  1 키로 강아지에게 추적을 명령"
                    : "🐈  2 키로 고양이에게 교란을 명령";
            }

            if (isPolice)
            {
                return "👮  도둑에게 붙어 1.5초 버티면 체포";
            }

            if (thiefCarrier != null && !thiefCarrier.HasLoot)
            {
                return "💎  E 키로 보물을 획득";
            }

            if (thiefWallet != null && thiefCarrier != null
                && thiefCarrier.HasLoot)
            {
                return "💰  너구리 장터에서 E 키로 판매";
            }

            return string.Empty;
        }

        public void Refresh(float deltaTime)
        {
            if (guideLabel == null)
            {
                return;
            }

            if (_retired)
            {
                guideLabel.text = string.Empty;
                return;
            }

            if (matchRuntime != null && matchRuntime.IsGameplayActive)
            {
                _elapsedSeconds += Mathf.Max(0f, deltaTime);
            }

            if (_elapsedSeconds >= visibleSeconds)
            {
                _retired = true;
                guideLabel.text = string.Empty;
                return;
            }

            guideLabel.text = ResolveGuide();
        }

        private void Update()
        {
            Refresh(Time.deltaTime);
        }
    }
}
