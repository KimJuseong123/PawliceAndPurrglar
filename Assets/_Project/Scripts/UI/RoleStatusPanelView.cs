using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class RoleStatusPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text roleLabel;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Image roleTint;

        public void Configure(
            TMP_Text configuredRoleLabel,
            TMP_Text configuredObjectiveLabel,
            TMP_Text configuredStatusLabel,
            Image configuredRoleTint)
        {
            roleLabel = configuredRoleLabel;
            objectiveLabel = configuredObjectiveLabel;
            statusLabel = configuredStatusLabel;
            roleTint = configuredRoleTint;
        }

        public void Bind(RoleStatusPanelViewModel model)
        {
            if (roleLabel != null) roleLabel.text = model.RoleLabel;
            if (objectiveLabel != null) objectiveLabel.text = model.Objective;
            if (statusLabel != null) statusLabel.text = model.Status;
            if (roleTint != null)
            {
                roleTint.color = model.Role == HudRole.Police
                    ? new Color(0.15f, 0.45f, 1f)
                    : new Color(0.95f, 0.2f, 0.16f);
            }
        }
    }
}
