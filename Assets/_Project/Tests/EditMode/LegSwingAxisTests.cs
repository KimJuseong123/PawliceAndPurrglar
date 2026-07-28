using NUnit.Framework;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// The limbs have to swing along the character's facing, not out to the
    /// side.
    ///
    /// This is measured against the real rigs because they disagree with each
    /// other: a rotation about X strides on the cat and the players but splays
    /// the dog's leg sideways. A single hardcoded axis passed every existing
    /// test while the dog visibly paddled instead of walking, so the property
    /// worth pinning is the direction the foot actually travels.
    /// </summary>
    public sealed class LegSwingAxisTests
    {
        [TestCase("dog", CompanionLegAnimator.GaitMode.Quadruped)]
        [TestCase("cat", CompanionLegAnimator.GaitMode.Quadruped)]
        [TestCase("police", CompanionLegAnimator.GaitMode.Biped)]
        [TestCase("thief", CompanionLegAnimator.GaitMode.Biped)]
        public void LimbsSwingForwardsNotSideways(
            string stem,
            CompanionLegAnimator.GaitMode gait)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Project/Art/Characters/{stem}.fbx");
            Assert.That(model, Is.Not.Null, $"{stem}.fbx is missing.");

            var root = new GameObject($"{stem} Root");
            GameObject instance = Object.Instantiate(model, root.transform);
            CompanionLegAnimator animator =
                root.AddComponent<CompanionLegAnimator>();
            animator.Configure(instance.transform, gait);

            Assert.That(
                animator.LegCount,
                Is.EqualTo(4),
                $"{stem} should drive four limbs.");

            Transform tip = FindDeepestLimbTip(animator, instance.transform);
            Assert.That(tip, Is.Not.Null);

            Vector3 rest = tip.position;
            Vector3 forward = root.transform.forward;
            Vector3 side = root.transform.right;

            // Walk the gait through a full cycle and track how far the limb tip
            // travels along each axis.
            float maxForward = 0f;
            float maxSide = 0f;
            for (int step = 0; step < 60; step++)
            {
                root.transform.position += forward * 0.1f;
                animator.Tick(0.02f);
                Vector3 delta = tip.position - rest;
                maxForward = Mathf.Max(
                    maxForward,
                    Mathf.Abs(Vector3.Dot(delta, forward)));
                maxSide = Mathf.Max(
                    maxSide,
                    Mathf.Abs(Vector3.Dot(delta, side)));
            }

            Assert.That(
                maxForward,
                Is.GreaterThan(0.01f),
                $"{stem}'s limb barely moved; the gait is not driving it.");
            Assert.That(
                maxForward,
                Is.GreaterThan(maxSide),
                $"{stem}'s limb travelled {maxSide:0.000} sideways against "
                + $"{maxForward:0.000} forward. A leg that swings out to the "
                + "side reads as paddling, not walking.");

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Deepest bone under the rig, which is a foot or paw on every one of
        /// these skeletons and therefore the point that shows the stride.
        /// </summary>
        private static Transform FindDeepestLimbTip(
            CompanionLegAnimator animator,
            Transform skeleton)
        {
            Transform best = null;
            int bestDepth = 0;
            foreach (Transform candidate in
                skeleton.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name.IndexOf(
                        "Twist",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                int depth = 0;
                for (Transform p = candidate;
                    p != skeleton && p != null;
                    p = p.parent)
                {
                    depth++;
                }

                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
