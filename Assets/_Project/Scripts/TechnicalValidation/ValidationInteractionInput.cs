using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawliceAndPurrglar.TechnicalValidation
{
    /// <summary>
    /// Validation-only E interaction driver. Production currently exposes the
    /// IHoldInteractable contract but does not own hold timing, so this driver
    /// proves the complete press/hold/release behavior in isolation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ValidationInteractionInput : MonoBehaviour
    {
        [SerializeField]
        private PlayerInteractionScanner scanner;

        [SerializeField]
        private PlayerRoleIdentity identity;

        private ValidationInteractionTarget heldTarget;
        private float holdElapsed;

        public ValidationInteractionTarget CurrentTarget =>
            scanner != null
                ? scanner.CurrentTarget as ValidationInteractionTarget
                : null;
        public ValidationInteractionTarget HeldTarget => heldTarget;
        public bool IsHolding => heldTarget != null;
        public float HoldProgress01 => heldTarget == null
            ? 0f
            : Mathf.Clamp01(
                holdElapsed / Mathf.Max(0.1f, heldTarget.HoldDurationSeconds));
        public string LastResult { get; private set; } = string.Empty;

        private void Awake()
        {
            scanner ??= GetComponent<PlayerInteractionScanner>();
            identity ??= GetComponent<PlayerRoleIdentity>();
        }

        private void OnEnable()
        {
            GameplayInputRouter.ContextInteractionPressed += HandlePressed;
            GameplayInputRouter.EscapePressed += CancelHold;
        }

        private void OnDisable()
        {
            GameplayInputRouter.ContextInteractionPressed -= HandlePressed;
            GameplayInputRouter.EscapePressed -= CancelHold;
            CancelHold();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || scanner == null)
            {
                return;
            }

            scanner.RefreshTarget(ContextInteractionKey.E);

            if (heldTarget == null)
            {
                return;
            }

            ValidationInteractionTarget current = CurrentTarget;
            if (!keyboard.eKey.isPressed || current != heldTarget)
            {
                CancelHold();
                return;
            }

            holdElapsed += Time.unscaledDeltaTime;
            if (HoldProgress01 < 1f)
            {
                return;
            }

            bool completed = heldTarget.CompleteHold(
                new PlayerInteractionContext(identity));
            LastResult = completed
                ? $"{heldTarget.TargetLabel}: {heldTarget.ActionLabel} complete"
                : $"{heldTarget.TargetLabel}: hold failed";
            heldTarget = null;
            holdElapsed = 0f;
            scanner.RefreshTarget(ContextInteractionKey.E);
        }

        private void HandlePressed()
        {
            if (heldTarget != null || scanner == null || identity == null)
            {
                return;
            }

            scanner.RefreshTarget(ContextInteractionKey.E);
            ValidationInteractionTarget target = CurrentTarget;
            if (target == null)
            {
                LastResult = "No interaction target";
                return;
            }

            PlayerInteractionContext context =
                new(identity);
            if (target.RequiresHold)
            {
                if (target.CanBeginHold(context))
                {
                    heldTarget = target;
                    holdElapsed = 0f;
                    LastResult = $"Holding E: {target.ActionLabel}";
                }

                return;
            }

            bool succeeded = InteractionResolver.TryExecute(
                target,
                context,
                ContextInteractionKey.E);
            LastResult = succeeded
                ? $"{target.TargetLabel}: {target.ActionLabel} complete"
                : $"{target.TargetLabel}: interaction failed";
            scanner.RefreshTarget(ContextInteractionKey.E);
        }

        private void CancelHold()
        {
            if (heldTarget != null && identity != null)
            {
                heldTarget.CancelHold(new PlayerInteractionContext(identity));
                LastResult = $"{heldTarget.TargetLabel}: hold cancelled";
            }

            heldTarget = null;
            holdElapsed = 0f;
        }
    }
}
