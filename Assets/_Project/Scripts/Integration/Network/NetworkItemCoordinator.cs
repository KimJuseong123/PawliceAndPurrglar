using System.Collections.Generic;
using PawliceAndPurrglar.Audio;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Gameplay.Sensing;
using PawliceAndPurrglar.Logging;
using PawliceAndPurrglar.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Network
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
        public const string PlaceMessageName = "PawliceAndPurrglar.TrapPlaced";
        public const string ClearMessageName = "PawliceAndPurrglar.TrapCleared";
        public const string PickupMessageName = "PawliceAndPurrglar.PickupTaken";
        public const string RevealMessageName = "PawliceAndPurrglar.ThiefRevealed";
        public const string NoiseMessageName = "PawliceAndPurrglar.NoiseHeard";

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

        /// <summary>
        /// Position, how far it carries, and who set it off.
        /// </summary>
        public static readonly int NoiseMessageBytes =
            sizeof(float) * 4 + sizeof(int);

        /// <summary>
        /// Who, which prop, where, and for how long.
        ///
        /// The duration used to be assumed at the far end
        /// (<c>ThrowableCatalog.RevealSeconds</c>), which was fine while the
        /// sensor light was the only thing that revealed anybody. The display
        /// case's alarm reveals for four seconds, so the number has to travel
        /// with the message rather than be guessed from it.
        /// </summary>
        public static readonly int RevealMessageBytes =
            FastBufferWriter.GetWriteSize<int>() * 2
            + FastBufferWriter.GetWriteSize<Vector3>()
            + FastBufferWriter.GetWriteSize<float>();

        /// <summary>
        /// A reveal that came from something other than a placed prop, so there
        /// is no lamp to flash.
        /// </summary>
        public const int NoTrap = -1;

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
        public int ActiveThrownPickupCount
        {
            get
            {
                SubscribeToPickups();
                PruneMissingPickups();

                int count = 0;
                foreach (ThrowablePickup pickup in _watchedPickups)
                {
                    if (pickup != null
                        && pickup.PickupId < 0
                        && !pickup.IsTaken)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

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
            trapObject.AddComponent<PawliceAndPurrglar.Animation.PlacedTrapView>()
                .Configure(kind, ResolveTrapMaterial(kind), placedBy);
            _traps[id] = trap;

            // Put down, on both screens. A firework announces itself with its
            // fuse rather than the ordinary thump, because from here on it is
            // burning down to something and the sound is the warning.
            GameSoundService.Request(
                kind == ThrowableKind.Firework
                    ? GameSoundId.NoisePropFuse
                    : GameSoundId.TrapPlaced);

            // Food works on being put down, not on being trodden on. Applied
            // where the trap is created so it happens on the host and on the
            // client alike — both machines draw the animal walking over, and
            // only the host's movement is the one that counts.
            ApplyLure(kind, position);
        }

        /// <summary>
        /// Calls the animal the prop is meant for.
        ///
        /// Matched by companion kind rather than by owner, because the prop
        /// names the animal it smells like: a tuna can pulls the cat wherever
        /// the cat came from. That also means a thief who somehow got hold of a
        /// tuna can would pull their own cat, which is the correct outcome
        /// rather than a special case.
        /// </summary>
        private void ApplyLure(ThrowableKind kind, Vector3 position)
        {
            CompanionKind? wanted =
                ThrowableCatalog.GetLuredCompanion(kind);
            if (!wanted.HasValue)
            {
                return;
            }

            foreach (CompanionAgent agent in
                FindObjectsByType<CompanionAgent>(
                    FindObjectsSortMode.None))
            {
                if (agent.CompanionKind != wanted.Value)
                {
                    continue;
                }

                agent.GetComponent<CompanionLure>()?.TryLure(
                    position,
                    ThrowableCatalog.LureSeconds);
            }
        }

        private void Clear(int id)
        {
            if (!_traps.TryGetValue(id, out PlacedTrap trap))
            {
                return;
            }

            _traps.Remove(id);
            AnnounceTrapFired(trap);
            if (trap != null)
            {
                Destroy(trap.gameObject);
            }
        }

        /// <summary>
        /// The sound a prop makes when it goes off.
        ///
        /// Raised here, in the coordinator, because this is the only place that
        /// runs on both machines. <c>PlacedTrap.Triggered</c> would have been the
        /// obvious hook and is the wrong one: <see cref="TickTraps"/> is a
        /// host-side sweep by design, so a client would never hear the banana it
        /// just slipped on.
        ///
        /// Once per trap per machine. A trap is only ever cleared after it fires,
        /// and the entry is gone by the time a repeat could arrive — including
        /// the host receiving its own broadcast.
        ///
        /// The firework is absent on purpose. Its bang is announced from
        /// <see cref="ApplyBangLocally"/> instead, which is the only place that
        /// knows how far away the listener is.
        /// </summary>
        private static void AnnounceTrapFired(PlacedTrap trap)
        {
            if (trap == null)
            {
                return;
            }

            GameSoundId sound = trap.Kind switch
            {
                ThrowableKind.RubberChicken => GameSoundId.NoisePropSquawk,
                ThrowableKind.Banana => GameSoundId.TrapSlip,
                ThrowableKind.GlueTrap => GameSoundId.TrapSticky,
                ThrowableKind.SensorLight => GameSoundId.SensorTripped,
                _ => GameSoundId.None
            };

            GameSoundService.Request(sound);
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
                if (trap == null)
                {
                    continue;
                }

                // Two ways to go off, asked in one place. A fuse burns whether
                // or not anybody came near; a tripwire waits. The prop knows
                // which it is, so this does not branch on the kind.
                PlayerRoleIdentity victim = null;
                bool fired = trap.HasFuse
                    ? trap.TickFuse(Time.deltaTime)
                    : trap.TryTrigger(out victim);
                if (!fired)
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

                    // Announced here as well, because this branch is the one
                    // case that does not go through `Clear` — a sensor lingers
                    // so its lamp can be seen flashing. Without this the host
                    // hears every prop except the one it set off itself.
                    AnnounceTrapFired(spentTrap);
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
            TrapEffect effect = ThrowableCatalog.GetEffect(trap.Kind);

            // Noise first, and it needs no victim. A bang happens at a place,
            // not to a person — the one prop whose whole job is to be heard by
            // whoever happens to be near, including whoever set it off.
            if (effect == TrapEffect.Noise)
            {
                Bang(
                    trap.transform.position,
                    ThrowableCatalog.GetNoiseRadius(trap.Kind),
                    trap.PlacedBy);
                return;
            }

            if (victim == null)
            {
                return;
            }

            if (effect == TrapEffect.Reveal)
            {
                Reveal(
                    victim.Role,
                    trap.TrapId,
                    trap.transform.position,
                    ThrowableCatalog.RevealSeconds);
                return;
            }

            StunState stun = victim.GetComponent<StunState>();
            bool held = stun?.TryApply(
                ThrowableCatalog.GetStunSeconds(trap.Kind),
                ThrowableCatalog.GetStunCause(trap.Kind)) == true;

            // A glue trap that catches the thief takes money too, on the same
            // terms as a thrown rock — the officer's props should not be worth
            // less than their arm. A banana does not: the thief taking money off
            // the officer would mean the officer's equipment funds itself out of
            // its own failures.
            if (held && trap.PlacedBy == PlayerRole.Police)
            {
                PawliceAndPurrglar.Gameplay.Loot.LootConfiscationRule.Apply(
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
        /// <summary>
        /// Writes a noise down here and repeats it to the other machine.
        ///
        /// Sent rather than recomputed, for the reason every other crossing
        /// value in this project is sent: the client does not run the fuse and
        /// does not test the tripwire, so a client left to work it out would
        /// hear nothing and the prop would be a single-player prop.
        /// </summary>
        private void Bang(Vector3 at, float radius, PlayerRole madeBy)
        {
            ApplyBangLocally(at, radius, madeBy);

            NetworkManager manager = ResolveManager();
            if (manager == null
                || !manager.IsListening
                || !manager.IsServer
                || manager.CustomMessagingManager == null)
            {
                return;
            }

            using var writer = new FastBufferWriter(
                NoiseMessageBytes,
                Allocator.Temp);
            writer.WriteValueSafe(at.x);
            writer.WriteValueSafe(at.y);
            writer.WriteValueSafe(at.z);
            writer.WriteValueSafe(radius);
            writer.WriteValueSafe((int)madeBy);
            manager.CustomMessagingManager.SendNamedMessageToAll(
                NoiseMessageName,
                writer);
        }

        /// <summary>
        /// Past this, the bang is the distant recording rather than the near one.
        ///
        /// Two clips instead of one turned down: distance is mostly the loss of
        /// the high end and the arrival of a room, and a quiet copy of a close
        /// explosion just sounds like a close explosion somebody muted. Splitting
        /// them is the entire reason the sheet asked for two files.
        /// </summary>
        private const float FarBangMetres = 8f;

        private void ApplyBangLocally(
            Vector3 at,
            float radius,
            PlayerRole madeBy)
        {
            foreach (NoiseBoard board in
                FindObjectsByType<NoiseBoard>(FindObjectsSortMode.None))
            {
                board.Report(at, radius, madeBy);
            }

            // Both machines reach here — the host directly and the client
            // through the noise message — and each asks the question about its
            // own player. That is what makes the same explosion able to be near
            // on one screen and far on the other.
            //
            // Only the props whose effect is Noise come through here. Breaking
            // glass reports straight to the board instead, which is why this
            // cannot be hung on `NoiseBoard.Heard`: a smashed case would make
            // the sound of a firework.
            GameSoundService.Request(
                DistanceToLocalPlayer(at) > FarBangMetres
                    ? GameSoundId.NoiseHeardFar
                    : GameSoundId.NoisePropBang);
        }

        /// <summary>
        /// How far the person at this machine is from a point.
        ///
        /// Returns zero when there is nobody to ask, so an unattended scene hears
        /// the near version — the alternative is silence in the editor, which
        /// reads as the sound being broken.
        /// </summary>
        private static float DistanceToLocalPlayer(Vector3 at)
        {
            PlayerRole role = LocalPlayerRoleSelector.OverriddenRole
                ?? PlayerRole.Police;
            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role == role)
                {
                    return Vector3.Distance(
                        candidate.transform.position,
                        at);
                }
            }

            return 0f;
        }

        private void HandleNoise(
            ulong senderClientId,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float radius);
            reader.ReadValueSafe(out int madeBy);

            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsServer)
            {
                // The host already wrote it down when it happened. Writing it
                // again on the way past would double every count.
                return;
            }

            ApplyBangLocally(
                new Vector3(x, y, z),
                radius,
                (PlayerRole)madeBy);
        }

        /// <summary>
        /// Reveals a role from something that is not a placed prop — the display
        /// case's alarm, for now.
        ///
        /// Public because the alternative was for each source to sweep the
        /// scene itself, and the alarm's own attempt at that was wrong in a way
        /// nothing could see (it asked the thief for a component only the
        /// officer has). Going through here also means the reveal **replicates**,
        /// which the alarm's local sweep never did: the thief's screen has to
        /// know it is lit up, and that is the whole point of telling them.
        /// </summary>
        public void RevealRole(
            PlayerRole revealed,
            float seconds,
            Vector3 source)
        {
            Reveal(revealed, NoTrap, source, seconds);
        }

        private void Reveal(
            PlayerRole revealed,
            int trapId,
            Vector3 source,
            float seconds)
        {
            ApplyRevealLocally(revealed, trapId, source, seconds);

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
            writer.WriteValueSafe(seconds);
            manager.CustomMessagingManager.SendNamedMessageToAll(
                RevealMessageName,
                writer);
        }

        private void ApplyRevealLocally(
            PlayerRole revealed,
            int trapId,
            Vector3 source,
            float seconds)
        {
            // The component sits on the watcher and hides the other side, so the
            // one to switch off is the one that is not the revealed player. That
            // rule lives on FlashlightVisibility now, because it was written
            // twice and the second copy had it backwards.
            FlashlightVisibility.RevealRole(revealed, seconds, source);

            if (_traps.TryGetValue(trapId, out PlacedTrap trap)
                && trap != null)
            {
                trap.GetComponent<
                    PawliceAndPurrglar.Animation.PlacedTrapView>()?.Flash();
            }
        }

        private void HandleRevealed(
            ulong sender,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out int revealed);
            reader.ReadValueSafe(out int trapId);
            reader.ReadValueSafe(out Vector3 source);
            reader.ReadValueSafe(out float seconds);

            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsServer)
            {
                // The host already applied it when it decided.
                return;
            }

            ApplyRevealLocally(
                (PlayerRole)revealed,
                trapId,
                source,
                seconds);
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
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                NoiseMessageName,
                HandleNoise);
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

        private void PruneMissingPickups()
        {
            _watchedPickups.RemoveWhere(pickup => pickup == null);
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
