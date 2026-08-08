using System.IO;
using NUnit.Framework;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// The other half of the guard: it must leave a working Animator alone.
    ///
    /// This case needs a real AnimatorController with a clip in it, which is an
    /// editor-only type, so it lives here rather than in the Play Mode suite. It
    /// builds its own controller instead of loading the project's, so the result
    /// is the same whether the controller is absent, empty, or fully populated.
    /// </summary>
    public sealed class AnimatorClipGuardTests
    {
        private const string TempFolder = "Assets/_TempGuardTest";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "_TempGuardTest");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void AWorkingControllerIsCounted()
        {
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    Path.Combine(TempFolder, "Guard.controller")
                        .Replace('\\', '/'));
            var clip = new AnimationClip { name = "Idle" };
            AssetDatabase.CreateAsset(
                clip,
                Path.Combine(TempFolder, "Idle.anim")
                    .Replace('\\', '/'));
            controller.AddMotion(clip);

            var root = new GameObject("Character");
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            Assert.That(
                AnimatorClipGuard.CountUsableClips(animator),
                Is.EqualTo(1),
                "A controller with one real clip has one usable clip.");

            Object.DestroyImmediate(root);
        }

        [Test]
        public void AnEmptyControllerCountsAsUnusable()
        {
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    Path.Combine(TempFolder, "Empty.controller")
                        .Replace('\\', '/'));

            var root = new GameObject("Character");
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            Assert.That(
                AnimatorClipGuard.CountUsableClips(animator),
                Is.EqualTo(0),
                "An assigned but empty controller is still nothing to play, "
                + "which is exactly the no-authored-clips case this ships in.");

            Object.DestroyImmediate(root);
        }
    }
}
