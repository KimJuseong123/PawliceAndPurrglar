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
        private float _peakArrestSeconds;
        private bool _sawArrestCompleted;
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

        private void Update()
        {
            if (_written)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
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
                return false;
            }

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

            DriveLootAndArrest(assigned.Value);
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

            if (!_placedForArrest && _elapsed >= PlaceForArrestAt)
            {
                _placedForArrest = true;
                PlacePoliceBesideThief();
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
        private void PlaceThiefBesideLoot()
        {
            LootItem loot = FindObjectsByType<LootItem>(
                    FindObjectsSortMode.None)
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

        private void PlacePoliceBesideThief()
        {
            NetworkPlayerLink thief = FindLink(PlayerRole.Thief);
            if (thief == null)
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
                // One the thief may actually take. The scene now holds
                // police-only props too, and the enumeration order is arbitrary:
                // taking the first available one handed the thief a glue trap it
                // was refused, so the throw leg silently had nothing to throw.
                if (!candidate.IsAvailable
                    || (candidate.IsRoleRestricted
                        && candidate.RestrictedTo != PlayerRole.Thief))
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
            thrower.SubmitUseToolRpc(aim);
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
            foreach (ThiefLootWallet wallet in
                FindObjectsByType<ThiefLootWallet>(
                    FindObjectsSortMode.None))
            {
                soldAmount = wallet.SoldAmount;
                creditedSales = wallet.CreditedSaleCount;
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
            AppendNumber(json, "peakSoldAmount", _peakSoldAmount);
            AppendNumber(json, "creditedSales", creditedSales);

            // NET-007
            AppendNumber(json, "arrestSeconds", arrestSeconds);
            AppendNumber(json, "peakArrestSeconds", _peakArrestSeconds);
            AppendBool(json, "arrestCompleted", arrestCompleted);
            AppendBool(json, "sawArrestCompleted", _sawArrestCompleted);
            AppendBool(
                json,
                "sawArrestRemoteControlled",
                _sawArrestRemoteControlled);

            // NET-006/007/010
            Append(json, "lastMatchState", _lastMatchState.ToString());
            Append(json, "decidedWinner", _decidedWinner);
            Append(json, "decidedReason", _decidedReason);

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
            bool scenarioPassed = _scenario == "disconnect"
                ? _mode != "host" || _disconnectCount == 1
                : _sawCarried
                    && _decidedWinner != "None"
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
                    && _lastPoliceAmount < _peakPoliceAmount;
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
