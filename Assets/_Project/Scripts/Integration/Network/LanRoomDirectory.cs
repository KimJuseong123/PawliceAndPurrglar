using System;
using System.Collections.Generic;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
#endif
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Network
{
    /// <summary>
    /// Finds and announces games on the local network so two players on one
    /// wifi do not have to read an address to each other.
    ///
    /// A shortcut, not the way in. Rooms are reached by invite code now, and
    /// this is what is left of the direct-IP model (DEC-027) — useful when two
    /// desktop builds are on one router and the room was opened with
    /// <c>TryStartHost</c>, useless everywhere else.
    ///
    /// Off entirely in the browser. WebGL has no UDP socket to broadcast from,
    /// and touching <c>UdpClient</c> there throws at the first call rather than
    /// failing to compile — so the whole mechanism is compiled out instead of
    /// left to discover its own absence at runtime.
    ///
    /// Both sockets are non-blocking. Receiving is polled from Update rather
    /// than run on a thread, because a background thread would need locking
    /// around the room list for no real gain at one datagram per second.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanRoomDirectory : MonoBehaviour
    {
        private readonly Dictionary<LanRoom, LanRoom> _rooms = new();
        private readonly List<LanRoom> _snapshot = new();

#if !UNITY_WEBGL || UNITY_EDITOR
        private UdpClient _listener;
        private UdpClient _announcer;
        private float _nextBroadcast;
        private bool _listenFailed;
#endif

        [SerializeField]
        private NetworkSessionController session;

        /// <summary>
        /// Shown in the other player's room list. The machine name is the most
        /// recognisable thing available without asking the player to type one.
        /// </summary>
        [SerializeField]
        private string roomLabel = string.Empty;

#if !UNITY_WEBGL || UNITY_EDITOR
        public bool IsListening => _listener != null;
        public bool IsAnnouncing => _announcer != null;
#else
        public bool IsListening => false;
        public bool IsAnnouncing => false;
#endif
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Rooms seen recently, newest activity first. Reuses one list so the
        /// per-frame UI refresh does not allocate.
        /// </summary>
        public IReadOnlyList<LanRoom> Rooms => _snapshot;

        public void Configure(NetworkSessionController configuredSession)
        {
            session = configuredSession;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private string ResolveLabel()
        {
            if (!string.IsNullOrWhiteSpace(roomLabel))
            {
                return roomLabel;
            }

            try
            {
                roomLabel = Environment.MachineName;
            }
            catch (InvalidOperationException)
            {
                roomLabel = "방";
            }

            return roomLabel;
        }

        private void StartListening()
        {
            if (_listener != null || _listenFailed)
            {
                return;
            }

            try
            {
                var socket = new UdpClient();
                // Two players testing on one PC bind the same discovery port,
                // so the address has to be shareable or the second instance
                // would never see the first.
                socket.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress,
                    true);
                socket.ExclusiveAddressUse = false;
                socket.Client.Bind(
                    new IPEndPoint(
                        IPAddress.Any,
                        LanRoomProtocol.DiscoveryPort));
                socket.EnableBroadcast = true;
                _listener = socket;
            }
            catch (SocketException exception)
            {
                // A blocked port is not fatal: the player can still type an IP.
                _listenFailed = true;
                LastError = exception.Message;
                GameLogger.Warning(
                    GameLogCategory.Network,
                    "LAN room discovery is unavailable; type an IP instead. "
                    + exception.Message,
                    this);
            }
        }

        private void Poll()
        {
            if (_listener == null)
            {
                return;
            }

            // Available is checked first so the socket is never read blocking.
            while (_listener.Available > 0)
            {
                IPEndPoint sender = null;
                byte[] payload;
                try
                {
                    payload = _listener.Receive(ref sender);
                }
                catch (SocketException)
                {
                    return;
                }

                if (sender == null
                    || !LanRoomProtocol.TryDecode(
                        payload,
                        sender.Address.ToString(),
                        Time.realtimeSinceStartup,
                        out LanRoom room))
                {
                    continue;
                }

                // Overwrite so the player count and last-seen time refresh
                // while the key (address and port) stays the same entry.
                _rooms[room] = room;
            }
        }

        private void Announce()
        {
            // A Relay room has no address on this network. Announcing one would
            // put a room in the other player's list that points at a port
            // nothing is listening on — a click that times out with no
            // explanation, which is worse than an empty list.
            bool shouldAnnounce = session != null
                && session.Mode == NetworkSessionController.SessionMode.Host
                && string.IsNullOrEmpty(session.InviteCode);
            if (!shouldAnnounce)
            {
                StopAnnouncing();
                return;
            }

            if (Time.realtimeSinceStartup < _nextBroadcast)
            {
                return;
            }

            _nextBroadcast = Time.realtimeSinceStartup
                + LanRoomProtocol.BroadcastIntervalSeconds;

            try
            {
                if (_announcer == null)
                {
                    _announcer = new UdpClient { EnableBroadcast = true };
                }

                byte[] payload = LanRoomProtocol.Encode(
                    session.Port,
                    ResolveLabel(),
                    session.ConnectedPlayerCount);
                _announcer.Send(
                    payload,
                    payload.Length,
                    new IPEndPoint(
                        IPAddress.Broadcast,
                        LanRoomProtocol.DiscoveryPort));
            }
            catch (SocketException exception)
            {
                LastError = exception.Message;
                StopAnnouncing();
            }
        }

#endif

        /// <summary>
        /// Drops rooms nobody has heard from lately. Outside the socket guard
        /// on purpose: it only walks a dictionary, and the browser still needs
        /// it to leave <see cref="Rooms"/> as a well-formed empty list rather
        /// than whatever the last platform left in it.
        /// </summary>
        private void Expire()
        {
            float cutoff = Time.realtimeSinceStartup
                - LanRoomProtocol.RoomTimeoutSeconds;
            _snapshot.Clear();
            List<LanRoom> stale = null;
            foreach (LanRoom room in _rooms.Values)
            {
                if (room.LastSeenRealtime < cutoff)
                {
                    stale ??= new List<LanRoom>();
                    stale.Add(room);
                    continue;
                }

                _snapshot.Add(room);
            }

            if (stale != null)
            {
                foreach (LanRoom room in stale)
                {
                    _rooms.Remove(room);
                }
            }

            _snapshot.Sort(
                (left, right) =>
                    right.LastSeenRealtime.CompareTo(
                        left.LastSeenRealtime));
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private void StopAnnouncing()
        {
            if (_announcer == null)
            {
                return;
            }

            _announcer.Close();
            _announcer = null;
        }
#endif

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            StartListening();
            Poll();
            Announce();
#endif
            Expire();
        }

        private void OnDestroy()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            StopAnnouncing();
            if (_listener == null)
            {
                return;
            }

            _listener.Close();
            _listener = null;
#endif
        }
    }
}
