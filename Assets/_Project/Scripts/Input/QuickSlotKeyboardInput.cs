using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Input
{
    /// <summary>
    /// Offline quick-slot input. In a network session the same key is read by
    /// <see cref="Integration.Network.NetworkInputBridge"/> and sent to the
    /// host instead, so this component intentionally stays local-only there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuickSlotKeyboardInput : MonoBehaviour
    {
        [SerializeField]
        private ToolCarrier carrier;

        [SerializeField]
        private bool isLocallyControlled = true;

        public void Configure(ToolCarrier configuredCarrier, bool locallyControlled)
        {
            carrier = configuredCarrier;
            isLocallyControlled = locallyControlled;
        }

        private void Awake()
        {
            carrier ??= GetComponent<ToolCarrier>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForSceneCarriers()
        {
            foreach (ToolCarrier candidate in
                FindObjectsByType<ToolCarrier>(FindObjectsSortMode.None))
            {
                if (candidate == null
                    || candidate.GetComponent<QuickSlotKeyboardInput>() != null)
                {
                    continue;
                }

                QuickSlotKeyboardInput input =
                    candidate.gameObject.AddComponent<QuickSlotKeyboardInput>();
                input.Configure(candidate, true);
            }
        }

        private void OnEnable()
        {
            GameplayInputRouter.QuickSlotPressed += HandleQuickSlotPressed;
        }

        private void OnDisable()
        {
            GameplayInputRouter.QuickSlotPressed -= HandleQuickSlotPressed;
        }

        private void HandleQuickSlotPressed(int slot)
        {
            if (!isLocallyControlled
                || carrier == null
                || NetworkManager.Singleton?.IsListening == true)
            {
                return;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            if (selector != null && carrier.Role != selector.ActiveRole)
            {
                return;
            }

            carrier.SelectSlot(slot);
        }
    }
}
