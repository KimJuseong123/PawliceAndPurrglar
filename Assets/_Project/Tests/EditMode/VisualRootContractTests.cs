using NUnit.Framework;
using PawliceAndPurrglar.TechnicalValidation;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    public sealed class VisualRootContractTests
    {
        [Test]
        public void ReplacingVisualModel_DoesNotReplaceGameplayComponents()
        {
            var player = new GameObject("PlayerRoot");
            var collider = player.AddComponent<CapsuleCollider>();
            var mover = player.AddComponent<KeyboardCubeMover>();
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(player.transform, false);

            var firstModel = new GameObject("FirstModel");
            firstModel.transform.SetParent(visualRoot.transform, false);
            var animator = firstModel.AddComponent<Animator>();
            animator.applyRootMotion = false;

            var contract = player.AddComponent<VisualRootContract>();
            contract.Initialize(visualRoot.transform, collider, animator);

            Object.DestroyImmediate(firstModel);
            var replacement = new GameObject("ReplacementModel");
            replacement.transform.SetParent(visualRoot.transform, false);
            var replacementAnimator = replacement.AddComponent<Animator>();
            replacementAnimator.applyRootMotion = false;
            contract.Initialize(visualRoot.transform, collider, replacementAnimator);

            Assert.That(player.GetComponent<CapsuleCollider>(), Is.SameAs(collider));
            Assert.That(player.GetComponent<KeyboardCubeMover>(), Is.SameAs(mover));
            Assert.That(contract.VisualAnimator, Is.SameAs(replacementAnimator));
            Assert.DoesNotThrow(contract.ValidateOrThrow);

            Object.DestroyImmediate(player);
        }
    }
}
