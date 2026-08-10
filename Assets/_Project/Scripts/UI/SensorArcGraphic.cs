using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// One arc of the wifi-style sensor signal, generated rather than drawn from
    /// a sprite.
    ///
    /// The first attempt used plain <c>Image</c> rects, which are rectangles: at
    /// the size needed to be noticed they read as three fat bars, not a signal.
    /// A ring segment is what makes the shape recognisable, and it is fifty lines
    /// of arithmetic against an imported sprite that would need its own asset,
    /// import settings and a matching thickness at every radius.
    ///
    /// Presentation only, and a <c>MaskableGraphic</c> so it lives in the canvas
    /// like any other UI element.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SensorArcGraphic : MaskableGraphic
    {
        [SerializeField, Min(1f)]
        private float innerRadius = 40f;

        [SerializeField, Min(1f)]
        private float thickness = 14f;

        /// <summary>
        /// Total sweep. A wifi arc is a good deal less than a semicircle — at
        /// 180° it reads as a ring rather than a signal pointing somewhere.
        /// </summary>
        [SerializeField, Range(20f, 180f)]
        private float sweepDegrees = 96f;

        [SerializeField, Min(2)]
        private int segments = 24;

        /// <summary>
        /// Serialised, so the presenter can sort its arcs innermost-first after
        /// finding them at runtime.
        /// </summary>
        public float InnerRadius => innerRadius;

        public void Configure(
            float configuredInnerRadius,
            float configuredThickness,
            float configuredSweepDegrees)
        {
            innerRadius = configuredInnerRadius;
            thickness = configuredThickness;
            sweepDegrees = configuredSweepDegrees;
            SetVerticesDirty();
        }

        /// <summary>
        /// Builds the ring segment, centred on straight up so the whole rect can
        /// be rotated to point wherever the sensor is.
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();

            float half = sweepDegrees * 0.5f;
            float outer = innerRadius + thickness;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            for (int step = 0; step <= segments; step++)
            {
                float angle =
                    (-half + sweepDegrees * step / segments)
                    * Mathf.Deg2Rad;
                var direction = new Vector2(
                    Mathf.Sin(angle),
                    Mathf.Cos(angle));

                vertex.position = direction * innerRadius;
                helper.AddVert(vertex);
                vertex.position = direction * outer;
                helper.AddVert(vertex);
            }

            for (int step = 0; step < segments; step++)
            {
                int at = step * 2;
                helper.AddTriangle(at, at + 1, at + 3);
                helper.AddTriangle(at, at + 3, at + 2);
            }
        }
    }
}
