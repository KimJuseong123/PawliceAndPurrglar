using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class TechnicalNetworkPlayer : NetworkBehaviour
    {
        private readonly NetworkVariable<Vector3> _networkPosition =
            new(
                Vector3.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<TechnicalPlayerRole> _role =
            new(
                TechnicalPlayerRole.Unassigned,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private Renderer _renderer;
        private Vector3 _spawnPosition;
        private bool _ownerSpawnInitialized;

        public Vector3 NetworkPosition => _networkPosition.Value;
        public TechnicalPlayerRole Role => _role.Value;

        public override void OnNetworkSpawn()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _role.OnValueChanged += OnRoleChanged;

            if (IsServer)
            {
                _role.Value = TechnicalRoleAssignment.GetRole(
                    OwnerClientId,
                    NetworkManager.ServerClientId);
            }

            ApplyRole(_role.Value);
        }

        public override void OnNetworkDespawn()
        {
            _role.OnValueChanged -= OnRoleChanged;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner && _ownerSpawnInitialized)
            {
                float phase = (float)OwnerClientId * 1.3f;
                Vector3 movement = new(
                    0f,
                    0f,
                    Mathf.Sin(Time.unscaledTime * 1.2f + phase) * 1.15f);
                _networkPosition.Value = _spawnPosition + movement;
            }

            transform.position = Vector3.Lerp(
                transform.position,
                _networkPosition.Value,
                12f * Time.deltaTime);
        }

        private void OnRoleChanged(
            TechnicalPlayerRole previousRole,
            TechnicalPlayerRole currentRole)
        {
            ApplyRole(currentRole);
        }

        private void ApplyRole(TechnicalPlayerRole role)
        {
            ApplyRoleColor(role);
            if (!IsOwner || role == TechnicalPlayerRole.Unassigned)
            {
                return;
            }

            _spawnPosition = TechnicalRoleAssignment.GetSpawnPosition(role);
            _networkPosition.Value = _spawnPosition;
            transform.position = _spawnPosition;
            _ownerSpawnInitialized = true;
        }

        private void ApplyRoleColor(TechnicalPlayerRole role)
        {
            if (_renderer == null)
            {
                return;
            }

            Color color = role switch
            {
                TechnicalPlayerRole.Police =>
                    new Color(0.08f, 0.38f, 0.95f),
                TechnicalPlayerRole.Thief =>
                    new Color(0.95f, 0.22f, 0.12f),
                _ => Color.white
            };
            var properties = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            _renderer.SetPropertyBlock(properties);
        }
    }
}
