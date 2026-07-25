using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    public interface IPlayerInteractable
    {
        Transform InteractionTransform { get; }
        PlayerInteractionType InteractionType { get; }
        string Prompt { get; }
        bool IsAvailable { get; }
        bool TryInteract(PlayerInteractionContext context);
    }
}
