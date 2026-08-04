using System;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// Every prop that has art must be loadable, the right size, and standing on
    /// the ground.
    ///
    /// Each of those has already failed in this project on something else. A
    /// model outside Resources loads as null in a build and the prop falls back
    /// to a grey shape, which reads as the feature never having been wired. A
    /// generated model arrives at any size at all — the five imported for this
    /// pass measured between 4 mm and 1 cm, so one of them needed scaling by a
    /// hundred. And a prop centred on the ground is half buried, which on a
    /// banana lying flat is most of the banana.
    ///
    /// Measured off the generated prefab rather than the source FBX, because the
    /// prefab is where the scale is baked and the prefab is what the match
    /// loads.
    /// </summary>
    public sealed class ThrowablePropModelTests
    {
        private const string PrefabFolder = "Assets/_Project/Resources/Props";

        private static ThrowableKind[] KindsWithArt =>
            Enum.GetValues(typeof(ThrowableKind))
                .Cast<ThrowableKind>()
                .Where(kind => !string.IsNullOrEmpty(
                    ThrowableCatalog.GetModelStem(kind)))
                .ToArray();

        [Test]
        public void EveryPropWithArtHasItsOwnModel()
        {
            // Not one model shared by eight props, which is what it was: every
            // kind but the rock answered "tuna can", so a banana, a firework and
            // a rubber chicken all looked like lunch.
            string[] stems = KindsWithArt
                .Select(ThrowableCatalog.GetModelStem)
                .ToArray();

            Assert.That(
                stems.Distinct().Count(),
                Is.EqualTo(stems.Length),
                "Two props share a model: "
                + string.Join(", ", stems.GroupBy(s => s)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)));
        }

        [Test]
        public void EveryPropModelIsLoadableFromResources()
        {
            foreach (ThrowableKind kind in KindsWithArt)
            {
                string stem = ThrowableCatalog.GetModelStem(kind);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{PrefabFolder}/{stem}.prefab");
                Assert.That(
                    prefab,
                    Is.Not.Null,
                    $"{kind} names '{stem}' but there is no prefab under "
                    + "Resources, so a build draws a grey shape instead. Run "
                    + "'Sync Throwable Props To Resources'.");
            }
        }

        [Test]
        public void EveryPropModelIsTheSizeItWasAskedFor()
        {
            foreach (ThrowableKind kind in KindsWithArt)
            {
                GameObject instance = Instantiate(kind);
                try
                {
                    Bounds bounds = Measure(instance);
                    float size = Mathf.Max(
                        bounds.size.x,
                        Mathf.Max(bounds.size.y, bounds.size.z));
                    float target = ThrowableCatalog.GetModelSize(kind);

                    Assert.That(
                        size,
                        Is.EqualTo(target).Within(0.02f),
                        $"{kind} measures {size:0.000} m along its longest side "
                        + $"against a target of {target:0.00} m. Measured the "
                        + "long way round because the footprint leaves a "
                        + "standing prop any height it likes — the rubber "
                        + "chicken came out over a metre tall.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void NoPropIsTallerThanAKnee()
        {
            // A thrown prop and a placed prop are both things on a street with
            // people running past them. The catalogue asks for sizes under half
            // a metre, and this pins that the models come out that way rather
            // than at whatever the generator produced.
            foreach (ThrowableKind kind in KindsWithArt)
            {
                GameObject instance = Instantiate(kind);
                try
                {
                    Bounds bounds = Measure(instance);
                    Assert.That(
                        bounds.size.y,
                        Is.LessThan(0.6f),
                        $"{kind} stands {bounds.size.y:0.00} m high. A prop "
                        + "taller than that reads as scenery rather than "
                        + "something somebody dropped.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void EveryPropModelSitsOnTheGroundRatherThanInIt()
        {
            foreach (ThrowableKind kind in KindsWithArt)
            {
                GameObject instance = Instantiate(kind);
                try
                {
                    Bounds bounds = Measure(instance);
                    Assert.That(
                        bounds.min.y,
                        Is.EqualTo(0f).Within(0.02f),
                        $"{kind}'s lowest point is at {bounds.min.y:0.000} m. "
                        + "Below zero is under the road; above it is floating.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void NoPropModelBlocksAnybody()
        {
            foreach (ThrowableKind kind in KindsWithArt)
            {
                GameObject instance = Instantiate(kind);
                try
                {
                    Assert.That(
                        instance.GetComponentsInChildren<Collider>(true),
                        Is.Empty,
                        $"{kind}'s model carries a collider. The prop's own "
                        + "trigger decides everything, and a solid prop in the "
                        + "road stops the runner it is meant to catch — and "
                        + "stops thrown rocks besides.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        /// <summary>
        /// The officer's two placed props have no art, and must say so rather
        /// than borrow somebody else's model. A grey shape reads as unfinished;
        /// a wrong model reads as a lie.
        /// </summary>
        [Test]
        public void PropsWithoutArtNameNoModel()
        {
            foreach (ThrowableKind kind in new[]
                { ThrowableKind.GlueTrap, ThrowableKind.SensorLight })
            {
                Assert.That(
                    ThrowableCatalog.GetModelStem(kind),
                    Is.Null.Or.Empty,
                    $"{kind} has no authored model, so it must not name one.");
            }
        }

        private static GameObject Instantiate(ThrowableKind kind)
        {
            string stem = ThrowableCatalog.GetModelStem(kind);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/{stem}.prefab");
            Assert.That(prefab, Is.Not.Null, stem);
            GameObject instance =
                (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            return instance;
        }

        private static Bounds Measure(GameObject instance)
        {
            Renderer[] parts =
                instance.GetComponentsInChildren<Renderer>(true);
            Assert.That(parts, Is.Not.Empty, $"{instance.name} draws nothing.");

            Bounds bounds = parts[0].bounds;
            foreach (Renderer part in parts.Skip(1))
            {
                bounds.Encapsulate(part.bounds);
            }

            return bounds;
        }
    }
}
