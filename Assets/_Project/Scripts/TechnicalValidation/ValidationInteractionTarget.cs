using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    public enum ValidationInteractionKind
    {
        Item,
        Drawer,
        LockedDoor,
        Animal,
        Ladder
    }

    /// <summary>
    /// Primitive-only interaction target used by the validation scene. It
    /// exercises the production interaction contracts without changing any
    /// authored door, item, animal, or ladder prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ValidationInteractionTarget : MonoBehaviour,
        IPlayerInteractable,
        IContextInteractable,
        IHoldInteractable
    {
        [SerializeField]
        private ValidationInteractionKind kind;

        [SerializeField, Min(0.1f)]
        private float holdDurationSeconds = 1.25f;

        public ValidationInteractionKind Kind => kind;
        public bool RequiresHold => kind == ValidationInteractionKind.Drawer
            || kind == ValidationInteractionKind.LockedDoor;
        public float HoldDurationSeconds => RequiresHold
            ? holdDurationSeconds
            : 0f;
        public int InteractionCount { get; private set; }
        public string LastOutcome { get; private set; } = string.Empty;

        public Transform InteractionTransform => transform;
        public PlayerInteractionType InteractionType =>
            kind == ValidationInteractionKind.Ladder
                ? PlayerInteractionType.Traversal
                : PlayerInteractionType.Generic;
        public bool IsAvailable => isActiveAndEnabled;
        public string Prompt => ActionLabel;

        public string ActionLabel => kind switch
        {
            ValidationInteractionKind.Item => "Pick up",
            ValidationInteractionKind.Drawer => "Search",
            ValidationInteractionKind.LockedDoor => "Lockpick",
            ValidationInteractionKind.Animal => "Interact with animal",
            ValidationInteractionKind.Ladder => "Climb ladder",
            _ => "Interact"
        };

        public string TargetLabel => kind switch
        {
            ValidationInteractionKind.Item => "Item",
            ValidationInteractionKind.Drawer => "Drawer",
            ValidationInteractionKind.LockedDoor => "Locked Door",
            ValidationInteractionKind.Animal => "Animal",
            ValidationInteractionKind.Ladder => "Ladder",
            _ => "Target"
        };

        public void Configure(
            ValidationInteractionKind configuredKind,
            float configuredHoldDurationSeconds = 1.25f)
        {
            kind = configuredKind;
            holdDurationSeconds = Mathf.Max(
                0.1f,
                configuredHoldDurationSeconds);
        }

        public bool Supports(ContextInteractionKey key) =>
            key == ContextInteractionKey.E;

        public bool TryInteract(
            PlayerInteractionContext context,
            ContextInteractionKey key)
        {
            if (RequiresHold || key != ContextInteractionKey.E)
            {
                return false;
            }

            return RecordInteraction();
        }

        public bool TryInteract(PlayerInteractionContext context)
        {
            return !RequiresHold && RecordInteraction();
        }

        public bool CanBeginHold(PlayerInteractionContext context)
        {
            return RequiresHold && context.Player != null;
        }

        public bool CompleteHold(PlayerInteractionContext context)
        {
            return RequiresHold && RecordInteraction();
        }

        public void CancelHold(PlayerInteractionContext context)
        {
            LastOutcome = "Hold cancelled";
        }

        private bool RecordInteraction()
        {
            InteractionCount++;
            LastOutcome = $"{ActionLabel} complete";
            return true;
        }
    }
}
