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

        private static NetworkItemCoordinator _instance;

        private readonly Dictionary<int, PlacedTrap> _traps = new();
        private readonly HashSet<ToolUseAction> _watched = new();
        private readonly HashSet<ThrowablePickup> _watchedPickups = new();

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

            using var writer = new FastBufferWriter(
                sizeof(int) * 2 + sizeof(float) * 3,
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

                StunState stun = victim.GetComponent<StunState>();
                stun?.TryApply(
                    ThrowableCatalog.GetStunSeconds(trap.Kind));
                spent ??= new List<int>();
                spent.Add(entry.Key);
            }

            if (spent == null)
            {
                return;
            }

            foreach (int id in spent)
            {
                Clear(id);
                if (manager != null
                    && manager.IsListening
                    && manager.IsServer)
                {
                    using var writer = new FastBufferWriter(
                        sizeof(int),
                        Allocator.Temp);
                    writer.WriteValueSafe(id);
                    manager.CustomMessagingManager
                        .SendNamedMessageToAll(
                            ClearMessageName,
                            writer);
                }
            }
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
                sizeof(int) + sizeof(byte),
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
