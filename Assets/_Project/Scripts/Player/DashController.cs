#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class DashController : MonoBehaviour
{
    public static DashController Instance;

    public event System.Action DashPerformed;

    [Header("独立 Dash")]
    [Tooltip("运行时状态。TunnelRailSystem 与视觉反馈会读取此值。")]
    public bool isDashing = false;

    [Min(0f)]
    [Tooltip("单次 Dash 的基础水平位移。顺着当前动量时最远。")]
    public float dashDistance = 4.6f;

    [Min(0.01f)]
    [Tooltip("单次 Dash 的持续时间。")]
    public float dashDuration = 0.18f;

    [Min(0f)]
    [Tooltip("从 Dash 开始计算的基础冷却时间。")]
    public float dashCooldown = 2f;

    [Min(0f)]
    [Tooltip("每次获得奖励的击杀减少多少 Dash 冷却。")]
    public float killCooldownReduction = 0.75f;

    [Header("电影级视角拉伸（共享接口）")]
    public float dashFOV = 100f;

    public float dashFOVExpandSpeed = 20f;

    public float fovRecoverySpeed = 8f;

    [Header("音效")]
    public AudioClip dashSound;

    public float CooldownRemaining => _cooldownRemaining;

    public float CooldownNormalized =>
        dashCooldown <= 0f
            ? 0f
            : Mathf.Clamp01(_cooldownRemaining / dashCooldown);

    public bool IsReady =>
        enabled &&
        AILURONEGameplayActionGate.AllowsGameplayActions &&
        !isDashing &&
        _cooldownRemaining <= 0f;

    private StarterAssets.FirstPersonController _firstPersonController;
    private StarterAssets.StarterAssetsInputs _inputs;
    private CharacterController _characterController;
    private AudioSource _audioSource;

    private Vector3 _dashDirection;
    private float _dashDistanceThisRun;
    private float _dashElapsed;
    private float _dashTimer;
    private float _cooldownRemaining;
    private bool _dashInputWasHeld;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        isDashing = false;

        EnemyTarget.AnyEnemyDied += HandleEnemyDied;
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (AILURONEGameplayActionGate.IsPaused)
        {
            return;
        }

        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining =
                Mathf.Max(
                    0f,
                    _cooldownRemaining - Time.deltaTime
                );
        }

        bool dashInputHeld =
            _inputs != null &&
            _inputs.sprint;

        bool dashPressed =
            dashInputHeld &&
            !_dashInputWasHeld;

        _dashInputWasHeld = dashInputHeld;

        if (dashPressed)
        {
            TryStartDash();
        }

        if (isDashing)
        {
            if (AILURONEGameplayActionGate.AllowsGameplayActions)
            {
                UpdateDash();
            }
            else
            {
                EndDash();
            }
        }
    }

    private void OnDisable()
    {
        EndDash();

        // 重新启用后必须先松开按键，避免 TP / 回溯结束时自动 Dash。
        _dashInputWasHeld = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            EnemyTarget.AnyEnemyDied -= HandleEnemyDied;
            Instance = null;
        }
    }

    public bool TryStartDash()
    {
        ResolveReferences();

        if (!IsReady ||
            _firstPersonController == null ||
            !_firstPersonController.enabled ||
            _characterController == null)
        {
            return false;
        }

        if (_firstPersonController.IsCannonControlled() ||
            _firstPersonController.IsTeleportChannelActive())
        {
            return false;
        }

        _dashDirection = ResolveDashDirection();
        _dashDistanceThisRun = ResolveDashDistance(
            _dashDirection
        );
        _dashElapsed = 0f;
        _dashTimer = Mathf.Max(0.01f, dashDuration);
        _cooldownRemaining = Mathf.Max(0f, dashCooldown);
        isDashing = true;

        // TunnelRailSystem 会保留 Dash，但暂时关闭 CharacterController。
        // Dash 是该系统约定的主动脱离方式，因此这里恢复碰撞移动。
        if (!_characterController.enabled)
        {
            _characterController.enabled = true;
        }

        HoldFOV(
            dashFOV,
            dashFOVExpandSpeed
        );

        if (VisualFeedbackController.Instance != null)
        {
            VisualFeedbackController.Instance
                .TriggerDashEffect(_dashDirection);
        }

        if (dashSound != null &&
            _audioSource != null)
        {
            _audioSource.pitch = 1f;
            _audioSource.PlayOneShot(dashSound);
        }

        DashPerformed?.Invoke();

        return true;
    }

    public void ReduceCooldown(float amount)
    {
        if (amount <= 0f ||
            _cooldownRemaining <= 0f)
        {
            return;
        }

        _cooldownRemaining =
            Mathf.Max(
                0f,
                _cooldownRemaining - amount
            );
    }

    private void UpdateDash()
    {
        if (_characterController == null ||
            !_characterController.enabled)
        {
            EndDash();
            return;
        }

        float safeDuration =
            Mathf.Max(0.01f, dashDuration);

        float previousProgress =
            Mathf.Clamp01(
                _dashElapsed / safeDuration
            );

        _dashElapsed =
            Mathf.Min(
                safeDuration,
                _dashElapsed + Time.deltaTime
            );

        float currentProgress =
            Mathf.Clamp01(
                _dashElapsed / safeDuration
            );

        float previousEase =
            EaseOutQuadratic(previousProgress);

        float currentEase =
            EaseOutQuadratic(currentProgress);

        float frameDistance =
            (currentEase - previousEase) *
            _dashDistanceThisRun;

        _characterController.Move(
            _dashDirection * frameDistance
        );

        _dashTimer =
            safeDuration - _dashElapsed;

        if (_dashTimer <= 0f)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        if (!isDashing)
        {
            return;
        }

        isDashing = false;
        _dashElapsed = 0f;
        _dashTimer = 0f;
        ReleaseFOV();
    }

    private Vector3 ResolveDashDirection()
    {
        Vector3 direction = Vector3.zero;

        if (_inputs != null &&
            _inputs.move.sqrMagnitude > 0.001f)
        {
            direction =
                transform.right * _inputs.move.x +
                transform.forward * _inputs.move.y;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction =
                _characterController != null
                    ? _characterController.velocity
                    : Vector3.zero;

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }
        }

        return direction.normalized;
    }

    private float ResolveDashDistance(
        Vector3 dashDirection
    )
    {
        float safeDistance =
            Mathf.Max(0f, dashDistance);

        if (_characterController == null)
        {
            return safeDistance;
        }

        Vector3 currentVelocity =
            _characterController.velocity;

        currentVelocity.y = 0f;

        if (currentVelocity.sqrMagnitude <= 0.01f)
        {
            return safeDistance;
        }

        float alignment =
            Vector3.Dot(
                currentVelocity.normalized,
                dashDirection
            );

        float alignment01 =
            Mathf.InverseLerp(
                -1f,
                1f,
                alignment
            );

        return
            safeDistance *
            Mathf.Lerp(
                0.82f,
                1f,
                alignment01
            );
    }

    private static float EaseOutQuadratic(
        float value
    )
    {
        float remaining =
            1f - Mathf.Clamp01(value);

        return 1f - remaining * remaining;
    }

    private void HandleEnemyDied(
        EnemyDeathInfo deathInfo
    )
    {
        if (!deathInfo.RewardsGranted)
        {
            return;
        }

        ReduceCooldown(killCooldownReduction);
    }

    // =========================================================
    // 共享 FOV 接口
    // JumpPad、TunnelRailSystem 等现有系统仍依赖这些方法。
    // =========================================================

    public void HoldFOV(
        float punchFOV,
        float expandSpeed,
        float autoReleaseTime = 0f
    )
    {
        ResolveReferences();

        if (_firstPersonController == null)
        {
            return;
        }

        _firstPersonController.RequestExternalFOV(
            punchFOV,
            expandSpeed,
            autoReleaseTime
        );
    }

    public void ReleaseFOV()
    {
        ResolveReferences();

        if (_firstPersonController == null)
        {
            return;
        }

        _firstPersonController.ReleaseExternalFOV(
            fovRecoverySpeed
        );
    }

    public void ConfirmTunnelEntry()
    {
        ResolveReferences();

        if (_firstPersonController == null)
        {
            return;
        }

        _firstPersonController
            .KeepExternalFOVUntilManualRelease();
    }

    private void ResolveReferences()
    {
        if (_firstPersonController == null)
        {
            _firstPersonController =
                GetComponent<StarterAssets.FirstPersonController>();
        }

        if (_inputs == null)
        {
            _inputs =
                GetComponent<StarterAssets.StarterAssetsInputs>();
        }

        if (_characterController == null)
        {
            _characterController =
                GetComponent<CharacterController>();
        }

        if (_audioSource == null)
        {
            _audioSource =
                GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                _audioSource =
                    gameObject.AddComponent<AudioSource>();

                _audioSource.playOnAwake = false;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        killCooldownReduction =
            Mathf.Max(0f, killCooldownReduction);
    }
#endif
}
