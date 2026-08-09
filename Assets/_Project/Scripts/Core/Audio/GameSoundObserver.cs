using System;
using System.Collections.Generic;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Gameplay.Sensing;
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
        private readonly List<Action<int>> _interiorHandlers = new();
        private readonly List<CompanionLootCourier> _couriers = new();
        private MatchRuntimeState _matchState;
        private bool _foundSources;
        private bool _subscribedToPurchases;
        private float _nextSourceScan;
        private bool _wasCountingDown;

        // C-1 to C-3. Found the same way and for the same reasons.
        //
        // The placed props are not here: they are made during a match, so a scan
        // at load would never see them. Their sounds are raised by
        // `NetworkItemCoordinator`, which is the one place that runs on both
        // machines.
        private readonly List<ToolUseAction> _toolUses = new();
        private readonly List<ThrowChargeController> _chargers = new();
        private readonly List<bool> _wasCharging = new();
        private readonly List<ThrowFlightTracker> _flights = new();
        private readonly List<StunState> _stuns = new();
        private readonly List<bool> _wasStunned = new();
        private readonly List<BlindedState> _blinds = new();
        private readonly List<CompanionLure> _lures = new();
        private readonly List<LootPickupProgress> _pickups = new();
        private readonly List<LootDisplayCase> _cases = new();
        private readonly List<CompanionNoiseAttention> _attentions = new();
        private readonly List<int> _lastInvestigated = new();

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
                // Per state, so the handler knows *whose* door it was. The
                // event carries the room id and nothing else, and subscribing
                // one shared method to both players meant a door anywhere on
                // the map sounded here.
                PlayerInteriorState captured = state;
                Action<int> handler = _ => HandleInteriorChanged(captured);
                _interiorStates.Add(state);
                _interiorHandlers.Add(handler);
                state.InteriorChanged += handler;
            }

            // Not per counter any more. The three supermarket counters were
            // removed on 2026-08-09 and the loop that was here then found
            // nothing, which took the purchase sound with them without a word —
            // the raccoon's stall rings up the sale through
            // `PoliceSupplyCatalogue` instead.
            //
            // Guarded because this scan retries while it comes up empty, and a
            // static event does not forget a duplicate subscription the way a
            // list of found counters did: without the flag a slow scene start
            // would play the purchase sound once per retry.
            if (!_subscribedToPurchases)
            {
                _subscribedToPurchases = true;
                PoliceSupplyCatalogue.Purchased += HandlePurchased;
            }

            foreach (CompanionLootCourier courier in
                FindObjectsByType<CompanionLootCourier>(
                    FindObjectsSortMode.None))
            {
                _couriers.Add(courier);
                courier.LootDelivered += HandleLootDelivered;
            }

            foreach (ToolUseAction tool in
                FindObjectsByType<ToolUseAction>(FindObjectsSortMode.None))
            {
                _toolUses.Add(tool);
                tool.Thrown += HandleThrown;
            }

            foreach (ThrowChargeController charger in
                FindObjectsByType<ThrowChargeController>(
                    FindObjectsSortMode.None))
            {
                _chargers.Add(charger);
                _wasCharging.Add(charger.IsCharging);
            }

            foreach (ThrowFlightTracker flight in
                FindObjectsByType<ThrowFlightTracker>(
                    FindObjectsSortMode.None))
            {
                _flights.Add(flight);
                flight.Hit += HandleThrowHit;
            }

            // Polled rather than subscribed. `StunState.Stunned` carries the
            // duration and not the cause, and the cause is the whole question
            // here — so the handler would have to close over which state raised
            // it, and a closure cannot be unsubscribed.
            foreach (StunState stun in
                FindObjectsByType<StunState>(FindObjectsSortMode.None))
            {
                _stuns.Add(stun);
                _wasStunned.Add(stun.IsStunned);
            }

            foreach (BlindedState blind in
                FindObjectsByType<BlindedState>(FindObjectsSortMode.None))
            {
                _blinds.Add(blind);
                blind.Blinded += HandleBlinded;
            }

            foreach (CompanionLure lure in
                FindObjectsByType<CompanionLure>(FindObjectsSortMode.None))
            {
                _lures.Add(lure);
                lure.LureStarted += HandleLureStarted;
            }

            foreach (LootPickupProgress pickup in
                FindObjectsByType<LootPickupProgress>(
                    FindObjectsSortMode.None))
            {
                _pickups.Add(pickup);
                pickup.Started += HandlePickupStarted;
            }

            foreach (LootDisplayCase displayCase in
                FindObjectsByType<LootDisplayCase>(FindObjectsSortMode.None))
            {
                _cases.Add(displayCase);
                displayCase.Broken += HandleCaseOpened;
            }

            foreach (CompanionNoiseAttention attention in
                FindObjectsByType<CompanionNoiseAttention>(
                    FindObjectsSortMode.None))
            {
                _attentions.Add(attention);
                _lastInvestigated.Add(attention.InvestigatedCount);
            }

            _matchState = FindFirstObjectByType<MatchRuntimeState>();
            _wasCountingDown =
                _matchState != null && _matchState.IsCountdownActive;

            return _interiorStates.Count > 0
                || _couriers.Count > 0
                || _toolUses.Count > 0
                || _cases.Count > 0
                || _matchState != null;
        }

        private void ReleaseSources()
        {
            for (int index = 0; index < _interiorStates.Count; index++)
            {
                PlayerInteriorState state = _interiorStates[index];
                if (state != null && index < _interiorHandlers.Count)
                {
                    state.InteriorChanged -= _interiorHandlers[index];
                }
            }

            _interiorHandlers.Clear();

            if (_subscribedToPurchases)
            {
                _subscribedToPurchases = false;
                PoliceSupplyCatalogue.Purchased -= HandlePurchased;
            }

            foreach (CompanionLootCourier courier in _couriers)
            {
                if (courier != null)
                {
                    courier.LootDelivered -= HandleLootDelivered;
                }
            }

            foreach (ToolUseAction tool in _toolUses)
            {
                if (tool != null)
                {
                    tool.Thrown -= HandleThrown;
                }
            }

            foreach (ThrowFlightTracker flight in _flights)
            {
                if (flight != null)
                {
                    flight.Hit -= HandleThrowHit;
                }
            }

            foreach (BlindedState blind in _blinds)
            {
                if (blind != null)
                {
                    blind.Blinded -= HandleBlinded;
                }
            }

            foreach (CompanionLure lure in _lures)
            {
                if (lure != null)
                {
                    lure.LureStarted -= HandleLureStarted;
                }
            }

            foreach (LootPickupProgress pickup in _pickups)
            {
                if (pickup != null)
                {
                    pickup.Started -= HandlePickupStarted;
                }
            }

            foreach (LootDisplayCase displayCase in _cases)
            {
                if (displayCase != null)
                {
                    displayCase.Broken -= HandleCaseOpened;
                }
            }

            _interiorStates.Clear();
            _interiorHandlers.Clear();
            _couriers.Clear();
            _toolUses.Clear();
            _chargers.Clear();
            _wasCharging.Clear();
            _flights.Clear();
            _stuns.Clear();
            _wasStunned.Clear();
            _blinds.Clear();
            _lures.Clear();
            _pickups.Clear();
            _cases.Clear();
            _attentions.Clear();
            _lastInvestigated.Clear();
            _matchState = null;
        }

        private static void HandleThrown(
            ThrowableKind kind,
            ThrowResolver.Result _)
        {
            GameSoundService.Request(GameSoundId.ThrowReleased);
        }

        private static void HandleThrowHit(
            ThrowableKind kind,
            PlayerRoleIdentity thrower,
            PlayerRoleIdentity victim)
        {
            GameSoundService.Request(GameSoundId.ThrowHitBody);
        }

        private static void HandleBlinded(float _)
        {
            GameSoundService.Request(GameSoundId.Blinded);
        }

        private static void HandleLureStarted(Vector3 _)
        {
            GameSoundService.Request(GameSoundId.LureTaken);
        }

        /// <summary>
        /// A theft, heard from wherever this machine is standing.
        ///
        /// The officer is meant to know a robbery is happening without being
        /// told which one. A flat sound says the same thing from anywhere on
        /// the map, which makes that information free; volume is what the thief
        /// pays for choosing a house across town.
        ///
        /// Addressed through <see cref="InteriorAddress"/> first, because most
        /// of this game's loot is indoors and interiors are rooms parked off the
        /// edge of the map. Measured raw, every indoor theft is a hundred metres
        /// away and therefore silent — and silence is exactly what this sound
        /// looked like before it existed, so nothing would say it had broken.
        /// </summary>
        private static void HandlePickupStarted(LootItem item)
        {
            GameSoundService.RequestAt(
                GameSoundId.LootPickupStart,
                AddressOf(item));
        }

        /// <summary>
        /// The town coordinate of a piece of loot, or the listener's own
        /// position when there is no loot to ask — an unplaceable sound is
        /// played at full volume rather than dropped.
        /// </summary>
        private static Vector3 AddressOf(Component source)
        {
            return source == null
                ? Vector3.zero
                : Gameplay.Interiors.InteriorAddress.TownPositionOf(
                    source.transform.position);
        }

        /// <summary>
        /// A case opened, one way or the other.
        ///
        /// One event covers both routes, so the case is asked which it was.
        /// <c>OpenedQuietly</c> is set before the event goes out, which is what
        /// makes reading it here safe.
        /// </summary>
        private static void HandleCaseOpened(LootDisplayCase displayCase)
        {
            if (displayCase == null)
            {
                return;
            }

            GameSoundService.RequestAt(
                displayCase.OpenedQuietly
                    ? GameSoundId.CaseKeyUnlock
                    : GameSoundId.GlassBreak,
                AddressOf(displayCase));
        }

        /// <summary>
        /// Going in and coming out are the same door, so they are the same sound.
        ///
        /// Raised from the state rather than from <c>HouseDoorway</c> because the
        /// doorway only runs on the host: a client's own character is moved by
        /// replication, and hanging the sound off the decision would have left
        /// one of the two players opening silent doors.
        ///
        /// Only for the character at this keyboard. Both role objects exist on
        /// both machines, so this used to sound for the other player's doors as
        /// well — and since every sound in this game is 2D, at full volume from
        /// anywhere on the map. With two windows open on one machine that is one
        /// door heard twice, a moment apart (`ISSUE-074`).
        /// </summary>
        private static void HandleInteriorChanged(PlayerInteriorState state)
        {
            if (state == null
                || !LocalPlayerRoleSelector.TryResolveLocalRole(
                    out PlayerRole local))
            {
                // No local role yet means no session — a focused test, or the
                // first frames of a match. Sounding it is the old behaviour and
                // is right for the single-player case, where the only character
                // with a door is this one.
                GameSoundService.Request(GameSoundId.DoorOpen);
                return;
            }

            var identity = state.GetComponent<PlayerRoleIdentity>();
            if (identity == null || identity.Role == local)
            {
                GameSoundService.Request(GameSoundId.DoorOpen);
            }
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
            UpdateCountdownSound();
            UpdateThrowChargeSound();
            UpdateStunSound();
            UpdateCompanionAlertSound();
        }

        /// <summary>
        /// Winding up to throw. No event exists and one is not worth adding.
        ///
        /// This machine's own wind-up only. Both role objects exist on both
        /// machines, so it used to sound for the opponent's too — and since
        /// every sound here is 2D, that told a player their opponent was
        /// drawing back an arm from anywhere on the map. Not merely one sound
        /// too many: it is the wind-up that a throw can be dodged during, so
        /// hearing it is the whole of the counterplay (`ISSUE-074`).
        /// </summary>
        private void UpdateThrowChargeSound()
        {
            for (int index = 0; index < _chargers.Count; index++)
            {
                ThrowChargeController charger = _chargers[index];
                if (charger == null)
                {
                    continue;
                }

                bool charging = charger.IsCharging;
                if (charging
                    && !_wasCharging[index]
                    && BelongsToLocalPlayer(charger))
                {
                    GameSoundService.Request(GameSoundId.ThrowCharge);
                }

                _wasCharging[index] = charging;
            }
        }

        /// <summary>
        /// Whether a component sitting on a player belongs to the one at this
        /// keyboard.
        ///
        /// A component with no identity above it, or a scene with no local role
        /// yet, answers true: that is a focused test or a single-player scene,
        /// where the only character there is is this one. The question being
        /// asked is "is this somebody else's", and "there is nobody else" is a
        /// no.
        /// </summary>
        private static bool BelongsToLocalPlayer(Component component)
        {
            if (component == null)
            {
                return false;
            }

            var identity = component.GetComponentInParent<PlayerRoleIdentity>();
            if (identity == null)
            {
                return true;
            }

            return !LocalPlayerRoleSelector.TryResolveLocalRole(
                    out PlayerRole local)
                || identity.Role == local;
        }

        /// <summary>
        /// Seeing stars, and only for a rock.
        ///
        /// A banana and a glue trap are stuns too, and both already have a sound
        /// of their own that says what happened. Playing this on top of them
        /// would be two sounds for one event, where the first one has already
        /// told the player everything — so the cause is checked rather than the
        /// stun.
        /// </summary>
        private void UpdateStunSound()
        {
            for (int index = 0; index < _stuns.Count; index++)
            {
                StunState stun = _stuns[index];
                if (stun == null)
                {
                    continue;
                }

                bool stunned = stun.IsStunned;
                if (stunned
                    && !_wasStunned[index]
                    && stun.Cause == StunCause.Impact)
                {
                    GameSoundService.Request(GameSoundId.Stunned);
                }

                _wasStunned[index] = stunned;
            }
        }

        /// <summary>
        /// An animal noticing a noise, which is not an animal being given an
        /// order — so this is deliberately not the bark and meow that answer a
        /// command.
        ///
        /// Counted rather than subscribed: the attention component reports how
        /// many noises it has gone to look at and raises nothing.
        /// </summary>
        private void UpdateCompanionAlertSound()
        {
            for (int index = 0; index < _attentions.Count; index++)
            {
                CompanionNoiseAttention attention = _attentions[index];
                if (attention == null)
                {
                    continue;
                }

                int investigated = attention.InvestigatedCount;
                if (investigated > _lastInvestigated[index])
                {
                    CompanionAgent agent =
                        attention.GetComponent<CompanionAgent>();

                    // Only this player's animal. An animal that has noticed
                    // something is reporting to its own owner, and hearing the
                    // opponent's dog perk up says their dog found a trail —
                    // which is a thing the game otherwise takes care to keep
                    // on one screen (`ISSUE-074`).
                    if (IsLocalPlayersCompanion(agent))
                    {
                        GameSoundService.Request(
                            agent != null
                                && agent.CompanionKind == CompanionKind.Dog
                                ? GameSoundId.DogAlerted
                                : GameSoundId.CatAlerted);
                    }
                }

                _lastInvestigated[index] = investigated;
            }
        }

        /// <summary>
        /// An animal belongs to whoever it follows.
        ///
        /// Asked of the owner transform rather than of the kind. Dog-is-police
        /// and cat-is-thief is true today and is written down in several
        /// places, but it is a fact about the current cast rather than a rule,
        /// and an animal that changed hands would take its sound to the wrong
        /// screen without anything saying so.
        /// </summary>
        private static bool IsLocalPlayersCompanion(CompanionAgent agent)
        {
            if (agent == null || agent.Owner == null)
            {
                return true;
            }

            return BelongsToLocalPlayer(agent.Owner);
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

        // The jump sound used to be raised here, on the frame `IsAirborne`
        // went true for any motor. Two things were wrong with that and both
        // made it fire far more often than anybody jumped: leaving the ground
        // is not jumping (a kerb, the park steps, any slope), and the host
        // simulates both characters, so the officer heard the thief's kerbs
        // from anywhere on the map. It moved to the two places this machine's
        // space bar is read — `PlayerKeyboardInput` and `NetworkInputBridge`
        // (`ISSUE-072`).

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
