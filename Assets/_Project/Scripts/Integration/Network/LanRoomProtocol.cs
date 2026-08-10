using System;
using System.Globalization;
using System.Text;

namespace PawliceAndPurrglar.Integration.Network
{
    /// <summary>
    /// The one-line datagram a host broadcasts so clients can list it.
    ///
    /// Deliberately plain text and deliberately tiny: it crosses the network
    /// from strangers' machines, so parsing has to fail closed on anything
    /// unexpected rather than throw inside a receive loop.
    /// </summary>
    public static class LanRoomProtocol
    {
        /// <summary>
        /// Separate from the game port so the broadcast never collides with the
        /// Netcode transport, and so two hosts on one machine still both
        /// announce.
        /// </summary>
        public const int DiscoveryPort = 7980;

        private const string Magic = "PAWSLOOT";
        private const int Version = 1;
        private const char Separator = '|';
        private const int MaxLabelLength = 24;

        /// <summary>
        /// A room disappears from the list this long after its last broadcast.
        /// Three missed beats at the one-second send rate, so a brief hiccup
        /// does not make the entry flicker.
        /// </summary>
        public const float RoomTimeoutSeconds = 3.5f;

        public const float BroadcastIntervalSeconds = 1f;

        public static byte[] Encode(
            ushort gamePort,
            string label,
            int playerCount)
        {
            string safeLabel = Sanitize(label);
            string line = string.Join(
                Separator.ToString(),
                Magic,
                Version.ToString(CultureInfo.InvariantCulture),
                gamePort.ToString(CultureInfo.InvariantCulture),
                safeLabel,
                playerCount.ToString(CultureInfo.InvariantCulture));
            return Encoding.UTF8.GetBytes(line);
        }

        /// <summary>
        /// Returns false for anything that is not a well-formed advert. The
        /// sender's address comes from the socket, never from the payload, so a
        /// malformed or hostile datagram cannot point the client somewhere else.
        /// </summary>
        public static bool TryDecode(
            byte[] payload,
            string senderAddress,
            float receivedRealtime,
            out LanRoom room)
        {
            room = default;
            if (payload == null
                || payload.Length == 0
                || payload.Length > 256
                || string.IsNullOrEmpty(senderAddress))
            {
                return false;
            }

            string line;
            try
            {
                line = Encoding.UTF8.GetString(payload);
            }
            catch (ArgumentException)
            {
                return false;
            }

            string[] parts = line.Split(Separator);
            if (parts.Length != 5
                || parts[0] != Magic
                || parts[1] != Version.ToString(
                    CultureInfo.InvariantCulture))
            {
                return false;
            }

            if (!ushort.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ushort gamePort)
                || gamePort < 1024)
            {
                return false;
            }

            if (!int.TryParse(
                    parts[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int playerCount))
            {
                playerCount = 0;
            }

            room = new LanRoom(
                senderAddress,
                gamePort,
                Sanitize(parts[3]),
                playerCount,
                receivedRealtime);
            return true;
        }

        /// <summary>
        /// Strips the separator and control characters, and caps the length.
        /// The label is drawn straight into the room list, so it must not be
        /// able to break the format or run off the panel.
        /// </summary>
        private static string Sanitize(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "방";
            }

            var builder = new StringBuilder(MaxLabelLength);
            foreach (char character in label)
            {
                if (builder.Length >= MaxLabelLength)
                {
                    break;
                }

                if (character != Separator
                    && !char.IsControl(character))
                {
                    builder.Append(character);
                }
            }

            return builder.Length == 0 ? "방" : builder.ToString();
        }
    }
}
