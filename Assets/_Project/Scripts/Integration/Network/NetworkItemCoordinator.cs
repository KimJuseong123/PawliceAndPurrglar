using System.Collections.Generic;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// THROW-005/007. Keeps the world's props in step across both machines:
    /// traps that were placed, and pickups that were taken.
    ///
    /// Named messages rather than a spawned NetworkObject per banana. Dynamic
    /// NetworkObjects are what went wrong in ISSUE-016, and a trap is only three
    /// numbers — an id, a kind and a position — so replicating the fact is
    /// cheaper and simpler than replicating an object. A pickup is one number and
    /// a flag.
    ///
    /// The host owns both decisions. Each machine draws the trap; only the host
    /// decides who stepped on it, then tells everyone it is gone. A client
    /// judging its own traps would slip the runner on one screen and not the
    /// other, and a client running its own respawn clock would show a rock lying
    /// in the road that the host had already given away.
    ///
    /// Pickups are here because they had no replication at all, and the result
    /// looked exactly like the feature not working: the press reached the host,
    /// the host took the rock, and on the other screen the rock stayed on the
    /// ground and the HUD kept saying the player was holding nothing.
    ///
    /// Offline it still works: with no session running it decides locally, so the
    /// single-player playtest is unaffected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkItemCoordinator : MonoBehaviour
    {
        public const string PlaceMessageName = "PawsAndLoot.TrapPlaced";
        public const string ClearMessageName = "PawsAndLoot.TrapCleared";
        public const string PickupMessageName = "PawsAndLoot.PickupTaken";
        public const string RevealMessageName = "PawsAndLoot.ThiefRevealed";

        /// <summary>
        /// Payload sizes, asked of the serialiser rather than counted.
        ///
        /// Public so a test can write each payload into a buffer of exactly this
        /// size and fail if the two ever disagree again. Hand arithmetic is what
        /// produced a buffer four bytes short of a Vector3.
        /// </summary>
        public static readonly int PlaceMessageBytes =
            FastBufferWriter.GetWriteSize<int>() * 3
            + FastBufferWriter.GetWriteSize<Vector3>();

        public static readonly int ClearMessageBytes =
            FastBufferWriter.GetWriteSize<int>();

        public static readonly int RevealMessageBytes =
            FastBufferWriter.GetWriteSize<int>() * 2
            + FastBufferWriter.GetWriteSize<Vector3>();

        public static readonly int PickupMessageBytes =
            FastBufferWriter.GetWriteSize<int>()
            + FastBufferWriter.GetWriteSize<bool>();

        private static NetworkItemCoordinator _instance;

        private readonly Dictionary<int, PlacedTrap> _traps = new();
        private readonly HashSet<ToolUseAction> _watched = new();
        private readonly HashSet<ThrowablePickup> _watchedPickups = new();
        private readonly Dictionary<ThrowableKind, Material>
            _trapMaterials = new();

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;
        private NetworkManager _networkManager;
        private bool _registered;
        private int _nextId = 1;

        public int ActiveTrapCount => _traps.Count;

        public void Configure(IMatchStateReader configuredMatchState)
        {
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        /// <summary>
        /// Called on whichever machine is simulating — the host in a session, or
        /// the only machine offline.
        /// </summary>
        public static void Place(
            ThrowableKind kind,
            PlayerRole placedBy,
            Vector3 position)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.PlaceInternal(kind, placedBy, position);
        }

        private void PlaceInternal(
            ThrowableKind kind,
            PlayerRole placedBy,
            Vector3 position)
        {
            int id = _nextId++;
            Spawn(id, kind, placedBy, position);

            NetworkManager manager = ResolveManager();
            if (manager == null
                || !manager.IsListening
                || !manager.IsServer)
            {
                return;
            }

            // Sized from the writes, not counted by hand.
            //
            // It was counted by hand and it was wrong: three ints and a Vector3
            // is 24 bytes and the buffer was 20, so placing anything threw an
            // overflow after the third int with 8 bytes left and 12 to write.
            // Nothing had ever been placed before the police got their props,
            // which is why it surfaced only now.
            using var writer = new FastBufferWriter(
                PlaceMessageBytes,
                Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe((int)kind);
            writer.WriteValueSafe((int)placedBy);
            writer.WriteValueSafe(position);
            manager.CustomMessagingManager.SendNamedMessageToAll(
                PlaceMessageName,
                writer);
        }

        /// <summary>
        /// Builds the local object. Runs on every machine so both draw the same
        /// banana in the same place.
        /// </summary>
        private void Spawn(
            int id,
            ThrowableKind kind,
            PlayerRole placedBy,
            Vector3 position)
        {
            if (_traps.ContainsKey(id))
            {
                return;
            }

            var trapObject = new GameObject($"Trap {id} ({kind})");
            trapObject.transform.position = position;
            PlacedTrap trap = trapObject.AddComponent<PlacedTrap>();
            trap.Configure(id, kind, placedBy, ResolveMatchState());

            // Something to look at. Placed props had no visual at all, which
            // went unnoticed only because nothing placeable was obtainable yet —
            // the first banana anybody put down would have been invisible, and a
            // trap you cannot see is not a trap.
            trapObject.AddComponent<PawsAndLoot.Animation.PlacedTrapView>()
                .Configure(kind, ResolveTrapMaterial(kind), placedBy);
            _traps[id] = trap;
        }

        private void Clear(int id)
        {
            if (!_traps.TryGetValue(id, out PlacedTrap trap))
            {
                return;
            }

            _traps.Remove(id);
            if (trap != null)
            {
                Destroy(trap.gameObject);
            }
        }

        /// <summary>
        /// Host-side sweep. Checked here rather than in the trap so a client's
        /// copy never fires, whatever its own collider thinks.
        /// </summary>
        private void TickTraps()
        {
            NetworkManager manager = ResolveManager();
            bool simulating = manager == null
                || !manager.IsListening
                || manager.IsServer;
            if (!simulating || _traps.Count == 0)
            {
                return;
            }

            List<int> spent = null;
            foreach (KeyValuePair<int, PlacedTrap> entry in _traps)
            {
                PlacedTrap trap = entry.Value;
                if (trap == null
                    || !trap.TryTrigger(out PlayerRoleIdentity victim))
                {
                    continue;
                }

                ApplyEffect(trap, victim);
                spent ??= new List<int>();
                spent.Add(entry.Key);
            }

            if (spent == null)
            {
                return;
            }

            foreach (int id in spent)
            {
                // A sensor light is destroyed a moment later rather than at
                // once. Removing it the instant it fired took its lamp with it,
                // so the flash never happened and the officer's alert had
                // nothing to point at — which read as the sensor not working.
                if (_traps.TryGetValue(id, out PlacedTrap spentTrap)
                    && spentTrap != null
                    && ThrowableCatalog.GetEffect(spentTrap.Kind)
                        == TrapEffect.Reveal)
                {
                    _traps.Remove(id);
                    Destroy(
                        spentTrap.gameObject,
                        ThrowableCatalog.RevealSeconds);
                }
                else
                {
                    Clear(id);
                }

                if (manager != null
                    && manager.IsListening
                    && manager.IsServer)
                {
                    using var writer = new FastBufferWriter(
                        ClearMessageBytes,
                        Allocator.Temp);
                    writer.WriteValueSafe(id);
                    manager.CustomMessagingManager
                        .SendNamedMessageToAll(
                            ClearMessageName,
                            writer);
                }
            }
        }

        /// <summary>
        /// Host-side. Turns a trip into what that prop actually does.
        ///
        /// Two effects, not one per prop, so adding a prop does not add a branch
        /// here. A hold stops them; a reveal does not slow them at all and simply
        /// makes them visible — the thief keeps running, in the open.
        /// </summary>
        private void ApplyEffect(
            PlacedTrap trap,
            PlayerRoleIdentity victim)
        {
            if (ThrowableCatalog.GetEffect(trap.Kind)
                == TrapEffect.Reveal)
            {
                Reveal(victim.Role, trap.TrapId, trap.transform.position);
                return;
            }

            StunState stun = victim.GetComponent<StunState>();
            bool held = stun?.TryApply(
                ThrowableCatalog.GetStunSeconds(trap.Kind)) == true;

            // A glue trap that catches the thief takes money too, on the same
            // terms as a thrown rock — the officer's props should not be worth
            // less than their arm. A banana does not: the thief taking money off
            // the officer would mean the officer's equipment funds itself out of
            // its own failures.
            if (held && trap.PlacedBy == PlayerRole.Police)
            {
                PawsAndLoot.Gameplay.Loot.LootConfiscationRule.Apply(
                    victim,
                    FindPolice());
            }
        }

        /// <summary>
        /// The officer, for crediting a trap they placed but are not standing on.
        /// </summary>
        private static PlayerRoleIdentity FindPolice()
        {
            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role == PlayerRole.Police)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Exposes a player on the other side's screen, and lights the lamp on
        /// every screen.
        ///
        /// Sent rather than recomputed: visibility is decided per screen, so the
        /// machine that has to stop hiding the thief is not the machine that
        /// decided the sensor went off.
        /// </summary>
        private void Reveal(
            PlayerRole revealed,
            int trapId,
            Vector3 source)
        {
            ApplyRevealLocally(revealed, trapId, source);

            NetworkManager manager = ResolveManager();
            if (manager == null
                || !manager.IsListening
                || !manager.IsServer)
            {
                return;
            }

            using var writer = new FastBufferWriter(
                RevealMessageBytes,
                Allocator.Temp);
            writer.WriteValueSafe((int)revealed);
            writer.WriteValueSafe(trapId);
            writer.WriteValueSafe(source);
            manager.CustomMessagingManager.SendNamedMessageToAll(
                RevealMessageName,
                writer);
        }

        private void ApplyRevealLocally(
            PlayerRole revealed,
            int trapId,
            Vector3 source)
        {
            foreach (FlashlightVisibility visibility in
                FindObjectsByType<FlashlightVisibility>(
                    FindObjectsSortMode.None))
            {
                // The component sits on the watcher and hides the other side, so
                // the one to switch off is the one that is not the revealed
                // player.
                if (visibility.GetComponent<PlayerRoleIdentity>()?.Role
                    != revealed)
                {
                    visibility.RevealFor(
                        ThrowableCatalog.RevealSeconds,
                        source);
                }
            }

            if (_traps.TryGetValue(trapId, out PlacedTrap trap)
                && trap != null)
            {
                trap.GetComponent<
                    PawsAndLoot.Animation.PlacedTrapView>()?.Flash();
            }
        }

        private void HandleRevealed(
            ulong sender,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out int revealed);
            reader.ReadValueSafe(out int trapId);
            reader.ReadValueSafe(out Vector3 source);

            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsServer)
            {
                // The host already applied it when it decided.
                return;
            }

            ApplyRevealLocally((PlayerRole)revealed, trapId, source);
        }

        /// <summary>
        /// Greybox colours for the placed props. Loaded rather than authored
        /// because these are prototype stand-ins.
        /// </summary>
        private Material ResolveTrapMaterial(ThrowableKind kind)
        {
            if (_trapMaterials.TryGetValue(kind, out Material cached))
            {
                return cached;
            }

            Color color = kind switch
            {
                ThrowableKind.GlueTrap => new Color(0.24f, 0.2f, 0.16f),
                ThrowableKind.SensorLight => new Color(0.86f, 0.88f, 0.9f),
                _ => new Color(1f, 0.85f, 0.2f)
            };

            var material = new Material(
                Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = $"PlacedTrap_{kind}"
            };
            material.SetColor("_BaseColor", color);
            _trapMaterials[kind] = material;
            return material;
        }

        private void HandlePlaced(
            ulong sender,
            FastBufferReader reader)
        {
            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsServer)
            {
                // The host already built its own copy when it placed it.
                return;
            }

            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out int kind);
            reader.ReadValueSafe(out int placedBy);
            reader.ReadValueSafe(out Vector3 position);
            Spawn(
                id,
                (ThrowableKind)kind,
                (PlayerRole)placedBy,
                position);
        }

        private void HandleCleared(
            ulong sender,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out int id);
            Clear(id);
        }

        private void EnsureRegistered(NetworkManager manager)
        {
            if (_registered
                || manager == null
                || manager.CustomMessagingManager == null)
            {
                return;
            }

            _registered = true;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                PlaceMessageName,
                HandlePlaced);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                ClearMessageName,
                HandleCleared);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                PickupMessageName,
                HandlePickupTaken);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                RevealMessageName,
                HandleRevealed);
            GameLogger.Debug(
                GameLogCategory.Network,
                "Trap messages registered.",
                this);
        }

        private NetworkManager ResolveManager()
        {
            if (_networkManager == null)
            {
                _networkManager = NetworkManager.Singleton;
            }

            return _networkManager;
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }

        /// <summary>
        /// Subscribes to every pickup so the machine that decides can announce
        /// it.
        ///
        /// Done here for the same reason placements are: the gameplay layer must
        /// not name the network layer, so the adapter listens instead.
        /// </summary>
        private void SubscribeToPickups()
        {
            foreach (ThrowablePickup pickup in
                FindObjectsByType<ThrowablePickup>(
                    FindObjectsSortMode.None))
            {
                if (_watchedPickups.Contains(pickup))
                {
                    continue;
                }

                _watchedPickups.Add(pickup);
                pickup.TakenChanged += HandlePickupChanged;
            }
        }

        private void HandlePickupChanged(int id, bool taken)
        {
            NetworkManager manager = ResolveManager();
            if (manager == null
                || !manager.IsListening
                || !manager.IsServer)
            {
                return;
            }

            using var writer = new FastBufferWriter(
                PickupMessageBytes,
                Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(taken);
            manager.CustomMessagingManager.SendNamedMessageToAll(
                PickupMessageName,
                writer);
        }

        private void HandlePickupTaken(
            ulong sender,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out bool taken);

            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsServer)
            {
                // The host already applied it when it decided.
                return;
            }

            foreach (ThrowablePickup pickup in
                FindObjectsByType<ThrowablePickup>(
                    FindObjectsSortMode.None))
            {
                if (pickup.PickupId == id)
                {
                    pickup.ApplyReplicatedTaken(taken);
                    return;
                }
            }
        }

        /// <summary>
        /// Subscribes to every player's placement event.
        ///
        /// The coordinator reaches into the gameplay layer rather than the other
        /// way round. Gameplay must not name Integration — that boundary is why
        /// the rules stay testable without a network — so the adapter does the
        /// listening, the same shape the scene-load and sound bridges use.
        /// </summary>
        private void SubscribeToPlacements()
        {
            foreach (ToolUseAction action in
                FindObjectsByType<ToolUseAction>(
                    FindObjectsSortMode.None))
            {
                if (_watched.Contains(action))
                {
                    continue;
                }

                _watched.Add(action);
                action.Placed += HandleLocalPlacement;
            }
        }

        private void HandleLocalPlacement(
            ThrowableKind kind,
            PlayerRole placedBy,
            Vector3 position)
        {
            NetworkManager manager = ResolveManager();
            // Only the simulating machine creates traps. A client's press
            // already travelled to the host as an RPC, so acting on it here too
            // would produce two bananas from one banana.
            if (manager != null
                && manager.IsListening
                && !manager.IsServer)
            {
                return;
            }

            PlaceInternal(kind, placedBy, position);
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsListening)
            {
                EnsureRegistered(manager);
            }
            else
            {
                _registered = false;
            }

            SubscribeToPlacements();
            SubscribeToPickups();
            TickTraps();
        }
    }
}
