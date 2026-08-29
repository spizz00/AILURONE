#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
public class GravityLiftVisual : MonoBehaviour
{
    [Header("引用")]
    public GravityLiftZone liftZone;
    public BoxCollider liftBox;

    [Header("竖直上升线")]
    public bool showFlowLines = true;
    public int lineCount = 14;
    public float lineWidth = 0.04f;
    public float lineHeightOffset = 0.15f;
    public float scrollSpeed = 2.5f;
    public Color idleLineColor = new Color(0.1f, 0.8f, 1f, 0.65f);
    public Color activeLineColor = new Color(0.35f, 1f, 1f, 1f);

    [Header("顶部出口环")]
    public bool showTopRing = true;
    public int ringSegments = 96;
    public float topRingWidth = 0.055f;
    public Color topRingColor = new Color(0.0f, 1f, 0.85f, 1f);
    public float ringPulseSpeed = 4f;
    public float ringPulseAmount = 0.12f;

    [Header("出口方向箭头")]
    public bool showExitArrow = true;
    public float arrowLength = 2.2f;
    public float arrowWidth = 0.08f;
    public Color arrowColor = new Color(0.4f, 1f, 0.25f, 1f);

    [Header("玩家进入后增强")]
    public float activeRadiusCheckPadding = 0.15f;
    public float activeLineWidthMultiplier = 1.6f;

    private LineRenderer[] _flowLines;
    private LineRenderer _topRing;
    private LineRenderer _exitArrow;

    private Material _flowMaterial;
    private Material _topRingMaterial;
    private Material _arrowMaterial;

    private Transform _player;
    private float[] _lineOffsets;

    private void Awake()
    {
        if (liftZone == null)
        {
            liftZone = GetComponent<GravityLiftZone>();
        }

        if (liftBox == null)
        {
            liftBox = GetComponent<BoxCollider>();
        }

        CreateMaterials();
        CreateVisualObjects();
        FindPlayer();
    }

    private void Update()
    {
        if (liftZone == null)
        {
            liftZone = GetComponent<GravityLiftZone>();
        }

        if (liftBox == null)
        {
            liftBox = GetComponent<BoxCollider>();
        }

        if (_player == null)
        {
            FindPlayer();
        }

        bool playerInside = IsPlayerInsideLiftBox();

        UpdateFlowLines(playerInside);
        UpdateTopRing();
        UpdateExitArrow();
    }

    private void CreateMaterials()
    {
        _flowMaterial = CreateUnlitMaterial(idleLineColor);
        _topRingMaterial = CreateUnlitMaterial(topRingColor);
        _arrowMaterial = CreateUnlitMaterial(arrowColor);
    }

    private Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void CreateVisualObjects()
    {
        int safeLineCount = Mathf.Max(1, lineCount);
        _flowLines = new LineRenderer[safeLineCount];
        _lineOffsets = new float[safeLineCount];

        for (int i = 0; i < safeLineCount; i++)
        {
            GameObject obj = new GameObject("GravityLift_FlowLine_" + i);
            obj.transform.SetParent(transform);

            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.sharedMaterial = _flowMaterial;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = lineWidth;
            lr.numCapVertices = 4;

            _flowLines[i] = lr;
            _lineOffsets[i] = Random.Range(0f, 1f);

            SetLayerToIgnoreRaycast(obj);
        }

        GameObject ringObj = new GameObject("GravityLift_TopRing");
        ringObj.transform.SetParent(transform);

        _topRing = ringObj.AddComponent<LineRenderer>();
        _topRing.sharedMaterial = _topRingMaterial;
        _topRing.useWorldSpace = true;
        _topRing.loop = false;
        _topRing.widthMultiplier = topRingWidth;
        _topRing.numCapVertices = 4;
        _topRing.numCornerVertices = 4;

        SetLayerToIgnoreRaycast(ringObj);

        GameObject arrowObj = new GameObject("GravityLift_ExitArrow");
        arrowObj.transform.SetParent(transform);

        _exitArrow = arrowObj.AddComponent<LineRenderer>();
        _exitArrow.sharedMaterial = _arrowMaterial;
        _exitArrow.useWorldSpace = true;
        _exitArrow.positionCount = 4;
        _exitArrow.widthMultiplier = arrowWidth;
        _exitArrow.numCapVertices = 4;
        _exitArrow.numCornerVertices = 4;

        SetLayerToIgnoreRaycast(arrowObj);
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            _player = playerObj.transform;
        }
    }

    private void UpdateFlowLines(bool playerInside)
    {
        if (_flowLines == null) return;

        bool shouldShow = showFlowLines && liftBox != null;

        for (int i = 0; i < _flowLines.Length; i++)
        {
            if (_flowLines[i] == null) continue;

            _flowLines[i].enabled = shouldShow;

            if (!shouldShow) continue;

            Vector3 bottom;
            Vector3 top;

            GetFlowLinePoints(i, out bottom, out top);

            float scroll = Mathf.Repeat(Time.time * scrollSpeed + _lineOffsets[i], 1f);
            Vector3 animatedBottom = Vector3.Lerp(bottom, top, scroll);
            Vector3 animatedTop = Vector3.Lerp(bottom, top, Mathf.Repeat(scroll + 0.35f, 1f));

            // 如果 wrap 后顺序反了，就直接画短线段，避免横跨整个区域。
            if (animatedTop.y < animatedBottom.y)
            {
                animatedTop = animatedBottom + Vector3.up * 0.8f;
            }

            _flowLines[i].SetPosition(0, animatedBottom);
            _flowLines[i].SetPosition(1, animatedTop);

            _flowLines[i].widthMultiplier = playerInside
                ? lineWidth * activeLineWidthMultiplier
                : lineWidth;
        }

        if (_flowMaterial != null)
        {
            _flowMaterial.color = playerInside ? activeLineColor : idleLineColor;
        }
    }

    private void GetFlowLinePoints(int index, out Vector3 bottom, out Vector3 top)
    {
        Vector3 localCenter = liftBox.center;
        Vector3 size = liftBox.size;

        int safeCount = Mathf.Max(1, _flowLines.Length);

        float angle = (float)index / safeCount * Mathf.PI * 2f;
        float radiusX = size.x * 0.38f;
        float radiusZ = size.z * 0.38f;

        float wobble = Mathf.Sin(Time.time * 1.4f + index * 1.73f) * 0.12f;

        Vector3 localBottom = new Vector3(
            localCenter.x + Mathf.Cos(angle) * radiusX * (1f + wobble),
            localCenter.y - size.y * 0.5f + lineHeightOffset,
            localCenter.z + Mathf.Sin(angle) * radiusZ * (1f - wobble)
        );

        Vector3 localTop = new Vector3(
            localBottom.x,
            localCenter.y + size.y * 0.5f - lineHeightOffset,
            localBottom.z
        );

        bottom = liftBox.transform.TransformPoint(localBottom);
        top = liftBox.transform.TransformPoint(localTop);
    }

    private void UpdateTopRing()
    {
        if (_topRing == null) return;

        if (!showTopRing || liftBox == null)
        {
            _topRing.enabled = false;
            return;
        }

        _topRing.enabled = true;

        Vector3 center = liftBox.center;
        Vector3 size = liftBox.size;

        float topY = center.y + size.y * 0.5f;
        Vector3 localRingCenter = new Vector3(center.x, topY, center.z);
        Vector3 worldCenter = liftBox.transform.TransformPoint(localRingCenter);

        float baseRadiusX = size.x * 0.48f;
        float baseRadiusZ = size.z * 0.48f;

        float pulse = 1f + Mathf.Sin(Time.time * ringPulseSpeed) * ringPulseAmount;

        int segments = Mathf.Max(12, ringSegments);
        _topRing.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            Vector3 localPoint = new Vector3(
                center.x + Mathf.Cos(angle) * baseRadiusX * pulse,
                topY,
                center.z + Mathf.Sin(angle) * baseRadiusZ * pulse
            );

            Vector3 worldPoint = liftBox.transform.TransformPoint(localPoint);
            _topRing.SetPosition(i, worldPoint);
        }
    }

    private void UpdateExitArrow()
    {
        if (_exitArrow == null) return;

        if (!showExitArrow || liftBox == null)
        {
            _exitArrow.enabled = false;
            return;
        }

        _exitArrow.enabled = true;

        Vector3 center = liftBox.center;
        Vector3 size = liftBox.size;

        float topY = center.y + size.y * 0.5f;
        Vector3 localStart = new Vector3(center.x, topY + 0.25f, center.z);
        Vector3 start = liftBox.transform.TransformPoint(localStart);

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (_player != null && IsPlayerInsideLiftBox())
        {
            forward = _player.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        Vector3 end = start + forward * arrowLength;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 arrowLeft = end - forward * 0.45f + right * 0.28f;
        Vector3 arrowRight = end - forward * 0.45f - right * 0.28f;

        _exitArrow.positionCount = 4;
        _exitArrow.SetPosition(0, start);
        _exitArrow.SetPosition(1, end);
        _exitArrow.SetPosition(2, arrowLeft);
        _exitArrow.SetPosition(3, end);

        // 注意：LineRenderer 不能一笔画出两个箭头边，所以这里画成折线箭头。
        // 如果想更完整，可以后面拆成两个 LineRenderer。
    }

    private bool IsPlayerInsideLiftBox()
    {
        if (_player == null || liftBox == null) return false;

        Vector3 localPoint = liftBox.transform.InverseTransformPoint(_player.position);
        Vector3 center = liftBox.center;
        Vector3 size = liftBox.size * 0.5f;

        size += Vector3.one * activeRadiusCheckPadding;

        Vector3 diff = localPoint - center;

        return Mathf.Abs(diff.x) <= size.x &&
               Mathf.Abs(diff.y) <= size.y &&
               Mathf.Abs(diff.z) <= size.z;
    }

    private void SetLayerToIgnoreRaycast(GameObject obj)
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer >= 0)
        {
            obj.layer = ignoreRaycastLayer;
        }
    }
}
