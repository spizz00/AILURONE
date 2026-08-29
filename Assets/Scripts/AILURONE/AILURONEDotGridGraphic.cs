#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Tiny vector dot matrix used as restrained HUD micro-detail.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONEDotGridGraphic : MaskableGraphic
    {
        [Min(1)]
        [SerializeField] private int columns = 3;

        [Min(1)]
        [SerializeField] private int rows = 4;

        [Min(0.5f)]
        [SerializeField] private float dotSize = 2.4f;

        [Min(0f)]
        [SerializeField] private float horizontalGap = 6f;

        [Min(0f)]
        [SerializeField] private float verticalGap = 6f;

        public void Configure(
            int columnCount,
            int rowCount,
            float size,
            float xGap,
            float yGap,
            Color dotColor
        )
        {
            columns = Mathf.Max(1, columnCount);
            rows = Mathf.Max(1, rowCount);
            dotSize = Mathf.Max(0.5f, size);
            horizontalGap = Mathf.Max(0f, xGap);
            verticalGap = Mathf.Max(0f, yGap);
            color = dotColor;
            raycastTarget = false;

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;

            float totalWidth =
                columns * dotSize
                + Mathf.Max(0, columns - 1) * horizontalGap;

            float totalHeight =
                rows * dotSize
                + Mathf.Max(0, rows - 1) * verticalGap;

            float startX = rect.center.x - totalWidth * 0.5f;
            float startY = rect.center.y - totalHeight * 0.5f;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float x =
                        startX + column * (dotSize + horizontalGap);

                    float y =
                        startY + row * (dotSize + verticalGap);

                    AddQuad(
                        vertexHelper,
                        new Rect(x, y, dotSize, dotSize),
                        color
                    );
                }
            }
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Rect rect,
            Color quadColor
        )
        {
            int startIndex = vertexHelper.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = quadColor;

            vertex.position = new Vector2(rect.xMin, rect.yMin);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector2(rect.xMin, rect.yMax);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector2(rect.xMax, rect.yMax);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector2(rect.xMax, rect.yMin);
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
