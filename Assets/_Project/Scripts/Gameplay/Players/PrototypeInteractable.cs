using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    public sealed class PrototypeInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private PlayerInteractionType interactionType;

        [SerializeField]
        private string prompt = "Interact";

        [SerializeField]
        private bool isAvailable = true;

        public Transform InteractionTransform => transform;
        public PlayerInteractionType InteractionType => interactionType;
        public string Prompt => prompt;
        public bool IsAvailable => isAvailable && isActiveAndEnabled;
        public int InteractionCount { get; private set; }

        public void Configure(
            PlayerInteractionType configuredType,
            string configuredPrompt)
        {
            interactionType = configuredType;
            prompt = configuredPrompt;
        }

        public void SetAvailable(bool available)
        {
            isAvailable = available;
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            if (!IsAvailable
                || context.Player == null
                || !context.Player.CanInteract(interactionType))
            {
                return false;
            }

            InteractionCount++;
            return true;
        }
    }
}
