using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class ThrowHitQueryPlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject>
            _spawned = new();

        [TearDown]
        public void DestroySpawnedObjects()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator PoliceGetsBonusRadiusOnlyAgainstThief()
        {
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(
                    ThrowableCatalog.ThrowHitRadiusMeters + 0.2f,
                    0f,
                    6f));
            yield return null;

            Assert.That(
                ThrowResolver.Resolve(
                    police,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Hit,
                Is.SameAs(thief));

            Object.DestroyImmediate(police.gameObject);
            thief.transform.position = Vector3.zero;
            PlayerRoleIdentity policeTarget = CreatePlayer(
                PlayerRole.Police,
                new Vector3(
                ThrowableCatalog.ThrowHitRadiusMeters + 0.2f,
                0f,
                6f));
            Physics.SyncTransforms();

            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False,
                "A thief throw must keep the base radius against police.");
            Assert.That(policeTarget, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PoliceBonusDoesNotReachBeyondItsExpandedRadius()
        {
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            CreatePlayer(
                PlayerRole.Thief,
                new Vector3(
                    ThrowableCatalog.ThrowHitRadiusMeters
                        + ThrowableCatalog.PoliceThrowHitRadiusBonusMeters
                        + 0.1f,
                    0f,
                    6f));
            yield return null;

            Assert.That(
                ThrowResolver.Resolve(
                    police,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False);
        }

        private PlayerRoleIdentity CreatePlayer(
            PlayerRole role,
            Vector3 position)
        {
            var player = new GameObject($"{role} Throw Target");
            player.transform.position = position;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            _spawned.Add(player);
            return identity;
        }
    }
}
