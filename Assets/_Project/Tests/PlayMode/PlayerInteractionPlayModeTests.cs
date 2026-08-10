using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class PlayerInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScannerSelectsNearestRolePermittedTarget()
        {
            var state = new MutableMatchStateReader
            {
                IsGameplayActive = true
            };
            PlayerInteractionScanner police = CreateScanner(
                PlayerRole.Police,
                state);
            PrototypeInteractable loot = CreateTarget(
                "Loot",
                new Vector3(0.75f, 0f, 0f),
                PlayerInteractionType.Loot);
            PrototypeInteractable generic = CreateTarget(
                "Generic",
                new Vector3(1.25f, 0f, 0f),
                PlayerInteractionType.Generic);
            Physics.SyncTransforms();

            police.RefreshTarget();

            Assert.That(police.CurrentTarget, Is.SameAs(generic));
            Assert.That(police.TryInteractCurrent(), Is.True);
            Assert.That(generic.InteractionCount, Is.EqualTo(1));
            Assert.That(loot.InteractionCount, Is.EqualTo(0));

            Object.Destroy(police.gameObject);
            Object.Destroy(loot.gameObject);
            Object.Destroy(generic.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScannerPrefersHigherPriorityTargetBeforeDistance()
        {
            var state = new MutableMatchStateReader
            {
                IsGameplayActive = true
            };
            PlayerInteractionScanner thief = CreateScanner(
                PlayerRole.Thief,
                state);
            PrototypeInteractable loot = CreateTarget(
                "Nearby Loot",
                new Vector3(0.6f, 0f, 0f),
                PlayerInteractionType.Loot);
            PriorityInteractable catBag = CreatePriorityTarget(
                "Cat Bag",
                new Vector3(1.2f, 0f, 0f),
                PlayerInteractionType.Loot,
                50);
            Physics.SyncTransforms();

            thief.RefreshTarget();

            Assert.That(thief.CurrentTarget, Is.SameAs(catBag));
            Assert.That(thief.TryInteractCurrent(), Is.True);
            Assert.That(catBag.InteractionCount, Is.EqualTo(1));
            Assert.That(loot.InteractionCount, Is.EqualTo(0));

            Object.Destroy(thief.gameObject);
            Object.Destroy(loot.gameObject);
            Object.Destroy(catBag.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InteractionRequiresPlayingAndSurvivesDestroyedTarget()
        {
            var state = new MutableMatchStateReader();
            PlayerInteractionScanner thief = CreateScanner(
                PlayerRole.Thief,
                state);
            PrototypeInteractable loot = CreateTarget(
                "Loot",
                new Vector3(1f, 0f, 0f),
                PlayerInteractionType.Loot);
            Physics.SyncTransforms();

            thief.RefreshTarget();
            Assert.That(thief.HasTarget, Is.False);

            state.IsGameplayActive = true;
            thief.RefreshTarget();
            Assert.That(thief.CurrentTarget, Is.SameAs(loot));

            Object.Destroy(loot.gameObject);
            yield return null;

            Assert.That(thief.TryInteractCurrent(), Is.False);
            Assert.DoesNotThrow(thief.RefreshTarget);

            Object.Destroy(thief.gameObject);
            yield return null;
        }

        private static PlayerInteractionScanner CreateScanner(
            PlayerRole role,
            IMatchStateReader state)
        {
            var player = new GameObject(
                $"{role} Player",
                typeof(CapsuleCollider));
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerInteractionScanner scanner =
                player.AddComponent<PlayerInteractionScanner>();
            scanner.Configure(identity, config, state);
            return scanner;
        }

        private static PrototypeInteractable CreateTarget(
            string name,
            Vector3 position,
            PlayerInteractionType type)
        {
            GameObject target =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = position;
            PrototypeInteractable interactable =
                target.AddComponent<PrototypeInteractable>();
            interactable.Configure(type, name);
            return interactable;
        }

        private static PriorityInteractable CreatePriorityTarget(
            string name,
            Vector3 position,
            PlayerInteractionType type,
            int priority)
        {
            GameObject target =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = position;
            PriorityInteractable interactable =
                target.AddComponent<PriorityInteractable>();
            interactable.Configure(type, name, priority);
            return interactable;
        }

        private sealed class MutableMatchStateReader : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ready;

            public bool IsGameplayActive { get; set; }
        }

        private sealed class PriorityInteractable :
            MonoBehaviour,
            IPlayerInteractable,
            IInteractionPriority
        {
            private PlayerInteractionType interactionType;
            private string prompt;

            public Transform InteractionTransform => transform;
            public PlayerInteractionType InteractionType => interactionType;
            public string Prompt => prompt;
            public bool IsAvailable => true;
            public int InteractionPriority { get; private set; }
            public int InteractionCount { get; private set; }

            public void Configure(
                PlayerInteractionType configuredType,
                string configuredPrompt,
                int configuredPriority)
            {
                interactionType = configuredType;
                prompt = configuredPrompt;
                InteractionPriority = configuredPriority;
            }

            public bool TryInteract(PlayerInteractionContext context)
            {
                if (context.Player == null
                    || !context.Player.CanInteract(interactionType))
                {
                    return false;
                }

                InteractionCount++;
                return true;
            }
        }
    }
}
