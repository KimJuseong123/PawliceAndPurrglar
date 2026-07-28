using System.Collections.Generic;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Input;
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

            // NET-005/006. The action keys are switched off for the same reason
            // movement is: a machine must have no local path that bypasses the
            // host, or the two sides can disagree about who picked up what.
            foreach (PlayerInteractionInput input in
                Object.FindObjectsByType<PlayerInteractionInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            foreach (LootDropInput input in
                Object.FindObjectsByType<LootDropInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            foreach (CompanionCommandKeyboardInput input in
                Object.FindObjectsByType<CompanionCommandKeyboardInput>(
                    FindObjectsSortMode.None))
            {
                input.IsLocallyControlled = false;
            }

            // THROW-007. The prop key joins the rest: a client that resolved its
            // own throw would decide it hit while the host decided it missed.
            foreach (PawsAndLoot.Gameplay.Items.ToolUseInput input in
                Object.FindObjectsByType<
                    PawsAndLoot.Gameplay.Items.ToolUseInput>(
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
            SubmitActions(link, keyboard);
        }

        /// <summary>
        /// Actions are edge-triggered, so they are sent only on the frame the key
        /// goes down. Sending them continuously would let one press be applied
        /// many times on the host.
        /// </summary>
        private static void SubmitActions(
            NetworkPlayerLink link,
            Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                link.SubmitInteractRpc();
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                link.SubmitDropRpc();
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                link.SubmitUseToolRpc();
            }

            int command = ReadCompanionCommandKey(keyboard);
            if (command > 0)
            {
                link.SubmitCompanionCommandRpc(command);
            }
        }

        private static int ReadCompanionCommandKey(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                return 1;
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                return 2;
            }

            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                return 3;
            }

            return keyboard.digit4Key.wasPressedThisFrame ? 4 : 0;
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
