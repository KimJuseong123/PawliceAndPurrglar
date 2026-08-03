using UnityEngine;
using UnityEngine.InputSystem;
using PawsAndLoot.Input;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class PlayerInteractionInput : MonoBehaviour
    {
        [SerializeField]
        private PlayerInteractionScanner scanner;

        [SerializeField]
        private PlayerRoleIdentity identity;

        private IHoldInteractable heldTarget;
        private MonoBehaviour heldTargetComponent;
        private float holdElapsedSeconds;

        public bool IsLocallyControlled { get; set; }
        public bool IsHolding => heldTarget != null;
        public float HoldProgress01 =>
            heldTarget != null && heldTarget.HoldDurationSeconds > 0f
                ? Mathf.Clamp01(holdElapsedSeconds / heldTarget.HoldDurationSeconds)
                : 0f;

        public void Configure(
            PlayerInteractionScanner configuredScanner,
            bool locallyControlled)
        {
            scanner = configuredScanner;
            ResolveIdentity();
            IsLocallyControlled = locallyControlled;
        }

        private void Awake()
        {
            ResolveIdentity();
        }

        private void OnDisable()
        {
            CancelCurrentHold();
        }

        private void Update()
        {
            if (!IsLocallyControlled
                || scanner == null
                || Keyboard.current == null
                || GameplayInputRouter.GameplayInputSuppressed)
            {
                CancelCurrentHold();
                return;
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                HandlePressed();
            }

            UpdateHold(Keyboard.current.eKey.isPressed);
        }

        private void HandlePressed()
        {
            if (heldTarget != null)
            {
                return;
            }

            scanner.RefreshTarget(ContextInteractionKey.E);
            IPlayerInteractable target = scanner.CurrentTarget;
            if (target == null)
            {
                return;
            }

            ResolveIdentity();
            if (identity == null)
            {
                return;
            }

            var context = new PlayerInteractionContext(identity);
            if (target is IHoldInteractable holdable)
            {
                if (holdable.HoldDurationSeconds <= 0f)
                {
                    holdable.CompleteHold(context);
                    scanner.RefreshTarget(ContextInteractionKey.E);
                    return;
                }

                if (holdable.CanBeginHold(context))
                {
                    heldTarget = holdable;
                    heldTargetComponent = target as MonoBehaviour;
                    holdElapsedSeconds = 0f;
                }

                return;
            }

            scanner.TryInteractCurrent();
        }

        private void UpdateHold(bool keyHeld)
        {
            if (heldTarget == null)
            {
                return;
            }

            ResolveIdentity();
            if (!keyHeld
                || identity == null
                || heldTargetComponent == null)
            {
                CancelCurrentHold();
                return;
            }

            scanner.RefreshTarget(ContextInteractionKey.E);
            if (!ReferenceEquals(scanner.CurrentTarget, heldTargetComponent))
            {
                CancelCurrentHold();
                return;
            }

            holdElapsedSeconds += Time.unscaledDeltaTime;
            if (HoldProgress01 < 1f)
            {
                return;
            }

            IHoldInteractable completed = heldTarget;
            ClearHold();
            completed.CompleteHold(new PlayerInteractionContext(identity));
            scanner.RefreshTarget(ContextInteractionKey.E);
        }

        private void CancelCurrentHold()
        {
            if (heldTarget == null)
            {
                return;
            }

            ResolveIdentity();
            if (identity != null)
            {
                heldTarget.CancelHold(new PlayerInteractionContext(identity));
            }

            ClearHold();
        }

        private void ClearHold()
        {
            heldTarget = null;
            heldTargetComponent = null;
            holdElapsedSeconds = 0f;
        }

        private void ResolveIdentity()
        {
            if (identity == null)
            {
                identity = scanner != null
                    ? scanner.GetComponent<PlayerRoleIdentity>()
                    : GetComponent<PlayerRoleIdentity>();
            }
        }
    }
}
