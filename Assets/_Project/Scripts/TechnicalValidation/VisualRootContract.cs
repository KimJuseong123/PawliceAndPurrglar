using System;
using UnityEngine;

namespace PawliceAndPurrglar.TechnicalValidation
{
    public sealed class VisualRootContract : MonoBehaviour
    {
        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private Collider gameplayCollider;

        [SerializeField]
        private Animator visualAnimator;

        public Transform VisualRoot => visualRoot;
        public Collider GameplayCollider => gameplayCollider;
        public Animator VisualAnimator => visualAnimator;

        public void Initialize(
            Transform assignedVisualRoot,
            Collider assignedGameplayCollider,
            Animator assignedVisualAnimator)
        {
            visualRoot = assignedVisualRoot;
            gameplayCollider = assignedGameplayCollider;
            visualAnimator = assignedVisualAnimator;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (visualRoot == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(VisualRootContract)} on '{name}' requires a VisualRoot.");
            }

            if (visualRoot.parent != transform)
            {
                throw new InvalidOperationException(
                    $"VisualRoot '{visualRoot.name}' must be a direct child of '{name}'.");
            }

            if (gameplayCollider == null || gameplayCollider.transform != transform)
            {
                throw new InvalidOperationException(
                    $"Gameplay Collider must remain on PlayerRoot '{name}'.");
            }

            if (GetComponent<KeyboardCubeMover>() == null)
            {
                throw new InvalidOperationException(
                    $"Movement must remain on PlayerRoot '{name}'.");
            }

            if (visualAnimator == null || !visualAnimator.transform.IsChildOf(visualRoot))
            {
                throw new InvalidOperationException(
                    $"Animator must be contained by VisualRoot '{visualRoot.name}'.");
            }

            if (visualAnimator.applyRootMotion)
            {
                throw new InvalidOperationException(
                    "TECH-002 requires root motion to be disabled.");
            }
        }
    }
}
