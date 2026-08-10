using NUnit.Framework;
using PawliceAndPurrglar.Core;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// NET-008. The rematch route has to be a separate handler from the plain
    /// load, because a client may ask for a rematch but may never load a scene
    /// itself.
    /// </summary>
    public sealed class NetworkSceneBridgeTests
    {
        [TearDown]
        public void TearDown()
        {
            NetworkSceneBridge.ClearHandler();
            NetworkSceneBridge.ClearRematchHandler();
        }

        [Test]
        public void WithoutAHandlerRematchDeclinesSoOfflinePlayIsUnaffected()
        {
            NetworkSceneBridge.ClearRematchHandler();

            Assert.That(NetworkSceneBridge.HasRematchHandler, Is.False);
            Assert.That(
                NetworkSceneBridge.TryRequestRematch(),
                Is.False,
                "Declining is what makes the caller fall back to a plain local "
                + "load when there is no session.");
        }

        [Test]
        public void RematchHandlerIsIndependentOfTheLoadHandler()
        {
            int loads = 0;
            int rematches = 0;
            NetworkSceneBridge.SetHandler(_ =>
            {
                loads++;
                return true;
            });
            NetworkSceneBridge.SetRematchHandler(() =>
            {
                rematches++;
                return true;
            });

            Assert.That(NetworkSceneBridge.TryRequestRematch(), Is.True);
            Assert.That(rematches, Is.EqualTo(1));
            Assert.That(
                loads,
                Is.EqualTo(0),
                "A rematch request must not go through the load handler; the "
                + "load handler refuses client-side loads.");

            Assert.That(NetworkSceneBridge.TryLoad("Game"), Is.True);
            Assert.That(loads, Is.EqualTo(1));
            Assert.That(rematches, Is.EqualTo(1));

            // Clearing one must not clear the other, or leaving a session would
            // silently disable ordinary scene loading.
            NetworkSceneBridge.ClearRematchHandler();
            Assert.That(NetworkSceneBridge.HasHandler, Is.True);
            Assert.That(NetworkSceneBridge.HasRematchHandler, Is.False);
        }

        [Test]
        public void ADecliningRematchHandlerFallsThroughToTheCaller()
        {
            NetworkSceneBridge.SetRematchHandler(() => false);

            Assert.That(
                NetworkSceneBridge.TryRequestRematch(),
                Is.False,
                "An installed handler that declines still has to let the "
                + "caller do the local load itself.");
        }
    }
}
