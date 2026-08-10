using System;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Companions
{
    /// <summary>
    /// Disables both companions when the match ends.
    ///
    /// A serialized component rather than an editor-time delegate, because a
    /// subscription made while building the scene would not survive
    /// serialization into the .unity file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionMatchEndBridge : MonoBehaviour
    {
        [SerializeField]
        private MatchEndController matchEndController;

        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        private bool _subscribed;

        public void Configure(
            MatchEndController configuredEndController,
            CompanionCommandDispatcher configuredDispatcher)
        {
            Unsubscribe();
            matchEndController = configuredEndController;
            dispatcher = configuredDispatcher;
            ValidateOrThrow();
            Subscribe();
        }

        public void ValidateOrThrow()
        {
            if (matchEndController == null || dispatcher == null)
            {
                throw new InvalidOperationException(
                    $"CompanionMatchEndBridge '{name}' has missing "
                    + "references.");
            }
        }

        private void Subscribe()
        {
            if (_subscribed || matchEndController == null)
            {
                return;
            }

            matchEndController.MatchEndingStarted += HandleMatchEnding;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (matchEndController != null)
            {
                matchEndController.MatchEndingStarted -= HandleMatchEnding;
            }

            _subscribed = false;
        }

        private void HandleMatchEnding(MatchResult _)
        {
            if (dispatcher != null)
            {
                dispatcher.HandleMatchEnded();
            }
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }
    }
}
