using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.Logging;
using PawsAndLoot.Companions;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    /// <summary>
    /// Verifies NET-003 to NET-010 in the match scene without a human watching
    /// two windows.
    ///
    /// Activated by the same <c>-netLobby</c> run once the match scene has
    /// loaded. Each process records the match clock, both player positions, the
    /// loot, the score and the arrest bar, so comparing the two files shows
    /// whether the screens agree.
    ///
    /// The scenario runs on a fixed timeline rather than reacting to the world,
    /// so both processes reach the same step at the same moment and a comparison
    /// is meaningful. Steps that place a character are host-only and are test
    /// setup, not gameplay: navigation is MAP-001's subject, while what is under
    /// test here is that a client's request reaches the host and the host's
    /// result reaches the client.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkMatchProbe : MonoBehaviour
    {
        private const string ModeArgument = "-netLobby";
        private const string SampleArgument = "-netMatchSeconds";
        private const string ScenarioArgument = "-netScenario";

        /// <summary>
        /// Timeline in seconds. Generous windows: a real two-process run has to
        /// absorb a scene load, a spawn handshake and replication latency.
        /// </summary>
        private const float MoveUntil = 4f;
        private const float PlaceForPickupAt = 4f;
        private const float MashPickupFrom = 5f;
        private const float MashPickupUntil = 7f;
        private const float PlaceForSaleAt = 7.5f;
        private const float MashSaleFrom = 8.5f;
        private const float MashSaleUntil = 10.5f;

        /// <summary>
        /// THROW-007. The officer throws a rock at the thief.
        ///
        /// Here because the stun's whole route — a client's key, a request to the
        /// host, the host's own hit test, the result replicated back — had no
        /// two-process coverage at all. It was reported as "I threw and nothing
        /// happened", and the local tests could not have found that.
        ///
        /// Before the arrest window and long enough before it that a 1.2 s stun
        /// has expired by the time the arrest is measured.
        /// </summary>
        private const float ThrowAt = 9f;

        /// <summary>
        /// THROW-009. The host puts a trap on the ground.
        ///
        /// Here because the placed-trap message was four bytes short of its
        /// payload and threw an overflow on the first placement ever made. The
        /// throw leg does not cover it — throwing sends a different message — and
        /// no trap had been placed in a session before the police got props.
        ///
        /// Dropped well away from both characters: what is under test is that the
        /// message crosses and both machines end up with the same trap, not that
        /// it catches anybody.
        /// </summary>
        private const float PlaceTrapAt = 9.6f;

        /// <summary>
        /// THROW-011. The officer buys a prop at the shop.
        ///
        /// The purse is replicated and the purchase is decided by the host, so
        /// both halves need two-process coverage: a client shown the wrong figure
        /// would be told it cannot afford what the host would sell it.
        /// </summary>
        private const float BuyAt = 10.6f;

        private const float PlaceForArrestAt = 11f;

        /// <summary>
        /// How long the thief spends standing by a piece, and then by the
        /// merchant. Long enough for a placement to settle and a request to
        /// cross the wire and come back.
        /// </summary>
        private const float SellCycle = 2.5f;

        /// <summary>
        /// What the least valuable piece fetches. Used only to decide when the
        /// purse is close enough to the target for one more sale to finish it.
        /// </summary>
        private const int CheapestLoot = 200;

        [SerializeField, Min(1f)]
        private float sampleSeconds = 14f;

        private string _mode = string.Empty;
        private string _scenario = "full";
        private float _elapsed;
        private bool _written;
        private float _driveTimer;
        private Vector2 _driveInput = Vector2.right;
        private bool _bridgeDisabled;

        // Latched observations. A property under test must not be undone by a
        // later step, so each is recorded the first time it becomes true.
        private bool _sawCarried;
        private int _sawCarrierRole = -1;
        private int _peakSoldAmount;
        private int _sellPhase = -1;
        private float _peakArrestSeconds;
        private bool _sawArrestCompleted;
        private int _peakArrestCount;
        private float _dogTravelled;
        private float _catTravelled;
        private Vector3 _lastDogPosition;
        private Vector3 _lastCatPosition;
        private bool _hasAnimalPositions;
        private int _jailSpells;
        private bool _wasJailed;
        private int _interactRequests;
        private bool _placedForPickup;
        private bool _placedForSale;
        private bool _placedForArrest;
        private bool _armedForThrow;
        private bool _requestedThrow;
        private bool _placedTrap;
        private bool _boughtProp;

        /// <summary>
        /// THROW-011. Latched: the purse changes again as soon as money is
        /// recovered or spent.
        /// </summary>
        private int _peakPoliceAmount;
        private int _policeSpentTotal;

        /// <summary>
        /// THROW-011. The purse as this machine last saw it.
        ///
        /// This is the figure worth comparing, not the spent total: the total is
        /// a host-side counter and is not replicated, so demanding it of a client
        /// asked for something a client cannot know. What has to cross is the
        /// balance falling when the host spends.
        /// </summary>
        private int _lastPoliceAmount = -1;

        /// <summary>
        /// ART-014. Widest thigh swing seen per role, on this machine.
        ///
        /// Here because the walk measures perfectly in the editor scene and the
        /// thief was reported as vibrating in place in a real session. The only
        /// thing a session changes is who simulates whom, so the swing has to be
        /// measured where that difference exists.
        /// </summary>
        private readonly Dictionary<PlayerRole, Quaternion> _thighRest =
            new();
        private readonly Dictionary<PlayerRole, float> _thighPeak = new();

        /// <summary>
        /// Vertical motion per role: how far the character bobs and how often it
        /// changes direction.
        ///
        /// Measured because "the thief vibrates" survived removing the only thing
        /// that deliberately bobbed a player, so something else is moving it and
        /// guessing has already cost three attempts.
        /// </summary>
        private readonly Dictionary<PlayerRole, float> _yLow = new();
        private readonly Dictionary<PlayerRole, float> _yHigh = new();
        private readonly Dictionary<PlayerRole, float> _yPrevious =
            new();
        private readonly Dictionary<PlayerRole, int> _ySign = new();
        private readonly Dictionary<PlayerRole, int> _yFlips = new();
        private readonly Dictionary<PlayerRole, float> _visualLow =
            new();
        private readonly Dictionary<PlayerRole, float> _visualHigh =
            new();

        /// <summary>
        /// THROW-009. Peak trap count seen on this machine. Latched, because the
        /// host clears a trap the moment it fires.
        /// </summary>
        private int _peakTrapCount;

        /// <summary>
        /// THROW-005. Latched on both machines: a rock in hand is momentary
        /// because the throw spends it.
        /// </summary>
        private bool _sawPickedUpRock;
        private bool _sawRockTaken;

        /// <summary>
        /// THROW-007. Latched, because a stun expires — reading it at write time
        /// would report a clean miss for a throw that landed perfectly.
        /// </summary>
        private bool _sawStun;
        private float _peakStunSeconds;
        private bool _sawPlaying;
        // Latched during the match. Reading these at write time is wrong: a
        // decided match despawns the links, so a late read reports zero spawned
        // objects and no remote control even though both held all match.
        private int _peakSpawnedLinks;
        private int _peakRemoteDrivenLinks;
        private int _peakSpawnedLootLinks;
        private bool _sawArrestRemoteControlled;
        private MatchState _lastMatchState = MatchState.Lobby;
        private string _decidedWinner = "None";
        private string _decidedReason = "None";
        private string _observedRole = "Unassigned";
        private int _disconnectCount;
        private string _disconnectReason = string.Empty;

        private void Awake()
        {
            IReadOnlyList<string> args =
                Environment.GetCommandLineArgs();
            _mode = ReadValue(args, ModeArgument)?.ToLowerInvariant()
                ?? string.Empty;
            if (string.IsNullOrEmpty(_mode))
            {
                enabled = false;
                return;
            }

            _scenario =
                ReadValue(args, ScenarioArgument)?.ToLowerInvariant()
                ?? "full";

            // Kept alive across the scene change.
            //
            // The probe used to die with the match scene, and that was fine
            // while no run ever reached a winner. Now that three arrests end a
            // match inside the run, the client reached the result screen and
            // was torn down before it could write anything: its file was simply
            // absent, which reads as a crash rather than a passing run.
            //
            // The guard is because Bootstrap and Game both carry one; a second
            // copy would race the first for the same file.
            if (_instance != null && _instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            string configured = ReadValue(args, SampleArgument);
            if (float.TryParse(
                    configured,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsed))
            {
                sampleSeconds = Mathf.Max(1f, parsed);
            }
        }

        private static NetworkMatchProbe _instance;
        private MatchResultEvaluator _watchedEvaluator;

        /// <summary>
        /// Keeps a subscription on whichever evaluator is live.
        ///
        /// Done every frame rather than on the sample tick. Sampling runs once
        /// a second, and on the client the window between the host's verdict
        /// arriving and the match scene unloading is far shorter than that — so
        /// the probe kept reporting no winner for a replication that was
        /// working, which is worse than no check at all.
        /// </summary>
        private void WatchEvaluator()
        {
            MatchResultEvaluator evaluator =
                FindFirstObjectByType<MatchResultEvaluator>();
            if (evaluator == null || evaluator == _watchedEvaluator)
            {
                return;
            }

            if (_watchedEvaluator != null)
            {
                _watchedEvaluator.ResultDecided -= HandleResultDecided;
            }

            _watchedEvaluator = evaluator;
            _watchedEvaluator.ResultDecided += HandleResultDecided;

            // Already decided before this probe found it.
            if (evaluator.HasResult)
            {
                HandleResultDecided(evaluator.CurrentResult);
            }
        }

        private void HandleResultDecided(MatchResult result)
        {
            _decidedWinner = result.Winner.ToString();
            _decidedReason = result.Reason.ToString();
        }

        /// <summary>
        /// Consecutive samples with no match runtime before the match counts as
        /// over. At the probe's sample rate this is a fraction of a second —
        /// long enough to outlast a transient, far shorter than the delay
        /// before the host quits.
        /// </summary>
        private const int RuntimeMissesForEnd = 20;

        private int _runtimeMisses;

        private void Update()
        {
            if (_written)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            WatchEvaluator();
            DriveScenario();
            Observe();

            // A decided match unloads this scene, which would destroy the probe
            // before it recorded anything. The result is the very thing under
            // test, so it is written the moment the match stops playing.
            // The rematch run has to survive into the result screen and out the
            // other side, so this probe drives the match but hands the verdict to
            // NetworkRematchProbe instead of quitting here.
            if (_scenario == "rematch")
            {
                HasMatchStoppedPlaying();
                return;
            }

            // A handled disconnect also unloads this scene, for the same reason:
            // NET-009 returns to the lobby. Record before that happens.
            if (_elapsed >= sampleSeconds
                || _disconnectCount > 0
                || HasMatchStoppedPlaying())
            {
                Write();
            }
        }

        private bool HasMatchStoppedPlaying()
        {
            MatchRuntimeState runtime =
                FindFirstObjectByType<MatchRuntimeState>();
            if (runtime == null)
            {
                // Gone, rather than not there for a frame.
                //
                // Treating the first miss as the end wrote the client's file
                // seconds into the match with nothing in it: this search skips
                // inactive objects, so a single frame where the runtime is
                // disabled looks identical to the scene having been unloaded.
                // Several samples in a row is the difference.
                _runtimeMisses++;
                return _sawPlaying && _runtimeMisses >= RuntimeMissesForEnd;
            }

            _runtimeMisses = 0;
            _lastMatchState = runtime.CurrentState;
            if (runtime.CurrentState == MatchState.Playing)
            {
                _sawPlaying = true;
                return false;
            }

            return _sawPlaying;
        }

        private void DriveScenario()
        {
            PlayerRole? assigned =
                LocalPlayerRoleSelector.OverriddenRole;
            if (!assigned.HasValue)
            {
                return;
            }

            EnsureBridgeDisabled();

            if (_scenario == "disconnect")
            {
                DriveDisconnect();
                return;
            }

            if (_elapsed < MoveUntil)
            {
                DriveMovement(assigned.Value);
                return;
            }

            if (_scenario == "steal" || _scenario == "clash")
            {
                DriveSelling(assigned.Value);
                return;
            }

            DriveLootAndArrest(assigned.Value);
        }

        /// <summary>
        /// NET-010's two unmeasured cases: the thief winning, and a sale
        /// landing in the same breath as the third catch.
        ///
        /// The match has two ways to end and only one of them had ever been run
        /// end to end. The officer's win was covered from the day it existed;
        /// the thief's was written down as blocked on there being too little
        /// treasure on the map to reach the target, and it stayed written down
        /// as blocked for long after six pieces were placed and the block went
        /// away. Nothing was watching, so nothing said so.
        ///
        /// The thief is walked round the same loop a player walks: stand by a
        /// piece, ask for it, stand by the merchant, ask to sell, repeat.
        /// Placement is the host's doing and the asking is the client's, for
        /// the same reason the rest of the run splits that way — a request that
        /// never crosses the wire proves nothing about the half most likely to
        /// be broken.
        ///
        /// In `clash` the officer is kept on the thief throughout, so the last
        /// sale and the last catch are both live at once and one of them has to
        /// lose. What is being checked is not which: it is that both machines
        /// name the same winner, and that the arbiter hands down one verdict
        /// rather than two.
        /// </summary>
        private void DriveSelling(PlayerRole role)
        {
            NetworkPlayerLink link = FindLink(role);
            if (link == null || !link.IsSpawned)
            {
                return;
            }

            link.SubmitInputRpc(Vector2.zero, false);

            // The officer only joins in for the clash, and only once the purse
            // is one sale away — arresting earlier would end the match before
            // there is anything to collide with.
            if (_scenario == "clash" && OneSaleShort())
            {
                KeepPoliceOnFreeThief();
            }

            float step = _elapsed - MoveUntil;
            bool selling = ((int)(step / SellCycle)) % 2 == 1;

            if (_mode == "host")
            {
                int phase = (int)(step / SellCycle);
                if (phase != _sellPhase)
                {
                    _sellPhase = phase;
                    if (selling)
                    {
                        PlaceThiefBesideSaleZone();
                    }
                    else
                    {
                        PlaceThiefBesideUnsoldLoot();
                    }
                }
            }

            // Asked for every frame rather than once. The window has to survive
            // the placement settling and the request crossing the wire, and the
            // sale is guarded against being credited twice anyway — that guard
            // is itself under test here.
            if (role == PlayerRole.Thief)
            {
                _interactRequests++;
                link.SubmitInteractRpc();
            }
        }

        /// <summary>
        /// Whether one more sale would take the thief over the line.
        ///
        /// Read from the purse rather than counted, because the officer's
        /// throws take a cut of it and a count would not know.
        /// </summary>
        private bool OneSaleShort()
        {
            foreach (ThiefLootWallet wallet in
                FindObjectsByType<ThiefLootWallet>(FindObjectsSortMode.None))
            {
                if (wallet.SoldAmount >= wallet.TargetAmount - CheapestLoot)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stands the thief beside a piece that is still there to be taken.
        ///
        /// By name, so the order is a property of the map rather than of
        /// whatever order the objects happened to be created in (ISSUE-041).
        /// </summary>
        private void PlaceThiefBesideUnsoldLoot()
        {
            LootItem loot = FindObjectsByType<LootItem>(
                    FindObjectsSortMode.None)
                .Where(item => item.isActiveAndEnabled
                    && item.CurrentState != LootState.Sold
                    && item.CurrentState != LootState.Carried)
                .OrderBy(item => item.name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (loot == null)
            {
                return;
            }

            PlaceRole(
                PlayerRole.Thief,
                loot.transform.position + new Vector3(1f, 0f, 0f));
        }

        /// <summary>
        /// The bridge reads real keys, and in an automated run no key is pressed,
        /// so it would overwrite the synthetic input with zero every frame. The
        /// probe takes its place instead of fighting it.
        /// </summary>
        private void EnsureBridgeDisabled()
        {
            if (_bridgeDisabled)
            {
                return;
            }

            _bridgeDisabled = true;
            foreach (NetworkInputBridge bridge in
                FindObjectsByType<NetworkInputBridge>(
                    FindObjectsSortMode.None))
            {
                bridge.enabled = false;
            }

            foreach (PlayerKeyboardInput input in
                FindObjectsByType<PlayerKeyboardInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            // The bridge normally does this. With it disabled the probe has to,
            // or a stray local action would bypass the host and invalidate the
            // whole comparison.
            foreach (PlayerInteractionInput input in
                FindObjectsByType<PlayerInteractionInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            foreach (LootDropInput input in
                FindObjectsByType<LootDropInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }
        }

        /// <summary>
        /// Feeds a changing input so the character actually moves, which is what
        /// makes a position comparison meaningful. Sent through the same link a
        /// player's keys use.
        /// </summary>
        private void DriveMovement(PlayerRole role)
        {
            _driveTimer += Time.unscaledDeltaTime;
            if (_driveTimer > 1.5f)
            {
                _driveTimer = 0f;
                _driveInput = new Vector2(
                    -_driveInput.y,
                    _driveInput.x);
            }

            NetworkPlayerLink link = FindLink(role);
            if (link != null && link.IsSpawned)
            {
                link.SubmitInputRpc(_driveInput, false);
            }
        }

        /// <summary>
        /// NET-005 to NET-007. Places the participants (host only) and then has
        /// each machine ask for its own role's action.
        ///
        /// The interact request is repeated every frame across a two-second
        /// window on purpose: that is NET-010's "loot button mashing" case, and
        /// the recorded score shows whether the host credited it once.
        /// </summary>
        private void DriveLootAndArrest(PlayerRole role)
        {
            NetworkPlayerLink link = FindLink(role);
            if (link == null || !link.IsSpawned)
            {
                return;
            }

            // Stop moving so a placed character stays where it was put.
            link.SubmitInputRpc(Vector2.zero, false);

            if (!_placedForPickup && _elapsed >= PlaceForPickupAt)
            {
                _placedForPickup = true;
                PlaceThiefBesideLoot();
            }

            if (!_placedForSale && _elapsed >= PlaceForSaleAt)
            {
                _placedForSale = true;
                PlaceThiefBesideSaleZone();
            }

            // THROW-007. The host puts a rock in the officer's hand and stands
            // them off at throwing distance; the machine playing the officer then
            // asks to throw it. Split that way on purpose — the request has to
            // travel the same road a real player's click does.
            if (!_armedForThrow && _elapsed >= ThrowAt - 0.5f)
            {
                _armedForThrow = true;
                ArmThiefWithRock();
            }

            // The thief throws, not the officer. The host takes police by
            // default, so making the officer the thrower would resolve the whole
            // thing inside one process and never send the request over the wire
            // at all — which is the half most likely to be broken.
            if (!_requestedThrow
                && _elapsed >= ThrowAt
                && role == PlayerRole.Thief)
            {
                _requestedThrow = true;
                RequestThrowAtOpponent(link, PlayerRole.Police);
            }

            if (!_placedTrap && _elapsed >= PlaceTrapAt)
            {
                _placedTrap = true;
                PlaceTrapAwayFromEverybody();
            }

            if (!_boughtProp && _elapsed >= BuyAt)
            {
                _boughtProp = true;
                BuyPolicePropOnHost();
            }

            // The officer is kept on the thief from here on rather than put
            // there once. One catch used to end the match; three are needed
            // now, and between them the thief is taken to the cells and put
            // back on the map. Driving this from the jail's own state instead
            // of a stopwatch means the run does not silently stop testing the
            // moment the sentence length is rebalanced.
            if (_elapsed >= PlaceForArrestAt)
            {
                _placedForArrest = true;
                KeepPoliceOnFreeThief();
            }

            bool mashing =
                (_elapsed >= MashPickupFrom
                    && _elapsed <= MashPickupUntil)
                || (_elapsed >= MashSaleFrom
                    && _elapsed <= MashSaleUntil);
            if (mashing && role == PlayerRole.Thief)
            {
                _interactRequests++;
                link.SubmitInteractRpc();
            }
        }

        /// <summary>
        /// NET-009. The client leaves early; the host has to notice and recover
        /// rather than wait forever.
        /// </summary>
        private void DriveDisconnect()
        {
            if (_mode != "client" || _elapsed < MoveUntil)
            {
                return;
            }

            Write();
        }

        private void Observe()
        {
            // Latched: when the peer quits, NET-009 clears the local role on
            // purpose. Reading it at write time would report Unassigned for a
            // machine that played the whole match in its role.
            PlayerRole? role = LocalPlayerRoleSelector.OverriddenRole;
            if (role.HasValue)
            {
                _observedRole = role.Value.ToString();
            }

            // ART-014. Thigh swing per role.
            foreach (PlayerRoleIdentity identity in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                Transform thigh = null;
                foreach (Transform bone in
                    identity.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name.Contains("Thigh"))
                    {
                        thigh = bone;
                        break;
                    }
                }

                if (thigh == null)
                {
                    continue;
                }

                if (!_thighRest.ContainsKey(identity.Role))
                {
                    _thighRest[identity.Role] = thigh.localRotation;
                    _thighPeak[identity.Role] = 0f;
                }

                // Only while the walk is what is writing this bone.
                //
                // The jump pose throws the same thigh 54 degrees against the walk's
                // 24, so counting those frames would let this figure look healthy
                // for a character whose walk had stopped working entirely — and
                // diagnosing exactly that is what it is for. Asked of the animator's
                // own blend rather than of grounded state: the pose eases out over
                // several frames after landing, so the feet are back down while the
                // limbs are still splayed.
                var poser = identity
                    .GetComponent<PawsAndLoot.Animation.CompanionLegAnimator>();
                if (poser != null && poser.AirborneBlend > 0.05f)
                {
                    continue;
                }

                _thighPeak[identity.Role] = Mathf.Max(
                    _thighPeak[identity.Role],
                    Quaternion.Angle(
                        _thighRest[identity.Role],
                        thigh.localRotation));
            }

            // Vertical motion per role, during the movement phase only.
            //
            // Restricted to before the first placement on purpose: the scenario
            // teleports characters to set up loot, sales and arrests, and a
            // teleport plus the fall that follows it swamps the bob being
            // measured. Nothing is placed before MoveUntil.
            //
            // The model is measured separately from the character, because "the
            // body vibrates" could be either the whole capsule moving or the mesh
            // moving inside it, and those have completely different causes.
            foreach (PlayerRoleIdentity identity in
                _elapsed < MoveUntil
                    ? FindObjectsByType<PlayerRoleIdentity>(
                        FindObjectsSortMode.None)
                    : System.Array.Empty<PlayerRoleIdentity>())
            {
                Transform visual =
                    identity.transform.Find("VisualRoot");
                if (visual != null)
                {
                    float vy = visual.position.y
                        - identity.transform.position.y;
                    if (!_visualLow.ContainsKey(identity.Role))
                    {
                        _visualLow[identity.Role] = vy;
                        _visualHigh[identity.Role] = vy;
                    }

                    _visualLow[identity.Role] = Mathf.Min(
                        _visualLow[identity.Role],
                        vy);
                    _visualHigh[identity.Role] = Mathf.Max(
                        _visualHigh[identity.Role],
                        vy);
                }

                float y = identity.transform.position.y;
                if (!_yLow.ContainsKey(identity.Role))
                {
                    _yLow[identity.Role] = y;
                    _yHigh[identity.Role] = y;
                    _yPrevious[identity.Role] = y;
                    _ySign[identity.Role] = 0;
                    _yFlips[identity.Role] = 0;
                }

                _yLow[identity.Role] = Mathf.Min(_yLow[identity.Role], y);
                _yHigh[identity.Role] = Mathf.Max(_yHigh[identity.Role], y);

                float step = y - _yPrevious[identity.Role];
                if (Mathf.Abs(step) > 0.0005f)
                {
                    int sign = step > 0f ? 1 : -1;
                    if (_ySign[identity.Role] != 0
                        && sign != _ySign[identity.Role])
                    {
                        _yFlips[identity.Role]++;
                    }

                    _ySign[identity.Role] = sign;
                    _yPrevious[identity.Role] = y;
                }
            }

            // THROW-011. The officer's purse, on both machines.
            foreach (PawsAndLoot.Gameplay.Players.PoliceWallet purse in
                FindObjectsByType<
                    PawsAndLoot.Gameplay.Players.PoliceWallet>(
                    FindObjectsSortMode.None))
            {
                _peakPoliceAmount = Mathf.Max(
                    _peakPoliceAmount,
                    purse.Amount);
                _policeSpentTotal = Mathf.Max(
                    _policeSpentTotal,
                    purse.SpentTotal);
                _lastPoliceAmount = purse.Amount;
            }

            // THROW-009. Both machines have to end up with the same trap. A
            // count of zero on the client means the message never crossed.
            NetworkItemCoordinator coordinator =
                FindFirstObjectByType<NetworkItemCoordinator>();
            if (coordinator != null)
            {
                _peakTrapCount = Mathf.Max(
                    _peakTrapCount,
                    coordinator.ActiveTrapCount);
            }

            // THROW-005. Recorded on both machines: the whole bug was that only
            // one of them ever knew.
            foreach (PawsAndLoot.Gameplay.Items.ToolCarrier carrier in
                FindObjectsByType<
                    PawsAndLoot.Gameplay.Items.ToolCarrier>(
                    FindObjectsSortMode.None))
            {
                if (carrier.HasTool)
                {
                    _sawPickedUpRock = true;
                }
            }

            foreach (PawsAndLoot.Gameplay.Items.ThrowablePickup pickup in
                FindObjectsByType<
                    PawsAndLoot.Gameplay.Items.ThrowablePickup>(
                    FindObjectsSortMode.None))
            {
                if (pickup.IsTaken)
                {
                    _sawRockTaken = true;
                }
            }

            // THROW-007. Recorded on both machines, because the point is that the
            // host's decision reached the other screen. A host that stuns the
            // thief while the client shows them still running is the exact
            // failure this exists to catch.
            foreach (StunState stun in
                FindObjectsByType<StunState>(FindObjectsSortMode.None))
            {
                if (!stun.IsStunned)
                {
                    continue;
                }

                _sawStun = true;
                _peakStunSeconds = Mathf.Max(
                    _peakStunSeconds,
                    stun.RemainingSeconds);
            }

            // NET-009. Measured rather than inferred: the handler is the thing
            // that has to notice a lost peer exactly once.
            NetworkDisconnectHandler disconnect =
                FindFirstObjectByType<NetworkDisconnectHandler>();
            if (disconnect != null && disconnect.HandledCount > 0)
            {
                _disconnectCount = disconnect.HandledCount;
                _disconnectReason = disconnect.LastReason;
            }

            // NET-006/007. Recorded on both machines so the two files can be
            // compared on the winner, which is the acceptance criterion for
            // "the same victory decision".
            MatchResultEvaluator evaluator =
                FindFirstObjectByType<MatchResultEvaluator>();

            if (evaluator != null && evaluator.HasResult)
            {
                _decidedWinner =
                    evaluator.CurrentResult.Winner.ToString();
                _decidedReason =
                    evaluator.CurrentResult.Reason.ToString();
            }
            else if (_decidedWinner == "None"
                && MatchResultSession.TryGet(out MatchResult stored))
            {
                // The evaluator lives in the match scene, and on the client
                // that scene starts unloading the moment the host's verdict is
                // adopted — often before the next sample. The session is a
                // static store that outlives the scene precisely so the result
                // screen can read it, which makes it the reliable place to ask.
                _decidedWinner = stored.Winner.ToString();
                _decidedReason = stored.Reason.ToString();
            }

            int spawnedLinks = 0;
            int remoteDriven = 0;
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(
                    FindObjectsSortMode.None))
            {
                if (link.IsSpawned)
                {
                    spawnedLinks++;
                }

                if (link.IsRemoteDriven)
                {
                    remoteDriven++;
                }
            }

            _peakSpawnedLinks = Mathf.Max(_peakSpawnedLinks, spawnedLinks);
            _peakRemoteDrivenLinks =
                Mathf.Max(_peakRemoteDrivenLinks, remoteDriven);

            int spawnedLoot = 0;
            foreach (NetworkLootLink loot in
                FindObjectsByType<NetworkLootLink>(
                    FindObjectsSortMode.None))
            {
                if (loot.IsSpawned)
                {
                    spawnedLoot++;
                }

                if (loot.ReplicatedState == LootState.Carried)
                {
                    _sawCarried = true;
                    _sawCarrierRole = loot.ReplicatedCarrierRole;
                }
            }

            _peakSpawnedLootLinks =
                Mathf.Max(_peakSpawnedLootLinks, spawnedLoot);

            foreach (ThiefLootWallet wallet in
                FindObjectsByType<ThiefLootWallet>(
                    FindObjectsSortMode.None))
            {
                _peakSoldAmount = Mathf.Max(
                    _peakSoldAmount,
                    wallet.SoldAmount);
            }

            // Latched during the match rather than read at the end: the match
            // scene unloads the moment a winner exists, and everything on it
            // reads as zero afterwards.
            foreach (CompanionAgent animal in
                FindObjectsByType<CompanionAgent>(
                    FindObjectsSortMode.None))
            {
                Vector3 now = animal.transform.position;
                bool isDog = animal.CompanionKind == CompanionKind.Dog;
                Vector3 last = isDog
                    ? _lastDogPosition
                    : _lastCatPosition;
                if (_hasAnimalPositions)
                {
                    float step = Vector3.Distance(
                        new Vector3(now.x, 0f, now.z),
                        new Vector3(last.x, 0f, last.z));
                    if (isDog)
                    {
                        _dogTravelled += step;
                    }
                    else
                    {
                        _catTravelled += step;
                    }
                }

                if (isDog)
                {
                    _lastDogPosition = now;
                }
                else
                {
                    _lastCatPosition = now;
                }
            }

            _hasAnimalPositions = true;

            MatchResultEvaluator arrestCounter =
                FindFirstObjectByType<MatchResultEvaluator>();
            if (arrestCounter != null)
            {
                _peakArrestCount = Mathf.Max(
                    _peakArrestCount,
                    arrestCounter.ArrestCount);
            }

            NetworkPlayerLink thiefLink = FindLink(PlayerRole.Thief);
            var thiefJail = thiefLink != null
                ? thiefLink.GetComponent<ThiefJailState>()
                : null;
            bool jailedNow = thiefJail != null && thiefJail.IsJailed;
            if (jailedNow && !_wasJailed)
            {
                _jailSpells++;
            }

            _wasJailed = jailedNow;

            foreach (ArrestProgressController arrest in
                FindObjectsByType<ArrestProgressController>(
                    FindObjectsSortMode.None))
            {
                _peakArrestSeconds = Mathf.Max(
                    _peakArrestSeconds,
                    arrest.ProgressSeconds);
                if (arrest.IsCompleted)
                {
                    _sawArrestCompleted = true;
                }

                if (arrest.IsRemoteControlled)
                {
                    _sawArrestRemoteControlled = true;
                }
            }
        }

        /// <summary>
        /// The same treasure every run.
        ///
        /// <c>FindFirstObjectByType</c> hands back whatever instance id ordering
        /// puts first, which is not a property of the map: adding nineteen rooms to
        /// the scene silently changed which treasure this was, and with it the
        /// height the thief was placed at. Ordering by name makes the run
        /// reproducible and the failure, when there is one, about the thing under
        /// test.
        /// </summary>
        /// <summary>
        /// Stands the thief beside a piece they can actually take.
        ///
        /// Sorted by name so the choice is a property of the map rather than of
        /// whatever order the objects were created in (`ISSUE-041`) — and
        /// filtered to pieces that are reachable, which is the same lesson
        /// arriving a second time. A treasure was added that sorts first and
        /// stands inside a glass case, so the run walked the thief up to the
        /// jeweller's window and asked for it every frame for a minute. The
        /// purse read zero and the failure said only "no winner".
        /// </summary>
        private void PlaceThiefBesideLoot()
        {
            LootItem loot = FindObjectsByType<LootItem>(
                    FindObjectsSortMode.None)
                .Where(item => item.isActiveAndEnabled)
                .OrderBy(item => item.name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (loot == null)
            {
                return;
            }

            PlaceRole(
                PlayerRole.Thief,
                loot.transform.position + new Vector3(1f, 0f, 0f));
        }

        private void PlaceThiefBesideSaleZone()
        {
            LootSaleZone zone = FindObjectsByType<LootSaleZone>(
                    FindObjectsSortMode.None)
                .OrderBy(area => area.name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (zone == null)
            {
                return;
            }

            PlaceRole(
                PlayerRole.Thief,
                zone.transform.position + new Vector3(1f, 0f, 0f));
        }

        /// <summary>
        /// Puts the officer within arresting distance whenever the thief is out
        /// of the cells, so the run reaches the third catch.
        ///
        /// Does nothing while a sentence is being served: teleporting the
        /// officer into the station would have them standing on a thief who
        /// cannot be arrested, and the next catch would land the instant the
        /// thief reappeared, which is not what the game does.
        /// </summary>
        private void KeepPoliceOnFreeThief()
        {
            NetworkPlayerLink thief = FindLink(PlayerRole.Thief);
            if (thief == null)
            {
                return;
            }

            var jail = thief.GetComponent<ThiefJailState>();
            if (jail != null && jail.IsJailed)
            {
                return;
            }

            PlaceRole(
                PlayerRole.Police,
                thief.transform.position + new Vector3(0.8f, 0f, 0f));
        }

        /// <summary>
        /// THROW-011 setup, host only. Buys from the counter directly.
        ///
        /// Called on the counter rather than through the scanner because the
        /// officer is standing next to the thief for the arrest at this point,
        /// not outside the shop. Whether the scanner finds a counter underfoot is
        /// the same question the pickup tests already answer against the real
        /// scene.
        /// </summary>
        private void BuyPolicePropOnHost()
        {
            if (_mode != "host")
            {
                return;
            }

            NetworkPlayerLink police = FindLink(PlayerRole.Police);
            if (police == null)
            {
                return;
            }

            PawsAndLoot.Gameplay.Players.PlayerRoleIdentity identity =
                police.GetComponent<
                    PawsAndLoot.Gameplay.Players.PlayerRoleIdentity>();
            PawsAndLoot.Gameplay.Items.ToolCarrier carrier =
                police.GetComponent<
                    PawsAndLoot.Gameplay.Items.ToolCarrier>();
            // Empty the hand first: the counter refuses a full one, which is
            // correct behaviour and would make this step measure nothing.
            carrier?.Clear();

            foreach (PawsAndLoot.Gameplay.Items.PoliceSupplyCounter counter in
                FindObjectsByType<
                    PawsAndLoot.Gameplay.Items.PoliceSupplyCounter>(
                    FindObjectsSortMode.None))
            {
                if (counter.TryInteract(
                        new PawsAndLoot.Gameplay.Players
                            .PlayerInteractionContext(identity)))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// THROW-009 setup, host only. Puts one trap down in an empty corner.
        /// </summary>
        private void PlaceTrapAwayFromEverybody()
        {
            if (_mode != "host")
            {
                return;
            }

            NetworkItemCoordinator.Place(
                PawsAndLoot.Gameplay.Items.ThrowableKind.GlueTrap,
                PlayerRole.Police,
                // A road intersection nobody is standing on at this point in the
                // timeline, so it never fires and never disturbs the arrest.
                new Vector3(-24f, 0f, 26f));
        }

        /// <summary>
        /// THROW-007 setup, host only. Puts a rock in the thief's hand and stands
        /// the officer a few metres off, across open ground.
        ///
        /// Deliberately not adjacent: a throw resolved at arm's length would pass
        /// even if the flight were broken entirely. Five metres is also far
        /// enough that a throw stopping on the nearest invisible trigger — the
        /// bug this covers — would fall short and fail the run.
        ///
        /// The rock goes in the tool slot, which is separate from the loot slot,
        /// so this does not disturb the sale the thief is in the middle of.
        /// </summary>
        private void ArmThiefWithRock()
        {
            if (_mode != "host")
            {
                return;
            }

            NetworkPlayerLink thief = FindLink(PlayerRole.Thief);
            if (thief == null)
            {
                return;
            }

            PlaceRole(
                PlayerRole.Police,
                thief.transform.position + new Vector3(0f, 0f, -5f));

            // Taken from a real pickup, not fabricated. That is what makes the
            // rock disappear from the ground and puts the fact on the wire — the
            // two things the other machine was never told.
            //
            // Called on the pickup directly rather than through the scanner: the
            // scanner picks the nearest interactable, and the thief is standing
            // at the sale point mid-sale at this moment. Whether the scanner
            // finds a rock underfoot is covered by
            // RockPickupScenePlayModeTests against the real scene.
            PawsAndLoot.Gameplay.Items.ThrowablePickup rock = null;
            foreach (PawsAndLoot.Gameplay.Items.ThrowablePickup candidate in
                FindObjectsByType<
                    PawsAndLoot.Gameplay.Items.ThrowablePickup>(
                    FindObjectsSortMode.None))
            {
                // A rock, named rather than inferred.
                //
                // This asked for "anything the thief may take" and that was
                // already once wrong: police-only props were being handed over
                // and refused. It went wrong a second way when the thief gained
                // props of their own — a shelf banana passes the same filter,
                // and a banana is placed rather than thrown, so the throw leg
                // silently stopped throwing anything and the stun it exists to
                // prove disappeared.
                //
                // The step is called ArmThiefWithRock. It should ask for a rock.
                if (!candidate.IsAvailable
                    || candidate.Kind
                        != PawsAndLoot.Gameplay.Items.ThrowableKind.Rock)
                {
                    continue;
                }

                rock = candidate;
                break;
            }

            if (rock != null)
            {
                rock.TryInteract(
                    new PlayerInteractionContext(
                        thief.GetComponent<PlayerRoleIdentity>()));
            }
        }

        /// <summary>
        /// Asks the host to throw at wherever the opponent currently is.
        ///
        /// Stands in for the cursor: a batch-mode run has no mouse, and the aim
        /// is the one value a real client computes locally and sends, so it has to
        /// be supplied here too.
        /// </summary>
        private void RequestThrowAtOpponent(
            NetworkPlayerLink thrower,
            PlayerRole target)
        {
            NetworkPlayerLink victim = FindLink(target);
            if (victim == null)
            {
                return;
            }

            Vector3 aim = victim.ReplicatedPosition
                - thrower.transform.position;
            aim.y = 0f;
            thrower.SubmitUseToolRpc(aim, 1f);
        }

        /// <summary>
        /// Host-only test setup. A client cannot move anyone — that is the
        /// property NET-003 established — so the placement has to happen where
        /// the simulation runs and reaches the client by replication.
        /// </summary>
        private void PlaceRole(PlayerRole role, Vector3 position)
        {
            if (_mode != "host")
            {
                return;
            }

            NetworkPlayerLink link = FindLink(role);
            if (link == null)
            {
                return;
            }

            var controller = link.GetComponent<CharacterController>();
            if (controller != null)
            {
                // Feet on the point, not the pivot on it.
                //
                // A character's transform sits about 0.9 m above their soles, so
                // writing the pivot to a spot on the ground buries the capsule and
                // the controller resolves that by pushing down — the character
                // drops away from whatever they were placed beside. It went
                // unnoticed while the treasure this lands next to happened to
                // stand on a plinth, and broke the moment a different one came
                // first. The same mistake the house doors made (ISSUE-040).
                float lift =
                    controller.height * 0.5f - controller.center.y;

                // A CharacterController ignores direct transform writes while
                // enabled, so it is cycled around the move.
                controller.enabled = false;
                link.transform.position = position + Vector3.up * lift;
                controller.enabled = true;
                return;
            }

            link.transform.position = position;
        }

        /// <summary>
        /// World height of a named bone on a character, or 999 when it is not
        /// found so "absent" cannot read as "correctly placed".
        ///
        /// Bones, not <c>Renderer.bounds</c>. A SkinnedMeshRenderer's bounds are
        /// derived from its root bone and a precomputed local box, so they do not
        /// track the animated pose: the mesh can be sunk to the waist while the
        /// bounds report it standing. Measuring the skeleton is the only way to
        /// see where the character actually is.
        /// </summary>
        private float MeasureBoneHeight(PlayerRole role, string boneName)
        {
            NetworkPlayerLink link = FindLink(role);
            if (link == null)
            {
                return 999f;
            }

            foreach (Transform bone in
                link.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == boneName)
                {
                    return bone.position.y;
                }
            }

            return 999f;
        }

        private static int CountEnabledAnimators()
        {
            int enabled = 0;
            foreach (Animator animator in
                FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (animator.enabled)
                {
                    enabled++;
                }
            }

            return enabled;
        }

        private NetworkPlayerLink FindLink(PlayerRole role)
        {
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(
                    FindObjectsSortMode.None))
            {
                if (link.Role == role)
                {
                    return link;
                }
            }

            return null;
        }

        private void Write()
        {
            _written = true;
            MatchRuntimeState runtime =
                FindFirstObjectByType<MatchRuntimeState>();
            NetworkRoleBoard board =
                FindFirstObjectByType<NetworkRoleBoard>();

            Vector3 police = Vector3.zero;
            Vector3 thief = Vector3.zero;
            int linkCount = 0;
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(
                    FindObjectsSortMode.None))
            {
                linkCount++;
                if (link.Role == PlayerRole.Police)
                {
                    police = link.transform.position;
                }
                else
                {
                    thief = link.transform.position;
                }
            }

            int lootLinks = 0;
            string lootState = "None";
            foreach (NetworkLootLink loot in
                FindObjectsByType<NetworkLootLink>(
                    FindObjectsSortMode.None))
            {
                lootLinks++;
                lootState = loot.ReplicatedState.ToString();
            }

            int soldAmount = 0;
            int creditedSales = 0;
            int targetAmount = 0;
            foreach (ThiefLootWallet wallet in
                FindObjectsByType<ThiefLootWallet>(
                    FindObjectsSortMode.None))
            {
                soldAmount = wallet.SoldAmount;
                creditedSales = wallet.CreditedSaleCount;
                targetAmount = wallet.TargetAmount;
            }

            float arrestSeconds = 0f;
            bool arrestCompleted = false;
            foreach (ArrestProgressController arrest in
                FindObjectsByType<ArrestProgressController>(
                    FindObjectsSortMode.None))
            {
                arrestSeconds = arrest.ProgressSeconds;
                arrestCompleted = arrest.IsCompleted;
            }

            var json = new StringBuilder();
            json.AppendLine("{");
            Append(json, "task", "NET-003-010");
            Append(json, "instance", _mode);
            Append(json, "scenario", _scenario);
            Append(
                json,
                "matchState",
                runtime != null
                    ? runtime.CurrentState.ToString()
                    : "None");
            AppendNumber(
                json,
                "remainingSeconds",
                runtime != null ? runtime.RemainingMatchSeconds : -1f);
            AppendBool(
                json,
                "matchRemoteControlled",
                runtime != null && runtime.IsRemoteControlled);
            Append(json, "localRole", _observedRole);
            // Split so a missing board is distinguishable from a board that is
            // present but reports no assignment. The two need different fixes.
            AppendBool(json, "boardExists", board != null);
            AppendBool(
                json,
                "boardAssigned",
                board != null && board.IsAssigned);
            AppendNumber(json, "playerLinks", linkCount);
            AppendNumber(json, "peakSpawnedLinks", _peakSpawnedLinks);
            AppendNumber(
                json,
                "peakRemoteDrivenLinks",
                _peakRemoteDrivenLinks);
            Append(json, "policePosition", Format(police));
            Append(json, "thiefPosition", Format(thief));

            // How far each character's drawn mesh sits above or below the point
            // its transform stands on. Near zero means it is on the ground; a
            // negative number means the model is sunk into it.
            //
            // Worth reporting because nothing else catches this: the transform
            // is correct either way, so positions and tests all pass while the
            // player sees a character buried to the waist.
            AppendNumber(
                json,
                "policeFootY",
                MeasureBoneHeight(PlayerRole.Police, "L_Foot"));
            AppendNumber(
                json,
                "policeHipY",
                MeasureBoneHeight(PlayerRole.Police, "Hip"));
            AppendNumber(
                json,
                "thiefFootY",
                MeasureBoneHeight(PlayerRole.Thief, "L_Foot"));
            AppendNumber(
                json,
                "thiefHipY",
                MeasureBoneHeight(PlayerRole.Thief, "Hip"));
            AppendNumber(json, "animatorsEnabled", CountEnabledAnimators());

            // NET-005
            AppendNumber(json, "lootLinks", lootLinks);
            AppendNumber(
                json,
                "peakSpawnedLootLinks",
                _peakSpawnedLootLinks);
            Append(json, "lootState", lootState);
            AppendBool(json, "sawLootCarried", _sawCarried);
            AppendNumber(json, "lootCarrierRole", _sawCarrierRole);

            // NET-006. interactRequests against creditedSales is the
            // duplicate-sale check: many requests must credit at most one sale.
            AppendNumber(json, "interactRequests", _interactRequests);
            AppendNumber(json, "soldAmount", soldAmount);
            AppendNumber(json, "targetAmount", targetAmount);
            AppendNumber(json, "peakSoldAmount", _peakSoldAmount);
            AppendNumber(json, "creditedSales", creditedSales);

            // NET-007
            AppendNumber(json, "arrestSeconds", arrestSeconds);
            AppendNumber(json, "peakArrestSeconds", _peakArrestSeconds);
            AppendBool(json, "arrestCompleted", arrestCompleted);
            AppendBool(json, "sawArrestCompleted", _sawArrestCompleted);
            // How far through the three the run actually got. Without it a
            // failure says only "no winner" and gives no way to tell a broken
            // arrest from a jail that never releases.
            AppendNumber(json, "peakArrestCount", _peakArrestCount);
            AppendNumber(json, "jailSpells", _jailSpells);
            // How far each animal moved on this machine. The animals were never
            // replicated: both sides ran their own copy, and since commands only
            // reach the host, a client's animal followed its owner and did
            // nothing else. Both files showing movement is what says the client
            // is being shown the host's animal rather than guessing at one.
            AppendNumber(json, "dogTravelled", _dogTravelled);
            AppendNumber(json, "catTravelled", _catTravelled);
            AppendBool(
                json,
                "sawArrestRemoteControlled",
                _sawArrestRemoteControlled);

            // NET-006/007/010
            Append(json, "lastMatchState", _lastMatchState.ToString());
            Append(json, "decidedWinner", _decidedWinner);
            Append(json, "decidedReason", _decidedReason);

            // The counters the result screen shows, from both machines.
            //
            // The verdict agreed and the counters did not: the client had the
            // winner and no summary, so its screen read "arrested the thief 0
            // times" over a clock of --:--. Comparing only the winner passed
            // that, twice, because the winner was never the broken part.
            MatchResultSession.TryGetSummary(out MatchSummary summary);
            AppendBool(json, "summaryReported", summary.IsReported);
            AppendNumber(json, "summaryElapsedSeconds", summary.ElapsedSeconds);
            AppendNumber(json, "summaryCatchCount", summary.CatchCount);
            AppendNumber(
                json,
                "summaryRequiredCatchCount",
                summary.RequiredCatchCount);
            AppendNumber(json, "summarySoldAmount", summary.SoldAmount);
            AppendNumber(json, "summaryTargetAmount", summary.TargetAmount);

            // THROW-007. Both files have to agree, or the two players are in
            // different chases.
            AppendNumber(json, "peakTrapCount", _peakTrapCount);
            AppendNumber(json, "peakPoliceAmount", _peakPoliceAmount);
            AppendNumber(json, "policeSpentTotal", _policeSpentTotal);
            AppendNumber(json, "lastPoliceAmount", _lastPoliceAmount);
            AppendNumber(
                json,
                "policeThighSwing",
                _thighPeak.TryGetValue(PlayerRole.Police, out float pv)
                    ? pv
                    : -1f);
            AppendNumber(
                json,
                "policeVisualRange",
                _visualHigh.TryGetValue(PlayerRole.Police, out float pv2)
                    ? pv2 - _visualLow[PlayerRole.Police]
                    : -1f);
            AppendNumber(
                json,
                "thiefVisualRange",
                _visualHigh.TryGetValue(PlayerRole.Thief, out float tv2)
                    ? tv2 - _visualLow[PlayerRole.Thief]
                    : -1f);
            AppendNumber(
                json,
                "policeYRange",
                _yHigh.TryGetValue(PlayerRole.Police, out float ph)
                    ? ph - _yLow[PlayerRole.Police]
                    : -1f);
            AppendNumber(
                json,
                "thiefYRange",
                _yHigh.TryGetValue(PlayerRole.Thief, out float th)
                    ? th - _yLow[PlayerRole.Thief]
                    : -1f);
            AppendNumber(
                json,
                "policeYFlips",
                _yFlips.TryGetValue(PlayerRole.Police, out int pf)
                    ? pf
                    : -1);
            AppendNumber(
                json,
                "thiefYFlips",
                _yFlips.TryGetValue(PlayerRole.Thief, out int tf)
                    ? tf
                    : -1);
            AppendNumber(
                json,
                "thiefThighSwing",
                _thighPeak.TryGetValue(PlayerRole.Thief, out float tv)
                    ? tv
                    : -1f);
            AppendBool(json, "sawPickedUpRock", _sawPickedUpRock);
            AppendBool(json, "sawRockTaken", _sawRockTaken);
            AppendBool(json, "sawStun", _sawStun);
            AppendNumber(json, "peakStunSeconds", _peakStunSeconds);
            AppendBool(json, "requestedThrow", _requestedThrow);

            // NET-009. More than one teardown would be the repeating-error bug.
            AppendNumber(json, "disconnectHandledCount", _disconnectCount);
            Append(json, "disconnectReason", _disconnectReason);

            bool sessionHealthy = runtime != null
                && linkCount == 2
                && _peakSpawnedLinks == 2
                && lootLinks >= 1
                && _peakSpawnedLootLinks == lootLinks;
            // The disconnect run deliberately never reaches a result: what it
            // has to show is that the survivor noticed exactly once. The client
            // is the one that leaves, so only the host is judged on that.
            // THROW-007 joins the full run's verdict. Both machines are judged on
            // it: the officer's client has to be able to ask, and the thief's
            // machine has to see the stun the host applied. Judging only the host
            // would pass the case where the throw works and nobody else sees it.
            // What the thief's run has to show: that the purse actually
            // reached the target and that both machines were told the thief
            // won. Not the officer's half — no rock is thrown, no trap laid,
            // nothing bought, and demanding those would fail a run that did
            // exactly what it set out to do.
            //
            // The clash asks something different, and the first version of it
            // asked wrongly: it demanded the purse reach the target, which
            // means it demanded the sale win the race. It passed twice by luck
            // and then failed the first time the officer got there first — a
            // correct outcome reported as a regression, which is worse than no
            // test at all.
            //
            // What has to hold is that the race resolved coherently. Whichever
            // side won, the thing that makes them the winner must have actually
            // happened: three catches for the officer, or a full purse for the
            // thief. Never both, never neither. Whether the two machines agree
            // is compared between the two result files, because neither process
            // can see the other's.
            bool sellingPassed =
                _sawCarried
                && _decidedWinner != "None"
                // Both machines, not just the host. The client used to be let
                // off this because its purse replicates, and that is exactly
                // where the hole was: the winning sale never crossed, so the
                // client called the thief the winner over a counter two
                // hundred short of the target.
                && _peakSoldAmount >= targetAmount;

            bool scenarioPassed = _scenario switch
            {
                "disconnect" => _mode != "host" || _disconnectCount == 1,
                "steal" => sellingPassed && _decidedWinner == "Thief",
                "clash" => _sawCarried
                    && _decidedWinner != "None"
                    && (_mode != "host"
                        || (_decidedWinner == "Police"
                            ? _peakArrestCount >= 3
                            : _peakSoldAmount >= targetAmount)),
                _ => _sawCarried
                    && _decidedWinner != "None"
                    // Three catches, but only where they are counted. The
                    // client adopts the host's verdict rather than counting for
                    // itself, so its arrest count and jail spells are always
                    // zero by design — judging them here would demand the
                    // client duplicate the host's simulation, which is the very
                    // thing that broke.
                    && (_mode != "host"
                        || (_peakArrestCount >= 3 && _jailSpells >= 3))
                    // Both animals moved on this machine. Judged on both sides:
                    // the host has to walk them and the client has to be shown
                    // it, and checking only the host passes the case where the
                    // animals work and nobody else sees them.
                    && _dogTravelled > 1f
                    && _catTravelled > 1f
                    && _sawStun
                    // THROW-005. Both machines have to have seen the rock in
                    // hand and gone from the ground.
                    && _sawPickedUpRock
                    && _sawRockTaken
                    // THROW-009. The placed-trap message reached this machine.
                    && _peakTrapCount >= 1
                    // THROW-011. The purse replicated and a purchase went
                    // through. Zero on the client means the figure never
                    // crossed.
                    && _peakPoliceAmount > 0
                    // The balance fell after the purchase, on both machines.
                    // That is the half that has to cross the wire; the spent
                    // total is a host-side counter.
                    && _lastPoliceAmount >= 0
                    && _lastPoliceAmount < _peakPoliceAmount
            };
            AppendBool(json, "passed", sessionHealthy && scenarioPassed);
            json.AppendLine("  \"end\": true");
            json.AppendLine("}");

            string directory = Application.persistentDataPath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory,
                $"net-match-{_mode}-result.json");
            File.WriteAllText(path, json.ToString());
            GameLogger.Info(
                GameLogCategory.Network,
                $"Match probe wrote '{path}'.",
                this);
            // Written, but not gone yet on the host.
            //
            // The host decides the winner and stops playing in the same frame, and
            // quitting there took the session down before the replicated state had
            // been sent even once. The client then recorded a match still in
            // progress and a winner of None, which reads exactly like the result
            // failing to cross when in fact nothing had been given the chance to
            // cross. Staying up for a moment lets the client observe the result it
            // is being judged on. The client itself has nobody waiting on it and
            // leaves immediately.
            if (_mode == "host")
            {
                DelayedQuit.Schedule(2.5f);
                return;
            }

            Application.Quit(0);
        }

        /// <summary>
        /// Quits a moment later, from an object that outlives the scene.
        ///
        /// Not a coroutine on the probe: deciding the match unloads the match
        /// scene, so the probe is destroyed within the same second and a coroutine
        /// on it would stop without ever quitting. A run that hangs until the
        /// harness times it out is worse than the race being fixed.
        /// </summary>
        private sealed class DelayedQuit : MonoBehaviour
        {
            private float _remaining;

            public static void Schedule(float seconds)
            {
                var host = new GameObject("Probe Delayed Quit");
                DontDestroyOnLoad(host);
                host.AddComponent<DelayedQuit>()._remaining = seconds;
            }

            private void Update()
            {
                _remaining -= Time.unscaledDeltaTime;
                if (_remaining <= 0f)
                {
                    Application.Quit(0);
                }
            }
        }

        private static string Format(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.00} {1:0.00} {2:0.00}",
                value.x,
                value.y,
                value.z);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            string flag)
        {
            for (int index = 0; index < args.Count - 1; index++)
            {
                if (string.Equals(
                        args[index],
                        flag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static void Append(
            StringBuilder json,
            string key,
            string value)
        {
            json.AppendLine($"  \"{key}\": \"{value}\",");
        }

        private static void AppendBool(
            StringBuilder json,
            string key,
            bool value)
        {
            json.AppendLine(
                $"  \"{key}\": {(value ? "true" : "false")},");
        }

        private static void AppendNumber(
            StringBuilder json,
            string key,
            float value)
        {
            json.AppendLine(
                $"  \"{key}\": "
                + value.ToString("0.###", CultureInfo.InvariantCulture)
                + ",");
        }
    }
}
