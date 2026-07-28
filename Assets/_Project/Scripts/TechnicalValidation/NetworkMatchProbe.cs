using System;
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

        private void PlaceThiefBesideLoot()
        {
            LootItem loot = FindFirstObjectByType<LootItem>();
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
            LootSaleZone zone = FindFirstObjectByType<LootSaleZone>();
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

            PawsAndLoot.Gameplay.Items.ToolCarrier carrier =
                thief.GetComponent<
                    PawsAndLoot.Gameplay.Items.ToolCarrier>();
            if (carrier != null)
            {
                carrier.TryPickUp(
                    PawsAndLoot.Gameplay.Items.ThrowableKind.Rock);
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
                // A CharacterController ignores direct transform writes while
                // enabled, so it is cycled around the move.
                controller.enabled = false;
                link.transform.position = position;
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
                : _sawCarried && _decidedWinner != "None" && _sawStun;
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
            Application.Quit(0);
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
