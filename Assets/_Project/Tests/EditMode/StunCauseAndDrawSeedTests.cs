using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// Two things that look like presentation and are not.
    ///
    /// The stun cause is presentation, and the danger is that somebody
    /// eventually reads it in a rule — so the durations are pinned here next to
    /// the causes, and a banana that quietly became shorter than a rock fails a
    /// test rather than a playtest.
    ///
    /// The draw seed is the opposite: it reads like a detail and it decides
    /// whether two players are looking at the same town. The hash is pinned
    /// because <c>string.GetHashCode()</c> — what this replaced — is randomised
    /// per process, so it agrees with itself within one run and with nothing
    /// else. A test that only ran once per process could never have caught it.
    /// </summary>
    public sealed class StunCauseAndDrawSeedTests
    {
        [Test]
        public void EachPropSaysWhatKindOfAccidentItWas()
        {
            Assert.That(
                ThrowableCatalog.GetStunCause(ThrowableKind.Banana),
                Is.EqualTo(StunCause.Slip),
                "Nobody is hit by a banana. Four stars over the head is the "
                + "cartoon for being struck.");

            Assert.That(
                ThrowableCatalog.GetStunCause(ThrowableKind.GlueTrap),
                Is.EqualTo(StunCause.Stuck),
                "Upright, awake, and your feet will not come off the floor.");

            Assert.That(
                ThrowableCatalog.GetStunCause(ThrowableKind.Rock),
                Is.EqualTo(StunCause.Impact));

            Assert.That(
                ThrowableCatalog.GetStunCause(ThrowableKind.Firework),
                Is.EqualTo(StunCause.Impact),
                "Anything new falls to the drawing the stars already meant.");
        }

        /// <summary>
        /// The point of splitting the cause out was to change the drawing and
        /// nothing else. If this fails, a presentation change has become a
        /// balance change.
        /// </summary>
        [Test]
        public void TellingTheAccidentsApartDidNotChangeHowLongTheyLast()
        {
            Assert.That(
                ThrowableCatalog.GetStunSeconds(ThrowableKind.Banana),
                Is.EqualTo(ThrowableCatalog.BananaSlipSeconds).Within(0.0001f));

            Assert.That(
                ThrowableCatalog.GetStunSeconds(ThrowableKind.Rock),
                Is.EqualTo(ThrowableCatalog.RockStunSeconds).Within(0.0001f));

            Assert.That(
                ThrowableCatalog.GetStunSeconds(ThrowableKind.GlueTrap),
                Is.EqualTo(ThrowableCatalog.GlueHoldSeconds).Within(0.0001f));
        }

        [Test]
        public void AStunRemembersWhatCausedIt()
        {
            var host = new UnityEngine.GameObject("Stunned");
            try
            {
                StunState stun = host.AddComponent<StunState>();

                Assert.That(
                    stun.Cause,
                    Is.EqualTo(StunCause.Impact),
                    "The default has to be the drawing that already existed.");

                Assert.That(stun.TryApply(1f, StunCause.Slip), Is.True);
                Assert.That(stun.Cause, Is.EqualTo(StunCause.Slip));

                // Held after it runs out, because the views fade over the next
                // half second and a cause that snapped back to Impact on the
                // last frame would pop stars onto somebody mid-slide.
                stun.Tick(2f);
                Assert.That(stun.IsStunned, Is.False);
                Assert.That(stun.Cause, Is.EqualTo(StunCause.Slip));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// The same string has to give the same number in every process, or the
        /// host and the client roll different contents for the same cupboard in
        /// the same match. These literals are the point: they were produced by
        /// this implementation and pin it against a "tidier" hash later.
        /// </summary>
        [Test]
        public void TheDrawHashIsTheSameEverywhere()
        {
            Assert.That(
                MatchDrawSeed.StableHash("jewel-ring"),
                Is.EqualTo(MatchDrawSeed.StableHash("jewel-ring")));

            Assert.That(
                MatchDrawSeed.StableHash("Interior 1"),
                Is.Not.EqualTo(MatchDrawSeed.StableHash("Interior 2")),
                "Thirteen rooms sharing a number would deal thirteen "
                + "identical layouts.");

            Assert.That(MatchDrawSeed.StableHash(string.Empty), Is.EqualTo(0));

            // FNV-1a of "a" and "ab". Written out rather than computed, so a
            // replacement algorithm cannot pass by agreeing with itself.
            Assert.That(
                MatchDrawSeed.StableHash("a"),
                Is.EqualTo(unchecked((int)0xE40C292C)));
            Assert.That(
                MatchDrawSeed.StableHash("ab"),
                Is.EqualTo(unchecked((int)0x4D2505CA)));
        }

        [Test]
        public void OneMatchSeedGivesEveryRoomADifferentDraw()
        {
            const int matchSeed = 987654321;

            Assert.That(
                MatchDrawSeed.For(matchSeed, "Interior 4"),
                Is.EqualTo(MatchDrawSeed.For(matchSeed, "Interior 4")),
                "Both machines run this with the same two inputs.");

            Assert.That(
                MatchDrawSeed.For(matchSeed, "Interior 4"),
                Is.Not.EqualTo(MatchDrawSeed.For(matchSeed, "Interior 5")));

            Assert.That(
                MatchDrawSeed.For(matchSeed, "Interior 4"),
                Is.Not.EqualTo(MatchDrawSeed.For(matchSeed + 1, "Interior 4")),
                "A rematch has to be a different town, or the second match is "
                + "the first one.");
        }

        /// <summary>
        /// The shuffle itself, run twice from one seed. This is what the two
        /// machines are actually doing, and it is the assertion the old
        /// <c>UnityEngine.Random</c> draw could not make.
        /// </summary>
        [Test]
        public void TheSameSeedDealsTheSameLayoutTwice()
        {
            int[] host = DealOrder(MatchDrawSeed.For(4242, "Jewellers"), 7, 4);
            int[] client = DealOrder(MatchDrawSeed.For(4242, "Jewellers"), 7, 4);
            int[] other = DealOrder(MatchDrawSeed.For(4242, "Bookshop"), 7, 4);

            Assert.That(host, Is.EqualTo(client));
            Assert.That(host, Is.Not.EqualTo(other));

            Assert.That(
                host,
                Is.Unique,
                "Removing each place as it is used is what stops two pieces "
                + "landing on one shelf.");
        }

        /// <summary>
        /// Mirrors <c>LootSpotDraw.Deal</c>: pick from a shrinking list.
        /// </summary>
        private static int[] DealOrder(int seed, int places, int pieces)
        {
            var random = new System.Random(seed);
            var remaining = new System.Collections.Generic.List<int>();
            for (int index = 0; index < places; index++)
            {
                remaining.Add(index);
            }

            var order = new int[pieces];
            for (int piece = 0; piece < pieces; piece++)
            {
                int slot = random.Next(remaining.Count);
                order[piece] = remaining[slot];
                remaining.RemoveAt(slot);
            }

            return order;
        }
    }
}
