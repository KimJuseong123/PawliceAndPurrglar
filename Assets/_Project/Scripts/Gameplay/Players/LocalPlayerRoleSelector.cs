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

        public void Configure(
            IEnumerable<PlayerRoleControlBinding> roleBindings,
            TopDownFollowCamera camera,
            PlayerRole configuredDefaultRole)
        {
            bindings = new List<PlayerRoleControlBinding>(roleBindings);
            followCamera = camera;
            defaultRole = configuredDefaultRole;
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
                binding.KeyboardInput.IsLocallyControlled = isSelected;
                if (binding.InteractionInput != null)
                {
                    binding.InteractionInput.IsLocallyControlled = isSelected;
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

        public static PlayerRole ResolveRole(
            IEnumerable<string> arguments,
            PlayerRole fallback)
        {
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
