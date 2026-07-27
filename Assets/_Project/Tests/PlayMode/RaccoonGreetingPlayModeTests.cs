using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Animation;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The raccoon's greeting is the only cue that marks the merchant as the
    /// place to sell, so "does it actually come out, and does it go back" is
    /// worth pinning down rather than checking by walking up to it.
    /// </summary>
    public sealed class RaccoonGreetingPlayModeTests
    {
        [UnityTest]
        public IEnumerator RaccoonStaysHiddenUntilAPlayerIsNear()
        {
            Fixture fixture = Create();

            // No player in the scene at all.
            yield return null;
            yield return null;

            Assert.That(fixture.Greeter.IsGreeting, Is.False);
            Assert.That(
                fixture.Model.localPosition.y,
                Is.EqualTo(HiddenY).Within(0.01f),
                "With nobody around the merchant must stay inside the bin.");

            Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator RaccoonRisesAndWavesForANearbyPlayer()
        {
            Fixture fixture = Create();
            GameObject player = CreatePlayer(new Vector3(2f, 0f, 0f));

            yield return WaitSeconds(1.5f);

            Assert.That(fixture.Greeter.IsGreeting, Is.True);
            Assert.That(
                fixture.Greeter.RisenFraction,
                Is.EqualTo(1f).Within(0.01f));
            Assert.That(
                fixture.Model.localPosition.y,
                Is.EqualTo(RaisedY).Within(0.01f));
            Assert.That(
                Quaternion.Angle(
                    fixture.UpperArm.localRotation,
                    Quaternion.identity),
                Is.GreaterThan(45f),
                "The arm has to actually lift, or there is no wave to see.");

            // The forearm swings, so two samples a beat apart must differ.
            Quaternion first = fixture.Forearm.localRotation;
            yield return WaitSeconds(0.2f);
            Assert.That(
                Quaternion.Angle(first, fixture.Forearm.localRotation),
                Is.GreaterThan(1f),
                "A held pose is a salute, not a wave.");

            Object.Destroy(player);
            Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator RaccoonSinksBackWhenThePlayerLeaves()
        {
            Fixture fixture = Create();
            GameObject player = CreatePlayer(new Vector3(2f, 0f, 0f));
            yield return WaitSeconds(1.5f);
            Assert.That(fixture.Greeter.IsGreeting, Is.True);

            player.transform.position = new Vector3(40f, 0f, 0f);
            yield return WaitSeconds(1.5f);

            Assert.That(fixture.Greeter.IsGreeting, Is.False);
            Assert.That(
                fixture.Model.localPosition.y,
                Is.EqualTo(HiddenY).Within(0.01f),
                "The merchant has to go back in, or the cue means nothing "
                + "the second time.");

            Object.Destroy(player);
            Destroy(fixture);
        }

        /// <summary>
        /// Being out of sight is the effect, so it is checked as one: the
        /// renderers are off, not merely positioned somewhere hopeful.
        /// </summary>
        [UnityTest]
        public IEnumerator MerchantIsInvisibleUntilTheLidIsOpen()
        {
            Fixture fixture = Create();
            yield return null;
            yield return null;

            Assert.That(
                fixture.Renderer.enabled,
                Is.False,
                "With nobody near, the merchant must not be drawn at all.");

            GameObject player = CreatePlayer(new Vector3(2f, 0f, 0f));

            // The moment it becomes visible, the lid must already be open.
            bool sawReveal = false;
            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                if (fixture.Renderer.enabled)
                {
                    Assert.That(
                        fixture.Greeter.LidOpenFraction,
                        Is.GreaterThanOrEqualTo(0.7f),
                        "The merchant appeared while the lid was still shut, "
                        + "so the player saw it pop into existence.");
                    sawReveal = true;
                }

                yield return null;
            }

            Assert.That(sawReveal, Is.True);
            Assert.That(fixture.Renderer.enabled, Is.True);

            Object.Destroy(player);
            Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator MerchantGoesInvisibleAgainAfterSinking()
        {
            Fixture fixture = Create();
            GameObject player = CreatePlayer(new Vector3(2f, 0f, 0f));
            yield return WaitSeconds(1.5f);
            Assert.That(fixture.Renderer.enabled, Is.True);

            player.transform.position = new Vector3(40f, 0f, 0f);
            yield return WaitSeconds(2f);

            Assert.That(
                fixture.Renderer.enabled,
                Is.False,
                "Once the greeting is over the merchant hides again, or the "
                + "second approach has nothing to reveal.");

            Object.Destroy(player);
            Destroy(fixture);
        }

        /// <summary>
        /// The order is the point: rising through a shut lid looks broken, and
        /// a lid dropping on the raccoon's head looks worse.
        /// </summary>
        [UnityTest]
        public IEnumerator LidOpensBeforeTheRaccoonRises()
        {
            Fixture fixture = Create();
            GameObject player = CreatePlayer(new Vector3(2f, 0f, 0f));

            bool sawLidLeadTheRise = false;
            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                if (fixture.Greeter.RisenFraction > 0f)
                {
                    Assert.That(
                        fixture.Greeter.LidOpenFraction,
                        Is.GreaterThanOrEqualTo(0.7f),
                        "The raccoon started moving through a shut lid.");
                    sawLidLeadTheRise = true;
                }

                yield return null;
            }

            Assert.That(sawLidLeadTheRise, Is.True);
            Assert.That(
                fixture.Greeter.LidOpenFraction,
                Is.EqualTo(1f).Within(0.01f));
            Assert.That(
                Quaternion.Angle(
                    fixture.Lid.localRotation,
                    Quaternion.identity),
                Is.GreaterThan(100f),
                "The lid should be swung well open, not ajar.");

            Object.Destroy(player);
            Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator LidWaitsForTheRaccoonBeforeClosing()
        {
            Fixture fixture = Create();
            GameObject player = CreatePlayer(new Vector3(2f, 0f, 0f));
            yield return WaitSeconds(1.5f);

            player.transform.position = new Vector3(40f, 0f, 0f);

            float elapsed = 0f;
            while (elapsed < 2f)
            {
                elapsed += Time.deltaTime;
                if (fixture.Greeter.LidOpenFraction < 1f)
                {
                    Assert.That(
                        fixture.Greeter.RisenFraction,
                        Is.LessThanOrEqualTo(0.2f),
                        "The lid started shutting while the raccoon was "
                        + "still up.");
                }

                yield return null;
            }

            Assert.That(
                fixture.Greeter.LidOpenFraction,
                Is.EqualTo(0f).Within(0.01f));

            Object.Destroy(player);
            Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator HeightIsMeasuredOnTheGroundNotThroughRoofs()
        {
            Fixture fixture = Create();
            // Directly overhead, well inside the radius in 3D but not on the
            // ground plane the player can actually reach the bin from.
            GameObject player = CreatePlayer(new Vector3(0f, 12f, 0f));

            yield return WaitSeconds(0.5f);

            Assert.That(
                fixture.Greeter.IsGreeting,
                Is.True,
                "Standing on a roof above the bin is still standing at it; "
                + "the check is deliberately planar.");

            player.transform.position = new Vector3(30f, 12f, 0f);
            yield return WaitSeconds(1.5f);
            Assert.That(fixture.Greeter.IsGreeting, Is.False);

            Object.Destroy(player);
            Destroy(fixture);
        }

        private const float HiddenY = -1.6f;
        private const float RaisedY = 0.35f;

        private sealed class Fixture
        {
            public GameObject Root;
            public RaccoonBinGreeter Greeter;
            public Transform Model;
            public Transform UpperArm;
            public Transform Forearm;
            public Transform Lid;
            public Renderer Renderer;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private static Fixture Create()
        {
            var root = new GameObject("Raccoon Pivot");
            root.SetActive(false);

            var model = new GameObject("raccoon Visual");
            model.transform.SetParent(root.transform, false);

            // A real renderer, so "is it hidden" is answered by the same thing
            // the player would be looking at.
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "raccoon Mesh";
            mesh.transform.SetParent(model.transform, false);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            var upperArm = new GameObject("L_Upperarm");
            upperArm.transform.SetParent(model.transform, false);
            var forearm = new GameObject("L_Forearm");
            forearm.transform.SetParent(upperArm.transform, false);

            var lid = new GameObject("Lid Hinge");
            lid.transform.SetParent(root.transform, false);

            RaccoonBinGreeter greeter =
                root.AddComponent<RaccoonBinGreeter>();
            greeter.Configure(
                model.transform,
                lid.transform,
                upperArm.transform,
                forearm.transform,
                HiddenY,
                RaisedY,
                6f);
            root.SetActive(true);

            return new Fixture
            {
                Root = root,
                Greeter = greeter,
                Model = model.transform,
                UpperArm = upperArm.transform,
                Forearm = forearm.transform,
                Lid = lid.transform,
                Renderer = mesh.GetComponent<Renderer>()
            };
        }

        private static GameObject CreatePlayer(Vector3 position)
        {
            var player = new GameObject("Police");
            player.SetActive(false);
            player.transform.position = position;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Police);
            player.SetActive(true);
            return player;
        }

        private static void Destroy(Fixture fixture)
        {
            Object.Destroy(fixture.Root);
        }
    }
}
