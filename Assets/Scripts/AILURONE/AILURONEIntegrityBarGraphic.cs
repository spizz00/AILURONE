#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Texture-free, segmented integrity line.
    ///
    /// Draw order:
    /// 1. empty track
    /// 2. red damage ghost interval
    /// 3. current integrity
    /// 4. technical cuts / nodes / reboot scan
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONEIntegrityBarGraphic : MaskableGraphic
    {
        [Header("Runtime")]
        [SerializeField, Range(0f, 1f)]
        private float integrityRatio = 1f;

        [SerializeField, Range(0f, 1f)]
        private float ghostRatio = 1f;

        [SerializeField] private bool rebootMode;

        [SerializeField] private float runtimeTime;

        [Header("Colours")]
        [SerializeField] private Color fillColor =
            new Color(0.94f, 0.98f, 1f, 1f);

        [SerializeField] private Color ghostColor =
            new Color(1f, 0.04f, 0.18f, 0.90f);

        [SerializeField] private Color trackColor =
            new Color(0.04f, 0.08f, 0.11f, 0.88f);

        [SerializeField] private Color accentColor =
            new Color(0.18f, 0.89f, 1f, 1f);

        [SerializeField] private Color warningAccentColor =
            new Color(1f, 0.04f, 0.42f, 1f);

        [Header("Geometry")]
        [Min(2f)]
        [SerializeField] private float trackHeight = 8f;

        [Min(0f)]
        [SerializeField] private float nodeRadius = 7f;

        private static readonly Vector2[] SegmentRanges =
        {
            new Vector2(0.000f, 0.565f),
            new Vector2(0.595f, 0.760f),
            new Vector2(0.790f, 0.875f),
            new Vector2(0.900f, 0.925f)
        };

        public void SetVisualState(
            float currentRatio,
            float delayedGhostRatio,
            Color currentFillColor,
            Color delayedGhostColor,
            bool isRebooting,
            float unscaledTime
        )
        {
            float newIntegrity =
                Mathf.Clamp01(currentRatio);

            float newGhost =
                Mathf.Clamp01(
                    Mathf.Max(newIntegrity, delayedGhostRatio)
                );

            bool changed =
                Mathf.Abs(integrityRatio - newIntegrity) > 0.0005f
                || Mathf.Abs(ghostRatio - newGhost) > 0.0005f
                || fillColor != currentFillColor
                || ghostColor != delayedGhostColor
                || rebootMode != isRebooting
                || isRebooting;

            integrityRatio = newIntegrity;
            ghostRatio = newGhost;
            fillColor = currentFillColor;
            ghostColor = delayedGhostColor;
            rebootMode = isRebooting;
            runtimeTime = unscaledTime;

            if (changed)
            {
                SetVerticesDirty();
            }
        }

        public void Configure(
            Color emptyTrack,
            Color cyanAccent,
            Color warningAccent,
            float lineHeight,
            float technicalNodeRadius
        )
        {
            trackColor = emptyTrack;
            accentColor = cyanAccent;
            warningAccentColor = warningAccent;
            trackHeight = Mathf.Max(2f, lineHeight);
            nodeRadius = Mathf.Max(0f, technicalNodeRadius);
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

            float lineHeight =
                Mathf.Min(trackHeight, rect.height * 0.55f);

            float centreY = rect.center.y;
            float trackEndRatio = 0.925f;

            DrawSegmentedInterval(
                vertexHelper,
                rect,
                0f,
                1f,
                trackColor,
                centreY,
                lineHeight
            );

            DrawSegmentedInterval(
                vertexHelper,
                rect,
                integrityRatio,
                ghostRatio,
                ghostColor,
                centreY,
                lineHeight + 1.5f
            );

            Color resolvedFill = fillColor;

            if (rebootMode)
            {
                float pulse =
                    0.64f
                    + Mathf.Sin(runtimeTime * 9f) * 0.20f;

                resolvedFill = Color.Lerp(
                    fillColor,
                    accentColor,
                    Mathf.Clamp01(pulse)
                );
            }

            DrawSegmentedInterval(
                vertexHelper,
                rect,
                0f,
                integrityRatio,
                resolvedFill,
                centreY,
                lineHeight
            );

            DrawDottedLead(
                vertexHelper,
                rect,
                centreY,
                lineHeight,
                integrityRatio,
                resolvedFill
            );

            DrawDiagonalBreaks(
                vertexHelper,
                rect,
                centreY,
                lineHeight,
                resolvedFill
            );

            DrawTechnicalNodes(
                vertexHelper,
                rect,
                centreY
            );

            if (rebootMode)
            {
                DrawRebootScan(
                    vertexHelper,
                    rect,
                    centreY,
                    lineHeight
                );
            }
            else if (integrityRatio > 0.005f
                && integrityRatio < trackEndRatio)
            {
                float x =
                    Mathf.Lerp(
                        rect.xMin,
                        rect.xMax,
                        integrityRatio * trackEndRatio
                    );

                AddLine(
                    vertexHelper,
                    new Vector2(x, centreY - lineHeight * 0.78f),
                    new Vector2(x, centreY + lineHeight * 0.78f),
                    1.4f,
                    accentColor
                );
            }
        }

        private static void DrawSegmentedInterval(
            VertexHelper vertexHelper,
            Rect rect,
            float lowerRatio,
            float upperRatio,
            Color intervalColor,
            float centreY,
            float height
        )
        {
            float lower =
                Mathf.Clamp01(Mathf.Min(lowerRatio, upperRatio));

            float upper =
                Mathf.Clamp01(Mathf.Max(lowerRatio, upperRatio));

            if (upper - lower <= 0.0001f
                || intervalColor.a <= 0.001f)
            {
                return;
            }

            foreach (Vector2 range in SegmentRanges)
            {
                float start = Mathf.Max(range.x, lower);
                float end = Mathf.Min(range.y, upper);

                if (end <= start)
                {
                    continue;
                }

                float xMin =
                    Mathf.Lerp(rect.xMin, rect.xMax, start);

                float xMax =
                    Mathf.Lerp(rect.xMin, rect.xMax, end);

                AddQuad(
                    vertexHelper,
                    new Rect(
                        xMin,
                        centreY - height * 0.5f,
                        xMax - xMin,
                        height
                    ),
                    intervalColor
                );
            }
        }

        private void DrawDottedLead(
            VertexHelper vertexHelper,
            Rect rect,
            float centreY,
            float height,
            float ratio,
            Color activeColor
        )
        {
            const int dotCount = 26;

            float startRatio = 0.025f;
            float endRatio = 0.245f;

            float dotSize =
                Mathf.Clamp(height * 0.24f, 1.4f, 2.6f);

            for (int index = 0; index < dotCount; index++)
            {
                float t =
                    dotCount <= 1
                        ? 0f
                        : index / (float)(dotCount - 1);

                float positionRatio =
                    Mathf.Lerp(startRatio, endRatio, t);

                float x =
                    Mathf.Lerp(rect.xMin, rect.xMax, positionRatio);

                Color dotColor =
                    ratio >= positionRatio
                        ? trackColor
                        : new Color(
                            trackColor.r,
                            trackColor.g,
                            trackColor.b,
                            trackColor.a * 0.55f
                        );

                if (rebootMode)
                {
                    dotColor = activeColor;
                    dotColor.a *=
                        0.42f
                        + Mathf.Sin(
                            runtimeTime * 8f + index * 0.44f
                        ) * 0.22f;
                }

                AddQuad(
                    vertexHelper,
                    new Rect(
                        x - dotSize * 0.5f,
                        centreY - dotSize * 0.5f,
                        dotSize,
                        dotSize
                    ),
                    dotColor
                );
            }
        }

        private void DrawDiagonalBreaks(
            VertexHelper vertexHelper,
            Rect rect,
            float centreY,
            float height,
            Color stripeColor
        )
        {
            float baseX =
                Mathf.Lerp(rect.xMin, rect.xMax, 0.566f);

            float stripeWidth =
                Mathf.Max(2f, rect.width * 0.006f);

            float stripeGap =
                Mathf.Max(3f, rect.width * 0.0075f);

            for (int index = 0; index < 3; index++)
            {
                float x =
                    baseX + index * stripeGap;

                AddSlantedQuad(
                    vertexHelper,
                    x,
                    centreY,
                    stripeWidth,
                    height * 1.25f,
                    stripeWidth * 0.75f,
                    stripeColor
                );
            }
        }

        private void DrawTechnicalNodes(
            VertexHelper vertexHelper,
            Rect rect,
            float centreY
        )
        {
            float radius =
                Mathf.Min(
                    nodeRadius,
                    rect.height * 0.30f
                );

            if (radius <= 0.5f)
            {
                return;
            }

            float firstX =
                Mathf.Lerp(rect.xMin, rect.xMax, 0.957f);

            float secondX =
                Mathf.Lerp(rect.xMin, rect.xMax, 0.992f);

            Color firstColor =
                integrityRatio <= 0.30f
                    ? warningAccentColor
                    : accentColor;

            DrawOctagonOutline(
                vertexHelper,
                new Vector2(firstX, centreY),
                radius,
                1.3f,
                firstColor
            );

            DrawPlus(
                vertexHelper,
                new Vector2(firstX, centreY),
                radius * 0.40f,
                1.1f,
                firstColor
            );

            DrawOctagonOutline(
                vertexHelper,
                new Vector2(secondX, centreY),
                radius,
                1.3f,
                warningAccentColor
            );

            DrawPlus(
                vertexHelper,
                new Vector2(secondX, centreY),
                radius * 0.40f,
                1.1f,
                warningAccentColor
            );
        }

        private void DrawRebootScan(
            VertexHelper vertexHelper,
            Rect rect,
            float centreY,
            float height
        )
        {
            float scan =
                Mathf.Repeat(runtimeTime * 0.82f, 1f);

            float x =
                Mathf.Lerp(
                    rect.xMin,
                    Mathf.Lerp(rect.xMin, rect.xMax, 0.925f),
                    scan
                );

            Color scanColor = accentColor;
            scanColor.a = 0.92f;

            AddLine(
                vertexHelper,
                new Vector2(x, centreY - height * 1.20f),
                new Vector2(x, centreY + height * 1.20f),
                2.2f,
                scanColor
            );
        }

        private static void DrawOctagonOutline(
            VertexHelper vertexHelper,
            Vector2 centre,
            float radius,
            float width,
            Color lineColor
        )
        {
            Vector2[] points = new Vector2[8];

            for (int index = 0; index < points.Length; index++)
            {
                float angle =
                    Mathf.Deg2Rad * (22.5f + index * 45f);

                points[index] =
                    centre
                    + new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)
                    ) * radius;
            }

            for (int index = 0; index < points.Length; index++)
            {
                AddLine(
                    vertexHelper,
                    points[index],
                    points[(index + 1) % points.Length],
                    width,
                    lineColor
                );
            }
        }

        private static void DrawPlus(
            VertexHelper vertexHelper,
            Vector2 centre,
            float halfSize,
            float width,
            Color plusColor
        )
        {
            AddLine(
                vertexHelper,
                centre + Vector2.left * halfSize,
                centre + Vector2.right * halfSize,
                width,
                plusColor
            );

            AddLine(
                vertexHelper,
                centre + Vector2.down * halfSize,
                centre + Vector2.up * halfSize,
                width,
                plusColor
            );
        }

        private static void AddSlantedQuad(
            VertexHelper vertexHelper,
            float centreX,
            float centreY,
            float width,
            float height,
            float slant,
            Color quadColor
        )
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            Vector2 bottomLeft =
                new Vector2(
                    centreX - halfWidth - slant,
                    centreY - halfHeight
                );

            Vector2 topLeft =
                new Vector2(
                    centreX - halfWidth + slant,
                    centreY + halfHeight
                );

            Vector2 topRight =
                new Vector2(
                    centreX + halfWidth + slant,
                    centreY + halfHeight
                );

            Vector2 bottomRight =
                new Vector2(
                    centreX + halfWidth - slant,
                    centreY - halfHeight
                );

            int startIndex = vertexHelper.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = quadColor;

            vertex.position = bottomLeft;
            vertexHelper.AddVert(vertex);

            vertex.position = topLeft;
            vertexHelper.AddVert(vertex);

            vertex.position = topRight;
            vertexHelper.AddVert(vertex);

            vertex.position = bottomRight;
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

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color lineColor
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
            vertex.color = lineColor;

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
