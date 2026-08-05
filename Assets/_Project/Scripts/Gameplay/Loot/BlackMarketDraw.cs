using System.Collections.Generic;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// Opens two of the five black markets each match, and closes the rest.
    ///
    /// The thief's selling point is the one place they have to come back to, so
    /// a fixed one is a place the officer can simply stand on. Standing on the
    /// selling point is the strongest thing an officer can do in this game and
    /// the least interesting. Five places and two open makes ten combinations —
    /// too many to learn, few enough that each place keeps a character — and the
    /// pair means the thief always has a choice of route rather than one correct
    /// one.
    ///
    /// **The host draws; the choice is replicated.** This is the part that had to
    /// be done carefully. Being switched off is not a fact that replicates:
    /// where something is does, which is why the treasure draw could simply move
    /// pieces and let the network carry it. Two machines each drawing two from
    /// five would show the thief a bin the host had never opened, and the thief
    /// would stand at it selling nothing.
    ///
    /// So the host writes a mask and both sides read it. Nobody draws twice and
    /// nobody guesses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackMarketDraw : MonoBehaviour
    {
        /// <summary>
        /// How many open at once. Two, so there is a choice; not three, or the
        /// officer stops having to guess at all.
        /// </summary>
        public const int OpenCount = 2;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        /// <summary>
        /// Every place a market might be, in a fixed order. The order is what
        /// the mask's bits mean, so it must not be shuffled here.
        /// </summary>
        [SerializeField]
        private List<GameObject> markets = new();

        private IMatchStateReader _matchState;
        private int _appliedMask = -1;
        private bool _drawn;

        public int MarketCount => markets.Count;
        public int AppliedMask => _appliedMask;

        public void Configure(
            IMatchStateReader configuredMatchState,
            IEnumerable<GameObject> configuredMarkets)
        {
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            markets = new List<GameObject>(configuredMarkets);
        }

        private void Update()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            if (_matchState == null || markets.Count == 0)
            {
                return;
            }

            if (_matchState.CurrentState != MatchState.Playing)
            {
                // Rearmed for a rematch, and everything opened again so the
                // lobby and the countdown do not show a town with three bins
                // missing for no reason anybody can see.
                if (_drawn)
                {
                    _drawn = false;
                    Apply(AllOpenMask());
                }

                return;
            }

            Integration.Network.NetworkMatchMirror mirror = ResolveMirror();

            // The host draws once, and only the host.
            if (!_drawn && (mirror == null || mirror.IsServer))
            {
                _drawn = true;
                int mask = Draw();
                mirror?.SetOpenMarkets(mask);
                Apply(mask);
                return;
            }

            // Everybody else waits to be told, and applies whatever arrives.
            // Zero means the host has not drawn yet — not "close everything".
            if (mirror != null && mirror.OpenMarkets != 0)
            {
                Apply(mirror.OpenMarkets);
            }
        }

        /// <summary>
        /// Two places, as bits. Picked by removing from a working list so the
        /// same place cannot come up twice.
        /// </summary>
        private int Draw()
        {
            var remaining = new List<int>();
            for (int index = 0; index < markets.Count; index++)
            {
                remaining.Add(index);
            }

            int mask = 0;
            for (int taken = 0;
                taken < OpenCount && remaining.Count > 0;
                taken++)
            {
                int slot = Random.Range(0, remaining.Count);
                mask |= 1 << remaining[slot];
                remaining.RemoveAt(slot);
            }

            return mask;
        }

        private int AllOpenMask()
        {
            int mask = 0;
            for (int index = 0; index < markets.Count; index++)
            {
                mask |= 1 << index;
            }

            return mask;
        }

        private void Apply(int mask)
        {
            if (_appliedMask == mask)
            {
                return;
            }

            _appliedMask = mask;
            var opened = new List<string>();
            for (int index = 0; index < markets.Count; index++)
            {
                if (markets[index] == null)
                {
                    continue;
                }

                bool open = (mask & (1 << index)) != 0;
                markets[index].SetActive(open);
                if (open)
                {
                    opened.Add(markets[index].name);
                }
            }

            GameLogger.Info(
                GameLogCategory.Loot,
                $"Black market open at: {string.Join(", ", opened)}.",
                this);
        }

        /// <summary>
        /// The one thing in the match scene the server writes and everybody
        /// reads. There is exactly one, so finding it by type is safe here in a
        /// way it would not be for anything the map has several of.
        /// </summary>
        private Integration.Network.NetworkMatchMirror ResolveMirror()
        {
            return FindFirstObjectByType<
                Integration.Network.NetworkMatchMirror>();
        }
    }
}
