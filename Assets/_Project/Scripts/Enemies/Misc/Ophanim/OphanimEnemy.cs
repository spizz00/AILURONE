#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
public class OphanimEnemy : MonoBehaviour, IEnemyExternalControlReceiver
{
    public enum OphanimState
    {
        Idle,
        Roaming,
        Tracking,
        Orbiting,
        Recovering,
        Stunned
    }

    [Header("核心引用")]
    public CombatPlatform combatPlatform;
    public EnemyTarget enemyTarget;
    public EnemyContactDamage contactDamage;
    public Rigidbody enemyRigidbody;

    [Tooltip("只负责模型视觉，不要拖 Enemy_Ophanim_Root。")]
    public Transform visualRoot;

    [Tooltip("位于敌人底部，用于检测前方是否有地面。")]
    public Transform groundProbe;

    [Header("感知")]
    [Tooltip(
        "绑定 CombatPlatform 时，玩家进入平台即触发。" +
        "没有绑定平台时才使用此距离。"
    )]
    public float detectionRange = 18f;

    [Header("随机游荡")]
    public float idleDurationMinimum = 1.2f;
    public float idleDurationMaximum = 2.8f;

    public float roamingSpeed = 2.2f;
    public float roamArrivalDistance = 0.35f;

    [Tooltip("随机游荡点距离当前敌人的最小距离。")]
    public float roamMinimumTravelDistance = 1f;

    [Tooltip("随机游荡点与 CombatArea 边缘保持的距离。")]
    public float roamEdgePadding = 1f;

    [Range(1, 50)]
    public int roamPointAttempts = 30;

    [Tooltip(
        "寻找游荡点时，从 Ophanim 当前高度向上偏移多少，" +
        "再向下检测地面。"
    )]
    public float roamRaycastHeight = 4f;

    [Tooltip("寻找游荡点时向下检测地面的最大距离。")]
    public float roamRaycastDistance = 20f;

    [Header("追踪")]
    public float trackingSpeed = 3.2f;
    public float turnSpeed = 220f;

    [Tooltip("进入绕行状态的距离。")]
    public float orbitEnterDistance = 3.2f;

    [Tooltip("超过这个距离后重新直接追踪。")]
    public float orbitExitDistance = 4.2f;

    [Header("近距离绕行")]
    public float orbitDistance = 2.45f;
    public float orbitSpeed = 2.8f;

    [Tooltip("绕行时靠近或远离玩家的修正力度。")]
    public float orbitDistanceCorrection = 1.6f;

    [Tooltip("绕行方向多久可能随机切换一次。")]
    public float orbitDirectionChangeMinimum = 1.5f;

    public float orbitDirectionChangeMaximum = 3.5f;

    [Header("接触后退")]
    public float recoverDuration = 0.75f;
    public float retreatMoveDuration = 0.35f;
    public float retreatSpeed = 5f;

    [Header("平台边缘检测")]
    public LayerMask groundMask;

    [Tooltip("前方地面探针距离。")]
    public float edgeProbeForwardDistance = 1.8f;

    [Tooltip("左右探针相对中心探针的偏移。")]
    public float sideProbeOffset = 0.8f;

    [Tooltip("GroundProbe 向下检测地面的距离。")]
    public float groundCheckDistance = 0.9f;

    [Tooltip("检测移动目标是否仍在 CombatArea 内的前瞻距离。")]
    public float boundaryLookAheadDistance = 1.2f;

    [Header("运行状态")]
    [SerializeField]
    private OphanimState currentState = OphanimState.Idle;

    public OphanimState CurrentState => currentState;

    public Vector3 CurrentMoveDirection =>
        _currentMoveDirection;

    public bool IsExternalControlActive =>
        _externalControlActive;

    [SerializeField]
    private bool _externalControlActive;

    private Transform _player;

    private Vector3 _spawnPosition;
    private Vector3 _roamTarget;
    private Vector3 _retreatDirection;
    private Vector3 _currentMoveDirection;

    private float _stateTimer;
    private float _retreatTimer;
    private float _orbitDirectionTimer;

    private int _orbitDirection = 1;

    private void Awake()
    {
        if (enemyTarget == null)
        {
            enemyTarget =
                GetComponent<EnemyTarget>();
        }

        if (enemyRigidbody == null)
        {
            enemyRigidbody =
                GetComponent<Rigidbody>();
        }

        if (contactDamage == null)
        {
            contactDamage =
                GetComponentInChildren<EnemyContactDamage>();
        }

        if (combatPlatform == null)
        {
            combatPlatform =
                GetComponentInParent<CombatPlatform>();
        }

        if (enemyRigidbody != null)
        {
            enemyRigidbody.isKinematic = true;
            enemyRigidbody.useGravity = false;

            enemyRigidbody.constraints =
                RigidbodyConstraints.FreezeRotation;
        }

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(true);

            contactDamage.PlayerDamaged +=
                HandlePlayerDamaged;
        }

        _spawnPosition = transform.position;

        _orbitDirection =
            Random.value < 0.5f ? -1 : 1;
    }

    private void Start()
    {
        RefreshPlayerReference();
        EnterIdle();
    }

    private void Update()
    {
        if (enemyTarget != null &&
            enemyTarget.IsDead)
        {
            return;
        }

        if (_externalControlActive)
        {
            return;
        }

        RefreshPlayerReference();

        switch (currentState)
        {
            case OphanimState.Idle:
                UpdateIdleState();
                break;

            case OphanimState.Roaming:
                UpdateRoamingState();
                break;

            case OphanimState.Tracking:
                UpdateTrackingState();
                break;

            case OphanimState.Orbiting:
                UpdateOrbitingState();
                break;

            case OphanimState.Recovering:
                UpdateRecoveringState();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (enemyTarget != null &&
            enemyTarget.IsDead)
        {
            return;
        }

        if (_externalControlActive)
        {
            return;
        }

        switch (currentState)
        {
            case OphanimState.Roaming:
                HandleRoamingMovement();
                break;

            case OphanimState.Tracking:
                HandleTrackingMovement();
                break;

            case OphanimState.Orbiting:
                HandleOrbitingMovement();
                break;

            case OphanimState.Recovering:
                HandleRecoveringMovement();
                break;
        }
    }

    private void OnDestroy()
    {
        if (contactDamage != null)
        {
            contactDamage.PlayerDamaged -=
                HandlePlayerDamaged;
        }
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
        currentState = OphanimState.Stunned;
        _currentMoveDirection = Vector3.zero;
        _stateTimer = 0f;
        _retreatTimer = 0f;

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

        RefreshPlayerReference();

        if (CanEngagePlayer())
        {
            EnterTracking();
        }
        else
        {
            EnterIdle();
        }
    }

    // =========================================================
    // 状态更新
    // =========================================================

    private void UpdateIdleState()
    {
        if (CanEngagePlayer())
        {
            EnterTracking();
            return;
        }

        _stateTimer -= Time.deltaTime;

        if (_stateTimer > 0f)
        {
            return;
        }

        if (TryChooseRoamTarget())
        {
            EnterRoaming();
        }
        else
        {
            ResetIdleTimer();
        }
    }

    private void UpdateRoamingState()
    {
        if (CanEngagePlayer())
        {
            EnterTracking();
            return;
        }

        float distanceToTarget =
            GetFlatDistance(
                GetCurrentPosition(),
                _roamTarget
            );

        if (distanceToTarget <=
            roamArrivalDistance)
        {
            EnterIdle();
        }
    }

    private void UpdateTrackingState()
    {
        if (!CanEngagePlayer())
        {
            EnterIdle();
            return;
        }

        float distanceToPlayer =
            GetFlatDistanceToPlayer();

        if (distanceToPlayer <=
            orbitEnterDistance)
        {
            EnterOrbiting();
        }
    }

    private void UpdateOrbitingState()
    {
        if (!CanEngagePlayer())
        {
            EnterIdle();
            return;
        }

        float distanceToPlayer =
            GetFlatDistanceToPlayer();

        if (distanceToPlayer >
            orbitExitDistance)
        {
            EnterTracking();
            return;
        }

        _orbitDirectionTimer -=
            Time.deltaTime;

        if (_orbitDirectionTimer <= 0f)
        {
            if (Random.value < 0.45f)
            {
                _orbitDirection *= -1;
            }

            ResetOrbitDirectionTimer();
        }
    }

    private void UpdateRecoveringState()
    {
        _stateTimer -= Time.deltaTime;
        _retreatTimer -= Time.deltaTime;

        if (_stateTimer > 0f)
        {
            return;
        }

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(true);
        }

        if (CanEngagePlayer())
        {
            EnterTracking();
        }
        else
        {
            EnterIdle();
        }
    }

    // =========================================================
    // 移动
    // =========================================================

    private void HandleRoamingMovement()
    {
        Vector3 direction =
            GetFlatDirection(
                GetCurrentPosition(),
                _roamTarget
            );

        if (direction.sqrMagnitude <=
            0.001f)
        {
            EnterIdle();
            return;
        }

        bool moved =
            MoveSafely(
                direction,
                roamingSpeed
            );

        if (!moved)
        {
            if (TryChooseRoamTarget())
            {
                return;
            }

            EnterIdle();
        }
    }

    private void HandleTrackingMovement()
    {
        if (_player == null)
        {
            return;
        }

        Vector3 direction =
            GetFlatDirectionToPlayer();

        MoveSafely(
            direction,
            trackingSpeed
        );
    }

    private void HandleOrbitingMovement()
    {
        if (_player == null)
        {
            return;
        }

        Vector3 toPlayer =
            GetFlatDirectionToPlayer();

        if (toPlayer.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        Vector3 tangentDirection =
            Vector3.Cross(
                Vector3.up,
                toPlayer
            ) * _orbitDirection;

        float distanceToPlayer =
            GetFlatDistanceToPlayer();

        float distanceError =
            (
                distanceToPlayer -
                orbitDistance
            ) /
            Mathf.Max(
                0.1f,
                orbitDistance
            );

        Vector3 correctionDirection =
            toPlayer *
            distanceError *
            orbitDistanceCorrection;

        Vector3 finalDirection =
            (
                tangentDirection +
                correctionDirection
            ).normalized;

        bool moved =
            MoveSafely(
                finalDirection,
                orbitSpeed
            );

        if (!moved)
        {
            _orbitDirection *= -1;

            finalDirection =
                (
                    -tangentDirection +
                    correctionDirection
                ).normalized;

            MoveSafely(
                finalDirection,
                orbitSpeed
            );
        }
    }

    private void HandleRecoveringMovement()
    {
        if (_retreatTimer <= 0f)
        {
            _currentMoveDirection =
                Vector3.zero;

            return;
        }

        MoveSafely(
            _retreatDirection,
            retreatSpeed
        );
    }

    private bool MoveSafely(
        Vector3 desiredDirection,
        float moveSpeed
    )
    {
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude <=
            0.001f)
        {
            _currentMoveDirection =
                Vector3.zero;

            return false;
        }

        desiredDirection.Normalize();

        if (!TryFindSafeDirection(
                desiredDirection,
                out Vector3 safeDirection
            ))
        {
            _currentMoveDirection =
                Vector3.zero;

            return false;
        }

        _currentMoveDirection =
            safeDirection;

        RotateTowardDirection(
            safeDirection
        );

        Vector3 movement =
            safeDirection *
            moveSpeed *
            Time.fixedDeltaTime;

        MoveControlled(
            GetCurrentPosition() +
            movement
        );

        return true;
    }

    private bool TryFindSafeDirection(
        Vector3 desiredDirection,
        out Vector3 safeDirection
    )
    {
        float[] testAngles =
        {
            0f,
            30f,
            -30f,
            60f,
            -60f,
            90f,
            -90f,
            135f,
            -135f,
            180f
        };

        foreach (float angle in testAngles)
        {
            Vector3 candidate =
                Quaternion.AngleAxis(
                    angle,
                    Vector3.up
                ) *
                desiredDirection;

            candidate.y = 0f;
            candidate.Normalize();

            if (CanMoveInDirection(candidate))
            {
                safeDirection =
                    candidate;

                return true;
            }
        }

        safeDirection = Vector3.zero;
        return false;
    }

    // =========================================================
    // 平台安全检测
    // =========================================================

    private bool CanMoveInDirection(
        Vector3 direction
    )
    {
        Vector3 currentPosition =
            GetCurrentPosition();

        Vector3 boundaryTestPoint =
            currentPosition +
            direction *
            boundaryLookAheadDistance;

        if (combatPlatform != null &&
            !combatPlatform.ContainsWorldPoint(
                boundaryTestPoint
            ))
        {
            return false;
        }

        Transform probe =
            groundProbe != null
                ? groundProbe
                : transform;

        Vector3 forwardOffset =
            direction *
            edgeProbeForwardDistance;

        Vector3 sideDirection =
            Vector3.Cross(
                Vector3.up,
                direction
            ).normalized;

        bool centerHasGround =
            HasGroundAtProbePosition(
                probe.position +
                forwardOffset
            );

        bool leftHasGround =
            HasGroundAtProbePosition(
                probe.position +
                forwardOffset +
                sideDirection *
                sideProbeOffset
            );

        bool rightHasGround =
            HasGroundAtProbePosition(
                probe.position +
                forwardOffset -
                sideDirection *
                sideProbeOffset
            );

        return
            centerHasGround &&
            leftHasGround &&
            rightHasGround;
    }

    private bool HasGroundAtProbePosition(
        Vector3 worldPosition
    )
    {
        Vector3 origin =
            worldPosition +
            Vector3.up * 0.2f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    // =========================================================
    // 随机游荡点
    // =========================================================

    private bool TryChooseRoamTarget()
    {
        if (combatPlatform == null ||
            combatPlatform.ZoneCollider == null)
        {
            return TryChooseFallbackRoamTarget();
        }

        Collider zoneCollider =
            combatPlatform.ZoneCollider;

        BoxCollider boxCollider =
            zoneCollider as BoxCollider;

        for (int attempt = 0;
             attempt < roamPointAttempts;
             attempt++)
        {
            Vector3 sampledWorldPoint;

            if (boxCollider != null)
            {
                sampledWorldPoint =
                    SamplePointInsideBox(
                        boxCollider
                    );
            }
            else
            {
                Bounds bounds =
                    zoneCollider.bounds;

                sampledWorldPoint =
                    new Vector3(
                        Random.Range(
                            bounds.min.x,
                            bounds.max.x
                        ),
                        bounds.center.y,
                        Random.Range(
                            bounds.min.z,
                            bounds.max.z
                        )
                    );
            }

            /*
             * 永久修复：
             * 射线从 Ophanim 当前高度上方开始，
             * 不再使用 CombatArea 的 bounds.max.y。
             *
             * CombatArea 即使非常高，
             * 也不会影响巡逻地面检测。
             */
            Vector3 rayOrigin =
                new Vector3(
                    sampledWorldPoint.x,
                    GetCurrentPosition().y +
                    roamRaycastHeight,
                    sampledWorldPoint.z
                );

            float safeRaycastDistance =
                GetSafeRoamRaycastDistance();

            if (!Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    safeRaycastDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore
                ))
            {
                continue;
            }

            Vector3 candidate =
                new Vector3(
                    hit.point.x,
                    GetCurrentPosition().y,
                    hit.point.z
                );

            if (!combatPlatform.ContainsWorldPoint(
                    candidate
                ))
            {
                continue;
            }

            float travelDistance =
                GetFlatDistance(
                    GetCurrentPosition(),
                    candidate
                );

            if (travelDistance <
                roamMinimumTravelDistance)
            {
                continue;
            }

            _roamTarget = candidate;
            return true;
        }

        return false;
    }

    private Vector3 SamplePointInsideBox(
        BoxCollider boxCollider
    )
    {
        Vector3 scale =
            boxCollider.transform.lossyScale;

        float safeScaleX =
            Mathf.Max(
                0.001f,
                Mathf.Abs(scale.x)
            );

        float safeScaleZ =
            Mathf.Max(
                0.001f,
                Mathf.Abs(scale.z)
            );

        float localPaddingX =
            roamEdgePadding /
            safeScaleX;

        float localPaddingZ =
            roamEdgePadding /
            safeScaleZ;

        Vector3 halfSize =
            boxCollider.size * 0.5f;

        float usableHalfX =
            Mathf.Max(
                0.1f,
                halfSize.x -
                localPaddingX
            );

        float usableHalfZ =
            Mathf.Max(
                0.1f,
                halfSize.z -
                localPaddingZ
            );

        Vector3 localPoint =
            boxCollider.center +
            new Vector3(
                Random.Range(
                    -usableHalfX,
                    usableHalfX
                ),
                0f,
                Random.Range(
                    -usableHalfZ,
                    usableHalfZ
                )
            );

        return boxCollider.transform.TransformPoint(
            localPoint
        );
    }

    private bool TryChooseFallbackRoamTarget()
    {
        for (int attempt = 0;
             attempt < roamPointAttempts;
             attempt++)
        {
            Vector2 randomCircle =
                Random.insideUnitCircle * 5f;

            Vector3 candidate =
                _spawnPosition +
                new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y
                );

            Vector3 rayOrigin =
                new Vector3(
                    candidate.x,
                    GetCurrentPosition().y +
                    roamRaycastHeight,
                    candidate.z
                );

            float safeRaycastDistance =
                GetSafeRoamRaycastDistance();

            if (!Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    safeRaycastDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore
                ))
            {
                continue;
            }

            _roamTarget =
                new Vector3(
                    hit.point.x,
                    GetCurrentPosition().y,
                    hit.point.z
                );

            return true;
        }

        return false;
    }

    private float GetSafeRoamRaycastDistance()
    {
        /*
         * 至少保证射线长度比起点高度多 5 米。
         * Inspector 设置为 Height=4、Distance=20 时，
         * 最终仍使用 20 米。
         */
        return Mathf.Max(
            roamRaycastDistance,
            roamRaycastHeight + 5f
        );
    }

    // =========================================================
    // 接触伤害反馈
    // =========================================================

    private void HandlePlayerDamaged(
        GameObject playerObject
    )
    {
        if (enemyTarget != null &&
            enemyTarget.IsDead)
        {
            return;
        }

        Transform playerTransform =
            playerObject != null
                ? playerObject.transform
                : _player;

        Vector3 retreatDirection;

        if (playerTransform != null)
        {
            retreatDirection =
                transform.position -
                playerTransform.position;
        }
        else
        {
            retreatDirection =
                -transform.forward;
        }

        retreatDirection.y = 0f;

        if (retreatDirection.sqrMagnitude <=
            0.001f)
        {
            retreatDirection =
                -transform.forward;
        }

        _retreatDirection =
            retreatDirection.normalized;

        currentState =
            OphanimState.Recovering;

        _stateTimer =
            Mathf.Max(
                0.05f,
                recoverDuration
            );

        _retreatTimer =
            Mathf.Clamp(
                retreatMoveDuration,
                0f,
                recoverDuration
            );

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(
                false
            );
        }
    }

    // =========================================================
    // 状态切换
    // =========================================================

    private void EnterIdle()
    {
        currentState =
            OphanimState.Idle;

        _currentMoveDirection =
            Vector3.zero;

        ResetIdleTimer();

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(true);
        }
    }

    private void EnterRoaming()
    {
        currentState =
            OphanimState.Roaming;

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(true);
        }
    }

    private void EnterTracking()
    {
        currentState =
            OphanimState.Tracking;

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(true);
        }
    }

    private void EnterOrbiting()
    {
        currentState =
            OphanimState.Orbiting;

        ResetOrbitDirectionTimer();

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(true);
        }
    }

    private void ResetIdleTimer()
    {
        _stateTimer =
            Random.Range(
                Mathf.Min(
                    idleDurationMinimum,
                    idleDurationMaximum
                ),
                Mathf.Max(
                    idleDurationMinimum,
                    idleDurationMaximum
                )
            );
    }

    private void ResetOrbitDirectionTimer()
    {
        _orbitDirectionTimer =
            Random.Range(
                Mathf.Min(
                    orbitDirectionChangeMinimum,
                    orbitDirectionChangeMaximum
                ),
                Mathf.Max(
                    orbitDirectionChangeMinimum,
                    orbitDirectionChangeMaximum
                )
            );
    }

    // =========================================================
    // 玩家检测
    // =========================================================

    private bool CanEngagePlayer()
    {
        if (_player == null)
        {
            return false;
        }

        if (combatPlatform != null)
        {
            return combatPlatform.IsPlayerInside;
        }

        return
            GetFlatDistanceToPlayer() <=
            detectionRange;
    }

    private void RefreshPlayerReference()
    {
        if (combatPlatform != null &&
            combatPlatform.Player != null)
        {
            _player =
                combatPlatform.Player;

            return;
        }

        if (_player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(
                    "Player"
                );

            if (playerObject != null)
            {
                _player =
                    playerObject.transform;
            }
        }
    }

    // =========================================================
    // 移动辅助
    // =========================================================

    private void RotateTowardDirection(
        Vector3 direction
    )
    {
        if (direction.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        Quaternion newRotation =
            Quaternion.RotateTowards(
                GetCurrentRotation(),
                targetRotation,
                turnSpeed *
                Time.fixedDeltaTime
            );

        if (enemyRigidbody != null &&
            enemyRigidbody.isKinematic)
        {
            enemyRigidbody.MoveRotation(
                newRotation
            );
        }
        else
        {
            transform.rotation =
                newRotation;
        }
    }

    private void MoveControlled(
        Vector3 targetPosition
    )
    {
        if (enemyRigidbody != null &&
            enemyRigidbody.isKinematic)
        {
            enemyRigidbody.MovePosition(
                targetPosition
            );
        }
        else
        {
            transform.position =
                targetPosition;
        }
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

    private Vector3 GetFlatDirectionToPlayer()
    {
        if (_player == null)
        {
            return Vector3.zero;
        }

        return GetFlatDirection(
            GetCurrentPosition(),
            _player.position
        );
    }

    private float GetFlatDistanceToPlayer()
    {
        if (_player == null)
        {
            return Mathf.Infinity;
        }

        return GetFlatDistance(
            GetCurrentPosition(),
            _player.position
        );
    }

    private static Vector3 GetFlatDirection(
        Vector3 from,
        Vector3 to
    )
    {
        Vector3 direction =
            to - from;

        direction.y = 0f;

        return direction.normalized;
    }

    private static float GetFlatDistance(
        Vector3 first,
        Vector3 second
    )
    {
        Vector3 difference =
            second - first;

        difference.y = 0f;

        return difference.magnitude;
    }

    // =========================================================
    // Gizmos
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            orbitEnterDistance
        );

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            orbitDistance
        );

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawSphere(
                _roamTarget,
                0.2f
            );

            Gizmos.DrawLine(
                transform.position,
                _roamTarget
            );
        }

        Transform probe =
            groundProbe != null
                ? groundProbe
                : transform;

        Vector3 direction =
            Application.isPlaying &&
            _currentMoveDirection.sqrMagnitude >
            0.001f
                ? _currentMoveDirection
                : transform.forward;

        Vector3 sideDirection =
            Vector3.Cross(
                Vector3.up,
                direction
            ).normalized;

        Vector3 forwardOffset =
            direction *
            edgeProbeForwardDistance;

        DrawGroundProbeGizmo(
            probe.position +
            forwardOffset,
            Color.green
        );

        DrawGroundProbeGizmo(
            probe.position +
            forwardOffset +
            sideDirection *
            sideProbeOffset,
            Color.blue
        );

        DrawGroundProbeGizmo(
            probe.position +
            forwardOffset -
            sideDirection *
            sideProbeOffset,
            Color.blue
        );
    }

    private void DrawGroundProbeGizmo(
        Vector3 probePosition,
        Color color
    )
    {
        Gizmos.color = color;

        Vector3 origin =
            probePosition +
            Vector3.up * 0.2f;

        Gizmos.DrawLine(
            origin,
            origin +
            Vector3.down *
            groundCheckDistance
        );

        Gizmos.DrawWireSphere(
            origin,
            0.08f
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        idleDurationMinimum =
            Mathf.Max(
                0f,
                idleDurationMinimum
            );

        idleDurationMaximum =
            Mathf.Max(
                idleDurationMinimum,
                idleDurationMaximum
            );

        roamingSpeed =
            Mathf.Max(
                0f,
                roamingSpeed
            );

        trackingSpeed =
            Mathf.Max(
                0f,
                trackingSpeed
            );

        orbitSpeed =
            Mathf.Max(
                0f,
                orbitSpeed
            );

        recoverDuration =
            Mathf.Max(
                0.05f,
                recoverDuration
            );

        retreatMoveDuration =
            Mathf.Clamp(
                retreatMoveDuration,
                0f,
                recoverDuration
            );

        groundCheckDistance =
            Mathf.Max(
                0.05f,
                groundCheckDistance
            );

        roamRaycastHeight =
            Mathf.Max(
                0.1f,
                roamRaycastHeight
            );

        roamRaycastDistance =
            Mathf.Max(
                0.1f,
                roamRaycastDistance
            );

        roamPointAttempts =
            Mathf.Max(
                1,
                roamPointAttempts
            );
    }
#endif
}
