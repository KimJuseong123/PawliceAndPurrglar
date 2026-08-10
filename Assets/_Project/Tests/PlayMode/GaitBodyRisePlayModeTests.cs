using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Animation;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// The body has to rise and fall in step with the feet.
    ///
    /// Vertical motion on its own is not what reads as walking — the animals
    /// bobbed for weeks and still looked like they were bouncing. What makes it
    /// a walk is that the body is lowest as a paw lands and rises over the
    /// planted limb in between, so that relationship is what gets pinned here
    /// rather than the mere presence of movement.
    /// </summary>
    public sealed class GaitBodyRisePlayModeTests
    {
        [UnityTest]
        public IEnumerator BodyIsLowestAtFootfallAndHighestBetween()
        {
            Fixture fixture = Create();

            // Walk far enough for the blend to reach full and the phase to
            // settle into the cycle.
            for (int step = 0; step < 40; step++)
            {
                fixture.Root.transform.position += Vector3.forward * 0.1f;
                fixture.Legs.Tick(0.02f);
                fixture.Body.Tick(0.02f);
                yield return null;
            }

            // Sample a whole stride, recording the body height against the
            // distance to the nearest footfall.
            float atFootfall = float.MaxValue;
            float betweenFootfalls = float.MinValue;
            for (int step = 0; step < 120; step++)
            {
                fixture.Root.transform.position += Vector3.forward * 0.1f;
                fixture.Legs.Tick(0.02f);
                fixture.Body.Tick(0.02f);

                float footfalls = fixture.Legs.FootfallsPerCycle;
                // 0 at a footfall, 0.5 midway between two of them.
                float sinceFootfall = Mathf.Repeat(
                    fixture.Legs.GaitCycle * footfalls,
                    1f);
                float height = fixture.Visual.localPosition.y;

                if (sinceFootfall < 0.05f || sinceFootfall > 0.95f)
                {
                    atFootfall = Mathf.Min(atFootfall, height);
                }

                if (sinceFootfall > 0.45f && sinceFootfall < 0.55f)
                {
                    betweenFootfalls =
                        Mathf.Max(betweenFootfalls, height);
                }

                yield return null;
            }

            Assert.That(
                atFootfall,
                Is.Not.EqualTo(float.MaxValue),
                "The stride never reached a footfall; the gait is not running.");
            Assert.That(
                betweenFootfalls,
                Is.Not.EqualTo(float.MinValue),
                "The stride never reached mid-step.");
            Assert.That(
                betweenFootfalls,
                Is.GreaterThan(atFootfall + 0.005f),
                $"The body sat at {betweenFootfalls:0.0000} mid-step against "
                + $"{atFootfall:0.0000} at footfall. Without that difference "
                + "the rise is not following the feet.");

            Object.Destroy(fixture.Root);
        }

        [UnityTest]
        public IEnumerator BodyHoldsStillWhenTheAnimalIsNotMoving()
        {
            Fixture fixture = Create();

            for (int step = 0; step < 40; step++)
            {
                fixture.Legs.Tick(0.02f);
                fixture.Body.Tick(0.02f);
                yield return null;
            }

            Assert.That(
                fixture.Visual.localPosition.y,
                Is.EqualTo(0f).Within(0.0001f),
                "A standing animal must not bob.");

            Object.Destroy(fixture.Root);
        }

        /// <summary>
        /// A body moved by having its position written must bob as steadily as
        /// one that walks there.
        ///
        /// This is how a client moves anything: the host's answer arrives every
        /// few frames, is applied in one step, and then nothing happens until
        /// the next packet. A body that works out its own speed from that reads
        /// "running, stopped, stopped, running" and its hop changes height every
        /// frame — not a bob, a vibration, and from the thief's own screen the
        /// cat is close enough to fill it.
        ///
        /// Fed in the same lumps here, deliberately. Ticking with smooth motion
        /// passes whether or not the told speed is used, which is why this went
        /// unnoticed while the legs were fixed and the body was not.
        /// </summary>
        [UnityTest]
        public IEnumerator BodyBobsSteadilyWhenItsPositionIsWrittenInLumps()
        {
            Fixture fixture = Create();
            const float Told = 5f;

            float low = float.MaxValue;
            float high = float.MinValue;
            for (int step = 0; step < 150; step++)
            {
                // One packet every third frame, and stillness in between.
                if (step % 3 == 0)
                {
                    fixture.Root.transform.position +=
                        Vector3.forward * Told * 0.06f;
                }

                fixture.Legs.SetExternalSpeed(Told);
                fixture.Body.SetExternalSpeed(Told);
                fixture.Legs.Tick(0.02f);
                fixture.Body.Tick(0.02f);

                // Only once the blend has had time to reach full.
                if (step >= 60)
                {
                    low = Mathf.Min(low, fixture.Body.MovingBlend);
                    high = Mathf.Max(high, fixture.Body.MovingBlend);
                }

                yield return null;
            }

            Assert.That(
                low,
                Is.GreaterThan(0.99f),
                $"The walk blend fell to {low:0.000} between packets, so the "
                + "hop height changes with every frame that carries no packet. "
                + "That is the vibration.");
            Assert.That(
                high - low,
                Is.LessThan(0.01f),
                $"The walk blend swung {high - low:0.000} while the animal "
                + "walked at a constant speed.");

            Object.Destroy(fixture.Root);
        }

        private sealed class Fixture
        {
            public GameObject Root;
            public Transform Visual;
            public CompanionLegAnimator Legs;
            public CompanionProceduralAnimator Body;
        }

        /// <summary>
        /// A four-limbed stand-in rig. Built by hand rather than loaded from an
        /// FBX so the test says the same thing in a checkout without the art.
        /// </summary>
        private static Fixture Create()
        {
            var root = new GameObject("Animal");
            root.SetActive(false);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (string pair in new[] { "0", "1" })
                {
                    var upper = new GameObject($"{pair}_{side}_Limb_1");
                    upper.transform.SetParent(visual.transform, false);
                    upper.transform.localPosition =
                        new Vector3(0f, 0.4f, 0f);
                    var lower = new GameObject($"{pair}_{side}_Limb_2");
                    lower.transform.SetParent(upper.transform, false);
                    lower.transform.localPosition =
                        new Vector3(0f, -0.2f, 0f);
                    var paw = new GameObject($"{pair}_{side}_Limb_3");
                    paw.transform.SetParent(lower.transform, false);
                    paw.transform.localPosition =
                        new Vector3(0f, -0.2f, 0f);
                }
            }

            CompanionLegAnimator legs =
                root.AddComponent<CompanionLegAnimator>();
            legs.Configure(visual.transform);

            CompanionProceduralAnimator body =
                root.AddComponent<CompanionProceduralAnimator>();
            body.Configure(null, visual.transform, legs);

            root.SetActive(true);

            return new Fixture
            {
                Root = root,
                Visual = visual.transform,
                Legs = legs,
                Body = body
            };
        }
    }
}
