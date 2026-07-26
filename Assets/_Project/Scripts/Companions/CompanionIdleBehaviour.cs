using System;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// COMP-007. Small idle flourishes so a waiting animal looks alive.
    ///
    /// Deliberately powerless. It only runs while the companion is idle or
    /// following, it never issues a command, never touches the cooldown, never
    /// changes the companion state and never reports a failure. The worst it can
    /// do is play a pose, so it cannot affect the match outcome.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionIdleBehaviour : MonoBehaviour
    {
        public enum IdleAction
        {
            None = 0,

            /// <summary>Dog chases its own tail.</summary>
            ChaseTail = 1,

            /// <summary>Dog trots up to its owner.</summary>
            RunToOwner = 2,

            /// <summary>Cat grooms itself.</summary>
            Groom = 3,

            /// <summary>Cat settles into a box.</summary>
            SitInBox = 4
        }

        [SerializeField]
        private CompanionAgent agent;

        [SerializeField]
        private Transform visual;

        [SerializeField, Min(1f)]
        private float minimumIdleSeconds = 4f;

        [SerializeField, Min(1f)]
        private float maximumIdleSeconds = 9f;

        [SerializeField, Min(0.5f)]
        private float actionSeconds = 2.5f;

        private float _untilNextAction;
        private float _actionRemaining;
        private int _sequence;

        public event Action<IdleAction> ActionStarted;

        public IdleAction CurrentAction { get; private set; }
        public int PlayedCount { get; private set; }

        public void Configure(
            CompanionAgent configuredAgent,
            Transform configuredVisual)
        {
            agent = configuredAgent;
            visual = configuredVisual;
            CurrentAction = IdleAction.None;
            PlayedCount = 0;
            _sequence = 0;
            _actionRemaining = 0f;
            _untilNextAction = minimumIdleSeconds;
        }

        /// <summary>
        /// Idle flourishes are only allowed when nothing important is
        /// happening. Any command, cooldown or disabled state cancels them.
        /// </summary>
        public bool CanPlay()
        {
            if (agent == null)
            {
                return false;
            }

            return agent.IsActive
                && !agent.IsBusyWithCommand
                && agent.CooldownRemainingSeconds <= 0f
                && (agent.CurrentState == CompanionState.Idle
                    || agent.CurrentState == CompanionState.Follow);
        }

        public void Tick(float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            if (!CanPlay())
            {
                Stop();
                return;
            }

            if (_actionRemaining > 0f)
            {
                _actionRemaining -= step;
                ApplyPose();
                if (_actionRemaining <= 0f)
                {
                    Stop();
                }

                return;
            }

            _untilNextAction -= step;
            if (_untilNextAction > 0f)
            {
                return;
            }

            Begin();
        }

        private void Begin()
        {
            CurrentAction = PickAction();
            _actionRemaining = actionSeconds;
            PlayedCount++;
            ActionStarted?.Invoke(CurrentAction);
        }

        private void Stop()
        {
            if (CurrentAction == IdleAction.None)
            {
                return;
            }

            CurrentAction = IdleAction.None;
            _actionRemaining = 0f;
            // Deterministic spacing rather than Random, so a replay of the same
            // inputs behaves the same way.
            _untilNextAction = Mathf.Lerp(
                minimumIdleSeconds,
                Mathf.Max(minimumIdleSeconds, maximumIdleSeconds),
                (_sequence % 4) / 3f);
            _sequence++;
            if (visual != null)
            {
                visual.localRotation = Quaternion.identity;
            }
        }

        private IdleAction PickAction()
        {
            bool isDog = agent != null
                && agent.CompanionKind == CompanionKind.Dog;
            bool second = _sequence % 2 == 1;
            if (isDog)
            {
                return second
                    ? IdleAction.RunToOwner
                    : IdleAction.ChaseTail;
            }

            return second ? IdleAction.SitInBox : IdleAction.Groom;
        }

        /// <summary>
        /// Poses are rotation only on the visual child, so the collider and the
        /// agent transform never move.
        /// </summary>
        private void ApplyPose()
        {
            if (visual == null)
            {
                return;
            }

            float progress = 1f - Mathf.Clamp01(
                _actionRemaining / Mathf.Max(0.01f, actionSeconds));
            switch (CurrentAction)
            {
                case IdleAction.ChaseTail:
                    visual.localRotation = Quaternion.Euler(
                        0f,
                        progress * 720f,
                        0f);
                    break;
                case IdleAction.Groom:
                    visual.localRotation = Quaternion.Euler(
                        Mathf.Sin(progress * Mathf.PI * 6f) * 16f,
                        0f,
                        0f);
                    break;
                case IdleAction.SitInBox:
                    visual.localRotation = Quaternion.Euler(
                        Mathf.Lerp(0f, -12f, progress),
                        0f,
                        0f);
                    break;
                case IdleAction.RunToOwner:
                    visual.localRotation = Quaternion.Euler(
                        0f,
                        Mathf.Sin(progress * Mathf.PI * 4f) * 20f,
                        0f);
                    break;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
