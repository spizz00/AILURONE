using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.Opening
{
    [DisallowMultipleComponent]
    public sealed class OpeningHoldSkipRingGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField, Min(0.5f)] private float thickness = 3f;
        [SerializeField, Range(8, 128)] private int segments = 64;
        [SerializeField] private bool clockwise = true;

        public float Progress => progress;
        public float Thickness => thickness;
        public int Segments => segments;
        public bool Clockwise => clockwise;

        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, value))
            {
                return;
            }

            progress = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (progress <= 0f)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            float outerRadius = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            float innerRadius = Mathf.Max(0f, outerRadius - thickness);
            int usedSegments = Mathf.Max(1, Mathf.CeilToInt(segments * progress));
            float direction = clockwise ? -1f : 1f;

            for (int index = 0; index <= usedSegments; index++)
            {
                float t = Mathf.Min(progress, index / (float)segments);
                float angle = (90f + direction * 360f * t) * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertexHelper.AddVert(radial * outerRadius, color, Vector2.zero);
                vertexHelper.AddVert(radial * innerRadius, color, Vector2.zero);
            }

            for (int index = 0; index < usedSegments; index++)
            {
                int vertex = index * 2;
                vertexHelper.AddTriangle(vertex, vertex + 2, vertex + 1);
                vertexHelper.AddTriangle(vertex + 2, vertex + 3, vertex + 1);
            }
        }
    }
}
