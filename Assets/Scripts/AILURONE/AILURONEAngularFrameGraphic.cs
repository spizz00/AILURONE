#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Lightweight vector UI frame with clipped corners.
    /// No texture, sprite, material or collider is created.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONEAngularFrameGraphic : MaskableGraphic
    {
        [Header("Frame")]
        [SerializeField] private Color fillColor =
            new Color(0.015f, 0.035f, 0.052f, 0.48f);

        [SerializeField] private Color borderColor =
            new Color(0.90f, 0.97f, 1.00f, 0.94f);

        [SerializeField] private Color accentColor =
            new Color(0.18f, 0.89f, 1.00f, 1f);

        [Min(0f)]
        [SerializeField] private float borderWidth = 2f;

        [Min(0f)]
        [SerializeField] private float cornerCut = 13f;

        [Min(0f)]
        [SerializeField] private float accentLength = 28f;

        [SerializeField] private bool drawAccents = true;

        public void Configure(
            Color fill,
            Color border,
            Color accent,
            float width,
            float cut,
            float accentSegmentLength,
            bool accents
        )
        {
            fillColor = fill;
            borderColor = border;
            accentColor = accent;
            borderWidth = Mathf.Max(0f, width);
            cornerCut = Mathf.Max(0f, cut);
            accentLength = Mathf.Max(0f, accentSegmentLength);
            drawAccents = accents;

            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;

            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float cut = Mathf.Clamp(
                cornerCut,
                0f,
                Mathf.Min(rect.width, rect.height) * 0.45f
            );

            Vector2[] points =
            {
                new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMax - cut, rect.yMin),
                new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax, rect.yMax - cut),
                new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMin + cut, rect.yMax),
                new Vector2(rect.xMin, rect.yMax - cut),
                new Vector2(rect.xMin, rect.yMin + cut)
            };

            if (fillColor.a > 0.001f)
            {
                Vector2 centre = rect.center;

                for (int index = 0; index < points.Length; index++)
                {
                    int next = (index + 1) % points.Length;

                    AddTriangle(
                        vertexHelper,
                        centre,
                        points[index],
                        points[next],
                        fillColor
                    );
                }
            }

            if (borderWidth > 0.001f && borderColor.a > 0.001f)
            {
                for (int index = 0; index < points.Length; index++)
                {
                    int next = (index + 1) % points.Length;

                    AddLine(
                        vertexHelper,
                        points[index],
                        points[next],
                        borderWidth,
                        borderColor
                    );
                }
            }

            if (!drawAccents || accentColor.a <= 0.001f)
            {
                return;
            }

            float availableTop =
                Mathf.Max(0f, rect.width - cut * 2f);

            float length =
                Mathf.Min(accentLength, availableTop * 0.34f);

            AddLine(
                vertexHelper,
                new Vector2(
                    rect.xMax - cut - length,
                    rect.yMax
                ),
                new Vector2(
                    rect.xMax - cut,
                    rect.yMax
                ),
                borderWidth + 0.8f,
                accentColor
            );

            AddLine(
                vertexHelper,
                new Vector2(
                    rect.xMin + cut,
                    rect.yMin
                ),
                new Vector2(
                    rect.xMin + cut + length * 0.68f,
                    rect.yMin
                ),
                borderWidth + 0.8f,
                accentColor
            );
        }

        private static void AddTriangle(
            VertexHelper vertexHelper,
            Vector2 pointA,
            Vector2 pointB,
            Vector2 pointC,
            Color color
        )
        {
            int startIndex = vertexHelper.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = pointA;
            vertexHelper.AddVert(vertex);

            vertex.position = pointB;
            vertexHelper.AddVert(vertex);

            vertex.position = pointC;
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(
                startIndex,
                startIndex + 1,
                startIndex + 2
            );
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color color
        )
        {
            Vector2 direction = end - start;

            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 perpendicular =
                new Vector2(-direction.y, direction.x).normalized
                * (width * 0.5f);

            int startIndex = vertexHelper.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = start - perpendicular;
            vertexHelper.AddVert(vertex);

            vertex.position = start + perpendicular;
            vertexHelper.AddVert(vertex);

            vertex.position = end + perpendicular;
            vertexHelper.AddVert(vertex);

            vertex.position = end - perpendicular;
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(
                startIndex,
                startIndex + 1,
                startIndex + 2
            );

            vertexHelper.AddTriangle(
                startIndex,
                startIndex + 2,
                startIndex + 3
            );
        }
    }
}
