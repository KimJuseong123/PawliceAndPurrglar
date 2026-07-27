using System;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// One host advertising itself on the local network.
    ///
    /// Direct IP has no server to keep a room list, so a room is nothing more
    /// than the last broadcast a host sent. <see cref="LastSeenRealtime"/> is
    /// what lets the browser drop a host that stopped talking, instead of
    /// showing a room that is no longer there.
    /// </summary>
    public readonly struct LanRoom : IEquatable<LanRoom>
    {
        public LanRoom(
            string address,
            ushort port,
            string label,
            int playerCount,
            float lastSeenRealtime)
        {
            Address = address ?? string.Empty;
            Port = port;
            Label = string.IsNullOrWhiteSpace(label)
                ? address ?? string.Empty
                : label;
            PlayerCount = Math.Max(0, playerCount);
            LastSeenRealtime = lastSeenRealtime;
        }

        public string Address { get; }
        public ushort Port { get; }
        public string Label { get; }
        public int PlayerCount { get; }
        public float LastSeenRealtime { get; }

        public bool IsFull =>
            PlayerCount >= NetworkSessionController.MaximumPlayers;

        /// <summary>
        /// Address and port identify a room. The label and player count change
        /// between broadcasts and must not split one host into two entries.
        /// </summary>
        public bool Equals(LanRoom other)
        {
            return Port == other.Port
                && string.Equals(
                    Address,
                    other.Address,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LanRoom other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Address, Port);
        }
    }
}
