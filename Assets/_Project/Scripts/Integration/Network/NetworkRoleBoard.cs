using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Integration.Voice;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Decides which connected player is the police and which is the thief.
    ///
    /// Host authority (DEC-027): only the server writes the assignment, and it
    /// travels as a NetworkVariable so both screens read the same value. A
    /// client can ask to swap but cannot apply one, which is what structurally
    /// prevents two players from picking the same role.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkRoleBoard : NetworkBehaviour
    {
        /// <summary>
        /// Client id owning the police role. Written by the server only.
        /// </summary>
        private readonly NetworkVariable<ulong> _policeClientId =
            new(
                0UL,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// True once two players are present and the split has been made.
        /// </summary>
        private readonly NetworkVariable<bool> _assigned =
            new(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public event Action RoleAssignmentChanged;

        public bool IsAssigned => _assigned.Value;
        public ulong PoliceClientId => _policeClientId.Value;

        /// <summary>
        /// The role this machine will play. Meaningful only once
        /// <see cref="IsAssigned"/> is true.
        /// </summary>
        public PlayerRole LocalRole =>
            NetworkManager != null
            && NetworkManager.LocalClientId == _policeClientId.Value
                ? PlayerRole.Police
                : PlayerRole.Thief;

        public override void OnNetworkSpawn()
        {
            _policeClientId.OnValueChanged += HandlePoliceChanged;
            _assigned.OnValueChanged += HandleAssignedChanged;

            if (!IsServer)
            {
                return;
            }

            // The host takes police by default so a session is always playable
            // without anyone touching the swap button.
            _policeClientId.Value = NetworkManager.LocalClientId;
            NetworkManager.OnClientConnectedCallback +=
                HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;
            RefreshAssignment();
        }

        public override void OnNetworkDespawn()
        {
            _policeClientId.OnValueChanged -= HandlePoliceChanged;
            _assigned.OnValueChanged -= HandleAssignedChanged;

            if (!IsServer || NetworkManager == null)
            {
                return;
            }

            NetworkManager.OnClientConnectedCallback -=
                HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -=
                HandleClientDisconnected;
        }

        /// <summary>
        /// Hands each machine its role for the coming match.
        ///
        /// Called by the server while the board is still alive in the lobby.
        /// Every machine stores the answer in a plain local value, which is what
        /// carries it across the scene load: a spawned NetworkObject does not
        /// survive a server-driven Single-mode load on the client side, and
        /// relying on one left the client with no role at all (`ISSUE-016`).
        /// </summary>
        [Rpc(SendTo.Everyone)]
        public void CommitRolesRpc(ulong policeClientId)
        {
            PlayerRole role =
                NetworkManager != null
                && NetworkManager.LocalClientId == policeClientId
                    ? PlayerRole.Police
                    : PlayerRole.Thief;
            LocalPlayerRoleSelector.OverrideRole(role);
            GameLogger.Info(
                GameLogCategory.Network,
                $"Committed local role '{role}' for the match.",
                this);
        }

        /// <summary>
        /// Server entry point used just before the match scene loads.
        /// </summary>
        public bool TryCommitRoles()
        {
            if (!IsServer || !IsAssigned)
            {
                return false;
            }

            CommitRolesRpc(_policeClientId.Value);
            VoiceSessionCapabilityClient capabilityClient =
                FindFirstObjectByType<VoiceSessionCapabilityClient>();
            capabilityClient?.RegisterForRoles(this);
            return true;
        }

        [Rpc(SendTo.Everyone)]
        public void ApplyVoiceTokensRpc(
            string sessionId,
            string hostToken,
            string policeToken,
            string thiefToken)
        {
            string token = LocalRole == PlayerRole.Police
                ? policeToken
                : thiefToken;
            if (NetworkManager != null
                && NetworkManager.LocalClientId == NetworkManager.ServerClientId)
            {
                token = hostToken;
            }

            VoiceCapabilityStore.Set(sessionId, token);
        }

        /// <summary>
        /// Either side may ask to swap; the server performs it. Routing the
        /// request instead of the change is what keeps one source of truth.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestSwapRolesRpc()
        {
            if (!IsServer)
            {
                return;
            }

            ApplySwap();
        }

        /// <summary>
        /// Server-side swap. Public so a host can swap without an RPC round
        /// trip and so tests can drive it directly.
        /// </summary>
        public bool ApplySwap()
        {
            if (!IsServer || NetworkManager == null)
            {
                return false;
            }

            ulong other = FindOtherClientId(_policeClientId.Value);
            if (other == _policeClientId.Value)
            {
                return false;
            }

            _policeClientId.Value = other;
            GameLogger.Info(
                GameLogCategory.Network,
                $"Roles swapped: police is now client {other}.",
                this);
            return true;
        }

        /// <summary>
        /// Assignment only counts once both players are present, so neither
        /// side starts a match believing it owns both roles.
        /// </summary>
        private void RefreshAssignment()
        {
            if (!IsServer || NetworkManager == null)
            {
                return;
            }

            int count = NetworkManager.ConnectedClientsIds.Count;
            _assigned.Value =
                count >= NetworkSessionController.MaximumPlayers;
        }

        private ulong FindOtherClientId(ulong current)
        {
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                if (id != current)
                {
                    return id;
                }
            }

            return current;
        }

        private void HandleClientConnected(ulong clientId)
        {
            RefreshAssignment();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId == _policeClientId.Value
                && NetworkManager != null)
            {
                // The remaining player inherits police so the board never
                // points at someone who has left.
                _policeClientId.Value = NetworkManager.LocalClientId;
            }

            RefreshAssignment();
        }

        private void HandlePoliceChanged(ulong previous, ulong current)
        {
            RoleAssignmentChanged?.Invoke();
        }

        private void HandleAssignedChanged(bool previous, bool current)
        {
            RoleAssignmentChanged?.Invoke();
        }
    }
}
