using PawsAndLoot.Logging;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Says how long the round trip to the host actually is.
    ///
    /// "The client lags and the host does not" has two very different causes
    /// and they are fixed in opposite places. This game does not predict client
    /// movement — <c>NetworkPlayerLink</c> says so on its first line, "client
    /// input -> RPC -> host simulates -> position replicates" — so the guest
    /// waits a full round trip for their own keypress while the host, being the
    /// server, waits for nothing. If that round trip is 40ms the game is fine
    /// and the problem is somewhere else entirely; if it is 300ms the relay is
    /// on the wrong continent and no amount of optimisation will help.
    ///
    /// Nobody could tell those apart by playing, so the number is written down.
    /// Client only: the host's round trip to itself is zero and reporting it
    /// would put a reassuring number in the log of the machine that never had
    /// the problem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkLatencyReport : MonoBehaviour
    {
        /// <summary>
        /// Seconds between readings. Often enough to see a relay that is far
        /// away, rare enough that it is not itself a cost.
        /// </summary>
        private const float IntervalSeconds = 10f;

        /// <summary>
        /// Above this, the round trip is the reason the game feels bad. Two
        /// players in Korea on a relay in Korea measure well under it; a relay
        /// in another region does not.
        /// </summary>
        private const ulong ConcerningMilliseconds = 150;

        [SerializeField]
        private NetworkManager networkManager;

        private float _nextReadingAt;
        private bool _warned;

        public ulong LastRoundTripMilliseconds { get; private set; }

        private void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextReadingAt)
            {
                return;
            }

            _nextReadingAt = Time.unscaledTime + IntervalSeconds;

            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsConnectedClient
                || networkManager.IsServer)
            {
                return;
            }

            var transport =
                networkManager.NetworkConfig?.NetworkTransport as UnityTransport;
            if (transport == null)
            {
                return;
            }

            LastRoundTripMilliseconds =
                transport.GetCurrentRtt(NetworkManager.ServerClientId);
            if (LastRoundTripMilliseconds == 0)
            {
                return;
            }

            GameLogger.Info(
                GameLogCategory.Network,
                $"Round trip to the host: {LastRoundTripMilliseconds}ms.",
                this);

            // Said once, and said as the thing to do about it. A number in a log
            // is only useful to somebody who already knows what a good one is.
            if (!_warned
                && LastRoundTripMilliseconds >= ConcerningMilliseconds)
            {
                _warned = true;
                GameLogger.Warning(
                    GameLogCategory.Network,
                    $"{LastRoundTripMilliseconds}ms is far enough that the "
                    + "guest feels every keypress. This game does not predict "
                    + "client movement, so the round trip is the input delay. "
                    + "Check which region the host's Relay allocation landed "
                    + "in — the host logs it when the room is created.",
                    this);
            }
        }
    }
}
