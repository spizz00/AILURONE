#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpikeVisualFeedback : MonoBehaviour
{
    [Header("核心引用")]
    [Tooltip("Enemy_Spike_Root 上的 SpikeEnemy 脚本。")]
    public SpikeEnemy spikeEnemy;

    [Tooltip("Enemy_Spike_Root 上的 EnemyTarget 脚本。")]
    public EnemyTarget enemyTarget;

    [Tooltip("需要改变颜色的模型 Renderer。")]
    public Renderer[] targetRenderers;

    [Tooltip("ChargeTrail 上的 TrailRenderer。")]
    public TrailRenderer chargeTrail;

    [Header("状态颜色")]
    public Color idleColor =
        new Color(0.04f, 0.08f, 1f, 1f);

    public Color trackingColor =
        new Color(0.02f, 0.45f, 1f, 1f);

    public Color windupColor =
        new Color(1f, 0.01f, 0.35f, 1f);

    public Color chargingColor =
        new Color(1f, 0.01f, 0.6f, 1f);

    public Color stunnedColor =
        new Color(0.65f, 0.7f, 0.75f, 1f);

    public Color fallingColor =
        new Color(0.55f, 0.02f, 0.25f, 1f);

    [Header("Emission 强度")]
    [Min(0f)]
    public float idleEmission = 0.15f;

    [Min(0f)]
    public float trackingEmission = 0.8f;

    [Min(0f)]
    public float windupEmission = 5f;

    [Min(0f)]
    public float chargingEmission = 7f;

    [Min(0f)]
    public float stunnedEmission = 1.5f;

    [Min(0f)]
    public float fallingEmission = 1.5f;

    [Header("过渡与闪烁")]
    [Tooltip("颜色切换速度。")]
    public float transitionSpeed = 10f;

    [Tooltip("蓄力时闪烁速度。")]
    public float windupPulseSpeed = 24f;

    [Range(0f, 1f)]
    public float windupPulseStrength = 0.25f;

    [Tooltip("眩晕时白色闪烁速度。")]
    public float stunnedFlashSpeed = 9f;

    [Header("非致命受击：能量断流")]
    [Tooltip("一次断流的总时长，使用真实时间，不受 Overclock 影响。")]
    [Min(0.05f)]
    public float disruptionDuration = 0.18f;

    [Tooltip("Tracking / Idle 等普通状态下，Emission 最低降到原值的比例。")]
    [Range(0.55f, 1f)]
    public float disruptionDropMultiplier = 0.82f;

    [Tooltip("断流回弹时，Emission 短暂超过原值的比例。")]
    [Range(1f, 1.2f)]
    public float disruptionReboundMultiplier = 1.05f;

    [Tooltip("Windup 状态下断流强度倍率。")]
    [Range(0f, 1f)]
    public float windupDisruptionMultiplier = 0.70f;

    [Tooltip("Charging 冲刺状态下断流强度倍率。保持较低，避免削弱压迫感。")]
    [Range(0f, 1f)]
    public float chargingDisruptionMultiplier = 0.32f;

    [Tooltip("Stunned 状态下断流强度倍率。")]
    [Range(0f, 1f)]
    public float stunnedDisruptionMultiplier = 0.85f;

    [Header("受击外层颜色")]
    [ColorUsage(true, true)]
    [Tooltip("Spike 为蓝色状态时，外层碎片使用偏暖颜色以提高对比。")]
    public Color warmHitAccent =
        new Color(1.20f, 0.82f, 0.38f, 1f);

    [ColorUsage(true, true)]
    [Tooltip("Spike 为洋红 / 红色状态时，外层碎片使用青色以提高对比。")]
    public Color cyanHitAccent =
        new Color(0.08f, 0.95f, 1.25f, 1f);

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _propertyBlock;

    private Color _currentBaseColor;
    private Color _currentEmissionColor;

    private SpikeEnemy.SpikeState _previousState;
    private bool _hasPreviousState;

    private bool _disruptionActive;
    private float _disruptionElapsed;
    private float _disruptionStrength;
    private float _disruptionEmissionMultiplier = 1f;
    private bool _externalVisualOverride;

    private void Awake()
    {
        ResolveReferences();

        if (targetRenderers == null ||
            targetRenderers.Length == 0)
        {
            FindModelRenderers();
        }

        _propertyBlock =
            new MaterialPropertyBlock();

        _currentBaseColor =
            idleColor;

        _currentEmissionColor =
            idleColor * idleEmission;

        if (chargeTrail != null)
        {
            chargeTrail.emitting = false;
            chargeTrail.Clear();
        }
    }

    private void Start()
    {
        ResolveReferences();

        if (spikeEnemy == null)
        {
            Debug.LogError(
                $"[SpikeVisualFeedback] " +
                $"{gameObject.name} 没有找到 SpikeEnemy。"
            );

            enabled = false;
            return;
        }

        HandleStateChanged(
            spikeEnemy.CurrentState
        );

        if (!_externalVisualOverride)
        {
            ApplyMaterialProperties();
        }
    }

    private void Update()
    {
        if (spikeEnemy == null)
        {
            return;
        }

        SpikeEnemy.SpikeState currentState =
            spikeEnemy.CurrentState;

        if (!_hasPreviousState ||
            currentState != _previousState)
        {
            HandleStateChanged(
                currentState
            );
        }

        UpdateVisualColors(
            currentState
        );

        UpdateEnergyDisruption(
            Time.unscaledDeltaTime,
            currentState
        );

        if (!_externalVisualOverride)
        {
            ApplyMaterialProperties();
        }
    }

    /// <summary>
    /// Temporarily lets a dedicated combat-feedback system control the Spike body material.
    /// State colors and charge-trail logic continue updating in the background.
    /// </summary>
    public void SetExternalVisualOverride(
        bool active
    )
    {
        _externalVisualOverride = active;

        if (!active &&
            _propertyBlock != null)
        {
            ApplyMaterialProperties();
        }
    }

    // =========================================================
    // 引用
    // =========================================================

    private void ResolveReferences()
    {
        if (spikeEnemy == null)
        {
            spikeEnemy =
                GetComponent<SpikeEnemy>();
        }

        if (enemyTarget == null)
        {
            enemyTarget =
                GetComponent<EnemyTarget>();
        }

        if (chargeTrail == null)
        {
            chargeTrail =
                GetComponentInChildren<TrailRenderer>(
                    true
                );
        }
    }

    // =========================================================
    // Renderer 自动查找
    // =========================================================

    private void FindModelRenderers()
    {
        Renderer[] allRenderers =
            GetComponentsInChildren<Renderer>(
                true
            );

        List<Renderer> validRenderers =
            new List<Renderer>();

        foreach (Renderer foundRenderer
                 in allRenderers)
        {
            if (foundRenderer == null)
            {
                continue;
            }

            if (foundRenderer is TrailRenderer)
            {
                continue;
            }

            if (foundRenderer is ParticleSystemRenderer)
            {
                continue;
            }

            validRenderers.Add(
                foundRenderer
            );
        }

        targetRenderers =
            validRenderers.ToArray();
    }

    // =========================================================
    // 状态切换
    // =========================================================

    private void HandleStateChanged(
        SpikeEnemy.SpikeState newState
    )
    {
        _previousState =
            newState;

        _hasPreviousState =
            true;

        if (chargeTrail == null)
        {
            return;
        }

        bool shouldEmit =
            newState ==
            SpikeEnemy.SpikeState.Charging;

        if (shouldEmit)
        {
            chargeTrail.Clear();
        }

        chargeTrail.emitting =
            shouldEmit;
    }

    // =========================================================
    // 状态颜色
    // =========================================================

    private void UpdateVisualColors(
        SpikeEnemy.SpikeState state
    )
    {
        Color targetBaseColor =
            idleColor;

        Color targetEmissionColor =
            idleColor * idleEmission;

        switch (state)
        {
            case SpikeEnemy.SpikeState.Idle:
            {
                targetBaseColor =
                    idleColor;

                targetEmissionColor =
                    idleColor *
                    idleEmission;

                break;
            }

            case SpikeEnemy.SpikeState.Tracking:
            {
                float pulse =
                    1f +
                    Mathf.Sin(
                        Time.time * 4f
                    ) *
                    0.08f;

                targetBaseColor =
                    trackingColor;

                targetEmissionColor =
                    trackingColor *
                    trackingEmission *
                    pulse;

                break;
            }

            case SpikeEnemy.SpikeState.Windup:
            {
                float progress =
                    spikeEnemy.WindupProgress;

                targetBaseColor =
                    Color.Lerp(
                        trackingColor,
                        windupColor,
                        progress
                    );

                float pulse =
                    1f +
                    Mathf.Sin(
                        Time.time *
                        windupPulseSpeed
                    ) *
                    windupPulseStrength;

                float emissionIntensity =
                    Mathf.Lerp(
                        trackingEmission,
                        windupEmission,
                        progress
                    );

                targetEmissionColor =
                    windupColor *
                    emissionIntensity *
                    pulse;

                break;
            }

            case SpikeEnemy.SpikeState.Charging:
            {
                float pulse =
                    1f +
                    Mathf.Sin(
                        Time.time * 30f
                    ) *
                    0.12f;

                targetBaseColor =
                    chargingColor;

                targetEmissionColor =
                    chargingColor *
                    chargingEmission *
                    pulse;

                break;
            }

            case SpikeEnemy.SpikeState.Recovering:
            {
                targetBaseColor =
                    Color.Lerp(
                        chargingColor,
                        trackingColor,
                        0.7f
                    );

                targetEmissionColor =
                    trackingColor *
                    trackingEmission;

                break;
            }

            case SpikeEnemy.SpikeState.Stunned:
            {
                float flash =
                    Mathf.PingPong(
                        Time.time *
                        stunnedFlashSpeed,
                        1f
                    );

                targetBaseColor =
                    Color.Lerp(
                        stunnedColor,
                        Color.white,
                        flash
                    );

                targetEmissionColor =
                    Color.white *
                    stunnedEmission *
                    flash;

                break;
            }

            case SpikeEnemy.SpikeState.Falling:
            {
                targetBaseColor =
                    fallingColor;

                targetEmissionColor =
                    fallingColor *
                    fallingEmission;

                break;
            }
        }

        float interpolation =
            1f -
            Mathf.Exp(
                -transitionSpeed *
                Time.deltaTime
            );

        _currentBaseColor =
            Color.Lerp(
                _currentBaseColor,
                targetBaseColor,
                interpolation
            );

        _currentEmissionColor =
            Color.Lerp(
                _currentEmissionColor,
                targetEmissionColor,
                interpolation
            );
    }

    // =========================================================
    // 非致命受击：能量断流
    // =========================================================

    /// <summary>
    /// 由 EnemyHitFXReceiver 在整发射击结算后调用。
    /// 只改变本体状态光，不改变 AI、速度、方向、碰撞或缩放。
    /// </summary>
    public void TriggerEnergyDisruption(
        float strength,
        bool firedAsAds
    )
    {
        float requestedStrength =
            Mathf.Clamp01(
                strength /
                1.25f
            );

        if (firedAsAds)
        {
            requestedStrength =
                Mathf.Clamp01(
                    requestedStrength *
                    1.08f
                );
        }

        // 连续命中刷新节奏并略微增强，但强度始终有上限。
        _disruptionStrength =
            Mathf.Clamp01(
                Mathf.Max(
                    requestedStrength,
                    _disruptionStrength * 0.82f
                )
            );

        _disruptionElapsed = 0f;
        _disruptionActive = true;
    }

    /// <summary>
    /// 根据 Spike 当前状态选择与本体颜色有清晰对比的外层碎片颜色。
    /// 蓝色状态返回暖色；洋红 / 红色状态返回青色。
    /// </summary>
    public Color GetHitAccentColor()
    {
        if (spikeEnemy == null)
        {
            return cyanHitAccent;
        }

        switch (spikeEnemy.CurrentState)
        {
            case SpikeEnemy.SpikeState.Idle:
            case SpikeEnemy.SpikeState.Tracking:
            case SpikeEnemy.SpikeState.Recovering:
            {
                return warmHitAccent;
            }

            case SpikeEnemy.SpikeState.Windup:
            case SpikeEnemy.SpikeState.Charging:
            case SpikeEnemy.SpikeState.Stunned:
            case SpikeEnemy.SpikeState.Falling:
            default:
            {
                return cyanHitAccent;
            }
        }
    }

    private void UpdateEnergyDisruption(
        float unscaledDeltaTime,
        SpikeEnemy.SpikeState state
    )
    {
        if (!_disruptionActive)
        {
            _disruptionEmissionMultiplier = 1f;
            return;
        }

        _disruptionElapsed +=
            Mathf.Max(
                0f,
                unscaledDeltaTime
            );

        float duration =
            Mathf.Max(
                0.05f,
                disruptionDuration
            );

        float normalizedTime =
            Mathf.Clamp01(
                _disruptionElapsed /
                duration
            );

        float stateMultiplier =
            ResolveStateDisruptionMultiplier(
                state
            );

        float effectiveStrength =
            Mathf.Clamp01(
                _disruptionStrength *
                stateMultiplier
            );

        float rawMultiplier =
            EvaluateDisruptionCurve(
                normalizedTime
            );

        _disruptionEmissionMultiplier =
            Mathf.Lerp(
                1f,
                rawMultiplier,
                effectiveStrength
            );

        if (_disruptionElapsed >= duration)
        {
            ResetEnergyDisruption();
        }
    }

    private float EvaluateDisruptionCurve(
        float normalizedTime
    )
    {
        const float dropEnd = 0.20f;
        const float reboundEnd = 0.52f;

        if (normalizedTime <= dropEnd)
        {
            float progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime /
                    dropEnd
                );

            return Mathf.Lerp(
                1f,
                disruptionDropMultiplier,
                progress
            );
        }

        if (normalizedTime <= reboundEnd)
        {
            float progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        dropEnd,
                        reboundEnd,
                        normalizedTime
                    )
                );

            return Mathf.Lerp(
                disruptionDropMultiplier,
                disruptionReboundMultiplier,
                progress
            );
        }

        float settleProgress =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    reboundEnd,
                    1f,
                    normalizedTime
                )
            );

        return Mathf.Lerp(
            disruptionReboundMultiplier,
            1f,
            settleProgress
        );
    }

    private float ResolveStateDisruptionMultiplier(
        SpikeEnemy.SpikeState state
    )
    {
        switch (state)
        {
            case SpikeEnemy.SpikeState.Windup:
                return windupDisruptionMultiplier;

            case SpikeEnemy.SpikeState.Charging:
                return chargingDisruptionMultiplier;

            case SpikeEnemy.SpikeState.Stunned:
                return stunnedDisruptionMultiplier;

            default:
                return 1f;
        }
    }

    private void ResetEnergyDisruption()
    {
        _disruptionActive = false;
        _disruptionElapsed = 0f;
        _disruptionStrength = 0f;
        _disruptionEmissionMultiplier = 1f;
    }

    // =========================================================
    // 材质属性
    // =========================================================

    private void ApplyMaterialProperties()
    {
        if (targetRenderers == null)
        {
            return;
        }

        Color displayedBaseColor =
            _currentBaseColor;

        Color displayedEmissionColor =
            _currentEmissionColor *
            _disruptionEmissionMultiplier;

        foreach (Renderer targetRenderer
                 in targetRenderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            _propertyBlock.Clear();

            targetRenderer.GetPropertyBlock(
                _propertyBlock
            );

            _propertyBlock.SetColor(
                BaseColorId,
                displayedBaseColor
            );

            _propertyBlock.SetColor(
                ColorId,
                displayedBaseColor
            );

            _propertyBlock.SetColor(
                EmissionColorId,
                displayedEmissionColor
            );

            targetRenderer.SetPropertyBlock(
                _propertyBlock
            );
        }
    }

    private void OnDisable()
    {
        ResetEnergyDisruption();

        if (chargeTrail != null)
        {
            chargeTrail.emitting = false;
            chargeTrail.Clear();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        transitionSpeed =
            Mathf.Max(
                0f,
                transitionSpeed
            );

        disruptionDuration =
            Mathf.Max(
                0.05f,
                disruptionDuration
            );

        disruptionDropMultiplier =
            Mathf.Clamp(
                disruptionDropMultiplier,
                0.55f,
                1f
            );

        disruptionReboundMultiplier =
            Mathf.Clamp(
                disruptionReboundMultiplier,
                1f,
                1.2f
            );

        windupDisruptionMultiplier =
            Mathf.Clamp01(
                windupDisruptionMultiplier
            );

        chargingDisruptionMultiplier =
            Mathf.Clamp01(
                chargingDisruptionMultiplier
            );

        stunnedDisruptionMultiplier =
            Mathf.Clamp01(
                stunnedDisruptionMultiplier
            );
    }
#endif
}
