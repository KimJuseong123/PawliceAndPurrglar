using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// Which arm waves, and which way it lifts, cannot be reasoned about from
    /// the bone names: it depends on the rig's local axes, and getting it wrong
    /// produced a raccoon swinging a hand down by its knee.
    ///
    /// This loads the real model and measures, so the answer comes from the
    /// asset rather than from an assumption about how it was authored.
    /// </summary>
    public sealed class RaccoonWaveArmTests
    {
        private const string ModelPath =
            "Assets/_Project/Art/Characters/raccoon.fbx";

        /// <summary>
        /// Kept in step with the value the scene builder passes to
        /// <c>RaccoonBinGreeter</c>.
        /// </summary>
        private const float WaveLiftDegrees = -105f;

        [Test]
        public void TheWavingArmLiftsTheHandAboveTheShoulder()
        {
            GameObject instance = Instantiate(
                out Transform upperArm,
                out Transform forearm,
                out Transform hand,
                "L_");

            // Measured on the authored rig: the left arm rests roughly
            // horizontal, a hair above the shoulder, and the lift takes it to
            // about +0.22 in model units. The right arm hangs at -0.19.
            float shoulderY = upperArm.position.y;
            Assert.That(
                hand.position.y - shoulderY,
                Is.LessThan(0.05f),
                "At rest the left hand sits about level with the shoulder. A "
                + "big offset means the rest pose changed and the lift angle "
                + "needs re-measuring.");

            upperArm.localRotation *=
                Quaternion.Euler(0f, 0f, WaveLiftDegrees);

            Assert.That(
                hand.position.y - shoulderY,
                Is.GreaterThan(0.15f),
                "The left arm must carry the hand clearly above the shoulder. "
                + "A hand that ends up level or lower is a wave nobody reads "
                + "as a wave.");

            Object.DestroyImmediate(instance);
        }

        [Test]
        public void TheOppositeArmWouldSwingTheHandDown()
        {
            // Pins down why the left arm is the one used: the identical
            // rotation on the right arm pushes the hand further down, which is
            // the bug this replaced.
            GameObject instance = Instantiate(
                out Transform upperArm,
                out Transform forearm,
                out Transform hand,
                "R_");

            float shoulderY = upperArm.position.y;
            upperArm.localRotation *=
                Quaternion.Euler(0f, 0f, WaveLiftDegrees);

            Assert.That(
                hand.position.y,
                Is.LessThan(shoulderY),
                "The same rotation leaves the right hand below the shoulder, "
                + "which is why the left arm is the one that waves. If this "
                + "ever passes the other way the rig changed.");

            Object.DestroyImmediate(instance);
        }

        private static GameObject Instantiate(
            out Transform upperArm,
            out Transform forearm,
            out Transform hand,
            string sidePrefix)
        {
            var model =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.That(
                model,
                Is.Not.Null,
                $"The raccoon model is missing at {ModelPath}.");

            GameObject instance = Object.Instantiate(model);
            upperArm = Find(instance, sidePrefix + "Upperarm");
            forearm = Find(instance, sidePrefix + "Forearm");
            hand = Find(instance, sidePrefix + "Hand");
            return instance;
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (Transform child in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail($"Bone '{name}' is missing from the raccoon rig.");
            return null;
        }
    }
}
