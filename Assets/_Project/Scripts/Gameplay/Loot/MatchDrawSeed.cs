using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// The one number every random draw in a match starts from.
    ///
    /// <b>Why this exists.</b> Two machines each calling
    /// <c>UnityEngine.Random</c> get two different answers, and the project has
    /// met that from four directions already — two rooms shuffling one set of
    /// objects, two markets opening on one screen, and a cupboard seeded from
    /// <c>ContainerId.GetHashCode()</c>, which .NET randomises **per process**,
    /// so it does not even agree with itself across two runs of the same build.
    ///
    /// The fix everywhere else was "the host decides and the choice replicates".
    /// That works when the choice is small enough to send — a two-bit market
    /// mask is. A room's layout is not, and sending one position per piece per
    /// room is a message format to keep in step for the rest of the project.
    ///
    /// So the host sends the *seed* and both machines run the same draw. One
    /// integer covers every draw in the match, present and future, and the two
    /// screens agree by construction rather than by a correction arriving a
    /// frame later.
    ///
    /// <b>Offline still works.</b> With no session the seed is rolled here, once
    /// per run, and everything downstream is unchanged.
    /// </summary>
    public static class MatchDrawSeed
    {
        /// <summary>
        /// Not yet known. Zero rather than a sentinel because that is what an
        /// unwritten <c>NetworkVariable&lt;int&gt;</c> already reads as, and a
        /// draw that ran on zero would be the same layout every match.
        /// </summary>
        public const int Unknown = 0;

        private static int _offlineSeed = Unknown;

        /// <summary>
        /// The match seed, or <see cref="Unknown"/> while the host has not
        /// published one yet. Callers wait rather than guess.
        /// </summary>
        public static int Current
        {
            get
            {
                Integration.Network.NetworkMatchMirror mirror =
                    Object.FindFirstObjectByType<
                        Integration.Network.NetworkMatchMirror>();

                if (mirror != null && mirror.IsSpawned)
                {
                    return mirror.MatchSeed;
                }

                // No session: a test, or the single-player playtest. Rolled once
                // so every room in the same run shares it, exactly as they would
                // share the host's.
                if (_offlineSeed == Unknown)
                {
                    _offlineSeed = Random.Range(1, int.MaxValue);
                }

                return _offlineSeed;
            }
        }

        /// <summary>
        /// Forgets the offline seed so the next run draws differently. Called
        /// when a match ends; harmless in a session, where the host owns it.
        /// </summary>
        public static void ClearOfflineSeed()
        {
            _offlineSeed = Unknown;
        }

        /// <summary>
        /// A seed for one drawer inside a match.
        ///
        /// Mixed with a name rather than shared, or every room in the town would
        /// deal its pieces into the same numbered shelves — thirteen rooms with
        /// identical layouts is a more obvious bug than thirteen wrong ones, but
        /// it is still a bug.
        /// </summary>
        public static int For(int matchSeed, string key)
        {
            return matchSeed ^ StableHash(key);
        }

        /// <summary>
        /// FNV-1a, because <c>string.GetHashCode()</c> is randomised per process
        /// on .NET. Two machines hashing the same container id got two different
        /// numbers, which is precisely the failure a hash was chosen to avoid.
        /// </summary>
        public static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;

                uint hash = offsetBasis;
                foreach (char character in value)
                {
                    hash ^= character;
                    hash *= prime;
                }

                return (int)hash;
            }
        }
    }
}
