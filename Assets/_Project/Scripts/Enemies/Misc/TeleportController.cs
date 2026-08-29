#pragma warning disable 0618
#pragma warning disable 0414
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class TeleportController : MonoBehaviour
{
    public enum TeleportCancelReason
    {
        None,
        ManualInput,
        PlayerDamaged,
        PlayerRewind,
        CannonCommit,
        AnchorExpired,
        InvalidDestination,
        ComponentDisabled,
        GameplayActionsLocked
    }

    [Header("核心引用")]
    public TeleportAnchorSystem anchorSystem;
    public FirstPersonController firstPersonController;
    public PlayerHealth playerHealth;
    public AlwaysEquippedWeaponController weaponController;
    public DashController dashController;
    public CharacterController characterController;
    public TeleportScreenFX screenFX;

    [Header("引导规则")]
    [Min(0.05f)]
    [Tooltip("确认锚点后，自动引导的真实时间。")]
    public float channelDuration = 0.5f;

    [Range(0.1f, 1f)]
    [Tooltip("引导期间只削弱玩家主动 WASD 位移；外部速度与跳跃不受影响。")]
    public float activeMovementMultiplier = 0.7f;

    [Tooltip("松开确认用的 E 后，再次按 E 可以取消引导。")]
    public bool allowInteractKeyCancel = true;

    [Tooltip("受到有效伤害时取消。")]
    public bool cancelOnDamage = true;

    [Tooltip("进入 Gravity Cannon Commit 时取消。")]
    public bool cancelOnCannonCommit = true;

    [Tooltip("玩家开始死亡回溯时取消。")]
    public bool cancelOnRewind = true;

    [Min(0f)]
    [Tooltip(
        "0.5 秒引导完成后，保留一个极短的屏幕闪烁时间，" +
        "再执行位置切换。"
    )]
    public float visualDepartureLeadTime = 0.04f;

    [Header("传送速度")]
    [Range(0f, 1f)]
    [Tooltip("向上速度保留比例；向下速度固定清零。")]
    public float upwardVelocityRetention = 0.5f;

    [Tooltip("成功传送不会直接恢复跳跃次数。")]
    public bool preventImmediateJumpRecharge = true;

    [Header("安全落点")]
    [Tooltip("只选择 Environment。留空时使用 FirstPersonController.GroundLayers。")]
    public LayerMask environmentMask;

    [Min(0f)]
    [Tooltip("只有地面距离目标点不超过该值时，才轻微贴地。飞行敌人的空中锚点不会被拉到地面。")]
    public float maximumGroundSnapDistance = 0.75f;

    [Min(0f)]
    public float groundClearance = 0.06f;

    [Min(0.1f)]
    public float maximumUpwardCorrection = 1.2f;

    [Min(0.05f)]
    public float correctionStep = 0.15f;

    [Range(0f, 1f)]
    public float radialCorrectionDistance = 0.35f;

    [Header("到达保护")]
    [Min(0f)]
    [Tooltip("只屏蔽普通 PlayerHealth.TakeDamage；KillZone 会绕过。")]
    public float arrivalDamageInvulnerability = 0.25f;

    [Header("可选 FOV")]
    public bool useChannelFOV = false;
    public float channelFOV = 102f;
    public float channelFOVSpeed = 16f;
    public float arrivalFOV = 112f;
    public float arrivalFOVSpeed = 24f;
    public float arrivalFOVDuration = 0.14f;

    [Header("调试")]
    public bool logStateChanges = true;

    [SerializeField]
    private bool isChanneling;

    [SerializeField, Range(0f, 1f)]
    private float channelProgress;

    [SerializeField]
    private int activeSlotIndex = -1;

    [SerializeField]
    private TeleportCancelReason lastCancelReason =
        TeleportCancelReason.None;

    public bool IsChanneling => isChanneling;
    public float ChannelProgress => channelProgress;
    public int ActiveSlotIndex => activeSlotIndex;
    public TeleportCancelReason LastCancelReason => lastCancelReason;

    public event Action<int, Vector3> ChannelStarted;
    public event Action<TeleportCancelReason> ChannelCancelled;
    public event Action<int, Vector3> TeleportCompleted;

    private Coroutine _channelRoutine;
    private Vector3 _activeDestination;
    private bool _waitForInteractRelease;

    private bool _savedAllowShooting;
    private bool _savedDashEnabled;
    private bool _hasSavedControlState;

    private void Awake()
    {
        ResolveReferences();
        ValidateValues();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void Start()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (isChanneling)
        {
            CancelChannel(
                TeleportCancelReason.ComponentDisabled
            );
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!isChanneling)
        {
            return;
        }

        if (AILURONEGameplayActionGate.IsPaused)
        {
            return;
        }

        if (!AILURONEGameplayActionGate.AllowsGameplayActions)
        {
            CancelChannel(
                TeleportCancelReason.GameplayActionsLocked
            );

            return;
        }

        if (cancelOnCannonCommit &&
            firstPersonController != null &&
            firstPersonController.IsCannonControlled())
        {
            CancelChannel(
                TeleportCancelReason.CannonCommit
            );

            return;
        }

        if (cancelOnRewind &&
            playerHealth != null &&
            playerHealth.IsRewinding)
        {
            CancelChannel(
                TeleportCancelReason.PlayerRewind
            );

            return;
        }

        HandleManualCancelInput();
    }

    private void ResolveReferences()
    {
        if (anchorSystem == null)
        {
            anchorSystem =
                GetComponent<TeleportAnchorSystem>();
        }

        if (firstPersonController == null)
        {
            firstPersonController =
                GetComponent<FirstPersonController>();
        }

        if (playerHealth == null)
        {
            playerHealth =
                GetComponent<PlayerHealth>();
        }

        if (weaponController == null)
        {
            weaponController =
                GetComponent<AlwaysEquippedWeaponController>();
        }

        if (dashController == null)
        {
            dashController =
                GetComponent<DashController>();
        }

        if (characterController == null)
        {
            characterController =
                GetComponent<CharacterController>();
        }

        if (screenFX == null)
        {
            screenFX =
                GetComponent<TeleportScreenFX>();
        }

        if (screenFX == null &&
            Application.isPlaying)
        {
            screenFX =
                gameObject.AddComponent<TeleportScreenFX>();
        }

        if (Application.isPlaying &&
            GetComponent<TeleportAudioFeedback>() == null)
        {
            gameObject.AddComponent<TeleportAudioFeedback>();
        }
    }

    private void Subscribe()
    {
        if (anchorSystem != null)
        {
            anchorSystem.AnchorConfirmed -=
                HandleAnchorConfirmed;

            anchorSystem.AnchorConfirmed +=
                HandleAnchorConfirmed;
        }

        if (firstPersonController != null)
        {
            firstPersonController.CannonControlStarted -=
                HandleCannonControlStarted;

            firstPersonController.CannonControlStarted +=
                HandleCannonControlStarted;
        }

        if (playerHealth != null)
        {
            playerHealth.Damaged -=
                HandlePlayerDamaged;

            playerHealth.Damaged +=
                HandlePlayerDamaged;

            playerHealth.RewindStarted -=
                HandleRewindStarted;

            playerHealth.RewindStarted +=
                HandleRewindStarted;
        }
    }

    private void Unsubscribe()
    {
        if (anchorSystem != null)
        {
            anchorSystem.AnchorConfirmed -=
                HandleAnchorConfirmed;
        }

        if (firstPersonController != null)
        {
            firstPersonController.CannonControlStarted -=
                HandleCannonControlStarted;
        }

        if (playerHealth != null)
        {
            playerHealth.Damaged -=
                HandlePlayerDamaged;

            playerHealth.RewindStarted -=
                HandleRewindStarted;
        }
    }

    private void HandleAnchorConfirmed(
        int slotIndex,
        Vector3 destination
    )
    {
        if (!AILURONEGameplayActionGate.AllowsGameplayActions ||
            isChanneling)
        {
            return;
        }

        if (firstPersonController == null ||
            characterController == null ||
            anchorSystem == null)
        {
            return;
        }

        if (firstPersonController.IsCannonControlled() ||
            playerHealth != null &&
            playerHealth.IsRewinding)
        {
            return;
        }

        _channelRoutine =
            StartCoroutine(
                ChannelRoutine(
                    slotIndex,
                    destination
                )
            );
    }

    private IEnumerator ChannelRoutine(
        int slotIndex,
        Vector3 destination
    )
    {
        BeginChannelState(
            slotIndex,
            destination
        );

        float elapsed = 0f;
        float safeDuration =
            Mathf.Max(
                0.01f,
                channelDuration
            );

        while (elapsed < safeDuration)
        {
            if (!isChanneling)
            {
                yield break;
            }

            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            if (!TryRefreshActiveDestination())
            {
                CancelChannel(
                    TeleportCancelReason.AnchorExpired
                );

                yield break;
            }

            elapsed +=
                Time.unscaledDeltaTime;

            channelProgress =
                Mathf.Clamp01(
                    elapsed /
                    safeDuration
                );

            anchorSystem.SetTeleportChannelProgress(
                activeSlotIndex,
                channelProgress
            );

            if (screenFX != null)
            {
                screenFX.SetChannelProgress(
                    channelProgress
                );
            }

            yield return null;
        }

        if (!isChanneling)
        {
            yield break;
        }

        if (!TryRefreshActiveDestination())
        {
            CancelChannel(
                TeleportCancelReason.AnchorExpired
            );

            yield break;
        }

        float visualLead =
            Mathf.Max(
                0f,
                visualDepartureLeadTime
            );

        if (screenFX != null)
        {
            screenFX.BeginDepartureFlash(
                visualLead
            );
        }

        float leadElapsed = 0f;

        while (leadElapsed < visualLead)
        {
            if (!isChanneling)
            {
                yield break;
            }

            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            if (!TryRefreshActiveDestination())
            {
                CancelChannel(
                    TeleportCancelReason.AnchorExpired
                );

                yield break;
            }

            leadElapsed +=
                Time.unscaledDeltaTime;

            yield return null;
        }

        CompleteTeleport();
    }

    private void BeginChannelState(
        int slotIndex,
        Vector3 destination
    )
    {
        isChanneling = true;
        channelProgress = 0f;
        activeSlotIndex = slotIndex;
        _activeDestination = destination;
        lastCancelReason =
            TeleportCancelReason.None;

        _waitForInteractRelease = true;

        SaveAndApplyControlState();

        anchorSystem.SetTeleportChannelProgress(
            activeSlotIndex,
            0f
        );

        if (screenFX != null)
        {
            screenFX.BeginChannel();
        }

        if (useChannelFOV &&
            firstPersonController != null)
        {
            firstPersonController.RequestExternalFOV(
                channelFOV,
                channelFOVSpeed,
                0f
            );
        }

        ChannelStarted?.Invoke(
            activeSlotIndex,
            _activeDestination
        );

        if (logStateChanges)
        {
            Debug.Log(
                $"[TeleportController] 开始引导锚点 " +
                $"{GetSlotLabel(activeSlotIndex)}。"
            );
        }
    }

    private void SaveAndApplyControlState()
    {
        if (!_hasSavedControlState)
        {
            _savedAllowShooting =
                weaponController == null ||
                weaponController.allowShooting;

            _savedDashEnabled =
                dashController != null &&
                dashController.enabled;

            _hasSavedControlState = true;
        }

        if (weaponController != null)
        {
            weaponController.allowShooting = false;
        }

        if (dashController != null)
        {
            dashController.enabled = false;
        }

        if (firstPersonController != null)
        {
            firstPersonController.SetTeleportChannelState(
                true,
                activeMovementMultiplier
            );
        }
    }

    private void RestoreControlState()
    {
        if (firstPersonController != null)
        {
            firstPersonController.SetTeleportChannelState(
                false,
                1f
            );
        }

        if (_hasSavedControlState)
        {
            if (weaponController != null)
            {
                weaponController.allowShooting =
                    _savedAllowShooting;
            }

            if (dashController != null)
            {
                dashController.enabled =
                    _savedDashEnabled;
            }
        }

        _hasSavedControlState = false;
    }

    private bool TryRefreshActiveDestination()
    {
        if (anchorSystem == null ||
            !anchorSystem.TryGetAnchorDestination(
                activeSlotIndex,
                out Vector3 currentDestination
            ))
        {
            return false;
        }

        if (Vector3.Distance(
                currentDestination,
                _activeDestination
            ) > 0.08f)
        {
            return false;
        }

        _activeDestination =
            currentDestination;

        return true;
    }

    private void HandleManualCancelInput()
    {
        if (!allowInteractKeyCancel ||
            Keyboard.current == null)
        {
            return;
        }

        bool held =
            Keyboard.current.eKey.isPressed;

        if (_waitForInteractRelease)
        {
            if (!held)
            {
                _waitForInteractRelease = false;
            }

            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            CancelChannel(
                TeleportCancelReason.ManualInput
            );
        }
    }

    private void HandleCannonControlStarted()
    {
        if (!isChanneling ||
            !cancelOnCannonCommit)
        {
            return;
        }

        CancelChannel(
            TeleportCancelReason.CannonCommit
        );
    }

    private void HandlePlayerDamaged(
        float actualDamage,
        float remainingHealth
    )
    {
        if (!isChanneling ||
            !cancelOnDamage ||
            actualDamage <= 0f)
        {
            return;
        }

        CancelChannel(
            TeleportCancelReason.PlayerDamaged
        );
    }

    private void HandleRewindStarted()
    {
        if (!isChanneling ||
            !cancelOnRewind)
        {
            return;
        }

        CancelChannel(
            TeleportCancelReason.PlayerRewind
        );
    }

    public void CancelChannel(
        TeleportCancelReason reason
    )
    {
        if (!isChanneling)
        {
            return;
        }

        isChanneling = false;
        channelProgress = 0f;
        lastCancelReason = reason;

        if (_channelRoutine != null)
        {
            StopCoroutine(_channelRoutine);
            _channelRoutine = null;
        }

        if (anchorSystem != null)
        {
            anchorSystem.ClearTeleportChannelProgress();
        }

        if (screenFX != null)
        {
            screenFX.CancelChannel();
        }

        RestoreControlState();

        if (useChannelFOV &&
            firstPersonController != null)
        {
            firstPersonController.ReleaseExternalFOV();
        }

        activeSlotIndex = -1;
        _waitForInteractRelease = false;

        ChannelCancelled?.Invoke(reason);

        if (logStateChanges)
        {
            Debug.Log(
                $"[TeleportController] 引导取消：{reason}。"
            );
        }
    }

    private void CompleteTeleport()
    {
        Vector3 preservedVelocity =
            firstPersonController != null
                ? firstPersonController
                    .GetTeleportPreservedVelocity()
                : Vector3.zero;

        if (!TryFindSafeDestination(
                _activeDestination,
                out Vector3 safeDestination
            ))
        {
            CancelChannel(
                TeleportCancelReason.InvalidDestination
            );

            return;
        }

        int completedSlot =
            activeSlotIndex;

        bool controllerWasEnabled =
            characterController != null &&
            characterController.enabled;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.position =
            safeDestination;

        Physics.SyncTransforms();

        if (characterController != null)
        {
            characterController.enabled =
                controllerWasEnabled;
        }

        if (firstPersonController != null)
        {
            firstPersonController
                .ApplyTeleportArrivalVelocity(
                    preservedVelocity,
                    upwardVelocityRetention,
                    preventImmediateJumpRecharge
                );
        }

        if (playerHealth != null)
        {
            playerHealth
                .GrantTemporaryDamageInvulnerability(
                    arrivalDamageInvulnerability
                );
        }

        if (anchorSystem != null)
        {
            anchorSystem.ClearTeleportChannelProgress();
            anchorSystem.ConsumeAnchor(completedSlot);
        }

        if (screenFX != null)
        {
            screenFX.PlayArrivalBurst();
        }

        isChanneling = false;
        channelProgress = 1f;
        _channelRoutine = null;

        RestoreControlState();

        if (useChannelFOV &&
            firstPersonController != null)
        {
            firstPersonController.RequestExternalFOV(
                arrivalFOV,
                arrivalFOVSpeed,
                arrivalFOVDuration
            );
        }

        activeSlotIndex = -1;
        _waitForInteractRelease = false;

        TeleportCompleted?.Invoke(
            completedSlot,
            safeDestination
        );

        if (logStateChanges)
        {
            Debug.Log(
                $"[TeleportController] 已传送到锚点 " +
                $"{GetSlotLabel(completedSlot)}：" +
                $"{safeDestination}"
            );
        }
    }

    private bool TryFindSafeDestination(
        Vector3 desiredDestination,
        out Vector3 safeDestination
    )
    {
        safeDestination =
            desiredDestination;

        LayerMask safeMask =
            ResolveEnvironmentMask();

        Vector3 baseCandidate =
            TrySnapToNearbyGround(
                desiredDestination,
                safeMask
            );

        if (IsCapsuleClear(
                baseCandidate,
                safeMask
            ))
        {
            safeDestination =
                baseCandidate;

            return true;
        }

        float safeStep =
            Mathf.Max(
                0.05f,
                correctionStep
            );

        float maxUp =
            Mathf.Max(
                safeStep,
                maximumUpwardCorrection
            );

        for (float up = safeStep;
             up <= maxUp + 0.001f;
             up += safeStep)
        {
            Vector3 upwardCandidate =
                baseCandidate +
                Vector3.up * up;

            if (IsCapsuleClear(
                    upwardCandidate,
                    safeMask
                ))
            {
                safeDestination =
                    upwardCandidate;

                return true;
            }
        }

        if (radialCorrectionDistance > 0f)
        {
            Vector3[] directions =
            {
                transform.forward,
                -transform.forward,
                transform.right,
                -transform.right
            };

            for (int i = 0;
                 i < directions.Length;
                 i++)
            {
                Vector3 radialCandidate =
                    baseCandidate +
                    directions[i].normalized *
                    radialCorrectionDistance +
                    Vector3.up * safeStep;

                if (IsCapsuleClear(
                        radialCandidate,
                        safeMask
                    ))
                {
                    safeDestination =
                        radialCandidate;

                    return true;
                }
            }
        }

        return false;
    }

    private Vector3 TrySnapToNearbyGround(
        Vector3 destination,
        LayerMask mask
    )
    {
        if (maximumGroundSnapDistance <= 0f ||
            mask.value == 0)
        {
            return destination;
        }

        float rayStartHeight =
            maximumGroundSnapDistance + 0.25f;

        Vector3 rayOrigin =
            destination +
            Vector3.up * rayStartHeight;

        float rayDistance =
            rayStartHeight +
            maximumGroundSnapDistance;

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                mask,
                QueryTriggerInteraction.Ignore
            ))
        {
            return destination;
        }

        float verticalDifference =
            destination.y -
            hit.point.y;

        if (verticalDifference < -0.1f ||
            verticalDifference >
            maximumGroundSnapDistance)
        {
            return destination;
        }

        return new Vector3(
            destination.x,
            hit.point.y +
            groundClearance,
            destination.z
        );
    }

    private bool IsCapsuleClear(
        Vector3 rootPosition,
        LayerMask mask
    )
    {
        if (characterController == null ||
            mask.value == 0)
        {
            return true;
        }

        float radius =
            Mathf.Max(
                0.05f,
                characterController.radius -
                characterController.skinWidth * 0.5f
            );

        float height =
            Mathf.Max(
                radius * 2f,
                characterController.height
            );

        Vector3 worldCenter =
            rootPosition +
            transform.rotation *
            characterController.center;

        float halfSegment =
            Mathf.Max(
                0f,
                height * 0.5f -
                radius
            );

        Vector3 bottom =
            worldCenter -
            Vector3.up * halfSegment;

        Vector3 top =
            worldCenter +
            Vector3.up * halfSegment;

        return !Physics.CheckCapsule(
            bottom,
            top,
            radius,
            mask,
            QueryTriggerInteraction.Ignore
        );
    }

    private LayerMask ResolveEnvironmentMask()
    {
        if (environmentMask.value != 0)
        {
            return environmentMask;
        }

        if (firstPersonController != null)
        {
            return firstPersonController.GroundLayers;
        }

        return 0;
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

    private void ValidateValues()
    {
        channelDuration =
            Mathf.Max(
                0.05f,
                channelDuration
            );

        activeMovementMultiplier =
            Mathf.Clamp(
                activeMovementMultiplier,
                0.1f,
                1f
            );

        maximumGroundSnapDistance =
            Mathf.Max(
                0f,
                maximumGroundSnapDistance
            );

        correctionStep =
            Mathf.Max(
                0.05f,
                correctionStep
            );

        maximumUpwardCorrection =
            Mathf.Max(
                correctionStep,
                maximumUpwardCorrection
            );

        visualDepartureLeadTime =
            Mathf.Max(
                0f,
                visualDepartureLeadTime
            );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateValues();
    }
#endif
}
