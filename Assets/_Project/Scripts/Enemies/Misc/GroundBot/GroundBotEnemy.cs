#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GroundBotEnemy : MonoBehaviour, IEnemyExternalControlReceiver
{
    public enum GroundBotState
    {
        Idle,
        Scanning,
        HighAlert,
        SearchLastSeen,
        Approach,
        Holding,
        Retreat,
        Suspended,
        Aim,
        BurstFire,
        Cooldown,
        Stunned
    }

    [Header("核心引用")]
    public CombatPlatform combatPlatform;

    [Tooltip(
        "Phase 1：直接订阅新的 CombatEncounter 生命周期。" +
        "留空时会从 CombatPlatform 的兼容桥自动获取。"
    )]
    public CombatEncounter combatEncounter;

    [Tooltip(
        "开启时，Ground Bot 死亡会计入所属 Encounter 的清场条件。"
    )]
    public bool requiredForEncounterClear = true;

    public EnemyTarget enemyTarget;
    public EnemyContactDamage contactDamage;
    public Rigidbody enemyRigidbody;

    [Tooltip("只负责模型视觉。不要拖最外层 GroundBot。")]
    public Transform visualRoot;

    [Tooltip("红色炮口内部的瞄准参考点。")]
    public Transform aimPoint;

    [Tooltip("红色炮口外侧的子弹生成点。")]
    public Transform muzzlePoint;

    [Tooltip("机器人前方、底部附近的地面检测点。")]
    public Transform groundCheckFront;

    [Tooltip("机器人后方、底部附近的地面检测点。")]
    public Transform groundCheckBack;

    [Header("CombatArea 激活门槛")]
    [Tooltip("绑定 CombatPlatform 后，CombatArea 是硬门槛。未绑定时才使用该距离作为备用激活距离。")]
    [Min(0.1f)]
    public float detectionRange = 30f;

    [Header("警戒扫描与视觉")]
    [Min(0.1f)]
    public float visionRange = 30f;

    [Range(1f, 179f)]
    public float visionAngle = 70f;

    [Range(1f, 179f)]
    public float scanHalfAngle = 55f;

    [Min(0f)]
    public float scanTurnSpeed = 45f;

    [Min(0f)]
    public float scanEndPause = 0.25f;

    [Min(0f)]
    public float targetMemoryDuration = 1.5f;

    [Min(0f)]
    public float highAlertDuration = 1.5f;

    [Tooltip("从视野外被玩家击中后，快速转向伤害来源的速度。")]
    [Min(0f)]
    public float highAlertTurnSpeed = 520f;

    [Tooltip("正式游戏不显示视野锥；只在 Scene 视图选中对象时显示。")]
    public bool drawVisionGizmos = true;

    [Header("头顶警戒状态提示")]
    [Tooltip("开启后，进入警戒扫描时短暂显示橙色问号，确认发现玩家时短暂显示红色感叹号。")]
    public bool showStateIndicators = true;

    [Min(0.05f)]
    [Tooltip("提示图标与 Ground Bot 机体顶部之间的垂直距离。")]
    public float stateIndicatorHeightPadding = 0.72f;

    [Range(0.20f, 1.20f)]
    public float suspicionIndicatorDuration = 0.52f;

    [Range(0.20f, 1.20f)]
    public float detectedIndicatorDuration = 0.62f;

    [Range(0.55f, 1.75f)]
    public float stateIndicatorScale = 1f;

    [Header("保持距离")]
    [Tooltip("距离大于该值时，Ground Bot 接近玩家。")]
    [Min(0.1f)]
    public float approachStartDistance = 12.5f;

    [Tooltip("接近到该距离后停止主动接近。")]
    [Min(0.1f)]
    public float approachStopDistance = 10.5f;

    [Tooltip("距离低于该值时全速后退，且不能开始新的 Aim。")]
    [Min(0.1f)]
    public float retreatStartDistance = 6f;

    [Tooltip("后退到该距离后停止主动后退。")]
    [Min(0.1f)]
    public float retreatStopDistance = 7.5f;

    [Header("移动")]
    [Min(0f)]
    public float approachSpeed = 3.2f;

    [Min(0f)]
    public float retreatSpeed = 2.8f;

    [Range(0f, 1f)]
    public float attackMovementMultiplier = 0.55f;

    [Tooltip("锁定玩家后的常规转向速度。")]
    [Min(0f)]
    public float turnSpeed = 260f;

    [Header("平台边缘保护")]
    public LayerMask groundMask;

    [Min(0.05f)]
    public float groundCheckDistance = 0.65f;

    [Min(0.05f)]
    public float boundaryLookAheadDistance = 0.55f;

    [Header("墙体与视线遮挡")]
    [Tooltip("只应包含 Environment。动态角色不会遮挡视线。")]
    public LayerMask obstacleMask;

    [Min(0.01f)]
    public float obstacleProbeRadius = 0.18f;

    [Min(0.01f)]
    public float obstacleProbeDistance = 0.35f;

    public float obstacleProbeHeight = 0.45f;

    [Min(0f)]
    public float obstacleProbeStartOffset = 0.46f;

    [Header("射击与可见弹丸")]
    public GroundBotProjectile projectilePrefab;

    [Min(1)]
    public int projectilePoolSize = 8;

    [Tooltip("最远开火距离。索敌可达到 30m，但只有进入 18m 后才允许开始 Aim。")]
    [Min(0.1f)]
    public float maximumFireDistance = 18f;

    [Tooltip("兼容旧 Inspector；实际 Aim 时长由距离分段参数决定。")]
    [Min(0f)]
    public float aimDuration = 0.4f;

    [Min(1)]
    public int burstShotCount = 3;

    [Min(0.01f)]
    public float burstShotInterval = 0.18f;

    [Tooltip("兼容旧 Inspector；实际冷却由距离分段参数决定。")]
    [Min(0f)]
    public float burstCooldown = 1.25f;

    [Header("距离分段射击节奏")]
    [Min(0.1f)]
    public float nearBandMaxDistance = 9f;

    [Min(0.1f)]
    public float midBandMaxDistance = 13f;

    [Min(0f)]
    public float nearAimDuration = 0.35f;

    [Min(0f)]
    public float midAimDuration = 0.40f;

    [Min(0f)]
    public float farAimDuration = 0.65f;

    [Min(0f)]
    public float nearBurstCooldown = 1.35f;

    [Min(0f)]
    public float midBurstCooldown = 1.25f;

    [Min(0f)]
    public float farBurstCooldown = 2.10f;

    [Min(0f)]
    public float projectileSpeed = 24f;

    [Min(0f)]
    public float projectileDamage = 15f;

    [Min(0.05f)]
    public float projectileLifeTime = 4f;

    public float playerAimHeightOffset = 1f;

    [Min(0f)]
    public float lineOfSightRadius = 0.035f;

    [Header("状态灯与瞄准预警")]
    [Tooltip("通常自动绑定 VisualRoot 下带 Emission Map 的 Renderer。")]
    public Renderer[] warningRenderers;

    [ColorUsage(true, true)]
    public Color idleBlueColor = new Color(0.08f, 0.55f, 1.35f, 1f);

    [ColorUsage(true, true)]
    public Color alertOrangeColor = new Color(1.55f, 0.26f, 0.025f, 1f);

    [ColorUsage(true, true)]
    public Color lockedRedColor = new Color(1.85f, 0.025f, 0.018f, 1f);

    [Min(0f)]
    public float idleLightIntensity = 0.85f;

    [Min(0f)]
    public float alertBaseIntensity = 0.85f;

    [Min(0f)]
    public float alertPulseIntensity = 0.65f;

    [Min(0.01f)]
    public float alertPulseFrequency = 0.85f;

    [Min(0f)]
    public float lockedBaseIntensity = 1.05f;

    [Min(0f)]
    public float lockedPulseIntensity = 0.75f;

    [Min(0.01f)]
    public float lockedPulseFrequency = 3.2f;

    [Tooltip("Aim 结束前红光逐步增强到该倍率。")]
    [Min(1f)]
    public float aimEmissionMultiplier = 3.2f;

    [Tooltip("每发开火的瞬间最高亮度倍率。")]
    [Min(1f)]
    public float shotEmissionMultiplier = 5f;

    [Min(0.01f)]
    public float warningResponseSpeed = 12f;

    [Min(0.01f)]
    public float shotEmissionFlashDuration = 0.08f;

    [Header("开火反馈")]
    public GroundBotCombatFX muzzleFlashPrefab;
    public AudioSource shotAudioSource;
    public AudioClip shotAudioClip;

    [Range(0f, 1f)]
    public float shotVolume = 0.55f;

    public Vector2 shotPitchRange = new Vector2(0.96f, 1.04f);
    public bool generateFallbackShotSound = true;

    [Header("Ground Bot 专属受击反馈")]
    [Tooltip("兼容旧 Prefab 的保留字段。当前版本不再复用橙色枪口火花。")]
    public GroundBotCombatFX hitImpactPrefab;

    [ColorUsage(true, true)]
    [Tooltip("命中后的局部深红余辉颜色。只保留在核心、炮口和原有发光区域。")]
    public Color hitFlashColor =
        new Color(2.10f, 0.12f, 0.04f, 1f);

    [ColorUsage(true, true)]
    [Tooltip("命中最初几帧的白热淡粉闪光。只用于极短的全身冲击确认。")]
    public Color hitWhiteHotColor =
        new Color(2.55f, 1.88f, 1.70f, 1f);

    [Min(0.01f)]
    [Tooltip("普通命中完整反馈时间：前段白热瞬闪，后段只保留局部深红余辉。")]
    public float hitFlashDuration = 0.18f;

    [Range(0.01f, 0.06f)]
    [Tooltip("全身白热瞬闪持续时间。必须明显短于完整反馈时间。")]
    public float hitWhiteHotDuration = 0.055f;

    [Min(0f)]
    [Tooltip("核心、炮口和原有发光区域在命中时的额外亮度。")]
    public float hitEmissionMultiplier = 3.45f;

    [Min(0f)]
    [Tooltip("只移动 VisualRoot，不改变碰撞体，也不再使用 Scale Punch。")]
    public float hitRecoilDistance = 0.055f;

    [Min(0.01f)]
    public float hitPunchDuration = 0.11f;

    [Header("白色方块粒子喷发")]
    [ColorUsage(true, true)]
    public Color hitBlockParticleColor =
        new Color(1.04f, 1.04f, 1.04f, 1f);

    [Tooltip("中等方块数量范围。大部分沿命中表面法线向外喷出。")]
    public Vector2Int hitMediumBlockCount =
        new Vector2Int(3, 4);

    [Tooltip("小方块数量范围。用于命中点四周的少量散射。")]
    public Vector2Int hitSmallBlockCount =
        new Vector2Int(4, 5);

    [Tooltip("稍大方块数量范围。作为爆发瞬间的视觉重心。")]
    public Vector2Int hitLargeBlockCount =
        new Vector2Int(1, 1);

    [Range(0.50f, 1.05f)]
    public float hitBlockLifetime = 0.92f;

    [Min(0.05f)]
    public float hitBlockOverallScale = 1.08f;

    [Range(1.001f, 1.03f)]
    [Tooltip("全身红闪覆盖模型的轻微外扩，避免与原模型发生深度闪烁。")]
    public float hitFlashOverlayScale = 1.007f;

    [Header("运行状态")]
    [SerializeField]
    private GroundBotState currentState = GroundBotState.Idle;

    [SerializeField]
    private Transform currentPlayer;

    [SerializeField]
    private bool movementBlocked;

    [SerializeField]
    private bool hasLineOfSight;

    [SerializeField]
    private bool isPlayerInVisionCone;

    [SerializeField]
    private int shotsRemaining;

    [SerializeField]
    private Vector3 lastSeenPlayerPosition;

    [SerializeField]
    private bool _externalControlActive;

    public GroundBotState CurrentState => currentState;
    public Transform CurrentPlayer => currentPlayer;
    public Vector3 CurrentMoveDirection => _currentMoveDirection;
    public bool IsExternalControlActive => _externalControlActive;

    private PlayerHealth _playerHealth;
    private CharacterController _playerCharacterController;
    private Vector3 _currentMoveDirection;
    private float _stateTimer;
    private float _nextPlayerSearchTime;
    private bool _rewindSuspended;
    private bool _combatGateWasOpen;
    private CombatEncounter _boundCombatEncounter;
    private CombatEncounterMember _encounterMember;
    private GroundBotAlertIndicatorFX _activeStateIndicator;
    private float _nextShotTime;
    private float _shotEmissionTimer;

    private float _hitFlashTimer;
    private float _activeHitFlashDuration;
    private float _activeHitStrength;
    private float _hitPunchTimer;
    private float _activeHitPunchDuration;
    private Vector3 _hitPunchLocalDirection;
    private Vector3 _visualBaseLocalPosition;
    private Vector3 _visualBaseLocalScale = Vector3.one;
    private bool _visualFeedbackCached;

    private readonly List<MeshRenderer> _hitFlashOverlayRenderers =
        new List<MeshRenderer>();
    private Material _hitFlashOverlayMaterial;
    private bool _hitFlashOverlayBuilt;

    private float _activeAimDuration;
    private float _activeBurstCooldown;
    private float _scanCenterYaw;
    private int _scanDirection = 1;
    private float _scanPauseRemaining;
    private Vector3 _suspectedDamageSourcePosition;
    private bool _missingProjectileWarningLogged;
    private bool _damageEventBound;
    private static AudioClip _sharedFallbackShotClip;

    private Transform _projectilePoolRoot;
    private readonly Queue<GroundBotProjectile> _availableProjectiles =
        new Queue<GroundBotProjectile>();
    private readonly List<GroundBotProjectile> _ownedProjectiles =
        new List<GroundBotProjectile>();

    private MaterialPropertyBlock _warningPropertyBlock;
    private Color _currentStatusEmission;
    private readonly RaycastHit[] _obstacleHits = new RaycastHit[12];
    private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[12];

    private void Awake()
    {
        AutoAssignReferences();
        ResolveCombatEncounterReference();
        EnsureEncounterMembership();
        BindCombatEncounterEvents();
        CacheHitFeedbackTransform();
        BuildHitFlashOverlay();
        PrepareShotAudioSource();

        if (enemyRigidbody != null)
        {
            enemyRigidbody.useGravity = false;
            enemyRigidbody.isKinematic = true;
            enemyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            enemyRigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            enemyRigidbody.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }

        SetContactDamageEnabled(IsCombatGateOpen());

        CacheWarningVisuals();
        BuildProjectilePool();
        BindEnemyDamageEvent();
    }

    private void OnEnable()
    {
        ResolveCombatEncounterReference();
        EnsureEncounterMembership();
        BindCombatEncounterEvents();
        BindEnemyDamageEvent();
        RefreshPlayerReference(true);
        BindPlayerHealth();

        // The bot may be disabled during rewind and therefore miss the
        // RewindCompleted event. Rebuild the local flag from PlayerHealth each
        // time it becomes active so it cannot remain suspended permanently.
        _rewindSuspended =
            _playerHealth != null && _playerHealth.IsRewinding;
    }

    private void Start()
    {
        RefreshPlayerReference(true);
        BindPlayerHealth();

        _combatGateWasOpen = IsCombatGateOpen();

        if (_combatGateWasOpen)
        {
            HandleCombatGateOpened(false);
        }
        else
        {
            HandleCombatGateClosed();
        }

        ValidateRuntimeSetup();
    }

    private void Update()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        RefreshPlayerReference(false);
        BindPlayerHealth();

        if (_rewindSuspended ||
            (_playerHealth != null && _playerHealth.IsRewinding))
        {
            EnterSuspended();
            UpdateStatusLight();
            return;
        }

        bool combatGateOpen = IsCombatGateOpen();

        if (!combatGateOpen)
        {
            if (_combatGateWasOpen ||
                currentState != GroundBotState.Suspended)
            {
                HandleCombatGateClosed();
            }

            _combatGateWasOpen = false;
            UpdateStatusLight();
            return;
        }

        if (!_combatGateWasOpen)
        {
            _combatGateWasOpen = true;
            HandleCombatGateOpened(true);
        }

        if (_externalControlActive)
        {
            return;
        }

        switch (currentState)
        {
            case GroundBotState.Idle:
            case GroundBotState.Suspended:
                EnterScanning();
                break;

            case GroundBotState.Scanning:
                UpdateScanningState();
                break;

            case GroundBotState.HighAlert:
                UpdateHighAlertState();
                break;

            case GroundBotState.SearchLastSeen:
                UpdateSearchLastSeenState();
                break;

            case GroundBotState.Approach:
            case GroundBotState.Holding:
            case GroundBotState.Retreat:
                UpdateCombatLocomotionState();
                break;

            case GroundBotState.Aim:
                UpdateAimState();
                break;

            case GroundBotState.BurstFire:
                UpdateBurstFireState();
                break;

            case GroundBotState.Cooldown:
                UpdateCooldownState();
                break;
        }

        UpdateStatusLight();
    }

    private void LateUpdate()
    {
        UpdateHitFeedback();
    }

    private void FixedUpdate()
    {
        _currentMoveDirection = Vector3.zero;
        movementBlocked = false;

        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        if (_externalControlActive || _rewindSuspended || !IsCombatGateOpen())
        {
            return;
        }

        switch (currentState)
        {
            case GroundBotState.Scanning:
                UpdateScanRotation();
                break;

            case GroundBotState.HighAlert:
                RotateTowardPosition(
                    _suspectedDamageSourcePosition,
                    highAlertTurnSpeed
                );
                break;

            case GroundBotState.SearchLastSeen:
                UpdateSearchMovement();
                break;

            case GroundBotState.Approach:
                RotateAndMoveTowardPlayer(approachSpeed);
                break;

            case GroundBotState.Retreat:
                RotateAndMoveAwayFromPlayer(retreatSpeed);
                break;

            case GroundBotState.Holding:
                RotateTowardPlayer(turnSpeed);
                break;

            case GroundBotState.Aim:
            case GroundBotState.BurstFire:
                UpdateAttackMovement();
                break;

            case GroundBotState.Cooldown:
                UpdateCooldownMovement();
                break;
        }
    }

    private void OnDisable()
    {
        _combatGateWasOpen = false;
        EnterSuspended();
        DespawnAllOwnedProjectiles();
        ClearStateIndicator();
        ClearStatusEmissionOverride();
        ResetHitFeedbackTransform();
        SetHitFlashOverlayVisible(false);
        UnbindPlayerHealth();
        UnbindEnemyDamageEvent();
        UnbindCombatEncounterEvents();
        _currentMoveDirection = Vector3.zero;
    }

    private void OnDestroy()
    {
        DestroyAllOwnedProjectiles();
        ClearStateIndicator();
        ClearStatusEmissionOverride();
        ResetHitFeedbackTransform();
        DestroyHitFlashOverlayMaterial();
        UnbindPlayerHealth();
        UnbindEnemyDamageEvent();
        UnbindCombatEncounterEvents();
    }

    // =========================================================
    // 外部硬直 / 受控击退
    // =========================================================

    public void BeginExternalControl()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        _externalControlActive = true;
        ClearStateIndicator();
        currentState = GroundBotState.Stunned;
        ResetAttackRuntime();
        _stateTimer = 0f;
        _currentMoveDirection = Vector3.zero;
        movementBlocked = false;
        hasLineOfSight = false;
        isPlayerInVisionCone = false;
        ClearStatusEmissionOverride();

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(false);
        }
    }

    public void EndExternalControl()
    {
        if (!_externalControlActive)
        {
            return;
        }

        _externalControlActive = false;

        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        RefreshPlayerReference(true);
        BindPlayerHealth();

        if (_rewindSuspended ||
            (_playerHealth != null && _playerHealth.IsRewinding))
        {
            EnterSuspended();

            if (contactDamage != null)
            {
                contactDamage.SetDamageEnabled(false);
            }

            return;
        }

        if (!IsCombatGateOpen())
        {
            HandleCombatGateClosed();
            return;
        }

        SetContactDamageEnabled(true);

        if (CanVisuallyDetectPlayer())
        {
            AcquirePlayer();
        }
        else
        {
            EnterScanning();
        }
    }

    // =========================================================
    // 状态切换
    // =========================================================

    private void EnterIdle()
    {
        ClearStateIndicator();
        currentState = GroundBotState.Idle;
        ResetAttackRuntime();
        _stateTimer = 0f;
        _currentMoveDirection = Vector3.zero;
        movementBlocked = false;
        hasLineOfSight = false;
        isPlayerInVisionCone = false;
        SetContactDamageEnabled(false);
    }

    private void EnterScanning(bool showSuspicionIndicator = false)
    {
        SetContactDamageEnabled(true);
        currentState = GroundBotState.Scanning;
        ResetAttackRuntime();
        _stateTimer = 0f;
        _currentMoveDirection = Vector3.zero;
        _scanCenterYaw = NormalizeAngle(GetCurrentRotation().eulerAngles.y);
        _scanDirection = 1;
        _scanPauseRemaining = 0f;
        hasLineOfSight = false;
        isPlayerInVisionCone = false;

        if (showSuspicionIndicator)
        {
            ShowStateIndicator(
                GroundBotAlertIndicatorFX.IndicatorKind.Suspicion
            );
        }
    }

    private void EnterHighAlert(Vector3 suspectedSourcePosition)
    {
        currentState = GroundBotState.HighAlert;
        ResetAttackRuntime();
        _stateTimer = Mathf.Max(0f, highAlertDuration);
        _suspectedDamageSourcePosition = suspectedSourcePosition;
        _currentMoveDirection = Vector3.zero;
    }

    private void EnterSearchLastSeen()
    {
        currentState = GroundBotState.SearchLastSeen;
        ResetAttackRuntime();
        _stateTimer = Mathf.Max(0f, targetMemoryDuration);
        _currentMoveDirection = Vector3.zero;
    }

    private void EnterApproach()
    {
        currentState = GroundBotState.Approach;
        ResetAttackRuntime();
    }

    private void EnterHolding()
    {
        currentState = GroundBotState.Holding;
        ResetAttackRuntime();
        _currentMoveDirection = Vector3.zero;
    }

    private void EnterRetreat()
    {
        currentState = GroundBotState.Retreat;
        ResetAttackRuntime();
    }

    private void EnterSuspended()
    {
        ClearStateIndicator();
        currentState = GroundBotState.Suspended;
        ResetAttackRuntime();
        _stateTimer = 0f;
        _currentMoveDirection = Vector3.zero;
        movementBlocked = false;
        hasLineOfSight = false;
        isPlayerInVisionCone = false;
        lastSeenPlayerPosition = Vector3.zero;
        _suspectedDamageSourcePosition = Vector3.zero;
        currentPlayer = null;
        SetContactDamageEnabled(false);
    }

    private void EnterAim()
    {
        float distance = GetFlatDistanceToPlayer();
        SelectFireTiming(distance);

        currentState = GroundBotState.Aim;
        shotsRemaining = 0;
        _stateTimer = Mathf.Max(0f, _activeAimDuration);
        _currentMoveDirection = Vector3.zero;
    }

    private void EnterBurstFire()
    {
        currentState = GroundBotState.BurstFire;
        shotsRemaining = Mathf.Max(1, burstShotCount);
        _nextShotTime = Time.time;
        _currentMoveDirection = Vector3.zero;
    }

    private void EnterCooldown()
    {
        currentState = GroundBotState.Cooldown;
        shotsRemaining = 0;
        _stateTimer = Mathf.Max(0f, _activeBurstCooldown);
        _currentMoveDirection = Vector3.zero;
    }

    private void AcquirePlayer()
    {
        if (currentPlayer == null)
        {
            EnterScanning();
            return;
        }

        bool isNewDetection =
            currentState == GroundBotState.Idle ||
            currentState == GroundBotState.Scanning ||
            currentState == GroundBotState.HighAlert ||
            currentState == GroundBotState.SearchLastSeen ||
            currentState == GroundBotState.Suspended ||
            currentState == GroundBotState.Stunned;

        if (isNewDetection)
        {
            ShowStateIndicator(
                GroundBotAlertIndicatorFX.IndicatorKind.Detected
            );
        }

        lastSeenPlayerPosition = currentPlayer.position;
        ChooseLocomotionStateFromDistance();
    }

    private void ChooseLocomotionStateFromDistance()
    {
        float distance = GetFlatDistanceToPlayer();

        if (distance > approachStartDistance)
        {
            EnterApproach();
        }
        else if (distance < retreatStartDistance)
        {
            EnterRetreat();
        }
        else
        {
            EnterHolding();
        }
    }

    private void ShowStateIndicator(
        GroundBotAlertIndicatorFX.IndicatorKind kind
    )
    {
        if (!showStateIndicators ||
            enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        ClearStateIndicator();

        float duration =
            kind == GroundBotAlertIndicatorFX.IndicatorKind.Suspicion
                ? suspicionIndicatorDuration
                : detectedIndicatorDuration;

        _activeStateIndicator =
            GroundBotAlertIndicatorFX.Spawn(
                transform,
                enemyTarget,
                kind,
                duration,
                stateIndicatorHeightPadding,
                stateIndicatorScale
            );
    }

    private void ClearStateIndicator()
    {
        if (_activeStateIndicator == null)
        {
            return;
        }

        _activeStateIndicator.CancelImmediate();
        _activeStateIndicator = null;
    }

    // =========================================================
    // 感知状态
    // =========================================================

    private void UpdateScanningState()
    {
        if (CanVisuallyDetectPlayer())
        {
            AcquirePlayer();
        }
    }

    private void UpdateHighAlertState()
    {
        if (CanVisuallyDetectPlayer())
        {
            AcquirePlayer();
            return;
        }

        _stateTimer -= Time.deltaTime;

        if (_stateTimer <= 0f)
        {
            EnterScanning();
        }
    }

    private void UpdateSearchLastSeenState()
    {
        if (CanVisuallyDetectPlayer())
        {
            AcquirePlayer();
            return;
        }

        _stateTimer -= Time.deltaTime;

        if (_stateTimer <= 0f)
        {
            EnterScanning();
        }
    }

    private bool CanVisuallyDetectPlayer()
    {
        if (!IsCombatGateOpen() || currentPlayer == null)
        {
            hasLineOfSight = false;
            isPlayerInVisionCone = false;
            return false;
        }

        float distance = GetFlatDistanceToPlayer();

        if (distance > visionRange)
        {
            hasLineOfSight = false;
            isPlayerInVisionCone = false;
            return false;
        }

        Vector3 direction = GetFlatDirectionToPlayer();

        if (direction.sqrMagnitude <= 0.0001f)
        {
            isPlayerInVisionCone = true;
        }
        else
        {
            float angle = Vector3.Angle(transform.forward, direction);
            isPlayerInVisionCone = angle <= visionAngle * 0.5f;
        }

        if (!isPlayerInVisionCone)
        {
            hasLineOfSight = false;
            return false;
        }

        hasLineOfSight = HasLineOfSightToPlayer();

        if (hasLineOfSight)
        {
            lastSeenPlayerPosition = currentPlayer.position;
        }

        return hasLineOfSight;
    }

    // =========================================================
    // 战斗状态与移动射击
    // =========================================================

    private void UpdateCombatLocomotionState()
    {
        if (!CanVisuallyDetectPlayer())
        {
            EnterSearchLastSeen();
            return;
        }

        float distance = GetFlatDistanceToPlayer();

        if (currentState == GroundBotState.Approach &&
            distance <= approachStopDistance)
        {
            EnterHolding();
            return;
        }

        if (currentState == GroundBotState.Holding)
        {
            if (distance > approachStartDistance)
            {
                EnterApproach();
                return;
            }

            if (distance < retreatStartDistance)
            {
                EnterRetreat();
                return;
            }
        }

        if (currentState == GroundBotState.Retreat &&
            distance >= retreatStopDistance)
        {
            EnterHolding();
            return;
        }

        if (CanBeginAttack())
        {
            EnterAim();
        }
    }

    private void UpdateAimState()
    {
        float distance = GetFlatDistanceToPlayer();

        if (distance < retreatStartDistance)
        {
            EnterRetreat();
            return;
        }

        if (!CanVisuallyDetectPlayer() || projectilePrefab == null)
        {
            EnterSearchLastSeen();
            return;
        }

        _stateTimer -= Time.deltaTime;

        if (_stateTimer <= 0f)
        {
            EnterBurstFire();
        }
    }

    private void UpdateBurstFireState()
    {
        if (shotsRemaining <= 0)
        {
            FinishBurst();
            return;
        }

        if (Time.time < _nextShotTime)
        {
            return;
        }

        // 三发分别在各自开火瞬间读取玩家位置。
        // 发射后的弹丸保持直线，不做跟踪。
        if (!FireProjectile())
        {
            FinishBurst();
            return;
        }

        shotsRemaining--;

        if (shotsRemaining <= 0)
        {
            FinishBurst();
            return;
        }

        _nextShotTime = Time.time + Mathf.Max(0.01f, burstShotInterval);
    }

    private void FinishBurst()
    {
        if (CanVisuallyDetectPlayer())
        {
            EnterCooldown();
        }
        else
        {
            EnterSearchLastSeen();
        }
    }

    private void UpdateCooldownState()
    {
        if (!CanVisuallyDetectPlayer())
        {
            EnterSearchLastSeen();
            return;
        }

        _stateTimer -= Time.deltaTime;

        if (_stateTimer <= 0f)
        {
            ChooseLocomotionStateFromDistance();
        }
    }

    private bool CanBeginAttack()
    {
        if (projectilePrefab == null || currentPlayer == null)
        {
            return false;
        }

        float distance = GetFlatDistanceToPlayer();

        if (distance < retreatStartDistance ||
            distance > maximumFireDistance)
        {
            return false;
        }

        return hasLineOfSight && isPlayerInVisionCone;
    }

    private void SelectFireTiming(float distance)
    {
        if (distance <= nearBandMaxDistance)
        {
            _activeAimDuration = nearAimDuration;
            _activeBurstCooldown = nearBurstCooldown;
        }
        else if (distance <= midBandMaxDistance)
        {
            _activeAimDuration = midAimDuration;
            _activeBurstCooldown = midBurstCooldown;
        }
        else
        {
            _activeAimDuration = farAimDuration;
            _activeBurstCooldown = farBurstCooldown;
        }

        aimDuration = _activeAimDuration;
        burstCooldown = _activeBurstCooldown;
    }

    // =========================================================
    // 旋转与移动
    // =========================================================

    private void UpdateScanRotation()
    {
        if (_scanPauseRemaining > 0f)
        {
            _scanPauseRemaining -= Time.fixedDeltaTime;
            return;
        }

        float targetYaw = NormalizeAngle(
            _scanCenterYaw + _scanDirection * scanHalfAngle
        );

        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
        Quaternion currentRotation = GetCurrentRotation();
        Quaternion nextRotation = Quaternion.RotateTowards(
            currentRotation,
            targetRotation,
            scanTurnSpeed * Time.fixedDeltaTime
        );

        MoveRotation(nextRotation);

        if (Quaternion.Angle(nextRotation, targetRotation) <= 0.15f)
        {
            _scanDirection *= -1;
            _scanPauseRemaining = Mathf.Max(0f, scanEndPause);
        }
    }

    private void UpdateSearchMovement()
    {
        Vector3 direction = lastSeenPlayerPosition - GetCurrentPosition();
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.25f)
        {
            RotateTowardDirection(direction, turnSpeed);
            return;
        }

        direction.Normalize();
        RotateTowardDirection(direction, turnSpeed);
        TryMove(direction, approachSpeed);
    }

    private void UpdateAttackMovement()
    {
        if (currentPlayer == null)
        {
            return;
        }

        Vector3 directionToPlayer = GetFlatDirectionToPlayer();
        RotateTowardDirection(directionToPlayer, turnSpeed);

        float distance = GetFlatDistanceToPlayer();

        if (distance < retreatStartDistance)
        {
            TryMove(-directionToPlayer, retreatSpeed);
        }
        else if (distance > approachStartDistance)
        {
            TryMove(
                directionToPlayer,
                approachSpeed * Mathf.Clamp01(attackMovementMultiplier)
            );
        }
        else if (distance < retreatStopDistance)
        {
            TryMove(
                -directionToPlayer,
                retreatSpeed * Mathf.Clamp01(attackMovementMultiplier)
            );
        }
    }

    private void UpdateCooldownMovement()
    {
        if (currentPlayer == null)
        {
            return;
        }

        Vector3 directionToPlayer = GetFlatDirectionToPlayer();
        RotateTowardDirection(directionToPlayer, turnSpeed);

        float distance = GetFlatDistanceToPlayer();

        if (distance > approachStartDistance)
        {
            TryMove(directionToPlayer, approachSpeed);
        }
        else if (distance < retreatStartDistance)
        {
            TryMove(-directionToPlayer, retreatSpeed);
        }
    }

    private void RotateAndMoveTowardPlayer(float speed)
    {
        Vector3 direction = GetFlatDirectionToPlayer();
        RotateTowardDirection(direction, turnSpeed);
        TryMove(direction, speed);
    }

    private void RotateAndMoveAwayFromPlayer(float speed)
    {
        Vector3 directionToPlayer = GetFlatDirectionToPlayer();
        RotateTowardDirection(directionToPlayer, turnSpeed);
        TryMove(-directionToPlayer, speed);
    }

    private void RotateTowardPlayer(float speed)
    {
        RotateTowardDirection(GetFlatDirectionToPlayer(), speed);
    }

    private void RotateTowardPosition(Vector3 worldPosition, float speed)
    {
        Vector3 direction = worldPosition - GetCurrentPosition();
        direction.y = 0f;
        RotateTowardDirection(direction, speed);
    }

    private void RotateTowardDirection(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude <= 0.0001f || speed <= 0f)
        {
            return;
        }

        direction.y = 0f;
        direction.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        Quaternion newRotation = Quaternion.RotateTowards(
            GetCurrentRotation(),
            targetRotation,
            speed * Time.fixedDeltaTime
        );

        MoveRotation(newRotation);
    }

    private void MoveRotation(Quaternion rotation)
    {
        if (enemyRigidbody != null && enemyRigidbody.isKinematic)
        {
            enemyRigidbody.MoveRotation(rotation);
        }
        else
        {
            transform.rotation = rotation;
        }
    }

    private void TryMove(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude <= 0.0001f || speed <= 0f)
        {
            return;
        }

        direction.y = 0f;
        direction.Normalize();

        if (!CanMoveInDirection(direction))
        {
            movementBlocked = true;
            return;
        }

        _currentMoveDirection = direction;

        Vector3 currentPosition = GetCurrentPosition();
        Vector3 targetPosition =
            currentPosition + direction * speed * Time.fixedDeltaTime;
        targetPosition.y = currentPosition.y;

        if (enemyRigidbody != null && enemyRigidbody.isKinematic)
        {
            enemyRigidbody.MovePosition(targetPosition);
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    private bool CanMoveInDirection(Vector3 direction)
    {
        Vector3 currentPosition = GetCurrentPosition();
        Vector3 boundaryTestPoint =
            currentPosition + direction * boundaryLookAheadDistance;

        if (combatPlatform != null &&
            !combatPlatform.ContainsWorldPoint(boundaryTestPoint))
        {
            return false;
        }

        Transform selectedGroundCheck =
            Vector3.Dot(direction, transform.forward) >= 0f
                ? groundCheckFront
                : groundCheckBack;

        if (!HasGroundBelow(selectedGroundCheck))
        {
            return false;
        }

        return !HasObstacleAhead(direction);
    }

    private bool HasGroundBelow(Transform groundCheck)
    {
        if (groundCheck == null)
        {
            return false;
        }

        Vector3 origin = groundCheck.position + Vector3.up * 0.08f;
        int mask = groundMask.value != 0
            ? groundMask.value
            : Physics.DefaultRaycastLayers;

        return Physics.Raycast(
            origin,
            Vector3.down,
            groundCheckDistance,
            mask,
            QueryTriggerInteraction.Ignore
        );
    }

    private bool HasObstacleAhead(Vector3 direction)
    {
        if (obstacleMask.value == 0)
        {
            return false;
        }

        Vector3 origin =
            GetCurrentPosition() +
            Vector3.up * obstacleProbeHeight +
            direction * obstacleProbeStartOffset;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            obstacleProbeRadius,
            direction,
            _obstacleHits,
            obstacleProbeDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        for (int index = 0; index < hitCount; index++)
        {
            Collider hitCollider = _obstacleHits[index].collider;

            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // =========================================================
    // 视线检测
    // =========================================================

    private bool HasLineOfSightToPlayer()
    {
        if (currentPlayer == null)
        {
            return false;
        }

        Vector3 origin = aimPoint != null
            ? aimPoint.position
            : GetCurrentPosition() + Vector3.up * 0.55f;
        Vector3 target = GetPlayerAimPosition();
        Vector3 delta = target - origin;
        float distance = delta.magnitude;

        if (distance <= 0.001f || obstacleMask.value == 0)
        {
            return true;
        }

        Vector3 direction = delta / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0f, lineOfSightRadius),
            direction,
            _lineOfSightHits,
            Mathf.Max(0f, distance - 0.05f),
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        for (int index = 0; index < hitCount; index++)
        {
            Collider hitCollider = _lineOfSightHits[index].collider;

            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private Vector3 GetPlayerAimPosition()
    {
        if (_playerCharacterController != null &&
            _playerCharacterController.enabled)
        {
            return _playerCharacterController.bounds.center;
        }

        return currentPlayer != null
            ? currentPlayer.position + Vector3.up * playerAimHeightOffset
            : GetCurrentPosition();
    }

    // =========================================================
    // 开火与对象池
    // =========================================================

    private bool FireProjectile()
    {
        if (projectilePrefab == null || muzzlePoint == null || currentPlayer == null)
        {
            if (!_missingProjectileWarningLogged)
            {
                _missingProjectileWarningLogged = true;
                Debug.LogWarning(
                    "[GroundBotEnemy] Projectile Prefab、Muzzle Point 或玩家引用缺失，无法开火。",
                    this
                );
            }

            return false;
        }

        GroundBotProjectile projectile = AcquireProjectile();

        if (projectile == null)
        {
            return false;
        }

        Vector3 origin = muzzlePoint.position;
        Vector3 direction = GetPlayerAimPosition() - origin;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        projectile.Launch(
            origin,
            direction,
            projectileSpeed,
            projectileDamage,
            projectileLifeTime,
            transform,
            ReturnProjectileToPool
        );

        SpawnMuzzleFlash(origin, direction);
        PlayShotAudio();
        _shotEmissionTimer = Mathf.Max(0.01f, shotEmissionFlashDuration);
        return true;
    }

    private void SpawnMuzzleFlash(Vector3 position, Vector3 direction)
    {
        if (muzzleFlashPrefab == null)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        Instantiate(
            muzzleFlashPrefab,
            position,
            Quaternion.LookRotation(direction.normalized, Vector3.up)
        );
    }

    private void BuildProjectilePool()
    {
        if (projectilePrefab == null || _projectilePoolRoot != null)
        {
            return;
        }

        GameObject poolObject = new GameObject("ProjectilePool_Runtime");
        _projectilePoolRoot = poolObject.transform;
        _projectilePoolRoot.SetParent(transform, false);

        int count = Mathf.Max(1, projectilePoolSize);

        for (int index = 0; index < count; index++)
        {
            GroundBotProjectile projectile = CreatePooledProjectile();

            if (projectile != null)
            {
                _availableProjectiles.Enqueue(projectile);
            }
        }
    }

    private GroundBotProjectile CreatePooledProjectile()
    {
        if (projectilePrefab == null)
        {
            return null;
        }

        if (_projectilePoolRoot == null)
        {
            GameObject poolObject = new GameObject("ProjectilePool_Runtime");
            _projectilePoolRoot = poolObject.transform;
            _projectilePoolRoot.SetParent(transform, false);
        }

        GroundBotProjectile projectile = Instantiate(projectilePrefab, _projectilePoolRoot);
        projectile.name = projectilePrefab.name + "_Pooled";
        projectile.gameObject.SetActive(false);
        _ownedProjectiles.Add(projectile);
        return projectile;
    }

    private GroundBotProjectile AcquireProjectile()
    {
        while (_availableProjectiles.Count > 0)
        {
            GroundBotProjectile projectile = _availableProjectiles.Dequeue();

            if (projectile != null)
            {
                return projectile;
            }
        }

        return CreatePooledProjectile();
    }

    private void ReturnProjectileToPool(GroundBotProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        if (_projectilePoolRoot != null)
        {
            projectile.transform.SetParent(_projectilePoolRoot, false);
        }

        _availableProjectiles.Enqueue(projectile);
    }

    private void DespawnAllOwnedProjectiles()
    {
        for (int index = 0; index < _ownedProjectiles.Count; index++)
        {
            GroundBotProjectile projectile = _ownedProjectiles[index];

            if (projectile != null && projectile.gameObject.activeSelf)
            {
                projectile.Despawn();
            }
        }
    }

    private void DestroyAllOwnedProjectiles()
    {
        for (int index = 0; index < _ownedProjectiles.Count; index++)
        {
            GroundBotProjectile projectile = _ownedProjectiles[index];

            if (projectile != null)
            {
                projectile.DestroyWithOwner();
            }
        }

        _ownedProjectiles.Clear();
        _availableProjectiles.Clear();
    }

    // =========================================================
    // Ground Bot 专属普通受击反馈
    // =========================================================

    /// <summary>
    /// Ground Bot 普通命中反馈：
    /// 1. 最初几帧出现淡粉白热瞬闪；
    /// 2. 随后关闭全身覆盖，只在核心与原有发光区域留下短促深红余辉；
    /// 3. 沿子弹进入机体的方向做轻微后坐；
    /// 4. 命中点爆出高亮白色方块，主体定向喷发、少量四周散射。
    ///
    /// 不修改生命、AI、硬直、击退或死亡逻辑。
    /// </summary>
    public void PlayHitFeedback(
        Vector3 hitPoint,
        Vector3 hitNormal,
        float strength
    )
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        CacheHitFeedbackTransform();
        BuildHitFlashOverlay();

        float safeStrength =
            Mathf.Clamp(strength, 0.35f, 1.5f);

        _activeHitStrength =
            Mathf.Max(_activeHitStrength, safeStrength);

        _activeHitFlashDuration =
            Mathf.Max(
                0.01f,
                hitFlashDuration *
                Mathf.Lerp(
                    0.92f,
                    1.08f,
                    Mathf.InverseLerp(
                        0.35f,
                        1.5f,
                        safeStrength
                    )
                )
            );

        _hitFlashTimer =
            _activeHitFlashDuration;

        _activeHitPunchDuration =
            Mathf.Max(
                0.01f,
                hitPunchDuration *
                Mathf.Lerp(
                    0.94f,
                    1.08f,
                    Mathf.InverseLerp(
                        0.35f,
                        1.5f,
                        safeStrength
                    )
                )
            );

        _hitPunchTimer =
            _activeHitPunchDuration;

        // RaycastHit.normal 通常朝向攻击者；其反方向就是子弹进入机体的方向。
        Vector3 recoilWorldDirection =
            hitNormal.sqrMagnitude > 0.0001f
                ? -hitNormal.normalized
                : transform.position - hitPoint;

        if (recoilWorldDirection.sqrMagnitude < 0.0001f)
        {
            recoilWorldDirection = -transform.forward;
        }

        // 机体只做轻微水平后坐，保留少量上下分量。
        recoilWorldDirection.y *= 0.24f;
        recoilWorldDirection.Normalize();

        Transform localSpace =
            visualRoot != null && visualRoot.parent != null
                ? visualRoot.parent
                : transform;

        _hitPunchLocalDirection =
            localSpace
                .InverseTransformDirection(
                    recoilWorldDirection
                )
                .normalized;

        SetHitFlashOverlayVisible(true);
        ApplyHitFlashOverlayColor(1f);

        // 方块需要从表面朝外爆出，才能在第一人称视角中清楚可见。
        Vector3 outwardDirection =
            hitNormal.sqrMagnitude > 0.0001f
                ? hitNormal.normalized
                : -recoilWorldDirection;

        Vector3 spawnPoint =
            hitPoint + outwardDirection * 0.018f;

        GroundBotHitBlockBurstFX.Spawn(
            spawnPoint,
            outwardDirection,
            safeStrength,
            hitBlockParticleColor,
            hitMediumBlockCount,
            hitSmallBlockCount,
            hitLargeBlockCount,
            hitBlockLifetime,
            hitBlockOverallScale
        );
    }

    private void CacheHitFeedbackTransform()
    {
        if (_visualFeedbackCached || visualRoot == null)
        {
            return;
        }

        _visualBaseLocalPosition =
            visualRoot.localPosition;

        _visualBaseLocalScale =
            visualRoot.localScale;

        _visualFeedbackCached = true;
    }

    private void UpdateHitFeedback()
    {
        float deltaTime = Time.deltaTime;

        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer =
                Mathf.Max(
                    0f,
                    _hitFlashTimer - deltaTime
                );

            float safeDuration =
                Mathf.Max(
                    0.001f,
                    _activeHitFlashDuration
                );

            float elapsed =
                safeDuration - _hitFlashTimer;

            float safeWhiteHotDuration =
                Mathf.Min(
                    Mathf.Max(0.001f, hitWhiteHotDuration),
                    safeDuration
                );

            if (elapsed < safeWhiteHotDuration)
            {
                float whiteHotRemaining01 =
                    1f - Mathf.Clamp01(
                        elapsed / safeWhiteHotDuration
                    );

                SetHitFlashOverlayVisible(true);
                ApplyHitFlashOverlayColor(
                    whiteHotRemaining01
                );
            }
            else
            {
                // 白热瞬闪结束后，关闭全身颜色覆盖。
                // 后续只由原有发光区域承担深红余辉。
                SetHitFlashOverlayVisible(false);
            }
        }
        else
        {
            SetHitFlashOverlayVisible(false);
        }

        if (_hitPunchTimer <= 0f)
        {
            if (_visualFeedbackCached && visualRoot != null)
            {
                visualRoot.localPosition =
                    _visualBaseLocalPosition;

                // 明确禁止普通命中 Scale Punch。
                visualRoot.localScale =
                    _visualBaseLocalScale;
            }

            _activeHitStrength = 0f;
            return;
        }

        _hitPunchTimer =
            Mathf.Max(
                0f,
                _hitPunchTimer - deltaTime
            );

        if (!_visualFeedbackCached || visualRoot == null)
        {
            return;
        }

        float duration =
            Mathf.Max(
                0.001f,
                _activeHitPunchDuration
            );

        float remaining01 =
            Mathf.Clamp01(
                _hitPunchTimer / duration
            );

        // 命中第一帧达到峰值，然后快速回到原位。
        // 只保留极轻的机械余震，不改变模型大小。
        float recoilEnvelope =
            remaining01 * remaining01;

        float elapsed01 =
            1f - remaining01;

        float microJolt =
            1f +
            Mathf.Sin(
                elapsed01 * Mathf.PI * 2.5f
            ) *
            0.06f * remaining01;

        float recoil =
            hitRecoilDistance *
            _activeHitStrength *
            recoilEnvelope *
            microJolt;

        visualRoot.localPosition =
            _visualBaseLocalPosition +
            _hitPunchLocalDirection * recoil;

        visualRoot.localScale =
            _visualBaseLocalScale;
    }

    private void ResetHitFeedbackTransform()
    {
        _hitFlashTimer = 0f;
        _hitPunchTimer = 0f;
        _activeHitStrength = 0f;

        SetHitFlashOverlayVisible(false);

        if (_visualFeedbackCached && visualRoot != null)
        {
            visualRoot.localPosition =
                _visualBaseLocalPosition;

            visualRoot.localScale =
                _visualBaseLocalScale;
        }
    }

    private void BuildHitFlashOverlay()
    {
        if (_hitFlashOverlayBuilt || visualRoot == null)
        {
            return;
        }

        _hitFlashOverlayBuilt = true;
        CreateHitFlashOverlayMaterial();

        if (_hitFlashOverlayMaterial == null)
        {
            return;
        }

        MeshRenderer[] sourceRenderers =
            visualRoot.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer sourceRenderer in sourceRenderers)
        {
            if (sourceRenderer == null ||
                sourceRenderer.gameObject.name.StartsWith(
                    "GroundBotHitFlashOverlay_Runtime"
                ))
            {
                continue;
            }

            MeshFilter sourceFilter =
                sourceRenderer.GetComponent<MeshFilter>();

            if (sourceFilter == null ||
                sourceFilter.sharedMesh == null)
            {
                continue;
            }

            GameObject overlayObject =
                new GameObject(
                    "GroundBotHitFlashOverlay_Runtime"
                );

            overlayObject.layer =
                sourceRenderer.gameObject.layer;

            Transform overlayTransform =
                overlayObject.transform;

            overlayTransform.SetParent(
                sourceRenderer.transform,
                false
            );

            overlayTransform.localPosition =
                Vector3.zero;
            overlayTransform.localRotation =
                Quaternion.identity;
            overlayTransform.localScale =
                Vector3.one *
                Mathf.Max(
                    1.001f,
                    hitFlashOverlayScale
                );

            MeshFilter overlayFilter =
                overlayObject.AddComponent<MeshFilter>();

            overlayFilter.sharedMesh =
                sourceFilter.sharedMesh;

            MeshRenderer overlayRenderer =
                overlayObject.AddComponent<MeshRenderer>();

            overlayRenderer.sharedMaterial =
                _hitFlashOverlayMaterial;
            overlayRenderer.shadowCastingMode =
                ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage =
                LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage =
                ReflectionProbeUsage.Off;
            overlayRenderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            overlayRenderer.sortingLayerID =
                sourceRenderer.sortingLayerID;
            overlayRenderer.sortingOrder =
                sourceRenderer.sortingOrder + 1;
            overlayRenderer.enabled = false;

            _hitFlashOverlayRenderers.Add(
                overlayRenderer
            );
        }
    }

    private void CreateHitFlashOverlayMaterial()
    {
        if (_hitFlashOverlayMaterial != null)
        {
            return;
        }

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Unlit"
            );

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            Debug.LogWarning(
                "[GroundBotEnemy] 找不到可用的白热瞬闪 Shader，将只播放局部红色余辉、后坐与方块粒子。",
                this
            );
            return;
        }

        _hitFlashOverlayMaterial =
            new Material(shader)
            {
                name =
                    $"Runtime_GroundBotHitFlash_{GetEntityId()}",
                hideFlags =
                    HideFlags.HideAndDontSave,
                enableInstancing = true
            };

        if (_hitFlashOverlayMaterial.HasProperty("_BaseMap"))
        {
            _hitFlashOverlayMaterial.SetTexture(
                "_BaseMap",
                Texture2D.whiteTexture
            );
        }

        if (_hitFlashOverlayMaterial.HasProperty("_MainTex"))
        {
            _hitFlashOverlayMaterial.SetTexture(
                "_MainTex",
                Texture2D.whiteTexture
            );
        }

        ApplyHitFlashOverlayColor(1f);
    }

    private void ApplyHitFlashOverlayColor(
        float remaining01
    )
    {
        if (_hitFlashOverlayMaterial == null)
        {
            return;
        }

        float envelope =
            Mathf.Clamp01(remaining01);

        float intensity =
            Mathf.Lerp(
                0.68f,
                1.04f,
                envelope * envelope
            );

        Color outputColor =
            hitWhiteHotColor * intensity;

        outputColor.a = 1f;

        if (_hitFlashOverlayMaterial.HasProperty("_BaseColor"))
        {
            _hitFlashOverlayMaterial.SetColor(
                "_BaseColor",
                outputColor
            );
        }

        if (_hitFlashOverlayMaterial.HasProperty("_Color"))
        {
            _hitFlashOverlayMaterial.SetColor(
                "_Color",
                outputColor
            );
        }

        if (_hitFlashOverlayMaterial.HasProperty("_EmissionColor"))
        {
            _hitFlashOverlayMaterial.EnableKeyword("_EMISSION");
            _hitFlashOverlayMaterial.SetColor(
                "_EmissionColor",
                outputColor
            );
        }
    }

    private void SetHitFlashOverlayVisible(
        bool visible
    )
    {
        foreach (MeshRenderer overlayRenderer
                 in _hitFlashOverlayRenderers)
        {
            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = visible;
            }
        }
    }

    private void DestroyHitFlashOverlayMaterial()
    {
        if (_hitFlashOverlayMaterial == null)
        {
            return;
        }

        Destroy(_hitFlashOverlayMaterial);
        _hitFlashOverlayMaterial = null;
    }

    // =========================================================
    // 状态灯
    // =========================================================

    private void CacheWarningVisuals()
    {
        if ((warningRenderers == null || warningRenderers.Length == 0) &&
            visualRoot != null)
        {
            warningRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        }

        if (warningRenderers == null)
        {
            warningRenderers = new Renderer[0];
        }

        _warningPropertyBlock = new MaterialPropertyBlock();
        _currentStatusEmission = Color.black;
    }

    private void UpdateStatusLight()
    {
        if (_externalControlActive || _warningPropertyBlock == null)
        {
            return;
        }

        if (_shotEmissionTimer > 0f)
        {
            _shotEmissionTimer -= Time.deltaTime;
        }

        Color targetColor;
        float targetIntensity;

        if (_hitFlashTimer > 0f)
        {
            float safeDuration =
                Mathf.Max(0.001f, _activeHitFlashDuration);

            float remaining01 =
                Mathf.Clamp01(_hitFlashTimer / safeDuration);

            float elapsed =
                safeDuration - _hitFlashTimer;

            float safeWhiteHotDuration =
                Mathf.Min(
                    Mathf.Max(0.001f, hitWhiteHotDuration),
                    safeDuration
                );

            if (elapsed < safeWhiteHotDuration)
            {
                float whiteHotProgress01 =
                    Mathf.Clamp01(
                        elapsed / safeWhiteHotDuration
                    );

                // 最初是白热冲击，随后迅速收敛到深红核心余辉。
                targetColor =
                    Color.Lerp(
                        hitWhiteHotColor,
                        hitFlashColor,
                        whiteHotProgress01
                    );

                targetIntensity =
                    hitEmissionMultiplier *
                    Mathf.Lerp(1.12f, 0.92f, whiteHotProgress01) *
                    Mathf.Lerp(0.84f, 1.12f, _activeHitStrength / 1.5f);
            }
            else
            {
                float afterglowDuration =
                    Mathf.Max(
                        0.001f,
                        safeDuration - safeWhiteHotDuration
                    );

                float afterglowRemaining01 =
                    Mathf.Clamp01(
                        _hitFlashTimer / afterglowDuration
                    );

                targetColor = hitFlashColor;
                targetIntensity =
                    hitEmissionMultiplier *
                    Mathf.Lerp(
                        0.42f,
                        1.00f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            afterglowRemaining01
                        )
                    ) *
                    Mathf.Lerp(0.84f, 1.10f, _activeHitStrength / 1.5f);
            }
        }
        else if (_shotEmissionTimer > 0f)
        {
            targetColor = lockedRedColor;
            targetIntensity = shotEmissionMultiplier;
        }
        else if (currentState == GroundBotState.Aim)
        {
            float safeDuration = Mathf.Max(0.001f, _activeAimDuration);
            float progress = 1f - Mathf.Clamp01(_stateTimer / safeDuration);
            targetColor = lockedRedColor;
            targetIntensity = Mathf.Lerp(
                lockedBaseIntensity,
                aimEmissionMultiplier,
                progress
            );
        }
        else if (currentState == GroundBotState.HighAlert)
        {
            targetColor = alertOrangeColor;
            targetIntensity = alertBaseIntensity +
                alertPulseIntensity * EvaluateDoublePulse(Time.time);
        }
        else if (currentState == GroundBotState.Scanning)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(
                Time.time * Mathf.PI * 2f * alertPulseFrequency
            );
            targetColor = alertOrangeColor;
            targetIntensity = alertBaseIntensity + alertPulseIntensity * pulse;
        }
        else if (IsRedStatusState(currentState))
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(
                Time.time * Mathf.PI * 2f * lockedPulseFrequency
            );
            targetColor = lockedRedColor;
            targetIntensity = lockedBaseIntensity + lockedPulseIntensity * pulse;
        }
        else
        {
            targetColor = idleBlueColor;
            targetIntensity = idleLightIntensity;
        }

        Color desired = targetColor * Mathf.Max(0f, targetIntensity);
        _currentStatusEmission = Color.Lerp(
            _currentStatusEmission,
            desired,
            1f - Mathf.Exp(-warningResponseSpeed * Time.deltaTime)
        );

        ApplyStatusEmission(_currentStatusEmission);
    }


    private static bool IsLockedCombatState(GroundBotState state)
    {
        return state == GroundBotState.SearchLastSeen ||
               state == GroundBotState.Approach ||
               state == GroundBotState.Holding ||
               state == GroundBotState.Retreat ||
               state == GroundBotState.Aim ||
               state == GroundBotState.BurstFire ||
               state == GroundBotState.Cooldown;
    }

    private static bool IsRedStatusState(GroundBotState state)
    {
        return state == GroundBotState.SearchLastSeen ||
               state == GroundBotState.Approach ||
               state == GroundBotState.Holding ||
               state == GroundBotState.Retreat ||
               state == GroundBotState.BurstFire ||
               state == GroundBotState.Cooldown;
    }

    private static float EvaluateDoublePulse(float time)
    {
        float phase = Mathf.Repeat(time, 0.72f);
        float first = 1f - Mathf.Clamp01(Mathf.Abs(phase - 0.08f) / 0.08f);
        float second = 1f - Mathf.Clamp01(Mathf.Abs(phase - 0.27f) / 0.08f);
        return Mathf.Max(first, second);
    }

    private void ApplyStatusEmission(Color emissionColor)
    {
        for (int index = 0; index < warningRenderers.Length; index++)
        {
            Renderer targetRenderer = warningRenderers[index];

            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(_warningPropertyBlock);
            _warningPropertyBlock.SetColor("_EmissionColor", emissionColor);
            targetRenderer.SetPropertyBlock(_warningPropertyBlock);
        }
    }

    private void ClearStatusEmissionOverride()
    {
        if (warningRenderers == null)
        {
            return;
        }

        for (int index = 0; index < warningRenderers.Length; index++)
        {
            Renderer targetRenderer = warningRenderers[index];

            if (targetRenderer != null)
            {
                targetRenderer.SetPropertyBlock(null);
            }
        }

        _currentStatusEmission = Color.black;
    }

    // =========================================================
    // 玩家、受击、高警戒与回溯
    // =========================================================

    private bool IsCombatGateOpen()
    {
        if (_playerHealth != null && _playerHealth.IsRewinding)
        {
            return false;
        }

        if (combatEncounter != null)
        {
            return combatEncounter.AllowsCombat;
        }

        if (combatPlatform != null)
        {
            return combatPlatform.IsPlayerInside;
        }

        return currentPlayer != null &&
               GetFlatDistanceToPlayer() <= detectionRange;
    }

    private void ResolveCombatEncounterReference()
    {
        if (combatEncounter == null && combatPlatform != null)
        {
            combatEncounter = combatPlatform.ActivationEncounter;
        }
    }

    private void EnsureEncounterMembership()
    {
        if (combatEncounter == null || enemyTarget == null)
        {
            return;
        }

        if (_encounterMember == null)
        {
            _encounterMember = GetComponent<CombatEncounterMember>();
        }

        if (_encounterMember == null)
        {
            _encounterMember =
                gameObject.AddComponent<CombatEncounterMember>();
        }

        _encounterMember.Configure(
            combatEncounter,
            enemyTarget,
            requiredForEncounterClear
        );
    }

    private void BindCombatEncounterEvents()
    {
        ResolveCombatEncounterReference();

        if (_boundCombatEncounter == combatEncounter)
        {
            return;
        }

        UnbindCombatEncounterEvents();
        _boundCombatEncounter = combatEncounter;

        if (_boundCombatEncounter == null)
        {
            return;
        }

        _boundCombatEncounter.EncounterActivated +=
            HandleEncounterActivated;
        _boundCombatEncounter.EncounterSuspended +=
            HandleEncounterSuspended;
        _boundCombatEncounter.EncounterCleared +=
            HandleEncounterCleared;
        _boundCombatEncounter.EncounterReset +=
            HandleEncounterReset;
    }

    private void UnbindCombatEncounterEvents()
    {
        if (_boundCombatEncounter == null)
        {
            return;
        }

        _boundCombatEncounter.EncounterActivated -=
            HandleEncounterActivated;
        _boundCombatEncounter.EncounterSuspended -=
            HandleEncounterSuspended;
        _boundCombatEncounter.EncounterCleared -=
            HandleEncounterCleared;
        _boundCombatEncounter.EncounterReset -=
            HandleEncounterReset;
        _boundCombatEncounter = null;
    }

    private void HandleEncounterActivated(
        CombatEncounter sourceEncounter
    )
    {
        if (sourceEncounter != combatEncounter ||
            (enemyTarget != null && enemyTarget.IsDead))
        {
            return;
        }

        _combatGateWasOpen = true;
        RefreshPlayerReference(true);

        if (_rewindSuspended || _externalControlActive)
        {
            SetContactDamageEnabled(false);
            return;
        }

        HandleCombatGateOpened(true);
    }

    private void HandleEncounterSuspended(
        CombatEncounter sourceEncounter
    )
    {
        if (sourceEncounter != combatEncounter)
        {
            return;
        }

        _combatGateWasOpen = false;
        HandleCombatGateClosed();
    }

    private void HandleEncounterCleared(
        CombatEncounter sourceEncounter
    )
    {
        if (sourceEncounter != combatEncounter)
        {
            return;
        }

        _combatGateWasOpen = false;
        HandleCombatGateClosed();
    }

    private void HandleEncounterReset(
        CombatEncounter sourceEncounter
    )
    {
        if (sourceEncounter != combatEncounter)
        {
            return;
        }

        _combatGateWasOpen = false;
        HandleCombatGateClosed();
    }

    private void HandleCombatGateOpened(
        bool showSuspicionIndicator
    )
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        if (_rewindSuspended || _externalControlActive)
        {
            SetContactDamageEnabled(false);
            return;
        }

        RefreshPlayerReference(true);
        SetContactDamageEnabled(true);
        EnterScanning(showSuspicionIndicator);
    }

    private void HandleCombatGateClosed()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            SetContactDamageEnabled(false);
            return;
        }

        // Do not despawn already-fired projectiles here. Encounter suspension
        // cancels only pending Aim/Burst state; active projectiles finish their
        // own lifetime. Rewind still clears them separately.
        EnterSuspended();
    }

    private void SetContactDamageEnabled(bool damageEnabled)
    {
        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(damageEnabled);
        }
    }

    private void BindEnemyDamageEvent()
    {
        if (_damageEventBound || enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged += HandleEnemyDamaged;
        _damageEventBound = true;
    }

    private void UnbindEnemyDamageEvent()
    {
        if (!_damageEventBound || enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged -= HandleEnemyDamaged;
        _damageEventBound = false;
    }

    private void HandleEnemyDamaged(float damage, float remainingHealth, Vector3 hitPoint)
    {
        if (damage <= 0f || _externalControlActive || !IsCombatGateOpen())
        {
            return;
        }

        if (enemyTarget == null ||
            enemyTarget.LastDamageSource != EnemyDamageSource.PlayerWeapon)
        {
            return;
        }

        if (CanVisuallyDetectPlayer())
        {
            if (!IsLockedCombatState(currentState))
            {
                AcquirePlayer();
            }

            return;
        }

        Vector3 suspectedPosition = currentPlayer != null
            ? currentPlayer.position
            : GetCurrentPosition() - transform.forward * 4f;

        EnterHighAlert(suspectedPosition);
    }

    private void RefreshPlayerReference(bool forceSearch)
    {
        if (combatEncounter != null)
        {
            if (!combatEncounter.AllowsCombat)
            {
                currentPlayer = null;
                return;
            }

            if (combatEncounter.Player != null)
            {
                currentPlayer = combatEncounter.Player;
                return;
            }
        }

        if (combatPlatform != null && combatPlatform.Player != null)
        {
            currentPlayer = combatPlatform.Player;
            return;
        }

        if (!forceSearch && currentPlayer != null)
        {
            return;
        }

        if (!forceSearch && Time.time < _nextPlayerSearchTime)
        {
            return;
        }

        _nextPlayerSearchTime = Time.time + 0.5f;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        currentPlayer = playerObject != null ? playerObject.transform : null;
    }

    private void BindPlayerHealth()
    {
        PlayerHealth resolvedHealth = null;

        if (currentPlayer != null)
        {
            resolvedHealth = currentPlayer.GetComponentInParent<PlayerHealth>();
        }

        if (resolvedHealth == null)
        {
            resolvedHealth = PlayerHealth.Instance;
        }

        if (resolvedHealth == _playerHealth)
        {
            return;
        }

        UnbindPlayerHealth();
        _playerHealth = resolvedHealth;
        _playerCharacterController = _playerHealth != null
            ? _playerHealth.GetComponent<CharacterController>()
            : null;

        if (_playerHealth == null)
        {
            return;
        }

        _playerHealth.RewindStarted += HandleRewindStarted;
        _playerHealth.RewindCompleted += HandleRewindCompleted;
    }

    private void UnbindPlayerHealth()
    {
        if (_playerHealth == null)
        {
            return;
        }

        _playerHealth.RewindStarted -= HandleRewindStarted;
        _playerHealth.RewindCompleted -= HandleRewindCompleted;
        _playerHealth = null;
        _playerCharacterController = null;
    }

    private void HandleRewindStarted()
    {
        _rewindSuspended = true;
        EnterSuspended();
        DespawnAllOwnedProjectiles();
    }

    private void HandleRewindCompleted()
    {
        _rewindSuspended = false;

        if (_externalControlActive)
        {
            return;
        }

        RefreshPlayerReference(true);

        if (IsCombatGateOpen())
        {
            SetContactDamageEnabled(true);

            if (CanVisuallyDetectPlayer())
            {
                AcquirePlayer();
            }
            else
            {
                EnterScanning();
            }
        }
        else
        {
            HandleCombatGateClosed();
        }
    }

    // =========================================================
    // 音频
    // =========================================================

    private void PrepareShotAudioSource()
    {
        if (shotAudioSource == null)
        {
            shotAudioSource = GetComponent<AudioSource>();
        }

        if (shotAudioSource == null)
        {
            shotAudioSource = gameObject.AddComponent<AudioSource>();
        }

        shotAudioSource.playOnAwake = false;
        shotAudioSource.loop = false;
        shotAudioSource.spatialBlend = 1f;
        shotAudioSource.dopplerLevel = 0f;
        shotAudioSource.rolloffMode = AudioRolloffMode.Linear;
        shotAudioSource.minDistance = 2f;
        shotAudioSource.maxDistance = 22f;
    }

    private void PlayShotAudio()
    {
        if (shotAudioSource == null)
        {
            return;
        }

        AudioClip clip = shotAudioClip;

        if (clip == null && generateFallbackShotSound)
        {
            clip = GetOrCreateFallbackShotClip();
        }

        if (clip == null || shotVolume <= 0f)
        {
            return;
        }

        float minimumPitch = Mathf.Min(shotPitchRange.x, shotPitchRange.y);
        float maximumPitch = Mathf.Max(shotPitchRange.x, shotPitchRange.y);
        shotAudioSource.pitch = Random.Range(minimumPitch, maximumPitch);
        shotAudioSource.PlayOneShot(clip, Mathf.Clamp01(shotVolume));
    }

    private static AudioClip GetOrCreateFallbackShotClip()
    {
        if (_sharedFallbackShotClip != null)
        {
            return _sharedFallbackShotClip;
        }

        const int sampleRate = 44100;
        const float duration = 0.13f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        int noiseState = 0x13579B;

        for (int index = 0; index < sampleCount; index++)
        {
            float time = (float)index / sampleRate;
            float normalizedTime = Mathf.Clamp01(time / duration);
            float attack = Mathf.Clamp01(normalizedTime / 0.025f);
            float decay = Mathf.Exp(-normalizedTime * 6.8f);
            float envelope = attack * decay * (1f - normalizedTime);
            float lowFrequency = Mathf.Lerp(300f, 145f, normalizedTime);
            float highFrequency = Mathf.Lerp(980f, 420f, normalizedTime);
            float lowTone = Mathf.Sin(Mathf.PI * 2f * lowFrequency * time);
            float highTone = Mathf.Sin(Mathf.PI * 2f * highFrequency * time);

            unchecked
            {
                noiseState = noiseState * 1103515245 + 12345;
            }

            float noise = (((noiseState >> 16) & 0x7FFF) / 16383f) * 2f - 1f;
            float sample = lowTone * 0.58f + highTone * 0.22f + noise * 0.20f;
            samples[index] = Mathf.Clamp(sample * envelope * 0.82f, -1f, 1f);
        }

        _sharedFallbackShotClip = AudioClip.Create(
            "GroundBot_Shot_Fallback",
            sampleCount,
            1,
            sampleRate,
            false
        );
        _sharedFallbackShotClip.SetData(samples, 0);
        return _sharedFallbackShotClip;
    }

    // =========================================================
    // 公共工具
    // =========================================================

    private void ResetAttackRuntime()
    {
        shotsRemaining = 0;
        _nextShotTime = 0f;
        hasLineOfSight = false;
        isPlayerInVisionCone = false;
    }

    private Vector3 GetFlatDirectionToPlayer()
    {
        if (currentPlayer == null)
        {
            return Vector3.zero;
        }

        Vector3 direction = currentPlayer.position - GetCurrentPosition();
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.zero;
    }

    private float GetFlatDistanceToPlayer()
    {
        if (currentPlayer == null)
        {
            return float.PositiveInfinity;
        }

        Vector3 difference = currentPlayer.position - GetCurrentPosition();
        difference.y = 0f;
        return difference.magnitude;
    }

    private Vector3 GetCurrentPosition()
    {
        return enemyRigidbody != null ? enemyRigidbody.position : transform.position;
    }

    private Quaternion GetCurrentRotation()
    {
        return enemyRigidbody != null ? enemyRigidbody.rotation : transform.rotation;
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    // =========================================================
    // 自动引用与验证
    // =========================================================

    [ContextMenu("Auto Assign Ground Bot References")]
    private void AutoAssignReferences()
    {
        if (combatPlatform == null)
        {
            combatPlatform = GetComponentInParent<CombatPlatform>();
        }

        if (combatEncounter == null && combatPlatform != null)
        {
            combatEncounter = combatPlatform.ActivationEncounter;
        }

        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        if (contactDamage == null)
        {
            contactDamage = GetComponentInChildren<EnemyContactDamage>(true);
        }

        if (enemyRigidbody == null)
        {
            enemyRigidbody = GetComponent<Rigidbody>();
        }

        if (shotAudioSource == null)
        {
            shotAudioSource = GetComponent<AudioSource>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform.Find("VisualRoot");
        }

        if (aimPoint == null)
        {
            aimPoint = transform.Find("AimPoint");
        }

        if (muzzlePoint == null)
        {
            muzzlePoint = transform.Find("MuzzlePoint");
        }

        if (groundCheckFront == null)
        {
            groundCheckFront = transform.Find("GroundCheck/GroundCheckFront");
        }

        if (groundCheckBack == null)
        {
            groundCheckBack = transform.Find("GroundCheck/GroundCheckBack");
        }

        if ((warningRenderers == null || warningRenderers.Length == 0) &&
            visualRoot != null)
        {
            warningRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        }
    }

    private void ValidateRuntimeSetup()
    {
        if (enemyRigidbody == null)
        {
            Debug.LogError("[GroundBotEnemy] GroundBot 根对象缺少 Rigidbody。", this);
        }

        if (enemyTarget == null)
        {
            Debug.LogError("[GroundBotEnemy] GroundBot 根对象缺少 EnemyTarget。", this);
        }

        if (groundCheckFront == null || groundCheckBack == null)
        {
            Debug.LogError("[GroundBotEnemy] 缺少 GroundCheckFront 或 GroundCheckBack。", this);
        }

        if (muzzlePoint == null)
        {
            Debug.LogError("[GroundBotEnemy] 缺少 MuzzlePoint。", this);
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                "[GroundBotEnemy] Projectile Prefab 尚未绑定，Ground Bot 不会开火。",
                this
            );
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        detectionRange = Mathf.Max(0.1f, detectionRange);
        visionRange = Mathf.Max(0.1f, visionRange);
        visionAngle = Mathf.Clamp(visionAngle, 1f, 179f);
        scanHalfAngle = Mathf.Clamp(scanHalfAngle, 1f, 179f);
        scanTurnSpeed = Mathf.Max(0f, scanTurnSpeed);
        scanEndPause = Mathf.Max(0f, scanEndPause);
        targetMemoryDuration = Mathf.Max(0f, targetMemoryDuration);
        highAlertDuration = Mathf.Max(0f, highAlertDuration);
        highAlertTurnSpeed = Mathf.Max(0f, highAlertTurnSpeed);
        stateIndicatorHeightPadding = Mathf.Max(0.05f, stateIndicatorHeightPadding);
        suspicionIndicatorDuration = Mathf.Clamp(suspicionIndicatorDuration, 0.20f, 1.20f);
        detectedIndicatorDuration = Mathf.Clamp(detectedIndicatorDuration, 0.20f, 1.20f);
        stateIndicatorScale = Mathf.Clamp(stateIndicatorScale, 0.55f, 1.75f);

        approachStartDistance = Mathf.Max(0.1f, approachStartDistance);
        approachStopDistance = Mathf.Clamp(
            approachStopDistance,
            0.1f,
            approachStartDistance
        );
        retreatStartDistance = Mathf.Clamp(
            retreatStartDistance,
            0.1f,
            approachStopDistance
        );
        retreatStopDistance = Mathf.Clamp(
            retreatStopDistance,
            retreatStartDistance,
            approachStopDistance
        );

        approachSpeed = Mathf.Max(0f, approachSpeed);
        retreatSpeed = Mathf.Max(0f, retreatSpeed);
        attackMovementMultiplier = Mathf.Clamp01(attackMovementMultiplier);
        turnSpeed = Mathf.Max(0f, turnSpeed);

        groundCheckDistance = Mathf.Max(0.05f, groundCheckDistance);
        boundaryLookAheadDistance = Mathf.Max(0.05f, boundaryLookAheadDistance);
        obstacleProbeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
        obstacleProbeDistance = Mathf.Max(0.01f, obstacleProbeDistance);
        obstacleProbeStartOffset = Mathf.Max(0f, obstacleProbeStartOffset);

        projectilePoolSize = Mathf.Max(1, projectilePoolSize);
        maximumFireDistance = Mathf.Max(0.1f, maximumFireDistance);
        burstShotCount = Mathf.Max(1, burstShotCount);
        burstShotInterval = Mathf.Max(0.01f, burstShotInterval);
        nearBandMaxDistance = Mathf.Max(retreatStartDistance, nearBandMaxDistance);
        midBandMaxDistance = Mathf.Max(nearBandMaxDistance, midBandMaxDistance);
        nearAimDuration = Mathf.Max(0f, nearAimDuration);
        midAimDuration = Mathf.Max(0f, midAimDuration);
        farAimDuration = Mathf.Max(0f, farAimDuration);
        nearBurstCooldown = Mathf.Max(0f, nearBurstCooldown);
        midBurstCooldown = Mathf.Max(0f, midBurstCooldown);
        farBurstCooldown = Mathf.Max(0f, farBurstCooldown);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileDamage = Mathf.Max(0f, projectileDamage);
        projectileLifeTime = Mathf.Max(0.05f, projectileLifeTime);
        lineOfSightRadius = Mathf.Max(0f, lineOfSightRadius);

        idleLightIntensity = Mathf.Max(0f, idleLightIntensity);
        alertBaseIntensity = Mathf.Max(0f, alertBaseIntensity);
        alertPulseIntensity = Mathf.Max(0f, alertPulseIntensity);
        alertPulseFrequency = Mathf.Max(0.01f, alertPulseFrequency);
        lockedBaseIntensity = Mathf.Max(0f, lockedBaseIntensity);
        lockedPulseIntensity = Mathf.Max(0f, lockedPulseIntensity);
        lockedPulseFrequency = Mathf.Max(0.01f, lockedPulseFrequency);
        aimEmissionMultiplier = Mathf.Max(1f, aimEmissionMultiplier);
        shotEmissionMultiplier = Mathf.Max(1f, shotEmissionMultiplier);
        warningResponseSpeed = Mathf.Max(0.01f, warningResponseSpeed);
        shotEmissionFlashDuration = Mathf.Max(0.01f, shotEmissionFlashDuration);

        shotVolume = Mathf.Clamp01(shotVolume);
        shotPitchRange.x = Mathf.Clamp(shotPitchRange.x, 0.1f, 3f);
        shotPitchRange.y = Mathf.Clamp(shotPitchRange.y, 0.1f, 3f);

        hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
        hitWhiteHotDuration = Mathf.Clamp(
            hitWhiteHotDuration,
            0.01f,
            Mathf.Max(0.01f, hitFlashDuration)
        );
        hitEmissionMultiplier = Mathf.Max(0f, hitEmissionMultiplier);
        hitRecoilDistance = Mathf.Max(0f, hitRecoilDistance);
        hitPunchDuration = Mathf.Max(0.01f, hitPunchDuration);
        hitMediumBlockCount.x = Mathf.Max(0, hitMediumBlockCount.x);
        hitMediumBlockCount.y = Mathf.Max(hitMediumBlockCount.x, hitMediumBlockCount.y);
        hitSmallBlockCount.x = Mathf.Max(0, hitSmallBlockCount.x);
        hitSmallBlockCount.y = Mathf.Max(hitSmallBlockCount.x, hitSmallBlockCount.y);
        hitLargeBlockCount.x = Mathf.Max(0, hitLargeBlockCount.x);
        hitLargeBlockCount.y = Mathf.Max(hitLargeBlockCount.x, hitLargeBlockCount.y);
        hitBlockLifetime = Mathf.Clamp(hitBlockLifetime, 0.50f, 1.05f);
        hitBlockOverallScale = Mathf.Max(0.05f, hitBlockOverallScale);
        hitFlashOverlayScale = Mathf.Clamp(hitFlashOverlayScale, 1.001f, 1.03f);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        if (drawVisionGizmos)
        {
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.85f);
            DrawFlatCircle(center, visionRange);
            DrawVisionCone(center, transform.forward, visionRange, visionAngle);

            Gizmos.color = new Color(1f, 0.55f, 0.08f, 0.9f);
            DrawDirectionRay(center, Quaternion.Euler(0f, -scanHalfAngle, 0f) * transform.forward, 3f);
            DrawDirectionRay(center, Quaternion.Euler(0f, scanHalfAngle, 0f) * transform.forward, 3f);
        }

        Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.8f);
        DrawFlatCircle(center, approachStartDistance);
        Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.8f);
        DrawFlatCircle(center, retreatStartDistance);

        DrawGroundRayGizmo(groundCheckFront, Color.green);
        DrawGroundRayGizmo(groundCheckBack, Color.blue);

        if (aimPoint != null && currentPlayer != null)
        {
            Gizmos.color = hasLineOfSight ? Color.cyan : Color.red;
            Gizmos.DrawLine(aimPoint.position, GetPlayerAimPosition());
        }
    }

    private void DrawVisionCone(Vector3 center, Vector3 forward, float range, float angle)
    {
        Vector3 left = Quaternion.Euler(0f, -angle * 0.5f, 0f) * forward;
        Vector3 right = Quaternion.Euler(0f, angle * 0.5f, 0f) * forward;
        DrawDirectionRay(center, left, range);
        DrawDirectionRay(center, right, range);

        const int segments = 20;
        Vector3 previous = center + left.normalized * range;

        for (int index = 1; index <= segments; index++)
        {
            float yaw = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, index / (float)segments);
            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * forward;
            Vector3 next = center + direction.normalized * range;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private static void DrawDirectionRay(Vector3 origin, Vector3 direction, float length)
    {
        Gizmos.DrawLine(origin, origin + direction.normalized * length);
    }

    private void DrawGroundRayGizmo(Transform groundCheck, Color color)
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = color;
        Vector3 origin = groundCheck.position + Vector3.up * 0.08f;
        Gizmos.DrawSphere(origin, 0.035f);
        Gizmos.DrawLine(origin, origin + Vector3.down * groundCheckDistance);
    }

    private static void DrawFlatCircle(Vector3 center, float radius)
    {
        const int segmentCount = 48;
        Vector3 previousPoint = center + Vector3.forward * radius;

        for (int index = 1; index <= segmentCount; index++)
        {
            float angle = index / (float)segmentCount * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius
            );
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
#endif
}
