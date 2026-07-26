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
            catch (SocketException)
            {
                // A machine with no resolvable host name still needs to be able
                // to host for a second process on the same PC.
            }

            var ordered = new List<string>();
            ordered.AddRange(lan);
            ordered.AddRange(other);
            ordered.Add(LoopbackAddress);
            return ordered;
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
