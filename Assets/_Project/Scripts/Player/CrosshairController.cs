#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CrosshairController : MonoBehaviour
{
    public static CrosshairController Instance { get; private set; }

    private const int CurrentRecommendedSettingsVersion = 6;
    private const string CanonicalHudCanvasName = "HUD_Canvas_AILURONE";
    private const float CanvasRetryInterval = 0.5f;

    [Header("自动查找")]
    [Tooltip("当前屏幕 UI Canvas。")]
    public Canvas targetCanvas;

    [Tooltip("玩家当前使用的武器。")]
    public PlayerWeapon playerWeapon;

    [Tooltip("玩家主摄像机。")]
    public Camera playerCamera;

    [Header("基础颜色")]
    public Color normalColor =
        new Color(0.88f, 0.98f, 1f, 0.96f);

    public Color hipfireOutlineColor =
        new Color(0.01f, 0.025f, 0.04f, 0.58f);

    public Color arcColor =
        new Color(0.64f, 0.91f, 1f, 0.42f);

    public Color adsColor =
        new Color(0.92f, 1f, 1f, 1f);

    public Color adsOutlineColor =
        new Color(0.015f, 0.035f, 0.05f, 0.8f);

    public Color hitOutlineColor =
        new Color(0.005f, 0.025f, 0.035f, 0.82f);

    public Color normalHitColor =
        new Color(0.12f, 0.92f, 1f, 1f);

    public Color killColor =
        new Color(1f, 0.12f, 0.1f, 1f);

    public Color timeSlowColor =
        new Color(1f, 0.18f, 0.14f, 1f);

    public Color fatalColor =
        new Color(1f, 0.05f, 0.05f, 1f);

    [Header("腰射散弹范围")]
    [Tooltip("根据武器 spreadAngle 计算准星范围后的校准倍率。")]
    [Min(0.1f)]
    public float spreadCalibration = 1f;

    [Min(1f)]
    public float minimumHipfireRadius = 14f;

    [Min(10f)]
    public float maximumHipfireRadius = 90f;

    [Header("腰射空心菱形")]
    [Tooltip("菱形沿着屏幕中心方向的总长度。")]
    [Min(2f)]
    public float hipfireDiamondRadialSize = 8f;

    [Tooltip("菱形横向的总宽度。")]
    [Min(2f)]
    public float hipfireDiamondTangentSize = 6f;

    [Min(0.5f)]
    public float hipfireDiamondThickness = 1.2f;

    [Min(0.25f)]
    public float hipfireOutlineExtraThickness = 1.1f;

    [Header("腰射括号")]
    [Min(0f)]
    public float bracketExtraDistance = 15f;

    [Min(1f)]
    public float bracketHeight = 12f;

    [Min(1f)]
    public float bracketArmLength = 4f;

    [Min(0.5f)]
    public float bracketThickness = 1.15f;

    [Header("腰射四段短弧")]
    [Min(0.5f)]
    public float arcThickness = 1.2f;

    [Range(6f, 25f)]
    public float arcHalfSpanDegrees = 13f;

    [Tooltip("每组短弧中间留下的缺口。")]
    [Range(0.5f, 10f)]
    public float arcCenterGapDegrees = 3.5f;

    [Header("ADS 准星")]
    [Min(1f)]
    public float adsDiamondRadius = 6.25f;

    [Min(0.5f)]
    public float adsLineThickness = 1.55f;

    [Min(0.25f)]
    public float adsOutlineExtraThickness = 1.35f;

    [Tooltip("ADS 四向定位线距离中心的位置。")]
    [Min(1f)]
    public float adsOuterLineStart = 14f;

    [Min(1f)]
    public float adsOuterLineLength = 6.5f;

    [Min(1f)]
    public float adsBlendSpeed = 18f;

    [Header("ADS 过渡层级")]
    [Range(0f, 1f)]
    public float hipfireFadeOutStart = 0.12f;

    [Range(0f, 1f)]
    public float hipfireFadeOutEnd = 0.62f;

    [Range(0f, 1f)]
    public float adsFadeInStart = 0.45f;

    [Range(0f, 1f)]
    public float adsFadeInEnd = 0.92f;

    [Header("开枪反馈")]
    [Min(0f)]
    public float hipfireKickDistance = 9f;

    [Min(0f)]
    public float bracketKickDistance = 3f;

    [Min(0.01f)]
    public float shotKickRecoverSpeed = 12f;

    [Header("ADS 开枪反馈")]
    [Tooltip("ADS 开枪后反馈恢复速度，使用真实时间。")]
    [Min(0.01f)]
    public float adsShotRecoverSpeed = 14f;

    [Range(0.7f, 1f)]
    public float adsShotDiamondScale = 0.88f;

    [Min(0f)]
    public float adsShotLineLengthBoost = 2f;

    [Min(0f)]
    public float adsShotLineThicknessBoost = 0.35f;

    [Range(0f, 1f)]
    public float adsShotBrightnessBoost = 0.3f;

    [Header("中心命中反馈")]
    [Min(0.01f)]
    public float normalHitDuration = 0.13f;

    [Min(0.01f)]
    public float killHitDuration = 0.18f;

    [Min(1f)]
    public float hitMarkerRadius = 10f;

    [Min(1f)]
    public float hitMarkerArmSize = 5f;

    [Min(0.5f)]
    public float hitMarkerThickness = 1.5f;

    [Min(0.25f)]
    public float hitMarkerOutlineExtraThickness = 1.15f;

    [Min(0f)]
    public float normalHitStartOffset = 8f;

    [Tooltip("普通命中时基础准星短暂变淡的真实时间。")]
    [Min(0.01f)]
    public float normalHitBaseFadeDuration = 0.08f;

    [Range(0.1f, 1f)]
    public float normalHitBaseAlpha = 0.58f;

    [Header("兼容现有时间系统")]
    public bool readTimeManagerState = true;

    [Header("Teleport Anchor Lock")]
    public Color teleportLockColor =
        new Color(0.2f, 1f, 0.96f, 0.92f);

    public Color teleportLockOutlineColor =
        new Color(0.005f, 0.04f, 0.06f, 0.88f);

    [Range(10f, 28f)]
    public float teleportLockRadius = 18f;

    [Range(2f, 10f)]
    public float teleportLockArmLength = 5.5f;

    [Range(0.6f, 2.5f)]
    public float teleportLockThickness = 1.25f;

    [Range(0f, 12f)]
    [Tooltip("Maximum screen-space pull. This never rotates or steers the camera.")]
    public float teleportMagnetPixels = 6f;

    [Min(1f)]
    public float teleportLockBlendSpeed = 14f;

    [Range(0.55f, 1f)]
    public float teleportChannelContractRatio = 0.72f;

    [Range(0f, 3f)]
    public float teleportCommitShakePixels = 1.5f;

    [Min(1f)]
    public float teleportRestoreSpeed = 18f;

    [SerializeField, HideInInspector]
    private int _recommendedSettingsVersion;

    public float AdsBlend => _adsBlend;

    private RectTransform _root;
    private RectTransform _adsRoot;
    private RectTransform _teleportLockRoot;

    private CanvasGroup _rootCanvasGroup;

    private CanvasGroup _hipfireGroup;
    private CanvasGroup _adsGroup;
    private CanvasGroup _hitGroup;
    private CanvasGroup _teleportLockGroup;

    private RectTransform[] _hipfireDiamondLines;
    private RectTransform[] _hipfireDiamondOutlineLines;

    private Image[] _hipfireDiamondImages;
    private Image[] _hipfireDiamondOutlineImages;

    private RectTransform[] _arcLines;
    private Image[] _arcImages;

    private RectTransform[] _bracketLines;
    private Image[] _bracketImages;

    private RectTransform[] _adsDiamondLines;
    private RectTransform[] _adsOuterLines;

    private RectTransform[] _adsOutlineDiamondLines;
    private RectTransform[] _adsOutlineOuterLines;

    private Image[] _adsForegroundImages;
    private Image[] _adsOutlineImages;

    private RectTransform[] _hitLines;
    private RectTransform[] _hitOutlineLines;
    private Image[] _hitImages;
    private Image[] _hitOutlineImages;

    private RectTransform[] _teleportLockLines;
    private RectTransform[] _teleportLockOutlineLines;
    private Image[] _teleportLockImages;
    private Image[] _teleportLockOutlineImages;

    private float _adsBlend;
    private float _shotKick;
    private float _adsShotKick;
    private float _baseFadeTimer;

    private float _hitTimer;
    private float _hitDuration;
    private float _hitStrength;
    private bool _hitIsKill;

    private bool _teleportFocusRequested;
    private Vector2 _teleportFocusScreenOffset;
    private float _teleportFocusStrength;
    private float _teleportFocusBlend;
    private float _teleportChannelProgress;
    private bool _teleportChannelActive;
    private float _teleportHiddenRemaining;
    private float _teleportPresentationAlpha = 1f;
    private float _deploymentAlpha = 1f;
    private float _nextCanvasRetryAt;
    private Vector2 _teleportPresentationOffset;

    private void Awake()
    {
        UpgradeRecommendedSettingsIfNeeded();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        TryInitializeVisuals();
    }

    private void LateUpdate()
    {
        if (_root == null)
        {
            if (Time.unscaledTime < _nextCanvasRetryAt)
            {
                return;
            }

            _nextCanvasRetryAt =
                Time.unscaledTime + CanvasRetryInterval;

            if (!TryInitializeVisuals())
            {
                return;
            }
        }

        ResolveReferences();

        if (AILURONEGameplayActionGate.IsPaused)
        {
            ApplyRootAlpha(true);
            return;
        }

        float delta = Time.unscaledDeltaTime;

        UpdateTeleportPresentation(delta);
        UpdateAdsBlend(delta);
        UpdateShotKick(delta);
        UpdateTeleportAnchorFocus(delta);
        UpdateCrosshairLayout();
        UpdateBaseColors();
        UpdateHitMarker(delta);
    }

    // =========================================================
    // 公共接口
    // =========================================================

    public void NotifyShotFired(bool firedAsAds)
    {
        if (firedAsAds)
        {
            _adsShotKick = 1f;
            return;
        }

        _shotKick = 1f;
    }

    public void SetTeleportAnchorFocus(
        bool focused,
        Vector2 screenOffsetPixels,
        float strength)
    {
        _teleportFocusRequested = focused;
        _teleportFocusScreenOffset = screenOffsetPixels;
        _teleportFocusStrength = focused
            ? Mathf.Clamp01(strength)
            : 0f;
    }

    public void SetTeleportChannelProgress(
        float progress
    )
    {
        _teleportChannelActive = true;
        _teleportChannelProgress =
            Mathf.Clamp01(progress);
    }

    public void BeginTeleportCommitPulse(
        float hiddenDuration
    )
    {
        _teleportChannelActive = false;
        _teleportChannelProgress = 1f;
        _teleportHiddenRemaining =
            Mathf.Max(
                _teleportHiddenRemaining,
                hiddenDuration
            );
        _teleportPresentationAlpha = 0f;
    }

    public void BeginTeleportArrivalRecovery()
    {
        _teleportChannelActive = false;
        _teleportChannelProgress = 0f;
    }

    public void CancelTeleportPresentation()
    {
        _teleportChannelActive = false;
        _teleportChannelProgress = 0f;
        _teleportHiddenRemaining = 0f;
        _teleportPresentationOffset = Vector2.zero;
    }

    public void NotifyHit(
        int pelletCount,
        int maximumPellets,
        bool killed,
        bool firedAsAds)
    {
        if (killed)
        {
            _hitIsKill = true;
            _hitStrength = 1f;
            _hitDuration = killHitDuration;
            _hitTimer = _hitDuration;
            _baseFadeTimer = 0f;
            return;
        }

        float normalizedPellets =
            maximumPellets > 0
                ? (float)pelletCount / maximumPellets
                : 1f;

        normalizedPellets =
            Mathf.Clamp01(normalizedPellets);

        _hitIsKill = false;

        _hitStrength = firedAsAds
            ? 0.72f
            : Mathf.Lerp(
                0.55f,
                1f,
                normalizedPellets
            );

        _hitDuration = normalHitDuration;
        _hitTimer = _hitDuration;
        _baseFadeTimer = normalHitBaseFadeDuration;
    }

    public void ClearTransientFeedback()
    {
        _shotKick = 0f;
        _adsShotKick = 0f;
        _baseFadeTimer = 0f;
        _hitTimer = 0f;

        if (_hitGroup != null)
        {
            _hitGroup.alpha = 0f;
        }
    }

    public void SetDeploymentAlpha(float alpha)
    {
        _deploymentAlpha = Mathf.Clamp01(alpha);
        ApplyRootAlpha(AILURONEGameplayActionGate.IsPaused);
    }

    // =========================================================
    // 推荐参数
    // =========================================================

    [ContextMenu("Apply Recommended Visual Settings")]
    private void ApplyRecommendedVisualSettings()
    {
        ApplyRecommendedVisualSettingsInternal();

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

        ApplyRecommendedVisualSettingsInternal();

        _recommendedSettingsVersion =
            CurrentRecommendedSettingsVersion;
    }

    private void ApplyRecommendedVisualSettingsInternal()
    {
        spreadCalibration = 1f;

        minimumHipfireRadius = 13f;
        maximumHipfireRadius = 90f;

        hipfireDiamondRadialSize = 6f;
        hipfireDiamondTangentSize = 4.5f;
        hipfireDiamondThickness = 1f;
        hipfireOutlineExtraThickness = 0.8f;

        bracketExtraDistance = 5f;
        bracketHeight = 2.5f;
        bracketArmLength = 2f;
        bracketThickness = 1f;

        arcThickness = 1f;
        arcHalfSpanDegrees = 7f;
        arcCenterGapDegrees = 3f;

        arcColor =
            new Color(
                0.64f,
                0.91f,
                1f,
                0.12f
            );

        hipfireOutlineColor =
            new Color(
                0.01f,
                0.025f,
                0.04f,
                0.58f
            );

        adsDiamondRadius = 3.5f;
        adsLineThickness = 1.2f;
        adsOutlineExtraThickness = 0.9f;
        adsOuterLineStart = 9.5f;
        adsOuterLineLength = 3.5f;

        adsOutlineColor =
            new Color(
                0.015f,
                0.035f,
                0.05f,
                0.8f
            );

        hitOutlineColor =
            new Color(
                0.005f,
                0.025f,
                0.035f,
                0.82f
            );

        normalHitColor =
            new Color(
                0.12f,
                0.92f,
                1f,
                1f
            );

        adsShotRecoverSpeed = 14f;
        adsShotDiamondScale = 0.88f;
        adsShotLineLengthBoost = 2f;
        adsShotLineThicknessBoost = 0.35f;
        adsShotBrightnessBoost = 0.3f;

        hitMarkerOutlineExtraThickness = 1.15f;
        normalHitStartOffset = 8f;
        normalHitBaseFadeDuration = 0.08f;
        normalHitBaseAlpha = 0.58f;

        hipfireFadeOutStart = 0.12f;
        hipfireFadeOutEnd = 0.62f;

        adsFadeInStart = 0.45f;
        adsFadeInEnd = 0.92f;
    }

    // =========================================================
    // 引用
    // =========================================================

    private void ResolveReferences()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (targetCanvas == null ||
            targetCanvas.gameObject.name != CanonicalHudCanvasName ||
            targetCanvas.gameObject.scene != activeScene)
        {
            targetCanvas = FindCanonicalHudCanvas(activeScene);
        }

        if (playerWeapon == null)
        {
            playerWeapon =
                FindAnyObjectByType<PlayerWeapon>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private static Canvas FindCanonicalHudCanvas(Scene scene)
    {
        Canvas[] canvases =
            Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        for (int index = 0; index < canvases.Length; index++)
        {
            Canvas canvas = canvases[index];

            if (canvas != null &&
                canvas.gameObject.name == CanonicalHudCanvasName &&
                canvas.gameObject.scene == scene)
            {
                return canvas;
            }
        }

        return null;
    }

    private bool TryInitializeVisuals()
    {
        if (_root != null)
        {
            return true;
        }

        ResolveReferences();

        if (targetCanvas == null)
        {
            return false;
        }

        BuildVisuals();

        if (_root == null)
        {
            return false;
        }

        ApplyImmediateState();
        ApplyRootAlpha(AILURONEGameplayActionGate.IsPaused);
        return true;
    }

    // =========================================================
    // 创建准星
    // =========================================================

    private void BuildVisuals()
    {
        if (targetCanvas == null)
        {
            Debug.LogError(
                "[CrosshairController] 没有找到 Canvas。"
            );

            return;
        }

        Transform existing =
            targetCanvas.transform.Find(
                "CrosshairSystem_Runtime"
            );

        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject rootObject =
            new GameObject(
                "CrosshairSystem_Runtime",
                typeof(RectTransform)
            );

        _root =
            rootObject.GetComponent<RectTransform>();

        _rootCanvasGroup =
            rootObject.AddComponent<CanvasGroup>();

        _rootCanvasGroup.interactable = false;
        _rootCanvasGroup.blocksRaycasts = false;

        _root.SetParent(
            targetCanvas.transform,
            false
        );

        _root.anchorMin =
            new Vector2(0.5f, 0.5f);

        _root.anchorMax =
            new Vector2(0.5f, 0.5f);

        _root.pivot =
            new Vector2(0.5f, 0.5f);

        _root.anchoredPosition =
            Vector2.zero;

        _root.sizeDelta =
            new Vector2(240f, 240f);

        _root.SetAsLastSibling();

        RectTransform hipfireRoot =
            CreateRoot(
                "HipfireCrosshair",
                _root,
                out _hipfireGroup
            );

        _adsRoot =
            CreateRoot(
                "ADSCrosshair",
                _root,
                out _adsGroup
            );

        _teleportLockRoot =
            CreateRoot(
                "TeleportAnchorLock",
                _root,
                out _teleportLockGroup
            );

        RectTransform hitRoot =
            CreateRoot(
                "HitMarker",
                _root,
                out _hitGroup
            );

        BuildHipfireVisuals(hipfireRoot);
        BuildAdsVisuals(_adsRoot);
        BuildTeleportLockVisuals(_teleportLockRoot);
        BuildHitVisuals(hitRoot);

        _hitGroup.alpha = 0f;
        _teleportLockGroup.alpha = 0f;
    }

    private void BuildHipfireVisuals(
        RectTransform parent)
    {
        _hipfireDiamondLines =
            new RectTransform[16];

        _hipfireDiamondOutlineLines =
            new RectTransform[16];

        _hipfireDiamondImages =
            new Image[16];

        _hipfireDiamondOutlineImages =
            new Image[16];

        for (int marker = 0;
             marker < 4;
             marker++)
        {
            for (int edge = 0;
                 edge < 4;
                 edge++)
            {
                int index =
                    marker * 4 + edge;

                CreateLinePair(
                    $"HipfireDiamond_{marker}_Outline_{edge}",
                    $"HipfireDiamond_{marker}_{edge}",
                    parent,
                    hipfireOutlineColor,
                    normalColor,
                    out _hipfireDiamondOutlineLines[index],
                    out _hipfireDiamondLines[index],
                    out _hipfireDiamondOutlineImages[index],
                    out _hipfireDiamondImages[index]
                );
            }
        }

        _arcLines =
            new RectTransform[8];

        _arcImages =
            new Image[8];

        for (int i = 0; i < 8; i++)
        {
            Image image =
                CreateImage(
                    $"SpreadArc_{i}",
                    parent,
                    arcColor
                );

            _arcImages[i] = image;
            _arcLines[i] =
                image.rectTransform;
        }

        _bracketLines =
            new RectTransform[6];

        _bracketImages =
            new Image[6];

        CreateBracketLine(
            0,
            "Left_Vertical",
            parent
        );

        CreateBracketLine(
            1,
            "Left_UpperArm",
            parent
        );

        CreateBracketLine(
            2,
            "Left_LowerArm",
            parent
        );

        CreateBracketLine(
            3,
            "Right_Vertical",
            parent
        );

        CreateBracketLine(
            4,
            "Right_UpperArm",
            parent
        );

        CreateBracketLine(
            5,
            "Right_LowerArm",
            parent
        );
    }

    private void CreateBracketLine(
        int index,
        string objectName,
        Transform parent)
    {
        Image image =
            CreateImage(
                objectName,
                parent,
                normalColor
            );

        _bracketImages[index] =
            image;

        _bracketLines[index] =
            image.rectTransform;
    }

    private void BuildAdsVisuals(
        RectTransform parent)
    {
        _adsDiamondLines =
            new RectTransform[4];

        _adsOuterLines =
            new RectTransform[4];

        _adsOutlineDiamondLines =
            new RectTransform[4];

        _adsOutlineOuterLines =
            new RectTransform[4];

        _adsForegroundImages =
            new Image[8];

        _adsOutlineImages =
            new Image[8];

        for (int i = 0; i < 4; i++)
        {
            CreateLinePair(
                $"ADS_DiamondOutline_{i}",
                $"ADS_Diamond_{i}",
                parent,
                adsOutlineColor,
                adsColor,
                out _adsOutlineDiamondLines[i],
                out _adsDiamondLines[i],
                out _adsOutlineImages[i],
                out _adsForegroundImages[i]
            );
        }

        string[] outerNames =
        {
            "Top",
            "Right",
            "Bottom",
            "Left"
        };

        for (int i = 0; i < 4; i++)
        {
            CreateLinePair(
                $"ADS_{outerNames[i]}LineOutline",
                $"ADS_{outerNames[i]}Line",
                parent,
                adsOutlineColor,
                adsColor,
                out _adsOutlineOuterLines[i],
                out _adsOuterLines[i],
                out _adsOutlineImages[i + 4],
                out _adsForegroundImages[i + 4]
            );
        }
    }

    private void BuildHitVisuals(
        RectTransform parent)
    {
        _hitLines =
            new RectTransform[8];

        _hitOutlineLines =
            new RectTransform[8];

        _hitImages =
            new Image[8];

        _hitOutlineImages =
            new Image[8];

        for (int i = 0;
             i < _hitLines.Length;
             i++)
        {
            CreateLinePair(
                $"HitAngleOutline_{i}",
                $"HitAngle_{i}",
                parent,
                hitOutlineColor,
                normalHitColor,
                out _hitOutlineLines[i],
                out _hitLines[i],
                out _hitOutlineImages[i],
                out _hitImages[i]
            );
        }
    }

    private void BuildTeleportLockVisuals(
        RectTransform parent)
    {
        _teleportLockLines = new RectTransform[8];
        _teleportLockOutlineLines = new RectTransform[8];
        _teleportLockImages = new Image[8];
        _teleportLockOutlineImages = new Image[8];

        for (int i = 0; i < 8; i++)
        {
            CreateLinePair(
                $"TeleportLockOutline_{i}",
                $"TeleportLock_{i}",
                parent,
                teleportLockOutlineColor,
                teleportLockColor,
                out _teleportLockOutlineLines[i],
                out _teleportLockLines[i],
                out _teleportLockOutlineImages[i],
                out _teleportLockImages[i]
            );
        }
    }

    private void CreateLinePair(
        string outlineName,
        string foregroundName,
        Transform parent,
        Color outlineColor,
        Color foregroundColor,
        out RectTransform outlineRect,
        out RectTransform foregroundRect,
        out Image outlineImage,
        out Image foregroundImage)
    {
        outlineImage =
            CreateImage(
                outlineName,
                parent,
                outlineColor
            );

        foregroundImage =
            CreateImage(
                foregroundName,
                parent,
                foregroundColor
            );

        outlineRect =
            outlineImage.rectTransform;

        foregroundRect =
            foregroundImage.rectTransform;
    }

    private RectTransform CreateRoot(
        string objectName,
        Transform parent,
        out CanvasGroup canvasGroup)
    {
        GameObject child =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasGroup)
            );

        RectTransform rect =
            child.GetComponent<RectTransform>();

        rect.SetParent(parent, false);

        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.anchoredPosition =
            Vector2.zero;

        rect.sizeDelta =
            new Vector2(220f, 220f);

        canvasGroup =
            child.GetComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        return rect;
    }

    private Image CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject child =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        RectTransform rect =
            child.GetComponent<RectTransform>();

        rect.SetParent(parent, false);

        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.anchoredPosition =
            Vector2.zero;

        Image image =
            child.GetComponent<Image>();

        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    // =========================================================
    // 状态更新
    // =========================================================

    private void ApplyImmediateState()
    {
        _adsBlend =
            playerWeapon != null &&
            playerWeapon.IsAiming
                ? 1f
                : 0f;

        _shotKick = 0f;
        _adsShotKick = 0f;
        _baseFadeTimer = 0f;

        UpdateCrosshairLayout();
        UpdateBaseColors();
    }

    private void UpdateAdsBlend(
        float delta)
    {
        bool aiming =
            playerWeapon != null &&
            playerWeapon.IsAiming;

        float target =
            aiming ? 1f : 0f;

        float factor =
            1f -
            Mathf.Exp(
                -adsBlendSpeed *
                delta
            );

        _adsBlend =
            Mathf.Lerp(
                _adsBlend,
                target,
                factor
            );

        if (Mathf.Abs(
                _adsBlend - target
            ) < 0.001f)
        {
            _adsBlend = target;
        }
    }

    private void UpdateShotKick(
        float delta)
    {
        _shotKick =
            Mathf.MoveTowards(
                _shotKick,
                0f,
                shotKickRecoverSpeed *
                delta
            );

        _adsShotKick =
            Mathf.MoveTowards(
                _adsShotKick,
                0f,
                adsShotRecoverSpeed *
                delta
            );

        _baseFadeTimer =
            Mathf.Max(
                0f,
                _baseFadeTimer - delta
            );
    }

    private void UpdateCrosshairLayout()
    {
        if (_hipfireGroup == null ||
            _adsGroup == null)
        {
            return;
        }

        float hipfireRadius =
            CalculateHipfireRadius();

        float kickRadius =
            hipfireKickDistance *
            _shotKick;

        float displayedRadius =
            Mathf.Lerp(
                hipfireRadius + kickRadius,
                adsDiamondRadius,
                Smooth01(_adsBlend)
            );

        PositionHipfireDiamonds(
            displayedRadius
        );

        PositionHipfireArcs(
            displayedRadius
        );

        PositionBrackets(
            displayedRadius
        );

        PositionAdsGeometry();

        _hipfireGroup.alpha =
            1f -
            SmoothRange(
                hipfireFadeOutStart,
                hipfireFadeOutEnd,
                _adsBlend
            );

        _adsGroup.alpha =
            SmoothRange(
                adsFadeInStart,
                adsFadeInEnd,
                _adsBlend
            );

        float baseCrosshairAlpha =
            GetBaseCrosshairAlpha();

        _hipfireGroup.alpha *=
            baseCrosshairAlpha;

        _adsGroup.alpha *=
            baseCrosshairAlpha;

        if (_adsRoot != null)
        {
            float scale =
                Mathf.Lerp(
                    0.68f,
                    1f,
                    SmoothRange(
                        adsFadeInStart,
                        1f,
                        _adsBlend
                    )
                );

            _adsRoot.localScale =
                Vector3.one * scale;
        }
    }

    private void UpdateTeleportPresentation(
        float delta
    )
    {
        if (_teleportHiddenRemaining > 0f)
        {
            _teleportHiddenRemaining -= delta;
            _teleportPresentationAlpha = 0f;
        }
        else if (_teleportChannelActive)
        {
            _teleportHiddenRemaining = 0f;
            _teleportPresentationAlpha =
                1f - Smooth01(
                    _teleportChannelProgress
                );
        }
        else
        {
            _teleportHiddenRemaining = 0f;
            _teleportPresentationAlpha =
                Mathf.MoveTowards(
                    _teleportPresentationAlpha,
                    1f,
                    teleportRestoreSpeed * delta
                );
        }

        float shakeStrength =
            Mathf.InverseLerp(
                0.86f,
                1f,
                _teleportChannelProgress
            ) *
            teleportCommitShakePixels *
            _teleportPresentationAlpha;

        _teleportPresentationOffset =
            shakeStrength > 0.001f
                ? Random.insideUnitCircle *
                    shakeStrength
                : Vector2.zero;

        if (_rootCanvasGroup != null)
        {
            ApplyRootAlpha(false);
        }
    }

    private void ApplyRootAlpha(bool forceHidden)
    {
        if (_rootCanvasGroup == null)
        {
            return;
        }

        _rootCanvasGroup.alpha = forceHidden
            ? 0f
            : _teleportPresentationAlpha *
              _deploymentAlpha *
              (AILURONEGameSettings.CrosshairVisible ? 1f : 0f);
    }

    private void UpdateTeleportAnchorFocus(
        float delta)
    {
        if (_root == null ||
            _teleportLockGroup == null)
        {
            return;
        }

        float targetBlend =
            _teleportFocusRequested
                ? _teleportFocusStrength
                : 0f;

        float blendFactor =
            1f -
            Mathf.Exp(
                -teleportLockBlendSpeed *
                Mathf.Max(0f, delta)
            );

        _teleportFocusBlend =
            Mathf.Lerp(
                _teleportFocusBlend,
                targetBlend,
                blendFactor
            );

        float canvasScale =
            targetCanvas != null
                ? Mathf.Max(
                    0.0001f,
                    targetCanvas.scaleFactor
                )
                : 1f;

        Vector2 desiredOffset =
            Vector2.zero;

        if (_teleportFocusRequested &&
            _teleportFocusScreenOffset.sqrMagnitude >
                0.0001f)
        {
            desiredOffset =
                Vector2.ClampMagnitude(
                    _teleportFocusScreenOffset /
                    canvasScale,
                    teleportMagnetPixels /
                    canvasScale
                );
        }

        float smoothBlend =
            Smooth01(_teleportFocusBlend);

        _root.anchoredPosition =
            Vector2.Lerp(
                Vector2.zero,
                desiredOffset,
                smoothBlend
            ) +
            _teleportPresentationOffset;

        _root.localScale =
            Vector3.one *
            Mathf.Lerp(
                1f,
                1.035f,
                smoothBlend
            );

        _teleportLockGroup.alpha =
            smoothBlend;

        PositionTeleportLockGeometry(
            smoothBlend
        );
    }

    private void PositionTeleportLockGeometry(
        float blend)
    {
        if (_teleportLockLines == null ||
            _teleportLockOutlineLines == null)
        {
            return;
        }

        float pulse =
            Mathf.Sin(
                Time.unscaledTime * 6.5f
            ) *
            0.6f *
            blend *
            (1f - _teleportChannelProgress);

        float channelContraction =
            Mathf.Lerp(
                1f,
                teleportChannelContractRatio,
                Smooth01(_teleportChannelProgress)
            );

        float radius =
            Mathf.Lerp(
                teleportLockRadius * 0.62f,
                teleportLockRadius,
                blend
            ) *
            channelContraction +
            pulse;

        float arm =
            teleportLockArmLength;

        Vector2[] starts =
        {
            new Vector2(-radius, radius),
            new Vector2(-radius, radius),
            new Vector2(radius, radius),
            new Vector2(radius, radius),
            new Vector2(radius, -radius),
            new Vector2(radius, -radius),
            new Vector2(-radius, -radius),
            new Vector2(-radius, -radius)
        };

        Vector2[] ends =
        {
            new Vector2(-radius + arm, radius),
            new Vector2(-radius, radius - arm),
            new Vector2(radius - arm, radius),
            new Vector2(radius, radius - arm),
            new Vector2(radius - arm, -radius),
            new Vector2(radius, -radius + arm),
            new Vector2(-radius + arm, -radius),
            new Vector2(-radius, -radius + arm)
        };

        for (int i = 0;
             i < _teleportLockLines.Length;
             i++)
        {
            SetLineBetween(
                _teleportLockOutlineLines[i],
                starts[i],
                ends[i],
                teleportLockThickness + 0.9f
            );

            SetLineBetween(
                _teleportLockLines[i],
                starts[i],
                ends[i],
                teleportLockThickness
            );
        }
    }

    private float GetBaseCrosshairAlpha()
    {
        if (_baseFadeTimer <= 0f ||
            normalHitBaseFadeDuration <= 0f)
        {
            return 1f;
        }

        float progress =
            1f -
            Mathf.Clamp01(
                _baseFadeTimer /
                normalHitBaseFadeDuration
            );

        float pulse =
            Mathf.Sin(
                progress *
                Mathf.PI
            );

        return Mathf.Lerp(
            1f,
            normalHitBaseAlpha,
            pulse
        );
    }

    private float CalculateHipfireRadius()
    {
        float spread =
            playerWeapon != null
                ? Mathf.Max(
                    0f,
                    playerWeapon.spreadAngle
                )
                : 0.04f;

        float currentFov =
            GetCurrentVerticalFov();

        float focalPixels =
            Screen.height *
            0.5f /
            Mathf.Tan(
                currentFov *
                0.5f *
                Mathf.Deg2Rad
            );

        float pixelRadius =
            spread *
            focalPixels *
            spreadCalibration;

        float canvasScale =
            targetCanvas != null
                ? Mathf.Max(
                    0.0001f,
                    targetCanvas.scaleFactor
                )
                : 1f;

        float canvasRadius =
            pixelRadius /
            canvasScale;

        return Mathf.Clamp(
            canvasRadius,
            minimumHipfireRadius,
            maximumHipfireRadius
        );
    }

    private float GetCurrentVerticalFov()
    {
        if (StarterAssets
                .FirstPersonController
                .Instance != null &&
            StarterAssets
                .FirstPersonController
                .Instance
                .virtualCamera != null)
        {
            return Mathf.Clamp(
                StarterAssets
                    .FirstPersonController
                    .Instance
                    .virtualCamera
                    .Lens
                    .FieldOfView,
                1f,
                179f
            );
        }

        if (playerCamera != null)
        {
            return Mathf.Clamp(
                playerCamera.fieldOfView,
                1f,
                179f
            );
        }

        return 90f;
    }

    // =========================================================
    // 腰射布局
    // =========================================================

    private void PositionHipfireDiamonds(
        float radius)
    {
        if (_hipfireDiamondLines == null ||
            _hipfireDiamondLines.Length < 16)
        {
            return;
        }

        Vector2[] directions =
        {
            Vector2.up,
            Vector2.right,
            Vector2.down,
            Vector2.left
        };

        float radialHalf =
            hipfireDiamondRadialSize *
            0.5f *
            (1f + _shotKick * 0.08f);

        float tangentHalf =
            hipfireDiamondTangentSize *
            0.5f *
            (1f + _shotKick * 0.05f);

        float foregroundThickness =
            hipfireDiamondThickness;

        float outlineThickness =
            hipfireDiamondThickness +
            hipfireOutlineExtraThickness;

        for (int marker = 0;
             marker < 4;
             marker++)
        {
            Vector2 radial =
                directions[marker];

            Vector2 tangent =
                new Vector2(
                    -radial.y,
                    radial.x
                );

            Vector2 center =
                radial * PixelSnap(radius);

            Vector2 inner =
                center -
                radial * radialHalf;

            Vector2 sideA =
                center +
                tangent * tangentHalf;

            Vector2 outer =
                center +
                radial * radialHalf;

            Vector2 sideB =
                center -
                tangent * tangentHalf;

            Vector2[] starts =
            {
                inner,
                sideA,
                outer,
                sideB
            };

            Vector2[] ends =
            {
                sideA,
                outer,
                sideB,
                inner
            };

            for (int edge = 0;
                 edge < 4;
                 edge++)
            {
                int index =
                    marker * 4 + edge;

                SetLineBetween(
                    _hipfireDiamondOutlineLines[index],
                    starts[edge],
                    ends[edge],
                    outlineThickness
                );

                SetLineBetween(
                    _hipfireDiamondLines[index],
                    starts[edge],
                    ends[edge],
                    foregroundThickness
                );
            }
        }
    }

    private void PositionHipfireArcs(
        float radius)
    {
        if (_arcLines == null ||
            _arcLines.Length < 8)
        {
            return;
        }

        float arcRadius =
            radius +
            _shotKick * 1.5f;

        float halfSpan =
            arcHalfSpanDegrees +
            _shotKick * 2f;

        float gap =
            Mathf.Min(
                arcCenterGapDegrees,
                halfSpan - 0.5f
            );

        float[] centers =
        {
            45f,
            135f,
            225f,
            315f
        };

        int lineIndex = 0;

        for (int group = 0;
             group < 4;
             group++)
        {
            float centerAngle =
                centers[group];

            SetArcSegment(
                _arcLines[lineIndex],
                centerAngle - halfSpan,
                centerAngle - gap,
                arcRadius
            );

            lineIndex++;

            SetArcSegment(
                _arcLines[lineIndex],
                centerAngle + gap,
                centerAngle + halfSpan,
                arcRadius
            );

            lineIndex++;
        }
    }

    private void SetArcSegment(
        RectTransform line,
        float startDegrees,
        float endDegrees,
        float radius)
    {
        float startRadians =
            startDegrees *
            Mathf.Deg2Rad;

        float endRadians =
            endDegrees *
            Mathf.Deg2Rad;

        Vector2 start =
            new Vector2(
                Mathf.Cos(startRadians),
                Mathf.Sin(startRadians)
            ) *
            radius;

        Vector2 end =
            new Vector2(
                Mathf.Cos(endRadians),
                Mathf.Sin(endRadians)
            ) *
            radius;

        SetLineBetween(
            line,
            start,
            end,
            arcThickness
        );
    }

    private void PositionBrackets(
        float radius)
    {
        if (_bracketLines == null ||
            _bracketLines.Length < 6)
        {
            return;
        }

        float horizontalDistance =
            PixelSnap(
                radius +
                bracketExtraDistance +
                bracketKickDistance *
                _shotKick
            );

        float height =
            PixelSnap(bracketHeight);

        float armLength =
            PixelSnap(bracketArmLength);

        float halfHeight =
            height * 0.5f;

        float halfArm =
            armLength * 0.5f;

        SetSimpleLine(
            _bracketLines[0],
            new Vector2(
                -horizontalDistance,
                0f
            ),
            height,
            bracketThickness,
            90f
        );

        SetSimpleLine(
            _bracketLines[1],
            new Vector2(
                -horizontalDistance + halfArm,
                halfHeight
            ),
            armLength,
            bracketThickness,
            0f
        );

        SetSimpleLine(
            _bracketLines[2],
            new Vector2(
                -horizontalDistance + halfArm,
                -halfHeight
            ),
            armLength,
            bracketThickness,
            0f
        );

        SetSimpleLine(
            _bracketLines[3],
            new Vector2(
                horizontalDistance,
                0f
            ),
            height,
            bracketThickness,
            90f
        );

        SetSimpleLine(
            _bracketLines[4],
            new Vector2(
                horizontalDistance - halfArm,
                halfHeight
            ),
            armLength,
            bracketThickness,
            0f
        );

        SetSimpleLine(
            _bracketLines[5],
            new Vector2(
                horizontalDistance - halfArm,
                -halfHeight
            ),
            armLength,
            bracketThickness,
            0f
        );
    }

    // =========================================================
    // ADS 布局
    // =========================================================

    private void PositionAdsGeometry()
    {
        if (_adsDiamondLines == null ||
            _adsOuterLines == null)
        {
            return;
        }

        float shotPulse =
            Smooth01(_adsShotKick);

        float radius =
            adsDiamondRadius *
            Mathf.Lerp(
                1f,
                adsShotDiamondScale,
                shotPulse
            );

        Vector2 top =
            new Vector2(0f, radius);

        Vector2 right =
            new Vector2(radius, 0f);

        Vector2 bottom =
            new Vector2(0f, -radius);

        Vector2 left =
            new Vector2(-radius, 0f);

        Vector2[] starts =
        {
            top,
            right,
            bottom,
            left
        };

        Vector2[] ends =
        {
            right,
            bottom,
            left,
            top
        };

        float foregroundThickness =
            adsLineThickness +
            adsShotLineThicknessBoost *
            shotPulse;

        float outlineThickness =
            foregroundThickness +
            adsOutlineExtraThickness;

        for (int i = 0; i < 4; i++)
        {
            SetLineBetween(
                _adsOutlineDiamondLines[i],
                starts[i],
                ends[i],
                outlineThickness
            );

            SetLineBetween(
                _adsDiamondLines[i],
                starts[i],
                ends[i],
                foregroundThickness
            );
        }

        float expansion =
            Mathf.Lerp(
                4f,
                adsOuterLineStart,
                Smooth01(_adsBlend)
            );

        float outerLineLength =
            adsOuterLineLength +
            adsShotLineLengthBoost *
            shotPulse;

        SetSimpleLine(
            _adsOutlineOuterLines[0],
            new Vector2(
                0f,
                expansion
            ),
            outerLineLength,
            outlineThickness,
            90f
        );

        SetSimpleLine(
            _adsOuterLines[0],
            new Vector2(
                0f,
                expansion
            ),
            outerLineLength,
            foregroundThickness,
            90f
        );

        SetSimpleLine(
            _adsOutlineOuterLines[1],
            new Vector2(
                expansion,
                0f
            ),
            outerLineLength,
            outlineThickness,
            0f
        );

        SetSimpleLine(
            _adsOuterLines[1],
            new Vector2(
                expansion,
                0f
            ),
            outerLineLength,
            foregroundThickness,
            0f
        );

        SetSimpleLine(
            _adsOutlineOuterLines[2],
            new Vector2(
                0f,
                -expansion
            ),
            outerLineLength,
            outlineThickness,
            90f
        );

        SetSimpleLine(
            _adsOuterLines[2],
            new Vector2(
                0f,
                -expansion
            ),
            outerLineLength,
            foregroundThickness,
            90f
        );

        SetSimpleLine(
            _adsOutlineOuterLines[3],
            new Vector2(
                -expansion,
                0f
            ),
            outerLineLength,
            outlineThickness,
            0f
        );

        SetSimpleLine(
            _adsOuterLines[3],
            new Vector2(
                -expansion,
                0f
            ),
            outerLineLength,
            foregroundThickness,
            0f
        );
    }

    // =========================================================
    // 颜色
    // =========================================================

    private void UpdateBaseColors()
    {
        Color primaryColor =
            normalColor;

        Color currentAdsColor =
            adsColor;

        Color currentArcColor =
            arcColor;

        bool specialState = false;

        if (readTimeManagerState &&
            TimeManager.Instance != null)
        {
            if (TimeManager.Instance.IsRewinding)
            {
                bool flash =
                    Mathf.FloorToInt(
                        Time.unscaledTime * 22f
                    ) %
                    2 ==
                    0;

                primaryColor =
                    flash
                        ? fatalColor
                        : Color.white;

                currentAdsColor =
                    primaryColor;

                currentArcColor =
                    primaryColor;

                specialState = true;
            }
            else if (
                TimeManager.Instance
                    .IsAbilityActive)
            {
                primaryColor =
                    timeSlowColor;

                currentAdsColor =
                    timeSlowColor;

                currentArcColor =
                    timeSlowColor;

                specialState = true;
            }
        }

        SetImagesColor(
            _hipfireDiamondImages,
            primaryColor,
            1f
        );

        SetImagesColorAbsolute(
            _hipfireDiamondOutlineImages,
            hipfireOutlineColor
        );

        SetImagesColor(
            _bracketImages,
            primaryColor,
            0.34f
        );

        float arcAlphaMultiplier =
            1f +
            _shotKick * 0.55f;

        Color finalArcColor =
            currentArcColor;

        finalArcColor.a =
            specialState
                ? Mathf.Clamp01(
                    currentArcColor.a *
                    0.72f *
                    arcAlphaMultiplier
                )
                : Mathf.Clamp01(
                    arcColor.a *
                    arcAlphaMultiplier
                );

        SetImagesColorAbsolute(
            _arcImages,
            finalArcColor
        );

        Color finalAdsColor =
            Color.Lerp(
                currentAdsColor,
                Color.white,
                _adsShotKick *
                adsShotBrightnessBoost
            );

        SetImagesColor(
            _adsForegroundImages,
            finalAdsColor,
            1f
        );

        SetImagesColorAbsolute(
            _adsOutlineImages,
            adsOutlineColor
        );
    }

    private void SetImagesColor(
        Image[] images,
        Color color,
        float alphaMultiplier)
    {
        if (images == null)
        {
            return;
        }

        Color finalColor =
            color;

        finalColor.a =
            Mathf.Clamp01(
                color.a *
                alphaMultiplier
            );

        for (int i = 0;
             i < images.Length;
             i++)
        {
            if (images[i] != null)
            {
                images[i].color =
                    finalColor;
            }
        }
    }

    private void SetImagesColorAbsolute(
        Image[] images,
        Color color)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0;
             i < images.Length;
             i++)
        {
            if (images[i] != null)
            {
                images[i].color =
                    color;
            }
        }
    }

    // =========================================================
    // 中央命中反馈
    // =========================================================

    private void UpdateHitMarker(
        float delta)
    {
        if (_hitGroup == null ||
            _hitLines == null)
        {
            return;
        }

        if (_hitTimer <= 0f)
        {
            _hitGroup.alpha = 0f;
            return;
        }

        _hitTimer -= delta;

        float normalized =
            _hitDuration > 0f
                ? Mathf.Clamp01(
                    1f -
                    _hitTimer /
                    _hitDuration
                )
                : 1f;

        float fade =
            1f -
            Smooth01(normalized);

        float strength =
            Mathf.Clamp01(
                _hitStrength
            );

        float radius;
        float armSize;

        if (_hitIsKill)
        {
            if (normalized < 0.42f)
            {
                float closeT =
                    Smooth01(
                        normalized /
                        0.42f
                    );

                radius =
                    Mathf.Lerp(
                        hitMarkerRadius + 3f,
                        3.5f,
                        closeT
                    );
            }
            else
            {
                float breakT =
                    Smooth01(
                        (normalized - 0.42f) /
                        0.58f
                    );

                radius =
                    Mathf.Lerp(
                        3.5f,
                        hitMarkerRadius + 6f,
                        breakT
                    );
            }

            armSize =
                hitMarkerArmSize * 1.1f;
        }
        else
        {
            radius =
                Mathf.Lerp(
                    hitMarkerRadius +
                    normalHitStartOffset,
                    hitMarkerRadius,
                    Smooth01(normalized)
                );

            armSize =
                hitMarkerArmSize *
                Mathf.Lerp(
                    0.85f,
                    1.25f,
                    strength
                );
        }

        PositionHitAngles(
            radius,
            armSize
        );

        Color color =
            _hitIsKill
                ? killColor
                : normalHitColor;

        float baseAlpha =
            _hitIsKill
                ? 1f
                : Mathf.Lerp(
                    0.65f,
                    1f,
                    strength
                );

        color.a *=
            fade *
            baseAlpha;

        SetImagesColorAbsolute(
            _hitImages,
            color
        );

        Color outlineColor =
            hitOutlineColor;

        outlineColor.a *=
            fade;

        SetImagesColorAbsolute(
            _hitOutlineImages,
            outlineColor
        );

        _hitGroup.alpha = 1f;

        if (_hitTimer <= 0f)
        {
            _hitGroup.alpha = 0f;
        }
    }

    private void PositionHitAngles(
        float radius,
        float armSize)
    {
        if (_hitLines == null ||
            _hitOutlineLines == null ||
            _hitLines.Length < 8 ||
            _hitOutlineLines.Length < 8)
        {
            return;
        }

        float half =
            armSize * 0.5f;

        SetHitLinePair(
            0,
            new Vector2(
                -half,
                radius + half
            ),
            new Vector2(
                0f,
                radius
            )
        );

        SetHitLinePair(
            1,
            new Vector2(
                0f,
                radius
            ),
            new Vector2(
                half,
                radius + half
            )
        );

        SetHitLinePair(
            2,
            new Vector2(
                -half,
                -radius - half
            ),
            new Vector2(
                0f,
                -radius
            )
        );

        SetHitLinePair(
            3,
            new Vector2(
                0f,
                -radius
            ),
            new Vector2(
                half,
                -radius - half
            )
        );

        SetHitLinePair(
            4,
            new Vector2(
                -radius - half,
                half
            ),
            new Vector2(
                -radius,
                0f
            )
        );

        SetHitLinePair(
            5,
            new Vector2(
                -radius,
                0f
            ),
            new Vector2(
                -radius - half,
                -half
            )
        );

        SetHitLinePair(
            6,
            new Vector2(
                radius + half,
                half
            ),
            new Vector2(
                radius,
                0f
            )
        );

        SetHitLinePair(
            7,
            new Vector2(
                radius,
                0f
            ),
            new Vector2(
                radius + half,
                -half
            )
        );
    }

    private void SetHitLinePair(
        int index,
        Vector2 start,
        Vector2 end)
    {
        SetLineBetween(
            _hitOutlineLines[index],
            start,
            end,
            hitMarkerThickness +
            hitMarkerOutlineExtraThickness
        );

        SetLineBetween(
            _hitLines[index],
            start,
            end,
            hitMarkerThickness
        );
    }

    // =========================================================
    // 通用线条函数
    // =========================================================

    private void SetSimpleLine(
        RectTransform line,
        Vector2 position,
        float length,
        float thickness,
        float rotation)
    {
        if (line == null)
        {
            return;
        }

        line.anchoredPosition =
            new Vector2(
                PixelSnap(position.x),
                PixelSnap(position.y)
            );

        line.sizeDelta =
            new Vector2(
                Mathf.Max(0.1f, length),
                Mathf.Max(0.1f, thickness)
            );

        line.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                rotation
            );
    }

    private void SetLineBetween(
        RectTransform line,
        Vector2 start,
        Vector2 end,
        float thickness)
    {
        if (line == null)
        {
            return;
        }

        Vector2 delta =
            end - start;

        line.anchoredPosition =
            (start + end) * 0.5f;

        line.sizeDelta =
            new Vector2(
                delta.magnitude,
                Mathf.Max(0.1f, thickness)
            );

        line.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(
                    delta.y,
                    delta.x
                ) *
                Mathf.Rad2Deg
            );
    }

    // =========================================================
    // 工具
    // =========================================================

    private float PixelSnap(float value)
    {
        float canvasScale =
            targetCanvas != null
                ? Mathf.Max(
                    0.0001f,
                    targetCanvas.scaleFactor
                )
                : 1f;

        return Mathf.Round(
            value * canvasScale
        ) /
        canvasScale;
    }

    private float Smooth01(float value)
    {
        value =
            Mathf.Clamp01(value);

        return value *
               value *
               (3f - 2f * value);
    }

    private float SmoothRange(
        float start,
        float end,
        float value)
    {
        if (end <= start)
        {
            return value >= end
                ? 1f
                : 0f;
        }

        float normalized =
            Mathf.InverseLerp(
                start,
                end,
                value
            );

        return Smooth01(normalized);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpgradeRecommendedSettingsIfNeeded();

        spreadCalibration =
            Mathf.Max(
                0.1f,
                spreadCalibration
            );

        minimumHipfireRadius =
            Mathf.Max(
                1f,
                minimumHipfireRadius
            );

        maximumHipfireRadius =
            Mathf.Max(
                minimumHipfireRadius,
                maximumHipfireRadius
            );

        hipfireDiamondRadialSize =
            Mathf.Max(
                2f,
                hipfireDiamondRadialSize
            );

        hipfireDiamondTangentSize =
            Mathf.Max(
                2f,
                hipfireDiamondTangentSize
            );

        hipfireDiamondThickness =
            Mathf.Max(
                0.5f,
                hipfireDiamondThickness
            );

        hipfireOutlineExtraThickness =
            Mathf.Max(
                0.25f,
                hipfireOutlineExtraThickness
            );

        bracketHeight =
            Mathf.Max(
                1f,
                bracketHeight
            );

        bracketArmLength =
            Mathf.Max(
                1f,
                bracketArmLength
            );

        bracketThickness =
            Mathf.Max(
                0.5f,
                bracketThickness
            );

        arcThickness =
            Mathf.Max(
                0.5f,
                arcThickness
            );

        arcCenterGapDegrees =
            Mathf.Clamp(
                arcCenterGapDegrees,
                0.5f,
                Mathf.Max(
                    0.5f,
                    arcHalfSpanDegrees - 0.5f
                )
            );

        adsDiamondRadius =
            Mathf.Max(
                1f,
                adsDiamondRadius
            );

        adsLineThickness =
            Mathf.Max(
                0.5f,
                adsLineThickness
            );

        adsOutlineExtraThickness =
            Mathf.Max(
                0.25f,
                adsOutlineExtraThickness
            );

        adsOuterLineStart =
            Mathf.Max(
                1f,
                adsOuterLineStart
            );

        adsOuterLineLength =
            Mathf.Max(
                1f,
                adsOuterLineLength
            );

        adsBlendSpeed =
            Mathf.Max(
                1f,
                adsBlendSpeed
            );

        adsShotRecoverSpeed =
            Mathf.Max(
                0.01f,
                adsShotRecoverSpeed
            );

        adsShotDiamondScale =
            Mathf.Clamp(
                adsShotDiamondScale,
                0.7f,
                1f
            );

        adsShotLineLengthBoost =
            Mathf.Max(
                0f,
                adsShotLineLengthBoost
            );

        adsShotLineThicknessBoost =
            Mathf.Max(
                0f,
                adsShotLineThicknessBoost
            );

        hitMarkerOutlineExtraThickness =
            Mathf.Max(
                0.25f,
                hitMarkerOutlineExtraThickness
            );

        normalHitStartOffset =
            Mathf.Max(
                0f,
                normalHitStartOffset
            );

        normalHitBaseFadeDuration =
            Mathf.Max(
                0.01f,
                normalHitBaseFadeDuration
            );

        normalHitBaseAlpha =
            Mathf.Clamp(
                normalHitBaseAlpha,
                0.1f,
                1f
            );

        hipfireFadeOutEnd =
            Mathf.Clamp(
                hipfireFadeOutEnd,
                hipfireFadeOutStart + 0.01f,
                1f
            );

        adsFadeInEnd =
            Mathf.Clamp(
                adsFadeInEnd,
                adsFadeInStart + 0.01f,
                1f
            );
    }
#endif
}
