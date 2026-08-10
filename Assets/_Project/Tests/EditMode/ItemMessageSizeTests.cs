using NUnit.Framework;
using PawliceAndPurrglar.Integration.Network;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// Every named item message has to fit the buffer declared for it.
    ///
    /// This is here because the arithmetic was done by hand and was wrong: the
    /// trap-placed payload is three ints and a Vector3, which is 24 bytes, and
    /// the buffer was 20. Placing anything threw an overflow — after the third
    /// int there were 8 bytes left and 12 to write, which is exactly what the
    /// exception reported.
    ///
    /// It survived because nothing placeable was obtainable until the police got
    /// their props, so no trap had ever been placed in a session. The lesson is
    /// not "count more carefully" — it is that the size and the writes were
    /// declared in two places, and only one of them was ever exercised.
    ///
    /// So each case below writes the real payload into a buffer of exactly the
    /// declared size. Any future field added to a message fails here rather than
    /// in a playtest.
    /// </summary>
    public sealed class ItemMessageSizeTests
    {
        [Test]
        public void TrapPlacedPayloadFitsItsBuffer()
        {
            using var writer = new FastBufferWriter(
                NetworkItemCoordinator.PlaceMessageBytes,
                Allocator.Temp);

            Assert.DoesNotThrow(() =>
            {
                writer.WriteValueSafe(7);
                writer.WriteValueSafe(2);
                writer.WriteValueSafe(1);
                writer.WriteValueSafe(new Vector3(3f, 0f, -4f));
            });
        }

        [Test]
        public void TrapClearedPayloadFitsItsBuffer()
        {
            using var writer = new FastBufferWriter(
                NetworkItemCoordinator.ClearMessageBytes,
                Allocator.Temp);

            Assert.DoesNotThrow(() => writer.WriteValueSafe(7));
        }

        [Test]
        public void RevealPayloadFitsItsBuffer()
        {
            using var writer = new FastBufferWriter(
                NetworkItemCoordinator.RevealMessageBytes,
                Allocator.Temp);

            Assert.DoesNotThrow(() =>
            {
                writer.WriteValueSafe(1);
                writer.WriteValueSafe(12);
            });
        }

        [Test]
        public void PickupPayloadFitsItsBuffer()
        {
            using var writer = new FastBufferWriter(
                NetworkItemCoordinator.PickupMessageBytes,
                Allocator.Temp);

            Assert.DoesNotThrow(() =>
            {
                writer.WriteValueSafe(4);
                writer.WriteValueSafe(true);
            });
        }
    }
}
