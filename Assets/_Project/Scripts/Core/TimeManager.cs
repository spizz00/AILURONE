#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TimeManager : MonoBehaviour
{
    public event System.Action TimeSlowActivated;

    public static TimeManager Instance;

    [Header("时间技能：硬核物理电池 (Hardcore Energy Pool)")]
    [Range(0.001f, 1f)]
    public float minTimeScale = 0.1f;

    [Tooltip("满电状态下，最长可维持的超频时间，单位为真实秒。")]
    [Min(0.05f)]
    public float maxAbilityDuration = 3f;

    [Tooltip("从 0% 充能到 100% 所需的真实时间。")]
    [Min(0.05f)]
    public float fullRechargeTime = 5f;

    [Space(10)]
    [Tooltip("每次启动技能时立即消耗的能量。0.15 = 15%。")]
    [Range(0f, 1f)]
    public float activationCost = 0.15f;

    [Tooltip("正常关闭技能后，需要等待多久才开始重新充能。")]
    [Min(0f)]
    public float rechargeDelay = 1f;

    [Header("死亡回溯后的技能处理")]
    [Tooltip(
        "死亡回溯完成后，技能短暂锁定的时间。" +
        "能量不会被清零。"
    )]
    [Min(0f)]
    public float postRewindLockDuration = 0.5f;

    [Header("肾上腺素衰减曲线")]
    public AnimationCurve adrenalineCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );

    [Header("音效反馈")]
    public AudioClip timeSlowActivateSound;

    [Header("工业像素 UI (Custom HUD)")]
    public Image jumpIconForeground;
    public Image overclockIconForeground;

    [Header("Crosshair UI (中心动态准星)")]
    public RectTransform leftBracket;
    public RectTransform rightBracket;

    public TextMeshProUGUI leftGraphic;
    public TextMeshProUGUI rightGraphic;

    public Image centerCrosshairImage;

    public float bracketDistanceOpen = 60f;
    public float bracketDistanceClosed = 15f;

    [Header("全局 UI 颜色设置")]
    public Color colorReady = Color.white;

    public Color colorCrosshairNormal =
        Color.white;

    public Color colorActive =
        new Color(
            1f,
            0.1f,
            0.1f,
            1f
        );

    public Color colorCooldown =
        new Color(
            0.4f,
            0.4f,
            0.4f,
            1f
        );

    public Color colorFatalError =
        new Color(
            0.9f,
            0.05f,
            0.05f,
            1f
        );

    [Header("运行状态（Play Mode 观察）")]
    [SerializeField]
    private bool _isAbilityActive;

    [SerializeField]
    [Range(0f, 1f)]
    private float _energy = 1f;

    [SerializeField]
    private float _rechargeDelayTimer;

    [SerializeField]
    private bool _isRewinding;

    public bool IsAbilityActive =>
        _isAbilityActive;

    public bool IsRewinding =>
        _isRewinding;

    public float CurrentEnergy =>
        _energy;

    public float RechargeDelayRemaining =>
        _rechargeDelayTimer;

    public bool CanActivateAbility
    {
        get
        {
            return
                AILURONEGameplayActionGate.AllowsGameplayActions &&
                !_isRewinding &&
                !_isAbilityActive &&
                _rechargeDelayTimer <= 0f &&
                _energy >= activationCost;
        }
    }

    private AudioSource _audioSource;

    private Coroutine _overclockCoroutine;
    private Coroutine _rewindCoroutine;

    private AILURONE.HUD.AILURONEAbilityHUDVisual _abilityHUDVisual;

    private float _defaultFixedDeltaTime = 0.02f;
    private bool _restoreNormalTimeWhenResumed;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        /*
         * 记录项目原本的 Fixed Delta Time。
         * 不再把 0.02 写死到所有恢复逻辑中。
         */
        if (Time.fixedDeltaTime > 0f)
        {
            _defaultFixedDeltaTime =
                Time.fixedDeltaTime;
        }
    }

    private void Start()
    {
        _audioSource =
            GetComponent<AudioSource>();

        if (_audioSource == null)
        {
            _audioSource =
                gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;

        _energy =
            Mathf.Clamp01(_energy);

        RestoreNormalTime();
        UpdateJumpUI();
        UpdatePassiveAbilityUI();

        _abilityHUDVisual =
            GetComponent<AILURONE.HUD.AILURONEAbilityHUDVisual>();

        if (_abilityHUDVisual == null)
        {
            _abilityHUDVisual =
                gameObject.AddComponent<AILURONE.HUD.AILURONEAbilityHUDVisual>();
        }

        _abilityHUDVisual.Initialize(
            this,
            jumpIconForeground,
            overclockIconForeground
        );
    }

    private void Update()
    {
        if (AILURONEGameplayActionGate.IsPaused)
        {
            return;
        }

        if (_restoreNormalTimeWhenResumed)
        {
            RestoreNormalTime();
        }

        /*
         * 回溯期间，UI 由 RewindGlitchRoutine
         * 自己控制，不处理输入和充能。
         */
        if (_isRewinding)
        {
            return;
        }

        UpdateJumpUI();

        if (!_isAbilityActive)
        {
            UpdateRechargeState();
            UpdatePassiveAbilityUI();
        }

        HandleAbilityInput();
    }

    // =========================================================
    // 输入
    // =========================================================

    private void HandleAbilityInput()
    {
        if (!AILURONEGameplayActionGate.AllowsGameplayActions ||
            Keyboard.current == null)
        {
            return;
        }

        if (!Keyboard.current.fKey.wasPressedThisFrame)
        {
            return;
        }

        if (_isAbilityActive)
        {
            StopOverclock(
                rechargeDelay,
                true
            );

            return;
        }

        if (CanActivateAbility)
        {
            BeginOverclock();
        }
    }

    // =========================================================
    // 被动充能
    // =========================================================

    private void UpdateRechargeState()
    {
        if (_rechargeDelayTimer > 0f)
        {
            _rechargeDelayTimer -=
                Time.unscaledDeltaTime;

            if (_rechargeDelayTimer < 0f)
            {
                _rechargeDelayTimer = 0f;
            }

            return;
        }

        if (_energy >= 1f)
        {
            _energy = 1f;
            return;
        }

        float safeRechargeTime =
            Mathf.Max(
                0.05f,
                fullRechargeTime
            );

        _energy +=
            Time.unscaledDeltaTime /
            safeRechargeTime;

        _energy =
            Mathf.Clamp01(_energy);
    }

    // =========================================================
    // 时间变缓
    // =========================================================

    private void BeginOverclock()
    {
        if (!CanActivateAbility)
        {
            return;
        }

        if (_overclockCoroutine != null)
        {
            StopCoroutine(
                _overclockCoroutine
            );

            _overclockCoroutine = null;
        }

        _overclockCoroutine =
            StartCoroutine(
                AdrenalineDecayRoutine()
            );
    }

    private IEnumerator AdrenalineDecayRoutine()
    {
        _isAbilityActive = true;
        _rechargeDelayTimer = 0f;

        _energy -= activationCost;
        _energy = Mathf.Clamp01(_energy);

        PlayActivationSound();
        TimeSlowActivated?.Invoke();

        while (
            _isAbilityActive &&
            !_isRewinding &&
            _energy > 0f
        )
        {
            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            float safeDuration =
                Mathf.Max(
                    0.05f,
                    maxAbilityDuration
                );

            _energy -=
                Time.unscaledDeltaTime /
                safeDuration;

            _energy =
                Mathf.Clamp01(_energy);

            ApplyActiveTimeScale();
            UpdateActiveAbilityUI();

            yield return null;
        }

        _overclockCoroutine = null;

        /*
         * 如果是死亡回溯打断了协程，
         * 回溯逻辑会负责恢复时间和状态，
         * 这里不能再添加普通散热时间。
         */
        if (!_isRewinding)
        {
            FinishOverclockNaturally();
        }
    }

    private void ApplyActiveTimeScale()
    {
        if (AILURONEGameplayActionGate.IsPaused)
        {
            return;
        }

        float progress =
            1f - _energy;

        float curveValue =
            adrenalineCurve != null
                ? adrenalineCurve.Evaluate(
                    progress
                )
                : progress;

        curveValue =
            Mathf.Clamp01(curveValue);

        Time.timeScale =
            Mathf.Lerp(
                minTimeScale,
                1f,
                curveValue
            );

        Time.fixedDeltaTime =
            _defaultFixedDeltaTime *
            Time.timeScale;
    }

    private void FinishOverclockNaturally()
    {
        _isAbilityActive = false;

        RestoreNormalTime();

        _rechargeDelayTimer =
            Mathf.Max(
                0f,
                rechargeDelay
            );

        UpdatePassiveAbilityUI();

        Debug.Log(
            $"[系统通告] 引擎停机！" +
            $"剩余电量：{_energy * 100f:F1}%。" +
            $"进入 {_rechargeDelayTimer:F1} 秒散热死区。"
        );
    }

    private void StopOverclock(
        float cooldownDuration,
        bool printLog
    )
    {
        if (_overclockCoroutine != null)
        {
            StopCoroutine(
                _overclockCoroutine
            );

            _overclockCoroutine = null;
        }

        _isAbilityActive = false;

        RestoreNormalTime();

        _rechargeDelayTimer =
            Mathf.Max(
                0f,
                cooldownDuration
            );

        UpdatePassiveAbilityUI();

        if (printLog)
        {
            Debug.Log(
                $"[系统通告] 手动关闭超频。" +
                $"剩余电量：{_energy * 100f:F1}%。" +
                $"进入 {_rechargeDelayTimer:F1} 秒散热死区。"
            );
        }
    }

    private void CancelOverclockForRewind()
    {
        if (_overclockCoroutine != null)
        {
            StopCoroutine(
                _overclockCoroutine
            );

            _overclockCoroutine = null;
        }

        _isAbilityActive = false;

        RestoreNormalTime();
    }

    private void RestoreNormalTime()
    {
        if (AILURONEGameplayActionGate.IsPaused)
        {
            _restoreNormalTimeWhenResumed = true;
            return;
        }

        _restoreNormalTimeWhenResumed = false;
        Time.timeScale = 1f;

        Time.fixedDeltaTime =
            _defaultFixedDeltaTime;
    }

    private void PlayActivationSound()
    {
        if (_audioSource == null ||
            timeSlowActivateSound == null)
        {
            return;
        }

        _audioSource.pitch = 1f;

        _audioSource.PlayOneShot(
            timeSlowActivateSound
        );
    }

    // =========================================================
    // 死亡回溯
    // =========================================================

    public void TriggerRewindGlitch(
        float duration
    )
    {
        if (_isRewinding)
        {
            return;
        }

        if (_rewindCoroutine != null)
        {
            StopCoroutine(
                _rewindCoroutine
            );

            _rewindCoroutine = null;
        }

        _rewindCoroutine =
            StartCoroutine(
                RewindGlitchRoutine(
                    Mathf.Max(0f, duration)
                )
            );
    }

    private IEnumerator RewindGlitchRoutine(
        float duration
    )
    {
        /*
         * 在回溯开始前记录当前资源。
         * 死亡不会再将能量清零。
         */
        float energyBeforeRewind =
            Mathf.Clamp01(_energy);

        float cooldownBeforeRewind =
            Mathf.Max(
                0f,
                _rechargeDelayTimer
            );

        _isRewinding = true;

        /*
         * 无论玩家死亡时是否正在使用技能，
         * 都彻底停止原来的超频协程。
         */
        CancelOverclockForRewind();

        float rewindElapsed = 0f;
        float nextGlitchTick = 0f;

        while (rewindElapsed < duration)
        {
            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            rewindElapsed += Time.unscaledDeltaTime;

            if (rewindElapsed >= nextGlitchTick)
            {
                UpdateRewindGlitchUI();
                nextGlitchTick += 0.05f;
            }

            yield return null;
        }

        /*
         * 恢复死亡前的实际能量。
         */
        _energy =
            energyBeforeRewind;

        /*
         * 若死亡前本来就在散热，
         * 不应通过死亡缩短原有散热时间。
         *
         * 否则只使用 0.5 秒左右的回溯后锁定。
         */
        _rechargeDelayTimer =
            Mathf.Max(
                cooldownBeforeRewind,
                postRewindLockDuration
            );

        _isAbilityActive = false;
        _isRewinding = false;

        _rewindCoroutine = null;

        RestoreNormalTime();

        UpdateJumpUI();
        UpdatePassiveAbilityUI();

        Debug.Log(
            $"[系统通告] 回溯完成。" +
            $"保留能量：{_energy * 100f:F1}%。" +
            $"技能锁定：{_rechargeDelayTimer:F1} 秒。"
        );
    }

    private void UpdateRewindGlitchUI()
    {
        if (jumpIconForeground != null)
        {
            jumpIconForeground.fillAmount =
                Random.Range(
                    0f,
                    1f
                );

            jumpIconForeground.color =
                Random.value > 0.5f
                    ? colorFatalError
                    : Color.white;
        }

        if (overclockIconForeground != null)
        {
            overclockIconForeground.fillAmount =
                Random.Range(
                    0f,
                    1f
                );

            overclockIconForeground.color =
                Random.value > 0.5f
                    ? colorFatalError
                    : Color.white;
        }

        UpdateCrosshair(
            Random.Range(
                0f,
                1f
            ),
            colorFatalError,
            colorFatalError
        );
    }

    // =========================================================
    // UI
    // =========================================================

    private void UpdateJumpUI()
    {
        if (jumpIconForeground == null)
        {
            return;
        }

        if (
            StarterAssets
                .FirstPersonController
                .Instance == null
        )
        {
            return;
        }

        int jumpsLeft =
            StarterAssets
                .FirstPersonController
                .Instance
                .GetCurrentJumps();

        int maxJumps =
            StarterAssets
                .FirstPersonController
                .Instance
                .GetMaxJumps();

        jumpIconForeground.fillAmount =
            (float)jumpsLeft /
            Mathf.Max(
                1,
                maxJumps
            );

        /*
         * 防止回溯 Glitch 的红色残留。
         */
        jumpIconForeground.color =
            colorReady;
    }

    private void UpdateActiveAbilityUI()
    {
        if (overclockIconForeground != null)
        {
            overclockIconForeground.fillAmount =
                _energy;

            overclockIconForeground.color =
                colorActive;
        }

        float progress =
            1f - _energy;

        UpdateCrosshair(
            progress,
            colorActive,
            colorActive
        );
    }

    private void UpdatePassiveAbilityUI()
    {
        bool isCooling =
            _rechargeDelayTimer > 0f;

        bool hasEnoughEnergy =
            _energy >= activationCost;

        bool isReady =
            !isCooling &&
            hasEnoughEnergy;

        Color iconColor =
            isReady
                ? colorReady
                : colorCooldown;

        if (overclockIconForeground != null)
        {
            /*
             * 填充量始终显示真实剩余能量。
             */
            overclockIconForeground.fillAmount =
                _energy;

            /*
             * 不再要求 100% 满电才显示白色。
             * 达到 Activation Cost 后就显示可用。
             */
            overclockIconForeground.color =
                iconColor;
        }

        UpdateCrosshair(
            1f - _energy,
            iconColor,
            colorCrosshairNormal
        );
    }

    private void UpdateCrosshair(
        float contractionProgress,
        Color bracketColor,
        Color centerCrosshairColor
    )
    {
        contractionProgress =
            Mathf.Clamp01(
                contractionProgress
            );

        if (
            leftBracket != null &&
            rightBracket != null
        )
        {
            float currentDistance =
                Mathf.Lerp(
                    bracketDistanceOpen,
                    bracketDistanceClosed,
                    contractionProgress
                );

            leftBracket.anchoredPosition =
                new Vector2(
                    -currentDistance,
                    0f
                );

            rightBracket.anchoredPosition =
                new Vector2(
                    currentDistance,
                    0f
                );
        }

        if (
            leftGraphic != null &&
            rightGraphic != null
        )
        {
            leftGraphic.color =
                bracketColor;

            rightGraphic.color =
                bracketColor;
        }

        if (centerCrosshairImage != null)
        {
            centerCrosshairImage.color =
                centerCrosshairColor;
        }
    }

    // =========================================================
    // 生命周期保险
    // =========================================================

    private void OnDisable()
    {
        RestoreNormalTime();

        _isAbilityActive = false;
        _isRewinding = false;

        _overclockCoroutine = null;
        _rewindCoroutine = null;
    }

    private void OnDestroy()
    {
        RestoreNormalTime();

        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minTimeScale =
            Mathf.Clamp(
                minTimeScale,
                0.001f,
                1f
            );

        maxAbilityDuration =
            Mathf.Max(
                0.05f,
                maxAbilityDuration
            );

        fullRechargeTime =
            Mathf.Max(
                0.05f,
                fullRechargeTime
            );

        activationCost =
            Mathf.Clamp01(
                activationCost
            );

        rechargeDelay =
            Mathf.Max(
                0f,
                rechargeDelay
            );

        postRewindLockDuration =
            Mathf.Max(
                0f,
                postRewindLockDuration
            );
    }
#endif
}
