using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Loot
{
    /// <summary>
    /// Whether this player is carrying the jeweller's key.
    ///
    /// One key, held or not held, rather than a count. A second key would let
    /// the thief open both cases quietly on one trip, and the whole point of the
    /// key is that it makes one theft quiet and the next one loud — the choice is
    /// which case is worth the silence.
    ///
    /// Not a throwable and not loot. It cannot be thrown, it cannot be sold, and
    /// carrying it costs no speed: a key that slowed the thief down would simply
    /// never be worth fetching, and one that could be sold would be a two-hundred
    /// gold pickup that happens to open doors.
    ///
    /// Spent on use. A key that stays in the pocket turns every case in the game
    /// quiet after one walk to the counter, and then breaking glass is a mistake
    /// rather than a decision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisplayCaseKeyHolder : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private bool hasKey;

        public bool HasKey => hasKey;

        public PlayerRole? Role => identity != null ? identity.Role : null;

        public void Configure(PlayerRoleIdentity configuredIdentity)
        {
            identity = configuredIdentity;
        }

        private void Awake()
        {
            identity ??= GetComponent<PlayerRoleIdentity>();
        }

        /// <summary>
        /// Takes a key, and says whether there was room for it.
        ///
        /// Refused when one is already held, so a second key pickup cannot be
        /// banked. The pickup reads the answer to decide whether to disappear —
        /// a key that vanishes without being carried is a key the thief walked
        /// to for nothing.
        /// </summary>
        public bool TryTake()
        {
            if (hasKey)
            {
                return false;
            }

            hasKey = true;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"{Role} picked up the display case key.",
                this);
            return true;
        }

        public bool TrySpend()
        {
            if (!hasKey)
            {
                return false;
            }

            hasKey = false;
            return true;
        }

        /// <summary>
        /// Emptied for a fresh match, like every other carried thing. A key held
        /// across a rematch would open the first case of the next match for free.
        /// </summary>
        public void Clear()
        {
            hasKey = false;
        }
    }
}
