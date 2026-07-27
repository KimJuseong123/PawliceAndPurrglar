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
        public bool IsCompleted { get; private set; }

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
            IsCompleted = false;
            ValidateOrThrow();
            SubscribeToSensor();
        }

        /// <summary>
        /// True when the host owns the arrest judgement for this match.
        /// </summary>
        public bool IsRemoteControlled { get; private set; }

        public void SetRemoteControlled(bool remoteControlled)
        {
            IsRemoteControlled = remoteControlled;
        }

        /// <summary>
        /// NET-007. Adopts the host's arrest progress.
        ///
        /// Distance and interruption are judged only where the simulation runs,
        /// so latency cannot make one machine complete an arrest the other
        /// rejects. A completed arrest is one-way here for the same reason
        /// <see cref="TryMarkCompleted"/> is: it must not be undone by a
        /// late packet.
        /// </summary>
        public void ApplyRemoteProgress(
            float progressSeconds,
            bool completed)
        {
            ProgressSeconds = arrestConfig != null
                ? Mathf.Clamp(
                    progressSeconds,
                    0f,
                    arrestConfig.ArrestDurationSeconds)
                : Mathf.Max(0f, progressSeconds);
            if (completed)
            {
                IsCompleted = true;
            }
        }

        public void Tick(float deltaTime)
        {
            if (IsCompleted || IsRemoteControlled)
            {
                return;
            }

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
            IsCompleted = false;
        }

        public void HandleMatchEnded()
        {
            Interrupt(ArrestInterruptionReason.MatchNotPlaying);
        }

        public bool TryMarkCompleted()
        {
            if (IsCompleted
                || !IsReadyToComplete
                || !CanProgress())
            {
                return false;
            }

            IsCompleted = true;
            return true;
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
            // A non-authority machine must not zero progress on its own: its
            // sensor can lag the host's by a packet, and clearing the bar here
            // would fight the replicated value. The host replicates both the
            // reset and the reason instead.
            if (IsCompleted || IsRemoteControlled || ProgressSeconds <= 0f)
            {
                return;
            }

            ProgressSeconds = 0f;
            InterruptionCount++;
            LastInterruptionReason = reason;
            ProgressInterrupted?.Invoke(reason);
        }

        /// <summary>
        /// Counts interruptions so the replicated view can tell a fresh
        /// interruption from a value that merely happens to be zero again.
        /// </summary>
        public int InterruptionCount { get; private set; }
        public ArrestInterruptionReason LastInterruptionReason
        {
            get;
            private set;
        }

        /// <summary>
        /// Replays one host interruption locally so the client's bar and sound
        /// react the same way the host's did.
        /// </summary>
        public void ApplyRemoteInterruption(
            int interruptionCount,
            ArrestInterruptionReason reason)
        {
            if (interruptionCount <= InterruptionCount)
            {
                return;
            }

            InterruptionCount = interruptionCount;
            LastInterruptionReason = reason;
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
