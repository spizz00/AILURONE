#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GravityCannonVisual : MonoBehaviour
{
    private enum CannonVisualState
    {
        Idle,
        Outer,
        Inner,
        Commit
    }

    [Header("References")]
    public GravityCannon cannon;
    public Transform launchPoint;
    public Transform launchTarget;
    public Transform player;
    public bool autoFindReferences = true;

    [Header("Core Node")]
    public bool showCore = true;
    public float coreWorldSize = 0.38f;
    public Color coreIdleColor = new Color(0.02f, 0.02f, 0.025f, 1f);
    public Color coreActiveTint = new Color(1f, 0.1f, 0.85f, 1f);

    [Header("Segmented Orbit Rings")]
    public bool showSegmentedRings = true;
    public int ringDashCount = 18;
    public int ringDashResolution = 4;
    [Range(0.15f, 0.9f)] public float ringDashFill = 0.52f;

    public float ringRadiusA = 0.75f;
    public float ringRadiusB = 1.05f;
    public float ringRadiusC = 1.35f;

    public float ringWidth = 0.026f;
    public float ringSpeedA = 45f;
    public float ringSpeedB = -32f;
    public float ringSpeedC = 23f;

    [Header("Vertical Data Lines")]
    public bool showVerticalDataLines = true;
    public int verticalLineCount = 8;
    public float verticalLineRadius = 0.72f;
    public float verticalLineHeight = 2.35f;
    public float verticalLineSegmentLength = 0.75f;
    public float verticalLineWidth = 0.018f;
    public float verticalLineScrollSpeed = 1.7f;

    [Header("Pull Volume Readability")]
    public bool showPullVolumes = false;
    public float outerVolumeLineWidth = 0.018f;
    public float innerVolumeLineWidth = 0.026f;
    [Range(0f, 1f)] public float outerVolumeAlpha = 0.34f;
    [Range(0f, 1f)] public float innerVolumeAlpha = 0.52f;
    public float volumePulseSpeed = 2.2f;

    [Header("Trajectory Protocol Line")]
    public bool showTrajectory = true;
    public int trajectoryDashCount = 18;
    public float trajectoryDashFill = 0.45f;
    public float trajectoryWidth = 0.025f;
    public float trajectoryScrollSpeed = 0f;

    [Header("Target Marker")]
    public bool showTargetMarker = true;
    public float targetMarkerSize = 0.7f;
    public float targetMarkerWidth = 0.035f;
    public float targetMarkerHeightOffset = 0.08f;

    [Header("Pull Line")]
    public bool showPullLine = true;
    public int pullDashCount = 7;
    public float pullDashFill = 0.42f;
    public float pullLineWidth = 0.028f;
    public float pullLineScrollSpeed = 2.1f;

    [Header("Protocol Labels")]
    public bool showProtocolLabels = true;
    public string coreLabel = "CORE//LAUNCH";
    public string targetLabel = "TRAJECTORY//SOLVED";
    public float labelWorldScale = 0.035f;
    public float coreLabelHeight = 1.25f;
    public float targetLabelHeight = 0.55f;

    [Header("Style Colors")]
    public Color idleColor = new Color(1f, 1f, 1f, 0.25f);
    public Color outerColor = new Color(0.20f, 1.00f, 1.00f, 0.80f);
    public Color innerColor = new Color(1.00f, 0.20f, 0.85f, 0.95f);
    public Color commitColor = new Color(1.00f, 0.05f, 0.20f, 1.00f);
    public Color targetColor = new Color(1.00f, 0.92f, 0.25f, 0.88f);
    public Color labelColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("Behaviour")]
    public bool drawOnlyWhenPlayerInVolume = false;
    public bool cleanLegacyVisualChildrenOnAwake = true;

    private Material _lineMaterial;
    private Material _coreMaterial;
    private Material _labelMaterial;

    private Transform _root;
    private Transform _ringRootA;
    private Transform _ringRootB;
    private Transform _ringRootC;
    private Transform _verticalRoot;
    private Transform _outerVolumeRoot;
    private Transform _innerVolumeRoot;
    private Transform _trajectoryRoot;
    private Transform _pullRoot;
    private Transform _targetRoot;
    private Transform _labelRoot;

    private GameObject _coreSphere;

    private LineRenderer[] _ringA;
    private LineRenderer[] _ringB;
    private LineRenderer[] _ringC;
    private LineRenderer[] _verticalLines;
    private LineRenderer[] _outerVolumeEdges;
    private LineRenderer[] _innerVolumeEdges;
    private LineRenderer[] _trajectoryDashes;
    private LineRenderer[] _pullDashes;
    private LineRenderer[] _targetBrackets;

    private TextMesh _coreText;
    private TextMesh _targetText;

    private bool _isSetup;

    private void Awake()
    {
        if (cleanLegacyVisualChildrenOnAwake)
        {
            CleanLegacyChildren();
        }

        EnsureSetup();
    }

    private void OnEnable()
    {
        EnsureSetup();
    }

    private void Update()
    {
        EnsureSetup();
        AutoFindMissingReferences();
        UpdateVisuals();
    }

    private void EnsureSetup()
    {
        if (_isSetup) return;

        AutoFindMissingReferences();
        CreateMaterials();

        _root = GetOrCreateChild(transform, "__CICADAMATA_CannonVisual");
        _ringRootA = GetOrCreateChild(_root, "SegmentedRing_A");
        _ringRootB = GetOrCreateChild(_root, "SegmentedRing_B");
        _ringRootC = GetOrCreateChild(_root, "SegmentedRing_C");
        _verticalRoot = GetOrCreateChild(_root, "VerticalDataLines");
        _outerVolumeRoot = GetOrCreateChild(_root, "OuterPullVolume");
        _innerVolumeRoot = GetOrCreateChild(_root, "InnerPullVolume");
        _trajectoryRoot = GetOrCreateChild(_root, "TrajectoryDashes");
        _pullRoot = GetOrCreateChild(_root, "PullDashes");
        _targetRoot = GetOrCreateChild(_root, "TargetMarker");
        _labelRoot = GetOrCreateChild(_root, "ProtocolLabels");

        _coreSphere = CreateCoreSphere();

        _ringA = CreateLineArray(_ringRootA, "A_Dash_", Mathf.Max(1, ringDashCount));
        _ringB = CreateLineArray(_ringRootB, "B_Dash_", Mathf.Max(1, ringDashCount));
        _ringC = CreateLineArray(_ringRootC, "C_Dash_", Mathf.Max(1, ringDashCount));

        _verticalLines = CreateLineArray(_verticalRoot, "DataLine_", Mathf.Max(1, verticalLineCount));
        _outerVolumeEdges = CreateLineArray(_outerVolumeRoot, "OuterEdge_", 12);
        _innerVolumeEdges = CreateLineArray(_innerVolumeRoot, "InnerEdge_", 12);
        _trajectoryDashes = CreateLineArray(_trajectoryRoot, "TrajectoryDash_", Mathf.Max(1, trajectoryDashCount));
        _pullDashes = CreateLineArray(_pullRoot, "PullDash_", Mathf.Max(1, pullDashCount));
        _targetBrackets = CreateLineArray(_targetRoot, "TargetBracket_", 4);

        _coreText = CreateTextMesh(_labelRoot, "CoreLabel", coreLabel);
        _targetText = CreateTextMesh(_labelRoot, "TargetLabel", targetLabel);

        _isSetup = true;
    }

    private void AutoFindMissingReferences()
    {
        if (!autoFindReferences) return;

        if (cannon == null)
        {
            cannon = GetComponent<GravityCannon>();

            if (cannon == null)
            {
                cannon = GetComponentInParent<GravityCannon>();
            }
        }

        if (cannon != null)
        {
            if (launchPoint == null)
            {
                launchPoint = cannon.launchPoint;
            }

            if (launchTarget == null)
            {
                launchTarget = cannon.launchTarget;
            }
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }

    private void UpdateVisuals()
    {
        if (launchPoint == null)
        {
            SetAllVisible(false);
            return;
        }

        float time = Time.time;

        CannonVisualState state = GetCurrentState();
        bool active = state != CannonVisualState.Idle;
        bool visible = !drawOnlyWhenPlayerInVolume || active;

        Color stateColor = GetStateColor(state);
        float statePower = GetStatePower(state);
        float pulse = 1f + Mathf.Sin(time * 4f) * Mathf.Lerp(0.025f, 0.12f, statePower);

        UpdateCore(visible, stateColor, statePower, pulse);
        UpdateSegmentedRings(visible, stateColor, pulse, time);
        UpdateVerticalLines(visible, stateColor, time);
        UpdatePullVolumes(visible, state, time);
        UpdateTrajectory(visible, active, stateColor, time);
        UpdateTargetMarker(visible, active, stateColor);
        UpdatePullLine(active, stateColor, time);
        UpdateLabels(visible, active, stateColor);
    }

    private void UpdateCore(bool visible, Color stateColor, float statePower, float pulse)
    {
        if (_coreSphere == null) return;

        _coreSphere.SetActive(visible && showCore);

        if (!visible || !showCore) return;

        _coreSphere.transform.position = launchPoint.position;
        _coreSphere.transform.rotation = Quaternion.identity;

        float finalSize = coreWorldSize * Mathf.Lerp(1f, 1.22f, statePower) * pulse;
        SetWorldUniformScale(_coreSphere.transform, finalSize);

        Color coreColor = Color.Lerp(coreIdleColor, coreActiveTint, statePower * 0.75f);
        coreColor = Color.Lerp(coreColor, stateColor, statePower * 0.25f);
        coreColor.a = 1f;

        ApplyMaterialColor(_coreMaterial, coreColor);
    }

    private void UpdateSegmentedRings(bool visible, Color color, float pulse, float time)
    {
        bool show = visible && showSegmentedRings;

        SetLineArrayVisible(_ringA, show);
        SetLineArrayVisible(_ringB, show);
        SetLineArrayVisible(_ringC, show);

        if (!show) return;

        UpdateRing(_ringA, ringRadiusA * pulse, ringWidth, color, Quaternion.Euler(0f, 0f, time * ringSpeedA));
        UpdateRing(_ringB, ringRadiusB * pulse, ringWidth * 0.82f, color, Quaternion.Euler(82f, time * ringSpeedB, 0f));
        UpdateRing(_ringC, ringRadiusC * pulse, ringWidth * 0.68f, color, Quaternion.Euler(time * ringSpeedC, 0f, 74f));
    }

    private void UpdateRing(LineRenderer[] lines, float radius, float width, Color color, Quaternion rotation)
    {
        if (lines == null) return;

        int count = lines.Length;
        int points = Mathf.Max(2, ringDashResolution);
        float cellAngle = Mathf.PI * 2f / count;
        float halfDashAngle = cellAngle * Mathf.Clamp01(ringDashFill) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            LineRenderer line = lines[i];
            if (line == null) continue;

            line.positionCount = points;
            ApplyLineStyle(line, color, width);

            float midAngle = i * cellAngle;

            for (int p = 0; p < points; p++)
            {
                float t = points == 1 ? 0f : p / (float)(points - 1);
                float angle = Mathf.Lerp(midAngle - halfDashAngle, midAngle + halfDashAngle, t);

                Vector3 localPoint = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                );

                Vector3 worldPoint = launchPoint.position + rotation * localPoint;
                line.SetPosition(p, worldPoint);
            }
        }
    }

    private void UpdateVerticalLines(bool visible, Color color, float time)
    {
        bool show = visible && showVerticalDataLines;

        SetLineArrayVisible(_verticalLines, show);

        if (!show || _verticalLines == null) return;

        float halfHeight = verticalLineHeight * 0.5f;
        float segmentLength = Mathf.Clamp(verticalLineSegmentLength, 0.1f, verticalLineHeight);

        for (int i = 0; i < _verticalLines.Length; i++)
        {
            LineRenderer line = _verticalLines[i];
            if (line == null) continue;

            float angle = i / (float)_verticalLines.Length * Mathf.PI * 2f;
            float offset = Mathf.Repeat(time * verticalLineScrollSpeed + i * 0.173f, 1f);

            float y0 = Mathf.Lerp(-halfHeight, halfHeight - segmentLength, offset);
            float y1 = y0 + segmentLength;

            float wobble = Mathf.Sin(time * 1.7f + i * 2.31f) * 0.035f;
            float radius = verticalLineRadius + wobble;

            Vector3 localA = new Vector3(Mathf.Cos(angle) * radius, y0, Mathf.Sin(angle) * radius);
            Vector3 localB = new Vector3(Mathf.Cos(angle) * radius, y1, Mathf.Sin(angle) * radius);

            line.positionCount = 2;
            ApplyLineStyle(line, color, verticalLineWidth);
            line.SetPosition(0, launchPoint.position + localA);
            line.SetPosition(1, launchPoint.position + localB);
        }
    }

    private void UpdatePullVolumes(
        bool visible,
        CannonVisualState state,
        float time)
    {
        bool show =
            visible &&
            showPullVolumes &&
            cannon != null &&
            cannon.outerPullBox != null;

        SetLineArrayVisible(_outerVolumeEdges, show);
        SetLineArrayVisible(_innerVolumeEdges, show);

        if (!show)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(time * volumePulseSpeed);

        Color outer = outerColor;
        outer.a = outerVolumeAlpha *
            (state == CannonVisualState.Outer
                ? Mathf.Lerp(0.85f, 1.2f, pulse)
                : 0.72f);

        Color inner = innerColor;
        inner.a = innerVolumeAlpha *
            (state == CannonVisualState.Inner ||
             state == CannonVisualState.Commit
                ? Mathf.Lerp(0.82f, 1.18f, pulse)
                : 0.68f);

        UpdateBoxEdges(
            _outerVolumeEdges,
            cannon.outerPullBox,
            Vector3.one,
            Vector3.zero,
            outer,
            outerVolumeLineWidth);

        UpdateBoxEdges(
            _innerVolumeEdges,
            cannon.outerPullBox,
            cannon.innerBoxScale,
            cannon.innerBoxLocalOffset,
            inner,
            innerVolumeLineWidth);
    }

    private void UpdateBoxEdges(
        LineRenderer[] edges,
        BoxCollider box,
        Vector3 sizeScale,
        Vector3 localCenterOffset,
        Color color,
        float width)
    {
        if (edges == null || edges.Length < 12 || box == null)
        {
            return;
        }

        Vector3 center = box.center + localCenterOffset;
        Vector3 halfSize = Vector3.Scale(
            box.size * 0.5f,
            new Vector3(
                Mathf.Abs(sizeScale.x),
                Mathf.Abs(sizeScale.y),
                Mathf.Abs(sizeScale.z)));

        Vector3[] localCorners =
        {
            center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x, -halfSize.y,  halfSize.z),
            center + new Vector3(-halfSize.x, -halfSize.y,  halfSize.z),
            center + new Vector3(-halfSize.x,  halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x,  halfSize.y, -halfSize.z),
            center + new Vector3( halfSize.x,  halfSize.y,  halfSize.z),
            center + new Vector3(-halfSize.x,  halfSize.y,  halfSize.z)
        };

        int[] edgePairs =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        for (int index = 0; index < 12; index++)
        {
            LineRenderer line = edges[index];
            if (line == null)
            {
                continue;
            }

            line.positionCount = 2;
            ApplyLineStyle(line, color, width);
            line.SetPosition(
                0,
                box.transform.TransformPoint(
                    localCorners[edgePairs[index * 2]]));
            line.SetPosition(
                1,
                box.transform.TransformPoint(
                    localCorners[edgePairs[index * 2 + 1]]));
        }
    }

    private void UpdateTrajectory(
        bool visible,
        bool active,
        Color stateColor,
        float time)
    {
        bool show = visible && showTrajectory && launchPoint != null && launchTarget != null;

        SetLineArrayVisible(_trajectoryDashes, show);

        if (!show || _trajectoryDashes == null) return;

        Color color = active ? Color.Lerp(stateColor, targetColor, 0.45f) : targetColor;
        color.a = active ? 0.9f : 0.45f;

        int count = _trajectoryDashes.Length;
        float dashFill = Mathf.Clamp01(trajectoryDashFill);
        float scroll = Mathf.Repeat(
            time * Mathf.Max(0f, trajectoryScrollSpeed),
            1f);

        for (int i = 0; i < count; i++)
        {
            LineRenderer line = _trajectoryDashes[i];
            if (line == null) continue;

            float cell = 1f / count;
            float t0 = Mathf.Repeat(i * cell + scroll, 1f);
            float t1 = Mathf.Min(t0 + cell * dashFill, 1f);

            Vector3 p0 = GetTrajectoryPoint(t0);
            Vector3 p1 = GetTrajectoryPoint(t1);

            line.positionCount = 2;
            ApplyLineStyle(line, color, trajectoryWidth);
            line.SetPosition(0, p0);
            line.SetPosition(1, p1);
        }
    }

    private Vector3 GetTrajectoryPoint(float normalizedTime)
    {
        if (launchPoint == null || launchTarget == null)
        {
            return transform.position;
        }

        if (cannon == null)
        {
            Vector3 simple = Vector3.Lerp(launchPoint.position, launchTarget.position, normalizedTime);
            simple += Vector3.up * Mathf.Sin(normalizedTime * Mathf.PI) * 1.4f;
            return simple;
        }

        float gravity = -15f;

        StarterAssets.FirstPersonController fpc = StarterAssets.FirstPersonController.Instance;

        if (fpc != null)
        {
            gravity = fpc.Gravity;
        }

        float flightTime = Mathf.Max(0.1f, cannon.flightTime);
        Vector3 launchVelocity = CalculateLaunchVelocity(
            launchPoint.position,
            launchTarget.position,
            flightTime,
            gravity
        );

        float t = normalizedTime * flightTime;

        return launchPoint.position +
               launchVelocity * t +
               0.5f * Vector3.up * gravity * t * t;
    }

    private void UpdateTargetMarker(bool visible, bool active, Color stateColor)
    {
        bool show = visible && showTargetMarker && launchTarget != null;

        SetLineArrayVisible(_targetBrackets, show);

        if (!show || _targetBrackets == null || _targetBrackets.Length < 4) return;

        Color color = active ? Color.Lerp(stateColor, targetColor, 0.6f) : targetColor;
        color.a = active ? 0.95f : 0.55f;

        Vector3 center = launchTarget.position + Vector3.up * targetMarkerHeightOffset;
        float s = targetMarkerSize;
        float arm = s * 0.42f;

        Vector3 right = Vector3.right;
        Vector3 forward = Vector3.forward;

        SetBracket(_targetBrackets[0],
            center + (-right * s) + (forward * arm),
            center + (-right * s) + (forward * s),
            center + (-right * arm) + (forward * s),
            color);

        SetBracket(_targetBrackets[1],
            center + (right * arm) + (forward * s),
            center + (right * s) + (forward * s),
            center + (right * s) + (forward * arm),
            color);

        SetBracket(_targetBrackets[2],
            center + (right * s) + (-forward * arm),
            center + (right * s) + (-forward * s),
            center + (right * arm) + (-forward * s),
            color);

        SetBracket(_targetBrackets[3],
            center + (-right * arm) + (-forward * s),
            center + (-right * s) + (-forward * s),
            center + (-right * s) + (-forward * arm),
            color);
    }

    private void SetBracket(LineRenderer line, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        if (line == null) return;

        line.positionCount = 3;
        ApplyLineStyle(line, color, targetMarkerWidth);
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.SetPosition(2, c);
    }

    private void UpdatePullLine(bool active, Color stateColor, float time)
    {
        bool show = active && showPullLine && player != null && launchPoint != null;

        SetLineArrayVisible(_pullDashes, show);

        if (!show || _pullDashes == null) return;

        Vector3 start = player.position + Vector3.up * 0.85f;
        Vector3 end = launchPoint.position;

        Color color = stateColor;
        color.a = 0.75f;

        int count = _pullDashes.Length;
        float scroll = Mathf.Repeat(time * pullLineScrollSpeed, 1f / Mathf.Max(1, count));

        for (int i = 0; i < count; i++)
        {
            LineRenderer line = _pullDashes[i];
            if (line == null) continue;

            float cell = 1f / count;
            float t0 = Mathf.Repeat(i * cell + scroll, 1f);
            float t1 = Mathf.Min(t0 + cell * Mathf.Clamp01(pullDashFill), 1f);

            Vector3 p0 = Vector3.Lerp(start, end, t0);
            Vector3 p1 = Vector3.Lerp(start, end, t1);

            line.positionCount = 2;
            ApplyLineStyle(line, color, pullLineWidth);
            line.SetPosition(0, p0);
            line.SetPosition(1, p1);
        }
    }

    private void UpdateLabels(bool visible, bool active, Color stateColor)
    {
        bool show = visible && showProtocolLabels;

        if (_coreText != null)
        {
            _coreText.gameObject.SetActive(show && launchPoint != null);

            if (show && launchPoint != null)
            {
                _coreText.text = active ? "CORE//LOCK" : coreLabel;
                _coreText.color = active ? stateColor : labelColor;
                _coreText.transform.position = launchPoint.position + Vector3.up * coreLabelHeight;
                SetWorldUniformScale(_coreText.transform, labelWorldScale);
                BillboardToCamera(_coreText.transform);
            }
        }

        if (_targetText != null)
        {
            _targetText.gameObject.SetActive(show && launchTarget != null);

            if (show && launchTarget != null)
            {
                _targetText.text = targetLabel;
                _targetText.color = targetColor;
                _targetText.transform.position = launchTarget.position + Vector3.up * targetLabelHeight;
                SetWorldUniformScale(_targetText.transform, labelWorldScale);
                BillboardToCamera(_targetText.transform);
            }
        }
    }

    private CannonVisualState GetCurrentState()
    {
        if (player == null || launchPoint == null)
        {
            return CannonVisualState.Idle;
        }

        if (cannon == null || cannon.outerPullBox == null)
        {
            float distanceFallback = Vector3.Distance(player.position, launchPoint.position);

            if (distanceFallback <= 1f)
            {
                return CannonVisualState.Commit;
            }

            if (distanceFallback <= 4f)
            {
                return CannonVisualState.Inner;
            }

            if (distanceFallback <= 8f)
            {
                return CannonVisualState.Outer;
            }

            return CannonVisualState.Idle;
        }

        bool insideOuter = IsInsideBox(
            cannon.outerPullBox,
            player.position,
            Vector3.one,
            Vector3.zero
        );

        if (!insideOuter)
        {
            return CannonVisualState.Idle;
        }

        float distanceToCore = Vector3.Distance(player.position, launchPoint.position);

        if (distanceToCore <= cannon.commitRadius)
        {
            return CannonVisualState.Commit;
        }

        bool insideInner = IsInsideBox(
            cannon.outerPullBox,
            player.position,
            cannon.innerBoxScale,
            cannon.innerBoxLocalOffset
        );

        if (insideInner)
        {
            return CannonVisualState.Inner;
        }

        return CannonVisualState.Outer;
    }

    private Color GetStateColor(CannonVisualState state)
    {
        switch (state)
        {
            case CannonVisualState.Outer:
                return outerColor;
            case CannonVisualState.Inner:
                return innerColor;
            case CannonVisualState.Commit:
                return commitColor;
            default:
                return idleColor;
        }
    }

    private float GetStatePower(CannonVisualState state)
    {
        switch (state)
        {
            case CannonVisualState.Outer:
                return 0.35f;
            case CannonVisualState.Inner:
                return 0.72f;
            case CannonVisualState.Commit:
                return 1f;
            default:
                return 0f;
        }
    }

    private bool IsInsideBox(
        BoxCollider box,
        Vector3 worldPosition,
        Vector3 sizeScale,
        Vector3 localCenterOffset
    )
    {
        if (box == null) return false;

        Vector3 localPoint = box.transform.InverseTransformPoint(worldPosition);
        Vector3 localCenter = box.center + localCenterOffset;

        Vector3 scaledSize = new Vector3(
            box.size.x * Mathf.Abs(sizeScale.x),
            box.size.y * Mathf.Abs(sizeScale.y),
            box.size.z * Mathf.Abs(sizeScale.z)
        );

        Vector3 halfSize = scaledSize * 0.5f;
        Vector3 difference = localPoint - localCenter;

        return Mathf.Abs(difference.x) <= halfSize.x &&
               Mathf.Abs(difference.y) <= halfSize.y &&
               Mathf.Abs(difference.z) <= halfSize.z;
    }

    private Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float time, float gravity)
    {
        float safeTime = Mathf.Max(0.1f, time);

        Vector3 displacement = target - start;
        Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);
        Vector3 horizontalVelocity = horizontalDisplacement / safeTime;

        float verticalVelocity =
            (displacement.y - 0.5f * gravity * safeTime * safeTime) / safeTime;

        return horizontalVelocity + Vector3.up * verticalVelocity;
    }

    private void CreateMaterials()
    {
        if (_lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _lineMaterial = new Material(shader);
            _lineMaterial.name = "CICADAMATA_Cannon_Line_Runtime";
        }

        if (_coreMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _coreMaterial = new Material(shader);
            _coreMaterial.name = "CICADAMATA_Cannon_Core_Runtime";
        }

        if (_labelMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _labelMaterial = new Material(shader);
            _labelMaterial.name = "CICADAMATA_Cannon_Label_Runtime";
        }
    }

    private GameObject CreateCoreSphere()
    {
        Transform existing = _root.Find("SmallBlackCore");

        GameObject core;

        if (existing != null)
        {
            core = existing.gameObject;
        }
        else
        {
            core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "SmallBlackCore";
            core.transform.SetParent(_root, false);

            Collider collider = core.GetComponent<Collider>();

            if (collider != null)
            {
                Destroy(collider);
            }
        }

        MeshRenderer renderer = core.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = _coreMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        SetLayerToIgnoreRaycast(core);
        return core;
    }

    private LineRenderer[] CreateLineArray(Transform parent, string prefix, int count)
    {
        LineRenderer[] lines = new LineRenderer[count];

        for (int i = 0; i < count; i++)
        {
            Transform child = parent.Find(prefix + i);

            GameObject obj;

            if (child == null)
            {
                obj = new GameObject(prefix + i);
                obj.transform.SetParent(parent, false);
            }
            else
            {
                obj = child.gameObject;
            }

            LineRenderer lr = obj.GetComponent<LineRenderer>();

            if (lr == null)
            {
                lr = obj.AddComponent<LineRenderer>();
            }

            SetupLineRenderer(lr);
            SetLayerToIgnoreRaycast(obj);
            lines[i] = lr;
        }

        return lines;
    }

    private TextMesh CreateTextMesh(Transform parent, string objectName, string text)
    {
        Transform child = parent.Find(objectName);

        GameObject obj;

        if (child == null)
        {
            obj = new GameObject(objectName);
            obj.transform.SetParent(parent, false);
        }
        else
        {
            obj = child.gameObject;
        }

        TextMesh textMesh = obj.GetComponent<TextMesh>();

        if (textMesh == null)
        {
            textMesh = obj.AddComponent<TextMesh>();
        }

        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.characterSize = 1f;
        textMesh.color = labelColor;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = _labelMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        SetLayerToIgnoreRaycast(obj);
        return textMesh;
    }

    private void SetupLineRenderer(LineRenderer lr)
    {
        if (lr == null) return;

        lr.sharedMaterial = _lineMaterial;
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 1;
        lr.numCapVertices = 1;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    private void ApplyLineStyle(LineRenderer line, Color color, float width)
    {
        if (line == null) return;

        line.sharedMaterial = _lineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
    }

    private void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void SetLineArrayVisible(LineRenderer[] lines, bool visible)
    {
        if (lines == null) return;

        foreach (LineRenderer line in lines)
        {
            if (line != null)
            {
                line.enabled = visible;
            }
        }
    }

    private void SetAllVisible(bool visible)
    {
        if (_coreSphere != null)
        {
            _coreSphere.SetActive(visible);
        }

        SetLineArrayVisible(_ringA, visible);
        SetLineArrayVisible(_ringB, visible);
        SetLineArrayVisible(_ringC, visible);
        SetLineArrayVisible(_verticalLines, visible);
        SetLineArrayVisible(_outerVolumeEdges, visible);
        SetLineArrayVisible(_innerVolumeEdges, visible);
        SetLineArrayVisible(_trajectoryDashes, visible);
        SetLineArrayVisible(_pullDashes, visible);
        SetLineArrayVisible(_targetBrackets, visible);

        if (_coreText != null)
        {
            _coreText.gameObject.SetActive(visible);
        }

        if (_targetText != null)
        {
            _targetText.gameObject.SetActive(visible);
        }
    }

    private Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);

        if (child != null)
        {
            return child;
        }

        GameObject obj = new GameObject(childName);
        obj.transform.SetParent(parent, false);
        SetLayerToIgnoreRaycast(obj);
        return obj.transform;
    }

    private void SetLayerToIgnoreRaycast(GameObject obj)
    {
        if (obj == null) return;

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer >= 0)
        {
            obj.layer = ignoreRaycastLayer;
        }
    }

    private void SetWorldUniformScale(Transform target, float worldSize)
    {
        if (target == null) return;

        Vector3 parentScale = Vector3.one;

        if (target.parent != null)
        {
            parentScale = target.parent.lossyScale;
        }

        target.localScale = new Vector3(
            SafeDivide(worldSize, parentScale.x),
            SafeDivide(worldSize, parentScale.y),
            SafeDivide(worldSize, parentScale.z)
        );
    }

    private float SafeDivide(float value, float divisor)
    {
        if (Mathf.Abs(divisor) < 0.0001f)
        {
            return value;
        }

        return value / divisor;
    }

    private void BillboardToCamera(Transform target)
    {
        if (target == null) return;

        Camera cam = Camera.main;

        if (cam == null) return;

        Vector3 direction = target.position - cam.transform.position;

        if (direction.sqrMagnitude < 0.001f) return;

        target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void CleanLegacyChildren()
    {
        string[] legacyNames =
        {
            "GravityCannon_CoreVisual",
            "GravityCannon_Ring_A",
            "GravityCannon_Ring_B",
            "GravityCannon_Ring_C",
            "GravityCannon_TrajectoryLine",
            "GravityCannon_PullLine",
            "__CannonVisuals"
        };

        foreach (string legacyName in legacyNames)
        {
            Transform child = transform.Find(legacyName);

            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
