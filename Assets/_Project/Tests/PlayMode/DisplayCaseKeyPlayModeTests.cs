using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Gameplay.Sensing;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// A key opens a case without a sound, and costs time to make up for it.
    ///
    /// The trade is the whole feature and it lives in two numbers and one event:
    /// unlocking is slower than smashing, and it does not reach the noise board.
    /// Either half on its own would be broken — a quiet instant open makes the
    /// glass pointless, and a slow open that still screams makes the key
    /// pointless.
    ///
    /// Measured off the noise board rather than off a flag, because what matters
    /// is whether the officer is told. A case that sets <c>OpenedQuietly</c> and
    /// reports the smash anyway would pass a flag test and lose the match.
    /// </summary>
    public sealed class DisplayCaseKeyPlayModeTests
    {
        private GameObject _case;
        private GameObject _thief;
        private GameObject _board;

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate, because Destroy waits for the end of the frame
            // and the next test would find this thief still standing.
            foreach (GameObject made in new[] { _case, _thief, _board })
            {
                if (made != null)
                {
                    Object.DestroyImmediate(made);
                }
            }

            _case = null;
            _thief = null;
            _board = null;
        }

        [UnityTest]
        public IEnumerator AKeyOpensTheCaseWithoutAnybodyHearing()
        {
            Fixture fixture = Create(withKey: true);

            int opened = Hold(fixture, LootDisplayCase.UnlockSeconds + 0.2f);

            Assert.That(
                fixture.Case.IsSealed,
                Is.False,
                "The key did not open the case at all.");
            Assert.That(
                fixture.Case.OpenedQuietly,
                Is.True,
                "The case opened loudly with a key in hand.");
            Assert.That(
                fixture.Board.ReportedCount,
                Is.Zero,
                $"Unlocking put {fixture.Board.ReportedCount} sounds on the noise "
                + "board. A key that carries thirty metres is not worth "
                + "fetching.");
            Assert.That(
                fixture.Key.HasKey,
                Is.False,
                "The key survived being used, so every case after this one is "
                + "quiet too and breaking glass becomes a mistake.");
            Assert.That(opened, Is.GreaterThan(0));

            yield return null;
        }

        [UnityTest]
        public IEnumerator WithoutAKeyTheGlassStillBreaksAndIsHeard()
        {
            Fixture fixture = Create(withKey: false);

            Hold(fixture, LootDisplayCase.BreakSeconds + 0.2f);

            Assert.That(fixture.Case.IsSealed, Is.False);
            Assert.That(
                fixture.Case.OpenedQuietly,
                Is.False,
                "The case opened quietly with no key in hand.");
            Assert.That(
                fixture.Board.ReportedCount,
                Is.EqualTo(1),
                "Breaking glass has to reach the noise board. It is the one "
                + "sound in this game the officer cannot have made himself.");

            yield return null;
        }

        /// <summary>
        /// The quiet way must be the slower way. Otherwise the key removes a
        /// decision instead of creating one.
        /// </summary>
        [UnityTest]
        public IEnumerator UnlockingIsSlowerThanSmashing()
        {
            Assert.That(
                LootDisplayCase.UnlockSeconds,
                Is.GreaterThan(LootDisplayCase.BreakSeconds));

            Fixture fixture = Create(withKey: true);

            // Held for exactly as long as a smash would take.
            Hold(fixture, LootDisplayCase.BreakSeconds + 0.05f);

            Assert.That(
                fixture.Case.IsSealed,
                Is.True,
                "A key opened the case in the time a smash takes, so the quiet "
                + "option costs nothing.");

            yield return null;
        }

        private static int Hold(Fixture fixture, float seconds)
        {
            const float Step = 0.05f;
            int opened = 0;
            for (float held = 0f; held < seconds; held += Step)
            {
                fixture.Case.Tick(Step);
                if (fixture.Case.Request(PlayerRole.Thief, fixture.Key))
                {
                    opened++;
                }
            }

            return opened;
        }

        private sealed class Active : IMatchStateReader
        {
            public MatchState CurrentState => MatchState.Playing;
            public bool IsGameplayActive => true;
        }

        private sealed class Fixture
        {
            public LootDisplayCase Case;
            public DisplayCaseKeyHolder Key;
            public NoiseBoard Board;
        }

        private Fixture Create(bool withKey)
        {
            _board = new GameObject("Noise Board");
            NoiseBoard board = _board.AddComponent<NoiseBoard>();

            _thief = new GameObject("Thief");
            _thief.AddComponent<PlayerRoleIdentity>()
                .Configure(PlayerRole.Thief);
            DisplayCaseKeyHolder key =
                _thief.AddComponent<DisplayCaseKeyHolder>();
            if (withKey)
            {
                Assert.That(key.TryTake(), Is.True);
            }

            _case = new GameObject("Case");
            var pane = new GameObject("Glass");
            pane.transform.SetParent(_case.transform, false);
            LootDisplayCase display = _case.AddComponent<LootDisplayCase>();
            display.Configure(null, pane.transform, new Active(), board);

            return new Fixture
            {
                Case = display,
                Key = key,
                Board = board
            };
        }
    }
}
