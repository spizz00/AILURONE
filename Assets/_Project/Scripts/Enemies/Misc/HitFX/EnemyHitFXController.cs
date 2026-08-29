#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一次敌人受击特效的运行参数。
/// 不包含伤害或 AI 逻辑，只描述视觉层。
/// </summary>
public readonly struct EnemyHitFXRequest
{
    public readonly Transform FollowTarget;
    public readonly Vector3 WorldPoint;
    public readonly Vector3 WorldNormal;
    public readonly Color AccentColor;
    public readonly bool FiredAsAds;
    public readonly bool Killed;
    public readonly float Strength;

    public EnemyHitFXRequest(
        Transform followTarget,
        Vector3 worldPoint,
        Vector3 worldNormal,
        Color accentColor,
        bool firedAsAds,
        bool killed,
        float strength
    )
    {
        FollowTarget = followTarget;
        WorldPoint = worldPoint;
        WorldNormal = worldNormal;
        AccentColor = accentColor;
        FiredAsAds = firedAsAds;
        Killed = killed;
        Strength = Mathf.Clamp(strength, 0.35f, 1.5f);
    }
}

/// <summary>
/// 通用敌人受击特效控制器。
/// 由 EnemyHitFX_Base.prefab 使用，可通过 Prefab Variant 调整不同敌人的风格。
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitFXController : MonoBehaviour
{
    [Header("核心引用")]
    public Transform orientationRoot;
    public MeshRenderer impactCoreRenderer;
    public MeshRenderer surfaceScanRenderer;
    public ParticleSystem digitalShards;
    public ParticleSystem warmSparks;

    [Header("通用颜色")]
    [ColorUsage(true, true)]
    public Color warmCoreColor =
        new Color(1.00f, 0.76f, 0.34f, 1f);

    [ColorUsage(true, true)]
    public Color warmSparkColor =
        new Color(1.00f, 0.66f, 0.28f, 1f);

    [Header("腰射")]
    [Min(0.05f)]
    public float hipfireDuration = 0.32f;

    [Min(0.01f)]
    public float hipfireCoreStartSize = 0.12f;

    [Min(0.01f)]
    public float hipfireCorePeakSize = 0.52f;

    [Range(1, 12)]
    public int hipfireShardCount = 6;

    [Range(0, 8)]
    public int hipfireSparkCount = 3;

    [Header("ADS 独头弹")]
    [Min(0.05f)]
    public float adsDuration = 0.40f;

    [Min(0.01f)]
    public float adsCoreStartSize = 0.16f;

    [Min(0.01f)]
    public float adsCorePeakSize = 0.82f;

    [Min(0.01f)]
    public float adsScanStartSize = 0.18f;

    [Min(0.01f)]
    public float adsScanPeakSize = 1.28f;

    [Range(1, 18)]
    public int adsShardCount = 10;

    [Range(0, 12)]
    public int adsSparkCount = 5;

    [Header("ADS Presentation Restraint")]
    [Min(0f)]
    [Tooltip("Delays ADS impact graphics so the moving tracer reaches the target first.")]
    public float adsPresentationDelay = 0.045f;

    [Range(0.1f, 1f)]
    public float adsDurationMultiplier = 0.72f;

    [Range(0.1f, 1f)]
    public float adsVisualScaleMultiplier = 0.72f;

    [Range(0.1f, 1f)]
    public float adsBrightnessMultiplier = 0.78f;

    [Range(0f, 1f)]
    public float adsScanAlphaMultiplier = 0.62f;

    [Range(0f, 1f)]
    public float adsParticleCountMultiplier = 0.68f;

    [Header("致命命中（交给死亡 FX 前的短过渡）")]
    [Min(0.05f)]
    public float fatalHipfireDuration = 0.18f;

    [Min(0.05f)]
    public float fatalAdsDuration = 0.22f;

    [Tooltip("致命命中时减少碎片数量，避免与死亡 FX 抢层级。")]
    [Range(0f, 1f)]
    public float fatalParticleMultiplier = 0.45f;

    [Header("空间与朝向")]
    [Tooltip("沿命中法线离开表面的距离，避免 Z-Fighting。")]
    [Min(0f)]
    public float surfaceOffset = 0.060f;

    [Tooltip("命中法线向相机方向混合的比例。0 表示完全贴面，1 表示完全朝向相机。")]
    [Range(0f, 0.6f)]
    public float cameraFacingBias = 0.42f;

    [Tooltip("致命命中不再附着在即将销毁的敌人身上。")]
    public bool detachWhenKilled = true;

    [Header("亮度")]
    [Min(0f)]
    public float hipfireCoreIntensity = 1.90f;

    [Min(0f)]
    public float adsCoreIntensity = 2.30f;

    [Min(0f)]
    public float adsScanIntensity = 1.55f;

    [Header("粒子可读性校准")]
    [Tooltip("腰射数字碎片尺寸倍率。用于把特效从小型敌人尺度提升到 Spike 的体型尺度。")]
    [Min(0.1f)]
    public float hipfireShardSizeMultiplier = 2.60f;

    [Min(0.1f)]
    public float adsShardSizeMultiplier = 3.20f;

    [Min(0.1f)]
    public float hipfireSparkSizeMultiplier = 2.00f;

    [Min(0.1f)]
    public float adsSparkSizeMultiplier = 2.40f;

    [Tooltip("提高碎片和火花的运动速度，让它们不只是一团发光点。")]
    [Min(0.1f)]
    public float hipfireParticleSpeedMultiplier = 1.20f;

    [Min(0.1f)]
    public float adsParticleSpeedMultiplier = 1.35f;

    [Tooltip("腰射也给一个很小的局部冲击脉冲，但仍然弱于 ADS 扫描环。")]
    [Range(0f, 1f)]
    public float hipfireMiniPulseAlpha = 0.34f;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");

    // UnityEngine objects must not be created from MonoBehaviour field initializers.
    // They are initialized lazily from Awake/Play instead.
    private MaterialPropertyBlock _coreBlock;
    private MaterialPropertyBlock _scanBlock;

    private float _digitalShardBaseSizeMultiplier = 1f;
    private float _digitalShardBaseSpeedMultiplier = 1f;
    private float _warmSparkBaseSizeMultiplier = 1f;
    private float _warmSparkBaseSpeedMultiplier = 1f;
    private bool _particleDefaultsCached;

    private Transform _followTarget;
    private Vector3 _localPoint;
    private Vector3 _localNormal;
    private Vector3 _worldPoint;
    private Vector3 _worldNormal;
    private Color _accentColor;
    private bool _firedAsAds;
    private bool _killed;
    private float _strength;
    private float _elapsed;
    private float _duration;
    private float _presentationDelayRemaining;
    private bool _presentationStarted;
    private Camera _camera;

    public float LastPlayRealtime { get; private set; }
    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        EnsurePropertyBlocks();
        CacheParticleDefaults();

        if (orientationRoot == null)
        {
            orientationRoot = transform;
        }

        SetRenderersVisible(false);
        StopParticleSystems();
        gameObject.SetActive(false);
    }

    public void Play(EnemyHitFXRequest request)
    {
        // Covers pooled instances, prefab-stage validation and domain-reload edge cases.
        EnsurePropertyBlocks();
        CacheParticleDefaults();
        gameObject.SetActive(true);

        _firedAsAds = request.FiredAsAds;
        _killed = request.Killed;
        _strength = request.Strength;
        _accentColor = request.AccentColor;
        _worldPoint = request.WorldPoint;
        _worldNormal = ResolveNormal(request.WorldNormal);
        _camera = Camera.main;
        _elapsed = 0f;
        _duration = _killed
            ? (_firedAsAds
                ? fatalAdsDuration
                : fatalHipfireDuration)
            : (_firedAsAds
                ? adsDuration
                : hipfireDuration);

        if (_firedAsAds)
        {
            _duration *=
                Mathf.Clamp(
                    adsDurationMultiplier,
                    0.1f,
                    1f
                );
        }

        _presentationDelayRemaining =
            _firedAsAds
                ? Mathf.Max(
                    0f,
                    adsPresentationDelay
                )
                : 0f;

        _presentationStarted = false;

        _followTarget =
            _killed && detachWhenKilled
                ? null
                : request.FollowTarget;

        if (_followTarget != null)
        {
            _localPoint =
                _followTarget.InverseTransformPoint(
                    _worldPoint
                );

            _localNormal =
                _followTarget.InverseTransformDirection(
                    _worldNormal
                ).normalized;
        }

        LastPlayRealtime = Time.realtimeSinceStartup;
        IsPlaying = true;

        UpdateAttachment();
        ConfigureParticles();
        StopParticleSystems();
        SetRenderersVisible(false);

        if (_presentationDelayRemaining <= 0f)
        {
            BeginPresentation();
        }
    }

    private void BeginPresentation()
    {
        if (_presentationStarted)
        {
            return;
        }

        _presentationStarted = true;
        SetRenderersVisible(true);
        EmitParticles();
        UpdateVisuals(0f);
    }

    public void ForceStop()
    {
        IsPlaying = false;
        _followTarget = null;
        _presentationStarted = false;
        _presentationDelayRemaining = 0f;
        StopParticleSystems();
        SetRenderersVisible(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        UpdateAttachment();

        float deltaTime =
            Mathf.Max(
                0f,
                Time.unscaledDeltaTime
            );

        if (!_presentationStarted)
        {
            _presentationDelayRemaining -= deltaTime;

            if (_presentationDelayRemaining > 0f)
            {
                return;
            }

            BeginPresentation();
        }

        _elapsed += deltaTime;

        float normalizedTime =
            Mathf.Clamp01(
                _elapsed /
                Mathf.Max(0.01f, _duration)
            );

        UpdateVisuals(normalizedTime);

        if (_elapsed >= _duration)
        {
            ForceStop();
        }
    }

    private void UpdateAttachment()
    {
        if (_followTarget != null)
        {
            _worldPoint =
                _followTarget.TransformPoint(
                    _localPoint
                );

            _worldNormal =
                ResolveNormal(
                    _followTarget.TransformDirection(
                        _localNormal
                    )
                );
        }

        Vector3 facingNormal =
            ResolveFacingNormal(
                _worldPoint,
                _worldNormal
            );

        Transform targetRoot =
            orientationRoot != null
                ? orientationRoot
                : transform;

        targetRoot.position =
            _worldPoint +
            _worldNormal * surfaceOffset;

        targetRoot.rotation =
            BuildStableRotation(
                facingNormal
            );
    }

    private Vector3 ResolveFacingNormal(
        Vector3 point,
        Vector3 surfaceNormal
    )
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
        {
            return surfaceNormal;
        }

        Vector3 towardCamera =
            (_camera.transform.position - point)
            .normalized;

        Vector3 blended =
            Vector3.Slerp(
                surfaceNormal,
                towardCamera,
                cameraFacingBias
            );

        return ResolveNormal(blended);
    }

    private static Quaternion BuildStableRotation(
        Vector3 forward
    )
    {
        Vector3 up = Vector3.up;

        if (Mathf.Abs(
                Vector3.Dot(
                    forward,
                    up
                )
            ) > 0.92f)
        {
            up = Vector3.right;
        }

        return Quaternion.LookRotation(
            forward,
            up
        );
    }

    private void UpdateVisuals(float t)
    {
        float coreEnvelope =
            EvaluateImpactEnvelope(t);

        float coreStartSize =
            _firedAsAds
                ? adsCoreStartSize
                : hipfireCoreStartSize;

        float corePeakSize =
            _firedAsAds
                ? adsCorePeakSize
                : hipfireCorePeakSize;

        float coreSize =
            Mathf.Lerp(
                coreStartSize,
                corePeakSize,
                EaseOutCubic(
                    Mathf.Clamp01(t * 4.2f)
                )
            ) *
            _strength *
            (_killed ? 0.90f : 1f) *
            (_firedAsAds
                ? adsVisualScaleMultiplier
                : 1f);

        if (impactCoreRenderer != null)
        {
            impactCoreRenderer.transform.localScale =
                Vector3.one * coreSize;

            float coreIntensity =
                (_firedAsAds
                    ? adsCoreIntensity
                    : hipfireCoreIntensity) *
                Mathf.Lerp(0.9f, 1.08f, _strength - 0.35f) *
                (_firedAsAds
                    ? adsBrightnessMultiplier
                    : 1f);

            SetRendererProperties(
                impactCoreRenderer,
                _coreBlock,
                warmCoreColor,
                coreEnvelope,
                coreIntensity
            );
        }

        if (surfaceScanRenderer != null)
        {
            bool showScan =
                (_firedAsAds && !_killed) ||
                (!_firedAsAds &&
                 !_killed &&
                 hipfireMiniPulseAlpha > 0.001f);

            surfaceScanRenderer.enabled = showScan;

            if (showScan)
            {
                float scanStart = _firedAsAds ? 0.04f : 0.02f;
                float scanWindow = _firedAsAds ? 0.70f : 0.42f;

                float scanProgress =
                    Mathf.Clamp01(
                        (t - scanStart) /
                        Mathf.Max(0.05f, scanWindow)
                    );

                float scanEnvelope =
                    Mathf.Sin(
                        scanProgress * Mathf.PI
                    );

                float startSize = _firedAsAds ? adsScanStartSize : hipfireCoreStartSize * 1.35f;
                float peakSize = _firedAsAds ? adsScanPeakSize : hipfireCorePeakSize * 1.25f;
                float alphaScale =
                    _firedAsAds
                        ? 0.92f * adsScanAlphaMultiplier
                        : hipfireMiniPulseAlpha;

                float intensity =
                    (_firedAsAds
                        ? adsScanIntensity
                        : adsScanIntensity * 0.72f) *
                    (_firedAsAds
                        ? adsBrightnessMultiplier
                        : 1f);

                float scanSize =
                    Mathf.Lerp(
                        startSize,
                        peakSize,
                        EaseOutCubic(scanProgress)
                    ) *
                    Mathf.Lerp(0.94f, 1.14f, _strength - 0.35f) *
                    (_killed ? 0.82f : 1f) *
                    (_firedAsAds
                        ? adsVisualScaleMultiplier
                        : 1f);

                surfaceScanRenderer.transform.localScale =
                    Vector3.one * scanSize;

                SetRendererProperties(
                    surfaceScanRenderer,
                    _scanBlock,
                    _accentColor,
                    scanEnvelope * alphaScale,
                    intensity
                );
            }
        }
    }

    private static float EvaluateImpactEnvelope(float t)
    {
        const float attackEnd = 0.18f;
        const float holdEnd = 0.42f;

        if (t <= attackEnd)
        {
            return Mathf.SmoothStep(
                0f,
                1f,
                t / attackEnd
            );
        }

        if (t <= holdEnd)
        {
            return 1f;
        }

        float release =
            Mathf.InverseLerp(
                holdEnd,
                1f,
                t
            );

        return 1f -
            Mathf.SmoothStep(
                0f,
                1f,
                release
            );
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private void ConfigureParticles()
    {
        bool adsMode = _firedAsAds;

        ConfigureParticleColor(
            digitalShards,
            _accentColor
        );

        ConfigureParticleColor(
            warmSparks,
            warmSparkColor
        );

        ConfigureParticleSizing(
            digitalShards,
            _digitalShardBaseSizeMultiplier,
            _digitalShardBaseSpeedMultiplier,
            adsMode
                ? adsShardSizeMultiplier *
                  adsVisualScaleMultiplier
                : hipfireShardSizeMultiplier,
            adsMode
                ? adsParticleSpeedMultiplier
                : hipfireParticleSpeedMultiplier
        );

        ConfigureParticleSizing(
            warmSparks,
            _warmSparkBaseSizeMultiplier,
            _warmSparkBaseSpeedMultiplier,
            adsMode
                ? adsSparkSizeMultiplier *
                  adsVisualScaleMultiplier
                : hipfireSparkSizeMultiplier,
            adsMode
                ? adsParticleSpeedMultiplier
                : hipfireParticleSpeedMultiplier
        );
    }

    private void CacheParticleDefaults()
    {
        if (_particleDefaultsCached)
        {
            return;
        }

        bool cachedAnyDefaults = false;

        if (digitalShards != null)
        {
            ParticleSystem.MainModule main = digitalShards.main;
            _digitalShardBaseSizeMultiplier = Mathf.Max(0.01f, main.startSizeMultiplier);
            _digitalShardBaseSpeedMultiplier = Mathf.Max(0.01f, main.startSpeedMultiplier);
            cachedAnyDefaults = true;
        }

        if (warmSparks != null)
        {
            ParticleSystem.MainModule main = warmSparks.main;
            _warmSparkBaseSizeMultiplier = Mathf.Max(0.01f, main.startSizeMultiplier);
            _warmSparkBaseSpeedMultiplier = Mathf.Max(0.01f, main.startSpeedMultiplier);
            cachedAnyDefaults = true;
        }

        _particleDefaultsCached =
            cachedAnyDefaults;
    }

    private static void ConfigureParticleSizing(
        ParticleSystem particleSystem,
        float baseSizeMultiplier,
        float baseSpeedMultiplier,
        float readableSizeMultiplier,
        float readableSpeedMultiplier
    )
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particleSystem.main;
        main.startSizeMultiplier = Mathf.Max(0.01f, baseSizeMultiplier * readableSizeMultiplier);
        main.startSpeedMultiplier = Mathf.Max(0.01f, baseSpeedMultiplier * readableSpeedMultiplier);
    }

    private static void ConfigureParticleColor(
        ParticleSystem particleSystem,
        Color color
    )
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main =
            particleSystem.main;

        main.startColor = color;
    }

    private void EmitParticles()
    {
        if (digitalShards != null)
        {
            int shardCount =
                _firedAsAds
                    ? adsShardCount
                    : hipfireShardCount;

            float particleMultiplier =
                _killed
                    ? fatalParticleMultiplier
                    : 1f;

            if (_firedAsAds)
            {
                particleMultiplier *=
                    adsParticleCountMultiplier;
            }

            shardCount =
                Mathf.Max(
                    _killed ? 0 : 1,
                    Mathf.RoundToInt(
                        shardCount *
                        Mathf.Lerp(
                            0.85f,
                            1.15f,
                            _strength - 0.35f
                        ) *
                        particleMultiplier
                    )
                );

            if (shardCount > 0)
            {
                digitalShards.Emit(shardCount);
            }
        }

        if (warmSparks != null)
        {
            int sparkCount =
                _firedAsAds
                    ? adsSparkCount
                    : hipfireSparkCount;

            float particleMultiplier =
                _killed
                    ? fatalParticleMultiplier
                    : 1f;

            if (_firedAsAds)
            {
                particleMultiplier *=
                    adsParticleCountMultiplier;
            }

            sparkCount =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        sparkCount *
                        Mathf.Lerp(
                            0.85f,
                            1.10f,
                            _strength - 0.35f
                        ) *
                        particleMultiplier
                    )
                );

            if (sparkCount > 0)
            {
                warmSparks.Emit(sparkCount);
            }
        }
    }

    private void EnsurePropertyBlocks()
    {
        if (_coreBlock == null)
        {
            _coreBlock = new MaterialPropertyBlock();
        }

        if (_scanBlock == null)
        {
            _scanBlock = new MaterialPropertyBlock();
        }
    }

    private static void SetRendererProperties(
        MeshRenderer renderer,
        MaterialPropertyBlock block,
        Color hdrColor,
        float alpha,
        float intensity
    )
    {
        if (renderer == null || block == null)
        {
            return;
        }

        block.Clear();
        renderer.GetPropertyBlock(block);

        Color displayedColor = hdrColor;
        displayedColor.a = Mathf.Clamp01(alpha);

        block.SetColor(
            BaseColorId,
            displayedColor
        );

        block.SetFloat(
            IntensityId,
            Mathf.Max(0f, intensity)
        );

        renderer.SetPropertyBlock(block);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (impactCoreRenderer != null)
        {
            impactCoreRenderer.enabled = visible;
        }

        if (surfaceScanRenderer != null)
        {
            surfaceScanRenderer.enabled =
                visible && _firedAsAds;
        }
    }

    private void StopParticleSystems()
    {
        StopParticleSystem(digitalShards);
        StopParticleSystem(warmSparks);
    }

    private static void StopParticleSystem(
        ParticleSystem particleSystem
    )
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private static Vector3 ResolveNormal(
        Vector3 normal
    )
    {
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return Vector3.up;
        }

        return normal.normalized;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hipfireDuration = Mathf.Max(0.05f, hipfireDuration);
        adsDuration = Mathf.Max(0.05f, adsDuration);
        fatalHipfireDuration = Mathf.Max(0.05f, fatalHipfireDuration);
        fatalAdsDuration = Mathf.Max(0.05f, fatalAdsDuration);
        fatalParticleMultiplier = Mathf.Clamp01(fatalParticleMultiplier);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        hipfireShardSizeMultiplier = Mathf.Max(0.1f, hipfireShardSizeMultiplier);
        adsShardSizeMultiplier = Mathf.Max(0.1f, adsShardSizeMultiplier);
        hipfireSparkSizeMultiplier = Mathf.Max(0.1f, hipfireSparkSizeMultiplier);
        adsSparkSizeMultiplier = Mathf.Max(0.1f, adsSparkSizeMultiplier);
        hipfireParticleSpeedMultiplier = Mathf.Max(0.1f, hipfireParticleSpeedMultiplier);
        adsParticleSpeedMultiplier = Mathf.Max(0.1f, adsParticleSpeedMultiplier);
        hipfireMiniPulseAlpha = Mathf.Clamp01(hipfireMiniPulseAlpha);
    }
#endif
}

/// <summary>
/// 小型全局对象池。
/// 每个受击 Prefab 最多保留 8 个实例，超过时复用最旧的活动实例。
/// </summary>
public static class EnemyHitFXPool
{
    private const int MaximumInstancesPerPrefab = 8;

    private sealed class Bucket
    {
        public readonly List<EnemyHitFXController> Instances =
            new List<EnemyHitFXController>();
    }

    private static readonly Dictionary<int, Bucket> Buckets =
        new Dictionary<int, Bucket>();

    private static Transform _poolRoot;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStaticState()
    {
        Buckets.Clear();
        _poolRoot = null;
    }

    public static void Play(
        GameObject effectPrefab,
        EnemyHitFXRequest request
    )
    {
        if (effectPrefab == null)
        {
            return;
        }

        EnemyHitFXController controller =
            GetController(effectPrefab);

        if (controller == null)
        {
            Debug.LogWarning(
                $"[EnemyHitFXPool] {effectPrefab.name} 没有 EnemyHitFXController。"
            );

            return;
        }

        controller.Play(request);
    }

    private static EnemyHitFXController GetController(
        GameObject effectPrefab
    )
    {
        EnsurePoolRoot();

        int key = effectPrefab.GetEntityId();

        if (!Buckets.TryGetValue(
                key,
                out Bucket bucket
            ))
        {
            bucket = new Bucket();
            Buckets.Add(key, bucket);
        }

        CleanupMissingInstances(bucket);

        foreach (EnemyHitFXController instance
                 in bucket.Instances)
        {
            if (instance != null &&
                !instance.IsPlaying)
            {
                return instance;
            }
        }

        if (bucket.Instances.Count <
            MaximumInstancesPerPrefab)
        {
            GameObject created =
                Object.Instantiate(
                    effectPrefab,
                    _poolRoot
                );

            created.name =
                effectPrefab.name + "_Pooled";

            EnemyHitFXController controller =
                created.GetComponent<EnemyHitFXController>();

            if (controller != null)
            {
                bucket.Instances.Add(controller);
            }
            else
            {
                Object.Destroy(created);
            }

            return controller;
        }

        EnemyHitFXController oldest = null;

        foreach (EnemyHitFXController instance
                 in bucket.Instances)
        {
            if (instance == null)
            {
                continue;
            }

            if (oldest == null ||
                instance.LastPlayRealtime <
                oldest.LastPlayRealtime)
            {
                oldest = instance;
            }
        }

        oldest?.ForceStop();
        return oldest;
    }

    private static void EnsurePoolRoot()
    {
        if (_poolRoot != null)
        {
            return;
        }

        GameObject rootObject =
            new GameObject(
                "[AILURONE] Enemy Hit FX Pool"
            );

        _poolRoot = rootObject.transform;
        Object.DontDestroyOnLoad(rootObject);
    }

    private static void CleanupMissingInstances(
        Bucket bucket
    )
    {
        for (int index =
                 bucket.Instances.Count - 1;
             index >= 0;
             index--)
        {
            if (bucket.Instances[index] == null)
            {
                bucket.Instances.RemoveAt(index);
            }
        }
    }
}
