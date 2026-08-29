#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Flying Bot Phase 2A foundation.
///
/// Scope of this phase:
/// - independent 3D patrol volume;
/// - CombatEncounter activation/suspension;
/// - vision cone + Environment LOS;
/// - suspicious scanning and target memory;
/// - mobile strafe/reposition behaviour while the player is visible;
/// - obstacle avoidance and volume clamping;
/// - shared EnemyTarget / health bar / external-control compatibility.
///
/// Phase 2B adds a telegraphed twin predictive interception shot:
/// - the first pair drives the player out of their current lane;
/// - the second pair re-acquires that reaction and brackets the new route;
/// - every projectile remains straight after its own lock point;
/// - no homing or post-lock correction is used.
///
/// The telegraphed dive attack is intentionally NOT implemented yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlyingBotEnemy : MonoBehaviour, IEnemyExternalControlReceiver
{
    public enum FlyingBotState
    {
        DormantPatrol,
        Suspicious,
        EngageReposition,
        SearchLastSeen,
        SuspendedHover,
        Stunned
    }

    [Header("Core References")]
    public CombatEncounter combatEncounter;
    public FlyingPatrolVolume patrolVolume;
    public EnemyTarget enemyTarget;
    public Rigidbody enemyRigidbody;

    [Tooltip("Visual-only root. Hover bob is applied here, never to the physics root.")]
    public Transform visualRoot;

    [Tooltip("Optional transform used as the center of vision and future muzzle aiming.")]
    public Transform aimPoint;

    [Tooltip("When enabled, this enemy counts toward CombatEncounter clear state.")]
    public bool requiredForEncounterClear = true;

    [Header("Dormant Patrol")]
    [Tooltip("Before the encounter is activated for the first time, the bot slowly patrols without detecting or attacking the player.")]
    public bool patrolBeforeFirstActivation = true;

    [Min(0f)]
    public float dormantPatrolSpeed = 1.45f;

    [Min(0f)]
    public float suspiciousPatrolSpeed = 1.15f;

    [Min(0.05f)]
    public float patrolArrivalRadius = 0.65f;

    public Vector2 patrolWaitRange = new Vector2(0.25f, 0.75f);

    [Min(0f)]
    [Tooltip("Keeps random patrol targets away from the volume walls.")]
    public float patrolWallPadding = 0.65f;

    [Header("Vision / Alert")]
    [Min(0.1f)]
    public float visionRange = 30f;

    [Range(1f, 179f)]
    public float visionAngle = 76f;

    [Min(0f)]
    [Tooltip("Inside this range the bot can notice the player in any direction, provided Environment does not block line of sight.")]
    public float nearbyAwarenessRange = 8f;

    [Range(1f, 179f)]
    public float scanHalfAngle = 70f;

    [Min(0f)]
    public float scanTurnSpeed = 58f;

    [Min(0f)]
    public float scanEndPause = 0.20f;

    [Min(0f)]
    public float targetMemoryDuration = 3.5f;

    [Min(0f)]
    public float playerAimHeightOffset = 1f;

    [Min(0f)]
    public float lineOfSightRadius = 0.045f;

    [Tooltip("Should contain Environment only.")]
    public LayerMask obstacleMask;

    [Header("Combat Reposition")]
    [Min(0.1f)]
    public float preferredCombatDistance = 10.5f;

    [Min(0f)]
    public float preferredHeightAbovePlayer = 3.4f;

    [Min(0f)]
    public float strafeOffset = 3.0f;

    [Min(0f)]
    public float combatMoveSpeed = 5.0f;

    public Vector2 strafeSwitchInterval = new Vector2(1.35f, 2.20f);

    [Min(0f)]
    public float searchMoveSpeed = 3.4f;

    [Header("Twin Intercept Shot")]
    [Tooltip("Enables the Phase 2B twin predictive interception attack.")]
    public bool enableTwinInterceptShot = true;

    [Min(0.05f)]
    [Tooltip("Visible charge time before the first projectile leaves AimPoint.")]
    public float interceptTelegraphDuration = 0.42f;

    [Min(0f)]
    [Tooltip("Minimum movement prediction time used when the telegraph ends.")]
    public float interceptPredictionTime = 0.45f;

    [Min(0f)]
    [Tooltip("Maximum movement prediction time used when the telegraph ends.")]
    public float interceptMaximumPredictionTime = 0.85f;

    [Min(0f)]
    [Tooltip("How far the delayed second projectile covers along the player's escape direction.")]
    public float interceptHalfSpacing = 1.0f;

    [Min(0f)]
    [Tooltip("Delay between the center shot and the escape-covering shot.")]
    public float interceptSecondShotDelay = 0.22f;

    [Min(0f)]
    [Tooltip("Time after the first shot before the second shot resamples and locks the player's new movement.")]
    public float interceptSecondShotLockDelay = 0.12f;

    [Min(0f)]
    [Tooltip("Pause after the first pair before the second pair begins.")]
    public float interceptSecondGroupDelay = 0.42f;

    [Min(0f)]
    [Tooltip("How long before the third shot the second pair resamples and locks the player's route.")]
    public float interceptSecondGroupLockLeadTime = 0.20f;

    [Min(0f)]
    [Tooltip("Delay between the third and fourth shots.")]
    public float interceptFourthShotDelay = 0.24f;

    [Min(0f)]
    [Tooltip("Half the distance between the two targets used by the second pair.")]
    public float interceptSecondGroupHalfSpacing = 0.90f;

    [Min(0f)]
    [Tooltip("Virtual left/right muzzle offset used by the second pair to make its trajectories visually distinct.")]
    public float interceptSecondGroupMuzzleOffset = 0.30f;

    [Min(0f)]
    [Tooltip("Small horizontal uncertainty so the prediction never feels mathematically perfect.")]
    public float interceptPredictionError = 0.12f;

    [Min(0f)]
    [Tooltip("Below this horizontal player speed, the second shot uses the bot's current strafe side as its fallback escape direction.")]
    public float interceptDirectionalSpeedThreshold = 0.65f;

    [Min(0.1f)]
    public float interceptMinimumRange = 6.0f;

    [Min(0.1f)]
    public float interceptMaximumRange = 18.0f;

    [Min(0f)]
    [Tooltip("Delay after first acquiring the player before the first interception charge can begin.")]
    public float interceptInitialDelay = 0.65f;

    [Tooltip("Random cooldown range after a completed twin shot.")]
    public Vector2 interceptCooldownRange = new Vector2(1.55f, 1.85f);

    [Range(0.1f, 1f)]
    [Tooltip("Movement multiplier while the interception charge is visible.")]
    public float interceptTelegraphMoveMultiplier = 0.68f;

    [Min(0.1f)]
    public float interceptProjectileSpeed = 24f;

    [Min(0f)]
    [Tooltip("Damage per projectile. Both can hit, so keep this lower than a full Ground Bot burst.")]
    public float interceptProjectileDamage = 12f;

    [Min(0.1f)]
    public float interceptProjectileLifeTime = 4f;

    [Min(0.01f)]
    public float interceptProjectileRadius = 0.10f;

    [Tooltip("Projectile collision mask. Triggers are ignored by the projectile.")]
    public LayerMask interceptProjectileCollisionMask = ~0;

    [Header("Twin Intercept Audio")]
    [Tooltip("Optional 3D source. A configured source is created at runtime when this is empty.")]
    public AudioSource interceptAudioSource;

    [Tooltip("Optional custom clips. Empty slots use distinct generated prototype cues.")]
    public AudioClip interceptLockSound;
    public AudioClip interceptReacquireSound;
    public AudioClip interceptProbeShotSound;
    public AudioClip interceptPunishmentShotSound;

    [Range(0f, 1f)]
    public float interceptAudioVolume = 0.58f;

    public bool generateFallbackInterceptAudio = true;

    [Min(0f)]
    [Tooltip("Maximum player speed considered by the predictor, preventing extreme dash samples from producing absurd lead points.")]
    public float interceptMaximumPredictionSpeed = 18f;

    [Header("Dive Attack")]
    public bool enableDiveAttack = true;

    [Min(0.1f)]
    public float diveMinimumRange = 4f;

    [Min(0.1f)]
    public float diveMaximumRange = 11f;

    [Min(0f)]
    public float diveInitialDelay = 1.4f;

    public Vector2 diveCooldownRange = new Vector2(5f, 7f);

    [Min(0.05f)]
    [Tooltip("Backward bow-string movement before the body finishes charging.")]
    public float divePullbackDuration = 0.32f;

    [Min(0f)]
    public float divePullbackDistance = 0.80f;

    [Min(0f)]
    public float divePullbackLift = 0.25f;

    [Min(0.05f)]
    public float diveChargeDuration = 0.26f;

    [Min(0.05f)]
    [Tooltip("Hard-lock window. The target no longer moves during this time.")]
    public float diveLockDuration = 0.20f;

    [Min(0.1f)]
    public float diveSpeed = 24f;

    [Min(0f)]
    public float diveOvershootDistance = 3f;

    [Min(0f)]
    public float divePredictionTime = 0.10f;

    [Min(0.1f)]
    public float diveHitRadius = 0.85f;

    [Min(0f)]
    public float diveDamage = 30f;

    [Min(0f)]
    [Tooltip("Legacy horizontal-only knockback distance used when airborne launch is disabled.")]
    public float diveKnockbackDistance = 0.8f;

    [Min(0.01f)]
    [Tooltip("Legacy horizontal-only knockback duration used when airborne launch is disabled.")]
    public float diveKnockbackDuration = 0.12f;

    [Header("Dive Player Launch")]
    [Tooltip("Launches the player upward while preserving normal aerial steering after a dive hit.")]
    public bool useAirborneDiveLaunch = false;

    [Min(0f)]
    public float diveLaunchHorizontalSpeed = 10f;

    [Min(0f)]
    public float diveLaunchUpwardSpeed = 12f;

    [Min(0f)]
    public float diveLaunchMaxHorizontalSpeed = 18f;

    [Min(0f)]
    public float diveLaunchNoDragDuration = 0.5f;

    [Min(0f)]
    public float diveLaunchGraceTime = 0.16f;

    [Min(0.05f)]
    public float diveRecoveryDuration = 0.65f;

    [Min(0.05f)]
    public float diveCrashRecoveryDuration = 1f;

    [Range(0f, 25f)]
    public float diveWindupTiltDegrees = 11f;

    [ColorUsage(true, true)]
    public Color diveChargeOrange = new Color(2.8f, 0.48f, 0.035f, 1f);

    [Header("Movement Response")]
    [Min(0f)]
    public float acceleration = 11f;

    [Min(0f)]
    public float turnSpeed = 240f;

    [Min(0f)]
    [Tooltip("The AI root is kept this far inside the patrol volume walls.")]
    public float volumeBoundaryPadding = 0.45f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Uses the enemy's non-trigger BoxColliders as the exact movement sweep shapes. Body and both wings are checked separately so the bot cannot rotate or translate through Environment geometry.")]
    public bool useHitboxEnvelopeForObstacleAvoidance = true;

    [Min(0f)]
    [Tooltip("Extra clearance added around the cached hitbox envelope when steering around Environment geometry.")]
    public float obstacleEnvelopePadding = 0.05f;

    [Min(0f)]
    [Tooltip("Small safety gap kept between the enemy envelope and Environment when applying actual movement.")]
    public float obstacleSkinWidth = 0.06f;

    [Min(0.02f)]
    [Tooltip("Fallback SphereCast radius used only when no valid BoxCollider envelope can be built.")]
    public float obstacleProbeRadius = 0.34f;

    [Min(0.05f)]
    public float obstacleProbeDistance = 1.75f;

    [Range(0f, 2f)]
    public float obstacleAvoidanceStrength = 1.25f;

    [Header("Hover Visual")]
    [Min(0f)]
    public float hoverBobAmplitude = 0.075f;

    [Min(0.01f)]
    public float hoverBobFrequency = 1.15f;

    [Range(0f, 8f)]
    public float hoverTiltDegrees = 2.0f;

    [Header("Status Light")]
    public Renderer[] statusRenderers;

    [ColorUsage(true, true)]
    public Color dormantBlue = new Color(0.08f, 0.55f, 1.35f, 1f);

    [ColorUsage(true, true)]
    public Color suspiciousOrange = new Color(1.55f, 0.26f, 0.025f, 1f);

    [ColorUsage(true, true)]
    public Color detectedRed = new Color(1.85f, 0.025f, 0.018f, 1f);

    [Min(0.01f)]
    public float statusLightResponse = 10f;

    [Header("Alert Indicators")]
    public bool showStateIndicators = true;

    [Min(0.05f)]
    public float stateIndicatorHeightPadding = 0.45f;

    [Range(0.20f, 1.20f)]
    public float suspicionIndicatorDuration = 0.50f;

    [Range(0.20f, 1.20f)]
    public float detectedIndicatorDuration = 0.60f;

    [Range(0.55f, 1.75f)]
    public float stateIndicatorScale = 0.90f;

    [Header("Debug")]
    public bool drawVisionGizmos = true;

    [SerializeField]
    private FlyingBotState currentState = FlyingBotState.DormantPatrol;

    [SerializeField]
    private Transform currentPlayer;

    [SerializeField]
    private Vector3 currentMoveVelocity;

    [SerializeField]
    private Vector3 patrolTarget;

    [SerializeField]
    private Vector3 lastSeenPlayerPosition;

    private CombatEncounter _boundEncounter;
    private CombatEncounterMember _encounterMember;
    private PlayerHealth _playerHealth;
    private MaterialPropertyBlock _statusPropertyBlock;
    private GroundBotAlertIndicatorFX _activeIndicator;

    private Vector3 _visualBaseLocalPosition;
    private Quaternion _visualBaseLocalRotation;
    private Vector3 _scanBaseForward;
    private float _scanYaw;
    private int _scanDirection = 1;
    private float _scanPauseRemaining;
    private float _patrolWaitRemaining;
    private float _lastSeenTime = float.NegativeInfinity;
    private float _nextStrafeSwitchTime;
    private int _strafeDirection = 1;
    private bool _externalControlActive;
    private bool _rewindSuspended;
    private bool _hasEverActivated;
    private bool _damageEventBound;
    private Color _currentEmission;

    private CharacterController _playerCharacterController;
    private Transform _velocitySamplePlayer;
    private Vector3 _lastPlayerSamplePosition;
    private Vector3 _smoothedPlayerVelocity;
    private float _lastPlayerSampleTime;
    private bool _hasPlayerVelocitySample;

    private bool _interceptTelegraphActive;
    private float _interceptTelegraphRemaining;
    private bool _interceptSecondShotPending;
    private bool _interceptSecondShotLocked;
    private float _interceptSecondShotRemaining;
    private bool _interceptSecondGroupPending;
    private bool _interceptSecondGroupLocked;
    private float _interceptSecondGroupRemaining;
    private bool _interceptFourthShotPending;
    private float _interceptFourthShotRemaining;
    private float _nextInterceptAttackTime;
    private Vector3 _lockedInterceptCenter;
    private Vector3 _lockedInterceptTargetB;
    private Vector3 _lockedInterceptEscapeAxis;
    private bool _lockedInterceptHasDirectionalMovement;
    private Vector3 _lockedSecondGroupTargetA;
    private Vector3 _lockedSecondGroupTargetB;
    private FlyingBotInterceptTelegraphFX _interceptTelegraphFx;
    private static AudioClip _sharedInterceptLockSound;
    private static AudioClip _sharedInterceptReacquireSound;
    private static AudioClip _sharedInterceptProbeSound;
    private static AudioClip _sharedInterceptPunishmentSound;

    private enum DivePhase
    {
        None,
        Pullback,
        Charge,
        Locked,
        Diving,
        Recovery
    }

    private DivePhase _divePhase;
    private float _divePhaseRemaining;
    private float _nextDiveAttackTime;
    private Vector3 _divePullbackTarget;
    private Vector3 _diveLockedTarget;
    private Vector3 _diveEndPoint;
    private Vector3 _diveDirection;
    private bool _divePlayerHit;
    private FlyingBotDiveTelegraphFX _diveTelegraphFx;
    private FlyingBotDiveJuiceFX _diveJuiceFx;
    private float _hitFlashRemaining;
    private float _hitReactionRemaining;
    private Vector3 _hitReactionLocalDirection;
    private float _muzzleReactionRemaining;
    private Vector3 _muzzleReactionLocalDirection;
    private float _muzzleReactionDistance;
    private float _muzzleReactionRoll;

    private readonly struct ObstacleHitboxProxy
    {
        public readonly BoxCollider Collider;
        public readonly Vector3 RootLocalObjectPosition;
        public readonly Quaternion RootLocalObjectRotation;
        public readonly Vector3 RootLocalCenter;
        public readonly Quaternion RootLocalRotation;
        public readonly Vector3 HalfExtents;

        public ObstacleHitboxProxy(
            BoxCollider collider,
            Vector3 rootLocalObjectPosition,
            Quaternion rootLocalObjectRotation,
            Vector3 rootLocalCenter,
            Quaternion rootLocalRotation,
            Vector3 halfExtents
        )
        {
            Collider = collider;
            RootLocalObjectPosition = rootLocalObjectPosition;
            RootLocalObjectRotation = rootLocalObjectRotation;
            RootLocalCenter = rootLocalCenter;
            RootLocalRotation = rootLocalRotation;
            HalfExtents = halfExtents;
        }
    }

    private readonly List<ObstacleHitboxProxy> _obstacleHitboxes =
        new List<ObstacleHitboxProxy>(4);

    private readonly Collider[] _obstacleOverlapBuffer =
        new Collider[48];

    private Quaternion _requestedRotation;
    private bool _hasRequestedRotation;

    private const int PenetrationRecoveryIterations = 4;
    private const int SafeRotationSearchIterations = 6;
    private const int PatrolTargetSampleAttempts = 12;
    private const float MaximumPenetrationRecoveryPerStep = 0.75f;

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    public FlyingBotState CurrentState => currentState;
    public bool IsExternalControlActive => _externalControlActive;

    private void Awake()
    {
        ResolveReferences();
        PrepareInterceptAudioSource();
        ConfigureRigidbody();
        CacheObstacleEnvelope();
        ResetRequestedRotation();
        CacheVisualBaseline();
        CacheStatusRenderers();
        EnsureEncounterMembership();
        BindEncounterEvents();
        BindDamageEvent();
        BindPlayerHealth();
        InitializeStateFromEncounter();
    }

    private void OnEnable()
    {
        ResolveReferences();
        PrepareInterceptAudioSource();
        ConfigureRigidbody();
        CacheObstacleEnvelope();
        ResetRequestedRotation();
        EnsureEncounterMembership();
        BindEncounterEvents();
        BindDamageEvent();
        BindPlayerHealth();
        InitializeStateFromEncounter();
    }

    private void OnDisable()
    {
        UnbindEncounterEvents();
        UnbindDamageEvent();
        UnbindPlayerHealth();
        CancelActiveIndicator();
        CancelTwinInterceptAttack(false);
        CancelDiveAttack(false);
        ResetPlayerVelocityTracking();
        currentMoveVelocity = Vector3.zero;
        ResetCombatReactions();
        RestoreVisualBaseline();
    }

    private void Update()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            CancelTwinInterceptAttack(false);
            CancelDiveAttack(false);
            return;
        }

        UpdateCombatReactions(Time.deltaTime);
        BindPlayerHealth();

        if (_externalControlActive)
        {
            currentState = FlyingBotState.Stunned;
            UpdateStatusLight(Time.deltaTime);
            UpdateVisualHover(Time.time);
            return;
        }

        if (_rewindSuspended ||
            (_playerHealth != null && _playerHealth.IsRewinding))
        {
            EnterSuspendedHover();
            UpdateStatusLight(Time.deltaTime);
            UpdateVisualHover(Time.time);
            return;
        }

        RefreshPlayerReference(false);
        UpdatePlayerVelocityEstimate();
        EvaluateCombatGate();

        switch (currentState)
        {
            case FlyingBotState.DormantPatrol:
                UpdateDormantPatrol();
                break;

            case FlyingBotState.Suspicious:
                UpdateSuspicious();
                break;

            case FlyingBotState.EngageReposition:
                UpdateEngage();
                break;

            case FlyingBotState.SearchLastSeen:
                UpdateSearchLastSeen();
                break;

            case FlyingBotState.SuspendedHover:
            case FlyingBotState.Stunned:
                currentMoveVelocity = Vector3.MoveTowards(
                    currentMoveVelocity,
                    Vector3.zero,
                    acceleration * Time.deltaTime
                );
                break;
        }

        UpdateStatusLight(Time.deltaTime);
        UpdateVisualHover(Time.time);
    }

    private void FixedUpdate()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        if (_externalControlActive ||
            _rewindSuspended ||
            currentState == FlyingBotState.Stunned)
        {
            return;
        }

        if (_divePhase == DivePhase.Diving)
        {
            FixedUpdateDiveMovement();
            return;
        }

        Vector3 currentPosition = GetCurrentPosition();
        Quaternion currentRotation = GetCurrentRotation();

        if (TryResolveObstaclePenetration(
                currentPosition,
                currentRotation,
                out Vector3 recoveryOffset))
        {
            Vector3 recoveryTarget =
                currentPosition + recoveryOffset;

            MoveRootPose(
                recoveryTarget,
                currentRotation
            );

            currentMoveVelocity *= 0.20f;
            return;
        }

        Quaternion safeRotation =
            ResolveSafeRequestedRotation(
                currentPosition,
                currentRotation
            );

        Vector3 displacement =
            currentMoveVelocity * Time.fixedDeltaTime;

        Vector3 targetPosition =
            currentPosition + displacement;

        if (patrolVolume != null)
        {
            if (patrolVolume.ContainsWorldPoint(
                    currentPosition,
                    volumeBoundaryPadding))
            {
                targetPosition = patrolVolume.ClampWorldPoint(
                    targetPosition,
                    volumeBoundaryPadding
                );
            }
            else
            {
                // External knockback may legitimately push a Flying Bot outside
                // its authored volume. Never snap it back in one physics frame;
                // return toward the nearest valid point at the current movement
                // step speed instead.
                Vector3 recoveryPoint = patrolVolume.ClampWorldPoint(
                    currentPosition,
                    volumeBoundaryPadding
                );

                targetPosition = Vector3.MoveTowards(
                    currentPosition,
                    recoveryPoint,
                    displacement.magnitude
                );
            }
        }

        displacement =
            targetPosition - currentPosition;

        displacement = ConstrainMovementAgainstObstacles(
            displacement,
            currentPosition,
            safeRotation
        );

        if (displacement.sqrMagnitude <= 0.0000001f)
        {
            currentMoveVelocity = Vector3.MoveTowards(
                currentMoveVelocity,
                Vector3.zero,
                acceleration * Time.fixedDeltaTime
            );

            targetPosition = currentPosition;
        }
        else
        {
            targetPosition =
                currentPosition + displacement;
        }

        MoveRootPose(
            targetPosition,
            safeRotation
        );
    }


    private void EvaluateCombatGate()
    {
        bool gateOpen = IsCombatGateOpen();

        if (!gateOpen)
        {
            if (_hasEverActivated)
            {
                if (currentState != FlyingBotState.SuspendedHover)
                {
                    EnterSuspendedHover();
                }
            }
            else if (currentState != FlyingBotState.DormantPatrol)
            {
                EnterDormantPatrol();
            }

            return;
        }

        _hasEverActivated = true;

        if (currentState == FlyingBotState.DormantPatrol ||
            currentState == FlyingBotState.SuspendedHover)
        {
            EnterSuspicious(true);
        }
    }

    private void UpdateDormantPatrol()
    {
        if (!patrolBeforeFirstActivation)
        {
            currentMoveVelocity = Vector3.MoveTowards(
                currentMoveVelocity,
                Vector3.zero,
                acceleration * Time.deltaTime
            );
            return;
        }

        UpdatePatrolMovement(dormantPatrolSpeed);
        RotateTowardVelocity();
    }

    private void UpdateSuspicious()
    {
        if (!IsCombatGateOpen())
        {
            return;
        }

        if (CanVisuallyDetectPlayer())
        {
            AcquirePlayer();
            return;
        }

        UpdatePatrolMovement(suspiciousPatrolSpeed);
        UpdateScanningRotation();
    }

    private void UpdateEngage()
    {
        if (!IsCombatGateOpen())
        {
            CancelTwinInterceptAttack(true);
            CancelDiveAttack(true);
            return;
        }

        if (currentPlayer == null)
        {
            CancelTwinInterceptAttack(true);
            CancelDiveAttack(true);
            EnterSearchLastSeen();
            return;
        }

        bool canSeePlayer = CanRetainTrackedPlayer();

        if (canSeePlayer)
        {
            lastSeenPlayerPosition = GetPlayerAimPosition();
            _lastSeenTime = Time.time;
        }
        else if (!IsDiveCommitted())
        {
            // A pending predictive shot is never allowed to complete through a wall.
            CancelTwinInterceptAttack(true);
            CancelDiveAttack(true);

            // Do not keep following the player's live transform through a wall.
            // Once LOS is lost, immediately switch to the remembered position;
            // SearchLastSeen owns the memory window.
            EnterSearchLastSeen();
            return;
        }

        if (UpdateDiveAttack())
        {
            return;
        }

        if (!_interceptTelegraphActive &&
            Time.time >= _nextStrafeSwitchTime)
        {
            _strafeDirection *= -1;
            ScheduleNextStrafeSwitch();
        }

        Vector3 desiredPoint = ResolveCombatDesiredPoint();
        float moveSpeed = _interceptTelegraphActive
            ? combatMoveSpeed * Mathf.Clamp(
                interceptTelegraphMoveMultiplier,
                0.1f,
                1f
            )
            : combatMoveSpeed;

        MoveTowardWorldPoint(desiredPoint, moveSpeed);
        RotateTowardWorldPoint(GetPlayerAimPosition());

        UpdateTwinInterceptAttack();
    }

    private void UpdateSearchLastSeen()
    {
        if (!IsCombatGateOpen())
        {
            return;
        }

        if (CanVisuallyDetectPlayer())
        {
            AcquirePlayer();
            return;
        }

        if (Time.time - _lastSeenTime > targetMemoryDuration)
        {
            EnterSuspicious(false);
            return;
        }

        Vector3 searchPoint = lastSeenPlayerPosition;
        searchPoint.y = Mathf.Max(searchPoint.y, GetCurrentPosition().y - 0.5f);

        if (patrolVolume != null)
        {
            searchPoint = patrolVolume.ClampWorldPoint(
                searchPoint,
                volumeBoundaryPadding
            );
        }

        MoveTowardWorldPoint(searchPoint, searchMoveSpeed);
        RotateTowardWorldPoint(lastSeenPlayerPosition);
    }

    private void UpdatePatrolMovement(float moveSpeed)
    {
        if (patrolVolume == null || moveSpeed <= 0.001f)
        {
            currentMoveVelocity = Vector3.MoveTowards(
                currentMoveVelocity,
                Vector3.zero,
                acceleration * Time.deltaTime
            );
            return;
        }

        if (_patrolWaitRemaining > 0f)
        {
            _patrolWaitRemaining -= Time.deltaTime;
            currentMoveVelocity = Vector3.MoveTowards(
                currentMoveVelocity,
                Vector3.zero,
                acceleration * Time.deltaTime
            );
            return;
        }

        if (!patrolVolume.ContainsWorldPoint(
                patrolTarget,
                patrolWallPadding) ||
            Vector3.Distance(GetCurrentPosition(), patrolTarget) <=
            patrolArrivalRadius)
        {
            ChooseNewPatrolTarget();

            float minWait = Mathf.Min(patrolWaitRange.x, patrolWaitRange.y);
            float maxWait = Mathf.Max(patrolWaitRange.x, patrolWaitRange.y);
            _patrolWaitRemaining = Random.Range(
                Mathf.Max(0f, minWait),
                Mathf.Max(0f, maxWait)
            );
        }

        MoveTowardWorldPoint(patrolTarget, moveSpeed);
    }

    private void MoveTowardWorldPoint(Vector3 desiredPoint, float moveSpeed)
    {
        Vector3 currentPosition = GetCurrentPosition();

        if (patrolVolume != null)
        {
            desiredPoint = patrolVolume.ClampWorldPoint(
                desiredPoint,
                volumeBoundaryPadding
            );
        }

        Vector3 desiredDirection = desiredPoint - currentPosition;
        float distance = desiredDirection.magnitude;

        if (distance <= 0.02f || moveSpeed <= 0.001f)
        {
            currentMoveVelocity = Vector3.MoveTowards(
                currentMoveVelocity,
                Vector3.zero,
                acceleration * Time.deltaTime
            );
            return;
        }

        desiredDirection /= distance;
        desiredDirection = ApplyObstacleAvoidance(desiredDirection);

        float arrivalMultiplier = Mathf.Clamp01(distance / 1.0f);
        float targetSpeed = moveSpeed * Mathf.Lerp(0.28f, 1f, arrivalMultiplier);
        Vector3 targetVelocity = desiredDirection * targetSpeed;

        currentMoveVelocity = Vector3.MoveTowards(
            currentMoveVelocity,
            targetVelocity,
            acceleration * Time.deltaTime
        );
    }

    private Vector3 ApplyObstacleAvoidance(Vector3 desiredDirection)
    {
        if (obstacleMask.value == 0 ||
            desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return desiredDirection.normalized;
        }

        float probeDistance = Mathf.Max(
            obstacleProbeDistance,
            currentMoveVelocity.magnitude * 0.22f
        );

        if (!TryCastObstacleHitboxes(
                GetCurrentPosition(),
                GetCurrentRotation(),
                desiredDirection.normalized,
                probeDistance,
                obstacleEnvelopePadding,
                out RaycastHit hit))
        {
            return desiredDirection.normalized;
        }

        Vector3 slideDirection = Vector3.ProjectOnPlane(
            desiredDirection,
            hit.normal
        );

        if (slideDirection.sqrMagnitude <= 0.0001f)
        {
            slideDirection = Vector3.Cross(hit.normal, Vector3.up);

            if (slideDirection.sqrMagnitude <= 0.0001f)
            {
                slideDirection = Vector3.Cross(hit.normal, Vector3.right);
            }
        }

        float proximity01 = 1f - Mathf.Clamp01(
            hit.distance / Mathf.Max(0.001f, probeDistance)
        );

        float blendStrength = Mathf.Clamp01(
            obstacleAvoidanceStrength * Mathf.Lerp(0.55f, 1f, proximity01)
        );

        Vector3 blended = Vector3.Lerp(
            desiredDirection,
            slideDirection.normalized,
            blendStrength
        );

        return blended.sqrMagnitude > 0.0001f
            ? blended.normalized
            : desiredDirection.normalized;
    }

    private Vector3 ConstrainMovementAgainstObstacles(
        Vector3 displacement,
        Vector3 rootPosition,
        Quaternion rootRotation
    )
    {
        if (obstacleMask.value == 0 ||
            displacement.sqrMagnitude <= 0.0000001f)
        {
            return displacement;
        }

        float requestedDistance = displacement.magnitude;
        Vector3 direction = displacement / requestedDistance;
        float castDistance = requestedDistance + obstacleSkinWidth;

        if (!TryCastObstacleHitboxes(
                rootPosition,
                rootRotation,
                direction,
                castDistance,
                0f,
                out RaycastHit hit))
        {
            return displacement;
        }

        float allowedDistance = Mathf.Max(
            0f,
            hit.distance - obstacleSkinWidth
        );

        if (allowedDistance >= requestedDistance)
        {
            return displacement;
        }

        return direction * allowedDistance;
    }

    private bool TryCastObstacleHitboxes(
        Vector3 rootPosition,
        Quaternion rootRotation,
        Vector3 direction,
        float distance,
        float extraPadding,
        out RaycastHit closestHit
    )
    {
        closestHit = default;

        if (direction.sqrMagnitude <= 0.0001f ||
            distance <= 0f)
        {
            return false;
        }

        direction.Normalize();

        if (useHitboxEnvelopeForObstacleAvoidance &&
            _obstacleHitboxes.Count > 0)
        {
            bool foundHit = false;
            float closestDistance = float.PositiveInfinity;

            for (int index = 0;
                 index < _obstacleHitboxes.Count;
                 index++)
            {
                ObstacleHitboxProxy proxy =
                    _obstacleHitboxes[index];

                BuildWorldBox(
                    proxy,
                    rootPosition,
                    rootRotation,
                    Mathf.Max(0f, extraPadding),
                    out Vector3 center,
                    out Vector3 halfExtents,
                    out Quaternion boxRotation
                );

                if (!Physics.BoxCast(
                        center,
                        halfExtents,
                        direction,
                        out RaycastHit candidateHit,
                        boxRotation,
                        distance,
                        obstacleMask,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (candidateHit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = candidateHit.distance;
                closestHit = candidateHit;
                foundHit = true;
            }

            return foundHit;
        }

        Vector3 fallbackOrigin = aimPoint != null
            ? aimPoint.position
            : rootPosition;

        return Physics.SphereCast(
            fallbackOrigin,
            obstacleProbeRadius,
            direction,
            out closestHit,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private bool WouldPoseOverlapEnvironment(
        Vector3 rootPosition,
        Quaternion rootRotation,
        float extraPadding
    )
    {
        if (obstacleMask.value == 0 ||
            _obstacleHitboxes.Count == 0)
        {
            return false;
        }

        for (int index = 0;
             index < _obstacleHitboxes.Count;
             index++)
        {
            BuildWorldBox(
                _obstacleHitboxes[index],
                rootPosition,
                rootRotation,
                Mathf.Max(0f, extraPadding),
                out Vector3 center,
                out Vector3 halfExtents,
                out Quaternion boxRotation
            );

            if (Physics.CheckBox(
                    center,
                    halfExtents,
                    boxRotation,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveObstaclePenetration(
        Vector3 rootPosition,
        Quaternion rootRotation,
        out Vector3 recoveryOffset
    )
    {
        recoveryOffset = Vector3.zero;

        if (obstacleMask.value == 0 ||
            _obstacleHitboxes.Count == 0)
        {
            return false;
        }

        for (int iteration = 0;
             iteration < PenetrationRecoveryIterations;
             iteration++)
        {
            bool foundPenetration = false;
            float deepestDistance = 0f;
            Vector3 deepestDirection = Vector3.zero;

            for (int hitboxIndex = 0;
                 hitboxIndex < _obstacleHitboxes.Count;
                 hitboxIndex++)
            {
                ObstacleHitboxProxy proxy =
                    _obstacleHitboxes[hitboxIndex];

                BuildWorldBox(
                    proxy,
                    rootPosition + recoveryOffset,
                    rootRotation,
                    0.001f,
                    out Vector3 boxCenter,
                    out Vector3 boxHalfExtents,
                    out Quaternion boxRotation
                );

                int overlapCount = Physics.OverlapBoxNonAlloc(
                    boxCenter,
                    boxHalfExtents,
                    _obstacleOverlapBuffer,
                    boxRotation,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore
                );

                if (overlapCount <= 0)
                {
                    continue;
                }

                BuildWorldColliderObjectPose(
                    proxy,
                    rootPosition + recoveryOffset,
                    rootRotation,
                    out Vector3 colliderPosition,
                    out Quaternion colliderRotation
                );

                for (int overlapIndex = 0;
                     overlapIndex < overlapCount;
                     overlapIndex++)
                {
                    Collider other =
                        _obstacleOverlapBuffer[overlapIndex];

                    _obstacleOverlapBuffer[overlapIndex] = null;

                    if (other == null ||
                        other == proxy.Collider)
                    {
                        continue;
                    }

                    if (!Physics.ComputePenetration(
                            proxy.Collider,
                            colliderPosition,
                            colliderRotation,
                            other,
                            other.transform.position,
                            other.transform.rotation,
                            out Vector3 separationDirection,
                            out float separationDistance))
                    {
                        continue;
                    }

                    if (separationDistance <= deepestDistance)
                    {
                        continue;
                    }

                    deepestDistance = separationDistance;
                    deepestDirection = separationDirection;
                    foundPenetration = true;
                }
            }

            if (!foundPenetration ||
                deepestDirection.sqrMagnitude <= 0.0001f)
            {
                break;
            }

            recoveryOffset +=
                deepestDirection.normalized *
                (deepestDistance + obstacleSkinWidth);

            if (recoveryOffset.magnitude >
                MaximumPenetrationRecoveryPerStep)
            {
                recoveryOffset =
                    recoveryOffset.normalized *
                    MaximumPenetrationRecoveryPerStep;
                break;
            }
        }

        return recoveryOffset.sqrMagnitude > 0.0000001f;
    }

    private Quaternion ResolveSafeRequestedRotation(
        Vector3 rootPosition,
        Quaternion currentRotation
    )
    {
        if (!_hasRequestedRotation ||
            turnSpeed <= 0.001f)
        {
            return currentRotation;
        }

        Quaternion candidateRotation =
            Quaternion.RotateTowards(
                currentRotation,
                _requestedRotation,
                turnSpeed * Time.fixedDeltaTime
            );

        float collisionPadding =
            Mathf.Min(
                Mathf.Max(0f, obstacleEnvelopePadding),
                0.02f
            );

        if (!WouldPoseOverlapEnvironment(
                rootPosition,
                candidateRotation,
                collisionPadding))
        {
            return candidateRotation;
        }

        float safeFraction = 0f;
        float blockedFraction = 1f;

        for (int iteration = 0;
             iteration < SafeRotationSearchIterations;
             iteration++)
        {
            float testFraction =
                (safeFraction + blockedFraction) * 0.5f;

            Quaternion testRotation =
                Quaternion.Slerp(
                    currentRotation,
                    candidateRotation,
                    testFraction
                );

            if (WouldPoseOverlapEnvironment(
                    rootPosition,
                    testRotation,
                    collisionPadding))
            {
                blockedFraction = testFraction;
            }
            else
            {
                safeFraction = testFraction;
            }
        }

        if (safeFraction <= 0.001f)
        {
            return currentRotation;
        }

        return Quaternion.Slerp(
            currentRotation,
            candidateRotation,
            safeFraction
        );
    }

    private void CacheObstacleEnvelope()
    {
        _obstacleHitboxes.Clear();

        BoxCollider[] boxColliders =
            GetComponentsInChildren<BoxCollider>(true);

        Vector3 rootWorldScale =
            AbsVector(transform.lossyScale);

        rootWorldScale.x =
            Mathf.Max(0.0001f, rootWorldScale.x);
        rootWorldScale.y =
            Mathf.Max(0.0001f, rootWorldScale.y);
        rootWorldScale.z =
            Mathf.Max(0.0001f, rootWorldScale.z);

        foreach (BoxCollider box in boxColliders)
        {
            if (box == null ||
                !box.enabled ||
                !box.gameObject.activeInHierarchy ||
                box.isTrigger ||
                box.transform == patrolVolume?.transform)
            {
                continue;
            }

            Vector3 worldCenter =
                box.transform.TransformPoint(box.center);

            Vector3 rootLocalCenter =
                transform.InverseTransformPoint(worldCenter);

            Vector3 rootLocalObjectPosition =
                transform.InverseTransformPoint(
                    box.transform.position
                );

            Quaternion rootLocalObjectRotation =
                Quaternion.Inverse(transform.rotation) *
                box.transform.rotation;

            Quaternion rootLocalRotation =
                rootLocalObjectRotation;

            Vector3 boxWorldScale =
                AbsVector(box.transform.lossyScale);

            Vector3 relativeScale = new Vector3(
                boxWorldScale.x / rootWorldScale.x,
                boxWorldScale.y / rootWorldScale.y,
                boxWorldScale.z / rootWorldScale.z
            );

            Vector3 halfExtents = Vector3.Scale(
                box.size * 0.5f,
                relativeScale
            );

            halfExtents.x = Mathf.Max(0.01f, halfExtents.x);
            halfExtents.y = Mathf.Max(0.01f, halfExtents.y);
            halfExtents.z = Mathf.Max(0.01f, halfExtents.z);

            _obstacleHitboxes.Add(
                new ObstacleHitboxProxy(
                    box,
                    rootLocalObjectPosition,
                    rootLocalObjectRotation,
                    rootLocalCenter,
                    rootLocalRotation,
                    halfExtents
                )
            );
        }
    }

    private void BuildWorldBox(
        ObstacleHitboxProxy proxy,
        Vector3 rootPosition,
        Quaternion rootRotation,
        float extraPadding,
        out Vector3 center,
        out Vector3 halfExtents,
        out Quaternion boxRotation
    )
    {
        Vector3 rootScale =
            AbsVector(transform.lossyScale);

        center =
            rootPosition +
            rootRotation *
            Vector3.Scale(
                proxy.RootLocalCenter,
                rootScale
            );

        boxRotation =
            rootRotation *
            proxy.RootLocalRotation;

        halfExtents = Vector3.Scale(
            proxy.HalfExtents,
            rootScale
        );

        if (extraPadding > 0f)
        {
            halfExtents +=
                Vector3.one * extraPadding;
        }

        halfExtents.x = Mathf.Max(0.01f, halfExtents.x);
        halfExtents.y = Mathf.Max(0.01f, halfExtents.y);
        halfExtents.z = Mathf.Max(0.01f, halfExtents.z);
    }

    private void BuildWorldColliderObjectPose(
        ObstacleHitboxProxy proxy,
        Vector3 rootPosition,
        Quaternion rootRotation,
        out Vector3 colliderPosition,
        out Quaternion colliderRotation
    )
    {
        Vector3 rootScale =
            AbsVector(transform.lossyScale);

        colliderPosition =
            rootPosition +
            rootRotation *
            Vector3.Scale(
                proxy.RootLocalObjectPosition,
                rootScale
            );

        colliderRotation =
            rootRotation *
            proxy.RootLocalObjectRotation;
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }


    private Vector3 ResolveCombatDesiredPoint()
    {
        Vector3 playerPosition = currentPlayer != null
            ? currentPlayer.position
            : lastSeenPlayerPosition;

        Vector3 currentPosition = GetCurrentPosition();
        Vector3 radial = currentPosition - playerPosition;
        radial.y = 0f;

        if (radial.sqrMagnitude <= 0.001f)
        {
            radial = -transform.forward;
            radial.y = 0f;
        }

        radial.Normalize();

        Vector3 tangent =
            Vector3.Cross(Vector3.up, radial) * _strafeDirection;

        Vector3 desiredPoint =
            playerPosition +
            radial * preferredCombatDistance +
            tangent * strafeOffset +
            Vector3.up * preferredHeightAbovePlayer;

        if (patrolVolume != null)
        {
            desiredPoint = patrolVolume.ClampWorldPoint(
                desiredPoint,
                volumeBoundaryPadding
            );
        }

        return desiredPoint;
    }

    private bool CanVisuallyDetectPlayer()
    {
        return CanDetectPlayer(
            visionRange,
            true
        );
    }

    private bool CanRetainTrackedPlayer()
    {
        return CanDetectPlayer(
            visionRange * 1.20f,
            false
        );
    }

    private bool CanDetectPlayer(
        float maximumRange,
        bool requireVisionCone
    )
    {
        if (!IsCombatGateOpen() || currentPlayer == null)
        {
            return false;
        }

        Vector3 origin = aimPoint != null
            ? aimPoint.position
            : GetCurrentPosition();

        Vector3 target = GetPlayerAimPosition();
        Vector3 toPlayer = target - origin;
        float distance = toPlayer.magnitude;

        if (distance <= 0.001f || distance > maximumRange)
        {
            return false;
        }

        Vector3 direction = toPlayer / distance;

        if (requireVisionCone &&
            distance > nearbyAwarenessRange &&
            Vector3.Angle(transform.forward, direction) >
                visionAngle * 0.5f)
        {
            return false;
        }

        if (obstacleMask.value == 0)
        {
            return true;
        }

        return !Physics.SphereCast(
            origin,
            Mathf.Max(0f, lineOfSightRadius),
            direction,
            out _,
            Mathf.Max(0f, distance - 0.05f),
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void AcquirePlayer()
    {
        if (currentPlayer == null)
        {
            return;
        }

        bool wasLocked =
            currentState == FlyingBotState.EngageReposition;

        currentState = FlyingBotState.EngageReposition;
        lastSeenPlayerPosition = GetPlayerAimPosition();
        _lastSeenTime = Time.time;
        ScheduleNextStrafeSwitch();

        if (!wasLocked)
        {
            _nextInterceptAttackTime = Mathf.Max(
                _nextInterceptAttackTime,
                Time.time + Mathf.Max(0f, interceptInitialDelay)
            );
            _nextDiveAttackTime = Mathf.Max(
                _nextDiveAttackTime,
                Time.time + Mathf.Max(0f, diveInitialDelay)
            );

            ResetPlayerVelocityTracking();
            ShowIndicator(GroundBotAlertIndicatorFX.IndicatorKind.Detected);
        }
    }

    private void EnterDormantPatrol()
    {
        CancelActiveIndicator();
        CancelTwinInterceptAttack(false);
        CancelDiveAttack(false);
        ResetPlayerVelocityTracking();
        currentState = FlyingBotState.DormantPatrol;
        currentPlayer = null;
        currentMoveVelocity = Vector3.zero;
        _patrolWaitRemaining = 0f;
        ChooseNewPatrolTarget();
    }

    private void EnterSuspicious(bool showIndicator)
    {
        CancelActiveIndicator();
        CancelTwinInterceptAttack(true);
        CancelDiveAttack(true);
        currentState = FlyingBotState.Suspicious;
        RefreshPlayerReference(true);
        currentMoveVelocity *= 0.45f;
        _scanBaseForward = transform.forward;
        _scanBaseForward.y = 0f;

        if (_scanBaseForward.sqrMagnitude <= 0.001f)
        {
            _scanBaseForward = Vector3.forward;
        }

        _scanBaseForward.Normalize();
        _scanYaw = 0f;
        _scanDirection = Random.value < 0.5f ? -1 : 1;
        _scanPauseRemaining = 0f;

        if (showIndicator)
        {
            ShowIndicator(GroundBotAlertIndicatorFX.IndicatorKind.Suspicion);
        }
    }

    private void EnterSearchLastSeen()
    {
        CancelActiveIndicator();
        CancelTwinInterceptAttack(true);
        CancelDiveAttack(true);
        currentState = FlyingBotState.SearchLastSeen;
        currentMoveVelocity *= 0.7f;

        if (_lastSeenTime == float.NegativeInfinity)
        {
            _lastSeenTime = Time.time;
            lastSeenPlayerPosition = GetCurrentPosition();
        }
    }

    private void EnterSuspendedHover()
    {
        CancelActiveIndicator();
        CancelTwinInterceptAttack(false);
        CancelDiveAttack(false);
        ResetPlayerVelocityTracking();
        currentState = FlyingBotState.SuspendedHover;
        currentPlayer = null;
        currentMoveVelocity = Vector3.zero;
        _lastSeenTime = float.NegativeInfinity;
        ResetRequestedRotation();
    }

    private void UpdateScanningRotation()
    {
        if (_scanPauseRemaining > 0f)
        {
            _scanPauseRemaining -= Time.deltaTime;
            return;
        }

        float nextYaw = _scanYaw +
            _scanDirection * scanTurnSpeed * Time.deltaTime;

        if (Mathf.Abs(nextYaw) >= scanHalfAngle)
        {
            _scanYaw = Mathf.Sign(nextYaw) * scanHalfAngle;
            _scanDirection *= -1;
            _scanPauseRemaining = scanEndPause;
        }
        else
        {
            _scanYaw = nextYaw;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(
            Quaternion.AngleAxis(_scanYaw, Vector3.up) * _scanBaseForward,
            Vector3.up
        );

        RequestRotation(desiredRotation);
    }

    private void RotateTowardVelocity()
    {
        Vector3 flatVelocity = currentMoveVelocity;
        flatVelocity.y = 0f;

        if (flatVelocity.sqrMagnitude <= 0.02f)
        {
            return;
        }

        RotateTowardWorldPoint(
            GetCurrentPosition() + flatVelocity
        );
    }

    private void RotateTowardWorldPoint(Vector3 worldPoint)
    {
        Vector3 toTarget = worldPoint - GetCurrentPosition();
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(
            toTarget.normalized,
            Vector3.up
        );

        RequestRotation(desiredRotation);
    }

    private void ScheduleNextStrafeSwitch()
    {
        float minInterval = Mathf.Min(
            strafeSwitchInterval.x,
            strafeSwitchInterval.y
        );
        float maxInterval = Mathf.Max(
            strafeSwitchInterval.x,
            strafeSwitchInterval.y
        );

        _nextStrafeSwitchTime = Time.time + Random.Range(
            Mathf.Max(0.25f, minInterval),
            Mathf.Max(0.25f, maxInterval)
        );
    }

    private void ChooseNewPatrolTarget()
    {
        Vector3 currentPosition = GetCurrentPosition();

        if (patrolVolume == null)
        {
            patrolTarget = currentPosition;
            return;
        }

        Quaternion currentRotation = GetCurrentRotation();

        for (int attempt = 0;
             attempt < PatrolTargetSampleAttempts;
             attempt++)
        {
            Vector3 candidate =
                patrolVolume.GetRandomWorldPoint(
                    patrolWallPadding
                );

            Vector3 toCandidate =
                candidate - currentPosition;

            float distance =
                toCandidate.magnitude;

            if (distance <= patrolArrivalRadius)
            {
                continue;
            }

            if (WouldPoseOverlapEnvironment(
                    candidate,
                    currentRotation,
                    obstacleEnvelopePadding))
            {
                continue;
            }

            if (TryCastObstacleHitboxes(
                    currentPosition,
                    currentRotation,
                    toCandidate / distance,
                    distance,
                    obstacleEnvelopePadding,
                    out RaycastHit hit) &&
                hit.distance <
                distance - obstacleSkinWidth)
            {
                continue;
            }

            patrolTarget = candidate;
            return;
        }

        // If the authored volume is heavily divided by walls, do not choose a
        // point through solid geometry. Pause locally and sample again later.
        patrolTarget = currentPosition;
    }

    private bool IsCombatGateOpen()
    {
        if (_rewindSuspended ||
            (_playerHealth != null && _playerHealth.IsRewinding))
        {
            return false;
        }

        if (combatEncounter != null)
        {
            return combatEncounter.AllowsCombat;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        if (enemyRigidbody == null)
        {
            enemyRigidbody = GetComponent<Rigidbody>();
        }

        if (visualRoot == null)
        {
            Transform candidate = transform.Find("VisualRoot");
            visualRoot = candidate != null ? candidate : transform;
        }

        if (aimPoint == null)
        {
            Transform candidate = transform.Find("AimPoint");
            aimPoint = candidate != null ? candidate : transform;
        }
    }

    private void ConfigureRigidbody()
    {
        if (enemyRigidbody == null)
        {
            return;
        }

        enemyRigidbody.useGravity = false;
        enemyRigidbody.isKinematic = true;
        enemyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        enemyRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
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
            _encounterMember = gameObject.AddComponent<CombatEncounterMember>();
        }

        _encounterMember.Configure(
            combatEncounter,
            enemyTarget,
            requiredForEncounterClear
        );
    }

    private void BindEncounterEvents()
    {
        if (_boundEncounter == combatEncounter)
        {
            return;
        }

        UnbindEncounterEvents();
        _boundEncounter = combatEncounter;

        if (_boundEncounter == null)
        {
            return;
        }

        _boundEncounter.EncounterActivated += HandleEncounterActivated;
        _boundEncounter.EncounterSuspended += HandleEncounterSuspended;
        _boundEncounter.EncounterCleared += HandleEncounterCleared;
        _boundEncounter.EncounterReset += HandleEncounterReset;
    }

    private void UnbindEncounterEvents()
    {
        if (_boundEncounter == null)
        {
            return;
        }

        _boundEncounter.EncounterActivated -= HandleEncounterActivated;
        _boundEncounter.EncounterSuspended -= HandleEncounterSuspended;
        _boundEncounter.EncounterCleared -= HandleEncounterCleared;
        _boundEncounter.EncounterReset -= HandleEncounterReset;
        _boundEncounter = null;
    }

    private void HandleEncounterActivated(CombatEncounter source)
    {
        if (source != combatEncounter ||
            (enemyTarget != null && enemyTarget.IsDead))
        {
            return;
        }

        _hasEverActivated = true;
        RefreshPlayerReference(true);

        if (_externalControlActive || _rewindSuspended)
        {
            return;
        }

        EnterSuspicious(true);
    }

    private void HandleEncounterSuspended(CombatEncounter source)
    {
        if (source == combatEncounter)
        {
            EnterSuspendedHover();
        }
    }

    private void HandleEncounterCleared(CombatEncounter source)
    {
        if (source == combatEncounter)
        {
            EnterSuspendedHover();
        }
    }

    private void HandleEncounterReset(CombatEncounter source)
    {
        if (source != combatEncounter)
        {
            return;
        }

        _hasEverActivated = false;
        EnterDormantPatrol();
    }

    private void InitializeStateFromEncounter()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        if (_externalControlActive)
        {
            currentState = FlyingBotState.Stunned;
            return;
        }

        if (combatEncounter == null)
        {
            EnterDormantPatrol();
            return;
        }

        if (combatEncounter.AllowsCombat)
        {
            _hasEverActivated = true;
            RefreshPlayerReference(true);
            EnterSuspicious(false);
        }
        else if (combatEncounter.State == CombatEncounterState.Suspended ||
                 combatEncounter.State == CombatEncounterState.Cleared)
        {
            _hasEverActivated = true;
            EnterSuspendedHover();
        }
        else
        {
            EnterDormantPatrol();
        }
    }

    private void RefreshPlayerReference(bool force)
    {
        Transform resolvedPlayer = null;

        if (combatEncounter != null)
        {
            if (!combatEncounter.AllowsCombat)
            {
                AssignPlayerReference(null);
                return;
            }

            if (combatEncounter.Player != null)
            {
                resolvedPlayer = combatEncounter.Player;
            }
        }

        if (resolvedPlayer == null)
        {
            if (!force && currentPlayer != null)
            {
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            resolvedPlayer = playerObject != null
                ? playerObject.transform
                : null;
        }

        AssignPlayerReference(resolvedPlayer);
    }

    private void AssignPlayerReference(Transform resolvedPlayer)
    {
        if (resolvedPlayer == currentPlayer)
        {
            if (_playerCharacterController == null &&
                currentPlayer != null)
            {
                _playerCharacterController =
                    currentPlayer.GetComponentInParent<CharacterController>();

                if (_playerCharacterController == null)
                {
                    _playerCharacterController =
                        currentPlayer.GetComponentInChildren<CharacterController>();
                }
            }

            return;
        }

        currentPlayer = resolvedPlayer;
        _playerCharacterController = null;
        ResetPlayerVelocityTracking();

        if (currentPlayer == null)
        {
            return;
        }

        _playerCharacterController =
            currentPlayer.GetComponentInParent<CharacterController>();

        if (_playerCharacterController == null)
        {
            _playerCharacterController =
                currentPlayer.GetComponentInChildren<CharacterController>();
        }
    }

    private void BindDamageEvent()
    {
        if (_damageEventBound || enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged += HandleEnemyDamaged;
        _damageEventBound = true;
    }

    private void UnbindDamageEvent()
    {
        if (!_damageEventBound || enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged -= HandleEnemyDamaged;
        _damageEventBound = false;
    }

    private void HandleEnemyDamaged(
        float actualDamage,
        float remainingHealth,
        Vector3 hitPoint
    )
    {
        if (actualDamage <= 0f ||
            _externalControlActive ||
            !IsCombatGateOpen())
        {
            return;
        }

        if (remainingHealth > 0f)
        {
            PlayNonFatalHitFeedback(actualDamage, hitPoint);
        }

        RefreshPlayerReference(true);

        if (CanVisuallyDetectPlayer())
        {
            AcquirePlayer();
        }
        else if (currentState != FlyingBotState.Suspicious)
        {
            EnterSuspicious(false);
        }
    }

    private void PlayNonFatalHitFeedback(
        float actualDamage,
        Vector3 hitPoint
    )
    {
        Vector3 outwardNormal = hitPoint - GetCurrentPosition();

        if (outwardNormal.sqrMagnitude <= 0.0001f)
        {
            outwardNormal = -transform.forward;
        }
        else
        {
            outwardNormal.Normalize();
        }

        _hitFlashRemaining = 0.10f;
        _hitReactionRemaining = 0.16f;
        _hitReactionLocalDirection = ToVisualParentDirection(
            -outwardNormal
        );

        float strength = Mathf.Lerp(
            0.82f,
            1.28f,
            Mathf.Clamp01(actualDamage / 120f)
        );
        FlyingBotCombatJuiceFX.SpawnHit(
            hitPoint,
            outwardNormal,
            Color.Lerp(Color.white, detectedRed, 0.62f),
            strength
        );
    }

    private void TriggerMuzzleReaction(
        Vector3 shotDirection,
        bool strong
    )
    {
        Vector3 safeDirection = shotDirection.sqrMagnitude > 0.0001f
            ? shotDirection.normalized
            : transform.forward;

        _muzzleReactionRemaining = 0.11f;
        _muzzleReactionLocalDirection = ToVisualParentDirection(
            -safeDirection
        );
        _muzzleReactionDistance = strong ? 0.105f : 0.07f;
        _muzzleReactionRoll = strong ? 3f : -2f;
    }

    private Vector3 ToVisualParentDirection(Vector3 worldDirection)
    {
        Transform parent = visualRoot != null
            ? visualRoot.parent
            : null;

        return parent != null
            ? parent.InverseTransformDirection(worldDirection).normalized
            : worldDirection.normalized;
    }

    private void UpdateCombatReactions(float deltaTime)
    {
        _hitFlashRemaining = Mathf.Max(
            0f,
            _hitFlashRemaining - deltaTime
        );
        _hitReactionRemaining = Mathf.Max(
            0f,
            _hitReactionRemaining - deltaTime
        );
        _muzzleReactionRemaining = Mathf.Max(
            0f,
            _muzzleReactionRemaining - deltaTime
        );
    }

    private void ResetCombatReactions()
    {
        _hitFlashRemaining = 0f;
        _hitReactionRemaining = 0f;
        _hitReactionLocalDirection = Vector3.zero;
        _muzzleReactionRemaining = 0f;
        _muzzleReactionLocalDirection = Vector3.zero;
        _muzzleReactionDistance = 0f;
        _muzzleReactionRoll = 0f;
    }

    private static float EvaluateReaction(
        float remaining,
        float duration
    )
    {
        if (remaining <= 0f)
        {
            return 0f;
        }

        float progress = 1f - Mathf.Clamp01(
            remaining / Mathf.Max(0.0001f, duration)
        );
        return Mathf.Sin(progress * Mathf.PI);
    }

    private void BindPlayerHealth()
    {
        PlayerHealth resolved = PlayerHealth.Instance;

        if (resolved == _playerHealth)
        {
            return;
        }

        UnbindPlayerHealth();
        _playerHealth = resolved;

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
    }

    private void HandleRewindStarted()
    {
        _rewindSuspended = true;
        EnterSuspendedHover();
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
            EnterSuspicious(false);
        }
        else if (_hasEverActivated)
        {
            EnterSuspendedHover();
        }
        else
        {
            EnterDormantPatrol();
        }
    }

    public void BeginExternalControl()
    {
        if (_externalControlActive)
        {
            return;
        }

        _externalControlActive = true;
        CancelActiveIndicator();
        CancelTwinInterceptAttack(false);
        CancelDiveAttack(false);
        currentMoveVelocity = Vector3.zero;
        currentState = FlyingBotState.Stunned;
        ResetRequestedRotation();
    }

    public void EndExternalControl()
    {
        if (!_externalControlActive)
        {
            return;
        }

        _externalControlActive = false;

        if (_rewindSuspended)
        {
            EnterSuspendedHover();
            return;
        }

        RefreshPlayerReference(true);

        if (IsCombatGateOpen())
        {
            if (CanVisuallyDetectPlayer())
            {
                AcquirePlayer();
            }
            else
            {
                EnterSuspicious(false);
            }
        }
        else if (_hasEverActivated)
        {
            EnterSuspendedHover();
        }
        else
        {
            EnterDormantPatrol();
        }
    }

    private void CacheVisualBaseline()
    {
        if (visualRoot == null)
        {
            return;
        }

        _visualBaseLocalPosition = visualRoot.localPosition;
        _visualBaseLocalRotation = visualRoot.localRotation;
    }

    private void RestoreVisualBaseline()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = _visualBaseLocalPosition;
        visualRoot.localRotation = _visualBaseLocalRotation;
    }

    private void UpdateVisualHover(float time)
    {
        if (visualRoot == null)
        {
            return;
        }

        float phase = time * hoverBobFrequency * Mathf.PI * 2f;
        float verticalOffset = Mathf.Sin(phase) * hoverBobAmplitude;

        float speed01 = Mathf.Clamp01(
            currentMoveVelocity.magnitude /
            Mathf.Max(0.1f, combatMoveSpeed)
        );

        float tilt = Mathf.Sin(phase * 0.52f) *
            hoverTiltDegrees *
            Mathf.Lerp(0.35f, 1f, speed01);
        float diveWindup01 = 0f;

        if (_divePhase == DivePhase.Pullback)
        {
            diveWindup01 = 1f - Mathf.Clamp01(
                _divePhaseRemaining /
                Mathf.Max(0.05f, divePullbackDuration)
            );
        }
        else if (_divePhase == DivePhase.Charge ||
                 _divePhase == DivePhase.Locked)
        {
            diveWindup01 = 1f;
        }

        float hitReaction01 = EvaluateReaction(
            _hitReactionRemaining,
            0.16f
        );
        float muzzleReaction01 = EvaluateReaction(
            _muzzleReactionRemaining,
            0.11f
        );
        Vector3 combatOffset =
            _hitReactionLocalDirection * (0.11f * hitReaction01) +
            _muzzleReactionLocalDirection *
                (_muzzleReactionDistance * muzzleReaction01);

        visualRoot.localPosition =
            _visualBaseLocalPosition +
            Vector3.up * verticalOffset +
            combatOffset;

        visualRoot.localRotation =
            _visualBaseLocalRotation *
            Quaternion.Euler(
                tilt - diveWindupTiltDegrees * diveWindup01,
                0f,
                -tilt * 0.65f
            ) *
            Quaternion.Euler(
                -_hitReactionLocalDirection.y * 7f * hitReaction01 -
                    5.5f * muzzleReaction01,
                _hitReactionLocalDirection.x * 6f * hitReaction01,
                -_hitReactionLocalDirection.x * 7f * hitReaction01 +
                    _muzzleReactionRoll * muzzleReaction01
            );
    }

    private void CacheStatusRenderers()
    {
        if (statusRenderers != null && statusRenderers.Length > 0)
        {
            return;
        }

        Transform root = visualRoot != null ? visualRoot : transform;
        Renderer[] candidates = root.GetComponentsInChildren<Renderer>(true);

        int validCount = 0;

        foreach (Renderer candidate in candidates)
        {
            if (RendererSupportsEmission(candidate))
            {
                validCount++;
            }
        }

        statusRenderers = new Renderer[validCount];
        int writeIndex = 0;

        foreach (Renderer candidate in candidates)
        {
            if (!RendererSupportsEmission(candidate))
            {
                continue;
            }

            statusRenderers[writeIndex] = candidate;
            writeIndex++;
        }

        _statusPropertyBlock = new MaterialPropertyBlock();
    }

    private static bool RendererSupportsEmission(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Material[] materials = renderer.sharedMaterials;

        foreach (Material material in materials)
        {
            if (material != null && material.HasProperty(EmissionColorId))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateStatusLight(float deltaTime)
    {
        if (statusRenderers == null || statusRenderers.Length == 0)
        {
            return;
        }

        if (_statusPropertyBlock == null)
        {
            _statusPropertyBlock = new MaterialPropertyBlock();
        }

        Color targetColor = ResolveStatusColor();
        float response = _hitFlashRemaining > 0f
            ? 1f
            : 1f - Mathf.Exp(
                -Mathf.Max(0.01f, statusLightResponse) * deltaTime
            );

        _currentEmission = Color.Lerp(
            _currentEmission,
            targetColor,
            response
        );

        foreach (Renderer targetRenderer in statusRenderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(_statusPropertyBlock);
            _statusPropertyBlock.SetColor(
                EmissionColorId,
                _currentEmission
            );
            targetRenderer.SetPropertyBlock(_statusPropertyBlock);
        }
    }

    private Color ResolveStatusColor()
    {
        if (_hitFlashRemaining > 0f)
        {
            float flash01 = Mathf.Clamp01(
                _hitFlashRemaining / 0.10f
            );
            return Color.Lerp(
                detectedRed * 1.25f,
                Color.white * 2.25f,
                flash01
            );
        }

        if (_externalControlActive)
        {
            return Color.white * 1.35f;
        }

        if (_divePhase == DivePhase.Pullback ||
            _divePhase == DivePhase.Charge ||
            _divePhase == DivePhase.Locked ||
            _divePhase == DivePhase.Diving)
        {
            return diveChargeOrange;
        }

        switch (currentState)
        {
            case FlyingBotState.Suspicious:
            case FlyingBotState.SearchLastSeen:
                return suspiciousOrange;

            case FlyingBotState.EngageReposition:
                return detectedRed;

            default:
                return dormantBlue;
        }
    }

    private void ShowIndicator(
        GroundBotAlertIndicatorFX.IndicatorKind kind
    )
    {
        if (!showStateIndicators)
        {
            return;
        }

        CancelActiveIndicator();

        _activeIndicator = GroundBotAlertIndicatorFX.Spawn(
            transform,
            enemyTarget,
            kind,
            kind == GroundBotAlertIndicatorFX.IndicatorKind.Suspicion
                ? suspicionIndicatorDuration
                : detectedIndicatorDuration,
            stateIndicatorHeightPadding,
            stateIndicatorScale
        );
    }

    private void CancelActiveIndicator()
    {
        if (_activeIndicator == null)
        {
            return;
        }

        _activeIndicator.CancelImmediate();
        _activeIndicator = null;
    }

    private void RequestRotation(Quaternion desiredRotation)
    {
        _requestedRotation = desiredRotation;
        _hasRequestedRotation = true;
    }

    private void ResetRequestedRotation()
    {
        _requestedRotation = GetCurrentRotation();
        _hasRequestedRotation = false;
    }

    private void MoveRootPose(
        Vector3 position,
        Quaternion rotation
    )
    {
        if (enemyRigidbody != null &&
            enemyRigidbody.isKinematic)
        {
            enemyRigidbody.MovePosition(position);
            enemyRigidbody.MoveRotation(rotation);
            return;
        }

        transform.SetPositionAndRotation(
            position,
            rotation
        );
    }

    private Vector3 GetCurrentPosition()
    {
        return enemyRigidbody != null
            ? enemyRigidbody.position
            : transform.position;
    }

    private Quaternion GetCurrentRotation()
    {
        return enemyRigidbody != null
            ? enemyRigidbody.rotation
            : transform.rotation;
    }

    private Vector3 GetPlayerAimPosition()
    {
        return currentPlayer != null
            ? currentPlayer.position + Vector3.up * playerAimHeightOffset
            : lastSeenPlayerPosition;
    }

    private void UpdatePlayerVelocityEstimate()
    {
        if (currentPlayer == null)
        {
            ResetPlayerVelocityTracking();
            return;
        }

        float now = Time.time;

        if (_velocitySamplePlayer != currentPlayer ||
            !_hasPlayerVelocitySample)
        {
            _velocitySamplePlayer = currentPlayer;
            _lastPlayerSamplePosition = currentPlayer.position;
            _lastPlayerSampleTime = now;
            _smoothedPlayerVelocity = Vector3.zero;
            _hasPlayerVelocitySample = true;
            return;
        }

        float deltaTime = now - _lastPlayerSampleTime;

        if (deltaTime <= 0.0001f)
        {
            return;
        }

        Vector3 sampledVelocity;

        if (_playerCharacterController != null &&
            _playerCharacterController.enabled)
        {
            sampledVelocity = _playerCharacterController.velocity;
        }
        else
        {
            Vector3 currentPosition = currentPlayer.position;
            sampledVelocity =
                (currentPosition - _lastPlayerSamplePosition) /
                deltaTime;
        }

        float maxSpeed = Mathf.Max(
            0.1f,
            interceptMaximumPredictionSpeed
        );

        if (sampledVelocity.magnitude > maxSpeed)
        {
            sampledVelocity =
                sampledVelocity.normalized * maxSpeed;
        }

        float response =
            1f - Mathf.Exp(-12f * deltaTime);

        _smoothedPlayerVelocity = Vector3.Lerp(
            _smoothedPlayerVelocity,
            sampledVelocity,
            response
        );

        _lastPlayerSamplePosition = currentPlayer.position;
        _lastPlayerSampleTime = now;
    }

    private void ResetPlayerVelocityTracking()
    {
        _velocitySamplePlayer = currentPlayer;
        _lastPlayerSamplePosition = currentPlayer != null
            ? currentPlayer.position
            : Vector3.zero;
        _lastPlayerSampleTime = Time.time;
        _smoothedPlayerVelocity = Vector3.zero;
        _hasPlayerVelocitySample = currentPlayer != null;
    }

    private bool UpdateDiveAttack()
    {
        if (!enableDiveAttack ||
            currentPlayer == null ||
            _externalControlActive ||
            _rewindSuspended)
        {
            CancelDiveAttack(false);
            return false;
        }

        if (_divePhase == DivePhase.None)
        {
            if (Time.time < _nextDiveAttackTime ||
                IsTwinInterceptAttackActive())
            {
                return false;
            }

            float distance = Vector3.Distance(
                GetCurrentPosition(),
                GetPlayerAimPosition()
            );
            float minRange = Mathf.Max(0.1f, diveMinimumRange);
            float maxRange = Mathf.Max(minRange, diveMaximumRange);

            if (distance < minRange || distance > maxRange)
            {
                return false;
            }

            BeginDiveAttack();
            return true;
        }

        _divePhaseRemaining -= Time.deltaTime;

        switch (_divePhase)
        {
            case DivePhase.Pullback:
            {
                float pullbackProgress = 1f - Mathf.Clamp01(
                    _divePhaseRemaining /
                    Mathf.Max(0.05f, divePullbackDuration)
                );
                _diveJuiceFx?.SetWindupProgress(
                    pullbackProgress * 0.45f
                );

                float remainingDistance = Vector3.Distance(
                    GetCurrentPosition(),
                    _divePullbackTarget
                );
                float moveSpeed = remainingDistance /
                    Mathf.Max(0.05f, _divePhaseRemaining);

                MoveTowardWorldPoint(
                    _divePullbackTarget,
                    Mathf.Max(0.5f, moveSpeed)
                );
                RotateTowardWorldPoint(GetPlayerAimPosition());
                UpdateDiveSoftTarget();

                if (_divePhaseRemaining <= 0f)
                {
                    _divePhase = DivePhase.Charge;
                    _divePhaseRemaining = Mathf.Max(
                        0.05f,
                        diveChargeDuration
                    );
                    currentMoveVelocity = Vector3.zero;
                }

                break;
            }

            case DivePhase.Charge:
            {
                float chargeProgress = 1f - Mathf.Clamp01(
                    _divePhaseRemaining /
                    Mathf.Max(0.05f, diveChargeDuration)
                );
                _diveJuiceFx?.SetWindupProgress(
                    Mathf.Lerp(0.45f, 0.92f, chargeProgress)
                );

                currentMoveVelocity = Vector3.MoveTowards(
                    currentMoveVelocity,
                    Vector3.zero,
                    acceleration * 2f * Time.deltaTime
                );
                RotateTowardWorldPoint(GetPlayerAimPosition());
                UpdateDiveSoftTarget();

                if (_divePhaseRemaining <= 0f)
                {
                    LockDiveTarget();
                }

                break;
            }

            case DivePhase.Locked:
                _diveJuiceFx?.SetWindupProgress(1f);
                currentMoveVelocity = Vector3.zero;
                RotateTowardWorldPoint(_diveEndPoint);

                if (_divePhaseRemaining <= 0f)
                {
                    BeginDiveMovement();
                }

                break;

            case DivePhase.Diving:
                currentMoveVelocity =
                    _diveDirection * Mathf.Max(0.1f, diveSpeed);
                RequestRotation(
                    Quaternion.LookRotation(_diveDirection, Vector3.up)
                );
                break;

            case DivePhase.Recovery:
                currentMoveVelocity = Vector3.MoveTowards(
                    currentMoveVelocity,
                    Vector3.zero,
                    acceleration * 2f * Time.deltaTime
                );

                if (_divePhaseRemaining <= 0f)
                {
                    _divePhase = DivePhase.None;
                    _divePhaseRemaining = 0f;
                    currentMoveVelocity = Vector3.zero;
                }

                break;
        }

        return true;
    }

    private void BeginDiveAttack()
    {
        CancelTwinInterceptAttack(false);

        Vector3 currentPosition = GetCurrentPosition();
        Vector3 toPlayer = GetPlayerAimPosition() - currentPosition;
        Vector3 retreatDirection = toPlayer;
        retreatDirection.y = 0f;

        if (retreatDirection.sqrMagnitude <= 0.0001f)
        {
            retreatDirection = transform.forward;
            retreatDirection.y = 0f;
        }

        retreatDirection.Normalize();
        _divePullbackTarget =
            currentPosition -
            retreatDirection * Mathf.Max(0f, divePullbackDistance) +
            Vector3.up * Mathf.Max(0f, divePullbackLift);

        if (patrolVolume != null)
        {
            _divePullbackTarget = patrolVolume.ClampWorldPoint(
                _divePullbackTarget,
                volumeBoundaryPadding
            );
        }

        _divePhase = DivePhase.Pullback;
        _divePhaseRemaining = Mathf.Max(0.05f, divePullbackDuration);
        _divePlayerHit = false;

        Transform fxAnchor = aimPoint != null ? aimPoint : transform;
        _diveTelegraphFx = FlyingBotDiveTelegraphFX.Spawn(
            fxAnchor,
            GetPlayerAimPosition()
        );
        _diveJuiceFx = FlyingBotDiveJuiceFX.Spawn(
            fxAnchor,
            visualRoot,
            diveChargeOrange
        );

        PlayInterceptAudio(InterceptAudioCue.Lock);
    }

    private void UpdateDiveSoftTarget()
    {
        if (_diveTelegraphFx != null)
        {
            _diveTelegraphFx.UpdateTarget(GetPlayerAimPosition());
        }
    }

    private void LockDiveTarget()
    {
        Vector3 predictedVelocity = _smoothedPlayerVelocity;
        float maximumSpeed = Mathf.Max(0.1f, interceptMaximumPredictionSpeed);

        if (predictedVelocity.magnitude > maximumSpeed)
        {
            predictedVelocity = predictedVelocity.normalized * maximumSpeed;
        }

        _diveLockedTarget =
            GetPlayerAimPosition() +
            predictedVelocity * Mathf.Max(0f, divePredictionTime);

        Vector3 origin = GetCurrentPosition();
        Vector3 direction = _diveLockedTarget - origin;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        _diveDirection = direction.normalized;
        _diveEndPoint =
            _diveLockedTarget +
            _diveDirection * Mathf.Max(0f, diveOvershootDistance);
        _divePhase = DivePhase.Locked;
        _divePhaseRemaining = Mathf.Max(0.05f, diveLockDuration);
        currentMoveVelocity = Vector3.zero;

        if (_diveTelegraphFx != null)
        {
            _diveTelegraphFx.LockTarget(_diveLockedTarget);
        }

        _diveJuiceFx?.LockFlash();

        PlayInterceptAudio(InterceptAudioCue.Reacquire);
    }

    private void BeginDiveMovement()
    {
        _divePhase = DivePhase.Diving;
        _divePhaseRemaining = 0f;
        currentMoveVelocity =
            _diveDirection * Mathf.Max(0.1f, diveSpeed);

        if (_diveTelegraphFx != null)
        {
            _diveTelegraphFx.LaunchFlash();
        }

        _diveJuiceFx?.Launch(_diveDirection);

        PlayInterceptAudio(InterceptAudioCue.Punishment);
    }

    private void FixedUpdateDiveMovement()
    {
        Vector3 currentPosition = GetCurrentPosition();
        float remainingDistance = Vector3.Distance(
            currentPosition,
            _diveEndPoint
        );

        if (remainingDistance <= 0.025f)
        {
            EnterDiveRecovery(false);
            return;
        }

        float requestedDistance = Mathf.Min(
            remainingDistance,
            Mathf.Max(0.1f, diveSpeed) * Time.fixedDeltaTime
        );
        Quaternion desiredRotation = Quaternion.LookRotation(
            _diveDirection,
            Vector3.up
        );
        RequestRotation(desiredRotation);
        Quaternion safeRotation = ResolveSafeRequestedRotation(
            currentPosition,
            GetCurrentRotation()
        );

        bool hitEnvironment = TryCastObstacleHitboxes(
            currentPosition,
            safeRotation,
            _diveDirection,
            requestedDistance + obstacleSkinWidth,
            0f,
            out RaycastHit obstacleHit
        );

        float allowedDistance = requestedDistance;

        if (hitEnvironment)
        {
            allowedDistance = Mathf.Min(
                requestedDistance,
                Mathf.Max(
                    0f,
                    obstacleHit.distance - obstacleSkinWidth
                )
            );
        }

        Vector3 targetPosition =
            currentPosition + _diveDirection * allowedDistance;

        TryApplyDivePlayerHit(currentPosition, targetPosition);
        MoveRootPose(targetPosition, safeRotation);

        if (hitEnvironment && allowedDistance < requestedDistance)
        {
            EnterDiveRecovery(true);
            return;
        }

        if (requestedDistance >= remainingDistance - 0.001f)
        {
            EnterDiveRecovery(false);
        }
    }

    private void TryApplyDivePlayerHit(Vector3 segmentStart, Vector3 segmentEnd)
    {
        if (_divePlayerHit || _playerHealth == null)
        {
            return;
        }

        Vector3 playerPoint = GetPlayerAimPosition();
        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;
        float segmentRatio = segmentLengthSquared <= 0.0001f
            ? 0f
            : Mathf.Clamp01(
                Vector3.Dot(playerPoint - segmentStart, segment) /
                segmentLengthSquared
            );
        Vector3 closestPoint = segmentStart + segment * segmentRatio;

        if (Vector3.Distance(playerPoint, closestPoint) >
            Mathf.Max(0.1f, diveHitRadius))
        {
            return;
        }

        _divePlayerHit = true;
        _playerHealth.TakeDamage(Mathf.Max(0f, diveDamage));
        _diveJuiceFx?.PlayerImpact(closestPoint, _diveDirection);
        ApplyDivePlayerKnockback();
    }

    private void ApplyDivePlayerKnockback()
    {
        if (_playerHealth == null)
        {
            return;
        }

        bool hasAirborneLaunch =
            useAirborneDiveLaunch &&
            (diveLaunchHorizontalSpeed > 0f ||
             diveLaunchUpwardSpeed > 0f);

        bool hasLegacyKnockback =
            !useAirborneDiveLaunch &&
            diveKnockbackDistance > 0f &&
            diveKnockbackDuration > 0f;

        if (!hasAirborneLaunch && !hasLegacyKnockback)
        {
            return;
        }

        FirstPersonController controller =
            _playerHealth.GetComponentInParent<FirstPersonController>();

        if (controller == null)
        {
            controller =
                _playerHealth.GetComponentInChildren<FirstPersonController>();
        }

        Vector3 knockbackDirection = _diveDirection;
        knockbackDirection.y = 0f;

        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            knockbackDirection = transform.forward;
        }

        if (controller == null)
        {
            return;
        }

        if (useAirborneDiveLaunch)
        {
            Vector3 launchVelocity =
                knockbackDirection.normalized *
                Mathf.Max(0f, diveLaunchHorizontalSpeed);

            if (diveLaunchMaxHorizontalSpeed > 0f)
            {
                launchVelocity = Vector3.ClampMagnitude(
                    launchVelocity,
                    diveLaunchMaxHorizontalSpeed
                );
            }

            launchVelocity.y =
                Mathf.Max(0f, diveLaunchUpwardSpeed);

            controller.ApplyGravityLiftExitBoost(
                launchVelocity,
                Mathf.Max(0f, diveLaunchGraceTime),
                Mathf.Max(0f, diveLaunchNoDragDuration)
            );

            return;
        }

        controller.ApplyControlledKnockback(
            knockbackDirection.normalized,
            Mathf.Max(0f, diveKnockbackDistance),
            Mathf.Max(0.01f, diveKnockbackDuration)
        );
    }

    private void EnterDiveRecovery(bool crashed)
    {
        _divePhase = DivePhase.Recovery;
        _divePhaseRemaining = crashed
            ? Mathf.Max(0.05f, diveCrashRecoveryDuration)
            : Mathf.Max(0.05f, diveRecoveryDuration);
        currentMoveVelocity = Vector3.zero;

        if (_diveJuiceFx != null)
        {
            _diveJuiceFx.Complete(
                crashed,
                GetCurrentPosition(),
                _diveDirection
            );
            _diveJuiceFx = null;
        }

        ScheduleDiveCooldown();
        _nextInterceptAttackTime = Mathf.Max(
            _nextInterceptAttackTime,
            Time.time + 1f
        );
    }

    private void CancelDiveAttack(bool applyRetryDelay)
    {
        if (_diveTelegraphFx != null)
        {
            _diveTelegraphFx.CancelImmediate();
            _diveTelegraphFx = null;
        }

        if (_diveJuiceFx != null)
        {
            _diveJuiceFx.CancelImmediate();
            _diveJuiceFx = null;
        }

        bool wasActive = _divePhase != DivePhase.None;
        _divePhase = DivePhase.None;
        _divePhaseRemaining = 0f;
        _divePlayerHit = false;

        if (wasActive)
        {
            currentMoveVelocity = Vector3.zero;
        }

        if (applyRetryDelay && wasActive)
        {
            _nextDiveAttackTime = Mathf.Max(
                _nextDiveAttackTime,
                Time.time + 0.55f
            );
        }
    }

    private bool IsDiveCommitted()
    {
        return _divePhase == DivePhase.Locked ||
               _divePhase == DivePhase.Diving ||
               _divePhase == DivePhase.Recovery;
    }

    private bool IsTwinInterceptAttackActive()
    {
        return _interceptTelegraphActive ||
               _interceptSecondShotPending ||
               _interceptSecondGroupPending ||
               _interceptFourthShotPending;
    }

    private void ScheduleDiveCooldown()
    {
        float minimum = Mathf.Min(
            diveCooldownRange.x,
            diveCooldownRange.y
        );
        float maximum = Mathf.Max(
            diveCooldownRange.x,
            diveCooldownRange.y
        );

        _nextDiveAttackTime =
            Time.time +
            Random.Range(
                Mathf.Max(0.1f, minimum),
                Mathf.Max(0.1f, maximum)
            );
    }

    private void UpdateTwinInterceptAttack()
    {
        if (!enableTwinInterceptShot ||
            currentPlayer == null ||
            _externalControlActive ||
            _rewindSuspended)
        {
            CancelTwinInterceptAttack(false);
            return;
        }

        if (_interceptSecondShotPending)
        {
            _interceptSecondShotRemaining -= Time.deltaTime;

            float lockRemaining = Mathf.Max(
                0f,
                interceptSecondShotDelay -
                interceptSecondShotLockDelay
            );

            if (!_interceptSecondShotLocked &&
                _interceptSecondShotRemaining <= lockRemaining)
            {
                LockDelayedInterceptShot();
            }

            if (_interceptSecondShotRemaining <= 0f)
            {
                FireDelayedInterceptShot();
            }

            return;
        }

        if (_interceptSecondGroupPending)
        {
            _interceptSecondGroupRemaining -= Time.deltaTime;

            if (!_interceptSecondGroupLocked &&
                _interceptSecondGroupRemaining <=
                Mathf.Max(0f, interceptSecondGroupLockLeadTime))
            {
                LockSecondInterceptGroup();
            }

            if (_interceptSecondGroupRemaining <= 0f)
            {
                FireSecondGroupProbeShot();
            }

            return;
        }

        if (_interceptFourthShotPending)
        {
            _interceptFourthShotRemaining -= Time.deltaTime;

            if (_interceptFourthShotRemaining <= 0f)
            {
                FireSecondGroupPunishmentShot();
            }

            return;
        }

        float distance = Vector3.Distance(
            GetCurrentPosition(),
            GetPlayerAimPosition()
        );

        float minRange = Mathf.Max(0.1f, interceptMinimumRange);
        float maxRange = Mathf.Max(
            minRange,
            interceptMaximumRange
        );

        if (_interceptTelegraphActive)
        {
            if (distance < minRange ||
                distance > maxRange ||
                !CanVisuallyDetectPlayer())
            {
                CancelTwinInterceptAttack(true);
                return;
            }

            _interceptTelegraphRemaining -= Time.deltaTime;

            if (_interceptTelegraphRemaining <= 0f)
            {
                FireTwinInterceptShot();
            }

            return;
        }

        if (Time.time < _nextInterceptAttackTime ||
            distance < minRange ||
            distance > maxRange)
        {
            return;
        }

        BeginTwinInterceptTelegraph();
    }

    private void BeginTwinInterceptTelegraph()
    {
        if (currentPlayer == null)
        {
            return;
        }

        _interceptTelegraphActive = true;
        _interceptTelegraphRemaining = Mathf.Max(
            0.05f,
            interceptTelegraphDuration
        );

        Transform fxAnchor = aimPoint != null
            ? aimPoint
            : transform;

        _interceptTelegraphFx =
            FlyingBotInterceptTelegraphFX.Spawn(
                fxAnchor,
                _interceptTelegraphRemaining
            );

        PlayInterceptAudio(InterceptAudioCue.Lock);
    }

    private void LockTwinInterceptPrediction()
    {
        Vector3 playerCenter = GetPlayerAimPosition();
        Vector3 playerVelocity = _smoothedPlayerVelocity;
        Vector3 origin = aimPoint != null
            ? aimPoint.position
            : GetCurrentPosition();

        float maxPredictionSpeed = Mathf.Max(
            0.1f,
            interceptMaximumPredictionSpeed
        );

        if (playerVelocity.magnitude > maxPredictionSpeed)
        {
            playerVelocity =
                playerVelocity.normalized * maxPredictionSpeed;
        }

        float minimumPredictionTime = Mathf.Max(
            0f,
            interceptPredictionTime
        );
        float maximumPredictionTime = Mathf.Max(
            minimumPredictionTime,
            interceptMaximumPredictionTime
        );
        float predictionTime = Vector3.Distance(
            origin,
            playerCenter
        ) / Mathf.Max(0.1f, interceptProjectileSpeed);

        predictionTime = Mathf.Clamp(
            predictionTime,
            minimumPredictionTime,
            maximumPredictionTime
        );

        _lockedInterceptCenter =
            playerCenter + playerVelocity * predictionTime;

        Vector3 horizontalVelocity = playerVelocity;
        horizontalVelocity.y = 0f;

        bool hasDirectionalMovement =
            horizontalVelocity.magnitude >=
            Mathf.Max(0f, interceptDirectionalSpeedThreshold);
        Vector3 escapeAxis;

        if (hasDirectionalMovement)
        {
            escapeAxis = horizontalVelocity.normalized;
        }
        else
        {
            escapeAxis = transform.right * _strafeDirection;
            escapeAxis.y = 0f;

            if (escapeAxis.sqrMagnitude <= 0.001f)
            {
                escapeAxis = Vector3.right;
            }
            else
            {
                escapeAxis.Normalize();
            }
        }

        float error = Mathf.Max(0f, interceptPredictionError);

        if (error > 0.0001f)
        {
            Vector2 randomError =
                Random.insideUnitCircle * error;

            Vector3 forwardAxis = horizontalVelocity.sqrMagnitude > 0.001f
                ? horizontalVelocity.normalized
                : Vector3.Cross(escapeAxis, Vector3.up).normalized;
            Vector3 sideAxis = Vector3.Cross(
                Vector3.up,
                forwardAxis
            ).normalized;

            _lockedInterceptCenter +=
                sideAxis * randomError.x +
                forwardAxis * randomError.y;
        }

        float escapeOffset = Mathf.Max(
            0f,
            interceptHalfSpacing
        );

        _lockedInterceptTargetB =
            _lockedInterceptCenter +
            escapeAxis * (hasDirectionalMovement ? escapeOffset : 0f);
        _lockedInterceptEscapeAxis = escapeAxis;
        _lockedInterceptHasDirectionalMovement =
            hasDirectionalMovement;
    }

    private void FireTwinInterceptShot()
    {
        if (!_interceptTelegraphActive)
        {
            return;
        }

        _interceptTelegraphActive = false;

        if (_interceptTelegraphFx != null)
        {
            _interceptTelegraphFx.CompleteAndFlash();
            _interceptTelegraphFx = null;
        }

        if (currentPlayer == null ||
            !IsCombatGateOpen() ||
            !CanVisuallyDetectPlayer())
        {
            ScheduleInterceptRetry(0.35f);
            return;
        }

        // Lock only after the readable telegraph has completed. From this
        // point onward neither projectile homes or corrects its target.
        LockTwinInterceptPrediction();

        Vector3 origin = aimPoint != null
            ? aimPoint.position
            : GetCurrentPosition();

        SpawnInterceptProjectile(
            origin,
            _lockedInterceptCenter,
            false
        );
        PlayInterceptAudio(InterceptAudioCue.Probe);

        _interceptSecondShotPending = true;
        _interceptSecondShotLocked = false;
        _interceptSecondShotRemaining = Mathf.Max(
            0f,
            interceptSecondShotDelay
        );

    }

    private void LockDelayedInterceptShot()
    {
        if (!_interceptSecondShotPending ||
            _interceptSecondShotLocked)
        {
            return;
        }

        // Re-sample after the player has had time to react to the probe shot.
        // The punishment shot remains straight and non-homing after this lock.
        LockTwinInterceptPrediction();
        _interceptSecondShotLocked = true;

        Transform fxAnchor = aimPoint != null
            ? aimPoint
            : transform;

        _interceptTelegraphFx =
            FlyingBotInterceptTelegraphFX.Spawn(
                fxAnchor,
                Mathf.Max(0.05f, _interceptSecondShotRemaining),
                true
            );
    }

    private void FireDelayedInterceptShot()
    {
        if (!_interceptSecondShotPending)
        {
            return;
        }

        if (!_interceptSecondShotLocked)
        {
            LockDelayedInterceptShot();
        }

        _interceptSecondShotPending = false;
        _interceptSecondShotLocked = false;
        _interceptSecondShotRemaining = 0f;

        if (_interceptTelegraphFx != null)
        {
            _interceptTelegraphFx.CompleteAndFlash();
            _interceptTelegraphFx = null;
        }

        Vector3 origin = aimPoint != null
            ? aimPoint.position
            : GetCurrentPosition();

        SpawnInterceptProjectile(
            origin,
            _lockedInterceptTargetB,
            true
        );
        PlayInterceptAudio(InterceptAudioCue.Punishment);

        _interceptSecondGroupPending = true;
        _interceptSecondGroupLocked = false;
        _interceptSecondGroupRemaining = Mathf.Max(
            0f,
            interceptSecondGroupDelay
        );
    }

    private void LockSecondInterceptGroup()
    {
        if (!_interceptSecondGroupPending ||
            _interceptSecondGroupLocked)
        {
            return;
        }

        // The second pair reads the route created by the first pair, then
        // locks two separated points ahead of and behind that new movement.
        LockTwinInterceptPrediction();

        float halfSpacing = Mathf.Max(
            0f,
            interceptSecondGroupHalfSpacing
        );

        if (_lockedInterceptHasDirectionalMovement)
        {
            _lockedSecondGroupTargetA =
                _lockedInterceptCenter +
                _lockedInterceptEscapeAxis * halfSpacing;
            _lockedSecondGroupTargetB =
                _lockedInterceptCenter -
                _lockedInterceptEscapeAxis * halfSpacing;
        }
        else
        {
            // A stationary player must not sit safely between two bracket
            // shots. Keep the probe on center and reserve the final shot for
            // the first escape direction they may choose.
            _lockedSecondGroupTargetA = _lockedInterceptCenter;
            _lockedSecondGroupTargetB =
                _lockedInterceptCenter +
                _lockedInterceptEscapeAxis * halfSpacing;
        }
        _interceptSecondGroupLocked = true;

        Transform fxAnchor = aimPoint != null
            ? aimPoint
            : transform;

        _interceptTelegraphFx =
            FlyingBotInterceptTelegraphFX.Spawn(
                fxAnchor,
                Mathf.Max(0.05f, _interceptSecondGroupRemaining),
                true
            );

        PlayInterceptAudio(InterceptAudioCue.Reacquire);
    }

    private void FireSecondGroupProbeShot()
    {
        if (!_interceptSecondGroupPending)
        {
            return;
        }

        if (!_interceptSecondGroupLocked)
        {
            LockSecondInterceptGroup();
        }

        _interceptSecondGroupPending = false;
        _interceptSecondGroupLocked = false;
        _interceptSecondGroupRemaining = 0f;

        if (_interceptTelegraphFx != null)
        {
            _interceptTelegraphFx.CompleteAndFlash();
            _interceptTelegraphFx = null;
        }

        SpawnInterceptProjectile(
            ResolveInterceptOrigin(-interceptSecondGroupMuzzleOffset),
            _lockedSecondGroupTargetA,
            false
        );
        PlayInterceptAudio(InterceptAudioCue.Probe);

        _interceptFourthShotPending = true;
        _interceptFourthShotRemaining = Mathf.Max(
            0f,
            interceptFourthShotDelay
        );

        Transform fxAnchor = aimPoint != null
            ? aimPoint
            : transform;

        _interceptTelegraphFx =
            FlyingBotInterceptTelegraphFX.Spawn(
                fxAnchor,
                Mathf.Max(0.05f, _interceptFourthShotRemaining),
                true
            );
    }

    private void FireSecondGroupPunishmentShot()
    {
        if (!_interceptFourthShotPending)
        {
            return;
        }

        _interceptFourthShotPending = false;
        _interceptFourthShotRemaining = 0f;

        if (_interceptTelegraphFx != null)
        {
            _interceptTelegraphFx.CompleteAndFlash();
            _interceptTelegraphFx = null;
        }

        SpawnInterceptProjectile(
            ResolveInterceptOrigin(interceptSecondGroupMuzzleOffset),
            _lockedSecondGroupTargetB,
            true
        );
        PlayInterceptAudio(InterceptAudioCue.Punishment);

        ScheduleInterceptCooldown();
    }

    private Vector3 ResolveInterceptOrigin(float localRightOffset)
    {
        Transform originAnchor = aimPoint != null
            ? aimPoint
            : transform;

        return originAnchor.position +
            originAnchor.right * localRightOffset;
    }

    private void ScheduleInterceptCooldown()
    {
        float minCooldown = Mathf.Min(
            interceptCooldownRange.x,
            interceptCooldownRange.y
        );
        float maxCooldown = Mathf.Max(
            interceptCooldownRange.x,
            interceptCooldownRange.y
        );

        _nextInterceptAttackTime =
            Time.time +
            Random.Range(
                Mathf.Max(0.1f, minCooldown),
                Mathf.Max(0.1f, maxCooldown)
            );
        _nextDiveAttackTime = Mathf.Max(
            _nextDiveAttackTime,
            Time.time + 1f
        );
    }

    private void SpawnInterceptProjectile(
        Vector3 origin,
        Vector3 target,
        bool punishmentShot
    )
    {
        Vector3 direction = target - origin;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        FlyingBotInterceptProjectile.Spawn(
            origin,
            direction.normalized,
            Mathf.Max(0.1f, interceptProjectileSpeed),
            Mathf.Max(0f, interceptProjectileDamage),
            Mathf.Max(0.1f, interceptProjectileLifeTime),
            Mathf.Max(0.01f, interceptProjectileRadius),
            interceptProjectileCollisionMask,
            transform,
            punishmentShot
        );

        Color muzzleColor = punishmentShot
            ? Color.Lerp(Color.white, detectedRed, 0.72f)
            : Color.Lerp(Color.white, diveChargeOrange, 0.58f);
        FlyingBotCombatJuiceFX.SpawnMuzzle(
            origin,
            direction.normalized,
            muzzleColor,
            punishmentShot
        );
        TriggerMuzzleReaction(
            direction.normalized,
            punishmentShot
        );
    }

    private enum InterceptAudioCue
    {
        Lock,
        Reacquire,
        Probe,
        Punishment
    }

    private void PrepareInterceptAudioSource()
    {
        if (interceptAudioSource == null)
        {
            interceptAudioSource = GetComponent<AudioSource>();
        }

        if (interceptAudioSource == null)
        {
            interceptAudioSource = gameObject.AddComponent<AudioSource>();
        }

        interceptAudioSource.playOnAwake = false;
        interceptAudioSource.loop = false;
        interceptAudioSource.spatialBlend = 1f;
        interceptAudioSource.dopplerLevel = 0f;
        interceptAudioSource.rolloffMode = AudioRolloffMode.Linear;
        interceptAudioSource.minDistance = 2.5f;
        interceptAudioSource.maxDistance = 28f;
    }

    private void PlayInterceptAudio(InterceptAudioCue cue)
    {
        if (interceptAudioSource == null)
        {
            PrepareInterceptAudioSource();
        }

        AudioClip clip = GetInterceptAudioClip(cue);

        if (clip == null)
        {
            return;
        }

        float cueVolume = cue == InterceptAudioCue.Punishment
            ? 1f
            : cue == InterceptAudioCue.Probe
                ? 0.82f
                : 0.68f;

        interceptAudioSource.pitch = 1f;
        interceptAudioSource.PlayOneShot(
            clip,
            Mathf.Clamp01(interceptAudioVolume * cueVolume)
        );
    }

    private AudioClip GetInterceptAudioClip(InterceptAudioCue cue)
    {
        AudioClip customClip = cue switch
        {
            InterceptAudioCue.Lock => interceptLockSound,
            InterceptAudioCue.Reacquire => interceptReacquireSound,
            InterceptAudioCue.Probe => interceptProbeShotSound,
            _ => interceptPunishmentShotSound
        };

        if (customClip != null || !generateFallbackInterceptAudio)
        {
            return customClip;
        }

        switch (cue)
        {
            case InterceptAudioCue.Lock:
                return _sharedInterceptLockSound ??=
                    CreateInterceptAudioClip(cue, 0.18f);
            case InterceptAudioCue.Reacquire:
                return _sharedInterceptReacquireSound ??=
                    CreateInterceptAudioClip(cue, 0.15f);
            case InterceptAudioCue.Probe:
                return _sharedInterceptProbeSound ??=
                    CreateInterceptAudioClip(cue, 0.09f);
            default:
                return _sharedInterceptPunishmentSound ??=
                    CreateInterceptAudioClip(cue, 0.14f);
        }
    }

    private static AudioClip CreateInterceptAudioClip(
        InterceptAudioCue cue,
        float duration
    )
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)sampleRate;
            float progress = index / (float)Mathf.Max(1, sampleCount - 1);
            float frequency;
            float envelope;
            float sample;

            switch (cue)
            {
                case InterceptAudioCue.Lock:
                    frequency = Mathf.Lerp(520f, 1180f, progress);
                    phase += frequency / sampleRate;
                    envelope = Mathf.Sin(Mathf.PI * progress);
                    sample =
                        Mathf.Sin(phase * Mathf.PI * 2f) * 0.62f +
                        Mathf.Sin(phase * Mathf.PI * 4f) * 0.16f;
                    break;

                case InterceptAudioCue.Reacquire:
                    bool secondPulse = progress >= 0.52f;
                    float pulseProgress = secondPulse
                        ? Mathf.InverseLerp(0.52f, 1f, progress)
                        : Mathf.InverseLerp(0f, 0.44f, progress);
                    frequency = secondPulse ? 1280f : 860f;
                    phase += frequency / sampleRate;
                    envelope = progress > 0.44f && progress < 0.52f
                        ? 0f
                        : Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulseProgress));
                    sample = Mathf.Sin(phase * Mathf.PI * 2f) * 0.72f;
                    break;

                case InterceptAudioCue.Probe:
                    frequency = Mathf.Lerp(1760f, 620f, progress);
                    phase += frequency / sampleRate;
                    envelope = (1f - progress) * (1f - progress);
                    sample =
                        Mathf.Sin(phase * Mathf.PI * 2f) * 0.58f +
                        DeterministicAudioNoise(index) * 0.12f;
                    break;

                default:
                    frequency = Mathf.Lerp(720f, 150f, progress);
                    phase += frequency / sampleRate;
                    envelope = (1f - progress) * Mathf.Min(1f, time * 90f);
                    sample =
                        Mathf.Sin(phase * Mathf.PI * 2f) * 0.58f +
                        Mathf.Sin(phase * Mathf.PI * 4f) * 0.22f +
                        DeterministicAudioNoise(index) * 0.18f;
                    break;
            }

            samples[index] = Mathf.Clamp(sample * envelope * 0.82f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(
            $"FlyingBot_{cue}_Fallback",
            sampleCount,
            1,
            sampleRate,
            false
        );
        clip.SetData(samples, 0);
        return clip;
    }

    private static float DeterministicAudioNoise(int sampleIndex)
    {
        float value = Mathf.Sin(sampleIndex * 12.9898f) * 43758.5453f;
        return (value - Mathf.Floor(value)) * 2f - 1f;
    }

    private void CancelTwinInterceptAttack(bool applyRetryDelay)
    {
        if (_interceptTelegraphFx != null)
        {
            _interceptTelegraphFx.CancelImmediate();
            _interceptTelegraphFx = null;
        }

        bool wasActive =
            _interceptTelegraphActive ||
            _interceptSecondShotPending ||
            _interceptSecondGroupPending ||
            _interceptFourthShotPending;
        _interceptTelegraphActive = false;
        _interceptTelegraphRemaining = 0f;
        _interceptSecondShotPending = false;
        _interceptSecondShotLocked = false;
        _interceptSecondShotRemaining = 0f;
        _interceptSecondGroupPending = false;
        _interceptSecondGroupLocked = false;
        _interceptSecondGroupRemaining = 0f;
        _interceptFourthShotPending = false;
        _interceptFourthShotRemaining = 0f;

        if (applyRetryDelay && wasActive)
        {
            ScheduleInterceptRetry(0.35f);
        }
    }

    private void ScheduleInterceptRetry(float delay)
    {
        _nextInterceptAttackTime = Mathf.Max(
            _nextInterceptAttackTime,
            Time.time + Mathf.Max(0f, delay)
        );
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        visionRange = Mathf.Max(0.1f, visionRange);
        visionAngle = Mathf.Clamp(visionAngle, 1f, 179f);
        nearbyAwarenessRange = Mathf.Max(0f, nearbyAwarenessRange);
        scanHalfAngle = Mathf.Clamp(scanHalfAngle, 1f, 179f);
        patrolArrivalRadius = Mathf.Max(0.05f, patrolArrivalRadius);
        obstacleEnvelopePadding = Mathf.Max(0f, obstacleEnvelopePadding);
        obstacleSkinWidth = Mathf.Max(0f, obstacleSkinWidth);
        obstacleProbeRadius = Mathf.Max(0.02f, obstacleProbeRadius);
        obstacleProbeDistance = Mathf.Max(0.05f, obstacleProbeDistance);
        preferredCombatDistance = Mathf.Max(0.1f, preferredCombatDistance);
        interceptTelegraphDuration = Mathf.Max(0.05f, interceptTelegraphDuration);
        interceptPredictionTime = Mathf.Max(0f, interceptPredictionTime);
        interceptMaximumPredictionTime = Mathf.Max(interceptPredictionTime, interceptMaximumPredictionTime);
        interceptHalfSpacing = Mathf.Max(0f, interceptHalfSpacing);
        interceptSecondShotDelay = Mathf.Max(0f, interceptSecondShotDelay);
        interceptSecondShotLockDelay = Mathf.Clamp(interceptSecondShotLockDelay, 0f, interceptSecondShotDelay);
        interceptSecondGroupDelay = Mathf.Max(0f, interceptSecondGroupDelay);
        interceptSecondGroupLockLeadTime = Mathf.Clamp(interceptSecondGroupLockLeadTime, 0f, interceptSecondGroupDelay);
        interceptFourthShotDelay = Mathf.Max(0f, interceptFourthShotDelay);
        interceptSecondGroupHalfSpacing = Mathf.Max(0f, interceptSecondGroupHalfSpacing);
        interceptSecondGroupMuzzleOffset = Mathf.Max(0f, interceptSecondGroupMuzzleOffset);
        interceptPredictionError = Mathf.Max(0f, interceptPredictionError);
        interceptMinimumRange = Mathf.Max(0.1f, interceptMinimumRange);
        interceptMaximumRange = Mathf.Max(interceptMinimumRange, interceptMaximumRange);
        interceptInitialDelay = Mathf.Max(0f, interceptInitialDelay);
        interceptProjectileSpeed = Mathf.Max(0.1f, interceptProjectileSpeed);
        interceptProjectileDamage = Mathf.Max(0f, interceptProjectileDamage);
        interceptProjectileLifeTime = Mathf.Max(0.1f, interceptProjectileLifeTime);
        interceptProjectileRadius = Mathf.Max(0.01f, interceptProjectileRadius);
        interceptMaximumPredictionSpeed = Mathf.Max(0.1f, interceptMaximumPredictionSpeed);
        diveMinimumRange = Mathf.Max(0.1f, diveMinimumRange);
        diveMaximumRange = Mathf.Max(diveMinimumRange, diveMaximumRange);
        diveInitialDelay = Mathf.Max(0f, diveInitialDelay);
        divePullbackDuration = Mathf.Max(0.05f, divePullbackDuration);
        divePullbackDistance = Mathf.Max(0f, divePullbackDistance);
        divePullbackLift = Mathf.Max(0f, divePullbackLift);
        diveChargeDuration = Mathf.Max(0.05f, diveChargeDuration);
        diveLockDuration = Mathf.Max(0.05f, diveLockDuration);
        diveSpeed = Mathf.Max(0.1f, diveSpeed);
        diveOvershootDistance = Mathf.Max(0f, diveOvershootDistance);
        divePredictionTime = Mathf.Max(0f, divePredictionTime);
        diveHitRadius = Mathf.Max(0.1f, diveHitRadius);
        diveDamage = Mathf.Max(0f, diveDamage);
        diveKnockbackDistance = Mathf.Max(0f, diveKnockbackDistance);
        diveKnockbackDuration = Mathf.Max(0.01f, diveKnockbackDuration);
        diveLaunchHorizontalSpeed = Mathf.Max(0f, diveLaunchHorizontalSpeed);
        diveLaunchUpwardSpeed = Mathf.Max(0f, diveLaunchUpwardSpeed);
        diveLaunchMaxHorizontalSpeed = Mathf.Max(0f, diveLaunchMaxHorizontalSpeed);
        diveLaunchNoDragDuration = Mathf.Max(0f, diveLaunchNoDragDuration);
        diveLaunchGraceTime = Mathf.Max(0f, diveLaunchGraceTime);
        diveRecoveryDuration = Mathf.Max(0.05f, diveRecoveryDuration);
        diveCrashRecoveryDuration = Mathf.Max(0.05f, diveCrashRecoveryDuration);
        ResolveReferences();
        CacheObstacleEnvelope();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawVisionGizmos)
        {
            return;
        }

        Vector3 origin = aimPoint != null
            ? aimPoint.position
            : transform.position;

        Vector3 forward = transform.forward;
        Quaternion leftRotation = Quaternion.AngleAxis(
            -visionAngle * 0.5f,
            Vector3.up
        );
        Quaternion rightRotation = Quaternion.AngleAxis(
            visionAngle * 0.5f,
            Vector3.up
        );

        Gizmos.color = new Color(1f, 0.72f, 0.10f, 0.78f);
        Gizmos.DrawLine(origin, origin + forward * visionRange);
        Gizmos.DrawLine(
            origin,
            origin + leftRotation * forward * visionRange
        );
        Gizmos.DrawLine(
            origin,
            origin + rightRotation * forward * visionRange
        );

        Gizmos.color = new Color(0.15f, 0.92f, 1f, 0.82f);
        Gizmos.DrawWireSphere(
            patrolTarget,
            Mathf.Max(0.06f, patrolArrivalRadius * 0.25f)
        );
    }
#endif
}
