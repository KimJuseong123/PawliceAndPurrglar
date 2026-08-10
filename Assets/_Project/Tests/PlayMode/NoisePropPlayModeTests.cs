using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Gameplay.Sensing;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// The rubber chicken and the firework.
    ///
    /// Both do nothing but make a noise, which makes them the two props whose
    /// failure is completely silent: nobody is stunned, nobody loses money,
    /// nothing is destroyed. If the fuse never burns or the bang is never
    /// written down, the prop simply sits there and the only symptom is that
    /// the game is quieter than it should be.
    /// </summary>
    public sealed class NoisePropPlayModeTests
    {
        [Test]
        public void AFireworkGoesOffByItselfAndAChickenWaitsToBeTroddenOn()
        {
            PlacedTrap firework = CreateTrap(
                ThrowableKind.Firework,
                Vector3.zero);
            PlacedTrap chicken = CreateTrap(
                ThrowableKind.RubberChicken,
                new Vector3(50f, 0f, 0f));

            Assert.That(firework.HasFuse, Is.True);
            Assert.That(chicken.HasFuse, Is.False);

            // Nobody anywhere near either of them.
            Assert.That(
                chicken.TryTrigger(out PlayerRoleIdentity _),
                Is.False,
                "A chicken with nobody near it should stay quiet.");

            bool wentOff = false;
            for (float spent = 0f;
                spent < ThrowableCatalog.FireworkFuseSeconds + 0.2f;
                spent += 0.05f)
            {
                if (firework.TickFuse(0.05f))
                {
                    wentOff = true;
                    break;
                }
            }

            Assert.That(
                wentOff,
                Is.True,
                "A firework should go off whether or not anybody came near.");
            Assert.That(
                firework.TickFuse(1f),
                Is.False,
                "It should only go off once.");

            Object.DestroyImmediate(firework.gameObject);
            Object.DestroyImmediate(chicken.gameObject);
        }

        [Test]
        public void ANoiseIsHeardInsideItsRadiusAndNotOutside()
        {
            var boardObject = new GameObject("Noise Board");
            NoiseBoard board = boardObject.AddComponent<NoiseBoard>();

            Assert.That(board.IsRinging, Is.False);
            Assert.That(board.ReportedCount, Is.EqualTo(0));

            board.Report(Vector3.zero, 10f, PlayerRole.Thief);

            Assert.That(board.ReportedCount, Is.EqualTo(1));
            Assert.That(
                board.IsAudibleAt(new Vector3(9f, 0f, 0f)),
                Is.True);
            Assert.That(
                board.IsAudibleAt(new Vector3(11f, 0f, 0f)),
                Is.False);

            // Height is ignored on purpose: a bang on the pavement is heard on
            // the roof above it, and the map has roofs.
            Assert.That(
                board.IsAudibleAt(new Vector3(0f, 8f, 0f)),
                Is.True);

            board.Tick(NoiseBoard.DefaultRingSeconds + 0.1f);
            Assert.That(
                board.IsAudibleAt(Vector3.zero),
                Is.False,
                "A noise that has faded is not still audible.");
            Assert.That(
                board.ReportedCount,
                Is.EqualTo(1),
                "Fading is not forgetting that it happened.");

            Object.DestroyImmediate(boardObject);
        }

        [UnityTest]
        public IEnumerator AnAnimalGoesToABangUnlessItIsAlreadyAtFood()
        {
            var boardObject = new GameObject("Noise Board");
            NoiseBoard board = boardObject.AddComponent<NoiseBoard>();

            var animalObject = new GameObject("Cat");
            animalObject.SetActive(false);
            CompanionLure lure = animalObject.AddComponent<CompanionLure>();
            CompanionNoiseAttention ears =
                animalObject.AddComponent<CompanionNoiseAttention>();
            ears.Configure(board);
            animalObject.SetActive(true);
            yield return null;

            var near = new NoiseReport(
                new Vector3(3f, 0f, 0f),
                ThrowableCatalog.NoiseRadiusMeters,
                PlayerRole.Police,
                NoiseBoard.DefaultRingSeconds);

            Assert.That(
                ears.Consider(near),
                Is.True,
                "An idle animal should go and look.");
            Assert.That(lure.IsActive, Is.True);
            Assert.That(lure.Point, Is.EqualTo(near.At));

            // Food outranks noise. An animal sitting at a tin does not abandon
            // it because something banged, or the cheap prop beats the dear one
            // every time.
            Assert.That(
                ears.Consider(new NoiseReport(
                    new Vector3(-4f, 0f, 0f),
                    ThrowableCatalog.NoiseRadiusMeters,
                    PlayerRole.Police,
                    NoiseBoard.DefaultRingSeconds)),
                Is.False,
                "An animal already called somewhere should ignore a bang.");
            Assert.That(lure.Point, Is.EqualTo(near.At));

            lure.Clear();
            Assert.That(
                ears.Consider(new NoiseReport(
                    new Vector3(400f, 0f, 0f),
                    ThrowableCatalog.NoiseRadiusMeters,
                    PlayerRole.Police,
                    NoiseBoard.DefaultRingSeconds)),
                Is.False,
                "A bang out of earshot should not be heard.");

            Assert.That(ears.InvestigatedCount, Is.EqualTo(1));

            Object.DestroyImmediate(animalObject);
            Object.DestroyImmediate(boardObject);
        }

        [Test]
        public void NoisePropsHurtNobody()
        {
            foreach (ThrowableKind kind in new[]
            {
                ThrowableKind.RubberChicken,
                ThrowableKind.Firework
            })
            {
                Assert.That(
                    ThrowableCatalog.GetEffect(kind),
                    Is.EqualTo(TrapEffect.Noise),
                    $"{kind} should only make a noise.");

                // Being startled is not being held. A squawk that also stopped
                // you would quietly be the best trap in the game.
                Assert.That(
                    ThrowableCatalog.GetStunSeconds(kind),
                    Is.EqualTo(0f),
                    $"{kind} must not hold anybody.");
                Assert.That(
                    ThrowableCatalog.GetNoiseRadius(kind),
                    Is.GreaterThan(0f),
                    $"{kind} should carry.");
            }

            // The chicken belongs to neither side. Everything else that can be
            // placed belongs to one of them.
            Assert.That(
                ThrowableCatalog.GetOwner(ThrowableKind.RubberChicken),
                Is.Null);
            Assert.That(
                ThrowableCatalog.GetOwner(ThrowableKind.Firework),
                Is.EqualTo(PlayerRole.Thief));
        }

        private static PlacedTrap CreateTrap(
            ThrowableKind kind,
            Vector3 at)
        {
            var trapObject = new GameObject($"{kind} Trap");
            trapObject.transform.position = at;
            PlacedTrap trap = trapObject.AddComponent<PlacedTrap>();
            trap.Configure(1, kind, PlayerRole.Thief, new Active());
            return trap;
        }

        private sealed class Active : IMatchStateReader
        {
            public MatchState CurrentState => MatchState.Playing;
            public bool IsGameplayActive => true;
        }
    }
}
