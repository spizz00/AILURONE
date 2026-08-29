#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Ground Bot 专属死亡表现。
/// 该脚本只控制独立死亡 Prefab，不依赖已被销毁的 Ground Bot。
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundBotDeathEffect : MonoBehaviour
{
    [Header("核心引用")]
    public Transform coreTransform;
    public Renderer coreRenderer;
    public LineRenderer shockRing;
    public Light flashLight;
    public ParticleSystem[] particleSystems;

    [Header("时间")]
    [Min(0.1f)]
    public float lifetime = 0.9f;

    [Header("核心闪光")]
    [Min(0.01f)]
    public float coreStartScale = 0.10f;

    [Min(0.01f)]
    public float corePeakScale = 1.05f;

    public Color coreColor =
        new Color(0.35f, 1.35f, 1.65f, 1f);

    [Header("冲击环")]
    [Min(0f)]
    public float ringStartRadius = 0.12f;

    [Min(0.01f)]
    public float ringEndRadius = 1.55f;

    [Min(0.001f)]
    public float ringWidth = 0.035f;

    [Range(16, 128)]
    public int ringSegments = 64;

    public Color ringColor =
        new Color(0.12f, 1.15f, 1.45f, 1f);

    [Header("动态灯光")]
    [Min(0f)]
    public float peakLightIntensity = 4.5f;

    [Min(0f)]
    public float lightRange = 3.2f;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _propertyBlock;
    private Vector3 _coreBaseScale = Vector3.one;
    private float _elapsed;
    private bool _started;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (coreTransform != null)
        {
            _coreBaseScale = coreTransform.localScale;
        }

        PrepareRing();
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        _started = true;

        if (particleSystems != null)
        {
            foreach (ParticleSystem system in particleSystems)
            {
                if (system == null)
                {
                    continue;
                }

                system.Clear(true);
                system.Play(true);
            }
        }

        ApplyCoreVisual(coreColor, coreStartScale);
        ApplyRingVisual(ringStartRadius, ringColor, 1f);

        if (flashLight != null)
        {
            flashLight.intensity = peakLightIntensity;
            flashLight.range = lightRange;
        }
    }

    private void Update()
    {
        if (!_started)
        {
            return;
        }

        _elapsed += Time.deltaTime;

        float safeLifetime =
            Mathf.Max(0.1f, lifetime);

        float t =
            Mathf.Clamp01(_elapsed / safeLifetime);

        UpdateCore(t);
        UpdateRing(t);
        UpdateLight(t);

        if (_elapsed >= safeLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateCore(float t)
    {
        float expandT =
            Mathf.Clamp01(t / 0.24f);

        float expandEase =
            1f - Mathf.Pow(1f - expandT, 3f);

        float collapseT =
            Mathf.InverseLerp(0.20f, 0.68f, t);

        float scale =
            Mathf.Lerp(
                coreStartScale,
                corePeakScale,
                expandEase
            );

        scale *=
            Mathf.Lerp(
                1f,
                0.12f,
                collapseT * collapseT
            );

        float alpha =
            1f -
            SmoothStep01(
                Mathf.InverseLerp(
                    0.10f,
                    0.58f,
                    t
                )
            );

        Color resolvedColor = coreColor;
        resolvedColor.a = alpha;

        ApplyCoreVisual(
            resolvedColor,
            Mathf.Max(0.001f, scale)
        );
    }

    private void UpdateRing(float t)
    {
        float radiusT =
            1f - Mathf.Pow(1f - t, 3f);

        float radius =
            Mathf.Lerp(
                ringStartRadius,
                ringEndRadius,
                radiusT
            );

        float alpha =
            1f -
            SmoothStep01(
                Mathf.InverseLerp(
                    0.18f,
                    0.88f,
                    t
                )
            );

        Color resolvedColor = ringColor;
        resolvedColor.a = alpha;

        ApplyRingVisual(
            radius,
            resolvedColor,
            alpha
        );
    }

    private void UpdateLight(float t)
    {
        if (flashLight == null)
        {
            return;
        }

        float fade =
            1f -
            SmoothStep01(
                Mathf.InverseLerp(
                    0.02f,
                    0.42f,
                    t
                )
            );

        flashLight.intensity =
            peakLightIntensity * fade;

        flashLight.range =
            Mathf.Lerp(
                lightRange,
                lightRange * 0.45f,
                t
            );
    }

    private void ApplyCoreVisual(
        Color color,
        float scale
    )
    {
        if (coreTransform != null)
        {
            coreTransform.localScale =
                _coreBaseScale * scale;
        }

        if (coreRenderer == null)
        {
            return;
        }

        coreRenderer.GetPropertyBlock(
            _propertyBlock
        );

        _propertyBlock.SetColor(
            BaseColorId,
            color
        );

        _propertyBlock.SetColor(
            ColorId,
            color
        );

        _propertyBlock.SetColor(
            EmissionColorId,
            color
        );

        coreRenderer.SetPropertyBlock(
            _propertyBlock
        );
    }

    private void PrepareRing()
    {
        if (shockRing == null)
        {
            return;
        }

        ringSegments =
            Mathf.Clamp(
                ringSegments,
                16,
                128
            );

        shockRing.useWorldSpace = false;
        shockRing.loop = true;
        shockRing.positionCount = ringSegments;
        shockRing.widthMultiplier = ringWidth;
    }

    private void ApplyRingVisual(
        float radius,
        Color color,
        float alpha
    )
    {
        if (shockRing == null)
        {
            return;
        }

        if (shockRing.positionCount !=
            ringSegments)
        {
            PrepareRing();
        }

        for (int index = 0;
             index < ringSegments;
             index++)
        {
            float angle =
                (float)index /
                ringSegments *
                Mathf.PI *
                2f;

            shockRing.SetPosition(
                index,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                )
            );
        }

        Color resolvedColor = color;
        resolvedColor.a = alpha;

        shockRing.startColor =
            resolvedColor;

        shockRing.endColor =
            resolvedColor;

        shockRing.widthMultiplier =
            ringWidth *
            Mathf.Lerp(
                1f,
                0.25f,
                1f - alpha
            );
    }

    private static float SmoothStep01(
        float value
    )
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lifetime = Mathf.Max(0.1f, lifetime);
        coreStartScale =
            Mathf.Max(0.01f, coreStartScale);
        corePeakScale =
            Mathf.Max(
                coreStartScale,
                corePeakScale
            );
        ringStartRadius =
            Mathf.Max(0f, ringStartRadius);
        ringEndRadius =
            Mathf.Max(
                ringStartRadius + 0.01f,
                ringEndRadius
            );
        ringWidth =
            Mathf.Max(0.001f, ringWidth);
        ringSegments =
            Mathf.Clamp(
                ringSegments,
                16,
                128
            );
        peakLightIntensity =
            Mathf.Max(
                0f,
                peakLightIntensity
            );
        lightRange =
            Mathf.Max(0f, lightRange);
    }
#endif
}
