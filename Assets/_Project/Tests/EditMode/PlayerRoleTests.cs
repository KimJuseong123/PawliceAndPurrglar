using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class PlayerRoleTests
    {
        [Test]
        public void PoliceAndThiefHaveDistinctRoleValues()
        {
            Assert.That(PlayerRole.Police, Is.Not.EqualTo(PlayerRole.Thief));
        }

        [TestCase(PlayerRole.Police, PlayerInteractionType.Arrest, true)]
        [TestCase(PlayerRole.Police, PlayerInteractionType.Loot, false)]
        [TestCase(PlayerRole.Police, PlayerInteractionType.Sale, false)]
        [TestCase(PlayerRole.Thief, PlayerInteractionType.Arrest, false)]
        [TestCase(PlayerRole.Thief, PlayerInteractionType.Loot, true)]
        [TestCase(PlayerRole.Thief, PlayerInteractionType.Sale, true)]
        [TestCase(PlayerRole.Police, PlayerInteractionType.Traversal, true)]
        [TestCase(PlayerRole.Thief, PlayerInteractionType.Traversal, true)]
        public void RolePermissionsMatchRules(
            PlayerRole role,
            PlayerInteractionType interactionType,
            bool expected)
        {
            Assert.That(
                PlayerRolePermissions.CanInteract(role, interactionType),
                Is.EqualTo(expected));
        }

        [Test]
        public void RoleSpawnsAreDistinctAndDisplayed()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            GreyboxMapDefinition map = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<GreyboxMapDefinition>(true))
                .Single();
            PlayerRoleIdentity[] identities = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<PlayerRoleIdentity>(true))
                .ToArray();

            Vector3 policePosition =
                PlayerRoleSpawnResolver.Resolve(map, PlayerRole.Police).position;
            Vector3 thiefPosition =
                PlayerRoleSpawnResolver.Resolve(map, PlayerRole.Thief).position;

            Assert.That(
                Vector3.Distance(policePosition, thiefPosition),
                Is.GreaterThan(2f));
            Assert.That(identities, Has.Length.EqualTo(2));
            Assert.That(
                identities.Select(identity => identity.Role),
                Is.EquivalentTo(new[] { PlayerRole.Police, PlayerRole.Thief }));

            Renderer[] renderers = identities
                .Select(identity =>
                    identity.GetComponentInChildren<Renderer>(true))
                .ToArray();
            Assert.That(renderers, Has.All.Not.Null);
            Assert.That(
                renderers[0].sharedMaterial,
                Is.Not.SameAs(renderers[1].sharedMaterial));
        }
    }
}
