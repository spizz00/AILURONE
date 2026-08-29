#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Ground Bot 枪口闪光和弹丸命中的短时独立特效。
/// Prefab 被实例化后自动播放，并在 Lifetime 结束后销毁。
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundBotCombatFX : MonoBehaviour
{
    [Header("核心引用")]
    public Transform coreTransform;
    public Renderer coreRenderer;
    public LineRenderer ringRenderer;
    public ParticleSystem[] particleSystems;
    public Light flashLight;

    [Header("时间")]
    [Min(0.03f)]
    public float lifetime = 0.22f;

    [Header("核心闪光")]
    [Min(0.001f)]
    public float coreStartScale = 0.08f;

    [Min(0.001f)]
    public float corePeakScale = 0.42f;

    public Color coreColor =
        new Color(2.2f, 0.18f, 0.035f, 1f);

    [Header("冲击环")]
    [Min(0f)]
    public float ringStartRadius = 0.04f;

    [Min(0f)]
    public float ringEndRadius = 0.42f;

    [Min(0.001f)]
    public float ringStartWidth = 0.035f;

    [Range(12, 96)]
    public int ringSegments = 40;

    public Color ringColor =
        new Color(1.8f, 0.12f, 0.025f, 1f);

    [Header("动态灯光")]
    [Min(0f)]
    public float peakLightIntensity = 2.5f;

    [Min(0f)]
    public float lightRange = 2.2f;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _propertyBlock;
    private Vector3 _coreBaseScale = Vector3.one;
    private float _elapsed;

    private void Awake()
    {
        _propertyBlock =
            new MaterialPropertyBlock();

        if (coreTransform != null)
        {
            _coreBaseScale =
                coreTransform.localScale;
        }

        PrepareRing();
    }

    private void OnEnable()
    {
        _elapsed = 0f;

        ApplyCore(
            coreColor,
            coreStartScale
        );

        ApplyRing(
            ringStartRadius,
            ringStartWidth,
            ringColor
        );

        if (flashLight != null)
        {
            flashLight.intensity =
                peakLightIntensity;

            flashLight.range =
                lightRange;
        }

        if (particleSystems != null)
        {
            foreach (ParticleSystem system
                     in particleSystems)
            {
                if (system == null)
                {
                    continue;
                }

                system.Clear(true);
                system.Play(true);
            }
        }
    }

    private void Update()
    {
        float safeLifetime =
            Mathf.Max(0.03f, lifetime);

        _elapsed += Time.deltaTime;

        float t =
            Mathf.Clamp01(
                _elapsed / safeLifetime
            );

        float expand =
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );

        float fade =
            1f -
            SmoothStep01(
                Mathf.InverseLerp(
                    0.10f,
                    1f,
                    t
                )
            );

        float coreScale =
            Mathf.Lerp(
                coreStartScale,
                corePeakScale,
                expand
            ) *
            Mathf.Lerp(
                1f,
                0.08f,
                t
            );

        Color resolvedCore = coreColor;
        resolvedCore.a *= fade;

        ApplyCore(
            resolvedCore,
            Mathf.Max(
                0.001f,
                coreScale
            )
        );

        float radius =
            Mathf.Lerp(
                ringStartRadius,
                ringEndRadius,
                expand
            );

        float width =
            ringStartWidth *
            Mathf.Lerp(
                1f,
                0.12f,
                t
            );

        Color resolvedRing = ringColor;
        resolvedRing.a *= fade;

        ApplyRing(
            radius,
            width,
            resolvedRing
        );

        if (flashLight != null)
        {
            float lightFade =
                1f -
                SmoothStep01(
                    Mathf.InverseLerp(
                        0f,
                        0.65f,
                        t
                    )
                );

            flashLight.intensity =
                peakLightIntensity *
                lightFade;

            flashLight.range =
                Mathf.Lerp(
                    lightRange,
                    lightRange * 0.45f,
                    t
                );
        }

        if (_elapsed >= safeLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void PrepareRing()
    {
        if (ringRenderer == null)
        {
            return;
        }

        ringSegments =
            Mathf.Clamp(
                ringSegments,
                12,
                96
            );

        ringRenderer.useWorldSpace = false;
        ringRenderer.loop = true;
        ringRenderer.positionCount =
            ringSegments;
        ringRenderer.alignment =
            LineAlignment.View;

        for (int index = 0;
             index < ringSegments;
             index++)
        {
            float angle =
                (float)index /
                ringSegments *
                Mathf.PI *
                2f;

            ringRenderer.SetPosition(
                index,
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f
                )
            );
        }
    }

    private void ApplyCore(
        Color color,
        float scale
    )
    {
        if (coreTransform != null)
        {
            coreTransform.localScale =
                _coreBaseScale *
                scale;
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

    private void ApplyRing(
        float radius,
        float width,
        Color color
    )
    {
        if (ringRenderer == null)
        {
            return;
        }

        if (ringRenderer.positionCount !=
            ringSegments)
        {
            PrepareRing();
        }

        ringRenderer.transform.localScale =
            Vector3.one *
            Mathf.Max(0f, radius);

        ringRenderer.widthMultiplier =
            Mathf.Max(0.001f, width);

        ringRenderer.startColor = color;
        ringRenderer.endColor = color;
    }

    private static float SmoothStep01(
        float value
    )
    {
        value = Mathf.Clamp01(value);
        return value *
               value *
               (3f - 2f * value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lifetime =
            Mathf.Max(0.03f, lifetime);

        coreStartScale =
            Mathf.Max(
                0.001f,
                coreStartScale
            );

        corePeakScale =
            Mathf.Max(
                coreStartScale,
                corePeakScale
            );

        ringStartRadius =
            Mathf.Max(
                0f,
                ringStartRadius
            );

        ringEndRadius =
            Mathf.Max(
                ringStartRadius,
                ringEndRadius
            );

        ringStartWidth =
            Mathf.Max(
                0.001f,
                ringStartWidth
            );

        ringSegments =
            Mathf.Clamp(
                ringSegments,
                12,
                96
            );

        peakLightIntensity =
            Mathf.Max(
                0f,
                peakLightIntensity
            );

        lightRange =
            Mathf.Max(
                0f,
                lightRange
            );
    }
#endif
}
