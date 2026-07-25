using NUnit.Framework;
using PawsAndLoot.Match;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class MatchResultArbiterTests
    {
        [Test]
        public void ArrestDecidesPoliceVictory()
        {
            var arbiter = new MatchResultArbiter();
            Assert.That(arbiter.RequestArrest(), Is.True);

            Assert.That(
                arbiter.TryResolve(500, 1000, 120f, out MatchResult result),
                Is.True);
            Assert.That(result.Winner, Is.EqualTo(MatchWinner.Police));
            Assert.That(
                result.Reason,
                Is.EqualTo(MatchEndReason.ThiefArrested));
        }

        [Test]
        public void TimeoutBelowTargetDecidesPoliceVictory()
        {
            var arbiter = new MatchResultArbiter();
            arbiter.RequestTimeout();

            Assert.That(
                arbiter.TryResolve(999, 1000, 0f, out MatchResult result),
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
                arbiter.TryResolve(1000, 1000, 30f, out MatchResult result),
                Is.True);
            Assert.That(result.Winner, Is.EqualTo(MatchWinner.Thief));
            Assert.That(
                result.Reason,
                Is.EqualTo(MatchEndReason.SaleTargetReached));
        }

        [Test]
        public void SimultaneousRequestsPrioritizeArrestThenSaleThenTimeout()
        {
            var arrestFirst = new MatchResultArbiter();
            arrestFirst.RequestTimeout();
            arrestFirst.RequestSaleCheck();
            arrestFirst.RequestArrest();
            arrestFirst.TryResolve(
                1000,
                1000,
                0f,
                out MatchResult arrestResult);
            Assert.That(
                arrestResult.Reason,
                Is.EqualTo(MatchEndReason.ThiefArrested));

            var saleBeforeTimeout = new MatchResultArbiter();
            saleBeforeTimeout.RequestTimeout();
            saleBeforeTimeout.RequestSaleCheck();
            saleBeforeTimeout.TryResolve(
                1000,
                1000,
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
                arbiter.TryResolve(0, 1000, 0f, out MatchResult first),
                Is.True);

            Assert.That(arbiter.RequestArrest(), Is.False);
            Assert.That(
                arbiter.TryResolve(1000, 1000, 0f, out MatchResult second),
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
                arbiter.TryResolve(500, 1000, 60f, out _),
                Is.False);
            Assert.That(arbiter.HasResult, Is.False);
        }
    }
}
