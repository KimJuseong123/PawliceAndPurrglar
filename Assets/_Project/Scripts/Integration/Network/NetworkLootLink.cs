using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Network
{
    /// <summary>
    /// NET-005. Replicates one loot item's ownership and state.
    ///
    /// Only the host decides who holds an item, so two players cannot pick up
    /// the same loot: the second request simply finds it already carried when
    /// the host evaluates it. Clients never run loot transitions; they mirror.
    ///
    /// One link per item rather than a central table, so adding loot to the map
    /// needs no change here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkLootLink : NetworkBehaviour
    {
        private const int NoCarrier = -1;

        private readonly NetworkVariable<int> _state =
            new(
                (int)LootState.Available,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> _position =
            new(
                Vector3.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// Carrier as a role rather than a client id, because the match has one
        /// player per role and the client resolves it to a local object.
        /// </summary>
        private readonly NetworkVariable<int> _carrierRole =
            new(
                NoCarrier,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        [SerializeField]
        private LootItem loot;

        public LootState ReplicatedState => (LootState)_state.Value;
        public int ReplicatedCarrierRole => _carrierRole.Value;

        public void Configure(LootItem configuredLoot)
        {
            loot = configuredLoot;
        }

        private void Awake()
        {
            if (loot == null)
            {
                loot = GetComponent<LootItem>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (loot == null)
            {
                loot = GetComponent<LootItem>();
            }

            if (loot != null)
            {
                loot.SetRemoteControlled(!IsServer);
            }
        }

        public override void OnNetworkDespawn()
        {
            // Back to local rules when the session ends, so the offline
            // playtest keeps working in the same scene.
            if (loot != null)
            {
                loot.SetRemoteControlled(false);
            }
        }

        private void Update()
        {
            if (loot == null || !IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                _state.Value = (int)loot.CurrentState;
                _position.Value = loot.PresentationRoot != null
                    ? loot.PresentationRoot.position
                    : loot.transform.position;
                _carrierRole.Value = loot.CurrentCarrier != null
                    ? (int)ResolveRole(loot.CurrentCarrier)
                    : NoCarrier;
                return;
            }

            loot.ApplyRemoteState(
                (LootState)_state.Value,
                _position.Value,
                ResolveCarrier(_carrierRole.Value));
        }

        private static PlayerRole ResolveRole(LootCarrier carrier)
        {
            PlayerRoleIdentity identity =
                carrier.GetComponent<PlayerRoleIdentity>();
            return identity != null ? identity.Role : PlayerRole.Thief;
        }

        private static LootCarrier ResolveCarrier(int role)
        {
            if (role == NoCarrier)
            {
                return null;
            }

            foreach (LootCarrier carrier in
                FindObjectsByType<LootCarrier>(
                    FindObjectsSortMode.None))
            {
                PlayerRoleIdentity identity =
                    carrier.GetComponent<PlayerRoleIdentity>();
                if (identity != null && (int)identity.Role == role)
                {
                    return carrier;
                }
            }

            return null;
        }
    }
}
