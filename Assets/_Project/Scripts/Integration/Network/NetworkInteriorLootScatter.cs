using System.Collections.Generic;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Scatters loot around the house interiors when a match starts, and tells the
    /// other machine exactly where it went.
    ///
    /// The host rolls and announces rather than both machines rolling the same
    /// seed. A shared seed is smaller on the wire but only works while every step
    /// that consumes it stays bit-identical on both sides, and one extra call or a
    /// reordered loop silently puts the loot in different rooms on two screens —
    /// the thief picks up something the officer never saw. Announcing the answer
    /// cannot drift.
    ///
    /// Named messages rather than spawned objects, for the reason everything else
    /// here uses them: dynamic <c>NetworkObject</c>s are what went wrong in
    /// ISSUE-016.
    ///
    /// Offline it still works. With no session running the host branch is the only
    /// branch, so the single-player playtest gets its scatter too.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkInteriorLootScatter : MonoBehaviour
    {
        public const string ScatterMessageName =
            "PawsAndLoot.InteriorLoot";
        public const string TakenMessageName =
            "PawsAndLoot.InteriorLootTaken";

        /// <summary>
        /// Money per piece, and the reason it is small.
        ///
        /// It has to hold one property: a thief who empties every room in the town
        /// still cannot win on rooms alone. Twenty-eight pieces across fourteen
        /// rooms comes to 140 against a 1,000 target, so real treasure still has to
        /// cross the town.
        ///
        /// It was 50 while eight houses had insides, which was 800 and fine. Opening
        /// every house took the same figure to 1,900 and quietly made the rooms a
        /// way around the game — caught by the test that asserts the property rather
        /// than the number.
        ///
        /// It became 5 when treasure prices were cut to a fifth. This figure has to
        /// move with them, and not because the total drifts: pocketing costs no
        /// carry penalty, no merchant trip and no risk of being robbed on the way,
        /// so it has to pay clearly less than the cheapest thing that does. Left at
        /// 20 against a 40-gold Common it would have paid half as much for none of
        /// the work, and nobody would ever have carried anything again — which is
        /// the sentence this constant was written to prevent.
        /// </summary>
        public const int ValuePerPiece = 5;

        /// <summary>
        /// One int for the count, then an interior id and a position each.
        /// Asked of the serialiser rather than counted by hand — counting it by
        /// hand is what left the trap message four bytes short of a Vector3.
        /// </summary>
        public static int MessageBytesFor(int count)
        {
            return FastBufferWriter.GetWriteSize<int>()
                + count * (
                    FastBufferWriter.GetWriteSize<int>()
                    + FastBufferWriter.GetWriteSize<Vector3>());
        }

        [SerializeField]
        private MonoBehaviour matchStateSource;

        /// <summary>
        /// How many pieces go in each room. Two is enough that a room is worth
        /// entering and few enough that clearing one is not the whole match.
        /// </summary>
        [SerializeField, Min(1)]
        private int piecesPerInterior = 2;

        [SerializeField, Min(0.2f)]
        private float clearanceMargin = 1.2f;

        private readonly List<GameObject> _spawned = new();
        private readonly Dictionary<int, InteriorValuablePickup>
            _pickups = new();
        private IMatchStateReader _matchState;
        private NetworkManager _networkManager;
        private bool _registered;
        private bool _scattered;
        private Material _material;

        public int ScatteredCount => _spawned.Count;

        public void Configure(
            IMatchStateReader configuredMatchState,
            Material configuredMaterial)
        {
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            _material = configuredMaterial;
        }

        /// <summary>
        /// Host side. Picks the spots and announces them.
        ///
        /// Rolled from the clock rather than a fixed seed, so two matches in a row
        /// are not the same search — the whole point of scattering is that the
        /// thief cannot learn the map once.
        /// </summary>
        private void ScatterOnHost()
        {
            HouseInterior[] interiors =
                FindObjectsByType<HouseInterior>(
                    FindObjectsSortMode.None);
            if (interiors.Length == 0)
            {
                return;
            }

            var random = new System.Random(
                (int)(Time.realtimeSinceStartup * 1000f) ^ 0x5f3a);
            var ids = new List<int>();
            var spots = new List<Vector3>();

            foreach (HouseInterior interior in interiors)
            {
                for (int piece = 0; piece < piecesPerInterior; piece++)
                {
                    Vector3 spot = FindClearSpot(interior, random);
                    ids.Add(interior.InteriorId);
                    spots.Add(spot);
                }
            }

            for (int index = 0; index < ids.Count; index++)
            {
                Spawn(ids[index], spots[index]);
            }

            NetworkManager manager = ResolveManager();
            if (manager == null
                || !manager.IsListening
                || !manager.IsServer)
            {
                return;
            }

            using var writer = new FastBufferWriter(
                MessageBytesFor(ids.Count),
                Allocator.Temp);
            writer.WriteValueSafe(ids.Count);
            for (int index = 0; index < ids.Count; index++)
            {
                writer.WriteValueSafe(ids[index]);
                writer.WriteValueSafe(spots[index]);
            }

            manager.CustomMessagingManager.SendNamedMessageToAll(
                ScatterMessageName,
                writer);
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Scattered {ids.Count} interior loot pieces.",
                this);
        }

        /// <summary>
        /// A floor spot with nothing already standing in it.
        ///
        /// Re-measured rather than trusted, because a piece of loot inside a
        /// wardrobe is unreachable and looks exactly like one that does not work —
        /// four of five rocks were sealed inside buildings once and every check
        /// the project had passed anyway.
        /// </summary>
        private Vector3 FindClearSpot(
            HouseInterior interior,
            System.Random random)
        {
            Vector3 fallback =
                interior.SampleFloorPoint(random, clearanceMargin);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector3 candidate =
                    interior.SampleFloorPoint(random, clearanceMargin);
                Vector3 probe = candidate + Vector3.up * 0.4f;
                if (Physics.OverlapSphere(
                        probe,
                        0.45f,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Ignore).Length == 0)
                {
                    return candidate;
                }
            }

            return fallback;
        }

        /// <summary>
        /// Builds the visible piece. Runs on every machine so both draw the same
        /// loot in the same place.
        /// </summary>
        private void Spawn(int interiorId, Vector3 position)
        {
            int sourceId = _spawned.Count + 1;
            var piece = new GameObject(
                $"Interior Loot {interiorId}.{sourceId}");
            piece.transform.position = position + Vector3.up * 0.3f;

            // A trigger, not a solid: something to walk up to, never something
            // to trip over in a room being chased through.
            SphereCollider trigger = piece.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.5f;

            var presentation = new GameObject("Visual");
            presentation.transform.SetParent(piece.transform, false);

            GameObject visual = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            visual.transform.SetParent(presentation.transform, false);
            visual.transform.localScale = Vector3.one * 0.55f;
            Destroy(visual.GetComponent<Collider>());
            if (_material != null)
            {
                visual.GetComponent<Renderer>().sharedMaterial = _material;
            }

            InteriorValuablePickup pickup =
                piece.AddComponent<InteriorValuablePickup>();
            pickup.Configure(
                sourceId,
                ValuePerPiece,
                ResolveMatchState(),
                presentation.transform);
            pickup.Taken += HandleTakenLocally;

            _pickups[sourceId] = pickup;
            _spawned.Add(piece);
        }

        private void HandleScatter(ulong sender, FastBufferReader reader)
        {
            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsServer)
            {
                // The host already built its own copies when it rolled them.
                return;
            }

            reader.ReadValueSafe(out int count);
            for (int index = 0; index < count; index++)
            {
                reader.ReadValueSafe(out int interiorId);
                reader.ReadValueSafe(out Vector3 spot);
                Spawn(interiorId, spot);
            }

            _scattered = true;
        }

        /// <summary>
        /// Announces a shelf that has just been emptied.
        ///
        /// The money is already replicated through the thief's total, but the
        /// object is not: without this the piece stays sitting there on the other
        /// screen, which is exactly the split that made a picked-up rock look like
        /// a rock that would not pick up.
        /// </summary>
        private void HandleTakenLocally(int sourceId)
        {
            NetworkManager manager = ResolveManager();
            if (manager == null
                || !manager.IsListening
                || !manager.IsServer)
            {
                return;
            }

            using var writer = new FastBufferWriter(
                FastBufferWriter.GetWriteSize<int>(),
                Allocator.Temp);
            writer.WriteValueSafe(sourceId);
            manager.CustomMessagingManager.SendNamedMessageToAll(
                TakenMessageName,
                writer);
        }

        private void HandleTakenMessage(
            ulong sender,
            FastBufferReader reader)
        {
            reader.ReadValueSafe(out int sourceId);

            NetworkManager manager = ResolveManager();
            if (manager != null && manager.IsServer)
            {
                // The host emptied it when it decided.
                return;
            }

            if (_pickups.TryGetValue(sourceId, out InteriorValuablePickup p)
                && p != null)
            {
                p.ApplyReplicatedTaken(true);
            }
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
                ScatterMessageName,
                HandleScatter);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                TakenMessageName,
                HandleTakenMessage);
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

            if (_scattered
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                return;
            }

            // Only the simulating machine rolls. A client waits to be told.
            bool simulating = manager == null
                || !manager.IsListening
                || manager.IsServer;
            if (!simulating)
            {
                return;
            }

            _scattered = true;
            ScatterOnHost();
        }
    }
}
