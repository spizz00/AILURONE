#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class EnemyHitMarker : MonoBehaviour
{
    private enum MarkerMode
    {
        None,
        NormalHit,
        DirectKill
    }

    private const int CurrentRecommendedSettingsVersion = 3;

    [Header("核心引用")]
    [Tooltip("当前敌人根节点上的 EnemyTarget。留空时自动查找。")]
    public EnemyTarget enemyTarget;

    [Tooltip("可选。手动指定头顶标记位置；留空时自动计算模型最高点。")]
    public Transform markerAnchor;

    [Tooltip("用于计算模型范围。留空时优先寻找 VisualRoot。")]
    public Transform visualRoot;

    [Tooltip("留空时自动使用 Camera.main。")]
    public Camera targetCamera;

    [Tooltip(
        "亮线材质模板。Spike 和 Ophanim 都建议使用 M_OphanimMarker。" +
        "脚本只会创建运行时副本，不会修改共享材质。"
    )]
    public Material materialTemplate;

    [Header("图形尺寸")]
    [Min(12f)]
    [Tooltip("整个头顶标记的屏幕尺寸，单位近似为像素。")]
    public float markerSizePixels = 42f;

    [Min(0.5f)]
    [Tooltip("上层冰青亮线的屏幕宽度。")]
    public float brightLineWidthPixels = 2.4f;

    [Min(0.75f)]
    [Tooltip("下层深色底线的屏幕宽度，必须大于亮线宽度。")]
    public float darkLineWidthPixels = 4.2f;

    [Min(0f)]
    [Tooltip("标记与敌人模型最高点之间的屏幕间距。")]
    public float headOffsetPixels = 14f;

    [Range(0.12f, 0.4f)]
    public float diamondSizeRatio = 0.24f;

    [Range(0.12f, 0.38f)]
    public float cornerLengthRatio = 0.25f;

    [Header("颜色")]
    [Tooltip("普通命中的冰青亮线颜色。")]
    public Color markerColor =
        new Color(0.58f, 1f, 0.96f, 1f);

    [Tooltip("白色背景上用于维持轮廓清晰度的深蓝黑底线。")]
    public Color darkUnderlayColor =
        new Color(0.012f, 0.035f, 0.06f, 0.96f);

    [Header("动画（真实时间）")]
    [Min(0.03f)]
    public float totalDuration = 0.18f;

    [Min(0f)]
    public float appearDuration = 0.02f;

    [Min(0f)]
    public float holdDuration = 0.07f;

    [Range(1f, 1.5f)]
    public float appearStartScale = 1.15f;

    [Range(0.75f, 1f)]
    public float fadeEndScale = 0.96f;

    [Header("直接击杀标记")]
    [Tooltip("玩家子弹直接击杀时使用的橙红亮线。")]
    public Color directKillColor =
        new Color(1f, 0.18f, 0.055f, 1f);

    [Tooltip("直接击杀标记的深红黑底线。")]
    public Color directKillDarkUnderlayColor =
        new Color(0.075f, 0.006f, 0.012f, 0.98f);

    [Min(0.05f)]
    [Tooltip("直接击杀标记保留的真实时间。")]
    public float directKillDuration = 0.20f;

    [Min(0f)]
    [Tooltip("击杀标记完整显示后，开始向内收紧前的停留时间。")]
    public float directKillHoldDuration = 0.055f;

    [Range(1f, 1.5f)]
    public float directKillStartScale = 1.18f;

    [Range(0.35f, 0.85f)]
    public float directKillEndScale = 0.52f;

    [Header("遮挡")]
    public bool hideOutsideCameraView = true;
    public bool hideWhenOccluded = true;

    [Tooltip("只选择 Environment 等遮挡层，不要选择 Enemy。")]
    public LayerMask occlusionMask;

    [Header("自动定位后备")]
    [Min(0f)]
    public float fallbackHeight = 2f;

    [SerializeField, HideInInspector]
    private int _recommendedSettingsVersion;

    [Header("运行状态")]
    [SerializeField]
    private MarkerMode markerMode =
        MarkerMode.None;

    [SerializeField]
    private bool markerActive;

    [SerializeField]
    private float markerTimer;

    public float DirectKillDuration =>
        Mathf.Max(
            0.05f,
            directKillDuration
        );

    private readonly List<Renderer> _modelRenderers =
        new List<Renderer>();

    private readonly List<LineRenderer> _brightLines =
        new List<LineRenderer>();

    private readonly List<LineRenderer> _darkLines =
        new List<LineRenderer>();

    private Transform _runtimeRoot;

    private Material _runtimeBrightMaterial;
    private Material _runtimeDarkMaterial;

    private bool _subscribed;

    private Vector3 _directKillTopPosition;
    private bool _hasDirectKillTopPosition;

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        UpgradeRecommendedSettingsIfNeeded();
        ResolveReferences();
        CacheModelRenderers();
        CreateRuntimeMarker();
        SetLinesEnabled(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void Start()
    {
        ResolveCamera();
    }

    private void LateUpdate()
    {
        if (!markerActive)
        {
            return;
        }

        ResolveCamera();

        if (targetCamera == null)
        {
            SetLinesEnabled(false);
            return;
        }

        markerTimer += Time.unscaledDeltaTime;

        float activeDuration =
            markerMode == MarkerMode.DirectKill
                ? DirectKillDuration
                : totalDuration;

        if (markerTimer >= activeDuration)
        {
            HideImmediately();
            return;
        }

        UpdateMarker();
    }

    private void OnDisable()
    {
        Unsubscribe();
        HideImmediately();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (_runtimeBrightMaterial != null)
        {
            Destroy(_runtimeBrightMaterial);
        }

        if (_runtimeDarkMaterial != null)
        {
            Destroy(_runtimeDarkMaterial);
        }
    }

    // =========================================================
    // 推荐参数升级
    // =========================================================

    [ContextMenu("Apply Recommended Marker Settings")]
    private void ApplyRecommendedMarkerSettings()
    {
        ApplyRecommendedMarkerSettingsInternal();
        _recommendedSettingsVersion =
            CurrentRecommendedSettingsVersion;
    }

    private void UpgradeRecommendedSettingsIfNeeded()
    {
        if (_recommendedSettingsVersion >=
            CurrentRecommendedSettingsVersion)
        {
            return;
        }

        /*
         * v2 用户可能已经手动调过普通命中参数。
         * 升级到 v3 时只补充击杀标记默认值，不覆盖现有设置。
         */
        if (_recommendedSettingsVersion >= 2)
        {
            ApplyRecommendedDirectKillSettings();
        }
        else
        {
            ApplyRecommendedMarkerSettingsInternal();
        }

        _recommendedSettingsVersion =
            CurrentRecommendedSettingsVersion;
    }

    private void ApplyRecommendedMarkerSettingsInternal()
    {
        markerSizePixels = 42f;
        brightLineWidthPixels = 2.4f;
        darkLineWidthPixels = 4.2f;
        headOffsetPixels = 14f;

        diamondSizeRatio = 0.24f;
        cornerLengthRatio = 0.25f;

        markerColor =
            new Color(0.58f, 1f, 0.96f, 1f);

        darkUnderlayColor =
            new Color(0.012f, 0.035f, 0.06f, 0.96f);

        totalDuration = 0.18f;
        appearDuration = 0.02f;
        holdDuration = 0.07f;

        appearStartScale = 1.15f;
        fadeEndScale = 0.96f;

        ApplyRecommendedDirectKillSettings();
    }

    private void ApplyRecommendedDirectKillSettings()
    {
        directKillColor =
            new Color(1f, 0.18f, 0.055f, 1f);

        directKillDarkUnderlayColor =
            new Color(0.075f, 0.006f, 0.012f, 0.98f);

        directKillDuration = 0.20f;
        directKillHoldDuration = 0.055f;
        directKillStartScale = 1.18f;
        directKillEndScale = 0.52f;
    }

    // =========================================================
    // 命中事件
    // =========================================================

    private void Subscribe()
    {
        if (_subscribed || enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged += HandleDamaged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        if (enemyTarget != null)
        {
            enemyTarget.Damaged -= HandleDamaged;
        }

        _subscribed = false;
    }

    private void HandleDamaged(
        float actualDamage,
        float remainingHealth,
        Vector3 hitPoint
    )
    {
        if (actualDamage <= 0f)
        {
            return;
        }

        // 致命伤害会由 EnemyTarget 在同一帧切换成直接击杀标记。
        if (remainingHealth <= 0f)
        {
            return;
        }

        TriggerNormalHit();
    }

    public void TriggerNormalHit()
    {
        if (markerMode == MarkerMode.DirectKill)
        {
            return;
        }

        markerMode = MarkerMode.NormalHit;
        markerActive = true;
        markerTimer = 0f;
        _hasDirectKillTopPosition = false;

        SetLinesEnabled(true);
        UpdateMarker();
    }

    /// <summary>
    /// 播放玩家子弹直接击杀标记。
    /// 返回敌人需要保留的真实时间；无法显示时返回 0。
    /// </summary>
    public float TriggerDirectKill()
    {
        ResolveCamera();

        if (!isActiveAndEnabled ||
            _runtimeRoot == null ||
            targetCamera == null)
        {
            return 0f;
        }

        _directKillTopPosition =
            GetTopPosition();

        _hasDirectKillTopPosition = true;

        markerMode = MarkerMode.DirectKill;
        markerActive = true;
        markerTimer = 0f;

        SetLinesEnabled(true);
        UpdateMarker();

        return DirectKillDuration;
    }

    /// <summary>
    /// EnemyTarget 用它判断哪些 Renderer 属于运行时标记，
    /// 避免击杀时把标记与敌人模型一起隐藏。
    /// </summary>
    public bool IsRuntimeMarkerRenderer(
        Renderer candidate
    )
    {
        if (candidate == null ||
            _runtimeRoot == null)
        {
            return false;
        }

        Transform candidateTransform =
            candidate.transform;

        return candidateTransform ==
               _runtimeRoot ||
               candidateTransform.IsChildOf(
                   _runtimeRoot
               );
    }

    /// <summary>
    /// 在直接击杀事件触发后，向持久 TP 锚点系统提供
    /// 击杀标记当前的世界位置。
    /// </summary>
    public bool TryGetDirectKillTransitionPosition(
        out Vector3 worldPosition
    )
    {
        worldPosition =
            transform.position +
            Vector3.up * fallbackHeight;

        if (markerMode != MarkerMode.DirectKill ||
            !markerActive)
        {
            return false;
        }

        if (_runtimeRoot != null)
        {
            worldPosition = _runtimeRoot.position;
            return true;
        }

        if (_hasDirectKillTopPosition)
        {
            worldPosition = _directKillTopPosition;
            return true;
        }

        return false;
    }

    public void HideImmediately()
    {
        markerMode = MarkerMode.None;
        markerActive = false;
        markerTimer = 0f;
        _hasDirectKillTopPosition = false;

        SetLinesEnabled(false);
    }

    // =========================================================
    // 引用与自动定位
    // =========================================================

    private void ResolveReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        if (visualRoot == null)
        {
            visualRoot = FindChildRecursive(
                transform,
                "VisualRoot"
            );

            if (visualRoot == null)
            {
                visualRoot = transform;
            }
        }
    }

    private void ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private Transform FindChildRecursive(
        Transform root,
        string targetName
    )
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0;
             i < root.childCount;
             i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == targetName)
            {
                return child;
            }

            Transform found = FindChildRecursive(
                child,
                targetName
            );

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void CacheModelRenderers()
    {
        _modelRenderers.Clear();

        if (visualRoot == null)
        {
            return;
        }

        Renderer[] found =
            visualRoot.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer candidate in found)
        {
            if (candidate == null ||
                candidate is TrailRenderer ||
                candidate is LineRenderer ||
                candidate is ParticleSystemRenderer)
            {
                continue;
            }

            _modelRenderers.Add(candidate);
        }
    }

    private Vector3 GetTopPosition()
    {
        if (markerAnchor != null)
        {
            return markerAnchor.position;
        }

        bool hasBounds = false;
        Bounds bounds = default;

        foreach (Renderer modelRenderer in _modelRenderers)
        {
            if (modelRenderer == null ||
                !modelRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = modelRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(modelRenderer.bounds);
            }
        }

        if (hasBounds)
        {
            return bounds.center +
                   Vector3.up * bounds.extents.y;
        }

        return transform.position +
               Vector3.up * fallbackHeight;
    }

    // =========================================================
    // 创建图形
    // =========================================================

    private void CreateRuntimeMarker()
    {
        if (!CreateRuntimeMaterials())
        {
            enabled = false;
            return;
        }

        GameObject rootObject =
            new GameObject("EnemyHitMarker_Runtime");

        rootObject.transform.SetParent(transform, true);
        _runtimeRoot = rootObject.transform;

        for (int i = 0; i < 5; i++)
        {
            _darkLines.Add(
                CreateLine(
                    $"MarkerDarkLine_{i}",
                    _runtimeDarkMaterial,
                    0
                )
            );

            _brightLines.Add(
                CreateLine(
                    $"MarkerBrightLine_{i}",
                    _runtimeBrightMaterial,
                    1
                )
            );
        }
    }

    private bool CreateRuntimeMaterials()
    {
        Shader fallbackShader =
            Shader.Find("Sprites/Default");

        if (fallbackShader == null)
        {
            Debug.LogError(
                "[EnemyHitMarker] 找不到 Sprites/Default Shader。"
            );

            return false;
        }

        _runtimeBrightMaterial =
            materialTemplate != null
                ? new Material(materialTemplate)
                : new Material(fallbackShader);

        /*
         * 深色底线不能使用 Additive 材质。
         * Additive 在白色背景上无法形成深色对比，
         * 因此固定使用普通透明 Sprites/Default。
         */
        _runtimeDarkMaterial =
            new Material(fallbackShader);

        _runtimeBrightMaterial.name =
            $"{name}_HitMarker_Bright_Runtime";

        _runtimeDarkMaterial.name =
            $"{name}_HitMarker_Dark_Runtime";

        _runtimeBrightMaterial.hideFlags =
            HideFlags.HideAndDontSave;

        _runtimeDarkMaterial.hideFlags =
            HideFlags.HideAndDontSave;

        SetMaterialBaseTint(
            _runtimeBrightMaterial,
            Color.white
        );

        SetMaterialBaseTint(
            _runtimeDarkMaterial,
            Color.white
        );

        _runtimeDarkMaterial.renderQueue = 3100;
        _runtimeBrightMaterial.renderQueue = 3101;

        return true;
    }

    private void SetMaterialBaseTint(
        Material material,
        Color color
    )
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }
    }

    private LineRenderer CreateLine(
        string objectName,
        Material material,
        int sortingOrder
    )
    {
        GameObject lineObject =
            new GameObject(objectName);

        lineObject.transform.SetParent(
            _runtimeRoot,
            false
        );

        LineRenderer line =
            lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = false;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;

        line.shadowCastingMode =
            ShadowCastingMode.Off;

        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;

        line.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;

        line.sharedMaterial = material;
        line.sortingOrder = sortingOrder;
        line.enabled = false;

        return line;
    }

    // =========================================================
    // 每帧表现
    // =========================================================

    private void UpdateMarker()
    {
        if (_runtimeRoot == null ||
            targetCamera == null)
        {
            return;
        }

        Vector3 topPosition =
            markerMode == MarkerMode.DirectKill &&
            _hasDirectKillTopPosition
                ? _directKillTopPosition
                : GetTopPosition();

        float worldPerPixel =
            WorldUnitsPerPixel(topPosition);

        Vector3 markerPosition =
            topPosition +
            targetCamera.transform.up *
            headOffsetPixels *
            worldPerPixel;

        _runtimeRoot.position = markerPosition;
        _runtimeRoot.rotation = targetCamera.transform.rotation;

        if (!IsVisible(markerPosition))
        {
            SetLinesEnabled(false);
            return;
        }

        SetLinesEnabled(true);

        bool isDirectKill =
            markerMode == MarkerMode.DirectKill;

        CalculateAnimation(
            isDirectKill,
            out float alpha,
            out float scale,
            out float appearProgress
        );

        UpdateGeometry(worldPerPixel, scale);

        UpdateAppearance(
            worldPerPixel,
            alpha,
            appearProgress,
            isDirectKill
        );
    }

    private float WorldUnitsPerPixel(
        Vector3 worldPosition
    )
    {
        float screenHeight =
            Mathf.Max(1f, Screen.height);

        if (targetCamera.orthographic)
        {
            return targetCamera.orthographicSize * 2f /
                   screenHeight;
        }

        float distance = Vector3.Distance(
            targetCamera.transform.position,
            worldPosition
        );

        float verticalSize =
            2f * distance *
            Mathf.Tan(
                targetCamera.fieldOfView *
                0.5f *
                Mathf.Deg2Rad
            );

        return verticalSize / screenHeight;
    }

    private bool IsVisible(
        Vector3 markerPosition
    )
    {
        Vector3 cameraToMarker =
            markerPosition -
            targetCamera.transform.position;

        if (Vector3.Dot(
                targetCamera.transform.forward,
                cameraToMarker
            ) <= 0f)
        {
            return false;
        }

        if (hideOutsideCameraView)
        {
            Vector3 viewport =
                targetCamera.WorldToViewportPoint(
                    markerPosition
                );

            if (viewport.z <= 0f ||
                viewport.x < 0f ||
                viewport.x > 1f ||
                viewport.y < 0f ||
                viewport.y > 1f)
            {
                return false;
            }
        }

        if (hideWhenOccluded &&
            occlusionMask.value != 0)
        {
            float distance = cameraToMarker.magnitude;

            if (Physics.Raycast(
                    targetCamera.transform.position,
                    cameraToMarker.normalized,
                    distance,
                    occlusionMask,
                    QueryTriggerInteraction.Ignore
                ))
            {
                return false;
            }
        }

        return true;
    }

    private void CalculateAnimation(
        bool isDirectKill,
        out float alpha,
        out float scale,
        out float appearProgress
    )
    {
        if (isDirectKill)
        {
            CalculateDirectKillAnimation(
                out alpha,
                out scale,
                out appearProgress
            );

            return;
        }

        float appear = Mathf.Clamp(
            appearDuration,
            0f,
            totalDuration
        );

        float hold = Mathf.Clamp(
            holdDuration,
            0f,
            totalDuration - appear
        );

        float holdEnd = appear + hold;

        if (markerTimer < appear &&
            appear > 0f)
        {
            float t = Smooth01(
                markerTimer / appear
            );

            alpha = t;
            scale = Mathf.Lerp(
                appearStartScale,
                1f,
                t
            );

            appearProgress = t;
            return;
        }

        appearProgress = 1f;

        if (markerTimer <= holdEnd)
        {
            alpha = 1f;
            scale = 1f;
            return;
        }

        float fadeDuration = Mathf.Max(
            0.0001f,
            totalDuration - holdEnd
        );

        float fadeT = Smooth01(
            (markerTimer - holdEnd) /
            fadeDuration
        );

        alpha = 1f - fadeT;
        scale = Mathf.Lerp(
            1f,
            fadeEndScale,
            fadeT
        );
    }

    private void CalculateDirectKillAnimation(
        out float alpha,
        out float scale,
        out float appearProgress
    )
    {
        float duration =
            DirectKillDuration;

        float appear =
            Mathf.Min(
                0.025f,
                duration
            );

        float hold =
            Mathf.Clamp(
                directKillHoldDuration,
                0f,
                duration - appear
            );

        float holdEnd =
            appear + hold;

        if (markerTimer < appear &&
            appear > 0f)
        {
            float t =
                Smooth01(
                    markerTimer / appear
                );

            alpha = t;

            scale =
                Mathf.Lerp(
                    directKillStartScale,
                    1f,
                    t
                );

            appearProgress = t;
            return;
        }

        appearProgress = 1f;

        if (markerTimer <= holdEnd)
        {
            alpha = 1f;
            scale = 1f;
            return;
        }

        float collapseDuration =
            Mathf.Max(
                0.0001f,
                duration - holdEnd
            );

        float collapseT =
            Smooth01(
                (markerTimer - holdEnd) /
                collapseDuration
            );

        alpha = 1f - collapseT;

        scale =
            Mathf.Lerp(
                1f,
                directKillEndScale,
                collapseT
            );
    }

    private void UpdateGeometry(
        float worldPerPixel,
        float scale
    )
    {
        float size =
            markerSizePixels *
            worldPerPixel *
            scale;

        float half = size * 0.5f;
        float corner = size * cornerLengthRatio;
        float diamond = size * diamondSizeRatio;

        Vector3[][] shapes =
        {
            new[]
            {
                new Vector3(0f, diamond, 0f),
                new Vector3(diamond, 0f, 0f),
                new Vector3(0f, -diamond, 0f),
                new Vector3(-diamond, 0f, 0f),
                new Vector3(0f, diamond, 0f)
            },
            new[]
            {
                new Vector3(-half, half - corner, 0f),
                new Vector3(-half, half, 0f),
                new Vector3(-half + corner, half, 0f)
            },
            new[]
            {
                new Vector3(half - corner, half, 0f),
                new Vector3(half, half, 0f),
                new Vector3(half, half - corner, 0f)
            },
            new[]
            {
                new Vector3(-half, -half + corner, 0f),
                new Vector3(-half, -half, 0f),
                new Vector3(-half + corner, -half, 0f)
            },
            new[]
            {
                new Vector3(half - corner, -half, 0f),
                new Vector3(half, -half, 0f),
                new Vector3(half, -half + corner, 0f)
            }
        };

        ApplyShapesToLines(
            _darkLines,
            shapes
        );

        ApplyShapesToLines(
            _brightLines,
            shapes
        );
    }

    private void ApplyShapesToLines(
        List<LineRenderer> lines,
        Vector3[][] shapes
    )
    {
        int safeCount = Mathf.Min(
            lines.Count,
            shapes.Length
        );

        for (int i = 0;
             i < safeCount;
             i++)
        {
            LineRenderer line = lines[i];

            if (line == null)
            {
                continue;
            }

            line.positionCount = shapes[i].Length;
            line.SetPositions(shapes[i]);
        }
    }

    private void UpdateAppearance(
        float worldPerPixel,
        float alpha,
        float appearProgress,
        bool isDirectKill
    )
    {
        Color targetBrightColor =
            isDirectKill
                ? directKillColor
                : markerColor;

        Color brightColor =
            Color.Lerp(
                Color.white,
                targetBrightColor,
                Mathf.Clamp01(
                    appearProgress
                )
            );

        brightColor.a *=
            Mathf.Clamp01(alpha);

        Color darkColor =
            isDirectKill
                ? directKillDarkUnderlayColor
                : darkUnderlayColor;

        darkColor.a *=
            Mathf.Clamp01(alpha);

        float brightWidth = Mathf.Max(
            0.0001f,
            brightLineWidthPixels *
            worldPerPixel
        );

        float darkWidth = Mathf.Max(
            brightWidth,
            darkLineWidthPixels *
            worldPerPixel
        );

        ApplyLineAppearance(
            _darkLines,
            darkColor,
            darkWidth
        );

        ApplyLineAppearance(
            _brightLines,
            brightColor,
            brightWidth
        );
    }

    private void ApplyLineAppearance(
        List<LineRenderer> lines,
        Color color,
        float width
    )
    {
        foreach (LineRenderer line in lines)
        {
            if (line == null)
            {
                continue;
            }

            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }
    }

    private void SetLinesEnabled(
        bool enabledState
    )
    {
        SetLineListEnabled(
            _darkLines,
            enabledState
        );

        SetLineListEnabled(
            _brightLines,
            enabledState
        );
    }

    private void SetLineListEnabled(
        List<LineRenderer> lines,
        bool enabledState
    )
    {
        foreach (LineRenderer line in lines)
        {
            if (line != null)
            {
                line.enabled = enabledState;
            }
        }
    }

    private float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);

        return value *
               value *
               (3f - 2f * value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpgradeRecommendedSettingsIfNeeded();

        markerSizePixels =
            Mathf.Max(12f, markerSizePixels);

        brightLineWidthPixels =
            Mathf.Max(0.5f, brightLineWidthPixels);

        darkLineWidthPixels =
            Mathf.Max(
                brightLineWidthPixels + 0.25f,
                darkLineWidthPixels
            );

        headOffsetPixels =
            Mathf.Max(0f, headOffsetPixels);

        totalDuration =
            Mathf.Max(0.03f, totalDuration);

        appearDuration = Mathf.Clamp(
            appearDuration,
            0f,
            totalDuration
        );

        holdDuration = Mathf.Clamp(
            holdDuration,
            0f,
            totalDuration - appearDuration
        );

        directKillDuration =
            Mathf.Max(
                0.05f,
                directKillDuration
            );

        directKillHoldDuration =
            Mathf.Clamp(
                directKillHoldDuration,
                0f,
                directKillDuration
            );

        fallbackHeight =
            Mathf.Max(0f, fallbackHeight);
    }
#endif
}
