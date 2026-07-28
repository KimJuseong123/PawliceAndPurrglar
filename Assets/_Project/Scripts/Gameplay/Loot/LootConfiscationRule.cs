using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// Turns a landed police hit into money moving.
    ///
    /// The thief loses half of what they have banked and the officer keeps a
    /// fifth of it; the difference leaves the match. Two fractions rather than
    /// one on purpose — a police purse funded pound for pound out of the thief's
    /// takings would make a hit a transfer rather than a setback, and the thief
    /// would still be on course to win while paying for the equipment used
    /// against them.
    ///
    /// Only the police taking money off the thief, never the reverse. The thief's
    /// props buy them seconds, which is their currency; giving them the officer's
    /// purse as well would mean the officer's equipment funds itself out of its
    /// own failures.
    ///
    /// Host side, like every other rule here. It hangs off the stun rather than
    /// the throw, which means the existing re-stun gap doubles as the limit on
    /// how often money can be taken — without that an officer with a rock could
    /// empty the thief in a few seconds.
    /// </summary>
    public static class LootConfiscationRule
    {
        /// <summary>
        /// From the brief: the thief loses 50% and the police recovers 20%.
        /// </summary>
        public const float LostFraction = 0.5f;
        public const float RecoveredFraction = 0.2f;

        /// <summary>
        /// Applies the transfer and returns what the officer gained.
        ///
        /// Returns zero when there was nothing to take, which is the common case
        /// early in a match — a thief who has sold nothing loses nothing, so the
        /// first hit of a match costs them only the stun.
        /// </summary>
        public static int Apply(
            PlayerRoleIdentity victim,
            PlayerRoleIdentity beneficiary)
        {
            if (victim == null
                || beneficiary == null
                || victim.Role != PlayerRole.Thief
                || beneficiary.Role != PlayerRole.Police)
            {
                return 0;
            }

            var wallet = victim.GetComponent<ThiefLootWallet>();
            if (wallet == null)
            {
                return 0;
            }

            int recovered = wallet.Confiscate(
                LostFraction,
                RecoveredFraction);
            if (recovered <= 0)
            {
                return 0;
            }

            beneficiary.GetComponent<PoliceWallet>()?.Recover(recovered);
            return recovered;
        }
    }
}
