using System.Collections.Generic;
using PawliceAndPurrglar.Logging;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Loot
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
    /// <b>Both machines draw, from the host's seed.</b> It used to be "the host
    /// draws and the positions replicate", which worked — the pieces carry a
    /// <c>NetworkLootLink</c> and their positions do arrive — but only for what
    /// the link happens to send. The link moves the piece's *presentation*, so a
    /// client's <c>LootItem</c> transform stayed wherever the scene was authored
    /// and the two machines disagreed about everything except the picture:
    /// nothing on the client could ask where a piece was and get the same answer
    /// the host had. It also meant a room could not draw anything the network
    /// was not already carrying.
    ///
    /// Seeding both draws from one host-rolled number costs one integer and
    /// makes the two rooms identical by construction rather than by a correction
    /// arriving a frame later. Nobody waits for a position to catch up because
    /// nobody was told a position.
    ///
    /// Redealt for a rematch. A room that keeps the first match's layout is a
    /// room the second match already knows — the seed is cleared with the match
    /// state, so this falls out rather than needing its own rule.
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
                // layout the last match taught both players. In a session the
                // host clears its seed on the same transition; offline there is
                // no host, so the seed is dropped here. Every room does this on
                // the same frame and the first one to ask for a seed next match
                // rolls the one they all share.
                _dealt = false;
                MatchDrawSeed.ClearOfflineSeed();
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
            // spawned yet, so the question was being asked of a scene that cannot
            // answer — and a room that misses that one frame stays as it was
            // authored for the rest of the match, with nothing at the marked places
            // and no log to say why. The question is now "has the host told us the
            // seed", which has the same shape and the same trap.
            int matchSeed = MatchDrawSeed.Current;
            if (matchSeed == MatchDrawSeed.Unknown)
            {
                return;
            }

            _dealt = true;
            _seed = MatchDrawSeed.For(matchSeed, name);
            Deal(new System.Random(_seed));
        }

        /// <summary>
        /// What this room drew from, logged so two machines can be compared
        /// without a screenshot. The counts alone match whether or not the
        /// layouts do.
        /// </summary>
        private int _seed;

        public int Seed => _seed;

        private void Deal(System.Random random)
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

            // Shuffled by picking, so a spot cannot come up twice. The generator
            // is passed in rather than taken from UnityEngine.Random: the draw
            // has to be different every match *and the same on both machines*,
            // and only the second of those needs saying because the first is
            // what everybody assumes a shuffle does.
            var remaining = new List<Transform>(spots);
            int dealt = 0;
            foreach (LootItem piece in pieces)
            {
                if (piece == null || remaining.Count == 0)
                {
                    continue;
                }

                int index = random.Next(remaining.Count);
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
                $"{name} dealt {dealt} pieces over {spots.Count} places "
                + $"from seed {_seed}.",
                this);
        }
    }
}
