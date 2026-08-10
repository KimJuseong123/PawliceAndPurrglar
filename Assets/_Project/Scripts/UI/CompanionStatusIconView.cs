using PawliceAndPurrglar.Companions;
using UnityEngine;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// World-space state icon driven by CompanionAgent state. The view owns the
    /// presentation; the AI never creates or manipulates UI objects directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionStatusIconView : MonoBehaviour
    {
        private CompanionAgent _agent;
        private TextMesh _text;
        private CompanionStatusId _lastStatus;

        public string CurrentIcon => _text != null ? _text.text : string.Empty;

        private void Awake()
        {
            _agent = GetComponent<CompanionAgent>();
            GameObject icon = new("Companion Status Icon");
            icon.transform.SetParent(transform, false);
            icon.transform.localPosition = Vector3.up * 2.35f;
            _text = icon.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = 0.1f;
            _text.fontSize = 48;
            _text.fontStyle = FontStyle.Bold;
            _text.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_agent == null || _text == null)
            {
                return;
            }

            CompanionStatusId status = _agent.CurrentStatus;
            if (status != _lastStatus)
            {
                _lastStatus = status;
                _text.text = GetIcon(status);
                _text.color = GetColor(status);
            }

            _text.gameObject.SetActive(status != CompanionStatusId.Idle);
            if (Camera.main != null)
            {
                _text.transform.rotation = Camera.main.transform.rotation;
            }
        }

        private static string GetIcon(CompanionStatusId status)
        {
            return status switch
            {
                CompanionStatusId.Stunned => "* * *",
                CompanionStatusId.Attacking => "!",
                CompanionStatusId.Confused => "?",
                CompanionStatusId.Tracking => "NOSE",
                CompanionStatusId.Guarding => "FLAG",
                CompanionStatusId.Chasing => ">>",
                CompanionStatusId.Distracted => "HEART",
                CompanionStatusId.HeardNoise => "SOUND",
                CompanionStatusId.FoundTarget => "FOUND",
                CompanionStatusId.SearchingHideout => "SEARCH",
                CompanionStatusId.CommandReceived => "OK",
                _ => string.Empty
            };
        }

        private static Color GetColor(CompanionStatusId status)
        {
            return status switch
            {
                CompanionStatusId.Stunned => new Color(1f, 0.85f, 0.2f),
                CompanionStatusId.Attacking => new Color(1f, 0.25f, 0.2f),
                CompanionStatusId.Confused => new Color(0.8f, 0.7f, 1f),
                CompanionStatusId.Distracted => new Color(1f, 0.45f, 0.75f),
                _ => Color.white
            };
        }
    }
}
