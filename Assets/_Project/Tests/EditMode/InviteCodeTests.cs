using NUnit.Framework;
using PawliceAndPurrglar.Integration.Network;
using PawliceAndPurrglar.Integration.Voice;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// The code makes a trip this game does not control: it is read out loud,
    /// or pasted through a chat window that adds things to it. Everything here
    /// is a shape a real paste actually takes.
    /// </summary>
    public sealed class InviteCodeTests
    {
        [TestCase("ABC123", "ABC123")]
        [TestCase("abc123", "ABC123")]
        [TestCase("  ABC123  ", "ABC123")]
        [TestCase("ABC-123", "ABC123")]
        [TestCase("ABC_123", "ABC123")]
        [TestCase("ABC 123", "ABC123")]
        [TestCase("ABC123\n", "ABC123")]
        [TestCase("ABC123\r\n", "ABC123")]
        public void PastedCodesAreAccepted(string raw, string expected)
        {
            Assert.That(InviteCode.Normalise(raw), Is.EqualTo(expected));
            Assert.That(
                InviteCode.TryNormalise(raw, out string code),
                Is.True,
                raw);
            Assert.That(code, Is.EqualTo(expected));
            Assert.That(InviteCode.DescribeProblem(raw), Is.Null);
        }

        /// <summary>
        /// A zero-width space rides along invisibly on a copy out of a browser
        /// and cannot be seen in the field, so a code that looks exactly right
        /// fails to match and the player has nothing to correct.
        /// </summary>
        [Test]
        public void InvisibleCharactersAreStripped()
        {
            Assert.That(
                InviteCode.Normalise("AB\u200BC12\uFEFF3"),
                Is.EqualTo("ABC123"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("ABC12")]
        [TestCase("ABC1234")]
        [TestCase("ABC12!")]
        [TestCase("https://example.com/ABC123")]
        public void MalformedCodesAreRefusedWithAReason(string raw)
        {
            Assert.That(InviteCode.IsValid(InviteCode.Normalise(raw)), Is.False);
            Assert.That(
                InviteCode.DescribeProblem(raw),
                Is.Not.Null.And.Not.Empty,
                $"'{raw}' was refused without saying why.");
        }

        /// <summary>
        /// Too short and wrong characters are different mistakes: one means a
        /// character was missed, the other means a URL was pasted. A single
        /// "invalid code" would leave the player retyping the same thing.
        /// </summary>
        [Test]
        public void TheReasonDistinguishesLengthFromContent()
        {
            Assert.That(
                InviteCode.DescribeProblem("ABC12"),
                Does.Contain("6글자"));
            Assert.That(
                InviteCode.DescribeProblem("ABC12!"),
                Does.Contain("숫자"));
        }

        /// <summary>
        /// Every message this screen can show has to fit the rect it is drawn
        /// in. TMP draws nothing at all when a line does not fit, so a long
        /// refusal does not merely look wrong — it disappears, and the player
        /// is left with a button that did nothing.
        /// </summary>
        [TestCase("")]
        [TestCase("ABC12")]
        [TestCase("ABC12!")]
        public void RefusalsAreShortEnoughForTheStatusLine(string raw)
        {
            string message = InviteCode.DescribeProblem(raw);
            Assert.That(
                message.Length,
                Is.LessThanOrEqualTo(26),
                $"'{message}' is {message.Length} characters and the status "
                + "line holds about 26.");
        }
    }

    /// <summary>
    /// A deployed page and its API are the same host. Getting this wrong is
    /// silent in the editor and total in the browser: an https page cannot call
    /// <c>http://localhost:3000</c> at all.
    /// </summary>
    public sealed class VoiceBackendAddressTests
    {
        [TestCase("https://play.example.com/index.html", "https://play.example.com")]
        [TestCase("https://play.example.com/", "https://play.example.com")]
        [TestCase("https://play.example.com", "https://play.example.com")]
        [TestCase("http://localhost:8080/game/", "http://localhost:8080")]
        [TestCase("https://play.example.com:8443/a/b?c=d", "https://play.example.com:8443")]
        [TestCase("https://play.example.com?c=d", "https://play.example.com")]
        [TestCase("https://play.example.com#top", "https://play.example.com")]
        public void OriginKeepsSchemeAndHostAndNothingElse(
            string url,
            string expected)
        {
            Assert.That(VoiceBackendAddress.OriginOf(url), Is.EqualTo(expected));
        }

        /// <summary>
        /// Empty rather than a guess. A half-parsed origin would produce a
        /// request that goes somewhere, and the somewhere would be wrong.
        /// </summary>
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("play.example.com")]
        [TestCase("file:///C:/build/index.html")]
        [TestCase("://nohost")]
        public void AnythingThatIsNotAnHttpUrlYieldsNothing(string url)
        {
            Assert.That(VoiceBackendAddress.OriginOf(url), Is.Empty);
        }
    }
}
