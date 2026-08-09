using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Reports this machine's LAN addresses so the host can read one out to the
    /// other player.
    ///
    /// Direct IP only, per the adopted connection model: no Relay and no
    /// discovery service, so the address has to be visible on screen.
    /// </summary>
    public static class LocalAddressProvider
    {
        public const string LoopbackAddress = "127.0.0.1";

        /// <summary>
        /// Every IPv4 address on this machine, loopback last.
        ///
        /// Ordered because a machine usually has several and the useful one for
        /// a second player is a private LAN address, not loopback. Loopback is
        /// still listed because two processes on one PC is the common test.
        /// </summary>
        public static IReadOnlyList<string> GetIPv4Addresses()
        {
            var lan = new List<string>();
            var other = new List<string>();

#if UNITY_WEBGL && !UNITY_EDITOR
            // A page has no local addresses to enumerate, and asking throws
            // rather than returning nothing. There is also nothing this would
            // be used for: a browser cannot listen, so no address of its own
            // could ever be handed to another player.
            return new List<string> { LoopbackAddress };
#else
            try
            {
                foreach (IPAddress address in Dns.GetHostAddresses(
                             Dns.GetHostName()))
                {
                    if (address.AddressFamily
                        != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    string text = address.ToString();
                    if (IsPrivateLan(text))
                    {
                        lan.Add(text);
                    }
                    else
                    {
                        other.Add(text);
                    }
                }
            }
            catch (Exception exception) when (
                exception is SocketException
                || exception is NotSupportedException
                || exception is PlatformNotSupportedException)
            {
                // A machine with no resolvable host name still needs to be able
                // to host for a second process on the same PC. The two
                // platform exceptions are for the players Unity does not give
                // a name lookup to at all — there the list is simply empty,
                // which is a state this already handles.
            }

            var ordered = new List<string>();
            ordered.AddRange(lan);
            ordered.AddRange(other);
            ordered.Add(LoopbackAddress);
            return ordered;
#endif
        }

        /// <summary>
        /// Best address to show first: a private LAN address if one exists,
        /// otherwise loopback.
        /// </summary>
        public static string GetPreferredAddress()
        {
            IReadOnlyList<string> all = GetIPv4Addresses();
            return all.Count > 0 ? all[0] : LoopbackAddress;
        }

        /// <summary>
        /// RFC 1918 ranges plus the link-local block, which is what a home or
        /// classroom network hands out.
        /// </summary>
        public static bool IsPrivateLan(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            if (address.StartsWith("10.") || address.StartsWith("192.168."))
            {
                return true;
            }

            if (address.StartsWith("169.254."))
            {
                return true;
            }

            if (!address.StartsWith("172."))
            {
                return false;
            }

            // 172.16.0.0 through 172.31.255.255.
            string[] parts = address.Split('.');
            return parts.Length == 4
                && int.TryParse(parts[1], out int second)
                && second >= 16
                && second <= 31;
        }

        /// <summary>
        /// Accepts only a dotted IPv4 literal. Host names are rejected because
        /// the transport takes an address, and a silent failure at connect time
        /// is much harder to explain than a rejected field.
        /// </summary>
        public static bool IsValidIPv4(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            string[] parts = address.Trim().Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (string part in parts)
            {
                if (part.Length == 0
                    || part.Length > 3
                    || !int.TryParse(part, out int value)
                    || value < 0
                    || value > 255)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Strips what a player pastes down to the host itself.
        ///
        /// The address arrives from wherever the host read it out: a tunnel
        /// prints <c>tcp://0.tcp.ngrok.io:12345</c>, a browser bar shows
        /// <c>wss://play.example.com/</c>, and somebody typing from memory adds
        /// a trailing slash. All of those name the same machine, and the
        /// transport wants the machine.
        ///
        /// The port inside the string is deliberately dropped rather than
        /// obeyed. There is a port field next to this one, and a string that
        /// silently overrides it is a field that lies about what it does.
        /// </summary>
        public static string NormaliseHost(string address)
        {
            string value = address?.Trim() ?? string.Empty;
            foreach (string scheme in
                new[] { "wss://", "ws://", "tcp://", "https://", "http://" })
            {
                if (value.StartsWith(
                        scheme,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = value[scheme.Length..];
                    break;
                }
            }

            int slash = value.IndexOf('/');
            if (slash >= 0)
            {
                value = value[..slash];
            }

            int colon = value.LastIndexOf(':');
            if (colon > 0 && !value.Contains(']'))
            {
                value = value[..colon];
            }

            return value.Trim();
        }

        /// <summary>
        /// Whether this is something the transport can be pointed at: an IPv4
        /// address, or a host name.
        ///
        /// Deliberately loose about names. Whether a name resolves is a question
        /// only DNS can answer, and answering it here would mean a lookup on the
        /// UI thread to tell the player something the connection attempt is
        /// about to tell them anyway. What is checked is that it could be a name
        /// at all — anything else is a typo worth catching before the wait.
        /// </summary>
        public static bool IsValidHost(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            if (IsValidIPv4(address))
            {
                return true;
            }

            if (address.Length > 253 || address.StartsWith('.')
                || address.EndsWith('.'))
            {
                return false;
            }

            foreach (char character in address)
            {
                if (!char.IsLetterOrDigit(character)
                    && character != '.'
                    && character != '-')
                {
                    return false;
                }
            }

            // A bare word is a machine on the local network, which is legal but
            // is far more often a half-typed address. A dot is the cheapest
            // signal that somebody meant a real host.
            //
            // Except for this one. Two clients on one desk is how this game is
            // tested every day, and "localhost" is what people type to do it.
            return address.Contains('.')
                || address.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsValidPort(string port, out ushort parsed)
        {
            parsed = 0;
            if (!ushort.TryParse(port?.Trim(), out ushort value)
                || value < 1024)
            {
                return false;
            }

            parsed = value;
            return true;
        }
    }
}
