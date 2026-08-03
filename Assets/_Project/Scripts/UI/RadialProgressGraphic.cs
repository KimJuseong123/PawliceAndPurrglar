using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    [DisallowMultipleComponent]
    public sealed class RadialProgressGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)]
        private float fillAmount;

        [SerializeField, Min(0.5f)]
        private float ringThickness = 4f;

        [SerializeField, Min(8)]
        private int segments = 48;

        public float FillAmount
        {
            get => fillAmount;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(fillAmount, clamped))
                {
                    return;
                }

                fillAmount = clamped;
                SetVerticesDirty();
            }
        }

        public float RingThickness
        {
            get => ringThickness;
            set
            {
                float clamped = Mathf.Max(0.5f, value);
                if (Mathf.Approximately(ringThickness, clamped))
                {
                    return;
                }

                ringThickness = clamped;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fillAmount <= 0f)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f)
            {
                return;
            }

            float innerRadius = Mathf.Max(0f, radius - ringThickness);
            int stepCount = Mathf.Max(
                1,
                Mathf.CeilToInt(Mathf.Max(8, segments) * fillAmount));
            float sweep = Mathf.PI * 2f * fillAmount;
            float start = Mathf.PI * 0.5f;
            Vector2 center = rect.center;

            for (int index = 0; index <= stepCount; index++)
            {
                float t = (float)index / stepCount;
                float angle = start - sweep * t;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * radius, color, Vector2.zero);
                vh.AddVert(center + direction * innerRadius, color, Vector2.zero);
            }

            for (int index = 0; index < stepCount; index++)
            {
                int outer0 = index * 2;
                int inner0 = outer0 + 1;
                int outer1 = outer0 + 2;
                int inner1 = outer0 + 3;
                vh.AddTriangle(outer0, outer1, inner0);
                vh.AddTriangle(outer1, inner1, inner0);
            }
        }
    }
}
