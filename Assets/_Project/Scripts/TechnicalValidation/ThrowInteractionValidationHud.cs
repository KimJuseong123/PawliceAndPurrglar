using PawliceAndPurrglar.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.TechnicalValidation
{
    /// <summary>
    /// Runtime-built HUD for the validation scene only. It uses safe anchors
    /// and a scale-with-screen-size canvas so the POC can be checked at several
    /// resolutions without changing any production HUD prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowInteractionValidationHud : MonoBehaviour
    {
        private ThrowInteractionValidationController throwController;
        private ValidationInteractionInput interactionInput;
        private Canvas canvas;
        private TextMeshProUGUI throwLabel;
        private TextMeshProUGUI instructionLabel;
        private TextMeshProUGUI promptKeyLabel;
        private TextMeshProUGUI promptActionLabel;
        private TextMeshProUGUI resultLabel;
        private Image promptProgress;
        private GameObject promptPanel;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError(
                    "[ThrowInteractionValidation] Canvas is missing.",
                    this);
                return;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>()
                ?? gameObject.AddComponent<CanvasScaler>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            throwController = FindFirstObjectByType<
                ThrowInteractionValidationController>();
            interactionInput = FindFirstObjectByType<ValidationInteractionInput>();
            BuildView();
        }

        private void Update()
        {
            if (throwLabel != null && throwController != null)
            {
                string landing = throwController.HasSolution
                    ? FormatVector(throwController.LastSolution.Landing)
                    : "-";
                string blocked = throwController.HasSolution
                    && throwController.LastSolution.Blocked
                    ? "BLOCKED"
                    : "CLEAR";
                throwLabel.text =
                    $"THROW TRAJECTORY POC\n" +
                    $"STATE  {throwController.State}\n" +
                    $"CHARGE {throwController.Charge01:P0}  " +
                    $"RANGE {throwController.LastRange:0.0}m\n" +
                    $"LANDING {landing}  {blocked}";
            }

            if (instructionLabel != null)
            {
                instructionLabel.text =
                    "WASD Move   1-4 Quick Slot   " +
                    "LMB / F Aim + Throw   RMB / ESC Cancel\n" +
                    "E Context Interaction   V Voice   " +
                    "R Unassigned (intentionally)";
            }

            BindPrompt();
            if (resultLabel != null && interactionInput != null)
            {
                resultLabel.text = interactionInput.LastResult;
            }
        }

        private void BindPrompt()
        {
            if (promptPanel == null || interactionInput == null)
            {
                return;
            }

            ValidationInteractionTarget target = interactionInput.CurrentTarget;
            bool visible = target != null;
            promptPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            string key = GameplayInputRouter.InteractionBindingLabel;
            promptKeyLabel.text = target.RequiresHold
                ? $"[Hold {key}]"
                : $"[{key}]";
            promptActionLabel.text =
                $"{target.TargetLabel}: {target.ActionLabel}";
            bool holding = interactionInput.IsHolding;
            promptProgress.gameObject.SetActive(holding);
            promptProgress.fillAmount = interactionInput.HoldProgress01;
        }

        private void BuildView()
        {
            throwLabel = CreateLabel(
                "Throw Status",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -190f),
                new Vector2(560f, -28f),
                24f,
                TextAlignmentOptions.TopLeft);
            throwLabel.color = new Color(1f, 0.85f, 0.45f);

            instructionLabel = CreateLabel(
                "Instructions",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(28f, 28f),
                new Vector2(900f, 104f),
                19f,
                TextAlignmentOptions.BottomLeft);
            instructionLabel.color = new Color(0.75f, 0.85f, 0.95f);

            resultLabel = CreateLabel(
                "Interaction Result",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-620f, 28f),
                new Vector2(-28f, 82f),
                18f,
                TextAlignmentOptions.BottomRight);
            resultLabel.color = new Color(0.7f, 1f, 0.75f);

            promptPanel = CreatePanel(
                "Context Prompt",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-330f, 128f),
                new Vector2(330f, 210f),
                new Color(0.03f, 0.06f, 0.1f, 0.92f));
            promptKeyLabel = CreateLabel(
                "Prompt Key",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(18f, 12f),
                new Vector2(150f, -12f),
                22f,
                TextAlignmentOptions.Center);
            promptKeyLabel.transform.SetParent(promptPanel.transform, false);
            promptActionLabel = CreateLabel(
                "Prompt Action",
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(160f, 12f),
                new Vector2(-18f, -12f),
                22f,
                TextAlignmentOptions.MidlineLeft);
            promptActionLabel.transform.SetParent(promptPanel.transform, false);
            promptProgress = CreateProgress(promptPanel.transform);
            promptPanel.SetActive(false);
        }

        private TextMeshProUGUI CreateLabel(
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject labelObject = new(name, typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        private GameObject CreatePanel(
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            GameObject panel = new(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Image CreateProgress(Transform parent)
        {
            GameObject progressObject = new(
                "Hold Progress",
                typeof(RectTransform),
                typeof(Image));
            progressObject.transform.SetParent(parent, false);
            RectTransform rect = progressObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(18f, 6f);
            rect.offsetMax = new Vector2(-18f, 12f);
            Image image = progressObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            return image;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.0}, {value.y:0.0}, {value.z:0.0})";
        }
    }
}
