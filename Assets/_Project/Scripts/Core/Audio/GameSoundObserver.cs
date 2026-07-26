using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Audio
{
    /// <summary>
    /// AUDIO-002. Subscribes to the rule layer and turns events into sounds.
    ///
    /// The direction matters: gameplay systems know nothing about audio, and
    /// this observer only listens. Deleting it removes every sound and changes
    /// no rule, which is the completion condition for AUDIO-002.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSoundObserver : MonoBehaviour
    {
        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        [SerializeField]
        private LootCarrier thiefCarrier;

        [SerializeField]
        private ThiefLootWallet thiefWallet;

        [SerializeField]
        private ArrestProgressController arrestProgress;

        [SerializeField]
        private ArrestCompletionController arrestCompletion;

        [SerializeField]
        private MatchEndController matchEndController;

        [SerializeField]
        private DistractionBoard distractionBoard;

        private bool _subscribed;
        private bool _wasProgressing;

        public void Configure(
            CompanionCommandDispatcher configuredDispatcher,
            LootCarrier configuredCarrier,
            ThiefLootWallet configuredWallet,
            ArrestProgressController configuredProgress,
            ArrestCompletionController configuredCompletion,
            MatchEndController configuredEndController,
            DistractionBoard configuredBoard)
        {
            Unsubscribe();
            dispatcher = configuredDispatcher;
            thiefCarrier = configuredCarrier;
            thiefWallet = configuredWallet;
            arrestProgress = configuredProgress;
            arrestCompletion = configuredCompletion;
            matchEndController = configuredEndController;
            distractionBoard = configuredBoard;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (dispatcher != null)
            {
                dispatcher.CommandAccepted += HandleCommandAccepted;
                dispatcher.CommandRejected += HandleCommandRejected;
            }

            if (thiefCarrier != null)
            {
                thiefCarrier.HeldLootChanged += HandleHeldLootChanged;
            }

            if (thiefWallet != null)
            {
                thiefWallet.SaleAmountChanged += HandleSaleAmountChanged;
            }

            if (arrestCompletion != null)
            {
                arrestCompletion.PoliceVictoryRequested +=
                    HandleArrestCompleted;
            }

            if (matchEndController != null)
            {
                matchEndController.MatchEndingStarted += HandleMatchEnded;
            }

            if (distractionBoard != null)
            {
                distractionBoard.DistractionStarted +=
                    HandleDistractionStarted;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (dispatcher != null)
            {
                dispatcher.CommandAccepted -= HandleCommandAccepted;
                dispatcher.CommandRejected -= HandleCommandRejected;
            }

            if (thiefCarrier != null)
            {
                thiefCarrier.HeldLootChanged -= HandleHeldLootChanged;
            }

            if (thiefWallet != null)
            {
                thiefWallet.SaleAmountChanged -= HandleSaleAmountChanged;
            }

            if (arrestCompletion != null)
            {
                arrestCompletion.PoliceVictoryRequested -=
                    HandleArrestCompleted;
            }

            if (matchEndController != null)
            {
                matchEndController.MatchEndingStarted -= HandleMatchEnded;
            }

            if (distractionBoard != null)
            {
                distractionBoard.DistractionStarted -=
                    HandleDistractionStarted;
            }

            _subscribed = false;
        }

        private static void HandleCommandAccepted(
            CompanionCommandRequest request)
        {
            GameSoundService.Request(GameSoundId.CommandSucceeded);
            GameSoundService.Request(
                request.CompanionKind == CompanionKind.Dog
                    ? GameSoundId.DogBark
                    : GameSoundId.CatMeow);
        }

        private static void HandleCommandRejected(
            CompanionCommandRequest request,
            CompanionCommandRejection rejection)
        {
            GameSoundService.Request(GameSoundId.CommandFailed);
        }

        private static void HandleHeldLootChanged(
            LootItem previous,
            LootItem current)
        {
            // Only picking up is a pickup sound; releasing is covered by the
            // sale and drop paths.
            if (current != null)
            {
                GameSoundService.Request(GameSoundId.LootAcquired);
            }
        }

        private static void HandleSaleAmountChanged(
            int previousAmount,
            int currentAmount)
        {
            if (currentAmount > previousAmount)
            {
                GameSoundService.Request(GameSoundId.LootSold);
            }
        }

        private static void HandleArrestCompleted()
        {
            GameSoundService.Request(GameSoundId.ArrestCompleted);
        }

        private static void HandleDistractionStarted(Vector3 _)
        {
            GameSoundService.Request(GameSoundId.CatMeow);
        }

        private static void HandleMatchEnded(MatchResult result)
        {
            // Both players hear both sides of the result on one machine, so the
            // local role decides which one plays.
            GameSoundService.Request(
                result.Winner == MatchWinner.Police
                    ? GameSoundId.Victory
                    : GameSoundId.Defeat);
        }

        /// <summary>
        /// Arrest start has no event, so the rising edge of the progress gauge
        /// stands in for it. Polled rather than pushed to avoid adding an event
        /// to the arrest rules purely for audio.
        /// </summary>
        private void Update()
        {
            if (arrestProgress == null)
            {
                return;
            }

            bool progressing = arrestProgress.IsProgressing
                && arrestProgress.ProgressSeconds > 0f;
            if (progressing && !_wasProgressing)
            {
                GameSoundService.Request(GameSoundId.ArrestStarted);
            }

            _wasProgressing = progressing;
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
