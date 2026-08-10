using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Audio;
using PawliceAndPurrglar.Gameplay.Interiors;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// AUDIO-001, sections C-4 to C-11 of the acquisition sheet.
    ///
    /// Every failure this guards against is a silent one. A sound with no clip,
    /// a sound raised in a scene with nothing to play it, and a sound nobody
    /// raises all produce exactly the same thing from the player's chair, and
    /// none of them writes a line to any log.
    ///
    /// Batch mode has no audio device, so nothing here listens. The assertions
    /// are structural: the clip resolved, the object that plays it exists, the
    /// call was made.
    /// </summary>
    public sealed class SoundVocabularyPlayModeTests
    {
        private const string BankResourcePath = "Audio/GameSoundBank";

        private GameObject _audioObject;
        private GameObject _observerObject;
        private GameObject _persistentObject;

        [TearDown]
        public void TearDown()
        {
            // Destroyed rather than left for the frame end. `Object.Destroy` in a
            // play mode test applies at the end of the frame, so a service left
            // behind here is still the live singleton for whatever test runs next
            // — and every assertion in this file counts requests on it.
            if (_observerObject != null)
            {
                Object.DestroyImmediate(_observerObject);
            }

            if (_audioObject != null)
            {
                Object.DestroyImmediate(_audioObject);
            }

            if (_persistentObject != null)
            {
                Object.DestroyImmediate(_persistentObject);
            }

            // The overridden role is static and outlives the scene, so a test
            // that set one and did not clear it decides the role for whatever
            // runs next. That has already cost six unrelated failures once
            // (`ISSUE-054`).
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        /// <summary>
        /// The bank that ships, not one built in memory.
        ///
        /// `SoundBankCoversEverySoundId` already checks that a fresh bank grows an
        /// entry per id. That is the data structure; this is the asset, and the
        /// two fail differently — an id can have a slot in every bank ever made
        /// and still have no file behind it, which is what "the sound does
        /// nothing" actually looks like.
        /// </summary>
        /// <summary>
        /// The ids that are wired but have no recording yet.
        ///
        /// Written down rather than tolerated by counting, so that adding an id
        /// and forgetting the file fails here — and so that the day one of these
        /// three arrives, the line that has to be deleted is obvious.
        ///
        /// The two alert sounds have to be distinct recordings from the bark and
        /// the meow that answer a command: an animal noticing a bang must not
        /// sound like an animal being given an order.
        /// </summary>
        private static readonly GameSoundId[] AwaitingARecording =
        {
            GameSoundId.DogAlerted,
            GameSoundId.CatAlerted,
            GameSoundId.TrapSticky
        };

        [Test]
        public void TheShippedBankHasAClipForEverySoundId()
        {
            var bank = Resources.Load<GameSoundBank>(BankResourcePath);
            Assert.That(
                bank,
                Is.Not.Null,
                $"No sound bank at Resources/{BankResourcePath}. It has to be "
                + "under a Resources folder or every sound raised outside the "
                + "Game scene is dropped — the lobby, the result screen and the "
                + "voice model coming up.");

            GameSoundId[] silent = System.Enum
                .GetValues(typeof(GameSoundId))
                .Cast<GameSoundId>()
                .Where(id => id != GameSoundId.None
                    && !AwaitingARecording.Contains(id)
                    && bank.Resolve(id) == null)
                .ToArray();

            Assert.That(
                silent,
                Is.Empty,
                "These ids resolve to no clip and are therefore silent for good: "
                + string.Join(", ", silent));

            // The other direction. Without this the list above could be padded
            // with ids that do have a file, and the exemption would quietly grow
            // into a way of not noticing missing sounds.
            GameSoundId[] arrived = AwaitingARecording
                .Where(id => bank.Resolve(id) != null)
                .ToArray();

            Assert.That(
                arrived,
                Is.Empty,
                "These have a clip now and must come off the waiting list: "
                + string.Join(", ", arrived));
        }

        /// <summary>
        /// The lobby, the result screen, and anywhere else the service is not.
        ///
        /// `GameSoundService` is built into the Game scene by `GreyboxMapSetup`,
        /// so until the fallback existed a role being handed out or the other
        /// player arriving raised a sound into nothing. Nothing reported it,
        /// because a request with no service and a request with no clip both
        /// return without doing anything.
        /// </summary>
        [UnityTest]
        public IEnumerator SoundsRaisedWhereThereIsNoServiceStillPlay()
        {
            // The fallback branch directly rather than through `Request`.
            //
            // `Request` only takes this path when there is no service, and play
            // mode tests share one editor session — something earlier loads the
            // Game scene and leaves its "Game Audio" behind, so going through
            // `Request` here would quietly test the ordinary path instead.
            GameSoundService.PlayWithoutAService(GameSoundId.RoleAssignedThief);
            yield return null;

            _persistentObject = Object
                .FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .FirstOrDefault(
                    candidate => candidate.name == "Persistent One Shot Audio");

            Assert.That(
                _persistentObject,
                Is.Not.Null,
                "With no service in the scene the request has to fall through to "
                + "the persistent source, or the lobby is silent.");
            Assert.That(
                _persistentObject.scene.name,
                Is.EqualTo("DontDestroyOnLoad"),
                "The role stingers are raised just before Bootstrap unloads, so "
                + "the object playing them has to survive the load.");
        }

        /// <summary>
        /// A character with a role and an arm to draw back.
        ///
        /// Built inactive and activated last, the way every fixture here does:
        /// components that validate their dependencies in Awake throw when they
        /// are added to a live object one at a time.
        /// </summary>
        private static GameObject ThrowingPlayer(
            PlayerRole role,
            out ThrowChargeController charger)
        {
            var player = new GameObject($"{role} Player");
            player.SetActive(false);
            player.AddComponent<PlayerRoleIdentity>().Configure(role);
            charger = player.AddComponent<ThrowChargeController>();
            player.SetActive(true);
            return player;
        }

        /// <summary>
        /// The opponent drawing back an arm is silent on this screen.
        ///
        /// Not one sound too many but a leak. The wind-up is the window a throw
        /// can be dodged in, so hearing the opponent's is the whole of the
        /// counterplay — and every sound here is 2D, so it carried from
        /// anywhere on the map (`ISSUE-074`).
        /// </summary>
        [UnityTest]
        public IEnumerator OnlyThisPlayersThrowWindUpIsHeard()
        {
            GameObject mine = ThrowingPlayer(
                PlayerRole.Thief,
                out ThrowChargeController mineCharger);
            GameObject theirs = ThrowingPlayer(
                PlayerRole.Police,
                out ThrowChargeController theirCharger);

            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);

            GameSoundService service = GameSoundService.Instance;
            if (service == null)
            {
                _audioObject = new GameObject("Audio");
                _audioObject.SetActive(false);
                service = _audioObject.AddComponent<GameSoundService>();
                service.Configure(null, null, null);
                _audioObject.SetActive(true);
            }

            int before = service.GetRequestCount(GameSoundId.ThrowCharge);

            _observerObject = new GameObject("Sound Observer");
            _observerObject.AddComponent<GameSoundObserver>();
            yield return null;

            theirCharger.Begin();
            yield return null;
            Assert.That(
                service.GetRequestCount(GameSoundId.ThrowCharge) - before,
                Is.EqualTo(0),
                "Hearing the opponent wind up hands over the dodge window.");

            mineCharger.Begin();
            yield return null;
            Assert.That(
                service.GetRequestCount(GameSoundId.ThrowCharge) - before,
                Is.EqualTo(1));

            Object.DestroyImmediate(mine);
            Object.DestroyImmediate(theirs);
            yield return null;
        }

        /// <summary>
        /// The other player's door is silent on this screen.
        ///
        /// Both role objects exist on both machines, so the observer finds two
        /// interior states and used to sound the door for either — and since
        /// every sound in this game is 2D, at full volume from anywhere on the
        /// map. With two windows open on one machine that is one door heard
        /// twice, a moment apart (`ISSUE-074`).
        ///
        /// The same shape as the raccoon greeting and the jump: "it happened"
        /// answered where "it happened to me" was meant.
        /// </summary>
        [UnityTest]
        public IEnumerator OnlyTheDoorOfThePlayerAtThisKeyboardIsHeard()
        {
            var mine = new GameObject("Thief");
            mine.SetActive(false);
            mine.AddComponent<PlayerRoleIdentity>()
                .Configure(PlayerRole.Thief);
            PlayerInteriorState mineInterior =
                mine.AddComponent<PlayerInteriorState>();
            mine.SetActive(true);

            var theirs = new GameObject("Police");
            theirs.SetActive(false);
            theirs.AddComponent<PlayerRoleIdentity>()
                .Configure(PlayerRole.Police);
            PlayerInteriorState theirInterior =
                theirs.AddComponent<PlayerInteriorState>();
            theirs.SetActive(true);

            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);

            GameSoundService service = GameSoundService.Instance;
            if (service == null)
            {
                _audioObject = new GameObject("Audio");
                _audioObject.SetActive(false);
                service = _audioObject.AddComponent<GameSoundService>();
                service.Configure(null, null, null);
                _audioObject.SetActive(true);
            }

            int before = service.GetRequestCount(GameSoundId.DoorOpen);

            _observerObject = new GameObject("Sound Observer");
            _observerObject.AddComponent<GameSoundObserver>();
            yield return null;

            // The officer, on the other side of town, walks into a house.
            theirInterior.SetInterior(3);
            Assert.That(
                service.GetRequestCount(GameSoundId.DoorOpen) - before,
                Is.EqualTo(0),
                "A door across the map is not a door on this screen.");

            // Now the thief, whose screen this is.
            mineInterior.SetInterior(3);
            Assert.That(
                service.GetRequestCount(GameSoundId.DoorOpen) - before,
                Is.EqualTo(1));

            mineInterior.SetInterior(PlayerInteriorState.Outside);
            Assert.That(
                service.GetRequestCount(GameSoundId.DoorOpen) - before,
                Is.EqualTo(2),
                "Coming back out is the same door and the same sound.");

            Object.DestroyImmediate(mine);
            Object.DestroyImmediate(theirs);
            yield return null;
        }

        /// <summary>
        /// Going in and coming out are the same door and the same sound.
        ///
        /// Raised off the state rather than off `HouseDoorway`, which only runs
        /// on the host — hanging it on the decision would have left the client's
        /// own doors silent on their own screen.
        ///
        /// This also covers the observer finding its sources at runtime, which is
        /// the part with no other safety net: it is deliberately not a serialised
        /// reference, because a list an editor script fills does not survive being
        /// saved into a scene.
        /// </summary>
        [UnityTest]
        public IEnumerator EnteringAndLeavingAHouseBothRaiseTheDoorSound()
        {
            var interiorObject = new GameObject("Player");
            PlayerInteriorState interior =
                interiorObject.AddComponent<PlayerInteriorState>();

            // Whichever service is already here, or one of our own.
            //
            // A second `GameSoundService` destroys itself in Awake, so a test
            // that always made its own would be counting on an object that no
            // longer exists whenever an earlier test left the Game scene loaded.
            GameSoundService service = GameSoundService.Instance;
            if (service == null)
            {
                _audioObject = new GameObject("Audio");
                _audioObject.SetActive(false);
                service = _audioObject.AddComponent<GameSoundService>();
                service.Configure(null, null, null);
                _audioObject.SetActive(true);
            }

            // Counted as a change, not as a total: a shared service carries
            // whatever the rest of the suite has already asked it for.
            int before = service.GetRequestCount(GameSoundId.DoorOpen);

            _observerObject = new GameObject("Sound Observer");
            _observerObject.AddComponent<GameSoundObserver>();

            // One frame for the observer's Update to find what is in the scene.
            yield return null;

            interior.SetInterior(3);
            Assert.That(
                service.GetRequestCount(GameSoundId.DoorOpen) - before,
                Is.EqualTo(1),
                "Walking into a house has to make a door sound.");

            interior.SetInterior(PlayerInteriorState.Outside);
            Assert.That(
                service.GetRequestCount(GameSoundId.DoorOpen) - before,
                Is.EqualTo(2),
                "Coming back out is the same door and the same sound.");

            // Same value twice is not a second door.
            interior.SetInterior(PlayerInteriorState.Outside);
            Assert.That(
                service.GetRequestCount(GameSoundId.DoorOpen) - before,
                Is.EqualTo(2));

            Object.DestroyImmediate(interiorObject);
            yield return null;
        }

        /// <summary>
        /// Stars for a rock, and nothing on top of a banana or glue.
        ///
        /// A slip and a stick are stuns too, and each already has its own sound.
        /// Playing the star sting over them would be two sounds for one event
        /// where the first has already said what happened — so the rule is the
        /// cause, not the stun.
        /// </summary>
        [UnityTest]
        public IEnumerator OnlyARockToTheHeadMakesTheStarsSound()
        {
            var victimObject = new GameObject("Victim");
            StunState stun = victimObject.AddComponent<StunState>();

            GameSoundService service = GameSoundService.Instance;
            if (service == null)
            {
                _audioObject = new GameObject("Audio");
                _audioObject.SetActive(false);
                service = _audioObject.AddComponent<GameSoundService>();
                service.Configure(null, null, null);
                _audioObject.SetActive(true);
            }

            int before = service.GetRequestCount(GameSoundId.Stunned);

            _observerObject = new GameObject("Sound Observer");
            _observerObject.AddComponent<GameSoundObserver>();
            yield return null;

            Assert.That(
                stun.TryApply(0.2f, StunCause.Slip),
                Is.True,
                "The banana still has to stun; only its sound is different.");
            yield return null;
            Assert.That(
                service.GetRequestCount(GameSoundId.Stunned) - before,
                Is.Zero,
                "A slip already sounds like a slip.");

            // Waited out rather than cleared, because a stun refuses to restart
            // while the immunity gap is still running and the second apply would
            // silently do nothing.
            //
            // A frame per step, not a tight loop. The observer watches the edge
            // into a stun, so it has to be given a frame in which the first one
            // is over — recovering and being hit again between two frames is a
            // thing only a test can do.
            while (stun.IsStunned || stun.IsImmune)
            {
                stun.Tick(0.1f);
                yield return null;
            }

            Assert.That(
                stun.TryApply(0.2f, StunCause.Impact),
                Is.True);
            yield return null;
            Assert.That(
                service.GetRequestCount(GameSoundId.Stunned) - before,
                Is.EqualTo(1),
                "A rock to the head is the one that makes stars.");

            Object.DestroyImmediate(victimObject);
            yield return null;
        }
    }
}
