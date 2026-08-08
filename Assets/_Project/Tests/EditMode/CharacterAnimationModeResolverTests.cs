using NUnit.Framework;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class CharacterAnimationModeResolverTests
    {
        private const string TempFolder =
            "Assets/_Project/Tests/TempAnimationProfiles";

        [SetUp]
        public void SetUp()
        {
            EnsureFolder("Assets/_Project/Tests", "TempAnimationProfiles");
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
        public void EmptyControllerSelectsProceduralFallback()
        {
            GameObject root = new("AnimationFallbackRoot");
            try
            {
                Animator animator = root.AddComponent<Animator>();
                CharacterAnimationModeResolver resolver =
                    root.AddComponent<CharacterAnimationModeResolver>();
                resolver.Configure(animator, root.transform);

                Assert.That(
                    resolver.Mode,
                    Is.EqualTo(CharacterAnimationMode.ProceduralFallback));
                Assert.That(animator.enabled, Is.False);
                Assert.That(resolver.Validation.IsValid, Is.False);
                Assert.That(resolver.Validation.PrimaryFailure, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ValidGenericProfileSelectsAuthoredAnimator()
        {
            AnimationClip idle = CreateClip("Idle");
            AnimationClip walk = CreateClip("Walk");
            AnimationClip run = CreateClip("Run");
            AnimatorController controller = CreateController(idle, walk, run);
            CharacterAnimationProfile profile = CreateProfile(
                CharacterAnimationRigType.Generic,
                controller,
                idle,
                walk,
                run);
            GameObject root = new("AnimationAuthoredRoot");
            try
            {
                Animator animator = root.AddComponent<Animator>();
                CharacterAnimationModeResolver resolver =
                    root.AddComponent<CharacterAnimationModeResolver>();
                resolver.Configure(animator, root.transform, profile);

                Assert.That(
                    resolver.Mode,
                    Is.EqualTo(CharacterAnimationMode.AuthoredAnimator));
                Assert.That(animator.enabled, Is.True);
                Assert.That(resolver.ProceduralFallbackEnabled, Is.False);
                Assert.That(resolver.Validation.RuntimeClipCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void HumanoidProfileRejectsGenericClips()
        {
            AnimationClip idle = CreateClip("Idle");
            AnimationClip walk = CreateClip("Walk");
            AnimationClip run = CreateClip("Run");
            AnimatorController controller = CreateController(idle, walk, run);
            CharacterAnimationProfile profile = CreateProfile(
                CharacterAnimationRigType.Humanoid,
                controller,
                idle,
                walk,
                run);
            GameObject root = new("AnimationRigMismatchRoot");
            try
            {
                Animator animator = root.AddComponent<Animator>();
                CharacterAnimationModeResolver resolver =
                    root.AddComponent<CharacterAnimationModeResolver>();
                resolver.Configure(animator, root.transform, profile);

                Assert.That(resolver.ProceduralFallbackEnabled, Is.True);
                Assert.That(
                    resolver.Validation.PrimaryFailure,
                    Does.Contain("Humanoid"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        // LegacyControllerGuidTextRemainsUnchanged lived here. It pinned the
        // six clip GUIDs inside CharacterLocomotion.controller so nobody
        // "tidied" them away — but every one of them was dangling: they pointed
        // at TopDownEngine FBX files that were never in the repository and were
        // not installed on this machine either. The test was guarding six holes.
        // Removed with the controller on 2026-08-08.

        private static AnimationClip CreateClip(string name)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 0.01f));
            AssetDatabase.CreateAsset(
                clip,
                $"{TempFolder}/{name}.anim");
            return clip;
        }

        private static AnimatorController CreateController(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run)
        {
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    $"{TempFolder}/Locomotion.controller");
            controller.AddParameter(
                CharacterAnimatorParameters.Speed,
                AnimatorControllerParameterType.Float);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = machine.AddState("Walk");
            walkState.motion = walk;
            AnimatorState runState = machine.AddState("Run");
            runState.motion = run;
            machine.defaultState = idleState;
            return controller;
        }

        private static CharacterAnimationProfile CreateProfile(
            CharacterAnimationRigType rig,
            AnimatorController controller,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run)
        {
            var profile = ScriptableObject.CreateInstance<CharacterAnimationProfile>();
            SerializedObject serialized = new(profile);
            serialized.FindProperty("rigType").enumValueIndex = (int)rig;
            serialized.FindProperty("animatorController").objectReferenceValue = controller;
            serialized.FindProperty("idleClip").objectReferenceValue = idle;
            serialized.FindProperty("walkClip").objectReferenceValue = walk;
            serialized.FindProperty("runClip").objectReferenceValue = run;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
