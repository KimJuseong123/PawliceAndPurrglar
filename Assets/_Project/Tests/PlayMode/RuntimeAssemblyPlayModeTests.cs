using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Core;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class RuntimeAssemblyPlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimeAssembly_RemainsAvailableAfterAFrame()
        {
            Assert.That(
                GameSceneCatalog.GetName(GameSceneId.Game),
                Is.EqualTo("Game"));

            yield return null;

            Assert.That(GameSceneCatalog.BuildOrder, Has.Count.EqualTo(3));
        }
    }
}
