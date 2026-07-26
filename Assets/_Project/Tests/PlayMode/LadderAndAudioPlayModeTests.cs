using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Audio;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class LadderAndAudioPlayModeTests
    {
        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ready;

            public bool IsGameplayActive { get; set; }
        }

        private static LadderTraversal CreateLadder(
            MutableMatchState state,
            Vector3 ground,
            float roofHeight)
        {
            var root = new GameObject("Ladder");
            root.SetActive(false);
            root.transform.position = ground;
            BoxCollider area = root.AddComponent<BoxCollider>();
            area.isTrigger = true;

            var bottom = new GameObject("Bottom").transform;
            bottom.SetParent(root.transform);
            bottom.position = ground;
            var top = new GameObject("Top").transform;
            top.SetParent(root.transform);
            top.position = ground + new Vector3(2f, roofHeight, 0f);

            LadderTraversal ladder = root.AddComponent<LadderTraversal>();
            ladder.Configure(area, bottom, top, state);
            root.SetActive(true);
            return ladder;
        }

        private static PlayerRoleIdentity CreatePlayer(
            PlayerRole role,
            Vector3 position)
        {
            var player = new GameObject($"{role} Player");
            player.transform.position = position;
            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            return identity;
        }

        [UnityTest]
        public IEnumerator LadderCarriesAPlayerUpAndBackDown()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            Vector3 ground = new(0f, 1f, 0f);
            LadderTraversal ladder = CreateLadder(state, ground, 5f);
            PlayerRoleIdentity police =
                CreatePlayer(PlayerRole.Police, ground);
            CharacterController controller =
                police.GetComponent<CharacterController>();

            // Up.
            Assert.That(
                ladder.TryInteract(new PlayerInteractionContext(police)),
                Is.True);
            Assert.That(ladder.IsClimbing, Is.True);
            Assert.That(
                controller.enabled,
                Is.False,
                "The controller must be off during the climb so gravity "
                + "cannot fight the transport.");

            for (int step = 0; step < 200 && ladder.IsClimbing; step++)
            {
                ladder.Tick(0.02f);
            }

            Assert.That(ladder.IsClimbing, Is.False);
            Assert.That(controller.enabled, Is.True);
            Assert.That(
                police.transform.position.y,
                Is.EqualTo(ladder.TopPoint.position.y).Within(0.01f));

            // Down, using the same key.
            Assert.That(
                ladder.TryInteract(new PlayerInteractionContext(police)),
                Is.True);
            for (int step = 0; step < 200 && ladder.IsClimbing; step++)
            {
                ladder.Tick(0.02f);
            }

            Assert.That(
                police.transform.position.y,
                Is.EqualTo(ground.y).Within(0.01f));

            Object.DestroyImmediate(police.gameObject);
            Object.DestroyImmediate(ladder.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LadderIsUsableByBothRolesSoRoofsAreNotSafe()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            Vector3 ground = new(0f, 1f, 0f);
            LadderTraversal ladder = CreateLadder(state, ground, 5f);

            Assert.That(
                PlayerRolePermissions.CanInteract(
                    PlayerRole.Police,
                    ladder.InteractionType),
                Is.True);
            Assert.That(
                PlayerRolePermissions.CanInteract(
                    PlayerRole.Thief,
                    ladder.InteractionType),
                Is.True);

            PlayerRoleIdentity thief =
                CreatePlayer(PlayerRole.Thief, ground);
            Assert.That(
                ladder.TryInteract(new PlayerInteractionContext(thief)),
                Is.True);

            Object.DestroyImmediate(thief.gameObject);
            Object.DestroyImmediate(ladder.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LadderRefusesOutsideAMatchAndWhileBusy()
        {
            var state = new MutableMatchState { IsGameplayActive = false };
            LadderTraversal ladder =
                CreateLadder(state, new Vector3(0f, 1f, 0f), 5f);
            PlayerRoleIdentity police =
                CreatePlayer(PlayerRole.Police, new Vector3(0f, 1f, 0f));

            Assert.That(ladder.IsAvailable, Is.False);
            Assert.That(
                ladder.TryInteract(new PlayerInteractionContext(police)),
                Is.False);

            state.IsGameplayActive = true;
            Assert.That(
                ladder.TryInteract(new PlayerInteractionContext(police)),
                Is.True);

            // A second request mid-climb must not restart the trip.
            Assert.That(ladder.IsAvailable, Is.False);
            Assert.That(
                ladder.TryInteract(new PlayerInteractionContext(police)),
                Is.False);

            Object.DestroyImmediate(police.gameObject);
            Object.DestroyImmediate(ladder.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClimbEndsIfTheClimberDisappears()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            LadderTraversal ladder =
                CreateLadder(state, new Vector3(0f, 1f, 0f), 5f);
            PlayerRoleIdentity police =
                CreatePlayer(PlayerRole.Police, new Vector3(0f, 1f, 0f));

            Assert.That(
                ladder.TryInteract(new PlayerInteractionContext(police)),
                Is.True);
            Object.DestroyImmediate(police.gameObject);

            ladder.Tick(0.02f);
            Assert.That(
                ladder.IsClimbing,
                Is.False,
                "A destroyed climber must not leave the ladder locked.");
            Assert.That(ladder.IsAvailable, Is.True);

            Object.DestroyImmediate(ladder.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoundRequestsAreRecordedWithoutAnyClips()
        {
            var audioObject = new GameObject("Audio");
            audioObject.SetActive(false);
            GameSoundService service =
                audioObject.AddComponent<GameSoundService>();
            service.Configure(null, null, null);
            audioObject.SetActive(true);

            // No bank and no source: the call must still be safe and counted,
            // so rules never depend on audio being present.
            service.Play(GameSoundId.LootAcquired);
            service.Play(GameSoundId.LootAcquired);
            service.Play(GameSoundId.Victory);
            service.Play(GameSoundId.None);

            Assert.That(service.TotalRequests, Is.EqualTo(3));
            Assert.That(
                service.GetRequestCount(GameSoundId.LootAcquired),
                Is.EqualTo(2));
            Assert.That(
                service.LastRequested,
                Is.EqualTo(GameSoundId.Victory));
            Assert.That(
                service.GetRequestCount(GameSoundId.None),
                Is.Zero);

            Object.DestroyImmediate(audioObject);
            yield return null;
        }

        [Test]
        public void SoundBankCoversEverySoundId()
        {
            GameSoundBank bank =
                ScriptableObject.CreateInstance<GameSoundBank>();
            bank.EnsureAllSoundIds();

            int expected =
                System.Enum.GetValues(typeof(GameSoundId)).Length - 1;
            Assert.That(bank.EntryCount, Is.EqualTo(expected));
            Assert.That(bank.CountMissingClips(), Is.EqualTo(expected));
            Assert.That(
                bank.Resolve(GameSoundId.DogBark),
                Is.Null);
            Assert.That(
                bank.GetVolume(GameSoundId.DogBark),
                Is.EqualTo(1f));

            Object.DestroyImmediate(bank);
        }
    }
}
