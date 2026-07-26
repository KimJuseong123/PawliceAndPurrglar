using System;
using System.Collections.Generic;
using PawsAndLoot.Gameplay.Camera;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class LocalPlayerRoleSelector : MonoBehaviour
    {
        [SerializeField]
        private PlayerRole defaultRole = PlayerRole.Police;

        [SerializeField]
        private List<PlayerRoleControlBinding> bindings = new();

        [SerializeField]
        private TopDownFollowCamera followCamera;

        public PlayerRole ActiveRole { get; private set; }
        public PlayerRoleControlBinding ActiveBinding { get; private set; }
        public IReadOnlyList<PlayerRoleControlBinding> Bindings => bindings;
        public bool IsGameplayInputEnabled { get; private set; } = true;

        public void Configure(
            IEnumerable<PlayerRoleControlBinding> roleBindings,
            TopDownFollowCamera camera,
            PlayerRole configuredDefaultRole)
        {
            bindings = new List<PlayerRoleControlBinding>(roleBindings);
            followCamera = camera;
            defaultRole = configuredDefaultRole;
            IsGameplayInputEnabled = true;
        }

        public void SelectRole(PlayerRole role)
        {
            if (!Enum.IsDefined(typeof(PlayerRole), role))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    "Unknown local player role.");
            }

            PlayerRoleControlBinding selected = null;
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                if (binding?.Identity == null
                    || binding.KeyboardInput == null)
                {
                    continue;
                }

                bool isSelected = binding.Role == role;
                bool canControl =
                    isSelected && IsGameplayInputEnabled;
                binding.KeyboardInput.IsLocallyControlled = canControl;
                if (binding.InteractionInput != null)
                {
                    binding.InteractionInput.IsLocallyControlled = canControl;
                }

                if (binding.LootDropInput != null)
                {
                    binding.LootDropInput.IsLocallyControlled = canControl;
                }

                if (isSelected)
                {
                    selected = binding;
                }
            }

            if (selected == null)
            {
                throw new InvalidOperationException(
                    $"No player control binding exists for role '{role}'.");
            }

            ActiveRole = role;
            ActiveBinding = selected;
            followCamera?.SetTarget(selected.Identity.transform, true);
        }

        public void DisableGameplayInput()
        {
            IsGameplayInputEnabled = false;
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                if (binding?.KeyboardInput == null)
                {
                    continue;
                }

                binding.KeyboardInput.IsLocallyControlled = false;
                if (binding.InteractionInput != null)
                {
                    binding.InteractionInput.IsLocallyControlled = false;
                }

                if (binding.LootDropInput != null)
                {
                    binding.LootDropInput.IsLocallyControlled = false;
                }
            }
        }

        /// <summary>
        /// Role decided by the lobby for this machine, if a session assigned
        /// one. Survives the scene load into the match, which is why it is
        /// static; cleared when a fresh process starts.
        /// </summary>
        private static PlayerRole? _overrideRole;

        public static PlayerRole? OverriddenRole => _overrideRole;

        public static void OverrideRole(PlayerRole role)
        {
            _overrideRole = role;
        }

        public static void ClearOverriddenRole()
        {
            _overrideRole = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOverride()
        {
            _overrideRole = null;
        }

        public static PlayerRole ResolveRole(
            IEnumerable<string> arguments,
            PlayerRole fallback)
        {
            // A lobby assignment wins over the command line, because the server
            // decided it and both machines must agree.
            if (_overrideRole.HasValue)
            {
                return _overrideRole.Value;
            }

            if (arguments == null)
            {
                return fallback;
            }

            string[] values = new List<string>(arguments).ToArray();
            for (int index = 0; index < values.Length - 1; index++)
            {
                if (!string.Equals(
                        values[index],
                        "-playerRole",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Enum.TryParse(
                    values[index + 1],
                    true,
                    out PlayerRole parsed)
                    && Enum.IsDefined(typeof(PlayerRole), parsed)
                    ? parsed
                    : fallback;
            }

            return fallback;
        }

        private void Awake()
        {
            SelectRole(ResolveRole(
                Environment.GetCommandLineArgs(),
                defaultRole));
        }
    }
}
