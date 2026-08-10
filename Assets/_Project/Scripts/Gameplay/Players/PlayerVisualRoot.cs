using System;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    public sealed class PlayerVisualRoot : MonoBehaviour
    {
        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private GameObject currentVisual;

        public Transform VisualRoot => visualRoot;
        public GameObject CurrentVisual => currentVisual;

        public void Configure(
            Transform configuredVisualRoot,
            GameObject configuredVisual)
        {
            visualRoot = configuredVisualRoot;
            currentVisual = configuredVisual;
        }

        public GameObject ReplaceVisual(GameObject replacementPrefab)
        {
            if (replacementPrefab == null)
            {
                throw new ArgumentNullException(nameof(replacementPrefab));
            }

            ValidateOrThrow();
            if (currentVisual != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(currentVisual);
                }
                else
                {
                    DestroyImmediate(currentVisual);
                }
            }

            currentVisual = Instantiate(
                replacementPrefab,
                visualRoot,
                false);
            currentVisual.name = replacementPrefab.name;
            DisableRootMotion(currentVisual);
            return currentVisual;
        }

        public void ValidateOrThrow()
        {
            if (visualRoot == null || visualRoot.parent != transform)
            {
                throw new InvalidOperationException(
                    $"'{name}' requires a direct VisualRoot child.");
            }

            if (GetComponent<CharacterController>() == null
                || GetComponent<PlayerMovementMotor>() == null
                || GetComponent<PlayerInteractionScanner>() == null
                || GetComponent<NetworkObject>() == null)
            {
                throw new InvalidOperationException(
                    $"'{name}' must keep collision, movement, interaction, and networking on PlayerRoot.");
            }

            if (currentVisual != null
                && !currentVisual.transform.IsChildOf(visualRoot))
            {
                throw new InvalidOperationException(
                    $"Visual '{currentVisual.name}' must be under VisualRoot.");
            }

            foreach (Animator animator in
                     visualRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator.applyRootMotion)
                {
                    throw new InvalidOperationException(
                        $"Animator '{animator.name}' must not use root motion.");
                }
            }
        }

        private static void DisableRootMotion(GameObject visual)
        {
            foreach (Animator animator in
                     visual.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
            }
        }
    }
}
