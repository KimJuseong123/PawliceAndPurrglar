using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Shows what the local player is holding and which key uses it.
    ///
    /// Read only, like every other presenter here. It never picks anything up
    /// and never uses anything, so the HUD cannot change the match.
    ///
    /// The key hint is on screen rather than in a manual because there is no
    /// tutorial: a player who does not know F exists is carrying a rock they
    /// will never throw.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private ToolCarrier carrier;

        [SerializeField]
        private StunState stun;

        [SerializeField]
        private Text slotLabel;

        public string SlotText =>
            slotLabel != null ? slotLabel.text : string.Empty;

        public void Configure(Text configuredSlotLabel)
        {
            slotLabel = configuredSlotLabel;
            Refresh();
        }

        /// <summary>
        /// Finds the local player's slot at runtime rather than at scene-build
        /// time.
        ///
        /// It has to be runtime: in a session the role is handed out by the host
        /// after the lobby, so which player is "mine" is not known when the
        /// scene is built. Binding it in the editor would show the host's slot
        /// on the client's screen.
        /// </summary>
        private void ResolveLocalPlayer()
        {
            if (carrier != null)
            {
                return;
            }

            // The lobby's assignment wins; offline it falls back to whichever
            // role the scene's selector actually activated.
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
                FindObjectsByType<ToolCarrier>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role != role)
                {
                    continue;
                }

                carrier = candidate;
                stun = candidate.GetComponent<StunState>();
                return;
            }
        }

        public void Refresh()
        {
            if (slotLabel == null)
            {
                return;
            }

            // Being stunned outranks the slot: a frozen player needs to know why
            // their keys stopped working before they need to know their
            // inventory.
            if (stun != null && stun.IsStunned)
            {
                slotLabel.text =
                    $"기절!  {stun.RemainingSeconds:0.0}초";
                slotLabel.color = new Color(1f, 0.55f, 0.4f);
                return;
            }

            if (carrier == null || !carrier.HasTool)
            {
                slotLabel.text = "손에 든 것 없음";
                slotLabel.color = new Color(0.55f, 0.6f, 0.68f);
                return;
            }

            bool placed = carrier.HeldUse == ThrowableUse.Placed;
            // Named by the catalog. A list here would drift out of step with the
            // enum the moment a prop is added.
            string what =
                ThrowableCatalog.GetDisplayName(carrier.HeldKind);
            // The mouse is named first for a throw, because aiming is the half a
            // player will not discover on their own: F alone worked, so nothing
            // ever told them the cursor mattered.
            slotLabel.text = placed
                ? $"{what}  [F] 설치"
                : $"{what}  [좌클릭] 커서 방향으로 던지기";
            slotLabel.color = new Color(1f, 0.92f, 0.72f);
        }

        private void Update()
        {
            ResolveLocalPlayer();
            Refresh();
        }
    }
}
