using System;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.TechnicalValidation
{
    public enum ThrowState
    {
        Idle,
        Aiming,
        Charging,
        Ready,
        Throwing,
        Cooldown,
        Cancelled
    }

    /// <summary>
    /// Local-only throw POC. It consumes no production gameplay effect and
    /// never invokes ToolUseAction; it proves that the selected item, current
    /// aim, shared solver, preview, and a visible flight agree.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowInteractionValidationController : MonoBehaviour
    {
        private const int DefaultObstacleLayers = 1 << 0;

        [SerializeField]
        private ToolCarrier carrier;

        [SerializeField]
        private ThrowChargeController chargeController;

        [SerializeField]
        private ThrowTrajectoryPreview trajectoryPreview;

        [SerializeField]
        private LayerMask obstacleLayers = DefaultObstacleLayers;

        [SerializeField, Min(0.05f)]
        private float fullChargeSeconds = 1f;

        [SerializeField, Min(0.05f)]
        private float cooldownSeconds = 0.35f;

        private ThrowTrajectorySolver.Solution lastSolution;
        private GameObject projectile;
        private float projectileElapsed;
        private float cooldownRemaining;
        private float cancelledRemaining;
        private float lastCharge01;
        private ThrowableKind lastThrownKind;

        public ThrowState State { get; private set; } = ThrowState.Idle;
        public float Charge01 => State == ThrowState.Charging
            || State == ThrowState.Ready
            ? chargeController != null
                ? chargeController.Charge01
                : lastCharge01
            : lastCharge01;
        public bool HasSolution { get; private set; }
        public ThrowTrajectorySolver.Solution LastSolution => lastSolution;
        public float LastRange { get; private set; }
        public event Action<ThrowState> StateChanged;

        private void Awake()
        {
            carrier ??= GetComponent<ToolCarrier>();
            chargeController ??= GetComponent<ThrowChargeController>();
            trajectoryPreview ??= GetComponent<ThrowTrajectoryPreview>();

            if (chargeController == null)
            {
                chargeController = gameObject.AddComponent<ThrowChargeController>();
            }

            if (trajectoryPreview == null)
            {
                trajectoryPreview = gameObject.AddComponent<ThrowTrajectoryPreview>();
            }

            if (carrier == null)
            {
                Debug.LogError(
                    "[ThrowInteractionValidation] ToolCarrier is missing.",
                    this);
                return;
            }

            carrier.ApplyReplicated(true, ThrowableKind.Rock);
        }

        private void OnEnable()
        {
            GameplayInputRouter.QuickSlotPressed += SelectSlot;
            GameplayInputRouter.EscapePressed += Cancel;
        }

        private void OnDisable()
        {
            GameplayInputRouter.QuickSlotPressed -= SelectSlot;
            GameplayInputRouter.EscapePressed -= Cancel;
        }

        private void Update()
        {
            if (cancelledRemaining > 0f)
            {
                cancelledRemaining -= Time.unscaledDeltaTime;
                if (cancelledRemaining <= 0f)
                {
                    Transition(ThrowState.Idle);
                }
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.unscaledDeltaTime;
                if (cooldownRemaining <= 0f)
                {
                    Transition(ThrowState.Idle);
                }
            }

            UpdateProjectile();
            if (State == ThrowState.Throwing
                || State == ThrowState.Cooldown
                || State == ThrowState.Cancelled)
            {
                return;
            }

            if (!HasThrowableSelected())
            {
                trajectoryPreview?.Hide();
                Transition(ThrowState.Idle);
                return;
            }

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.fKey.wasPressedThisFrame);
            bool held = (mouse != null && mouse.leftButton.isPressed)
                || (keyboard != null && keyboard.fKey.isPressed);
            bool released = (mouse != null && mouse.leftButton.wasReleasedThisFrame)
                || (keyboard != null && keyboard.fKey.wasReleasedThisFrame);

            if (State == ThrowState.Idle && pressed)
            {
                Transition(ThrowState.Aiming);
                chargeController.Begin();
            }

            if (State != ThrowState.Aiming
                && State != ThrowState.Charging
                && State != ThrowState.Ready)
            {
                return;
            }

            if (State == ThrowState.Aiming)
            {
                Transition(ThrowState.Charging);
            }

            if (!held || released)
            {
                ReleaseThrow();
                return;
            }

            chargeController.Tick(Time.unscaledDeltaTime);
            lastCharge01 = chargeController.Charge01;
            if (lastCharge01 >= 0.999f)
            {
                Transition(ThrowState.Ready);
            }

            UpdatePreview();
        }

        private void SelectSlot(int slot)
        {
            carrier?.SelectSlot(slot);
        }

        private bool HasThrowableSelected()
        {
            return carrier != null
                && carrier.HasTool
                && carrier.HeldUse == ThrowableUse.Thrown;
        }

        private void UpdatePreview()
        {
            if (trajectoryPreview == null)
            {
                return;
            }

            Vector3 direction = ToolUseInput.ReadAimDirection(transform.position)
                ?? transform.forward;
            float range = Mathf.Lerp(
                ThrowableCatalog.MinimumThrowRangeMeters,
                ThrowableCatalog.ThrowRangeMeters,
                Mathf.Clamp01(lastCharge01));
            lastSolution = ThrowTrajectorySolver.Solve(
                transform.position + Vector3.up * 0.9f,
                direction,
                range,
                obstacleLayers.value,
                0f);
            LastRange = range;
            HasSolution = true;
            trajectoryPreview.Show(
                lastSolution.Origin,
                lastSolution.Direction,
                range,
                obstacleLayers.value);
        }

        private void ReleaseThrow()
        {
            if (!HasSolution)
            {
                UpdatePreview();
            }

            chargeController.Release();
            lastCharge01 = Mathf.Clamp01(lastCharge01);
            trajectoryPreview?.Hide();
            if (!HasSolution)
            {
                Transition(ThrowState.Idle);
                return;
            }

            lastThrownKind = carrier.HeldKind;
            CreateProjectile(lastSolution);
            Transition(ThrowState.Throwing);
        }

        private void CreateProjectile(ThrowTrajectorySolver.Solution solution)
        {
            DestroyProjectile();
            projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = $"Validation {lastThrownKind} Projectile";
            projectile.transform.position = solution.Origin;
            projectile.transform.localScale = Vector3.one * 0.34f;
            Collider collider = projectile.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.72f, 0.12f);
            }

            projectileElapsed = 0f;
        }

        private void UpdateProjectile()
        {
            if (projectile == null)
            {
                return;
            }

            projectileElapsed += Time.unscaledDeltaTime;
            float elapsed = Mathf.Min(
                projectileElapsed,
                lastSolution.TravelledTime);
            projectile.transform.position = ThrowTrajectorySolver.PositionAt(
                lastSolution.Origin,
                lastSolution.Velocity,
                elapsed);

            if (projectileElapsed < lastSolution.TravelledTime)
            {
                return;
            }

            DestroyProjectile();
            cooldownRemaining = cooldownSeconds;
            Transition(ThrowState.Cooldown);
        }

        public void Cancel()
        {
            if (State != ThrowState.Aiming
                && State != ThrowState.Charging
                && State != ThrowState.Ready)
            {
                return;
            }

            chargeController.Cancel();
            trajectoryPreview?.Hide();
            HasSolution = false;
            cancelledRemaining = 0.2f;
            Transition(ThrowState.Cancelled);
        }

        private void DestroyProjectile()
        {
            if (projectile != null)
            {
                Destroy(projectile);
                projectile = null;
            }
        }

        private void Transition(ThrowState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(next);
        }

        private void OnDestroy()
        {
            DestroyProjectile();
        }
    }
}
