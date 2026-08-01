using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class AnimationClipSourceAuditTests
    {
        [Test]
        public void ExpectedControllerMotionGuidMapIsStable()
        {
            Dictionary<string, string> expected =
                new Dictionary<string, string>
                {
                    ["Idle"] = "4154cd260b34a4c408b7abaf7ddadc97",
                    ["Walk"] = "27cca24148160794ea268c6d64671e5b",
                    ["Run"] = "70c18e0752b220f439894f5c0b0fe381",
                    ["Command"] = "dca26005bd5b52e499d280f68bfe24ea",
                    ["Win"] = "1f6a9fbf200d8684e83ba2a8237e83b9",
                    ["Lose"] = "9cd03327ea1e2e146adce20d40d05a49"
                };

            string controllerPath =
                "Assets/_Project/Art/Characters/CharacterLocomotion.controller";
            string controllerText = File.ReadAllText(controllerPath);
            foreach (KeyValuePair<string, string> binding in expected)
            {
                Assert.That(
                    controllerText,
                    Does.Contain(
                        $"guid: {binding.Value}"),
                    $"Controller Motion GUID for {binding.Key} changed.");
            }
        }

        [Test]
        public void CurrentControllerReportsUnresolvedMotionSources()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/_Project/Art/Characters/CharacterLocomotion.controller");
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.animationClips, Is.Empty);
        }

        [Test]
        public void AuthoredModelsKeepTheirIntendedRigKindsAndHaveNoEmbeddedClips()
        {
            string[] humanPaths =
            {
                "Assets/_Project/Art/Characters/police.fbx",
                "Assets/_Project/Art/Characters/thief.fbx"
            };
            string[] animalPaths =
            {
                "Assets/_Project/Art/Characters/dog.fbx",
                "Assets/_Project/Art/Characters/cat.fbx",
                "Assets/_Project/Art/Characters/raccoon.fbx"
            };

            foreach (string path in humanPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path)
                    as ModelImporter;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Animator animator = model.GetComponentInChildren<Animator>(true);
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
                Assert.That(importer.importAnimation, Is.True);
                Assert.That(importer.clipAnimations, Is.Empty);
                Assert.That(animator.avatar, Is.Not.Null);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
                Assert.That(LoadEmbeddedClips(path), Is.Empty);
            }

            foreach (string path in animalPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path)
                    as ModelImporter;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Animator animator = model.GetComponentInChildren<Animator>(true);
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
                Assert.That(importer.importAnimation, Is.True);
                Assert.That(importer.clipAnimations, Is.Empty);
                Assert.That(animator.avatar, Is.Not.Null);
                Assert.That(animator.avatar.isHuman, Is.False);
                Assert.That(LoadEmbeddedClips(path), Is.Empty);
            }
        }

        [Test]
        public void CurrentControllerDoesNotPassRuntimeLocomotionGate()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    "Assets/_Project/Art/Characters/CharacterLocomotion.controller");
            var root = new GameObject("Animation Source Audit Test");
            try
            {
                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

                bool usable = AnimatorClipGuard.HasUsableLocomotion(
                    animator,
                    out string reason);

                Assert.That(usable, Is.False);
                Assert.That(reason, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static List<AnimationClip> LoadEmbeddedClips(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToList();
        }
    }
}
