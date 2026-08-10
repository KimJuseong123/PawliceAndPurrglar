using NUnit.Framework;
using PawliceAndPurrglar.Integration.Voice;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// The two halves of "where is the voice API".
    ///
    /// The choice between a configured address and the page's own origin is
    /// `#if UNITY_WEBGL`, so no desktop test can reach it — that branch is only
    /// compiled by a WebGL build. What is testable here is the pair of
    /// predicates the branch is built from, and both of them have a wrong answer
    /// that costs the whole feature:
    ///
    /// - a loopback address honoured from a browser sends every command to the
    ///   player's own machine, which is not running anything
    /// - a deployable address rejected as loopback falls back to the page
    ///   origin, and on GitHub Pages that origin has no API on it
    ///
    /// Either way the game loads, the microphone opens, and the command dies
    /// with nothing on screen to say why.
    /// </summary>
    public sealed class VoiceBackendAddressOriginTests
    {
        [TestCase("http://localhost:3000")]
        [TestCase("https://LOCALHOST:3000")]
        [TestCase("http://127.0.0.1:3000")]
        [TestCase("http://0.0.0.0:3000")]
        [TestCase("http://[::1]:3000")]
        public void LoopbackIsNotADeployableBackend(string origin)
        {
            Assert.That(
                VoiceBackendAddress.IsLoopback(origin),
                Is.True,
                $"'{origin}' points at the machine running the browser, which "
                + "is not serving the API. Honouring it from a deployed page "
                + "sends every voice command nowhere.");
        }

        [TestCase("https://pawlice.duckdns.org")]
        [TestCase("https://kimjuseong123.github.io")]
        [TestCase("https://voice.example.com:8443")]
        public void ADeployedHostIsNotMistakenForLoopback(string origin)
        {
            Assert.That(
                VoiceBackendAddress.IsLoopback(origin),
                Is.False,
                $"'{origin}' is a real host. Treating it as loopback drops back "
                + "to the page's own origin, and a page served from somewhere "
                + "that has no API then fails every command.");
        }

        [Test]
        public void EmptyIsNotLoopback()
        {
            // "Nothing was configured" and "loopback was configured" lead to the
            // same branch but are different facts; conflating them would hide
            // an empty asset behind a plausible-looking reason.
            Assert.That(VoiceBackendAddress.IsLoopback(string.Empty), Is.False);
            Assert.That(VoiceBackendAddress.IsLoopback(null), Is.False);
        }

        [Test]
        public void TheConfiguredAddressLosesItsPathAndTrailingSlash()
        {
            Assert.That(
                VoiceBackendAddress.OriginOf(
                    "https://pawlice.duckdns.org/Build/index.html?v=2"),
                Is.EqualTo("https://pawlice.duckdns.org"));
        }
    }
}
