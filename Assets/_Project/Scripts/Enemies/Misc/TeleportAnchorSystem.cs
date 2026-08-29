#pragma warning disable 0618
#pragma warning disable 0414
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class TeleportAnchorSystem : MonoBehaviour
{
    private const int MaximumAnchorCount = 3;
    private const float MinimumBirthDuration = 0.26f;
    private const float BirthOvershootScale = 1.12f;
    private const float BirthOvershootPoint = 0.68f;
    private const float ExpiryWarningDuration = 3f;
    private const float ExpiryCollapseEndScale = 0.20f;

    private static readonly Color ExpiryWarningBrightColor =
        new Color(1.35f, 0.34f, 0.06f, 1f);

    private static readonly Color ExpiryWarningDarkColor =
        new Color(0.16f, 0.025f, 0.008f, 1f);

    private sealed class AnchorRecord
    {
        public int slotIndex;

        public Vector3 teleportDestination;
        public Vector3 visualPosition;

        public float revealTime;
        public float expireTime;

        public float brightWidthPixels;
        public float darkWidthPixels;

        public Color brightColor;
        public Color darkColor;

        public GameObject rootObject;
        public Transform rootTransform;
        public SphereCollider aimCollider;

        public readonly List<LineRenderer> compactBrightLines =
            new List<LineRenderer>();

        public readonly List<LineRenderer> compactDarkLines =
            new List<LineRenderer>();

        public readonly List<LineRenderer> detailBrightLines =
            new List<LineRenderer>();

        public readonly List<LineRenderer> detailDarkLines =
            new List<LineRenderer>();

        public LineRenderer connectorBrightLine;
        public LineRenderer connectorDarkLine;

        public LineRenderer confirmBrightLine;
        public LineRenderer confirmDarkLine;

        public readonly List<LineRenderer> channelBrightLines =
            new List<LineRenderer>();

        public readonly List<LineRenderer> channelDarkLines =
            new List<LineRenderer>();

        public bool visibleThisFrame;
        public bool selectedThisFrame;

        public float focusBlend;
        public float distanceToCamera;
        public float screenCenterDistance;

        public Vector2 rawScreenPosition;
        public Vector2 screenOffsetPixels;
    }

    public static TeleportAnchorSystem Instance
    {
        get;
        private set;
    }

    [Header("核心引用")]
    [Tooltip("留空时自动使用 Camera.main。")]
    public Camera targetCamera;

    [Tooltip(
        "可选的亮线材质覆盖。留空时自动复制第一个被击杀敌人的 " +
        "EnemyHitMarker 材质。"
    )]
    public Material materialTemplateOverride;

    [Header("锚点规则")]
    [Min(0.5f)]
    [Tooltip("锚点完整可用的真实时间，不受慢动作影响。")]
    public float anchorLifetime = 12f;

    [Range(0f, 0.1f)]
    [Tooltip(
        "让持久锚点比击杀标记结束稍早出现，" +
        "形成自然接替而不是闪断。"
    )]
    public float transitionOverlap = 0.035f;

    [Min(0.01f)]
    public float appearDuration = 0.12f;

    [Min(0.01f)]
    public float fadeDuration = 0.24f;

    [Range(0.2f, 1f)]
    public float appearStartScale = 0.58f;

    [Range(0.5f, 1f)]
    public float fadeEndScale = 0.78f;

    [Header("距离尺寸（1080p / FOV 40 推荐）")]
    [Min(1f)]
    public float nearDistance = 12f;

    [Min(1f)]
    public float middleDistance = 30f;

    [Min(1f)]
    public float farDistance = 50f;

    [Range(12f, 32f)]
    public float nearSizePixels = 24f;

    [Range(12f, 28f)]
    public float middleSizePixels = 20f;

    [Range(10f, 24f)]
    public float farSizePixels = 17f;

    [Range(10f, 20f)]
    public float minimumSizePixels = 14f;

    [Range(26f, 46f)]
    [Tooltip("准星指向锚点时的展开尺寸。")]
    public float focusedSizePixels = 36f;

    [Header("准星选择")]
    [Range(20f, 180f)]
    [Tooltip("锚点距离屏幕中心小于该像素值时，允许成为当前目标。")]
    public float focusRadiusPixels = 34f;

    [Range(0.12f, 0.8f)]
    [Tooltip(
        "Invisible world-space radius used by the centre-screen ray. " +
        "It is queried directly and cannot interfere with weapon physics."
    )]
    public float anchorAimRadius = 0.22f;

    [Min(1f)]
    [Tooltip("默认与展开状态之间的过渡速度。")]
    public float focusTransitionSpeed = 15f;

    [Range(0.15f, 0.9f)]
    [Tooltip("未被准星指向的锚点亮度。")]
    public float unfocusedAlpha = 0.48f;

    [Range(0f, 0.08f)]
    public float pulseAmount = 0.018f;

    [Min(0f)]
    public float pulseSpeed = 5f;

    [Header("按住 E 确认（阶段 2）")]
    [Tooltip(
        "测试阶段直接读取新版 Input System 的 E 键。" +
        "未来接入统一交互路由后可以关闭。"
    )]
    public bool allowDirectKeyboardInput = true;

    [Min(1f)]
    [Tooltip("超过这个距离的锚点仍可见，但不能被准星选中。")]
    public float maximumSelectionDistance = 100f;

    [Range(0.08f, 0.6f)]
    [Tooltip("准星锁定锚点后，需要持续按住 E 的真实时间。")]
    public float holdToConfirmDuration = 0.2f;

    [Range(1f, 1.8f)]
    [Tooltip("确认进度环相对于当前锚点尺寸的半径倍率。")]
    public float confirmationRingRadiusMultiplier = 1.3f;

    [Range(12, 64)]
    public int confirmationRingSegments = 36;

    [Range(0.05f, 0.3f)]
    [Tooltip("确认完成后完整圆环保持的真实时间。")]
    public float confirmedFlashDuration = 0.12f;

    [Header("传送目标锁定视觉")]
    [Tooltip(
        "完成确认后，目标位置只保留稳定的冰青锁定框。" +
        "0.5 秒的实际传送进度会显示在玩家屏幕中心。"
    )]
    public Color channelColor =
        new Color(
            0.38f,
            1f,
            0.98f,
            1f
        );

    [Tooltip("冰青锁定框在白色环境上的深色底线。")]
    public Color channelDarkUnderlayColor =
        new Color(
            0.005f,
            0.045f,
            0.065f,
            0.98f
        );

    [Range(0.9f, 1.8f)]
    [Tooltip("目标锁定框相对于展开锚点尺寸的倍率。")]
    public float channelFrameSizeMultiplier = 1.18f;

    [Range(0.08f, 0.35f)]
    public float channelBracketStubRatio = 0.19f;

    [Range(0f, 0.12f)]
    [Tooltip("稳定锁定框的轻微呼吸幅度。")]
    public float channelLockPulseAmount = 0.025f;

    [Range(0f, 20f)]
    public float channelLockPulseSpeed = 7.5f;

    [Range(0.75f, 1f)]
    [Tooltip("传送完成前，目标锁定框开始向白色增强的进度。")]
    public float channelCompletionFlashStart = 0.9f;

    [Header("防重叠")]
    [Range(12f, 60f)]
    [Tooltip("两个锚点屏幕中心小于该距离时开始错开显示。")]
    public float overlapThresholdPixels = 28f;

    [Range(4f, 30f)]
    [Tooltip("发生重叠时额外留出的间距。")]
    public float overlapExtraSpacingPixels = 12f;

    [Range(8f, 60f)]
    [Tooltip("锚点图形相对真实位置的最大视觉偏移。")]
    public float maximumOverlapOffsetPixels = 34f;

    [Range(0f, 12f)]
    [Tooltip("视觉偏移超过该值时显示指向真实位置的连接线。")]
    public float connectorStartPixels = 4f;

    [Header("极简默认造型")]
    [Range(0.18f, 0.42f)]
    public float compactRingRadiusRatio = 0.29f;

    [Range(0.08f, 0.25f)]
    public float compactTickLengthRatio = 0.15f;

    [Range(0.08f, 0.3f)]
    public float compactTailLengthRatio = 0.2f;

    [Range(0.11f, 0.24f)]
    public float compactLabelHeightRatio = 0.17f;

    [Header("准星展开造型")]
    [Range(0.48f, 0.95f)]
    public float focusedBracketWidthRatio = 0.72f;

    [Range(0.6f, 1.2f)]
    public float focusedBracketHeightRatio = 0.9f;

    [Range(0.08f, 0.25f)]
    public float focusedBracketStubRatio = 0.15f;

    [Range(0.08f, 0.28f)]
    public float focusedDashLengthRatio = 0.18f;

    [Header("可见性")]
    public bool hideOutsideCameraView = true;
    public bool hideWhenOccluded = true;

    [Header("Screen Readout")]
    public bool showAnchorReadout = true;

    [Range(0.75f, 1.5f)]
    public float readoutScale = 1f;

    [Tooltip(
        "只选择 Environment 等真正遮挡视线的层，" +
        "不要选择 Player 或 Enemy。"
    )]
    public LayerMask occlusionMask;

    [Header("调试")]
    public bool logAnchorChanges = false;

    [SerializeField]
    private int activeAnchorCount;

    [SerializeField]
    private int nextSlotIndex;

    [SerializeField]
    private int focusedSlotIndex = -1;

    [SerializeField, Range(0f, 1f)]
    private float confirmationProgress;

    [SerializeField]
    private int confirmationSlotIndex = -1;

    [SerializeField]
    private int confirmedSlotIndex = -1;

    private bool _pauseClockActive;
    private float _pauseStartedAt;

    public int ActiveAnchorCount =>
        activeAnchorCount;

    public int FocusedSlotIndex =>
        focusedSlotIndex;

    public float ConfirmationProgress =>
        confirmationProgress;

    public int ConfirmedSlotIndex =>
        confirmedSlotIndex;

    public bool HasConfirmedAnchor =>
        confirmedSlotIndex >= 0;

    /// <summary>
    /// 阶段 2 的确认完成事件。
    /// 参数：槽位索引（0=A / 1=B / 2=C）、真实传送目的地。
    /// 下一阶段的引导与传送控制器会订阅它。
    /// </summary>
    public event Action<int, Vector3> AnchorConfirmed;

    public event Action<int, Vector3, float, float> AnchorCreated;

    public event Action<int, Vector3, bool> AnchorRemoved;

    private readonly AnchorRecord[] _anchors =
        new AnchorRecord[MaximumAnchorCount];

    private readonly List<AnchorRecord> _visibleAnchors =
        new List<AnchorRecord>(MaximumAnchorCount);

    private Material _runtimeBrightMaterial;
    private Material _runtimeDarkMaterial;

    private bool _subscribed;

    private bool _confirmationLatchedUntilRelease;
    private bool _externalInteractHeld;
    private bool _hasExternalInteractState;
    private float _confirmedFlashTimer;

    private int _teleportChannelSlotIndex = -1;
    private float _teleportChannelProgress = 0f;

    private GUIStyle _anchorDistanceStyle;
    private GUIStyle _anchorTimeStyle;
    private GUIStyle _anchorSlotStyle;

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Debug.LogWarning(
                "[TeleportAnchorSystem] 场景中存在重复实例，已禁用后创建的实例。"
            );

            enabled = false;
            return;
        }

        Instance = this;
        ResolveCamera();
        ValidateRuntimeValues();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ResetConfirmationState(true);
        SetAllAnchorLinesEnabled(false);
        SendCrosshairFocus(null, 0f);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ClearAllAnchors();

        if (_runtimeBrightMaterial != null)
        {
            Destroy(_runtimeBrightMaterial);
        }

        if (_runtimeDarkMaterial != null)
        {
            Destroy(_runtimeDarkMaterial);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        ResolveCamera();

        if (AILURONEGameplayActionGate.IsPaused)
        {
            if (!_pauseClockActive)
            {
                _pauseClockActive = true;
                _pauseStartedAt = Time.unscaledTime;
            }

            return;
        }

        if (_pauseClockActive)
        {
            ShiftAnchorClocks(
                Mathf.Max(0f, Time.unscaledTime - _pauseStartedAt)
            );

            _pauseClockActive = false;
        }

        if (targetCamera == null)
        {
            focusedSlotIndex = -1;
            SetAllAnchorLinesEnabled(false);
            SendCrosshairFocus(null, 0f);
            return;
        }

        float now = Time.unscaledTime;

        RemoveExpiredAnchors(now);
        PrepareVisibleAnchors(now);
        SelectFocusedAnchor();
        UpdateConfirmationInput();
        ResolveScreenOverlaps();
        UpdateVisibleAnchors(now);
        RefreshActiveCount();
    }

    private void ShiftAnchorClocks(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        for (int index = 0; index < _anchors.Length; index++)
        {
            AnchorRecord anchor = _anchors[index];

            if (anchor == null)
            {
                continue;
            }

            anchor.revealTime += duration;
            anchor.expireTime += duration;
        }
    }

    private void OnGUI()
    {
        if (AILURONEGameplayActionGate.IsPaused ||
            !showAnchorReadout ||
            targetCamera == null ||
            Event.current.type != EventType.Repaint)
        {
            return;
        }

        EnsureReadoutStyles();

        float scale =
            Mathf.Clamp(
                Screen.height / 1080f,
                0.75f,
                1.5f) *
            readoutScale;

        float now = Time.unscaledTime;

        for (int index = 0;
             index < _anchors.Length;
             index++)
        {
            AnchorRecord anchor = _anchors[index];

            if (anchor == null ||
                !anchor.visibleThisFrame ||
                now < anchor.revealTime ||
                anchor.focusBlend <= 0.01f)
            {
                continue;
            }

            DrawAnchorReadout(anchor, now, scale);
        }
    }

    private void EnsureReadoutStyles()
    {
        if (_anchorDistanceStyle != null)
        {
            return;
        }

        Font font = Resources.Load<Font>(
            "Fonts/AILURONE/Rajdhani-SemiBold");

        _anchorDistanceStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = font,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Overflow
        };

        _anchorDistanceStyle.normal.textColor =
            new Color(0.94f, 0.99f, 1f, 1f);

        _anchorTimeStyle = new GUIStyle(_anchorDistanceStyle)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 12,
            fontStyle = FontStyle.Normal
        };

        _anchorTimeStyle.normal.textColor =
            new Color(0.68f, 0.92f, 0.94f, 0.92f);

        _anchorSlotStyle = new GUIStyle(_anchorTimeStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };

        _anchorSlotStyle.normal.textColor = channelColor;
    }

    private void DrawAnchorReadout(
        AnchorRecord anchor,
        float now,
        float scale)
    {
        Vector2 screenPosition =
            anchor.rawScreenPosition +
            anchor.screenOffsetPixels;

        float reveal = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(anchor.focusBlend)
        );
        float fullWidth = 96f * scale;
        float width = fullWidth * reveal;
        float height = 45f * scale;
        float markerSize =
            Mathf.Lerp(
                GetDistanceBasedSize(anchor.distanceToCamera),
                focusedSizePixels,
                anchor.focusBlend) *
            scale;

        Rect panel = new Rect(
            screenPosition.x - fullWidth * 0.5f,
            Screen.height - screenPosition.y + markerSize * 0.72f,
            width,
            height);

        DrawGUIRect(
            panel,
            new Color(
                0.008f,
                0.018f,
                0.025f,
                0.82f * reveal
            ));

        DrawGUIRect(
            new Rect(panel.x, panel.y, 2f * scale, panel.height),
            new Color(
                channelColor.r,
                channelColor.g,
                channelColor.b,
                channelColor.a * reveal
            ));

        float contentAlpha = Mathf.InverseLerp(
            0.55f,
            1f,
            reveal
        );

        if (contentAlpha <= 0f)
        {
            return;
        }

        float remaining =
            Mathf.Max(0f, anchor.expireTime - now);

        float lifetime01 =
            Mathf.Clamp01(
                remaining /
                Mathf.Max(0.5f, anchorLifetime));

        _anchorDistanceStyle.fontSize =
            Mathf.RoundToInt(20f * scale);
        _anchorTimeStyle.fontSize =
            Mathf.RoundToInt(12f * scale);
        _anchorSlotStyle.fontSize =
            Mathf.RoundToInt(12f * scale);

        Color previousGUIColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, contentAlpha);

        GUI.Label(
            new Rect(
                panel.x + 10f * scale,
                panel.y + 1f * scale,
                panel.width - 20f * scale,
                25f * scale),
            $"{Mathf.RoundToInt(anchor.distanceToCamera)}M",
            _anchorDistanceStyle);

        GUI.Label(
            new Rect(
                panel.x + 10f * scale,
                panel.y + 25f * scale,
                28f * scale,
                14f * scale),
            GetSlotLabel(anchor.slotIndex),
            _anchorSlotStyle);

        Color previousTimeColor =
            _anchorTimeStyle.normal.textColor;

        if (remaining <= 3f)
        {
            _anchorTimeStyle.normal.textColor =
                new Color(1f, 0.42f, 0.30f, 1f);
        }

        GUI.Label(
            new Rect(
                panel.x + 36f * scale,
                panel.y + 25f * scale,
                panel.width - 46f * scale,
                14f * scale),
            $"{remaining:00.0}S",
            _anchorTimeStyle);

        _anchorTimeStyle.normal.textColor = previousTimeColor;
        GUI.color = previousGUIColor;

        Rect track = new Rect(
            panel.x + 10f * scale,
            panel.yMax - 5f * scale,
            panel.width - 20f * scale,
            2f * scale);

        DrawGUIRect(
            track,
            new Color(1f, 1f, 1f, 0.20f * contentAlpha));

        Color fillColor = remaining <= 3f
            ? new Color(1f, 0.32f, 0.24f, 1f)
            : channelColor;

        fillColor.a *= contentAlpha;

        DrawGUIRect(
            new Rect(
                track.x,
                track.y,
                track.width * lifetime01,
                track.height),
            fillColor);
    }

    private static void DrawGUIRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    // =========================================================
    // 敌人死亡事件
    // =========================================================

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        PlayerWeapon.AnyEnemyShotResolved +=
            HandleEnemyShotResolved;

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        PlayerWeapon.AnyEnemyShotResolved -=
            HandleEnemyShotResolved;

        _subscribed = false;
    }

    private void HandleEnemyShotResolved(
        EnemyShotResult shotResult
    )
    {
        if (!shotResult.FiredAsAds ||
            !shotResult.Killed ||
            !shotResult.DirectPlayerKill ||
            shotResult.Target == null)
        {
            return;
        }

        EnemyHitMarker sourceMarker =
            shotResult.Target.GetComponent<EnemyHitMarker>();

        Vector3 killPosition =
            shotResult.Target.transform.position;

        Vector3 visualPosition =
            killPosition +
            Vector3.up * 1.5f;

        float revealDelay = 0.18f;

        Color brightColor =
            new Color(
                1f,
                0.07f,
                0.28f,
                1f
            );

        Color darkColor =
            new Color(
                0.125f,
                0f,
                0.03f,
                0.98f
            );

        float brightWidth = 2.4f;
        float darkWidth = 4.2f;

        Material sourceMaterial = null;

        if (sourceMarker != null)
        {
            sourceMarker.TryGetDirectKillTransitionPosition(
                out visualPosition
            );

            revealDelay =
                Mathf.Max(
                    0f,
                    sourceMarker.DirectKillDuration -
                    transitionOverlap
                );

            brightColor =
                sourceMarker.directKillColor;

            darkColor =
                sourceMarker.directKillDarkUnderlayColor;

            brightWidth =
                sourceMarker.brightLineWidthPixels;

            darkWidth =
                sourceMarker.darkLineWidthPixels;

            sourceMaterial =
                sourceMarker.materialTemplate;
        }

        EnsureRuntimeMaterials(sourceMaterial);

        if (_runtimeBrightMaterial == null ||
            _runtimeDarkMaterial == null)
        {
            return;
        }

        CreateOrReplaceAnchor(
            nextSlotIndex,
            killPosition,
            visualPosition,
            revealDelay,
            brightColor,
            darkColor,
            brightWidth,
            darkWidth
        );

        nextSlotIndex =
            (nextSlotIndex + 1) %
            MaximumAnchorCount;
    }

    // =========================================================
    // 锚点生命周期
    // =========================================================

    private void CreateOrReplaceAnchor(
        int slotIndex,
        Vector3 teleportDestination,
        Vector3 visualPosition,
        float revealDelay,
        Color brightColor,
        Color darkColor,
        float brightWidthPixels,
        float darkWidthPixels
    )
    {
        int safeSlot =
            Mathf.Clamp(
                slotIndex,
                0,
                MaximumAnchorCount - 1
            );

        RemoveAnchor(safeSlot);

        float now = Time.unscaledTime;
        float safeRevealDelay =
            Mathf.Max(0f, revealDelay);
        float safeLifetime =
            Mathf.Max(0.5f, anchorLifetime);

        AnchorRecord anchor =
            new AnchorRecord
            {
                slotIndex = safeSlot,
                teleportDestination =
                    teleportDestination,
                visualPosition =
                    visualPosition,
                revealTime =
                    now + safeRevealDelay,
                expireTime =
                    now +
                    safeRevealDelay +
                    safeLifetime,
                brightWidthPixels =
                    Mathf.Max(
                        0.5f,
                        brightWidthPixels
                    ),
                darkWidthPixels =
                    Mathf.Max(
                        brightWidthPixels + 0.25f,
                        darkWidthPixels
                    ),
                brightColor =
                    brightColor,
                darkColor =
                    darkColor
            };

        CreateAnchorVisual(anchor);

        _anchors[safeSlot] = anchor;
        RefreshActiveCount();

        AnchorCreated?.Invoke(
            safeSlot,
            visualPosition,
            safeRevealDelay,
            safeLifetime
        );

        if (logAnchorChanges)
        {
            Debug.Log(
                $"[TeleportAnchorSystem] 创建锚点 " +
                $"{GetSlotLabel(safeSlot)}，" +
                $"目标位置：{teleportDestination}"
            );
        }
    }

    private void RemoveExpiredAnchors(
        float now
    )
    {
        for (int i = 0;
             i < _anchors.Length;
             i++)
        {
            AnchorRecord anchor =
                _anchors[i];

            if (anchor != null &&
                now >= anchor.expireTime)
            {
                RemoveAnchor(i, true);
            }
        }
    }

    private void RemoveAnchor(
        int slotIndex,
        bool expiredNaturally = false
    )
    {
        if (slotIndex < 0 ||
            slotIndex >= _anchors.Length)
        {
            return;
        }

        AnchorRecord anchor =
            _anchors[slotIndex];

        if (anchor == null)
        {
            return;
        }

        AnchorRemoved?.Invoke(
            slotIndex,
            anchor.visualPosition,
            expiredNaturally
        );

        if (anchor.rootObject != null)
        {
            Destroy(anchor.rootObject);
        }

        _anchors[slotIndex] = null;

        if (focusedSlotIndex == slotIndex)
        {
            focusedSlotIndex = -1;
        }

        if (confirmationSlotIndex == slotIndex ||
            confirmedSlotIndex == slotIndex)
        {
            ResetConfirmationState(true);
        }

        if (_teleportChannelSlotIndex == slotIndex)
        {
            ClearTeleportChannelProgress();
        }

        if (logAnchorChanges)
        {
            Debug.Log(
                $"[TeleportAnchorSystem] 移除锚点 " +
                $"{GetSlotLabel(slotIndex)}。"
            );
        }

        RefreshActiveCount();
    }

    public void ClearAllAnchors()
    {
        for (int i = 0;
             i < _anchors.Length;
             i++)
        {
            RemoveAnchor(i);
        }

        nextSlotIndex = 0;
        focusedSlotIndex = -1;
        ResetConfirmationState(true);
        RefreshActiveCount();
    }

    public bool TryGetAnchorDestination(
        int slotIndex,
        out Vector3 destination
    )
    {
        destination = Vector3.zero;

        if (slotIndex < 0 ||
            slotIndex >= _anchors.Length)
        {
            return false;
        }

        AnchorRecord anchor =
            _anchors[slotIndex];

        if (anchor == null ||
            Time.unscaledTime <
            anchor.revealTime ||
            Time.unscaledTime >=
            anchor.expireTime)
        {
            return false;
        }

        destination =
            anchor.teleportDestination;

        return true;
    }

    public bool TryGetFocusedAnchorDestination(
        out int slotIndex,
        out Vector3 destination
    )
    {
        slotIndex = focusedSlotIndex;
        destination = Vector3.zero;

        if (focusedSlotIndex < 0)
        {
            return false;
        }

        return TryGetAnchorDestination(
            focusedSlotIndex,
            out destination
        );
    }

    public bool ConsumeAnchor(
        int slotIndex
    )
    {
        if (!TryGetAnchorDestination(
                slotIndex,
                out _
            ))
        {
            return false;
        }

        RemoveAnchor(slotIndex);
        return true;
    }

    public void SetTeleportChannelProgress(
        int slotIndex,
        float progress
    )
    {
        if (slotIndex < 0 ||
            slotIndex >= MaximumAnchorCount)
        {
            ClearTeleportChannelProgress();
            return;
        }

        _teleportChannelSlotIndex =
            slotIndex;

        _teleportChannelProgress =
            Mathf.Clamp01(progress);

        /*
         * 一旦进入 0.5 秒传送引导，
         * 立即结束红洋红确认圆环的完成闪光。
         * 后续只显示独立的冰青空间重构视觉。
         */
        _confirmedFlashTimer = 0f;
        confirmedSlotIndex = -1;
        confirmationProgress = 0f;
        confirmationSlotIndex = -1;
    }

    public void ClearTeleportChannelProgress()
    {
        _teleportChannelSlotIndex = -1;
        _teleportChannelProgress = 0f;
    }

    private void RefreshActiveCount()
    {
        int count = 0;
        float now = Time.unscaledTime;

        for (int i = 0;
             i < _anchors.Length;
             i++)
        {
            AnchorRecord anchor =
                _anchors[i];

            if (anchor != null &&
                now < anchor.expireTime)
            {
                count++;
            }
        }

        activeAnchorCount = count;
    }

    // =========================================================
    // 每帧选择与防重叠
    // =========================================================

    private void PrepareVisibleAnchors(
        float now
    )
    {
        _visibleAnchors.Clear();

        Vector2 screenCenter =
            new Vector2(
                Screen.width * 0.5f,
                Screen.height * 0.5f
            );

        for (int i = 0;
             i < _anchors.Length;
             i++)
        {
            AnchorRecord anchor =
                _anchors[i];

            if (anchor == null)
            {
                continue;
            }

            anchor.visibleThisFrame = false;
            anchor.selectedThisFrame = false;
            anchor.screenOffsetPixels = Vector2.zero;

            if (now < anchor.revealTime ||
                !IsVisible(anchor.visualPosition))
            {
                SetAnchorLinesEnabled(
                    anchor,
                    false
                );

                continue;
            }

            Vector3 screenPoint =
                targetCamera.WorldToScreenPoint(
                    anchor.visualPosition
                );

            anchor.rawScreenPosition =
                new Vector2(
                    screenPoint.x,
                    screenPoint.y
                );

            anchor.screenCenterDistance =
                Vector2.Distance(
                    anchor.rawScreenPosition,
                    screenCenter
                );

            anchor.distanceToCamera =
                Vector3.Distance(
                    targetCamera.transform.position,
                    anchor.visualPosition
                );

            anchor.visibleThisFrame = true;
            _visibleAnchors.Add(anchor);
        }
    }

    private void SelectFocusedAnchor()
    {
        focusedSlotIndex = -1;

        AnchorRecord directAnchor = null;
        float directHitDistance = float.PositiveInfinity;

        Ray centreRay =
            targetCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

        for (int i = 0;
             i < _visibleAnchors.Count;
             i++)
        {
            AnchorRecord candidate =
                _visibleAnchors[i];

            if (candidate.distanceToCamera >
                    maximumSelectionDistance ||
                candidate.aimCollider == null)
            {
                continue;
            }

            if (candidate.aimCollider.Raycast(
                    centreRay,
                    out RaycastHit hit,
                    maximumSelectionDistance
                ) &&
                hit.distance < directHitDistance)
            {
                directHitDistance = hit.distance;
                directAnchor = candidate;
            }
        }

        if (directAnchor != null)
        {
            directAnchor.selectedThisFrame = true;
            focusedSlotIndex = directAnchor.slotIndex;
            SendCrosshairFocus(directAnchor, 1f);
            return;
        }

        AnchorRecord bestAnchor = null;
        float bestDistance =
            focusRadiusPixels;

        for (int i = 0;
             i < _visibleAnchors.Count;
             i++)
        {
            AnchorRecord candidate =
                _visibleAnchors[i];

            if (candidate.distanceToCamera >
                maximumSelectionDistance)
            {
                continue;
            }

            if (candidate.screenCenterDistance <=
                bestDistance)
            {
                bestDistance =
                    candidate.screenCenterDistance;

                bestAnchor =
                    candidate;
            }
        }

        if (bestAnchor != null)
        {
            bestAnchor.selectedThisFrame = true;
            focusedSlotIndex =
                bestAnchor.slotIndex;

            float proximity =
                1f -
                Mathf.Clamp01(
                    bestDistance /
                    Mathf.Max(1f, focusRadiusPixels)
                );

            SendCrosshairFocus(
                bestAnchor,
                Mathf.Lerp(0.65f, 1f, proximity)
            );
            return;
        }

        SendCrosshairFocus(null, 0f);
    }

    private void SendCrosshairFocus(
        AnchorRecord anchor,
        float strength
    )
    {
        CrosshairController crosshair =
            CrosshairController.Instance;

        if (crosshair == null)
        {
            return;
        }

        if (anchor == null)
        {
            crosshair.SetTeleportAnchorFocus(
                false,
                Vector2.zero,
                0f
            );

            return;
        }

        Vector2 screenCenter =
            new Vector2(
                Screen.width * 0.5f,
                Screen.height * 0.5f
            );

        crosshair.SetTeleportAnchorFocus(
            true,
            anchor.rawScreenPosition - screenCenter,
            Mathf.Clamp01(strength)
        );
    }


    // =========================================================
    // 按住 E 确认
    // =========================================================

    /// <summary>
    /// 未来统一交互路由可以调用此接口。
    /// 一旦调用，外部输入会暂时覆盖脚本直接读取 E 键。
    /// </summary>
    public void SetInteractHeld(
        bool held
    )
    {
        _hasExternalInteractState = true;
        _externalInteractHeld = held;
    }

    /// <summary>
    /// 重新允许脚本直接读取键盘 E。
    /// </summary>
    public void ReleaseExternalInteractOverride()
    {
        _hasExternalInteractState = false;
        _externalInteractHeld = false;
    }

    public void CancelConfirmation()
    {
        ResetConfirmationState(false);
    }

    private void UpdateConfirmationInput()
    {
        _confirmedFlashTimer =
            Mathf.Max(
                0f,
                _confirmedFlashTimer -
                Time.unscaledDeltaTime
            );

        bool interactHeld =
            ResolveInteractHeld();

        if (!interactHeld)
        {
            _confirmationLatchedUntilRelease = false;

            if (_confirmedFlashTimer <= 0f)
            {
                confirmationProgress = 0f;
                confirmationSlotIndex = -1;
                confirmedSlotIndex = -1;
            }

            return;
        }

        if (_confirmationLatchedUntilRelease)
        {
            return;
        }

        if (focusedSlotIndex < 0 ||
            focusedSlotIndex >= _anchors.Length)
        {
            ResetConfirmationProgressOnly();
            return;
        }

        AnchorRecord focusedAnchor =
            _anchors[focusedSlotIndex];

        if (focusedAnchor == null ||
            !focusedAnchor.visibleThisFrame ||
            focusedAnchor.distanceToCamera >
            maximumSelectionDistance)
        {
            ResetConfirmationProgressOnly();
            return;
        }

        if (confirmationSlotIndex !=
            focusedSlotIndex)
        {
            confirmationSlotIndex =
                focusedSlotIndex;

            confirmationProgress = 0f;
            confirmedSlotIndex = -1;
        }

        float safeDuration =
            Mathf.Max(
                0.01f,
                holdToConfirmDuration
            );

        confirmationProgress =
            Mathf.Clamp01(
                confirmationProgress +
                Time.unscaledDeltaTime /
                safeDuration
            );

        if (confirmationProgress < 1f)
        {
            return;
        }

        ConfirmFocusedAnchor(
            focusedAnchor
        );
    }

    private bool ResolveInteractHeld()
    {
        if (!AILURONEGameplayActionGate.AllowsGameplayActions)
        {
            return false;
        }

        if (_hasExternalInteractState)
        {
            return _externalInteractHeld;
        }

        if (!allowDirectKeyboardInput ||
            Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.eKey.isPressed;
    }

    private void ConfirmFocusedAnchor(
        AnchorRecord anchor
    )
    {
        if (!AILURONEGameplayActionGate.AllowsGameplayActions ||
            anchor == null)
        {
            return;
        }

        confirmationProgress = 1f;
        confirmationSlotIndex =
            anchor.slotIndex;

        confirmedSlotIndex =
            anchor.slotIndex;

        _confirmedFlashTimer =
            confirmedFlashDuration;

        _confirmationLatchedUntilRelease = true;

        AnchorConfirmed?.Invoke(
            anchor.slotIndex,
            anchor.teleportDestination
        );

        if (logAnchorChanges)
        {
            Debug.Log(
                $"[TeleportAnchorSystem] 已确认锚点 " +
                $"{GetSlotLabel(anchor.slotIndex)}，" +
                $"目的地：{anchor.teleportDestination}"
            );
        }
    }

    private void ResetConfirmationProgressOnly()
    {
        confirmationProgress = 0f;
        confirmationSlotIndex = -1;
        confirmedSlotIndex = -1;
        _confirmedFlashTimer = 0f;
    }

    private void ResetConfirmationState(
        bool clearLatch
    )
    {
        ResetConfirmationProgressOnly();

        if (clearLatch)
        {
            _confirmationLatchedUntilRelease = false;
        }
    }

    private void ResolveScreenOverlaps()
    {
        SortVisibleAnchorsByPriority();

        for (int iteration = 0;
             iteration < 2;
             iteration++)
        {
            for (int i = 0;
                 i < _visibleAnchors.Count;
                 i++)
            {
                AnchorRecord current =
                    _visibleAnchors[i];

                Vector2 currentPosition =
                    current.rawScreenPosition +
                    current.screenOffsetPixels;

                for (int j = 0;
                     j < i;
                     j++)
                {
                    AnchorRecord higherPriority =
                        _visibleAnchors[j];

                    Vector2 higherPosition =
                        higherPriority.rawScreenPosition +
                        higherPriority.screenOffsetPixels;

                    Vector2 difference =
                        currentPosition -
                        higherPosition;

                    float distance =
                        difference.magnitude;

                    if (distance >=
                        overlapThresholdPixels)
                    {
                        continue;
                    }

                    Vector2 direction;

                    if (distance > 0.001f)
                    {
                        direction =
                            difference / distance;
                    }
                    else
                    {
                        float angle =
                            (
                                current.slotIndex *
                                137.5f +
                                iteration *
                                31f
                            ) *
                            Mathf.Deg2Rad;

                        direction =
                            new Vector2(
                                Mathf.Cos(angle),
                                Mathf.Sin(angle)
                            );
                    }

                    float requiredPush =
                        overlapThresholdPixels -
                        distance +
                        overlapExtraSpacingPixels;

                    current.screenOffsetPixels +=
                        direction * requiredPush;

                    current.screenOffsetPixels =
                        Vector2.ClampMagnitude(
                            current.screenOffsetPixels,
                            maximumOverlapOffsetPixels
                        );

                    currentPosition =
                        current.rawScreenPosition +
                        current.screenOffsetPixels;
                }
            }
        }
    }

    private void SortVisibleAnchorsByPriority()
    {
        _visibleAnchors.Sort(
            CompareAnchorPriority
        );
    }

    private int CompareAnchorPriority(
        AnchorRecord left,
        AnchorRecord right
    )
    {
        if (left.selectedThisFrame !=
            right.selectedThisFrame)
        {
            return left.selectedThisFrame
                ? -1
                : 1;
        }

        int centerComparison =
            left.screenCenterDistance.CompareTo(
                right.screenCenterDistance
            );

        if (centerComparison != 0)
        {
            return centerComparison;
        }

        return left.slotIndex.CompareTo(
            right.slotIndex
        );
    }

    private void UpdateVisibleAnchors(
        float now
    )
    {
        for (int i = 0;
             i < _anchors.Length;
             i++)
        {
            AnchorRecord anchor =
                _anchors[i];

            if (anchor == null)
            {
                continue;
            }

            float targetFocus =
                anchor.selectedThisFrame
                    ? 1f
                    : 0f;

            float focusInterpolation =
                1f -
                Mathf.Exp(
                    -focusTransitionSpeed *
                    Time.unscaledDeltaTime
                );

            anchor.focusBlend =
                Mathf.Lerp(
                    anchor.focusBlend,
                    targetFocus,
                    focusInterpolation
                );

            if (!anchor.visibleThisFrame)
            {
                continue;
            }

            UpdateAnchorVisual(
                anchor,
                now
            );
        }
    }

    // =========================================================
    // 锚点图形
    // =========================================================

    private void CreateAnchorVisual(
        AnchorRecord anchor
    )
    {
        GameObject root =
            new GameObject(
                $"TP_Anchor_{GetSlotLabel(anchor.slotIndex)}"
            );

        anchor.rootObject = root;
        anchor.rootTransform = root.transform;
        anchor.rootTransform.position =
            anchor.visualPosition;

        GameObject aimVolume =
            new GameObject("AimVolume");

        aimVolume.layer = 2;
        aimVolume.transform.SetParent(
            root.transform,
            false
        );

        anchor.aimCollider =
            aimVolume.AddComponent<SphereCollider>();

        anchor.aimCollider.isTrigger = true;
        anchor.aimCollider.radius = anchorAimRadius;

        Vector3[][] compactShapes =
            BuildCompactShapes(
                anchor.slotIndex,
                1f
            );

        Vector3[][] detailShapes =
            BuildDetailShapes(1f);

        CreateLinePairs(
            root.transform,
            compactShapes.Length,
            "Compact",
            anchor.compactDarkLines,
            anchor.compactBrightLines,
            10,
            11
        );

        CreateLinePairs(
            root.transform,
            detailShapes.Length,
            "Detail",
            anchor.detailDarkLines,
            anchor.detailBrightLines,
            12,
            13
        );

        anchor.connectorDarkLine =
            CreateLine(
                root.transform,
                "ConnectorDark",
                _runtimeDarkMaterial,
                8
            );

        anchor.connectorBrightLine =
            CreateLine(
                root.transform,
                "ConnectorBright",
                _runtimeBrightMaterial,
                9
            );

        anchor.confirmDarkLine =
            CreateLine(
                root.transform,
                "ConfirmDark",
                _runtimeDarkMaterial,
                14
            );

        anchor.confirmBrightLine =
            CreateLine(
                root.transform,
                "ConfirmBright",
                _runtimeBrightMaterial,
                15
            );

        Vector3[][] channelShapes =
            BuildTeleportChannelShapes(
                1f,
                Vector3.zero
            );

        CreateLinePairs(
            root.transform,
            channelShapes.Length,
            "Channel",
            anchor.channelDarkLines,
            anchor.channelBrightLines,
            16,
            17
        );

        SetAnchorLinesEnabled(
            anchor,
            false
        );
    }

    private void CreateLinePairs(
        Transform parent,
        int count,
        string prefix,
        List<LineRenderer> darkLines,
        List<LineRenderer> brightLines,
        int darkSortingOrder,
        int brightSortingOrder
    )
    {
        for (int i = 0;
             i < count;
             i++)
        {
            darkLines.Add(
                CreateLine(
                    parent,
                    $"{prefix}Dark_{i}",
                    _runtimeDarkMaterial,
                    darkSortingOrder
                )
            );

            brightLines.Add(
                CreateLine(
                    parent,
                    $"{prefix}Bright_{i}",
                    _runtimeBrightMaterial,
                    brightSortingOrder
                )
            );
        }
    }

    private LineRenderer CreateLine(
        Transform parent,
        string objectName,
        Material material,
        int sortingOrder
    )
    {
        GameObject lineObject =
            new GameObject(objectName);

        lineObject.transform.SetParent(
            parent,
            false
        );

        LineRenderer line =
            lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = false;
        line.alignment =
            LineAlignment.TransformZ;

        line.textureMode =
            LineTextureMode.Stretch;

        line.numCornerVertices = 1;
        line.numCapVertices = 1;

        line.shadowCastingMode =
            ShadowCastingMode.Off;

        line.receiveShadows = false;
        line.lightProbeUsage =
            LightProbeUsage.Off;

        line.reflectionProbeUsage =
            ReflectionProbeUsage.Off;

        line.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;

        line.sharedMaterial = material;
        line.sortingOrder = sortingOrder;
        line.enabled = false;

        return line;
    }

    private void UpdateAnchorVisual(
        AnchorRecord anchor,
        float now
    )
    {
        if (anchor.rootTransform == null)
        {
            return;
        }

        anchor.rootTransform.position =
            anchor.visualPosition;

        anchor.rootTransform.rotation =
            targetCamera.transform.rotation;

        SetAnchorLinesEnabled(
            anchor,
            true
        );

        float age =
            now - anchor.revealTime;

        float remaining =
            anchor.expireTime - now;

        float appearT =
            Mathf.Clamp01(
                age /
                Mathf.Max(
                    MinimumBirthDuration,
                    appearDuration
                )
            );

        float fadeT =
            remaining < fadeDuration
                ? Mathf.Clamp01(
                    remaining /
                    Mathf.Max(
                        0.01f,
                        fadeDuration
                    )
                )
                : 1f;

        float lifecycleAlpha =
            Smooth01(appearT) *
            Smooth01(fadeT);

        float birthStartScale =
            Mathf.Min(
                appearStartScale,
                0.40f
            );

        float appearScale;

        if (appearT < BirthOvershootPoint)
        {
            float expandT =
                Mathf.InverseLerp(
                    0f,
                    BirthOvershootPoint,
                    appearT
                );

            appearScale =
                Mathf.Lerp(
                    birthStartScale,
                    BirthOvershootScale,
                    Smooth01(expandT)
                );
        }
        else
        {
            float settleT =
                Mathf.InverseLerp(
                    BirthOvershootPoint,
                    1f,
                    appearT
                );

            appearScale =
                Mathf.Lerp(
                    BirthOvershootScale,
                    1f,
                    Smooth01(settleT)
                );
        }

        float fadeScale =
            remaining < fadeDuration
                ? Mathf.Lerp(
                    Mathf.Min(
                        fadeEndScale,
                        ExpiryCollapseEndScale
                    ),
                    1f,
                    Smooth01(fadeT)
                )
                : 1f;

        float expiryWarning =
            Smooth01(
                1f -
                Mathf.Clamp01(
                    remaining /
                    ExpiryWarningDuration
                )
            );

        float expiryPulse01 =
            0.5f +
            0.5f *
            Mathf.Sin(
                now *
                Mathf.Lerp(
                    6f,
                    12f,
                    expiryWarning
                ) +
                anchor.slotIndex * 1.7f
            );

        float pulse =
            1f +
            Mathf.Sin(
                now * pulseSpeed +
                anchor.slotIndex * 1.7f
            ) *
            pulseAmount;

        float compactSize =
            GetDistanceBasedSize(
                anchor.distanceToCamera
            );

        float sizePixels =
            Mathf.Lerp(
                compactSize,
                focusedSizePixels,
                anchor.focusBlend
            ) *
            appearScale *
            fadeScale *
            pulse *
            (
                1f +
                expiryWarning *
                expiryPulse01 *
                0.04f
            );

        float worldPerPixel =
            WorldUnitsPerPixel(
                anchor.visualPosition
            );

        Vector3 localOffset =
            new Vector3(
                anchor.screenOffsetPixels.x *
                worldPerPixel,
                anchor.screenOffsetPixels.y *
                worldPerPixel,
                0f
            );

        UpdateAnchorGeometry(
            anchor,
            worldPerPixel,
            sizePixels,
            localOffset
        );

        float compactAlpha =
            lifecycleAlpha *
            Mathf.Lerp(
                unfocusedAlpha,
                1f,
                anchor.focusBlend
            );

        float birthDetail =
            Mathf.Sin(appearT * Mathf.PI) *
            0.55f;

        float detailAlpha =
            lifecycleAlpha *
            Mathf.Max(
                anchor.focusBlend,
                birthDetail,
                expiryWarning *
                expiryPulse01 *
                0.20f
            );

        bool isChannelTarget =
            anchor.slotIndex ==
            _teleportChannelSlotIndex;

        if (isChannelTarget)
        {
            /*
             * 传送引导开始后，红洋红目标图形退居背景，
             * 只留下稳定的冰青目的地锁定框。
             */
            compactAlpha *= 0.32f;
            detailAlpha = 0f;
            expiryWarning = 0f;
        }

        UpdateAnchorAppearance(
            anchor,
            worldPerPixel,
            compactAlpha,
            detailAlpha,
            expiryWarning,
            expiryPulse01
        );

        UpdateConnector(
            anchor,
            worldPerPixel,
            localOffset,
            lifecycleAlpha
        );

        UpdateConfirmationVisual(
            anchor,
            worldPerPixel,
            sizePixels,
            localOffset,
            lifecycleAlpha
        );

        UpdateTeleportChannelVisual(
            anchor,
            worldPerPixel,
            sizePixels,
            localOffset,
            lifecycleAlpha
        );
    }

    private float GetDistanceBasedSize(
        float distance
    )
    {
        if (distance <= nearDistance)
        {
            return nearSizePixels;
        }

        if (distance <= middleDistance)
        {
            float t =
                Mathf.InverseLerp(
                    nearDistance,
                    middleDistance,
                    distance
                );

            return Mathf.Lerp(
                nearSizePixels,
                middleSizePixels,
                t
            );
        }

        if (distance <= farDistance)
        {
            float t =
                Mathf.InverseLerp(
                    middleDistance,
                    farDistance,
                    distance
                );

            return Mathf.Lerp(
                middleSizePixels,
                farSizePixels,
                t
            );
        }

        float farBlend =
            Mathf.Clamp01(
                (distance - farDistance) /
                Mathf.Max(
                    1f,
                    farDistance
                )
            );

        return Mathf.Lerp(
            farSizePixels,
            minimumSizePixels,
            farBlend
        );
    }

    private void UpdateAnchorGeometry(
        AnchorRecord anchor,
        float worldPerPixel,
        float sizePixels,
        Vector3 localOffset
    )
    {
        float size =
            sizePixels *
            worldPerPixel;

        Vector3[][] compactShapes =
            BuildCompactShapes(
                anchor.slotIndex,
                size
            );

        Vector3[][] detailShapes =
            BuildDetailShapes(size);

        TranslateShapes(
            compactShapes,
            localOffset
        );

        TranslateShapes(
            detailShapes,
            localOffset
        );

        ApplyShapes(
            anchor.compactDarkLines,
            compactShapes
        );

        ApplyShapes(
            anchor.compactBrightLines,
            compactShapes
        );

        ApplyShapes(
            anchor.detailDarkLines,
            detailShapes
        );

        ApplyShapes(
            anchor.detailBrightLines,
            detailShapes
        );
    }

    private Vector3[][] BuildCompactShapes(
        int slotIndex,
        float size
    )
    {
        List<Vector3[]> shapes =
            new List<Vector3[]>();

        float radius =
            size * compactRingRadiusRatio;

        float gapAngle =
            27f * Mathf.Deg2Rad;

        shapes.Add(
            BuildArcShape(
                radius,
                gapAngle,
                Mathf.PI - gapAngle,
                10
            )
        );

        shapes.Add(
            BuildArcShape(
                radius,
                Mathf.PI + gapAngle,
                Mathf.PI * 2f - gapAngle,
                10
            )
        );

        float tickLength =
            size * compactTickLengthRatio;

        shapes.Add(
            new[]
            {
                new Vector3(
                    -radius - tickLength,
                    0f,
                    0f
                ),
                new Vector3(
                    -radius + tickLength * 0.25f,
                    0f,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                new Vector3(
                    radius - tickLength * 0.25f,
                    0f,
                    0f
                ),
                new Vector3(
                    radius + tickLength,
                    0f,
                    0f
                )
            }
        );

        float centerMark =
            size * 0.045f;

        shapes.Add(
            new[]
            {
                new Vector3(
                    -centerMark,
                    0f,
                    0f
                ),
                new Vector3(
                    centerMark,
                    0f,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                new Vector3(
                    0f,
                    -centerMark,
                    0f
                ),
                new Vector3(
                    0f,
                    centerMark,
                    0f
                )
            }
        );

        float tailStart =
            -radius -
            size * 0.055f;

        float tailEnd =
            tailStart -
            size * compactTailLengthRatio;

        shapes.Add(
            new[]
            {
                new Vector3(
                    0f,
                    tailStart,
                    0f
                ),
                new Vector3(
                    0f,
                    tailEnd,
                    0f
                )
            }
        );

        float chevronHalf =
            size * 0.085f;

        float chevronHeight =
            size * 0.075f;

        shapes.Add(
            new[]
            {
                new Vector3(
                    -chevronHalf,
                    tailEnd + chevronHeight,
                    0f
                ),
                new Vector3(
                    0f,
                    tailEnd,
                    0f
                ),
                new Vector3(
                    chevronHalf,
                    tailEnd + chevronHeight,
                    0f
                )
            }
        );

        AddLabelShapes(
            shapes,
            slotIndex,
            size,
            radius +
            size * 0.22f,
            compactLabelHeightRatio
        );

        return shapes.ToArray();
    }

    private Vector3[][] BuildDetailShapes(
        float size
    )
    {
        List<Vector3[]> shapes =
            new List<Vector3[]>();

        float halfWidth =
            size *
            focusedBracketWidthRatio *
            0.5f;

        float halfHeight =
            size *
            focusedBracketHeightRatio *
            0.5f;

        float stub =
            size *
            focusedBracketStubRatio;

        shapes.Add(
            new[]
            {
                new Vector3(
                    -halfWidth + stub,
                    halfHeight,
                    0f
                ),
                new Vector3(
                    -halfWidth,
                    halfHeight,
                    0f
                ),
                new Vector3(
                    -halfWidth,
                    halfHeight - stub,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                new Vector3(
                    halfWidth - stub,
                    halfHeight,
                    0f
                ),
                new Vector3(
                    halfWidth,
                    halfHeight,
                    0f
                ),
                new Vector3(
                    halfWidth,
                    halfHeight - stub,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                new Vector3(
                    -halfWidth,
                    -halfHeight + stub,
                    0f
                ),
                new Vector3(
                    -halfWidth,
                    -halfHeight,
                    0f
                ),
                new Vector3(
                    -halfWidth + stub,
                    -halfHeight,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                new Vector3(
                    halfWidth - stub,
                    -halfHeight,
                    0f
                ),
                new Vector3(
                    halfWidth,
                    -halfHeight,
                    0f
                ),
                new Vector3(
                    halfWidth,
                    -halfHeight + stub,
                    0f
                )
            }
        );

        float dashLength =
            size * focusedDashLengthRatio;

        float dashY =
            -halfHeight -
            size * 0.12f;

        for (int i = -1;
             i <= 1;
             i++)
        {
            float dashX =
                i * dashLength * 1.45f;

            shapes.Add(
                new[]
                {
                    new Vector3(
                        dashX -
                        dashLength * 0.5f,
                        dashY,
                        0f
                    ),
                    new Vector3(
                        dashX +
                        dashLength * 0.5f,
                        dashY,
                        0f
                    )
                }
            );
        }

        return shapes.ToArray();
    }

    private Vector3[] BuildArcShape(
        float radius,
        float startAngle,
        float endAngle,
        int segmentCount
    )
    {
        int safeSegments =
            Mathf.Max(
                2,
                segmentCount
            );

        Vector3[] points =
            new Vector3[
                safeSegments + 1
            ];

        for (int i = 0;
             i <= safeSegments;
             i++)
        {
            float t =
                (float)i /
                safeSegments;

            float angle =
                Mathf.Lerp(
                    startAngle,
                    endAngle,
                    t
                );

            points[i] =
                new Vector3(
                    Mathf.Cos(angle) *
                    radius,
                    Mathf.Sin(angle) *
                    radius,
                    0f
                );
        }

        return points;
    }

    private void AddLabelShapes(
        List<Vector3[]> shapes,
        int slotIndex,
        float size,
        float centerY,
        float heightRatio
    )
    {
        float height =
            size * heightRatio;

        float width =
            height * 0.68f;

        float left =
            -width * 0.5f;

        float right =
            width * 0.5f;

        float top =
            centerY + height * 0.5f;

        float bottom =
            centerY - height * 0.5f;

        float middle =
            centerY;

        if (slotIndex == 0)
        {
            shapes.Add(
                new[]
                {
                    new Vector3(
                        left,
                        bottom,
                        0f
                    ),
                    new Vector3(
                        0f,
                        top,
                        0f
                    )
                }
            );

            shapes.Add(
                new[]
                {
                    new Vector3(
                        0f,
                        top,
                        0f
                    ),
                    new Vector3(
                        right,
                        bottom,
                        0f
                    )
                }
            );

            shapes.Add(
                new[]
                {
                    new Vector3(
                        left * 0.52f,
                        middle,
                        0f
                    ),
                    new Vector3(
                        right * 0.52f,
                        middle,
                        0f
                    )
                }
            );

            return;
        }

        if (slotIndex == 1)
        {
            shapes.Add(
                new[]
                {
                    new Vector3(
                        left,
                        bottom,
                        0f
                    ),
                    new Vector3(
                        left,
                        top,
                        0f
                    )
                }
            );

            shapes.Add(
                new[]
                {
                    new Vector3(
                        left,
                        top,
                        0f
                    ),
                    new Vector3(
                        right,
                        top,
                        0f
                    ),
                    new Vector3(
                        right,
                        middle,
                        0f
                    ),
                    new Vector3(
                        left,
                        middle,
                        0f
                    )
                }
            );

            shapes.Add(
                new[]
                {
                    new Vector3(
                        left,
                        middle,
                        0f
                    ),
                    new Vector3(
                        right,
                        middle,
                        0f
                    ),
                    new Vector3(
                        right,
                        bottom,
                        0f
                    ),
                    new Vector3(
                        left,
                        bottom,
                        0f
                    )
                }
            );

            return;
        }

        shapes.Add(
            new[]
            {
                new Vector3(
                    right,
                    top,
                    0f
                ),
                new Vector3(
                    left,
                    top,
                    0f
                ),
                new Vector3(
                    left,
                    bottom,
                    0f
                ),
                new Vector3(
                    right,
                    bottom,
                    0f
                )
            }
        );
    }

    private void TranslateShapes(
        Vector3[][] shapes,
        Vector3 offset
    )
    {
        for (int shapeIndex = 0;
             shapeIndex < shapes.Length;
             shapeIndex++)
        {
            Vector3[] shape =
                shapes[shapeIndex];

            for (int pointIndex = 0;
                 pointIndex < shape.Length;
                 pointIndex++)
            {
                shape[pointIndex] +=
                    offset;
            }
        }
    }

    private void ApplyShapes(
        List<LineRenderer> lines,
        Vector3[][] shapes
    )
    {
        int safeCount =
            Mathf.Min(
                lines.Count,
                shapes.Length
            );

        for (int i = 0;
             i < lines.Count;
             i++)
        {
            LineRenderer line =
                lines[i];

            if (line == null)
            {
                continue;
            }

            if (i >= safeCount)
            {
                line.enabled = false;
                continue;
            }

            line.enabled = true;
            line.positionCount =
                shapes[i].Length;

            line.SetPositions(
                shapes[i]
            );
        }
    }

    private void UpdateAnchorAppearance(
        AnchorRecord anchor,
        float worldPerPixel,
        float compactAlpha,
        float detailAlpha,
        float expiryWarning,
        float expiryPulse01
    )
    {
        float brightWidth =
            Mathf.Max(
                0.0001f,
                anchor.brightWidthPixels *
                worldPerPixel
            );

        float darkWidth =
            Mathf.Max(
                brightWidth,
                anchor.darkWidthPixels *
                worldPerPixel
            );

        float warningBlend =
            expiryWarning *
            Mathf.Lerp(
                0.38f,
                0.82f,
                expiryPulse01
            );

        Color brightColor =
            Color.Lerp(
                anchor.brightColor,
                ExpiryWarningBrightColor,
                warningBlend
            );

        Color darkColor =
            Color.Lerp(
                anchor.darkColor,
                ExpiryWarningDarkColor,
                warningBlend
            );

        ApplyAppearance(
            anchor.compactDarkLines,
            darkColor,
            darkWidth,
            compactAlpha
        );

        ApplyAppearance(
            anchor.compactBrightLines,
            brightColor,
            brightWidth,
            compactAlpha
        );

        ApplyAppearance(
            anchor.detailDarkLines,
            darkColor,
            darkWidth,
            detailAlpha
        );

        ApplyAppearance(
            anchor.detailBrightLines,
            brightColor,
            brightWidth,
            detailAlpha
        );
    }

    private void UpdateConnector(
        AnchorRecord anchor,
        float worldPerPixel,
        Vector3 localOffset,
        float lifecycleAlpha
    )
    {
        float offsetPixels =
            anchor.screenOffsetPixels.magnitude;

        bool showConnector =
            offsetPixels >= connectorStartPixels;

        if (anchor.connectorDarkLine == null ||
            anchor.connectorBrightLine == null)
        {
            return;
        }

        anchor.connectorDarkLine.enabled =
            showConnector;

        anchor.connectorBrightLine.enabled =
            showConnector;

        if (!showConnector)
        {
            return;
        }

        Vector3 direction =
            localOffset.sqrMagnitude > 0.000001f
                ? localOffset.normalized
                : Vector3.zero;

        float markerClearance =
            GetDistanceBasedSize(
                anchor.distanceToCamera
            ) *
            0.25f *
            worldPerPixel;

        Vector3 connectorEnd =
            localOffset -
            direction * markerClearance;

        Vector3[] connectorPoints =
        {
            Vector3.zero,
            connectorEnd
        };

        anchor.connectorDarkLine.positionCount =
            connectorPoints.Length;

        anchor.connectorBrightLine.positionCount =
            connectorPoints.Length;

        anchor.connectorDarkLine.SetPositions(
            connectorPoints
        );

        anchor.connectorBrightLine.SetPositions(
            connectorPoints
        );

        float brightWidth =
            Mathf.Max(
                0.0001f,
                anchor.brightWidthPixels *
                0.65f *
                worldPerPixel
            );

        float darkWidth =
            Mathf.Max(
                brightWidth,
                anchor.darkWidthPixels *
                0.75f *
                worldPerPixel
            );

        ApplySingleLineAppearance(
            anchor.connectorDarkLine,
            anchor.darkColor,
            darkWidth,
            lifecycleAlpha * 0.75f
        );

        ApplySingleLineAppearance(
            anchor.connectorBrightLine,
            anchor.brightColor,
            brightWidth,
            lifecycleAlpha * 0.6f
        );
    }


    private void UpdateConfirmationVisual(
        AnchorRecord anchor,
        float worldPerPixel,
        float sizePixels,
        Vector3 localOffset,
        float lifecycleAlpha
    )
    {
        if (anchor.confirmDarkLine == null ||
            anchor.confirmBrightLine == null)
        {
            return;
        }

        bool isProgressTarget =
            anchor.slotIndex ==
            confirmationSlotIndex &&
            confirmationProgress > 0f;

        bool isConfirmedFlash =
            anchor.slotIndex ==
            confirmedSlotIndex &&
            _confirmedFlashTimer > 0f;

        bool shouldShow =
            anchor.selectedThisFrame &&
            (
                isProgressTarget ||
                isConfirmedFlash
            );

        anchor.confirmDarkLine.enabled =
            shouldShow;

        anchor.confirmBrightLine.enabled =
            shouldShow;

        if (!shouldShow)
        {
            return;
        }

        float progress =
            isConfirmedFlash
                ? 1f
                : Mathf.Clamp01(
                    confirmationProgress
                );

        float radius =
            sizePixels *
            confirmationRingRadiusMultiplier *
            0.5f *
            worldPerPixel;

        Vector3[] points =
            BuildProgressArc(
                radius,
                progress,
                confirmationRingSegments,
                localOffset
            );

        anchor.confirmDarkLine.positionCount =
            points.Length;

        anchor.confirmBrightLine.positionCount =
            points.Length;

        anchor.confirmDarkLine.SetPositions(points);
        anchor.confirmBrightLine.SetPositions(points);

        float flashPulse =
            isConfirmedFlash
                ? 1f +
                  Mathf.Sin(
                      Time.unscaledTime *
                      48f
                  ) *
                  0.08f
                : 1f;

        float brightWidth =
            Mathf.Max(
                0.0001f,
                anchor.brightWidthPixels *
                1.15f *
                flashPulse *
                worldPerPixel
            );

        float darkWidth =
            Mathf.Max(
                brightWidth,
                anchor.darkWidthPixels *
                1.12f *
                flashPulse *
                worldPerPixel
            );

        float alpha =
            lifecycleAlpha *
            (
                isConfirmedFlash
                    ? 1f
                    : Mathf.Lerp(
                        0.72f,
                        1f,
                        progress
                    )
            );

        ApplySingleLineAppearance(
            anchor.confirmDarkLine,
            anchor.darkColor,
            darkWidth,
            alpha
        );

        Color brightColor =
            anchor.brightColor;

        if (isConfirmedFlash)
        {
            brightColor =
                Color.Lerp(
                    brightColor,
                    Color.white,
                    0.68f
                );
        }

        ApplySingleLineAppearance(
            anchor.confirmBrightLine,
            brightColor,
            brightWidth,
            alpha
        );
    }


    private void UpdateTeleportChannelVisual(
        AnchorRecord anchor,
        float worldPerPixel,
        float sizePixels,
        Vector3 localOffset,
        float lifecycleAlpha
    )
    {
        bool isChannelTarget =
            anchor.slotIndex ==
            _teleportChannelSlotIndex;

        SetLineListEnabled(
            anchor.channelDarkLines,
            isChannelTarget
        );

        SetLineListEnabled(
            anchor.channelBrightLines,
            isChannelTarget
        );

        if (!isChannelTarget)
        {
            return;
        }

        float progress =
            Mathf.Clamp01(
                _teleportChannelProgress
            );

        float lockPulse =
            1f +
            Mathf.Sin(
                Time.unscaledTime *
                channelLockPulseSpeed
            ) *
            channelLockPulseAmount;

        float baseSizePixels =
            Mathf.Max(
                sizePixels,
                focusedSizePixels
            ) *
            channelFrameSizeMultiplier *
            lockPulse;

        float size =
            baseSizePixels *
            worldPerPixel;

        Vector3[][] shapes =
            BuildTeleportChannelShapes(
                size,
                localOffset
            );

        ApplyShapes(
            anchor.channelDarkLines,
            shapes
        );

        ApplyShapes(
            anchor.channelBrightLines,
            shapes
        );

        float completionT =
            Mathf.InverseLerp(
                channelCompletionFlashStart,
                1f,
                progress
            );

        float brightWidth =
            Mathf.Max(
                0.0001f,
                anchor.brightWidthPixels *
                1.08f *
                worldPerPixel
            );

        float darkWidth =
            Mathf.Max(
                brightWidth,
                anchor.darkWidthPixels *
                1.12f *
                worldPerPixel
            );

        float alpha =
            lifecycleAlpha *
            Mathf.Lerp(
                0.82f,
                1f,
                Smooth01(progress)
            );

        Color bright =
            Color.Lerp(
                channelColor,
                Color.white,
                Smooth01(completionT) *
                0.72f
            );

        ApplyAppearance(
            anchor.channelDarkLines,
            channelDarkUnderlayColor,
            darkWidth,
            alpha
        );

        ApplyAppearance(
            anchor.channelBrightLines,
            bright,
            brightWidth,
            alpha
        );
    }

    private Vector3[][] BuildTeleportChannelShapes(
        float size,
        Vector3 offset
    )
    {
        float halfWidth =
            size * 0.5f;

        float halfHeight =
            size * 0.38f;

        float stub =
            size *
            channelBracketStubRatio;

        float sideTickLength =
            size * 0.13f;

        List<Vector3[]> shapes =
            new List<Vector3[]>();

        // 稳定四角锁定框，不承担进度表达。
        shapes.Add(
            new[]
            {
                offset +
                new Vector3(
                    -halfWidth + stub,
                    halfHeight,
                    0f
                ),
                offset +
                new Vector3(
                    -halfWidth,
                    halfHeight,
                    0f
                ),
                offset +
                new Vector3(
                    -halfWidth,
                    halfHeight - stub,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                offset +
                new Vector3(
                    halfWidth - stub,
                    halfHeight,
                    0f
                ),
                offset +
                new Vector3(
                    halfWidth,
                    halfHeight,
                    0f
                ),
                offset +
                new Vector3(
                    halfWidth,
                    halfHeight - stub,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                offset +
                new Vector3(
                    -halfWidth,
                    -halfHeight + stub,
                    0f
                ),
                offset +
                new Vector3(
                    -halfWidth,
                    -halfHeight,
                    0f
                ),
                offset +
                new Vector3(
                    -halfWidth + stub,
                    -halfHeight,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                offset +
                new Vector3(
                    halfWidth - stub,
                    -halfHeight,
                    0f
                ),
                offset +
                new Vector3(
                    halfWidth,
                    -halfHeight,
                    0f
                ),
                offset +
                new Vector3(
                    halfWidth,
                    -halfHeight + stub,
                    0f
                )
            }
        );

        // 左右短刻度进一步强调“目的地已锁定”。
        shapes.Add(
            new[]
            {
                offset +
                new Vector3(
                    -halfWidth -
                    sideTickLength,
                    0f,
                    0f
                ),
                offset +
                new Vector3(
                    -halfWidth +
                    sideTickLength * 0.2f,
                    0f,
                    0f
                )
            }
        );

        shapes.Add(
            new[]
            {
                offset +
                new Vector3(
                    halfWidth -
                    sideTickLength * 0.2f,
                    0f,
                    0f
                ),
                offset +
                new Vector3(
                    halfWidth +
                    sideTickLength,
                    0f,
                    0f
                )
            }
        );

        return shapes.ToArray();
    }


    private Vector3[] BuildProgressArc(
        float radius,
        float progress,
        int segmentCount,
        Vector3 offset
    )
    {
        int safeSegments =
            Mathf.Max(
                12,
                segmentCount
            );

        float safeProgress =
            Mathf.Clamp01(progress);

        int usedSegments =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    safeSegments *
                    safeProgress
                )
            );

        Vector3[] points =
            new Vector3[
                usedSegments + 1
            ];

        float startAngle =
            Mathf.PI * 0.5f;

        float totalAngle =
            -Mathf.PI *
            2f *
            safeProgress;

        for (int i = 0;
             i <= usedSegments;
             i++)
        {
            float t =
                (float)i /
                usedSegments;

            float angle =
                startAngle +
                totalAngle *
                t;

            points[i] =
                offset +
                new Vector3(
                    Mathf.Cos(angle) *
                    radius,
                    Mathf.Sin(angle) *
                    radius,
                    0f
                );
        }

        return points;
    }

    private void ApplyAppearance(
        List<LineRenderer> lines,
        Color sourceColor,
        float width,
        float alpha
    )
    {
        Color color =
            sourceColor;

        color.a *=
            Mathf.Clamp01(alpha);

        for (int i = 0;
             i < lines.Count;
             i++)
        {
            ApplySingleLineAppearance(
                lines[i],
                color,
                width,
                1f
            );
        }
    }

    private void ApplySingleLineAppearance(
        LineRenderer line,
        Color sourceColor,
        float width,
        float alphaMultiplier
    )
    {
        if (line == null)
        {
            return;
        }

        Color color =
            sourceColor;

        color.a *=
            Mathf.Clamp01(
                alphaMultiplier
            );

        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
    }

    private void SetAnchorLinesEnabled(
        AnchorRecord anchor,
        bool enabledState
    )
    {
        SetLineListEnabled(
            anchor.compactDarkLines,
            enabledState
        );

        SetLineListEnabled(
            anchor.compactBrightLines,
            enabledState
        );

        SetLineListEnabled(
            anchor.detailDarkLines,
            enabledState
        );

        SetLineListEnabled(
            anchor.detailBrightLines,
            enabledState
        );

        if (anchor.connectorDarkLine != null)
        {
            anchor.connectorDarkLine.enabled =
                enabledState;
        }

        if (anchor.connectorBrightLine != null)
        {
            anchor.connectorBrightLine.enabled =
                enabledState;
        }

        if (anchor.confirmDarkLine != null)
        {
            anchor.confirmDarkLine.enabled =
                enabledState;
        }

        if (anchor.confirmBrightLine != null)
        {
            anchor.confirmBrightLine.enabled =
                enabledState;
        }

        SetLineListEnabled(
            anchor.channelDarkLines,
            enabledState
        );

        SetLineListEnabled(
            anchor.channelBrightLines,
            enabledState
        );
    }

    private void SetAllAnchorLinesEnabled(
        bool enabledState
    )
    {
        for (int i = 0;
             i < _anchors.Length;
             i++)
        {
            AnchorRecord anchor =
                _anchors[i];

            if (anchor != null)
            {
                SetAnchorLinesEnabled(
                    anchor,
                    enabledState
                );
            }
        }
    }

    private void SetLineListEnabled(
        List<LineRenderer> lines,
        bool enabledState
    )
    {
        for (int i = 0;
             i < lines.Count;
             i++)
        {
            if (lines[i] != null)
            {
                lines[i].enabled =
                    enabledState;
            }
        }
    }

    // =========================================================
    // 材质、相机与可见性
    // =========================================================

    private void EnsureRuntimeMaterials(
        Material sourceMaterial
    )
    {
        if (_runtimeBrightMaterial != null &&
            _runtimeDarkMaterial != null)
        {
            return;
        }

        Shader fallbackShader =
            Shader.Find("Sprites/Default");

        if (fallbackShader == null)
        {
            Debug.LogError(
                "[TeleportAnchorSystem] 找不到 Sprites/Default Shader。"
            );

            enabled = false;
            return;
        }

        Material selectedTemplate =
            materialTemplateOverride != null
                ? materialTemplateOverride
                : sourceMaterial;

        _runtimeBrightMaterial =
            selectedTemplate != null
                ? new Material(selectedTemplate)
                : new Material(fallbackShader);

        _runtimeDarkMaterial =
            new Material(fallbackShader);

        _runtimeBrightMaterial.name =
            "TP_Anchor_Bright_Runtime";

        _runtimeDarkMaterial.name =
            "TP_Anchor_Dark_Runtime";

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

        _runtimeDarkMaterial.renderQueue = 3120;
        _runtimeBrightMaterial.renderQueue = 3121;
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
            material.SetColor(
                ColorId,
                color
            );
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(
                BaseColorId,
                color
            );
        }
    }

    private void ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private float WorldUnitsPerPixel(
        Vector3 worldPosition
    )
    {
        float screenHeight =
            Mathf.Max(
                1f,
                Screen.height
            );

        if (targetCamera.orthographic)
        {
            return
                targetCamera.orthographicSize *
                2f /
                screenHeight;
        }

        float distance =
            Vector3.Distance(
                targetCamera.transform.position,
                worldPosition
            );

        float verticalSize =
            2f *
            distance *
            Mathf.Tan(
                targetCamera.fieldOfView *
                0.5f *
                Mathf.Deg2Rad
            );

        return verticalSize /
               screenHeight;
    }

    private bool IsVisible(
        Vector3 worldPosition
    )
    {
        Vector3 cameraToAnchor =
            worldPosition -
            targetCamera.transform.position;

        if (Vector3.Dot(
                targetCamera.transform.forward,
                cameraToAnchor
            ) <= 0f)
        {
            return false;
        }

        if (hideOutsideCameraView)
        {
            Vector3 viewport =
                targetCamera.WorldToViewportPoint(
                    worldPosition
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
            float distance =
                cameraToAnchor.magnitude;

            if (Physics.Raycast(
                    targetCamera.transform.position,
                    cameraToAnchor.normalized,
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

    private string GetSlotLabel(
        int slotIndex
    )
    {
        switch (slotIndex)
        {
            case 0:
                return "A";

            case 1:
                return "B";

            default:
                return "C";
        }
    }

    private float Smooth01(
        float value
    )
    {
        value =
            Mathf.Clamp01(value);

        return
            value *
            value *
            (3f - 2f * value);
    }

    private void ValidateRuntimeValues()
    {
        focusRadiusPixels =
            Mathf.Clamp(
                focusRadiusPixels,
                20f,
                34f
            );

        anchorAimRadius =
            Mathf.Clamp(
                anchorAimRadius,
                0.12f,
                0.8f
            );

        nearDistance =
            Mathf.Max(
                1f,
                nearDistance
            );

        middleDistance =
            Mathf.Max(
                nearDistance + 1f,
                middleDistance
            );

        farDistance =
            Mathf.Max(
                middleDistance + 1f,
                farDistance
            );

        middleSizePixels =
            Mathf.Min(
                nearSizePixels,
                middleSizePixels
            );

        farSizePixels =
            Mathf.Min(
                middleSizePixels,
                farSizePixels
            );

        minimumSizePixels =
            Mathf.Min(
                farSizePixels,
                minimumSizePixels
            );

        focusedSizePixels =
            Mathf.Max(
                nearSizePixels,
                focusedSizePixels
            );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        anchorLifetime =
            Mathf.Max(
                0.5f,
                anchorLifetime
            );

        transitionOverlap =
            Mathf.Clamp(
                transitionOverlap,
                0f,
                0.1f
            );

        appearDuration =
            Mathf.Max(
                0.01f,
                appearDuration
            );

        fadeDuration =
            Mathf.Max(
                0.01f,
                fadeDuration
            );

        pulseSpeed =
            Mathf.Max(
                0f,
                pulseSpeed
            );

        focusTransitionSpeed =
            Mathf.Max(
                1f,
                focusTransitionSpeed
            );

        maximumSelectionDistance =
            Mathf.Max(
                1f,
                maximumSelectionDistance
            );

        holdToConfirmDuration =
            Mathf.Max(
                0.01f,
                holdToConfirmDuration
            );

        confirmedFlashDuration =
            Mathf.Max(
                0.01f,
                confirmedFlashDuration
            );

        channelFrameSizeMultiplier =
            Mathf.Clamp(
                channelFrameSizeMultiplier,
                0.9f,
                1.8f
            );

        channelLockPulseAmount =
            Mathf.Clamp(
                channelLockPulseAmount,
                0f,
                0.12f
            );

        channelLockPulseSpeed =
            Mathf.Clamp(
                channelLockPulseSpeed,
                0f,
                20f
            );

        channelCompletionFlashStart =
            Mathf.Clamp(
                channelCompletionFlashStart,
                0.75f,
                1f
            );

        confirmationRingSegments =
            Mathf.Clamp(
                confirmationRingSegments,
                12,
                64
            );

        ValidateRuntimeValues();
    }
#endif
}
