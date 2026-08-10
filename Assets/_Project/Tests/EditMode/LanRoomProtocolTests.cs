using System.Text;
using NUnit.Framework;
using PawliceAndPurrglar.Integration.Network;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// The room advert is the only thing in this project parsed straight off the
    /// network from an unknown sender, so it has to fail closed on anything
    /// unexpected rather than throw inside the receive loop.
    /// </summary>
    public sealed class LanRoomProtocolTests
    {
        [Test]
        public void RoundTripKeepsPortLabelAndCount()
        {
            byte[] payload =
                LanRoomProtocol.Encode(7979, "DESKTOP-1", 1);

            Assert.That(
                LanRoomProtocol.TryDecode(
                    payload,
                    "192.168.0.10",
                    12f,
                    out LanRoom room),
                Is.True);
            Assert.That(room.Address, Is.EqualTo("192.168.0.10"));
            Assert.That(room.Port, Is.EqualTo(7979));
            Assert.That(room.Label, Is.EqualTo("DESKTOP-1"));
            Assert.That(room.PlayerCount, Is.EqualTo(1));
            Assert.That(room.LastSeenRealtime, Is.EqualTo(12f));
            Assert.That(room.IsFull, Is.False);
        }

        [Test]
        public void AddressComesFromTheSocketNotThePayload()
        {
            // A host cannot advertise someone else's address, so a hostile
            // advert cannot redirect a client to a third machine.
            byte[] payload =
                LanRoomProtocol.Encode(7979, "10.0.0.1", 0);

            LanRoomProtocol.TryDecode(
                payload,
                "192.168.0.55",
                0f,
                out LanRoom room);

            Assert.That(room.Address, Is.EqualTo("192.168.0.55"));
        }

        [Test]
        public void FullRoomIsMarked()
        {
            LanRoomProtocol.TryDecode(
                LanRoomProtocol.Encode(
                    7979,
                    "full",
                    NetworkSessionController.MaximumPlayers),
                "192.168.0.10",
                0f,
                out LanRoom room);

            Assert.That(room.IsFull, Is.True);
        }

        [TestCase("")]
        [TestCase("hello")]
        [TestCase("PAWSLOOT")]
        [TestCase("PAWSLOOT|1|7979|room")]
        [TestCase("PAWSLOOT|9|7979|room|0")]
        [TestCase("NOPE|1|7979|room|0")]
        [TestCase("PAWSLOOT|1|notaport|room|0")]
        [TestCase("PAWSLOOT|1|80|room|0")]
        [TestCase("PAWSLOOT|1|0|room|0")]
        public void MalformedAdvertsAreRejected(string line)
        {
            Assert.That(
                LanRoomProtocol.TryDecode(
                    Encoding.UTF8.GetBytes(line),
                    "192.168.0.10",
                    0f,
                    out _),
                Is.False,
                $"'{line}' should not have produced a room.");
        }

        [Test]
        public void NullEmptyOversizedAndUnknownSenderAreRejected()
        {
            Assert.That(
                LanRoomProtocol.TryDecode(null, "1.2.3.4", 0f, out _),
                Is.False);
            Assert.That(
                LanRoomProtocol.TryDecode(
                    new byte[0],
                    "1.2.3.4",
                    0f,
                    out _),
                Is.False);
            Assert.That(
                LanRoomProtocol.TryDecode(
                    new byte[512],
                    "1.2.3.4",
                    0f,
                    out _),
                Is.False,
                "An oversized datagram is not a room advert.");
            Assert.That(
                LanRoomProtocol.TryDecode(
                    LanRoomProtocol.Encode(7979, "room", 0),
                    string.Empty,
                    0f,
                    out _),
                Is.False,
                "Without a sender there is nothing to connect to.");
        }

        [Test]
        public void LabelCannotBreakTheFormatOrRunOffThePanel()
        {
            byte[] payload = LanRoomProtocol.Encode(
                7979,
                "a|b|c" + new string('x', 100),
                0);

            Assert.That(
                LanRoomProtocol.TryDecode(
                    payload,
                    "192.168.0.10",
                    0f,
                    out LanRoom room),
                Is.True,
                "A separator inside the label must be stripped, not left to "
                + "split the line into extra fields.");
            Assert.That(room.Label, Does.Not.Contain("|"));
            Assert.That(room.Label.Length, Is.LessThanOrEqualTo(24));
        }

        [Test]
        public void BlankLabelFallsBackSoTheRowIsNeverEmpty()
        {
            LanRoomProtocol.TryDecode(
                LanRoomProtocol.Encode(7979, "   ", 0),
                "192.168.0.10",
                0f,
                out LanRoom room);

            Assert.That(room.Label, Is.Not.Empty);
        }

        [Test]
        public void RoomsAreIdentifiedByAddressAndPortOnly()
        {
            var first = new LanRoom("192.168.0.10", 7979, "A", 0, 1f);
            var refreshed = new LanRoom("192.168.0.10", 7979, "B", 2, 9f);
            var otherPort = new LanRoom("192.168.0.10", 7980, "A", 0, 1f);

            Assert.That(
                first,
                Is.EqualTo(refreshed),
                "A refreshed advert must update the row, not add a second one.");
            Assert.That(
                first.GetHashCode(),
                Is.EqualTo(refreshed.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(otherPort));
        }
    }
}
