using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    public sealed class MinimapAlertView : MonoBehaviour
    {
        [SerializeField] private Image marker;

        public void Configure(Image configuredMarker)
        {
            marker = configuredMarker;
        }

        public void Bind(MinimapAlertViewModel model)
        {
            gameObject.SetActive(model.Visible);
            if (marker == null) return;
            marker.sprite = model.Icon;
            marker.color = model.Tint;
            marker.enabled = model.Visible && model.Icon != null;
        }
    }
}
