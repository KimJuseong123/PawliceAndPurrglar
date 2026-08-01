using UnityEngine;
using UnityEngine.InputSystem;
using PawsAndLoot.Input;
using PawsAndLoot.Integration.Network;
using PawsAndLoot.Gameplay.Players;
using Unity.Netcode;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Reads the throw input for a locally controlled player, and where they
    /// aimed.
    ///
    /// Mirrors <c>PlayerInteractionInput</c> and <c>LootDropInput</c> exactly,
    /// including <see cref="IsLocallyControlled"/>. In a session the network
    /// input bridge switches every one of these off and sends the press to the
    /// host instead, so a client cannot use a prop on its own authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolUseInput : MonoBehaviour
    {
        [SerializeField]
        private ToolUseAction action;

        [SerializeField]
        private ThrowChargeController chargeController;

        [SerializeField]
        private ThrowTrajectoryPreview trajectoryPreview;

        [SerializeField]
        private LayerMask obstacleLayers = ~0;

        [SerializeField]
        private bool isLocallyControlled;

        private PlayerRoleIdentity identity;
        private bool isChargingThrow;

        public bool IsLocallyControlled
        {
            get => isLocallyControlled;
            set => isLocallyControlled = value;
        }

        public void Configure(
            ToolUseAction configuredAction,
            bool locallyControlled)
        {
            Configure(
                configuredAction,
                locallyControlled,
                null,
                null,
                Physics.AllLayers);
        }

        public void Configure(
            ToolUseAction configuredAction,
            bool locallyControlled,
            ThrowChargeController configuredChargeController,
            ThrowTrajectoryPreview configuredTrajectoryPreview,
            LayerMask configuredObstacleLayers)
        {
            action = configuredAction;
            isLocallyControlled = locallyControlled;
            chargeController = configuredChargeController;
            trajectoryPreview = configuredTrajectoryPreview;
            obstacleLayers = configuredObstacleLayers;
        }

        /// <summary>
        /// Where the cursor is pointing, as a flat direction from a player.
        ///
        /// The camera is fixed and tilted, so a screen position is only
        /// meaningful once it is put back on the ground: the cursor ray is
        /// crossed with the horizontal plane through the player's feet, and the
        /// direction to that spot is the aim. Reading the ray's own direction
        /// instead would aim everything at the horizon.
        ///
        /// Returns null when there is no cursor, no camera, or the ray points
        /// away from the ground — callers fall back to the character's facing
        /// rather than throwing at nothing.
        /// </summary>
        public static Vector3? ReadAimDirection(Vector3 fromPosition)
        {
            Mouse mouse = Mouse.current;
            // Fully qualified: PawsAndLoot.Gameplay.Camera is a namespace, so a
            // bare Camera inside Gameplay resolves to it and not to the type.
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (view == null)
            {
                return null;
            }

            // No cursor to read indoors.
            //
            // The interior view hides and locks the pointer so it can turn with a
            // low sensitivity, which leaves the cursor pinned to the middle of the
            // screen — reading it would aim every throw straight ahead regardless
            // of where the player was looking. Throwing along the camera's own
            // facing is both correct and what a third-person view implies: you
            // throw where you are looking.
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector3 facing = view.transform.forward;
                facing.y = 0f;
                return facing.sqrMagnitude > 0.0001f
                    ? facing.normalized
                    : null;
            }

            if (mouse == null)
            {
                return null;
            }

            Ray ray = view.ScreenPointToRay(
                mouse.position.ReadValue());
            var ground = new Plane(Vector3.up, fromPosition);
            if (!ground.Raycast(ray, out float distance))
            {
                return null;
            }

            Vector3 aim = ray.GetPoint(distance) - fromPosition;
            aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f
                ? aim.normalized
                : null;
        }

        private void Update()
        {
            if (!CanReadLocalInput() || action == null)
            {
                CancelCharge();
                return;
            }

            // Two bindings for one action: the mouse, because aiming and firing
            // with the same hand is what makes the cursor worth having, and F
            // because a player whose hand is on the keyboard should not have to
            // reach for the mouse to drop a banana at their own feet.
            bool pressed =
                (Mouse.current != null
                    && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null
                    && Keyboard.current.fKey.wasPressedThisFrame);
            bool held =
                (Mouse.current != null
                    && Mouse.current.leftButton.isPressed)
                || (Keyboard.current != null
                    && Keyboard.current.fKey.isPressed);
            bool released =
                (Mouse.current != null
                    && Mouse.current.leftButton.wasReleasedThisFrame)
                || (Keyboard.current != null
                    && Keyboard.current.fKey.wasReleasedThisFrame);

            if (GameplayInputRouter.GameplayInputSuppressed)
            {
                CancelCharge();
                return;
            }

            if (!action.HasThrowableSelected)
            {
                CancelCharge();
                if (pressed)
                {
                    SubmitOrUse(ReadAimDirection(transform.position), 1f);
                }

                return;
            }

            EnsureChargeComponents();
            if (pressed && !isChargingThrow)
            {
                isChargingThrow = true;
                chargeController.Begin();
            }

            if (!isChargingThrow)
            {
                trajectoryPreview.Hide();
                return;
            }

            if (released || !held)
            {
                Vector3? aim = ReadAimDirection(transform.position);
                float charge01 = chargeController.Release();
                isChargingThrow = false;
                trajectoryPreview.Hide();
                SubmitOrUse(aim, charge01);
                return;
            }

            chargeController.Tick(Time.unscaledDeltaTime);
            ShowTrajectoryPreview(chargeController.Charge01);
        }

        private void ShowTrajectoryPreview(float charge01)
        {
            Vector3 direction = ReadAimDirection(transform.position)
                ?? transform.forward;
            float range = Mathf.Lerp(
                ThrowableCatalog.MinimumThrowRangeMeters,
                ThrowableCatalog.ThrowRangeMeters,
                Mathf.Clamp01(charge01));
            trajectoryPreview.Show(
                action.ThrowOrigin,
                direction,
                range,
                obstacleLayers.value);
        }

        private void Awake()
        {
            identity ??= GetComponent<PlayerRoleIdentity>();
            EnsureChargeComponents();
        }

        private bool CanReadLocalInput()
        {
            if (isLocallyControlled)
            {
                return true;
            }

            identity ??= GetComponent<PlayerRoleIdentity>();
            if (identity == null)
            {
                return false;
            }

            if (NetworkManager.Singleton?.IsListening == true)
            {
                return IsLocalNetworkRole();
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null
                && selector.IsGameplayInputEnabled
                && selector.ActiveRole == identity.Role;
        }

        private bool IsLocalNetworkRole()
        {
            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (assigned.HasValue)
            {
                return assigned.Value == identity.Role;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null
                && selector.IsGameplayInputEnabled
                && selector.ActiveRole == identity.Role;
        }

        private void SubmitOrUse(Vector3? aim, float charge01)
        {
            if (NetworkManager.Singleton?.IsListening == true
                && TryFindNetworkLink(out NetworkPlayerLink link))
            {
                Vector3 direction = aim ?? Vector3.zero;
                direction.y = 0f;
                link.SubmitUseToolRpc(
                    direction.sqrMagnitude > 0.0001f
                        ? direction.normalized
                        : Vector3.zero,
                    charge01);
                return;
            }

            action.TryUse(aim, charge01);
        }

        private bool TryFindNetworkLink(out NetworkPlayerLink link)
        {
            identity ??= GetComponent<PlayerRoleIdentity>();
            foreach (NetworkPlayerLink candidate in
                FindObjectsByType<NetworkPlayerLink>(
                    FindObjectsSortMode.None))
            {
                if (candidate == null
                    || !candidate.IsSpawned
                    || identity == null
                    || candidate.Role != identity.Role)
                {
                    continue;
                }

                link = candidate;
                return true;
            }

            link = null;
            return false;
        }

        private void OnDisable()
        {
            CancelCharge();
        }

        private void EnsureChargeComponents()
        {
            if (chargeController == null)
            {
                chargeController = GetComponent<ThrowChargeController>();
            }

            if (chargeController == null)
            {
                chargeController =
                    gameObject.AddComponent<ThrowChargeController>();
            }

            if (trajectoryPreview == null)
            {
                trajectoryPreview = GetComponent<ThrowTrajectoryPreview>();
            }

            if (trajectoryPreview == null)
            {
                trajectoryPreview =
                    gameObject.AddComponent<ThrowTrajectoryPreview>();
            }
        }

        private void CancelCharge()
        {
            if (!isChargingThrow)
            {
                trajectoryPreview?.Hide();
                return;
            }

            isChargingThrow = false;
            chargeController?.Cancel();
            trajectoryPreview?.Hide();
        }
    }
}
