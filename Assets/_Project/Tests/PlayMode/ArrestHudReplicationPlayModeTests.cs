using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Arrest;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// The arrest tally and the sentence have to reach the machine that is not
    /// simulating them.
    ///
    /// Both were read straight off components the host owns, so a client played
    /// the whole match with the counter on zero and no idea how long it had
    /// been locked up for. The rules were working perfectly and the player
    /// could not see them — a different failure from the rules being wrong, and
    /// no easier to play around.
    ///
    /// Neither of these decides anything, which is the point: the numbers are
    /// told to a machine that could not work them out, and nothing about who
    /// wins moves with them.
    /// </summary>
    public sealed class ArrestHudReplicationPlayModeTests
    {
        [Test]
        public void ATallyCanBeToldToAMachineThatIsNotCounting()
        {
            var host = new GameObject("Completion");
            host.SetActive(false);
            ArrestCompletionController completion =
                host.AddComponent<ArrestCompletionController>();

            int announced = -1;
            int announcedRequired = -1;
            completion.CatchCountChanged += (current, required) =>
            {
                announced = current;
                announcedRequired = required;
            };

            Assert.That(completion.CurrentCatchCount, Is.EqualTo(0));

            completion.ApplyReplicatedCatchCount(2);
            Assert.That(completion.CurrentCatchCount, Is.EqualTo(2));
            Assert.That(
                announced,
                Is.EqualTo(2),
                "The screen has to be told, not left to poll.");
            Assert.That(
                announcedRequired,
                Is.EqualTo(
                    ArrestCompletionController.DefaultRequiredCatchCount));

            // Told the same thing twice is not news.
            announced = -1;
            completion.ApplyReplicatedCatchCount(2);
            Assert.That(announced, Is.EqualTo(-1));

            Object.DestroyImmediate(host);
        }

        [Test]
        public void ASentenceCanBeToldButOnlyToTheMachineNotServingIt()
        {
            var thief = new GameObject("Thief");
            thief.SetActive(false);
            ThiefJailState jail = thief.AddComponent<ThiefJailState>();
            thief.SetActive(true);

            int jailed = 0;
            int released = 0;
            jail.Jailed += () => jailed++;
            jail.Released += () => released++;

            // The host serves its own sentence and must not have it overwritten
            // by a packet that is always a frame or two behind its own clock.
            jail.SetAuthority(true);
            jail.ApplyReplicatedRemaining(9f);
            Assert.That(
                jail.IsJailed,
                Is.False,
                "The machine running the sentence keeps its own clock.");

            jail.SetAuthority(false);
            jail.ApplyReplicatedRemaining(9f);
            Assert.That(jail.IsJailed, Is.True);
            Assert.That(
                jail.RemainingSeconds,
                Is.EqualTo(9f).Within(0.001f));
            Assert.That(
                jailed,
                Is.EqualTo(1),
                "Going in has to be announced, or nothing on screen changes.");

            jail.ApplyReplicatedRemaining(4f);
            Assert.That(jail.RemainingSeconds, Is.EqualTo(4f).Within(0.001f));
            Assert.That(jailed, Is.EqualTo(1), "Still the same sentence.");

            jail.ApplyReplicatedRemaining(0f);
            Assert.That(jail.IsJailed, Is.False);
            Assert.That(released, Is.EqualTo(1));

            Object.DestroyImmediate(thief);
        }
    }
}
