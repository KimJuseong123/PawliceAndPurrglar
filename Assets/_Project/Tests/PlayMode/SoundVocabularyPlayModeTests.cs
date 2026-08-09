using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Audio;
using PawsAndLoot.Gameplay.Interiors;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
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
                    && bank.Resolve(id) == null)
                .ToArray();

            Assert.That(
                silent,
                Is.Empty,
                "These ids resolve to no clip and are therefore silent for good: "
                + string.Join(", ", silent));
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
    }
}
