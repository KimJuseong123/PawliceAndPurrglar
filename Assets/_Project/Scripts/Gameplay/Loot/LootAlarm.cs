using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Gameplay.Sensing;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    /// <summary>
    /// What happens when somebody lifts the flagship piece.
    ///
    /// The design document describes it as a chain: the case opens, the ring
    /// comes off its cushion, a siren goes, the officer is told where, and for
    /// a while they are faster than the thief. It is the one moment in a match
    /// where the map stops being symmetric — the thief has chosen to be found
    /// in exchange for the best payday on it.
    ///
    /// That trade only works if the exchange is real on both halves. An alarm
    /// that only sped the officer up would be a punishment; one that only lit
    /// the thief up would be a tax. Together they are a decision: take the
    /// small things quietly, or take the big one and run.
    ///
    /// Host-authoritative and raised once. Something that fires per frame while
    /// the ring is held would keep the officer permanently fast, which reads as
    /// the officer being broken rather than as the alarm working.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootAlarm : MonoBehaviour
    {
        /// <summary>
        /// How long the officer keeps the extra pace.
        ///
        /// Twelve seconds is roughly a run across two blocks. Long enough that
        /// the thief cannot simply wait it out behind the nearest wall, short
        /// enough that a thief who gets clear has genuinely got clear.
        /// </summary>
        public const float PoliceBoostSeconds = 12f;

        /// <summary>
        /// How much faster.
        ///
        /// A quarter again. Deliberately less than the gap a bulky treasure
        /// already opens — the alarm should make the chase winnable, not
        /// decided. Catching the thief still has to be done.
        /// </summary>
        public const float PoliceBoostMultiplier = 1.25f;

        /// <summary>
        /// How long the thief stays lit up.
        ///
        /// Shorter than the officer's advantage. Being seen at the moment of
        /// the theft is the cost; being seen for the whole escape would mean
        /// there was no escape to play.
        /// </summary>
        public const float ThiefRevealSeconds = 4f;

        /// <summary>
        /// How far the siren carries. The whole town, in practice — an alarm
        /// nobody hears is a light on a box.
        /// </summary>
        public const float SirenRadiusMeters = 120f;

        /// <summary>
        /// How long the officer's screen keeps pointing at the shop.
        ///
        /// Longer than the four seconds the thief stays lit, and deliberately so
        /// — those two answer different questions. The reveal says "there they
        /// are", and it is short because being seen for a whole escape would
        /// mean there was no escape to play. The beacon says "it happened
        /// *here*", and that stays true after the thief has run off; ten seconds
        /// is about how long it takes to cross two blocks and look.
        /// </summary>
        public const float BeaconSeconds = 10f;

        [SerializeField]
        private NoiseBoard noiseBoard;

        private float _beaconUntil;

        /// <summary>
        /// Whether the officer should still be shown which shop went off.
        /// </summary>
        public bool IsBeaconActive => Time.time < _beaconUntil;

        /// <summary>
        /// Where it went off. Meaningful only while the beacon is active.
        /// </summary>
        public Vector3 BeaconSource { get; private set; }

        /// <summary>
        /// How much of the beacon is left, for anything that wants to fade.
        /// </summary>
        public float BeaconRemainingSeconds =>
            Mathf.Max(0f, _beaconUntil - Time.time);

        public event Action<Vector3> Raised;

        /// <summary>
        /// How many times an alarm has gone off this match. Latched rather than
        /// a flag, so a test can tell "it never fired" from "it fired once and
        /// has since finished".
        /// </summary>
        public int RaisedCount { get; private set; }

        public Vector3 LastRaisedAt { get; private set; }

        public void Configure(NoiseBoard configuredBoard)
        {
            noiseBoard = configuredBoard;
        }

        /// <summary>
        /// Sounds the siren for a piece taken at this spot.
        ///
        /// Takes the position rather than reading it off the thief, because the
        /// alarm belongs to the shop: it should point at the empty cushion, not
        /// at wherever the thief has got to by the time it is heard.
        /// </summary>
        public void Raise(Vector3 at, PlayerRoleIdentity thief)
        {
            RaisedCount++;
            LastRaisedAt = at;
            BeaconSource = at;
            _beaconUntil = Time.time + BeaconSeconds;

            ResolveNoiseBoard()?.Report(
                at,
                SirenRadiusMeters,
                PlayerRole.Thief,
                ThiefRevealSeconds);

            foreach (PlayerRoleIdentity player in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (player.Role == PlayerRole.Police)
                {
                    player.GetComponent<PlayerMovementMotor>()?.ApplyBoost(
                        PoliceBoostMultiplier,
                        PoliceBoostSeconds);
                }
            }

            // Lit from the cushion rather than from wherever they are standing,
            // so the mark on the officer's screen is the place worth running to
            // and not a place the thief has already left.
            //
            // This used to ask the thief for their own FlashlightVisibility.
            // **Only the officer has one** — the component sits on the watcher
            // and hides the other side — so the null-conditional swallowed the
            // whole thing and the alarm's four seconds of exposure never
            // happened, not once, since the day it was written. The siren still
            // sounded and the officer still got their sprint, which is why it
            // read as working.
            //
            // Through the coordinator rather than a local sweep, because the
            // alarm is raised on the host and the person being exposed is on the
            // other machine. Telling only the host is telling nobody who needed
            // to hear it.
            // Whoever actually lifted it, rather than an assumption that it
            // was the thief. It always is today — only the thief can take a
            // piece — but an alarm that names the wrong role would expose the
            // wrong player, and that is a silent failure of exactly the kind
            // this line already made once.
            RevealLifter(
                thief != null ? thief.Role : PlayerRole.Thief,
                at);

            GameLogger.Info(
                GameLogCategory.Loot,
                $"Alarm raised at {at}. Police boosted for "
                + $"{PoliceBoostSeconds}s.",
                this);
            Raised?.Invoke(at);
        }

        /// <summary>
        /// How many watchers the last alarm switched off. Zero means nobody was
        /// exposed, which is what this did for its whole existence — latched so
        /// a test can tell that apart from "the alarm never fired".
        /// </summary>
        public int LastRevealedWatchers { get; private set; }

        private void RevealLifter(PlayerRole lifter, Vector3 at)
        {
            Integration.Network.NetworkItemCoordinator coordinator =
                FindFirstObjectByType<
                    Integration.Network.NetworkItemCoordinator>();

            if (coordinator != null)
            {
                coordinator.RevealRole(lifter, ThiefRevealSeconds, at);
            }

            // Counted from the components afterwards rather than from the
            // coordinator's return, so an offline scene with no coordinator is
            // still measured — and so is the case where the coordinator is
            // there and reveals nobody.
            LastRevealedWatchers = coordinator != null
                ? CountRevealed(lifter)
                : FlashlightVisibility.RevealRole(
                    lifter,
                    ThiefRevealSeconds,
                    at);
        }

        private static int CountRevealed(PlayerRole lifter)
        {
            int revealed = 0;
            foreach (FlashlightVisibility watcher in
                FindObjectsByType<FlashlightVisibility>(
                    FindObjectsSortMode.None))
            {
                if (watcher.ViewerRole != lifter
                    && watcher.IsRevealed)
                {
                    revealed++;
                }
            }

            return revealed;
        }

        private NoiseBoard ResolveNoiseBoard()
        {
            if (noiseBoard == null)
            {
                noiseBoard = FindFirstObjectByType<NoiseBoard>();
            }

            return noiseBoard;
        }
    }
}
