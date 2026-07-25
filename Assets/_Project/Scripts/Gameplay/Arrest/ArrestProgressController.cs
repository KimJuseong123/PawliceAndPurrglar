using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Arrest
{
    public sealed class ArrestProgressController : MonoBehaviour
    {
        [SerializeField]
        private ArrestRangeSensor rangeSensor;

        [SerializeField]
        private ArrestConfig arrestConfig;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;
        private bool _subscribedToSensor;

        public event Action<ArrestInterruptionReason> ProgressInterrupted;

        public float ProgressSeconds { get; private set; }
        public float ProgressNormalized =>
            arrestConfig == null
                ? 0f
                : Mathf.Clamp01(
                    ProgressSeconds
                    / arrestConfig.ArrestDurationSeconds);
        public bool IsProgressing =>
            CanProgress() && ProgressNormalized < 1f;
        public bool IsReadyToComplete =>
            ProgressNormalized >= 1f;

        public void Configure(
            ArrestRangeSensor configuredRangeSensor,
            IMatchStateReader configuredMatchState,
            ArrestConfig configuredArrestConfig)
        {
            rangeSensor = configuredRangeSensor;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            arrestConfig = configuredArrestConfig;
            ProgressSeconds = 0f;
            ValidateOrThrow();
            SubscribeToSensor();
        }

        public void Tick(float deltaTime)
        {
            if (TryGetInterruptionReason(
                    out ArrestInterruptionReason reason))
            {
                Interrupt(reason);
                return;
            }

            if (IsReadyToComplete)
            {
                return;
            }

            ProgressSeconds = Mathf.Min(
                arrestConfig.ArrestDurationSeconds,
                ProgressSeconds + Mathf.Max(0f, deltaTime));
        }

        public void ResetProgress()
        {
            ProgressSeconds = 0f;
        }

        public void ValidateOrThrow()
        {
            if (rangeSensor == null
                || arrestConfig == null
                || ResolveMatchState() == null)
            {
                throw new InvalidOperationException(
                    $"ArrestProgressController '{name}' has missing references.");
            }

            arrestConfig.ValidateOrThrow();
        }

        private bool CanProgress()
        {
            return rangeSensor != null
                && rangeSensor.IsTargetDetected
                && ResolveMatchState()?.IsGameplayActive == true;
        }

        private bool TryGetInterruptionReason(
            out ArrestInterruptionReason reason)
        {
            IMatchStateReader matchState = ResolveMatchState();
            if (matchState?.IsGameplayActive != true)
            {
                reason = ArrestInterruptionReason.MatchNotPlaying;
                return true;
            }

            if (rangeSensor == null
                || rangeSensor.Police == null
                || rangeSensor.Thief == null
                || !rangeSensor.Police.isActiveAndEnabled
                || !rangeSensor.Thief.isActiveAndEnabled)
            {
                reason =
                    ArrestInterruptionReason.ParticipantUnavailable;
                return true;
            }

            if (!rangeSensor.IsTargetDetected)
            {
                reason =
                    ArrestInterruptionReason.TargetNoLongerDetectable;
                return true;
            }

            reason = default;
            return false;
        }

        private void Interrupt(ArrestInterruptionReason reason)
        {
            if (ProgressSeconds <= 0f)
            {
                return;
            }

            ProgressSeconds = 0f;
            ProgressInterrupted?.Invoke(reason);
        }

        private void HandleTargetExited(PlayerRoleIdentity _)
        {
            if (TryGetInterruptionReason(
                    out ArrestInterruptionReason reason))
            {
                Interrupt(reason);
            }
        }

        private void SubscribeToSensor()
        {
            if (_subscribedToSensor || rangeSensor == null)
            {
                return;
            }

            rangeSensor.TargetExited += HandleTargetExited;
            _subscribedToSensor = true;
        }

        private void UnsubscribeFromSensor()
        {
            if (!_subscribedToSensor || rangeSensor == null)
            {
                return;
            }

            rangeSensor.TargetExited -= HandleTargetExited;
            _subscribedToSensor = false;
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            return _matchState;
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void OnEnable()
        {
            SubscribeToSensor();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            UnsubscribeFromSensor();
            Interrupt(
                ArrestInterruptionReason.ParticipantUnavailable);
        }
    }
}
