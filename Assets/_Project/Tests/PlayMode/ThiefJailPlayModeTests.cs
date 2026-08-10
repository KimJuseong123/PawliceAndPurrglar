using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Arrest;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// A caught thief is held, then put back on the map able to play.
    ///
    /// Three of the four things checked here have gone wrong before in this
    /// project and none of them raised anything at the time. A teleport that
    /// places the pivot on the floor buries the capsule, because the pivot sits
    /// most of a metre above the soles and the controller resolves the overlap
    /// by pushing the player down through the slab. Fall speed carries across a
    /// teleport, so a few in a row build up enough to punch through the floor
    /// between two frames. And a state flag left set when the simulation stops
    /// freezes whoever was holding it.
    /// </summary>
    public sealed class ThiefJailPlayModeTests
    {
        private GameObject _floor;
        private GameObject _thiefObject;
        private GameObject _matchObject;
        private MatchConfig _matchConfig;
        private ArrestConfig _arrestConfig;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = 1f / 60f;
        }

        /// <summary>
        /// Everything made here is destroyed immediately rather than left to
        /// <c>Destroy</c>, which only takes effect at the end of the frame and
        /// leaves the next test running against a floor and a player it did not
        /// create.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            foreach (Object created in
                new Object[]
                {
                    _thiefObject,
                    _matchObject,
                    _floor,
                    _matchConfig,
                    _arrestConfig
                })
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }
        }

        [UnityTest]
        public IEnumerator JailHoldsTheThiefThenPutsThemBackOnTheGround()
        {
            BuildWorld();
            var jail = _thiefObject.GetComponent<ThiefJailState>();
            var controller = _thiefObject.GetComponent<CharacterController>();
            var motor = _thiefObject.GetComponent<PlayerMovementMotor>();

            var cell = new Vector3(30f, 0f, 30f);
            var release = new Vector3(-10f, 0f, 5f);

            Assert.That(jail.TryJail(2f, cell, release), Is.True);
            Assert.That(jail.IsJailed, Is.True);
            yield return null;

            // Standing on the cell floor, not sunk into it. Measured from the
            // bottom of the capsule: the pivot stays where it was put, so a
            // check on transform.position passes while the feet are a metre
            // underground.
            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThan(-0.2f),
                $"The thief's feet are at {controller.bounds.min.y:0.00}, "
                + "which means the capsule was buried in the cell floor.");
            Assert.That(
                Vector2.Distance(
                    new Vector2(
                        _thiefObject.transform.position.x,
                        _thiefObject.transform.position.z),
                    new Vector2(cell.x, cell.z)),
                // Inside the cell, not standing exactly on the spot they were
                // put. They can walk now, so half a metre was measuring that
                // their input was ignored. The cell floor is twelve metres
                // across; anything within five of the middle is still in it.
                Is.LessThan(5f),
                "The thief left the cell. What holds them is the room, so if "
                + "they are outside it the walls are not doing their job.");

            // Held, but not frozen.
            //
            // A jailed thief used to have their input ignored, and it read as the
            // game having stopped responding: eleven seconds of pressing keys
            // against a character who would not turn. They walk around the cell
            // now. What holds them is the cell, not a disabled motor — the
            // sentence is a place they cannot leave rather than a moment they
            // cannot act in.
            Assert.That(
                motor.CanMove,
                Is.True,
                "A jailed thief whose input is ignored reads as a frozen game "
                + "rather than as a sentence.");
            Vector3 heldAt = _thiefObject.transform.position;
            for (int frame = 0; frame < 30; frame++)
            {
                motor.Move(Vector2.one, Time.deltaTime);
                yield return null;
            }

            // Measured against the cell, not against where they were standing.
            //
            // This asked that thirty frames of held input moved them less than
            // half a metre, which is another way of saying their input was
            // ignored. It is not any more: a jailed thief walks around the cell,
            // because eleven seconds of a character refusing to turn read as the
            // game having stopped rather than as a sentence.
            //
            // What has to hold is that walking does not get them out.
            Assert.That(
                Vector3.Distance(
                    _thiefObject.transform.position,
                    heldAt),
                Is.LessThan(5f),
                "The thief walked out of the cell. They may move inside it, but "
                + "the walls have to keep them there — the sentence is a place, "
                + "not a frozen frame.");

            // Served, and back out on the map.
            for (int frame = 0; frame < 180 && jail.IsJailed; frame++)
            {
                yield return null;
            }

            Assert.That(jail.IsJailed, Is.False, "The sentence never ended.");
            Assert.That(
                Vector2.Distance(
                    new Vector2(
                        _thiefObject.transform.position.x,
                        _thiefObject.transform.position.z),
                    new Vector2(release.x, release.z)),
                Is.LessThan(0.5f),
                "Released somewhere other than the release point.");
            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThan(-0.2f),
                "Released underground.");

            // And playing again.
            Assert.That(motor.CanMove, Is.True);
            Vector3 before = _thiefObject.transform.position;
            for (int frame = 0; frame < 30; frame++)
            {
                motor.Move(Vector2.right, Time.deltaTime);
                yield return null;
            }

            Assert.That(
                Vector3.Distance(_thiefObject.transform.position, before),
                Is.GreaterThan(0.3f),
                "The thief is out but still cannot move.");
        }

        /// <summary>
        /// Repeated sentences do not sink the player.
        ///
        /// Gravity keeps accumulating while the controller is momentarily off
        /// the ground after a placement. Three trips through a door was enough
        /// to build up speed that went through the floor between frames, and
        /// three arrests is exactly how many trips this match has.
        /// </summary>
        [UnityTest]
        public IEnumerator ThreeSentencesLeaveTheThiefOnTheFloor()
        {
            BuildWorld();
            var jail = _thiefObject.GetComponent<ThiefJailState>();
            var controller = _thiefObject.GetComponent<CharacterController>();

            for (int sentence = 0; sentence < 3; sentence++)
            {
                Assert.That(
                    jail.TryJail(
                        0.2f,
                        new Vector3(30f, 0f, 30f),
                        new Vector3(0f, 0f, 0f)),
                    Is.True,
                    $"Sentence {sentence + 1} was refused.");

                for (int frame = 0; frame < 120 && jail.IsJailed; frame++)
                {
                    yield return null;
                }

                Assert.That(jail.IsJailed, Is.False);
            }

            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
            }

            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThan(-0.2f),
                $"After three sentences the feet are at "
                + $"{controller.bounds.min.y:0.00}: the fall speed carried "
                + "across the placements.");
        }

        [UnityTest]
        public IEnumerator ASecondSentenceCannotStartWhileServingTheFirst()
        {
            BuildWorld();
            var jail = _thiefObject.GetComponent<ThiefJailState>();

            Assert.That(
                jail.TryJail(5f, Vector3.zero, Vector3.one),
                Is.True);
            Assert.That(
                jail.TryJail(5f, Vector3.zero, Vector3.one),
                Is.False,
                "An officer standing on a jailed thief extends the sentence "
                + "every frame.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AClientDoesNotRunItsOwnJailClock()
        {
            BuildWorld();
            var jail = _thiefObject.GetComponent<ThiefJailState>();
            jail.SetAuthority(false);

            Assert.That(
                jail.TryJail(2f, Vector3.zero, Vector3.one),
                Is.False,
                "The client jailed the thief itself, so its clock and the "
                + "host's would run separately.");
            yield return null;
        }

        private void BuildWorld()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Jail Test Floor";
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(200f, 1f, 200f);

            _matchConfig = ScriptableObject.CreateInstance<MatchConfig>();
            _arrestConfig = ScriptableObject.CreateInstance<ArrestConfig>();

            _matchObject = new GameObject("Match Runtime");
            _matchObject.SetActive(false);
            MatchRuntimeState runtime =
                _matchObject.AddComponent<MatchRuntimeState>();
            runtime.Configure(_matchConfig, false);
            _matchObject.SetActive(true);
            runtime.BeginCountdown();
            runtime.Tick(_matchConfig.ReadyCountdownSeconds);
            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Playing));

            _thiefObject = new GameObject("Thief");
            _thiefObject.SetActive(false);
            _thiefObject.transform.position = new Vector3(0f, 1f, 0f);

            CharacterController controller =
                _thiefObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.zero;

            PlayerConfig playerConfig =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerMovementMotor motor =
                _thiefObject.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, playerConfig, runtime, null);
            _thiefObject.AddComponent<ThiefJailState>();
            _thiefObject.SetActive(true);

            Physics.SyncTransforms();
        }
    }
}
