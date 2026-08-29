using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Short, surface-aligned geometric feedback for non-lethal Octahedron hits.
/// It never changes damage, health, collision or death behavior.
/// </summary>
[DisallowMultipleComponent]
public sealed class OctahedronHitFeedback : MonoBehaviour
{
    [Header("References")]
    public Transform visualRoot;
    public Material lineMaterial;
    public OctahedronEnemy octahedronEnemy;

    [Header("Timing")]
    [Min(0.05f)]
    public float hipfireDuration = 0.14f;

    [Min(0.08f)]
    public float adsMinimumDuration = 0.20f;

    [Min(0.08f)]
    public float adsMaximumDuration = 0.28f;

    [Header("Geometry")]
    [Min(0.02f)]
    public float hipfireRadius = 0.28f;

    [Min(0.02f)]
    public float adsRadius = 0.48f;

    [Min(0.001f)]
    public float lineWidth = 0.028f;

    [Min(0f)]
    public float surfaceOffset = 0.025f;

    [Header("Color")]
    [ColorUsage(true, true)]
    public Color hipfireColor =
        new Color(0.12f, 1.25f, 1.8f, 1f);

    [ColorUsage(true, true)]
    public Color adsColor =
        new Color(2.1f, 0.12f, 1.35f, 1f);

    private Transform _effectRoot;
    private LineRenderer _innerTriangle;
    private LineRenderer _outerTriangle;
    private readonly LineRenderer[] _facetRays =
        new LineRenderer[3];

    private float _remaining;
    private float _duration;
    private float _strength;
    private float _charge01;
    private bool _firedAsAds;

    private void Awake()
    {
        ResolveReferences();
        EnsureGeometry();
        HideGeometry();
    }

    private void Update()
    {
        if (_remaining <= 0f)
        {
            return;
        }

        _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);

        float progress = _duration > 0.001f
            ? 1f - _remaining / _duration
            : 1f;

        EvaluateGeometry(Mathf.Clamp01(progress));

        if (_remaining <= 0f)
        {
            HideGeometry();
        }
    }

    public void PlayHit(
        Vector3 hitPoint,
        Vector3 hitNormal,
        bool firedAsAds,
        float charge01,
        float strength)
    {
        ResolveReferences();
        EnsureGeometry();

        if (_effectRoot == null || lineMaterial == null)
        {
            return;
        }

        Vector3 normal = hitNormal.sqrMagnitude > 0.0001f
            ? hitNormal.normalized
            : ResolveFallbackNormal(hitPoint);

        Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f
            ? Vector3.forward
            : Vector3.up;

        _effectRoot.SetPositionAndRotation(
            hitPoint + normal * surfaceOffset,
            Quaternion.LookRotation(normal, up));

        _firedAsAds = firedAsAds;
        _charge01 = Mathf.Clamp01(charge01);
        _strength = Mathf.Clamp(strength, 0.35f, 1.5f);
        _duration = firedAsAds
            ? Mathf.Lerp(
                adsMinimumDuration,
                adsMaximumDuration,
                _charge01)
            : hipfireDuration;
        _remaining = _duration;

        if (octahedronEnemy != null)
        {
            octahedronEnemy.TriggerHitFeedback(
                firedAsAds,
                _charge01,
                _strength);
        }

        SetGeometryVisible(true);
        EvaluateGeometry(0f);
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
        {
            Transform candidate = transform.Find("VisualRoot");
            visualRoot = candidate != null ? candidate : transform;
        }

        if (octahedronEnemy == null)
        {
            octahedronEnemy = GetComponent<OctahedronEnemy>();
        }
    }

    private void EnsureGeometry()
    {
        if (_effectRoot != null || lineMaterial == null)
        {
            return;
        }

        GameObject effectObject =
            new GameObject("OctahedronHitFacetFX");

        effectObject.layer = gameObject.layer;
        _effectRoot = effectObject.transform;
        _effectRoot.SetParent(transform, false);

        _innerTriangle = CreateLine("InnerFacet", true);
        _outerTriangle = CreateLine("OuterFacet", true);

        for (int index = 0; index < _facetRays.Length; index++)
        {
            _facetRays[index] =
                CreateLine($"FacetRay_{index + 1:00}", false);
        }
    }

    private LineRenderer CreateLine(string lineName, bool loop)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(_effectRoot, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = loop ? 3 : 2;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.enabled = false;
        return line;
    }

    private void EvaluateGeometry(float progress)
    {
        float attack = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(0f, 0.12f, progress));
        float release = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(0.30f, 1f, progress));
        float envelope = attack * release;

        float baseRadius = _firedAsAds
            ? adsRadius
            : hipfireRadius;
        float radius =
            baseRadius *
            Mathf.Lerp(0.72f, 1.18f, _strength / 1.5f) *
            Mathf.Lerp(0.32f, 1f, attack);

        Color innerColor = _firedAsAds
            ? Color.Lerp(Color.white, hipfireColor, 0.62f)
            : hipfireColor;

        SetTriangle(
            _innerTriangle,
            radius,
            _firedAsAds ? -8f : 0f);
        SetLineAppearance(
            _innerTriangle,
            innerColor,
            envelope,
            lineWidth * (_firedAsAds ? 1.18f : 1f));

        float outerAttack = _firedAsAds
            ? Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.08f, 0.28f, progress))
            : 0f;
        float outerEnvelope = outerAttack * release;

        if (_outerTriangle != null)
        {
            _outerTriangle.enabled =
                _firedAsAds && outerEnvelope > 0.001f;

            if (_outerTriangle.enabled)
            {
                SetTriangle(
                    _outerTriangle,
                    radius * Mathf.Lerp(0.78f, 1.62f, outerAttack),
                    52f + _charge01 * 18f);
                SetLineAppearance(
                    _outerTriangle,
                    adsColor,
                    outerEnvelope * 0.9f,
                    lineWidth * 0.82f);
            }
        }

        for (int index = 0; index < _facetRays.Length; index++)
        {
            LineRenderer ray = _facetRays[index];

            if (ray == null)
            {
                continue;
            }

            float delay = index * (_firedAsAds ? 0.055f : 0.025f);
            float rayAttack = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(delay, delay + 0.18f, progress));
            float rayEnvelope = rayAttack * release;

            float angle = 90f + index * 120f;
            float angleRadians = angle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(
                Mathf.Cos(angleRadians),
                Mathf.Sin(angleRadians),
                0f);

            float startRadius = radius * 0.34f;
            float endRadius = radius *
                (_firedAsAds
                    ? Mathf.Lerp(0.62f, 1.86f, rayAttack)
                    : Mathf.Lerp(0.58f, 1.24f, rayAttack));

            ray.SetPosition(0, direction * startRadius);
            ray.SetPosition(1, direction * endRadius);
            SetLineAppearance(
                ray,
                _firedAsAds && index > 0
                    ? Color.Lerp(hipfireColor, adsColor, index * 0.45f)
                    : hipfireColor,
                rayEnvelope * (_firedAsAds ? 0.82f : 0.62f),
                lineWidth * 0.62f);
        }
    }

    private static void SetTriangle(
        LineRenderer line,
        float radius,
        float rotationDegrees)
    {
        if (line == null)
        {
            return;
        }

        for (int index = 0; index < 3; index++)
        {
            float angle =
                (rotationDegrees + 90f + index * 120f) *
                Mathf.Deg2Rad;

            line.SetPosition(
                index,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f));
        }
    }

    private static void SetLineAppearance(
        LineRenderer line,
        Color color,
        float alpha,
        float width)
    {
        if (line == null)
        {
            return;
        }

        Color visibleColor = color;
        visibleColor.a = Mathf.Clamp01(alpha);
        line.startColor = visibleColor;
        line.endColor = visibleColor;
        line.widthMultiplier = Mathf.Max(0.001f, width);
        line.enabled = visibleColor.a > 0.001f;
    }

    private Vector3 ResolveFallbackNormal(Vector3 hitPoint)
    {
        Vector3 center = visualRoot != null
            ? visualRoot.position
            : transform.position;
        Vector3 outward = hitPoint - center;
        return outward.sqrMagnitude > 0.0001f
            ? outward.normalized
            : transform.forward;
    }

    private void SetGeometryVisible(bool visible)
    {
        if (_innerTriangle != null)
        {
            _innerTriangle.enabled = visible;
        }

        if (_outerTriangle != null)
        {
            _outerTriangle.enabled = visible && _firedAsAds;
        }

        foreach (LineRenderer ray in _facetRays)
        {
            if (ray != null)
            {
                ray.enabled = visible;
            }
        }
    }

    private void HideGeometry()
    {
        SetGeometryVisible(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hipfireDuration = Mathf.Max(0.05f, hipfireDuration);
        adsMinimumDuration = Mathf.Max(0.08f, adsMinimumDuration);
        adsMaximumDuration = Mathf.Max(
            adsMinimumDuration,
            adsMaximumDuration);
        hipfireRadius = Mathf.Max(0.02f, hipfireRadius);
        adsRadius = Mathf.Max(0.02f, adsRadius);
        lineWidth = Mathf.Max(0.001f, lineWidth);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
    }
#endif
}
