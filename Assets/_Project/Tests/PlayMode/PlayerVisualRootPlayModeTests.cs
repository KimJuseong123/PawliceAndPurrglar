using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class PlayerVisualRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator ReplacingVisualPreservesColliderAndMovement()
        {
            var player = new GameObject("PlayerRoot");
            player.SetActive(false);
            CharacterController controller =
                player.AddComponent<CharacterController>();
            PlayerMovementMotor motor =
                player.AddComponent<PlayerMovementMotor>();
            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();
            motor.Configure(
                controller,
                config,
                new ActiveMatchState(),
                null);
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Police);
            PlayerInteractionScanner scanner =
                player.AddComponent<PlayerInteractionScanner>();
            scanner.Configure(
                identity,
                config,
                new ActiveMatchState());
            player.AddComponent<NetworkObject>();

            var visualRootObject = new GameObject("VisualRoot");
            visualRootObject.transform.SetParent(player.transform, false);
            var placeholder = new GameObject("PlaceholderModel");
            placeholder.transform.SetParent(
                visualRootObject.transform,
                false);
            PlayerVisualRoot visualRoot =
                player.AddComponent<PlayerVisualRoot>();
            visualRoot.Configure(
                visualRootObject.transform,
                placeholder);
            player.SetActive(true);

            var replacementPrefab = new GameObject("ReplacementModel");
            replacementPrefab.SetActive(false);
            replacementPrefab.AddComponent<Animator>().applyRootMotion = true;
            GameObject replacement =
                visualRoot.ReplaceVisual(replacementPrefab);
            replacement.SetActive(true);
            motor.Move(Vector2.right, 1f);

            Assert.That(
                player.GetComponent<CharacterController>(),
                Is.SameAs(controller));
            Assert.That(player.transform.position.x, Is.GreaterThan(1f));
            Assert.That(
                replacement.GetComponent<Animator>().applyRootMotion,
                Is.False);
            Assert.DoesNotThrow(visualRoot.ValidateOrThrow);

            Object.Destroy(replacementPrefab);
            Object.Destroy(player);
            Object.Destroy(config);
            yield return null;
        }

        private sealed class ActiveMatchState : IMatchStateReader
        {
            public MatchState CurrentState => MatchState.Playing;
            public bool IsGameplayActive => true;
        }
    }
}
