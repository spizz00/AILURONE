#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
public class OphanimVisualFeedback : MonoBehaviour
{
    [Header("核心引用")]
    public OphanimEnemy ophanimEnemy;
    public EnemyTarget enemyTarget;

    [Tooltip("Enemy_Ophanim_Root 下的 VisualRoot。")]
    public Transform visualRoot;

    [Tooltip("VisualRoot 下的 FBX 模型根对象 OphanimEnemy。")]
    public Transform modelRoot;

    [Header("模型部件")]
    public Transform core;

    public Transform ring1;
    public Transform ring2;
    public Transform ring3;
    public Transform ring4;

    [Header("模型 Renderer")]
    public Renderer coreRenderer;

    [Tooltip("顺序建议：Ring1、Ring2、Ring3、Ring4。")]
    public Renderer[] ringRenderers;

    [Header("旋转参照点")]
    [Tooltip("四个 SpinMarker 上的 TrailRenderer。")]
    public TrailRenderer[] markerTrails;

    [Tooltip("四个 MarkerDot 上的 MeshRenderer。")]
    public Renderer[] markerRenderers;

    [Header("四环旋转轴")]
    public Vector3 ring1Axis = Vector3.right;
    public Vector3 ring2Axis = Vector3.up;

    public Vector3 ring3Axis =
        new Vector3(1f, 0.35f, 0.45f);

    public Vector3 ring4Axis = Vector3.forward;

    [Header("四环基础转速")]
    public float ring1BaseSpeed = 80f;
    public float ring2BaseSpeed = -115f;
    public float ring3BaseSpeed = 150f;
    public float ring4BaseSpeed = -210f;

    [Header("状态转速倍率")]
    public float idleSpinMultiplier = 0.45f;
    public float roamingSpinMultiplier = 0.85f;
    public float trackingSpinMultiplier = 3.5f;
    public float orbitingSpinMultiplier = 5f;

    [Tooltip("Recovering 时使用负数高速反转。")]
    public float recoveringSpinMultiplier = -4f;

    public float spinTransitionSpeed = 14f;

    [Range(0f, 1f)]
    public float engagementSpeedSnap = 0.8f;

    [Header("状态颜色")]
    public Color idleColor =
        new Color(0.1f, 0.75f, 0.08f, 1f);

    public Color roamingColor =
        new Color(0.35f, 1f, 0.05f, 1f);

    public Color trackingColor =
        new Color(0.85f, 1f, 0.02f, 1f);

    public Color orbitingColor =
        new Color(1f, 0.25f, 0.01f, 1f);

    public Color recoveringColor =
        new Color(0.2f, 0.95f, 1f, 1f);


    [Header("Emission 强度")]
    public float idleEmission = 0.25f;
    public float roamingEmission = 0.8f;
    public float trackingEmission = 4f;
    public float orbitingEmission = 7f;
    public float recoveringEmission = 6f;

    public float colorTransitionSpeed = 14f;

    [Header("核心运动")]
    public Vector3 coreRotationAxis =
        new Vector3(0.25f, 1f, 0.15f);

    public float coreBaseSpinSpeed = 45f;
    public float corePulseAmount = 0.09f;

    [Header("整体悬浮")]
    public float idleHoverAmplitude = 0.07f;
    public float movingHoverAmplitude = 0.12f;
    public float orbitHoverAmplitude = 0.18f;

    public float idleHoverFrequency = 1.6f;
    public float movingHoverFrequency = 2.8f;
    public float orbitHoverFrequency = 6f;

    [Header("攻击姿态")]
    [Tooltip("Tracking 时视觉模型朝玩家平移。")]
    public float trackingPressureOffset = 0.16f;

    [Tooltip("Orbiting 时视觉模型朝玩家明显压近。")]
    public float orbitPressureOffset = 0.38f;

    public float trackingLeanAngle = 12f;
    public float orbitLeanAngle = 23f;

    public float postureSmoothSpeed = 16f;

    [Header("Orbiting 圆环扩张")]
    public float orbitRingExpansion = 0.14f;
    public float orbitRingPulseAmount = 0.05f;
    public float orbitRingPulseSpeed = 11f;

    [Header("Recovering 强反馈")]
    [Tooltip("接触玩家后先停止旋转多久。")]
    public float recoveringFreezeDuration = 0.07f;

    [Tooltip("Recovering 视觉效果总时长。")]
    public float recoveringVisualDuration = 0.8f;

    public float recoveringKickDistance = 0.42f;
    public float recoveringWobbleAngle = 32f;
    public float recoveringWobbleFrequency = 18f;
    public float recoveringRingExpansion = 0.22f;

    [Header("非致命命中反馈")]
    public float hitReactionDuration = 0.2f;

    [Tooltip("命中时环瞬间向外撑开的幅度。")]
    public float hitRingExpansion = 0.22f;

    public float hitScalePunch = 0.18f;
    public float hitKickDistance = 0.16f;
    public float hitRotationPunch = 16f;

    [Range(0f, 1f)]
    public float hitBaseStrength = 0.32f;

    public float hitDamageMultiplier = 3f;

    [Header("Marker Trail")]
    public float trackingTrailTime = 0.10f;
    public float orbitingTrailTime = 0.18f;
    public float recoveringTrailTime = 0.22f;

    [Header("运行状态")]
    [SerializeField]
    private float currentSpinMultiplier;

    [SerializeField]
    private float recoveringTimer;

    [SerializeField]
    private float recoveringFreezeTimer;

    [SerializeField]
    private float hitTimer;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _propertyBlock;

    private Transform _player;

    private Vector3 _visualBasePosition;
    private Quaternion _visualBaseRotation;
    private Vector3 _visualBaseScale;

    private Vector3 _coreBaseScale;

    private Vector3[] _ringBaseScales =
        new Vector3[4];

    private Color _currentColor;
    private Color _currentEmission;

    private float _hitStrength;
    private Vector3 _hitDirectionLocal;

    private OphanimEnemy.OphanimState _previousState;
    private bool _stateInitialized;
    private bool _damageEventSubscribed;

    private void Awake()
    {
        ResolveReferences();

        _propertyBlock =
            new MaterialPropertyBlock();

        if (visualRoot != null)
        {
            _visualBasePosition =
                visualRoot.localPosition;

            _visualBaseRotation =
                visualRoot.localRotation;

            _visualBaseScale =
                visualRoot.localScale;
        }

        if (core != null)
        {
            _coreBaseScale =
                core.localScale;
        }

        StoreRingBaseScales();

        _currentColor = idleColor;

        _currentEmission =
            idleColor * idleEmission;

        currentSpinMultiplier =
            idleSpinMultiplier;
    }

    private void OnEnable()
    {
        SubscribeToDamageEvent();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToDamageEvent();
        RefreshPlayerReference();

        if (ophanimEnemy == null ||
            visualRoot == null)
        {
            Debug.LogError(
                $"[OphanimVisualFeedback] " +
                $"{gameObject.name} 缺少必要引用。"
            );

            enabled = false;
            return;
        }

        _previousState =
            ophanimEnemy.CurrentState;

        _stateInitialized = true;

        SetMarkerTrails(false, 0f);
    }

    private void Update()
    {
        if (ophanimEnemy == null ||
            visualRoot == null)
        {
            return;
        }

        RefreshPlayerReference();

        OphanimEnemy.OphanimState state =
            ophanimEnemy.CurrentState;

        if (!_stateInitialized ||
            state != _previousState)
        {
            HandleStateChanged(state);
        }

        float scaledDeltaTime =
            Time.deltaTime;

        float unscaledDeltaTime =
            Time.unscaledDeltaTime;

        UpdateTimers(unscaledDeltaTime);

        UpdateSpinMultiplier(
            state,
            scaledDeltaTime
        );

        RotateParts(
            state,
            scaledDeltaTime
        );

        UpdateVisualPosture(
            state,
            unscaledDeltaTime
        );

        UpdateRingScales(
            state,
            unscaledDeltaTime
        );

        UpdateCorePulse(
            state,
            unscaledDeltaTime
        );

        UpdateColors(
            state,
            unscaledDeltaTime
        );

        UpdateMarkerTrails(state);
    }

    // =========================================================
    // 状态切换
    // =========================================================

    private void HandleStateChanged(
        OphanimEnemy.OphanimState newState
    )
    {
        _previousState = newState;
        _stateInitialized = true;

        if (newState ==
            OphanimEnemy.OphanimState.Tracking)
        {
            currentSpinMultiplier =
                Mathf.Lerp(
                    currentSpinMultiplier,
                    trackingSpinMultiplier,
                    engagementSpeedSnap
                );

            ClearMarkerTrails();
        }
        else if (
            newState ==
            OphanimEnemy.OphanimState.Orbiting
        )
        {
            currentSpinMultiplier =
                Mathf.Lerp(
                    currentSpinMultiplier,
                    orbitingSpinMultiplier,
                    engagementSpeedSnap
                );

            ClearMarkerTrails();
        }
        else if (
            newState ==
            OphanimEnemy.OphanimState.Recovering
        )
        {
            recoveringTimer =
                recoveringVisualDuration;

            recoveringFreezeTimer =
                recoveringFreezeDuration;

            currentSpinMultiplier = 0f;

            ClearMarkerTrails();
        }
    }

    // =========================================================
    // 引用
    // =========================================================

    private void ResolveReferences()
    {
        if (ophanimEnemy == null)
        {
            ophanimEnemy =
                GetComponent<OphanimEnemy>();
        }

        if (enemyTarget == null)
        {
            enemyTarget =
                GetComponent<EnemyTarget>();
        }

        if (visualRoot == null)
        {
            visualRoot =
                FindChildRecursive(
                    transform,
                    "VisualRoot"
                );
        }

        if (modelRoot == null &&
            visualRoot != null)
        {
            modelRoot =
                FindChildRecursive(
                    visualRoot,
                    "OphanimEnemy"
                );
        }

        Transform searchRoot =
            modelRoot != null
                ? modelRoot
                : visualRoot;

        if (core == null)
        {
            core =
                FindChildRecursive(
                    searchRoot,
                    "Icosphere"
                );
        }

        if (ring1 == null)
        {
            ring1 =
                FindChildRecursive(
                    searchRoot,
                    "Ring1"
                );
        }

        if (ring2 == null)
        {
            ring2 =
                FindChildRecursive(
                    searchRoot,
                    "Ring2"
                );
        }

        if (ring3 == null)
        {
            ring3 =
                FindChildRecursive(
                    searchRoot,
                    "Ring3"
                );
        }

        if (ring4 == null)
        {
            ring4 =
                FindChildRecursive(
                    searchRoot,
                    "Ring4"
                );
        }

        if (coreRenderer == null &&
            core != null)
        {
            coreRenderer =
                core.GetComponent<Renderer>();
        }

        ResolveRingRenderers();
        ResolveMarkers();
    }

    private void ResolveRingRenderers()
    {
        if (ringRenderers != null &&
            ringRenderers.Length == 4)
        {
            return;
        }

        ringRenderers =
            new Renderer[4];

        ringRenderers[0] =
            ring1 != null
                ? ring1.GetComponent<Renderer>()
                : null;

        ringRenderers[1] =
            ring2 != null
                ? ring2.GetComponent<Renderer>()
                : null;

        ringRenderers[2] =
            ring3 != null
                ? ring3.GetComponent<Renderer>()
                : null;

        ringRenderers[3] =
            ring4 != null
                ? ring4.GetComponent<Renderer>()
                : null;
    }

    private void ResolveMarkers()
    {
        Transform[] rings =
        {
            ring1,
            ring2,
            ring3,
            ring4
        };

        if (markerTrails == null ||
            markerTrails.Length != 4)
        {
            markerTrails =
                new TrailRenderer[4];
        }

        if (markerRenderers == null ||
            markerRenderers.Length != 4)
        {
            markerRenderers =
                new Renderer[4];
        }

        for (int i = 0; i < 4; i++)
        {
            if (rings[i] == null)
            {
                continue;
            }

            Transform marker =
                FindChildRecursive(
                    rings[i],
                    "SpinMarker"
                );

            if (marker == null)
            {
                continue;
            }

            markerTrails[i] =
                marker.GetComponent<TrailRenderer>();

            Transform markerDot =
                FindChildRecursive(
                    marker,
                    "MarkerDot"
                );

            if (markerDot != null)
            {
                markerRenderers[i] =
                    markerDot.GetComponent<Renderer>();
            }
        }
    }

    private Transform FindChildRecursive(
        Transform parent,
        string targetName
    )
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == targetName)
            {
                return child;
            }

            Transform result =
                FindChildRecursive(
                    child,
                    targetName
                );

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void StoreRingBaseScales()
    {
        Transform[] rings =
        {
            ring1,
            ring2,
            ring3,
            ring4
        };

        for (int i = 0; i < rings.Length; i++)
        {
            _ringBaseScales[i] =
                rings[i] != null
                    ? rings[i].localScale
                    : Vector3.one;
        }
    }

    private void RefreshPlayerReference()
    {
        if (ophanimEnemy != null &&
            ophanimEnemy.combatPlatform != null &&
            ophanimEnemy.combatPlatform.Player != null)
        {
            _player =
                ophanimEnemy.combatPlatform.Player;

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
    // 旋转
    // =========================================================

    private void UpdateSpinMultiplier(
        OphanimEnemy.OphanimState state,
        float deltaTime
    )
    {
        if (recoveringFreezeTimer > 0f)
        {
            currentSpinMultiplier = 0f;
            return;
        }

        float targetMultiplier;

        if (recoveringTimer > 0f)
        {
            targetMultiplier =
                recoveringSpinMultiplier;
        }
        else
        {
            targetMultiplier =
                GetStateSpinMultiplier(state);
        }

        float interpolation =
            1f -
            Mathf.Exp(
                -spinTransitionSpeed *
                deltaTime
            );

        currentSpinMultiplier =
            Mathf.Lerp(
                currentSpinMultiplier,
                targetMultiplier,
                interpolation
            );
    }

    private float GetStateSpinMultiplier(
        OphanimEnemy.OphanimState state
    )
    {
        switch (state)
        {
            case OphanimEnemy.OphanimState.Idle:
                return idleSpinMultiplier;

            case OphanimEnemy.OphanimState.Roaming:
                return roamingSpinMultiplier;

            case OphanimEnemy.OphanimState.Tracking:
                return trackingSpinMultiplier;

            case OphanimEnemy.OphanimState.Orbiting:
                return orbitingSpinMultiplier;

            case OphanimEnemy.OphanimState.Recovering:
                return recoveringSpinMultiplier;

            case OphanimEnemy.OphanimState.Stunned:
                return 0f;

            default:
                return idleSpinMultiplier;
        }
    }

    private void RotateParts(
        OphanimEnemy.OphanimState state,
        float deltaTime
    )
    {
        RotatePart(
            ring1,
            ring1Axis,
            ring1BaseSpeed,
            deltaTime
        );

        RotatePart(
            ring2,
            ring2Axis,
            ring2BaseSpeed,
            deltaTime
        );

        RotatePart(
            ring3,
            ring3Axis,
            ring3BaseSpeed,
            deltaTime
        );

        RotatePart(
            ring4,
            ring4Axis,
            ring4BaseSpeed,
            deltaTime
        );

        if (core == null)
        {
            return;
        }

        float coreMultiplier =
            Mathf.Abs(
                currentSpinMultiplier
            );

        Vector3 axis =
            coreRotationAxis.sqrMagnitude >
            0.001f
                ? coreRotationAxis.normalized
                : Vector3.up;

        core.Rotate(
            axis,
            coreBaseSpinSpeed *
            coreMultiplier *
            deltaTime,
            Space.Self
        );
    }

    private void RotatePart(
        Transform part,
        Vector3 axis,
        float speed,
        float deltaTime
    )
    {
        if (part == null)
        {
            return;
        }

        Vector3 safeAxis =
            axis.sqrMagnitude > 0.001f
                ? axis.normalized
                : Vector3.up;

        part.Rotate(
            safeAxis,
            speed *
            currentSpinMultiplier *
            deltaTime,
            Space.Self
        );
    }

    // =========================================================
    // 姿态
    // =========================================================

    private void UpdateVisualPosture(
        OphanimEnemy.OphanimState state,
        float deltaTime
    )
    {
        float hoverAmplitude =
            idleHoverAmplitude;

        float hoverFrequency =
            idleHoverFrequency;

        if (state ==
            OphanimEnemy.OphanimState.Roaming ||
            state ==
            OphanimEnemy.OphanimState.Tracking)
        {
            hoverAmplitude =
                movingHoverAmplitude;

            hoverFrequency =
                movingHoverFrequency;
        }
        else if (
            state ==
            OphanimEnemy.OphanimState.Orbiting
        )
        {
            hoverAmplitude =
                orbitHoverAmplitude;

            hoverFrequency =
                orbitHoverFrequency;
        }

        Vector3 targetPosition =
            _visualBasePosition +
            Vector3.up *
            (
                Mathf.Sin(
                    Time.time *
                    hoverFrequency
                ) *
                hoverAmplitude
            );

        Quaternion targetRotation =
            _visualBaseRotation;

        Vector3 toPlayerWorld =
            GetFlatDirectionToPlayer();

        Vector3 toPlayerLocal =
            transform.InverseTransformDirection(
                toPlayerWorld
            );

        if (state ==
            OphanimEnemy.OphanimState.Tracking)
        {
            targetPosition +=
                toPlayerLocal *
                trackingPressureOffset;

            targetRotation *=
                CreateLeanRotation(
                    toPlayerLocal,
                    trackingLeanAngle
                );
        }
        else if (
            state ==
            OphanimEnemy.OphanimState.Orbiting
        )
        {
            float pressurePulse =
                0.8f +
                Mathf.Sin(
                    Time.unscaledTime * 9f
                ) *
                0.2f;

            targetPosition +=
                toPlayerLocal *
                orbitPressureOffset *
                pressurePulse;

            targetRotation *=
                CreateLeanRotation(
                    toPlayerLocal,
                    orbitLeanAngle
                );
        }

        if (recoveringTimer > 0f)
        {
            float strength =
                Mathf.Clamp01(
                    recoveringTimer /
                    Mathf.Max(
                        0.01f,
                        recoveringVisualDuration
                    )
                );

            targetPosition -=
                toPlayerLocal *
                recoveringKickDistance *
                strength;

            float wobble =
                Mathf.Sin(
                    Time.unscaledTime *
                    recoveringWobbleFrequency
                ) *
                recoveringWobbleAngle *
                strength;

            targetRotation *=
                Quaternion.Euler(
                    wobble * 0.35f,
                    wobble * 0.15f,
                    wobble
                );
        }

        Vector3 targetScale =
            _visualBaseScale;

        if (hitTimer > 0f)
        {
            float envelope =
                GetHitEnvelope();

            targetPosition +=
                _hitDirectionLocal *
                hitKickDistance *
                envelope *
                _hitStrength;

            targetScale *=
                1f +
                hitScalePunch *
                envelope *
                _hitStrength;

            float shake =
                Mathf.Sin(
                    Time.unscaledTime * 75f
                );

            targetRotation *=
                Quaternion.Euler(
                    shake *
                    hitRotationPunch *
                    envelope *
                    _hitStrength,
                    0f,
                    -shake *
                    hitRotationPunch *
                    envelope *
                    _hitStrength
                );
        }

        float interpolation =
            1f -
            Mathf.Exp(
                -postureSmoothSpeed *
                deltaTime
            );

        visualRoot.localPosition =
            Vector3.Lerp(
                visualRoot.localPosition,
                targetPosition,
                interpolation
            );

        visualRoot.localRotation =
            Quaternion.Slerp(
                visualRoot.localRotation,
                targetRotation,
                interpolation
            );

        visualRoot.localScale =
            Vector3.Lerp(
                visualRoot.localScale,
                targetScale,
                interpolation
            );
    }

    private Quaternion CreateLeanRotation(
        Vector3 localDirection,
        float angle
    )
    {
        return Quaternion.Euler(
            -localDirection.z * angle,
            0f,
            -localDirection.x * angle
        );
    }

    private Vector3 GetFlatDirectionToPlayer()
    {
        if (_player == null)
        {
            return transform.forward;
        }

        Vector3 direction =
            _player.position -
            transform.position;

        direction.y = 0f;

        return direction.sqrMagnitude >
               0.001f
            ? direction.normalized
            : transform.forward;
    }

    // =========================================================
    // 环缩放
    // =========================================================

    private void UpdateRingScales(
        OphanimEnemy.OphanimState state,
        float deltaTime
    )
    {
        Transform[] rings =
        {
            ring1,
            ring2,
            ring3,
            ring4
        };

        float stateExpansion = 0f;

        if (state ==
            OphanimEnemy.OphanimState.Orbiting)
        {
            stateExpansion =
                orbitRingExpansion +
                Mathf.Sin(
                    Time.unscaledTime *
                    orbitRingPulseSpeed
                ) *
                orbitRingPulseAmount;
        }

        if (recoveringTimer > 0f)
        {
            float recoveryStrength =
                Mathf.Clamp01(
                    recoveringTimer /
                    recoveringVisualDuration
                );

            stateExpansion +=
                recoveringRingExpansion *
                recoveryStrength;
        }

        if (hitTimer > 0f)
        {
            stateExpansion +=
                hitRingExpansion *
                GetHitEnvelope() *
                _hitStrength;
        }

        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] == null)
            {
                continue;
            }

            float ringVariation =
                1f +
                i * 0.12f;

            Vector3 targetScale =
                _ringBaseScales[i] *
                (
                    1f +
                    stateExpansion *
                    ringVariation
                );

            float interpolation =
                1f -
                Mathf.Exp(
                    -18f *
                    deltaTime
                );

            rings[i].localScale =
                Vector3.Lerp(
                    rings[i].localScale,
                    targetScale,
                    interpolation
                );
        }
    }

    // =========================================================
    // 核心脉冲
    // =========================================================

    private void UpdateCorePulse(
        OphanimEnemy.OphanimState state,
        float deltaTime
    )
    {
        if (core == null)
        {
            return;
        }

        float speed =
            state ==
            OphanimEnemy.OphanimState.Orbiting
                ? 12f
                : state ==
                  OphanimEnemy.OphanimState.Tracking
                    ? 8f
                    : 2.5f;

        float amount =
            state ==
            OphanimEnemy.OphanimState.Orbiting
                ? corePulseAmount * 1.6f
                : corePulseAmount;

        float pulse =
            1f +
            Mathf.Sin(
                Time.unscaledTime *
                speed
            ) *
            amount;

        core.localScale =
            Vector3.Lerp(
                core.localScale,
                _coreBaseScale * pulse,
                1f -
                Mathf.Exp(
                    -16f *
                    deltaTime
                )
            );
    }

    // =========================================================
    // 颜色和发光
    // =========================================================

    private void UpdateColors(
        OphanimEnemy.OphanimState state,
        float deltaTime
    )
    {
        Color targetColor;
        float emission;

        GetStateColor(
            state,
            out targetColor,
            out emission
        );

        if (recoveringTimer > 0f)
        {
            targetColor =
                recoveringColor;

            emission =
                recoveringEmission;
        }

        float interpolation =
            1f -
            Mathf.Exp(
                -colorTransitionSpeed *
                deltaTime
            );

        _currentColor =
            Color.Lerp(
                _currentColor,
                targetColor,
                interpolation
            );

        _currentEmission =
            Color.Lerp(
                _currentEmission,
                targetColor * emission,
                interpolation
            );

        ApplyRendererColor(
            coreRenderer,
            _currentColor,
            _currentEmission
        );

        if (ringRenderers != null)
        {
            for (int i = 0;
                 i < ringRenderers.Length;
                 i++)
            {
                Color ringColor =
                    Color.Lerp(
                        _currentColor,
                        Color.white,
                        i * 0.06f
                    );

                ApplyRendererColor(
                    ringRenderers[i],
                    ringColor,
                    ringColor * emission
                );
            }
        }

        if (markerRenderers != null)
        {
            foreach (Renderer markerRenderer
                     in markerRenderers)
            {
                ApplyRendererColor(
                    markerRenderer,
                    targetColor,
                    targetColor *
                    Mathf.Max(
                        5f,
                        emission
                    )
                );
            }
        }
    }

    private void GetStateColor(
        OphanimEnemy.OphanimState state,
        out Color color,
        out float emission
    )
    {
        switch (state)
        {
            case OphanimEnemy.OphanimState.Roaming:
                color = roamingColor;
                emission = roamingEmission;
                break;

            case OphanimEnemy.OphanimState.Tracking:
                color = trackingColor;
                emission = trackingEmission;
                break;

            case OphanimEnemy.OphanimState.Orbiting:
                color = orbitingColor;
                emission = orbitingEmission;
                break;

            case OphanimEnemy.OphanimState.Recovering:
                color = recoveringColor;
                emission = recoveringEmission;
                break;

            default:
                color = idleColor;
                emission = idleEmission;
                break;
        }
    }

    private void ApplyRendererColor(
        Renderer targetRenderer,
        Color baseColor,
        Color emissionColor
    )
    {
        if (targetRenderer == null)
        {
            return;
        }

        _propertyBlock.Clear();

        targetRenderer.GetPropertyBlock(
            _propertyBlock
        );

        _propertyBlock.SetColor(
            BaseColorId,
            baseColor
        );

        _propertyBlock.SetColor(
            ColorId,
            baseColor
        );

        _propertyBlock.SetColor(
            EmissionColorId,
            emissionColor
        );

        targetRenderer.SetPropertyBlock(
            _propertyBlock
        );
    }

    // =========================================================
    // Marker Trails
    // =========================================================

    private void UpdateMarkerTrails(
        OphanimEnemy.OphanimState state
    )
    {
        bool enableTrails =
            state ==
                OphanimEnemy.OphanimState.Tracking ||
            state ==
                OphanimEnemy.OphanimState.Orbiting ||
            recoveringTimer > 0f;

        float selectedTime =
            state ==
            OphanimEnemy.OphanimState.Orbiting
                ? orbitingTrailTime
                : recoveringTimer > 0f
                    ? recoveringTrailTime
                    : trackingTrailTime;

        SetMarkerTrails(
            enableTrails,
            selectedTime
        );

        if (markerTrails == null)
        {
            return;
        }

        foreach (TrailRenderer trail
                 in markerTrails)
        {
            if (trail == null)
            {
                continue;
            }

            Color startColor =
                _currentColor;

            startColor.a = 0.9f;

            Color endColor =
                _currentColor;

            endColor.a = 0f;

            trail.startColor =
                startColor;

            trail.endColor =
                endColor;
        }
    }

    private void SetMarkerTrails(
        bool enabledState,
        float trailTime
    )
    {
        if (markerTrails == null)
        {
            return;
        }

        foreach (TrailRenderer trail
                 in markerTrails)
        {
            if (trail == null)
            {
                continue;
            }

            trail.time =
                Mathf.Max(
                    0.01f,
                    trailTime
                );

            trail.emitting =
                enabledState;
        }
    }

    private void ClearMarkerTrails()
    {
        if (markerTrails == null)
        {
            return;
        }

        foreach (TrailRenderer trail
                 in markerTrails)
        {
            if (trail != null)
            {
                trail.Clear();
            }
        }
    }

    // =========================================================
    // 伤害事件
    // =========================================================

    private void SubscribeToDamageEvent()
    {
        if (_damageEventSubscribed)
        {
            return;
        }

        if (enemyTarget == null)
        {
            enemyTarget =
                GetComponent<EnemyTarget>();
        }

        if (enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged +=
            HandleEnemyDamaged;

        _damageEventSubscribed = true;
    }

    private void UnsubscribeFromDamageEvent()
    {
        if (!_damageEventSubscribed)
        {
            return;
        }

        if (enemyTarget != null)
        {
            enemyTarget.Damaged -=
                HandleEnemyDamaged;
        }

        _damageEventSubscribed = false;
    }

    private void HandleEnemyDamaged(
        float actualDamage,
        float remainingHealth,
        Vector3 hitPoint
    )
    {
        if (actualDamage <= 0f)
        {
            return;
        }

        float damageRatio =
            enemyTarget != null &&
            enemyTarget.MaxHealth > 0f
                ? actualDamage /
                  enemyTarget.MaxHealth
                : 0f;

        float addedStrength =
            hitBaseStrength +
            damageRatio *
            hitDamageMultiplier;

        _hitStrength =
            Mathf.Clamp01(
                _hitStrength +
                addedStrength
            );

        hitTimer =
            Mathf.Max(
                hitTimer,
                hitReactionDuration
            );

        Vector3 localPoint =
            visualRoot.InverseTransformPoint(
                hitPoint
            );

        if (localPoint.sqrMagnitude <
            0.001f)
        {
            localPoint =
                Vector3.back;
        }

        _hitDirectionLocal =
            -localPoint.normalized;
    }

    private float GetHitEnvelope()
    {
        if (hitTimer <= 0f)
        {
            return 0f;
        }

        float normalized =
            Mathf.Clamp01(
                hitTimer /
                Mathf.Max(
                    0.01f,
                    hitReactionDuration
                )
            );

        return normalized * normalized;
    }

    private void UpdateTimers(
        float deltaTime
    )
    {
        if (recoveringTimer > 0f)
        {
            recoveringTimer -= deltaTime;

            if (recoveringTimer < 0f)
            {
                recoveringTimer = 0f;
            }
        }

        if (recoveringFreezeTimer > 0f)
        {
            recoveringFreezeTimer -=
                deltaTime;

            if (recoveringFreezeTimer < 0f)
            {
                recoveringFreezeTimer = 0f;
            }
        }

        if (hitTimer > 0f)
        {
            hitTimer -= deltaTime;

            if (hitTimer <= 0f)
            {
                hitTimer = 0f;
                _hitStrength = 0f;
            }
        }
    }

    // =========================================================
    // 生命周期
    // =========================================================

    private void OnDisable()
    {
        UnsubscribeFromDamageEvent();
        SetMarkerTrails(false, 0.1f);
        ClearMarkerTrails();

        if (visualRoot != null)
        {
            visualRoot.localPosition =
                _visualBasePosition;

            visualRoot.localRotation =
                _visualBaseRotation;

            visualRoot.localScale =
                _visualBaseScale;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromDamageEvent();
    }
}
