using System;
using PawsAndLoot.Config;
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
        }

        public void Tick(float deltaTime)
        {
            if (!CanProgress() || IsReadyToComplete)
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

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
