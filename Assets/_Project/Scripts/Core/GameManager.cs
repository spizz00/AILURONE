#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using AILURONE.Ranking;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI 引用 (状态)")]
    public Text deathTextUI;
    public Text winTextUI;

    [Header("UI 引用 (竞速与收集)")]
    public TextMeshProUGUI ballCountTextUI;
    public TextMeshProUGUI timerTextUI;
    public TextMeshProUGUI scoreTextUI;
    public TextMeshProUGUI comboTextUI;

    [Header("收集系统")]
    public int totalBallsRequired = 3;
    private int _currentBalls = 0;

    /// <summary>
    /// Fired whenever the player's carried Core inventory changes.
    /// Arguments: current carried count, configured total requirement.
    /// </summary>
    public event System.Action<int, int> CoreInventoryChanged;

    public int CurrentCoreCount => _currentBalls;
    public int TotalCoreRequirement => totalBallsRequired;

    [Header("📈 竞速计分引擎")]
    public float startingScore = 0f;
    public float parTime = 45f;
    public float bonusScorePerSecond = 2000f;

    [Header("🔥 连击评价引擎 (Style Combo)")]
    public int currentComboTier = 0;
    public int maxComboTier = 5;
    public float comboDecayTime = 2.8f;
    private float _currentComboTimer = 0f;

    public float[] comboMultipliers = { 1.0f, 1.2f, 1.5f, 2.0f, 2.5f, 3.0f };

    [Header("🧃 减时蓄水池 (Stack Buffer)")]
    public TextMeshProUGUI stackBufferTextUI;
    public float rollSpeed = 10f;
    [Tooltip("唯一的连击/减时结算窗口。每次有效击杀都会重置；归零时奖励结算且连击等级清零。")]
    [Min(0.1f)]
    public float stackLingerTime = 2.8f;

    [Header("🧲 减时数字撞入参数 (Cash-in Impact)")]
    [Tooltip("低额奖励使用的较慢飞行时间。")]
    [Min(0.1f)]
    public float cashInSlowDuration = 0.42f;

    [Tooltip("高额奖励使用的最快飞行时间。")]
    [Min(0.1f)]
    public float cashInFastDuration = 0.20f;

    [Tooltip("达到该秒数后，飞行速度和撞击强度按最大值处理。")]
    [Min(1f)]
    public float cashInFullStrengthSeconds = 9f;

    [Header("🧃 弹簧物理参数 (Spring Math)")]
    public float punchScaleForce = 0.4f;
    public float springStiffness = 150f;
    public float springDamping = 12f;

    private float _currentStackScale = 1f;
    private float _stackScaleVelocity = 0f;

    private float _currentStackLingerTimer = 0f;
    private float _pendingTimeReduction = 0f;
    private float _displayPendingTime = 0f;

    /// <summary>Current uncommitted time reward waiting in the stack buffer.</summary>
    public float PendingTimeReduction => _pendingTimeReduction;

    /// <summary>Previous pending seconds, current pending seconds.</summary>
    public event System.Action<float, float> PendingTimeRewardChanged;

    /// <summary>Raised when cash-in starts. Arguments: reward seconds, number flight duration.</summary>
    public event System.Action<float, float> TimeRewardCashInStarted;

    /// <summary>Raised on the exact frame the reward number reaches the main timer.</summary>
    public event System.Action<float> TimeRewardImpact;

    /// <summary>Raised when a positive time penalty is applied to the timer.</summary>
    public event System.Action<float> TimePenaltyApplied;

    /// <summary>The single shared duration used by both combo retention and reward settlement.</summary>
    public float SharedComboWindow => Mathf.Max(0.1f, stackLingerTime);

    /// <summary>Seconds left before the current pending reward is settled.</summary>
    public float ComboWindowRemaining =>
        _pendingTimeReduction > 0f && !_isCashInFlying
            ? Mathf.Max(0f, _currentStackLingerTimer)
            : 0f;

    /// <summary>0..1 countdown value for the TimerAdjustment underline.</summary>
    public float ComboWindowRemainingNormalized =>
        _pendingTimeReduction > 0f && !_isCashInFlying
            ? Mathf.Clamp01(_currentStackLingerTimer / SharedComboWindow)
            : 0f;

    private RectTransform _timerRect;
    private Vector2 _origStackPos;
    private bool _isCashInFlying = false;

    private Coroutine _timerFlashCoroutine;
    private Coroutine _scoreHighlightCoroutine;

    [Header("💥 视觉特效与颜色 (VFX)")]
    public GameObject floatingTextPrefab;

    public Color scoreColor = new Color(1f, 0.8f, 0.1f);
    public Color defaultScoreColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color defaultTimerColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color timeColor = new Color(0.1f, 0.9f, 0.9f);
    public Color penaltyColor = new Color(0.9f, 0.1f, 0.1f);

    [Header("🎧 音效引擎：动态连杀系统")]
    public AudioClip rewindSound;
    public float comboWindow = 2.0f;
    public float basePitch = 1.0f;
    public float pitchStep = 0.15f;
    public float maxPitch = 2.0f;

    private AudioSource _audioSource;
    private float _lastKillTime = -100f;
    private int _comboCount = 0;

    private float _elapsedTime = 0f;
    private float _currentScore = 0f;
    private bool _isTiming = false;
    private bool _isLevelEnded = false;
    public bool isGamePaused = false;

    public bool IsLevelEnded => _isLevelEnded;

    private GameObject _player;

    void Awake()
    {
        SynchronizeComboWindowSettings();

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnValidate()
    {
        SynchronizeComboWindowSettings();

        cashInSlowDuration = Mathf.Max(0.1f, cashInSlowDuration);
        cashInFastDuration = Mathf.Clamp(cashInFastDuration, 0.1f, cashInSlowDuration);
        cashInFullStrengthSeconds = Mathf.Max(1f, cashInFullStrengthSeconds);
    }

    private void SynchronizeComboWindowSettings()
    {
        stackLingerTime = Mathf.Max(0.1f, stackLingerTime);

        // Keep the legacy Inspector field visible and synchronized for old scenes/prefabs.
        // Runtime logic uses one shared countdown rather than two competing timers.
        comboDecayTime = stackLingerTime;
    }

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _player = GameObject.FindGameObjectWithTag("Player");

        if (deathTextUI != null)
        {
            deathTextUI.gameObject.SetActive(false);
        }

        if (winTextUI != null)
        {
            winTextUI.gameObject.SetActive(false);
        }

        if (comboTextUI != null)
        {
            comboTextUI.text = "";
        }

        if (timerTextUI != null)
        {
            _timerRect = timerTextUI.GetComponent<RectTransform>();
            timerTextUI.color = defaultTimerColor;
        }

        if (scoreTextUI != null)
        {
            scoreTextUI.color = defaultScoreColor;
        }

        if (stackBufferTextUI != null)
        {
            _origStackPos = stackBufferTextUI.rectTransform.anchoredPosition;
            stackBufferTextUI.gameObject.SetActive(false);
        }

        UpdateBallUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartTimer();
    }

    void Update()
    {
        if (_isTiming && !_isLevelEnded && !isGamePaused)
        {
            _elapsedTime += Time.unscaledDeltaTime;

            if (_pendingTimeReduction > 0f && !_isCashInFlying)
            {
                _currentStackLingerTimer -= Time.unscaledDeltaTime;
                _currentComboTimer = _currentStackLingerTimer;

                if (_currentStackLingerTimer <= 0f)
                {
                    CashInPendingPool();
                }
            }
            else if (currentComboTier > 0 && !_isCashInFlying)
            {
                // Score-only combo events still expire through the same shared window.
                _currentComboTimer -= Time.unscaledDeltaTime;

                if (_currentComboTimer <= 0f)
                {
                    ResetComboState();
                }
            }

            if (stackBufferTextUI != null && stackBufferTextUI.gameObject.activeSelf && !_isCashInFlying)
            {
                float targetScale = 1f;

                _stackScaleVelocity += (targetScale - _currentStackScale) * springStiffness * Time.unscaledDeltaTime;
                _stackScaleVelocity = Mathf.Lerp(_stackScaleVelocity, 0f, springDamping * Time.unscaledDeltaTime);
                _currentStackScale += _stackScaleVelocity * Time.unscaledDeltaTime;

                // Keep the amount readable at all reward sizes. New kills still create a short
                // scale punch, but the old high-value random shake has been removed.
                stackBufferTextUI.rectTransform.localScale = Vector3.one * Mathf.Max(0.1f, _currentStackScale);
                stackBufferTextUI.rectTransform.anchoredPosition = _origStackPos;
                stackBufferTextUI.alpha = 1f;

                if (Mathf.Abs(_displayPendingTime - _pendingTimeReduction) > 0.01f)
                {
                    _displayPendingTime = Mathf.Lerp(_displayPendingTime, _pendingTimeReduction, rollSpeed * Time.unscaledDeltaTime);

                    string hexTime = ColorUtility.ToHtmlStringRGB(timeColor);
                    stackBufferTextUI.text = $"<color=#{hexTime}>-{_displayPendingTime:F1}s</color>";
                }
            }

            UpdateTimerAndScoreUI();
        }
    }

    public void StartTimer()
    {
        _isTiming = true;
        _elapsedTime = 0f;
        _currentScore = startingScore;
    }

    public void StopTimer()
    {
        _isTiming = false;
    }

    public void AddBonusScore(float scoreBonus, float timeReduction = 0f, string enemyName = "UNKNOWN", Vector3 killPos = default)
    {
        if (!_isTiming || _isLevelEnded) return;

        float finalScore = scoreBonus;
        float finalTimeRed = timeReduction;

        if (scoreBonus > 0f || timeReduction > 0f)
        {
            float multiplier = comboMultipliers[Mathf.Clamp(currentComboTier, 0, comboMultipliers.Length - 1)];

            finalScore *= multiplier;
            finalTimeRed *= multiplier;

            UpgradeCombo();
        }

        if (floatingTextPrefab != null && killPos != default)
        {
            string combinedText = "";

            if (finalScore != 0f)
            {
                string prefix = finalScore > 0f ? "+" : "";
                string hexColor = ColorUtility.ToHtmlStringRGB(finalScore > 0f ? scoreColor : penaltyColor);
                combinedText += $"<color=#{hexColor}>{prefix}{finalScore:F0} PTS</color>";
            }

            if (finalTimeRed != 0f)
            {
                if (!string.IsNullOrEmpty(combinedText))
                {
                    combinedText += "\n";
                }

                string prefix = finalTimeRed > 0f ? "-" : "+";
                string hexColor = ColorUtility.ToHtmlStringRGB(finalTimeRed > 0f ? timeColor : penaltyColor);
                combinedText += $"<color=#{hexColor}>{prefix}{Mathf.Abs(finalTimeRed):F1}s</color>";
            }

            if (!string.IsNullOrEmpty(combinedText))
            {
                Vector3 spawnPos = killPos + Vector3.up * 1.5f;
                GameObject textObj = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);

                FloatingText floatingText = textObj.GetComponent<FloatingText>();
                if (floatingText != null)
                {
                    floatingText.Setup(combinedText, Color.white);
                }
            }
        }

        if (TerminalLogManager.Instance != null)
        {
            if (finalScore > 0f)
            {
                TerminalLogManager.Instance.AddLog($"+{finalScore:F0}", "ELIMINATED", $"■ {enemyName}");
            }
            else if (finalScore < 0f)
            {
                TerminalLogManager.Instance.AddLog($"{finalScore:F0}", "SYS_ERROR", $"■ {enemyName}");
            }
        }

        if (finalScore > 0f)
        {
            _currentScore += finalScore;

            if (_scoreHighlightCoroutine != null)
            {
                StopCoroutine(_scoreHighlightCoroutine);
            }

            _scoreHighlightCoroutine = StartCoroutine(ScoreHighlightRoutine());
        }
        else if (finalScore < 0f)
        {
            _currentScore += finalScore;
        }

        if (finalTimeRed > 0f)
        {
            float previousPendingTime = _pendingTimeReduction;
            _pendingTimeReduction += finalTimeRed;
            PendingTimeRewardChanged?.Invoke(previousPendingTime, _pendingTimeReduction);
            _currentStackLingerTimer = SharedComboWindow;
            _currentComboTimer = SharedComboWindow;

            if (stackBufferTextUI != null && !_isCashInFlying)
            {
                stackBufferTextUI.gameObject.SetActive(true);
                stackBufferTextUI.rectTransform.anchoredPosition = _origStackPos;
                stackBufferTextUI.alpha = 1f;

                _currentStackScale += punchScaleForce;
                _stackScaleVelocity += 2.5f;
            }
        }
        else if (finalTimeRed < 0f)
        {
            float penaltySeconds = -finalTimeRed;
            _elapsedTime = Mathf.Max(0f, _elapsedTime + penaltySeconds);
            TimePenaltyApplied?.Invoke(penaltySeconds);
            TriggerTimerCalmFlash(true);
        }

        if (finalTimeRed > 0f && rewindSound != null && _audioSource != null)
        {
            if (Time.time - _lastKillTime <= comboWindow)
            {
                _comboCount++;
            }
            else
            {
                _comboCount = 0;
            }

            _lastKillTime = Time.time;

            float calculatedPitch = basePitch + (_comboCount * pitchStep);
            calculatedPitch = Mathf.Min(calculatedPitch, maxPitch);

            _audioSource.pitch = calculatedPitch + Random.Range(-0.05f, 0.05f);
            _audioSource.volume = Random.Range(0.85f, 1.0f);
            _audioSource.PlayOneShot(rewindSound);
        }

        UpdateTimerAndScoreUI();
    }

    private void CashInPendingPool()
    {
        if (_pendingTimeReduction <= 0f)
        {
            ResetComboState();
            return;
        }

        float timeToCash = _pendingTimeReduction;
        float flightDuration = CalculateCashInFlightDuration(timeToCash);

        ResetComboState();
        TimeRewardCashInStarted?.Invoke(timeToCash, flightDuration);

        _pendingTimeReduction = 0f;
        _currentStackLingerTimer = 0f;
        PendingTimeRewardChanged?.Invoke(timeToCash, 0f);

        if (stackBufferTextUI != null && stackBufferTextUI.gameObject.activeSelf)
        {
            StartCoroutine(FlyAndCashInRoutine(timeToCash, flightDuration));
        }
        else
        {
            ExecuteCashIn(timeToCash);
        }
    }

    private float CalculateCashInFlightDuration(float rewardSeconds)
    {
        float strength = Mathf.Clamp01(
            (Mathf.Max(0f, rewardSeconds) - 1f) /
            Mathf.Max(0.001f, cashInFullStrengthSeconds - 1f));

        return Mathf.Lerp(cashInSlowDuration, cashInFastDuration, strength);
    }

    private IEnumerator FlyAndCashInRoutine(float time, float duration)
    {
        if (stackBufferTextUI == null || _timerRect == null)
        {
            ExecuteCashIn(time);
            yield break;
        }

        _isCashInFlying = true;
        duration = Mathf.Max(0.1f, duration);

        RectTransform stackRect = stackBufferTextUI.rectTransform;
        float elapsed = 0f;

        Vector3 startWorldPosition = stackRect.position;
        Vector3 startWorldCentre = stackRect.TransformPoint(stackRect.rect.center);
        Vector3 timerWorldCentre = _timerRect.TransformPoint(_timerRect.rect.center);
        Vector3 targetWorldPosition = startWorldPosition + (timerWorldCentre - startWorldCentre);
        Vector3 startScale = stackRect.localScale;

        float strength = Mathf.Clamp01(
            (Mathf.Max(0f, time) - 1f) /
            Mathf.Max(0.001f, cashInFullStrengthSeconds - 1f));

        string hexTime = ColorUtility.ToHtmlStringRGB(timeColor);
        stackBufferTextUI.text = $"<color=#{hexTime}>-{time:F1}s</color>";
        stackBufferTextUI.alpha = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Strong ease-in: it starts readable, then accelerates decisively into TimerValue.
            float moveT = t * t * t;
            stackRect.position = Vector3.LerpUnclamped(startWorldPosition, targetWorldPosition, moveT);

            // Keep the number readable for most of the trip. Compression only begins near impact;
            // it never shrinks to zero before reaching the timer.
            float holdScale = 1f + Mathf.Sin(Mathf.Clamp01(t / 0.68f) * Mathf.PI) * 0.025f * strength;
            float compressionT = Mathf.Clamp01((t - 0.76f) / 0.24f);
            float scaleMultiplier = Mathf.Lerp(holdScale, 0.78f, compressionT * compressionT);
            stackRect.localScale = startScale * scaleMultiplier;
            stackBufferTextUI.alpha = 1f;

            yield return null;
        }

        // Place the number exactly on the timer for one impact frame, then apply the reward.
        stackRect.position = targetWorldPosition;
        stackRect.localScale = startScale * 0.78f;
        stackBufferTextUI.alpha = 1f;

        ExecuteCashIn(time);
        yield return null;

        stackBufferTextUI.gameObject.SetActive(false);
        stackRect.anchoredPosition = _origStackPos;
        stackRect.localScale = Vector3.one;
        stackBufferTextUI.alpha = 1f;

        _displayPendingTime = 0f;
        _currentStackScale = 1f;
        _stackScaleVelocity = 0f;
        _isCashInFlying = false;

        // A new reward may have arrived during the short flight. Show it cleanly after impact.
        if (_pendingTimeReduction > 0f)
        {
            _displayPendingTime = _pendingTimeReduction;
            stackBufferTextUI.text = $"<color=#{hexTime}>-{_displayPendingTime:F1}s</color>";
            stackBufferTextUI.gameObject.SetActive(true);
            stackRect.anchoredPosition = _origStackPos;
        }
        else
        {
            stackRect.anchoredPosition = _origStackPos;
        }
    }

    private void ExecuteCashIn(float time)
    {
        _elapsedTime = Mathf.Max(0f, _elapsedTime - time);

        Debug.Log($"[系统通告] 独立清算！秒表扣除 {time:F1}秒！");

        TimeRewardImpact?.Invoke(time);
        TriggerTimerCalmFlash(false);
        UpdateTimerAndScoreUI();
    }

    private void UpgradeCombo()
    {
        if (currentComboTier < maxComboTier)
        {
            currentComboTier++;
        }

        _currentComboTimer = SharedComboWindow;
    }

    private void ResetComboState()
    {
        currentComboTier = 0;
        _currentComboTimer = 0f;
        _comboCount = 0;
        _lastKillTime = -100f;
    }

    public void BreakCombo()
    {
        ResetComboState();
        CashInPendingPool();
    }

    private IEnumerator ScoreHighlightRoutine()
    {
        if (scoreTextUI == null)
        {
            yield break;
        }

        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float scale = 1f + 0.5f * Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 5f);

            scoreTextUI.rectTransform.localScale = Vector3.one * scale;
            scoreTextUI.color = Color.Lerp(scoreColor, defaultScoreColor, t);

            yield return null;
        }

        scoreTextUI.rectTransform.localScale = Vector3.one;
        scoreTextUI.color = defaultScoreColor;
    }

    private void TriggerTimerCalmFlash(bool isPenalty)
    {
        if (timerTextUI == null) return;

        if (_timerFlashCoroutine != null)
        {
            StopCoroutine(_timerFlashCoroutine);
        }

        _timerFlashCoroutine = StartCoroutine(TimerFlashRoutine(isPenalty));
    }

    private IEnumerator TimerFlashRoutine(bool isPenalty)
    {
        if (timerTextUI == null)
        {
            yield break;
        }

        float elapsed = 0f;
        float duration = 0.45f;

        Color targetColor = isPenalty ? penaltyColor : timeColor;

        timerTextUI.color = targetColor;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            timerTextUI.color = Color.Lerp(targetColor, defaultTimerColor, elapsed / duration);

            yield return null;
        }

        timerTextUI.color = defaultTimerColor;
    }

    private void UpdateTimerAndScoreUI()
    {
        if (timerTextUI != null)
        {
            int minutes = Mathf.FloorToInt(_elapsedTime / 60F);
            int seconds = Mathf.FloorToInt(_elapsedTime - minutes * 60);
            int milliseconds = Mathf.FloorToInt((_elapsedTime - minutes * 60 - seconds) * 100);

            timerTextUI.text = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, milliseconds);
        }

        if (scoreTextUI != null)
        {
            scoreTextUI.text = Mathf.FloorToInt(_currentScore).ToString();
        }

        if (comboTextUI != null)
        {
            if (currentComboTier == 0)
            {
                comboTextUI.text = "";
            }
            else
            {
                string[] tierNames = { "NONE", "C", "B", "A", "S", "SS" };

                string currentName = tierNames[Mathf.Clamp(currentComboTier, 0, tierNames.Length - 1)];
                float currentMultiplier = comboMultipliers[Mathf.Clamp(currentComboTier, 0, comboMultipliers.Length - 1)];

                comboTextUI.text = $"STYLE: {currentName} (x{currentMultiplier:F1})";

                if (currentComboTier >= 4)
                {
                    comboTextUI.color = new Color(0.9f, 0.1f, 0.1f);
                }
                else
                {
                    comboTextUI.color = Color.white;
                }
            }
        }
    }

    public void AddBall()
    {
        if (_isLevelEnded) return;

        _currentBalls++;
        UpdateBallUI();
    }

    public int GetCurrentBallsCount()
    {
        return _currentBalls;
    }

    public bool TrySpendBall()
    {
        if (_currentBalls > 0)
        {
            _currentBalls--;
            UpdateBallUI();
            return true;
        }

        return false;
    }

    private void UpdateBallUI()
    {
        if (ballCountTextUI != null)
        {
            ballCountTextUI.text =
                "REWRITE NODES: " + _currentBalls + " / " + totalBallsRequired;
        }

        CoreInventoryChanged?.Invoke(
            _currentBalls,
            totalBallsRequired
        );
    }

    // =========================================================
    // DeadZone / 敌方子弹调用这里
    // 不重开场景，不传送出生点，而是触发 PlayerHealth 的回溯系统
    // =========================================================
    public void PlayerDied()
    {
        if (_isLevelEnded) return;

        if (_player == null)
        {
            _player = GameObject.FindGameObjectWithTag("Player");
        }

        if (_player == null)
        {
      Debug.LogWarning("️ [GameManager] PlayerDied 被调用，但找不到 Player。");
            return;
        }

        PlayerHealth health = _player.GetComponent<PlayerHealth>();

        if (health == null)
        {
      Debug.LogWarning("️ [GameManager] PlayerDied 被调用，但 Player 身上没有 PlayerHealth。");
            return;
        }

        // 造成一次必死伤害，让 PlayerHealth 自己执行：
        // 扣分 / 加时间惩罚 / 断连击 / 视觉回溯 / 位置回溯
        float lethalDamage = Mathf.Max(health.currentHealth, health.maxHealth) + 9999f;
        health.TakeDamage(
            lethalDamage,
            true
        );
    }

    public void TriggerWin()
    {
        if (_isLevelEnded) return;

        _isLevelEnded = true;

        StopTimer();
        CashInPendingPool();

        float timeSaved = Mathf.Max(0f, parTime - _elapsedTime);
        float timeBonusScore = timeSaved * bonusScorePerSecond;

        _currentScore += timeBonusScore;

        CheckAndSaveEasterEgg();

        StartCoroutine(LevelClearedRoutine(timeSaved, timeBonusScore));
    }

    private void CheckAndSaveEasterEgg()
    {
        if (RunDataManager.Instance == null) return;

        // 旧版逻辑依赖 WeaponInteraction 判断是否携带 GlitchCore。
        // 现在已经废弃捡枪/丢枪系统，所以这里先关闭旧彩蛋保存。
        RunDataManager.Instance.hasGlitchCore = false;
    }

    private IEnumerator LevelClearedRoutine(float timeSaved, float timeBonus)
    {
        Scene completedScene = SceneManager.GetActiveScene();
        bool useTutorialTransit =
            completedScene.name == "Tutorial";

        SetPlayerControls(false, true);

        Time.timeScale = useTutorialTransit ? 1f : 0.1f;

        if (winTextUI != null && !useTutorialTransit)
        {
            winTextUI.gameObject.SetActive(true);

            winTextUI.text =
                $"LEVEL CLEARED\n\n" +
                $"FINAL TIME: {_elapsedTime:F2}s\n" +
                $"TIME SAVED: {timeSaved:F2}s\n" +
                $"TIME BONUS: +{Mathf.FloorToInt(timeBonus)}\n\n" +
                $"TOTAL SCORE: {Mathf.FloorToInt(_currentScore)}\n\n" +
                $"WARPING...";
        }

        if (useTutorialTransit)
        {
            if (winTextUI != null)
            {
                winTextUI.gameObject.SetActive(false);
            }

            TutorialExitTransitionFX transition =
                gameObject.GetComponent<TutorialExitTransitionFX>();
            if (transition == null)
            {
                transition =
                    gameObject.AddComponent<TutorialExitTransitionFX>();
            }

            yield return transition.PlayRoutine(0.8f);
        }
        else if (completedScene.name == "Level")
        {
            AILURONELevelCompleteRankingScreen rankingScreen =
                AILURONELevelCompleteRankingScreen.Show(
                    Mathf.FloorToInt(_currentScore),
                    _elapsedTime);

            while (rankingScreen != null &&
                   rankingScreen.Choice == LevelCompleteChoice.None)
            {
                yield return null;
            }

            LevelCompleteChoice choice = rankingScreen != null
                ? rankingScreen.Choice
                : LevelCompleteChoice.MainMenu;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            if (choice == LevelCompleteChoice.Restart)
            {
                SceneManager.LoadScene(completedScene.buildIndex);
            }
            else if (choice == LevelCompleteChoice.Quit)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
            yield break;
        }
        else
        {
            yield return new WaitForSecondsRealtime(4f);
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        int nextSceneIndex = completedScene.buildIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            SceneManager.LoadScene(completedScene.buildIndex);
        }
    }

    private void SetPlayerControls(bool enabled, bool unlockCursorWhenDisabled)
    {
        if (_player == null)
        {
            _player = GameObject.FindGameObjectWithTag("Player");
        }

        if (_player != null)
        {
            var fpc = _player.GetComponent<StarterAssets.FirstPersonController>();
            if (fpc != null)
            {
                fpc.enabled = enabled;
            }

            var alwaysWeapon = _player.GetComponent<AlwaysEquippedWeaponController>();
            if (alwaysWeapon != null)
            {
                alwaysWeapon.enabled = enabled;
            }

            var dash = _player.GetComponent<DashController>();
            if (dash != null)
            {
                dash.enabled = enabled;
            }

            var playerInput = _player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = enabled;
            }

            var input = _player.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (input != null && !enabled)
            {
                input.move = Vector2.zero;
                input.look = Vector2.zero;
                input.jump = false;
                input.sprint = false;
            }
        }

        if (enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (unlockCursorWhenDisabled)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
