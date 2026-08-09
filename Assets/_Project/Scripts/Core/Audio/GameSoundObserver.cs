using System.Collections.Generic;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Items;
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

        /// <summary>
        /// The sources found in the scene rather than handed over by the builder.
        ///
        /// Everything above this line is a <c>[SerializeField]</c> filled in by
        /// <c>GreyboxMapSetup</c>. Nothing below it is, for two reasons and the
        /// second is the one that decided it:
        ///
        /// <list type="number">
        /// <item>A list an editor script fills is not saved with the scene. That
        /// has already killed the lobby buttons, the leg animator and the sensor
        /// arcs, and it fails without a log line.</item>
        /// <item>Adding a serialised field here means regenerating
        /// <c>Game.unity</c> to populate it, which rewrites the
        /// <c>GlobalObjectIdHash</c> of all 130 in-scene NetworkObjects and makes
        /// every existing build incompatible with every new one. Sound is not
        /// worth that.</item>
        /// </list>
        ///
        /// Found once in <see cref="Start"/>, then retried slowly while empty —
        /// the match objects are placed in the scene, so one pass normally does
        /// it, and scanning every frame for something that already exists is the
        /// cost this codebase has been bitten by before.
        /// </summary>
        private readonly List<PlayerInteriorState> _interiorStates = new();
        private readonly List<PlayerMovementMotor> _motors = new();
        private readonly List<bool> _wasAirborne = new();
        private readonly List<PoliceSupplyCounter> _counters = new();
        private readonly List<CompanionLootCourier> _couriers = new();
        private MatchRuntimeState _matchState;
        private bool _foundSources;
        private float _nextSourceScan;
        private bool _wasCountingDown;

        /// <summary>
        /// Set by the shelf path immediately before the total changes, so the
        /// handler that hears the total can tell a pocketed trinket from a sale.
        /// Cleared by that handler, never left standing.
        /// </summary>
        private bool _lastRiseWasPocketed;

        /// <summary>
        /// How long to wait before looking for the match objects again.
        ///
        /// Only used while none have been found. A scene that has them finds them
        /// on the first pass and never scans again.
        /// </summary>
        private const float SourceScanInterval = 1f;

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
                thiefWallet.CashPocketed += HandleCashPocketed;
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
                thiefWallet.CashPocketed -= HandleCashPocketed;
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

        private void HandleCashPocketed(int _)
        {
            _lastRiseWasPocketed = true;
        }

        private void HandleSaleAmountChanged(
            int previousAmount,
            int currentAmount)
        {
            bool pocketed = _lastRiseWasPocketed;

            // Cleared whether or not the total went up, so a refused credit
            // cannot leave the flag set for whatever raises the total next.
            _lastRiseWasPocketed = false;

            if (currentAmount <= previousAmount)
            {
                return;
            }

            // A trinket off a shelf sounds like picking something up; a treasure
            // handed to the merchant sounds like being paid. The amount cannot
            // tell them apart, which is why the wallet says which it was.
            GameSoundService.Request(
                pocketed ? GameSoundId.LootAcquired : GameSoundId.LootSold);
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
        /// Finds the match objects this cannot be handed and subscribes to them.
        ///
        /// Returns whether anything was found, so the caller can decide whether
        /// to look again. "Nothing" is the honest answer in an editor scene with
        /// no players in it, and it must not turn into a scan every frame.
        /// </summary>
        private bool FindSources()
        {
            ReleaseSources();

            foreach (PlayerInteriorState state in
                FindObjectsByType<PlayerInteriorState>(
                    FindObjectsSortMode.None))
            {
                _interiorStates.Add(state);
                state.InteriorChanged += HandleInteriorChanged;
            }

            foreach (PlayerMovementMotor motor in
                FindObjectsByType<PlayerMovementMotor>(
                    FindObjectsSortMode.None))
            {
                _motors.Add(motor);
                _wasAirborne.Add(motor.IsAirborne);
            }

            foreach (PoliceSupplyCounter counter in
                FindObjectsByType<PoliceSupplyCounter>(
                    FindObjectsSortMode.None))
            {
                _counters.Add(counter);
                counter.Purchased += HandlePurchased;
            }

            foreach (CompanionLootCourier courier in
                FindObjectsByType<CompanionLootCourier>(
                    FindObjectsSortMode.None))
            {
                _couriers.Add(courier);
                courier.LootDelivered += HandleLootDelivered;
            }

            _matchState = FindFirstObjectByType<MatchRuntimeState>();
            _wasCountingDown =
                _matchState != null && _matchState.IsCountdownActive;

            return _interiorStates.Count > 0
                || _motors.Count > 0
                || _counters.Count > 0
                || _couriers.Count > 0
                || _matchState != null;
        }

        private void ReleaseSources()
        {
            foreach (PlayerInteriorState state in _interiorStates)
            {
                if (state != null)
                {
                    state.InteriorChanged -= HandleInteriorChanged;
                }
            }

            foreach (PoliceSupplyCounter counter in _counters)
            {
                if (counter != null)
                {
                    counter.Purchased -= HandlePurchased;
                }
            }

            foreach (CompanionLootCourier courier in _couriers)
            {
                if (courier != null)
                {
                    courier.LootDelivered -= HandleLootDelivered;
                }
            }

            _interiorStates.Clear();
            _motors.Clear();
            _wasAirborne.Clear();
            _counters.Clear();
            _couriers.Clear();
            _matchState = null;
        }

        /// <summary>
        /// Going in and coming out are the same door, so they are the same sound.
        ///
        /// Raised from the state rather than from <c>HouseDoorway</c> because the
        /// doorway only runs on the host: a client's own character is moved by
        /// replication, and hanging the sound off the decision would have left
        /// one of the two players opening silent doors.
        /// </summary>
        private static void HandleInteriorChanged(int _)
        {
            GameSoundService.Request(GameSoundId.DoorOpen);
        }

        private static void HandlePurchased(ThrowableKind _)
        {
            GameSoundService.Request(GameSoundId.PurchaseMade);
        }

        /// <summary>
        /// The cat handing over what it fetched.
        ///
        /// Only the delivery. Picking up already goes through the thief's own
        /// carrier, so it raises <see cref="GameSoundId.LootAcquired"/> — a meow
        /// on top of that would be two sounds in one frame, which is the thing
        /// the acquisition sheet warns about for the companion icons.
        /// </summary>
        private static void HandleLootDelivered(LootItem _)
        {
            GameSoundService.Request(GameSoundId.CatMeow);
        }

        /// <summary>
        /// The three things with no event of their own, on the rising edge.
        ///
        /// Arrest start was already here. Jump and the countdown joined it for
        /// the same reason: neither is worth an event added to the rules purely
        /// so that something can be heard.
        /// </summary>
        private void Update()
        {
            if (!_foundSources && Time.unscaledTime >= _nextSourceScan)
            {
                _nextSourceScan = Time.unscaledTime + SourceScanInterval;
                _foundSources = FindSources();
            }

            UpdateArrestSound();
            UpdateJumpSound();
            UpdateCountdownSound();
        }

        private void UpdateArrestSound()
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

        /// <summary>
        /// Leaving the ground, per character.
        ///
        /// The edge is kept per motor rather than as one flag: both characters
        /// are simulated on the host, and a single flag would swallow the second
        /// jump whenever the other player was already in the air.
        /// </summary>
        private void UpdateJumpSound()
        {
            for (int index = 0; index < _motors.Count; index++)
            {
                PlayerMovementMotor motor = _motors[index];
                if (motor == null)
                {
                    continue;
                }

                bool airborne = motor.IsAirborne;
                if (airborne && !_wasAirborne[index])
                {
                    GameSoundService.Request(GameSoundId.Jump);
                }

                _wasAirborne[index] = airborne;
            }
        }

        /// <summary>
        /// Once, when the count starts — not once per second.
        ///
        /// The recording is a whole three-second countdown rather than a single
        /// beep, so a tick per second would be three copies of the same count
        /// playing a second apart.
        /// </summary>
        private void UpdateCountdownSound()
        {
            if (_matchState == null)
            {
                return;
            }

            bool counting = _matchState.IsCountdownActive;
            if (counting && !_wasCountingDown)
            {
                GameSoundService.Request(GameSoundId.CountdownTick);
            }

            _wasCountingDown = counting;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseSources();
            _foundSources = false;
            _nextSourceScan = 0f;
        }
    }
}
