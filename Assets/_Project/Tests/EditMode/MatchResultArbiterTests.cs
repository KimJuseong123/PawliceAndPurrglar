using NUnit.Framework;
using PawsAndLoot.Match;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class MatchResultArbiterTests
    {
        private const int Target = 1000;
        private const int ArrestsToWin = 3;

        [Test]
        public void OneArrestDoesNotDecideTheMatch()
        {
            var arbiter = new MatchResultArbiter();
            Assert.That(arbiter.RequestArrest(), Is.True);

            Assert.That(
                arbiter.TryResolve(500, Target, ArrestsToWin, 120f, out _),
                Is.False,
                "A single catch ended a four-minute match in thirty seconds "
                + "and left the thief no way back from one mistake.");
            Assert.That(arbiter.ArrestCount, Is.EqualTo(1));
            Assert.That(arbiter.HasResult, Is.False);
        }

        [Test]
        public void ThirdArrestDecidesPoliceVictory()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestArrest();
            arbiter.TryResolve(500, Target, ArrestsToWin, 120f, out _);
            arbiter.RequestArrest();
            arbiter.TryResolve(500, Target, ArrestsToWin, 110f, out _);
            arbiter.RequestArrest();

            Assert.That(
                arbiter.TryResolve(
                    500,
                    Target,
                    ArrestsToWin,
                    100f,
                    out MatchResult result),
                Is.True);
            Assert.That(result.Winner, Is.EqualTo(MatchWinner.Police));
            Assert.That(
                result.Reason,
                Is.EqualTo(MatchEndReason.ThiefArrested));
        }

        /// <summary>
        /// The threshold is configuration, not a constant, so a balance change
        /// does not need this rule rewritten.
        /// </summary>
        [Test]
        public void ArrestThresholdComesFromTheCaller()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestArrest();

            Assert.That(
                arbiter.TryResolve(0, Target, 1, 100f, out MatchResult result),
                Is.True);
            Assert.That(result.Winner, Is.EqualTo(MatchWinner.Police));
        }

        [Test]
        public void TimeoutBelowTargetDecidesPoliceVictory()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestTimeout();

            Assert.That(
                arbiter.TryResolve(
                    999,
                    Target,
                    ArrestsToWin,
                    0f,
                    out MatchResult result),
                Is.True);
            Assert.That(result.Winner, Is.EqualTo(MatchWinner.Police));
            Assert.That(
                result.Reason,
                Is.EqualTo(
                    MatchEndReason.TimeExpiredBelowTarget));
        }

        [Test]
        public void ReachingSaleTargetDecidesThiefVictory()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestSaleCheck();

            Assert.That(
                arbiter.TryResolve(
                    Target,
                    Target,
                    ArrestsToWin,
                    30f,
                    out MatchResult result),
                Is.True);
            Assert.That(result.Winner, Is.EqualTo(MatchWinner.Thief));
            Assert.That(
                result.Reason,
                Is.EqualTo(MatchEndReason.SaleTargetReached));
        }

        /// <summary>
        /// A thief who reaches the target on the same frame as their third
        /// arrest loses. The officer completed their condition; the sale that
        /// arrives alongside it is money the thief will not get to keep.
        /// </summary>
        [Test]
        public void SimultaneousRequestsPrioritizeArrestThenSaleThenTimeout()
        {
            var arrestFirst = new MatchResultArbiter();
            arrestFirst.RequestTimeout();
            arrestFirst.RequestSaleCheck();
            arrestFirst.RequestArrest();
            arrestFirst.RequestArrest();
            arrestFirst.RequestArrest();
            arrestFirst.TryResolve(
                Target,
                Target,
                ArrestsToWin,
                0f,
                out MatchResult arrestResult);
            Assert.That(
                arrestResult.Reason,
                Is.EqualTo(MatchEndReason.ThiefArrested));

            var saleBeforeTimeout = new MatchResultArbiter();
            saleBeforeTimeout.RequestTimeout();
            saleBeforeTimeout.RequestSaleCheck();
            saleBeforeTimeout.TryResolve(
                Target,
                Target,
                ArrestsToWin,
                0f,
                out MatchResult saleResult);
            Assert.That(
                saleResult.Reason,
                Is.EqualTo(MatchEndReason.SaleTargetReached));
        }

        [Test]
        public void ResultCanOnlyBeDecidedOnce()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestTimeout();
            Assert.That(
                arbiter.TryResolve(
                    0,
                    Target,
                    ArrestsToWin,
                    0f,
                    out MatchResult first),
                Is.True);

            Assert.That(arbiter.RequestArrest(), Is.False);
            Assert.That(
                arbiter.TryResolve(
                    Target,
                    Target,
                    ArrestsToWin,
                    0f,
                    out MatchResult second),
                Is.False);
            Assert.That(second.Winner, Is.EqualTo(first.Winner));
            Assert.That(second.Reason, Is.EqualTo(first.Reason));
        }

        [Test]
        public void SaleBelowTargetDoesNotDecideResult()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestSaleCheck();

            Assert.That(
                arbiter.TryResolve(500, Target, ArrestsToWin, 60f, out _),
                Is.False);
            Assert.That(arbiter.HasResult, Is.False);
        }

        /// <summary>
        /// A rematch starts the officer back at zero. Carrying the count over
        /// would end the second match on the first catch.
        /// </summary>
        [Test]
        public void ResetClearsTheArrestCount()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestArrest();
            arbiter.RequestArrest();
            Assert.That(arbiter.ArrestCount, Is.EqualTo(2));

            arbiter.Reset();

            Assert.That(arbiter.ArrestCount, Is.Zero);
            arbiter.RequestArrest();
            Assert.That(
                arbiter.TryResolve(0, Target, ArrestsToWin, 60f, out _),
                Is.False);
        }
    }
}
