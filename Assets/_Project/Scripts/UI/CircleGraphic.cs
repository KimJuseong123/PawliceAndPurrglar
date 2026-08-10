using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Sprite-free circle graphic used by the essential HUD. It keeps the
    /// minimap independent from Unity's editor-only built-in skin assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CircleGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float ringThickness;

        public float RingThickness
        {
            get => ringThickness;
            set
            {
                ringThickness = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radiusX = Mathf.Max(0f, rect.width * 0.5f);
            float radiusY = Mathf.Max(0f, rect.height * 0.5f);
            const int segments = 48;

            if (radiusX <= 0f || radiusY <= 0f)
            {
                return;
            }

            if (ringThickness <= 0f)
            {
                vertexHelper.AddVert(center, color, Vector2.zero);
                for (int index = 0; index <= segments; index++)
                {
                    float angle = Mathf.PI * 2f * index / segments;
                    Vector3 point = new(
                        center.x + Mathf.Cos(angle) * radiusX,
                        center.y + Mathf.Sin(angle) * radiusY,
                        0f);
                    vertexHelper.AddVert(point, color, Vector2.zero);
                }

                for (int index = 0; index < segments; index++)
                {
                    vertexHelper.AddTriangle(0, index + 1, index + 2);
                }

                return;
            }

            float innerRadiusX = Mathf.Max(0f, radiusX - ringThickness);
            float innerRadiusY = Mathf.Max(0f, radiusY - ringThickness);
            for (int index = 0; index <= segments; index++)
            {
                float angle = Mathf.PI * 2f * index / segments;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                vertexHelper.AddVert(
                    new Vector3(
                        center.x + cosine * radiusX,
                        center.y + sine * radiusY,
                        0f),
                    color,
                    Vector2.zero);
                vertexHelper.AddVert(
                    new Vector3(
                        center.x + cosine * innerRadiusX,
                        center.y + sine * innerRadiusY,
                        0f),
                    color,
                    Vector2.zero);
            }

            for (int index = 0; index < segments; index++)
            {
                int outer = index * 2;
                int inner = outer + 1;
                int nextOuter = outer + 2;
                int nextInner = inner + 2;
                vertexHelper.AddTriangle(outer, nextOuter, nextInner);
                vertexHelper.AddTriangle(outer, nextInner, inner);
            }
        }
    }
}
