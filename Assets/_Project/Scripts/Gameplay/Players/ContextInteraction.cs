using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    public enum ContextInteractionKey
    {
        E = 0,
        G = 1
    }

    /// <summary>
    /// Optional context behavior layered over the legacy E interaction
    /// contract. Existing interactables do not need to implement it.
    /// </summary>
    public interface IContextInteractable
    {
        bool Supports(ContextInteractionKey key);
        bool TryInteract(PlayerInteractionContext context, ContextInteractionKey key);
    }

    public interface IHoldInteractable
    {
        float HoldDurationSeconds { get; }
        bool CanBeginHold(PlayerInteractionContext context);
        bool CompleteHold(PlayerInteractionContext context);
        void CancelHold(PlayerInteractionContext context);
    }

    public interface IInteractionPriority
    {
        int InteractionPriority { get; }
    }

    public static class InteractionResolver
    {
        public static bool IsValid(
            IPlayerInteractable candidate,
            PlayerRoleIdentity identity,
            ContextInteractionKey key = ContextInteractionKey.E)
        {
            if (candidate == null
                || identity == null
                || !candidate.IsAvailable
                || !identity.CanInteract(candidate.InteractionType))
            {
                return false;
            }

            return candidate is IContextInteractable contextual
                ? contextual.Supports(key)
                : key == ContextInteractionKey.E;
        }

        public static bool TryExecute(
            IPlayerInteractable candidate,
            PlayerInteractionContext context,
            ContextInteractionKey key)
        {
            if (!IsValid(candidate, context.Player, key))
            {
                return false;
            }

            if (candidate is IContextInteractable contextual)
            {
                return contextual.TryInteract(context, key);
            }

            return key == ContextInteractionKey.E
                && candidate.TryInteract(context);
        }
    }
}
