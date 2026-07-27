using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Animation;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// A checkout without TopDownEngine has no locomotion clips, and a humanoid
    /// Animator with nothing to play buried the characters in the ground. The
    /// guard is what turns that into "standing still" instead of "broken", so it
    /// is worth holding in place.
    /// </summary>
    public sealed class AnimatorClipGuardPlayModeTests
    {
        [UnityTest]
        public IEnumerator AnimatorIsDisabledWhenThereAreNoClips()
        {
            var root = new GameObject("Character");
            root.SetActive(false);
            Animator animator = root.AddComponent<Animator>();
            // No controller is the same situation as a controller whose clips
            // all failed to resolve: nothing to play.
            animator.runtimeAnimatorController = null;
            AnimatorClipGuard guard =
                root.AddComponent<AnimatorClipGuard>();
            guard.Configure(animator);
            root.SetActive(true);

            yield return null;

            Assert.That(
                guard.DisabledForMissingClips,
                Is.True,
                "The guard has to notice, or nothing switches the Animator "
                + "off.");
            Assert.That(
                animator.enabled,
                Is.False,
                "A running Animator with no clips retargets the rig to a pose "
                + "the baked height offsets were not measured from.");

            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator GuardFindsItsOwnAnimatorWhenNotConfigured()
        {
            // The scene builder wires the reference, but a hand-placed guard
            // should still work rather than silently do nothing.
            var root = new GameObject("Character");
            root.SetActive(false);
            Animator animator = root.AddComponent<Animator>();
            AnimatorClipGuard guard =
                root.AddComponent<AnimatorClipGuard>();
            root.SetActive(true);

            yield return null;

            Assert.That(guard.DisabledForMissingClips, Is.True);
            Assert.That(animator.enabled, Is.False);

            Object.Destroy(root);
        }

        [Test]
        public void CountingIsNullSafe()
        {
            Assert.That(
                AnimatorClipGuard.CountUsableClips(null),
                Is.EqualTo(0),
                "A null Animator must reach zero rather than throw.");
        }
    }
}
