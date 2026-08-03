using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class CompanionCommandEffectPlayModeTests
    {
        private static CompanionCommandRequest TrackRequest(float at = 0f)
        {
            return new CompanionCommandRequest(
                CompanionCommandId.Track,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                at);
        }

        private static CompanionCommandRequest DistractRequest(
            Vector3 target,
            float at = 0f)
        {
            return new CompanionCommandRequest(
                CompanionCommandId.Distract,
                PlayerRole.Thief,
                CompanionKind.Cat,
                CompanionCommandInputSource.Keyboard,
                at,
                null,
                target);
        }

        private static CompanionCommandRequest CatRoofRequest(float at = 0f)
        {
            return new CompanionCommandRequest(
                CompanionCommandId.Steal,
                PlayerRole.Thief,
                CompanionKind.Cat,
                CompanionCommandInputSource.Keyboard,
                at);
        }

        [UnityTest]
        public IEnumerator TrackFollowsTheTrailNotTheLiveThief()
        {
            var thief = new GameObject("Thief");
            ThiefScentTrail trail =
                thief.AddComponent<ThiefScentTrail>();
            trail.Configure(0.5f, 12f, 0.5f);

            // The thief walks a path, dropping points as it goes.
            thief.transform.position = new Vector3(0f, 0f, 0f);
            trail.Sample(0f);
            thief.transform.position = new Vector3(4f, 0f, 0f);
            trail.Sample(1f);
            Assert.That(trail.PointCount, Is.EqualTo(2));

            // Then it runs far away without any further sampling.
            thief.transform.position = new Vector3(60f, 0f, 60f);

            var resolverObject = new GameObject("Resolver");
            CompanionCommandResolver resolver =
                resolverObject.AddComponent<CompanionCommandResolver>();
            resolver.Configure(trail, null, null, 26f);

            CompanionCommandResolver.Resolution resolution =
                resolver.Resolve(TrackRequest(1.2f), Vector3.zero, 1.2f);

            Assert.That(resolution.Accepted, Is.True);
            Assert.That(
                resolution.Outcome,
                Is.EqualTo(CompanionCommandOutcome.TrailFound));
            Assert.That(resolution.Destination.HasValue, Is.True);
            Assert.That(
                resolution.Destination.Value,
                Is.EqualTo(new Vector3(4f, 0f, 0f)),
                "The dog must chase the freshest trail point, not the "
                + "thief's live position.");

            Object.DestroyImmediate(resolverObject);
            Object.DestroyImmediate(thief);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackFailsWhenTheTrailHasGoneCold()
        {
            var thief = new GameObject("Thief");
            ThiefScentTrail trail =
                thief.AddComponent<ThiefScentTrail>();
            trail.Configure(0.5f, 2f, 0.5f);
            trail.Sample(0f);
            Assert.That(trail.PointCount, Is.EqualTo(1));

            var resolverObject = new GameObject("Resolver");
            CompanionCommandResolver resolver =
                resolverObject.AddComponent<CompanionCommandResolver>();
            resolver.Configure(trail, null, null, 26f);

            // Past the point lifetime the trail is unusable.
            CompanionCommandResolver.Resolution resolution =
                resolver.Resolve(TrackRequest(10f), Vector3.zero, 10f);

            Assert.That(resolution.Accepted, Is.False);
            Assert.That(
                resolution.Outcome,
                Is.EqualTo(CompanionCommandOutcome.TrailMissing));
            Assert.That(trail.PointCount, Is.Zero);

            Object.DestroyImmediate(resolverObject);
            Object.DestroyImmediate(thief);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistractionIsInformationOnlyAndCannotStack()
        {
            var police = new GameObject("Police");
            police.transform.position = new Vector3(5f, 0f, 0f);
            var boardObject = new GameObject("Board");
            DistractionBoard board =
                boardObject.AddComponent<DistractionBoard>();
            board.Configure(4f);

            var resolverObject = new GameObject("Resolver");
            CompanionCommandResolver resolver =
                resolverObject.AddComponent<CompanionCommandResolver>();
            resolver.Configure(null, board, police.transform, 26f);

            Vector3 spot = new(8f, 0f, 0f);
            Vector3 thiefBefore = new(40f, 0f, 40f);
            var thief = new GameObject("Thief");
            thief.transform.position = thiefBefore;

            CompanionCommandResolver.Resolution first =
                resolver.Resolve(DistractRequest(spot), Vector3.zero, 0f);
            Assert.That(first.Accepted, Is.True);
            Assert.That(
                first.Outcome,
                Is.EqualTo(CompanionCommandOutcome.DistractionStarted));
            Assert.That(board.IsActive, Is.True);
            Assert.That(board.StartedCount, Is.EqualTo(1));

            // The thief must not be moved by its own distraction.
            Assert.That(thief.transform.position, Is.EqualTo(thiefBefore));

            // A second order while one runs is refused, not stacked.
            CompanionCommandResolver.Resolution second =
                resolver.Resolve(DistractRequest(spot), Vector3.zero, 1f);
            Assert.That(second.Accepted, Is.False);
            Assert.That(
                second.Outcome,
                Is.EqualTo(
                    CompanionCommandOutcome.DistractionAlreadyActive));
            Assert.That(board.StartedCount, Is.EqualTo(1));

            // It expires on its own.
            board.Tick(5f);
            Assert.That(board.IsActive, Is.False);
            Assert.That(board.RemainingSeconds(5f), Is.Zero);

            CompanionCommandResolver.Resolution third =
                resolver.Resolve(DistractRequest(spot), Vector3.zero, 5f);
            Assert.That(third.Accepted, Is.True);
            Assert.That(board.StartedCount, Is.EqualTo(2));

            Object.DestroyImmediate(resolverObject);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(police);
            Object.DestroyImmediate(thief);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistractionNeedsThePoliceWithinRange()
        {
            var police = new GameObject("Police");
            police.transform.position = new Vector3(200f, 0f, 200f);
            var boardObject = new GameObject("Board");
            DistractionBoard board =
                boardObject.AddComponent<DistractionBoard>();
            board.Configure(4f);

            var resolverObject = new GameObject("Resolver");
            CompanionCommandResolver resolver =
                resolverObject.AddComponent<CompanionCommandResolver>();
            resolver.Configure(null, board, police.transform, 26f);

            CompanionCommandResolver.Resolution resolution =
                resolver.Resolve(
                    DistractRequest(Vector3.zero),
                    Vector3.zero,
                    0f);

            Assert.That(resolution.Accepted, Is.False);
            Assert.That(
                resolution.Outcome,
                Is.EqualTo(
                    CompanionCommandOutcome.DistractionTargetTooFar));
            Assert.That(board.IsActive, Is.False);

            Object.DestroyImmediate(resolverObject);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(police);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CatRoofCommandTargetsTheNearestRooftop()
        {
            var mapObject = new GameObject("Map");
            GreyboxMapDefinition map =
                mapObject.AddComponent<GreyboxMapDefinition>();
            GameObject nearRoof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nearRoof.name = "Near Rooftop";
            nearRoof.transform.position = new Vector3(4f, 3.9f, 0f);
            nearRoof.transform.localScale = new Vector3(6f, 0.6f, 6f);
            GameObject farRoof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farRoof.name = "Far Rooftop";
            farRoof.transform.position = new Vector3(20f, 3.9f, 0f);
            farRoof.transform.localScale = new Vector3(6f, 0.6f, 6f);
            map.Configure(
                56f,
                44f,
                new GreyboxLocationReference[0],
                new GreyboxRouteReference[0],
                new[] { nearRoof.transform, farRoof.transform },
                new Transform[0],
                new Transform[0]);

            var resolverObject = new GameObject("Resolver");
            CompanionCommandResolver resolver =
                resolverObject.AddComponent<CompanionCommandResolver>();
            resolver.Configure(null, null, null, 26f);

            CompanionCommandResolver.Resolution resolution =
                resolver.Resolve(CatRoofRequest(), Vector3.zero, 0f);

            Assert.That(resolution.Accepted, Is.True);
            Assert.That(
                resolution.Outcome,
                Is.EqualTo(CompanionCommandOutcome.RoofClimbStarted));
            Assert.That(resolution.Destination.HasValue, Is.True);
            Assert.That(resolution.Destination.Value.x, Is.EqualTo(4f).Within(0.01f));
            Assert.That(resolution.Destination.Value.y, Is.GreaterThan(nearRoof.transform.position.y));

            Object.DestroyImmediate(resolverObject);
            Object.DestroyImmediate(mapObject);
            Object.DestroyImmediate(nearRoof);
            Object.DestroyImmediate(farRoof);
            yield return null;
        }

        [Test]
        public void FeedbackTextCoversEveryOutcomeAndRejection()
        {
            foreach (CompanionCommandOutcome outcome in
                System.Enum.GetValues(typeof(CompanionCommandOutcome)))
            {
                string text = CompanionCommandOutcomeText.Describe(
                    CompanionKind.Dog,
                    outcome);
                if (outcome == CompanionCommandOutcome.None)
                {
                    Assert.That(text, Is.Empty);
                    continue;
                }

                Assert.That(
                    text,
                    Does.StartWith("강아지: "),
                    $"Missing wording for {outcome}.");
            }

            foreach (CompanionCommandRejection rejection in
                System.Enum.GetValues(typeof(CompanionCommandRejection)))
            {
                string text = CompanionCommandOutcomeText.Describe(
                    CompanionKind.Cat,
                    rejection);
                if (rejection == CompanionCommandRejection.None)
                {
                    Assert.That(text, Is.Empty);
                    continue;
                }

                Assert.That(
                    text,
                    Does.StartWith("고양이: "),
                    $"Missing wording for {rejection}.");
            }
        }
    }
}
