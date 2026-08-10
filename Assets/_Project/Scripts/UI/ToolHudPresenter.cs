using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Shows what the local player is holding and which key uses it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private ToolCarrier carrier;

        [SerializeField]
        private StunState stun;

        [SerializeField]
        private PoliceWallet policeWallet;

        [SerializeField]
        private Text slotLabel;

        public string SlotText =>
            slotLabel != null ? slotLabel.text : string.Empty;

        public void Configure(Text configuredSlotLabel)
        {
            slotLabel = configuredSlotLabel;
            Refresh();
        }

        private bool SuppressIfModernHudExists()
        {
            RoleAwareHudController modernHud =
                FindFirstObjectByType<RoleAwareHudController>();
            if (modernHud == null)
            {
                return false;
            }

            gameObject.SetActive(false);
            return true;
        }

        private void ResolveLocalPlayer()
        {
            if (carrier != null)
            {
                return;
            }

            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (!assigned.HasValue)
            {
                LocalPlayerRoleSelector selector =
                    FindFirstObjectByType<LocalPlayerRoleSelector>();
                if (selector == null)
                {
                    return;
                }

                assigned = selector.ActiveRole;
            }

            PlayerRole role = assigned.Value;
            foreach (ToolCarrier candidate in
                FindObjectsByType<ToolCarrier>(FindObjectsSortMode.None))
            {
                if (candidate.Role != role)
                {
                    continue;
                }

                carrier = candidate;
                stun = candidate.GetComponent<StunState>();
                policeWallet = candidate.GetComponent<PoliceWallet>();
                return;
            }
        }

        public void Refresh()
        {
            if (slotLabel == null)
            {
                return;
            }

            if (stun != null && stun.IsStunned)
            {
                slotLabel.text = $"기절!  {stun.RemainingSeconds:0.0}초";
                slotLabel.color = new Color(1f, 0.55f, 0.4f);
                return;
            }

            if (carrier == null || !carrier.HasTool)
            {
                slotLabel.text = policeWallet != null
                    ? $"손에 든 것 없음  ·  {policeWallet.Amount}골드"
                    : "손에 든 것 없음";
                slotLabel.color = new Color(0.55f, 0.6f, 0.68f);
                return;
            }

            bool placed = carrier.HeldUse == ThrowableUse.Placed;
            string what = ThrowableCatalog.GetDisplayName(carrier.HeldKind);
            string quantity = carrier.HeldQuantity > 1
                ? $" x{carrier.HeldQuantity}"
                : string.Empty;
            string purse = policeWallet != null
                ? $"  ·  {policeWallet.Amount}골드"
                : string.Empty;
            slotLabel.text = placed
                ? $"{what}{quantity}  [F] 설치{purse}"
                : $"{what}{quantity}  [좌클릭/F] 조준 후 던지기{purse}";
            slotLabel.color = new Color(1f, 0.92f, 0.72f);
        }

        private void Update()
        {
            if (SuppressIfModernHudExists())
            {
                return;
            }

            ResolveLocalPlayer();
            Refresh();
        }
    }
}
