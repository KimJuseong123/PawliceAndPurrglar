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

        /// <summary>
        /// The clip was never short. The AudioSource playing it lives in the Game
        /// scene, and the Result scene load that follows a win destroyed it a few
        /// frames in — so a two-second fanfare came out as a click (`ISSUE-070`).
        ///
        /// Asserted structurally rather than by listening: batch mode has no audio
        /// device, so `isPlaying` is false either way and an ear-based assertion
        /// would pass while the bug was back.
        /// </summary>
        [UnityTest]
        public IEnumerator MatchEndSoundsPlayFromAnObjectTheSceneLoadCannotDestroy()
        {
            GameSoundBank bank =
                ScriptableObject.CreateInstance<GameSoundBank>();
            bank.EnsureAllSoundIds();
            AudioClip fanfare = AudioClip.Create("fanfare", 44100, 1, 44100, false);
            Assert.That(
                bank.TryAssignClip(GameSoundId.Victory, fanfare),
                Is.True,
                "The bank must have a Victory slot to fill.");

            var audioObject = new GameObject("Audio");
            audioObject.SetActive(false);
            AudioSource sceneSource = audioObject.AddComponent<AudioSource>();
            sceneSource.playOnAwake = false;
            GameSoundService service =
                audioObject.AddComponent<GameSoundService>();
            service.Configure(bank, sceneSource, null);
            audioObject.SetActive(true);

            service.Play(GameSoundId.Victory);
            yield return null;

            GameObject persistent = null;
            foreach (GameObject candidate in
                Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (candidate.name == "Persistent One Shot Audio")
                {
                    persistent = candidate;
                    break;
                }
            }

            Assert.That(
                persistent,
                Is.Not.Null,
                "A match-end sound must play from a persistent object, or the "
                + "Result scene load cuts it off.");
            Assert.That(
                persistent.scene.name,
                Is.EqualTo("DontDestroyOnLoad"),
                "The player has to be outside the Game scene to survive the load.");
            Assert.That(
                GameSoundBank.OutlivesTheScene(GameSoundId.Victory),
                Is.True);
            Assert.That(
                GameSoundBank.OutlivesTheScene(GameSoundId.Defeat),
                Is.True);
            Assert.That(
                GameSoundBank.OutlivesTheScene(GameSoundId.LootAcquired),
                Is.False,
                "Only the match-end stingers outlive the scene; everything else "
                + "belongs to the match that raised it.");

            Object.DestroyImmediate(audioObject);
            Object.DestroyImmediate(persistent);
            Object.DestroyImmediate(bank);
            yield return null;
        }

        /// <summary>
        /// Whose ears, not whose win. The handler played the fanfare whenever the
        /// police won, so the thief heard a victory sting for losing on every match
        /// — the same winner-vs-viewer confusion `UI-016` fixed in the title.
        /// </summary>
        [Test]
        public void TheMatchEndStingFollowsTheViewerNotTheWinner()
        {
            Assert.That(
                GameSoundObserver.ResolveMatchEndSound(
                    MatchWinner.Police, PlayerRole.Police),
                Is.EqualTo(GameSoundId.Victory));
            Assert.That(
                GameSoundObserver.ResolveMatchEndSound(
                    MatchWinner.Police, PlayerRole.Thief),
                Is.EqualTo(GameSoundId.Defeat),
                "The thief lost; the fanfare is not theirs.");
            Assert.That(
                GameSoundObserver.ResolveMatchEndSound(
                    MatchWinner.Thief, PlayerRole.Thief),
                Is.EqualTo(GameSoundId.Victory),
                "The thief won and must hear the fanfare.");
            Assert.That(
                GameSoundObserver.ResolveMatchEndSound(
                    MatchWinner.Thief, PlayerRole.Police),
                Is.EqualTo(GameSoundId.Defeat));
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
