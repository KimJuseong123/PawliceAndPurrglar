using System.Collections.Generic;
using PawsAndLoot.Gameplay.Players;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// NET-003 input half. Reads this machine's keys and sends them to the host
    /// for its own role only.
    ///
    /// The local keyboard components are switched off in a session so a machine
    /// cannot move a character directly; everything goes through the host. That
    /// is what satisfies "only control your own character": a machine has no
    /// path at all to the other role.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkInputBridge : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager networkManager;

        [SerializeField]
        private List<NetworkPlayerLink> links = new();

        [SerializeField]
        private NetworkRoleBoard roleBoard;

        private bool _configuredLocalControl;

        public PlayerRole LocalRole { get; private set; } =
            PlayerRole.Police;
        public bool HasLocalRole { get; private set; }

        public void Configure(
            NetworkManager manager,
            IEnumerable<NetworkPlayerLink> playerLinks)
        {
            networkManager = manager;
            links = new List<NetworkPlayerLink>(playerLinks);
        }

        private NetworkRoleBoard ResolveRoleBoard()
        {
            if (roleBoard == null)
            {
                roleBoard = Object.FindFirstObjectByType<
                    NetworkRoleBoard>();
            }

            return roleBoard;
        }

        private NetworkPlayerLink FindLink(PlayerRole role)
        {
            foreach (NetworkPlayerLink link in links)
            {
                if (link != null && link.Role == role)
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>
        /// Turns off every local keyboard driver once a session owns movement.
        /// Done once, and only in a session, so the offline playtest keeps its
        /// direct local control.
        /// </summary>
        private void EnsureLocalControlDisabled()
        {
            if (_configuredLocalControl)
            {
                return;
            }

            _configuredLocalControl = true;
            foreach (PlayerKeyboardInput input in
                Object.FindObjectsByType<PlayerKeyboardInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }
        }

        private void Update()
        {
            // Resolved at runtime because the manager lives in the Bootstrap
            // scene and only exists once a session has started.
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (networkManager == null
                || !networkManager.IsListening)
            {
                return;
            }

            // Read the role from the local value the server committed before
            // the scene load. The board itself does not exist in the match
            // scene on a client, which is exactly why the role is stored
            // locally rather than looked up here.
            PlayerRole? assigned =
                LocalPlayerRoleSelector.OverriddenRole;
            if (!assigned.HasValue)
            {
                return;
            }

            EnsureLocalControlDisabled();
            LocalRole = assigned.Value;
            HasLocalRole = true;

            NetworkPlayerLink link = FindLink(LocalRole);
            if (link == null || !link.IsSpawned)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Vector2 move = keyboard == null
                ? Vector2.zero
                : new Vector2(
                    ReadAxis(keyboard.aKey, keyboard.dKey),
                    ReadAxis(keyboard.sKey, keyboard.wKey));
            bool dash = keyboard != null
                && keyboard.spaceKey.wasPressedThisFrame;

            link.SubmitInputRpc(move, dash);
        }

        private static float ReadAxis(
            KeyControl negative,
            KeyControl positive)
        {
            float value = 0f;
            if (negative != null && negative.isPressed)
            {
                value -= 1f;
            }

            if (positive != null && positive.isPressed)
            {
                value += 1f;
            }

            return value;
        }
    }
}
