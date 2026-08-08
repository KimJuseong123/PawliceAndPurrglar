using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
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
                arrestCompletion.ArrestCompleted +=
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
                arrestCompletion.ArrestCompleted -=
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
            // Whose ears, not whose win. The comment here has always said the local
            // role decides — the code never asked it, and played the fanfare whenever
            // the *police* won. So the thief heard a victory sting for losing and a
            // defeat sting for winning, on every single match. `UI-016` fixed exactly
            // this confusion in the result screen's title (winner-based → viewer-based)
            // and the audio was left behind.
            //
            // Same expression the title uses (`ResultScreenPresenter`), so the two
            // cannot disagree.
            GameSoundService.Request(ResolveMatchEndSound(
                result.Winner,
                LocalPlayerRoleSelector.OverriddenRole ?? PlayerRole.Police));
        }

        /// <summary>
        /// Which sting the player at this machine hears. Separated from the event
        /// handler and given the viewer as an argument so a test can check all four
        /// combinations without setting the local role — that role is a static value
        /// and setting it leaks into whatever test runs next (`ISSUE-054`).
        /// </summary>
        public static GameSoundId ResolveMatchEndSound(
            MatchWinner winner,
            PlayerRole viewer)
        {
            bool viewerWon = winner == MatchWinner.Police
                ? viewer == PlayerRole.Police
                : viewer == PlayerRole.Thief;
            return viewerWon ? GameSoundId.Victory : GameSoundId.Defeat;
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
