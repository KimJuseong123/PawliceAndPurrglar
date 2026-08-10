using NUnit.Framework;
using PawliceAndPurrglar.Integration.Network;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// How a client moves anything the host owns.
    ///
    /// Both players and both animals go through this, and until now none of
    /// them had a test. That is how the animals came to be left behind when the
    /// players were fixed: the same bug was reported twice, months apart, and
    /// the second time it was in code sitting twenty lines from the first fix.
    ///
    /// Fed here the way a client is actually fed — a target that jumps once and
    /// then holds for several frames — because a target that moves smoothly
    /// every frame passes whatever the arithmetic is.
    /// </summary>
    public sealed class ReplicatedFollowTests
    {
        private const float Snap = 4f;
        private const float Smooth = 0.09f;
        private const float CatchUp = 14f;
        private const float Frame = 1f / 120f;
        private const int FramesPerPacket = 4;

        [Test]
        public void ItKeepsMovingOnTheFramesThatCarryNoPacket()
        {
            var velocity = Vector3.zero;
            Vector3 here = Vector3.zero;
            Vector3 told = Vector3.zero;

            int stalled = 0;
            int counted = 0;
            for (int frame = 0; frame < 240; frame++)
            {
                if (frame % FramesPerPacket == 0)
                {
                    // Three metres a second, arriving in lumps.
                    told += Vector3.forward * (3f * Frame * FramesPerPacket);
                }

                Vector3 before = here;
                here = ReplicatedFollow.Step(
                    here, told, ref velocity, Snap, Smooth, CatchUp, Frame);

                // Only once the follow has caught up to a steady state.
                if (frame < 60)
                {
                    continue;
                }

                counted++;
                if (Vector3.Distance(before, here) < 0.0005f)
                {
                    stalled++;
                }
            }

            Assert.That(
                stalled,
                Is.Zero,
                $"Stood still on {stalled} of {counted} frames while the host "
                + "was walking. Moving on some frames and not others is the "
                + "judder — the eye reads the stops, not the average.");
        }

        [Test]
        public void ItKeepsUpWithTheHostRatherThanFallingBehind()
        {
            var velocity = Vector3.zero;
            Vector3 here = Vector3.zero;
            Vector3 told = Vector3.zero;

            for (int frame = 0; frame < 600; frame++)
            {
                if (frame % FramesPerPacket == 0)
                {
                    told += Vector3.forward * (3f * Frame * FramesPerPacket);
                }

                here = ReplicatedFollow.Step(
                    here, told, ref velocity, Snap, Smooth, CatchUp, Frame);
            }

            Assert.That(
                Vector3.Distance(here, told),
                Is.LessThan(0.5f),
                "The follow lagged half a metre behind a walking target, so it "
                + "is not keeping up — it is being dragged.");
        }

        [Test]
        public void ItGoesStraightThereWhenTheOwnerWalksThroughADoor()
        {
            var velocity = new Vector3(0f, 0f, 3f);
            Vector3 here = Vector3.zero;

            // A room off the edge of the map, which is where interiors are.
            var told = new Vector3(-28f, 0.04f, -336f);
            here = ReplicatedFollow.Step(
                here, told, ref velocity, Snap, Smooth, CatchUp, Frame);

            Assert.That(
                here,
                Is.EqualTo(told),
                "An animal whose owner went through a door has to arrive, not "
                + "set off walking. At a walk that trip outlasts the match.");
            Assert.That(
                velocity,
                Is.EqualTo(Vector3.zero),
                "Carrying the old velocity into the new place throws the "
                + "animal past it on the next frame.");
        }
    }
}
