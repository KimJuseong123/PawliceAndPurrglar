using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    /// <summary>
    /// Wires the existing carrier and interaction scanner to the validation
    /// match state. This object exists only in ThrowInteractionValidation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowInteractionValidationBootstrap : MonoBehaviour
    {
        [SerializeField]
        private PlayerConfig playerConfig;

        [SerializeField]
        private GameObject player;

        public void Configure(PlayerConfig configuredPlayerConfig, GameObject configuredPlayer)
        {
            playerConfig = configuredPlayerConfig;
            player = configuredPlayer;
        }

        private void Awake()
        {
            ValidationMatchState matchState =
                FindFirstObjectByType<ValidationMatchState>();
            if (matchState == null || player == null || playerConfig == null)
            {
                Debug.LogError(
                    "[ThrowInteractionValidation] Bootstrap requires " +
                    "ValidationMatchState, player, and PlayerConfig.",
                    this);
                return;
            }

            PlayerRoleIdentity identity =
                player.GetComponent<PlayerRoleIdentity>();
            ToolCarrier carrier = player.GetComponent<ToolCarrier>();
            PlayerInteractionScanner scanner =
                player.GetComponent<PlayerInteractionScanner>();
            if (identity == null || carrier == null || scanner == null)
            {
                Debug.LogError(
                    "[ThrowInteractionValidation] Player requires " +
                    "PlayerRoleIdentity, ToolCarrier, and PlayerInteractionScanner.",
                    this);
                return;
            }

            identity.Configure(PlayerRole.Police);
            carrier.Configure(identity, matchState);
            carrier.ApplyReplicated(true, ThrowableKind.Rock);
            scanner.Configure(identity, playerConfig, matchState);
        }
    }
}
