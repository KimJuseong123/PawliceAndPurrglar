using System.Collections.Generic;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// Deals a room's treasure out over its shelves at the start of a match.
    ///
    /// Seven places in the jeweller's and four pieces to put in them, so the
    /// room is never the same twice. Neither player can learn it: the officer
    /// cannot camp the gold bar's spot because there is no gold bar's spot, and
    /// the thief cannot run a route from memory. What they both keep is the
    /// shape of the room and the case in the middle.
    ///
    /// The host draws and nobody else. Positions replicate host to client, so
    /// one machine choosing and the other following is the whole mechanism —
    /// two machines each shuffling would put the same ring in two rooms, which
    /// is the failure this project has met from four directions.
    ///
    /// Redealt for a rematch. A room that keeps the first match's layout is a
    /// room the second match already knows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootSpotDraw : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour matchStateSource;

        /// <summary>
        /// Where a piece may stand. More of these than pieces, or there is
        /// nothing to choose between.
        /// </summary>
        [SerializeField]
        private List<Transform> spots = new();

        /// <summary>
        /// What gets dealt. One of each: the point is four different things in
        /// four different places, not four chances at the same thing.
        /// </summary>
        [SerializeField]
        private List<LootItem> pieces = new();

        private IMatchStateReader _matchState;
        private bool _dealt;

        public bool HasDealt => _dealt;
        public int SpotCount => spots.Count;
        public int PieceCount => pieces.Count;

        /// <summary>
        /// How many of this room's pieces are still lying in it.
        ///
        /// Read by the officer's screen, which is the only way they can tell an
        /// empty room from a room the thief has already been through — the two
        /// look identical from the doorway, and a room whose shelves are bare
        /// because nothing was ever dealt there says nothing about where the
        /// thief has been.
        ///
        /// Counted rather than tracked, because a piece leaves a room by four
        /// different routes — picked up, hidden, sold, confiscated — and a counter
        /// decremented at each of them is four places to forget one.
        /// </summary>
        public int RemainingCount
        {
            get
            {
                int remaining = 0;
                foreach (LootItem piece in pieces)
                {
                    if (piece != null
                        && piece.CurrentCarrier == null
                        && piece.CurrentState == LootState.Available)
                    {
                        remaining++;
                    }
                }

                return remaining;
            }
        }

        public void Configure(
            IMatchStateReader configuredMatchState,
            IEnumerable<Transform> configuredSpots,
            IEnumerable<LootItem> configuredPieces)
        {
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            spots = new List<Transform>(configuredSpots);
            pieces = new List<LootItem>(configuredPieces);
        }

        private void Update()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            if (_matchState == null)
            {
                return;
            }

            if (_matchState.CurrentState != MatchState.Playing)
            {
                // Rearmed, so a rematch deals again instead of replaying the
                // layout the last match taught both players.
                _dealt = false;
                return;
            }

            if (_dealt)
            {
                return;
            }

            // Latched after the decision, not before.
            //
            // It used to set this first and then ask whether this machine deals,
            // which means one transient answer switches the draw off for the whole
            // match. On the frame a match starts the player objects may not have
            // spawned yet, so "am I the authority" is being asked of a scene that
            // cannot answer — and a room that misses that one frame stays as it was
            // authored for the rest of the match, with nothing at the marked places
            // and no log to say why.
            if (!HasAuthority())
            {
                return;
            }

            _dealt = true;
            Deal();
        }

        /// <summary>
        /// Only the machine that decides where things are.
        ///
        /// Asked of the thief's interior state, which is the one component in
        /// the scene that already knows whether this machine is the authority.
        /// A client that dealt for itself would disagree with the host about
        /// which room holds the ring.
        /// </summary>
        private PlayerInteriorState _authority;

        private bool HasAuthority()
        {
            // Remembered once found. This is asked every frame until the deal
            // happens, and on a machine that never deals that is for the rest of
            // the match — a scene-wide search per room per frame is not a thing to
            // leave running on a build whose first target is WebGL.
            if (_authority == null)
            {
                foreach (PlayerInteriorState state in
                    FindObjectsByType<PlayerInteriorState>(
                        FindObjectsSortMode.None))
                {
                    _authority = state;
                    break;
                }
            }

            // Nothing to ask means nothing is replicating either — an offline
            // scene or a test — and then dealing is right.
            return _authority == null || _authority.HasAuthority;
        }

        private void Deal()
        {
            if (spots.Count < pieces.Count)
            {
                GameLogger.Warning(
                    GameLogCategory.Loot,
                    $"{name} has {pieces.Count} pieces and only "
                    + $"{spots.Count} places to put them, so some of the room's "
                    + "treasure has nowhere to appear.",
                    this);
            }

            // Shuffled by picking, so a spot cannot come up twice. Random.Range
            // rather than an ordering trick: the draw has to be different every
            // match and readable by whoever reads this next.
            var remaining = new List<Transform>(spots);
            int dealt = 0;
            foreach (LootItem piece in pieces)
            {
                if (piece == null || remaining.Count == 0)
                {
                    continue;
                }

                int index = Random.Range(0, remaining.Count);
                Transform spot = remaining[index];
                remaining.RemoveAt(index);
                if (spot == null)
                {
                    continue;
                }

                piece.transform.position = spot.position;
                dealt++;
            }

            GameLogger.Info(
                GameLogCategory.Loot,
                $"{name} dealt {dealt} pieces over {spots.Count} places.",
                this);
        }
    }
}
