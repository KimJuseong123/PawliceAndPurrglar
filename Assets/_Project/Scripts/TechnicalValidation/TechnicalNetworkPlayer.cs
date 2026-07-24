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

        private Renderer _renderer;
        private Vector3 _spawnPosition;

        public Vector3 NetworkPosition => _networkPosition.Value;

        public override void OnNetworkSpawn()
        {
            _renderer = GetComponentInChildren<Renderer>();
            ApplyOwnerColor();

            if (!IsOwner)
            {
                return;
            }

            _spawnPosition = OwnerClientId == NetworkManager.ServerClientId
                ? new Vector3(-2.2f, 0.65f, 0f)
                : new Vector3(2.2f, 0.65f, 0f);
            _networkPosition.Value = _spawnPosition;
            transform.position = _spawnPosition;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
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

        private void ApplyOwnerColor()
        {
            if (_renderer == null)
            {
                return;
            }

            Color color = OwnerClientId == NetworkManager.ServerClientId
                ? new Color(0.08f, 0.38f, 0.95f)
                : new Color(0.95f, 0.22f, 0.12f);
            var properties = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            _renderer.SetPropertyBlock(properties);
        }
    }
}
