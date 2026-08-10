using UnityEngine;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Small world-space transcript/interpretation bubble. It is deliberately
    /// self-contained so the Mock voice route is visible even in generated
    /// scenes that do not have a hand-authored UI prefab.
    /// </summary>
    public sealed class SpeechBubbleView : MonoBehaviour
    {
        private TextMesh _text;
        private float _remaining;

        public string Message => _text != null ? _text.text : string.Empty;

        public static void ShowOn(
            Transform target,
            string message,
            Color color,
            float seconds = 3f)
        {
            if (target == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SpeechBubbleView view = target.GetComponent<SpeechBubbleView>()
                ?? target.gameObject.AddComponent<SpeechBubbleView>();
            view.Show(message, color, seconds);
        }

        public void Show(string message, Color color, float seconds = 3f)
        {
            EnsureText();
            _text.text = message;
            _text.color = color;
            _remaining = Mathf.Max(0.1f, seconds);
            _text.gameObject.SetActive(true);
        }

        private void EnsureText()
        {
            if (_text != null)
            {
                return;
            }

            GameObject bubble = new("Speech Bubble");
            bubble.transform.SetParent(transform, false);
            bubble.transform.localPosition = Vector3.up * 2.2f;
            _text = bubble.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = 0.08f;
            _text.fontSize = 42;
            _text.fontStyle = FontStyle.Bold;
            _text.richText = false;
        }

        private void LateUpdate()
        {
            if (_text == null)
            {
                return;
            }

            if (Camera.main != null)
            {
                _text.transform.rotation = Camera.main.transform.rotation;
            }

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                _text.gameObject.SetActive(false);
            }
        }
    }
}
