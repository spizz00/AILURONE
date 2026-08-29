using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.Opening
{
    [DisallowMultipleComponent]
    public sealed class OpeningSoftPanelGraphic : MaskableGraphic
    {
        public enum PanelMode
        {
            BottomGradient,
            SoftPanel
        }

        [SerializeField] private PanelMode mode;
        [SerializeField, Range(0.02f, 0.45f)] private float edgeFade = 0.14f;

        public PanelMode Mode => mode;
        public float EdgeFade => edgeFade;

        public void Configure(PanelMode panelMode, Color panelColor, float fade)
        {
            mode = panelMode;
            color = panelColor;
            edgeFade = Mathf.Clamp(fade, 0.02f, 0.45f);
            raycastTarget = false;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            if (mode == PanelMode.BottomGradient)
            {
                PopulateBottomGradient(vertexHelper, rect);
                return;
            }

            PopulateSoftPanel(vertexHelper, rect);
        }

        private void PopulateBottomGradient(VertexHelper vertexHelper, Rect rect)
        {
            Color32 opaque = color;
            Color transparent = color;
            transparent.a = 0f;
            Color32 clear = transparent;

            AddVertex(vertexHelper, new Vector2(rect.xMin, rect.yMin), opaque);
            AddVertex(vertexHelper, new Vector2(rect.xMax, rect.yMin), opaque);
            AddVertex(vertexHelper, new Vector2(rect.xMax, rect.yMax), clear);
            AddVertex(vertexHelper, new Vector2(rect.xMin, rect.yMax), clear);
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
        }

        private void PopulateSoftPanel(VertexHelper vertexHelper, Rect rect)
        {
            float fade = Mathf.Clamp(edgeFade, 0.02f, 0.45f);
            float[] x =
            {
                rect.xMin,
                Mathf.Lerp(rect.xMin, rect.xMax, fade),
                Mathf.Lerp(rect.xMin, rect.xMax, 1f - fade),
                rect.xMax
            };
            float[] y =
            {
                rect.yMin,
                Mathf.Lerp(rect.yMin, rect.yMax, fade),
                Mathf.Lerp(rect.yMin, rect.yMax, 1f - fade),
                rect.yMax
            };
            float[] alpha = { 0f, 1f, 1f, 0f };

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    Color vertexColor = color;
                    vertexColor.a *= alpha[column] * alpha[row];
                    AddVertex(
                        vertexHelper,
                        new Vector2(x[column], y[row]),
                        vertexColor);
                }
            }

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    int bottomLeft = row * 4 + column;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + 4;
                    int topRight = topLeft + 1;
                    vertexHelper.AddTriangle(bottomLeft, bottomRight, topRight);
                    vertexHelper.AddTriangle(topRight, topLeft, bottomLeft);
                }
            }
        }

        private static void AddVertex(
            VertexHelper vertexHelper,
            Vector2 position,
            Color32 vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertex.uv0 = Vector2.zero;
            vertexHelper.AddVert(vertex);
        }
    }
}
