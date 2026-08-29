#pragma warning disable 0618
#pragma warning disable 0414
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Reference Timer v3 behaviour while retaining the existing V2 component/class name.
    ///
    /// GameManager remains authoritative for gameplay values. This component only:
    /// - displays pending reward through nine symmetric segments;
    /// - creates a thin countdown underline beneath TimerAdjustment;
    /// - collapses segments from outside to centre during cash-in;
    /// - adds a reward-size-dependent impact response when the number hits TimerValue;
    /// - expands the top penalty line on time penalties.
    ///
    /// Authored positions and rotations are never overwritten. Reward segments only change
    /// colour and local scale; the timer root only receives temporary visual offsets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONETimerHUDReferenceV2 : MonoBehaviour
    {
        private const int SegmentCount = 9;

        [Header("References")]
        [SerializeField] private RectTransform timerArtRoot;
        [SerializeField] private RectTransform rewardSegmentsRoot;
        [SerializeField] private Image[] rewardSegments = new Image[SegmentCount];
        [SerializeField] private Image topPenaltyLine;
        [SerializeField] private TMP_Text timerValue;
        [SerializeField] private TMP_Text timerAdjustment;
        [SerializeField] private Image comboWindowLine;

        [Header("Reward segment colours")]
        [SerializeField] private Color rewardDimColor = new Color32(36, 90, 98, 255);       // #245A62
        [SerializeField] private Color rewardBrightColor = new Color32(69, 230, 241, 255);  // #45E6F1
        [SerializeField] private Color rewardFlashColor = new Color32(204, 252, 255, 255);

        [Header("Pending reward response")]
        [Min(0.01f)]
        [SerializeField] private float fillResponseSpeed = 13f;

        [Min(0.05f)]
        [SerializeField] private float newRewardPunchDuration = 0.22f;

        [Range(0f, 0.5f)]
        [SerializeField] private float newRewardPunchScale = 0.20f;

        [Tooltip("Extra one-shot punch when more reward arrives after all nine segments are full.")]
        [Range(0f, 0.35f)]
        [SerializeField] private float overflowRewardPunchScale = 0.10f;

        [Header("Combo window underline")]
        [SerializeField] private Color comboLineNormalColor = new Color32(69, 230, 241, 220);
        [SerializeField] private Color comboLineUrgentColor = new Color32(204, 252, 255, 255);

        [Min(20f)]
        [SerializeField] private float comboLineFullWidth = 78f;

        [Min(1f)]
        [SerializeField] private float comboLineHeight = 2.0f;

        [SerializeField] private float comboLineLocalY = -12.5f;

        [Tooltip("The line becomes brighter during this final normalized portion of the window.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float comboLineUrgentThreshold = 0.20f;

        [Header("Cash-in segment response")]
        [Tooltip("Fallback only. Runtime cash-in duration is synchronized to the flying number.")]
        [Min(0.12f)]
        [SerializeField] private float cashInDuration = 0.36f;

        [Range(0f, 0.08f)]
        [SerializeField] private float cashInRingDelay = 0.04f;

        [Range(1f, 1.6f)]
        [SerializeField] private float cashInFlashScale = 1.24f;

        [Header("Reward impact on TimerValue")]
        [Min(0.15f)]
        [SerializeField] private float rewardImpactDuration = 0.36f;

        [Range(0f, 0.20f)]
        [SerializeField] private float rewardImpactMinimumScale = 0.06f;

        [Range(0f, 0.30f)]
        [SerializeField] private float rewardImpactMaximumScale = 0.16f;

        [Range(0f, 6f)]
        [SerializeField] private float rewardImpactMinimumKick = 0.7f;

        [Range(0f, 8f)]
        [SerializeField] private float rewardImpactMaximumKick = 2.8f;

        [Min(1f)]
        [SerializeField] private float rewardImpactFullStrengthSeconds = 9f;

        [Header("Penalty line")]
        [SerializeField] private Color penaltyIdleColor = new Color32(75, 67, 72, 255); // #4B4348
        [SerializeField] private Color penaltyActiveColor = new Color32(255, 68, 93, 255); // #FF445D

        [Min(0.2f)]
        [SerializeField] private float penaltyDuration = 0.52f;

        [Min(42f)]
        [SerializeField] private float penaltyMinimumExpandedWidth = 130f;

        [Min(0f)]
        [SerializeField] private float penaltyWidthPerSecond = 13f;

        [Min(42f)]
        [SerializeField] private float penaltyMaximumExpandedWidth = 220f;

        [Range(0f, 0.15f)]
        [SerializeField] private float penaltyTimerPunchScale = 0.045f;

        [Range(0f, 8f)]
        [SerializeField] private float penaltyHorizontalKick = 2.0f;

        private readonly float[] _displayFills = new float[SegmentCount];
        private readonly float[] _targetFills = new float[SegmentCount];
        private readonly float[] _cashStartFills = new float[SegmentCount];
        private readonly float[] _punchTimers = new float[SegmentCount];
        private readonly Vector3[] _segmentBaseScales = new Vector3[SegmentCount];

        private GameManager _gameManager;
        private float _pendingSeconds;

        private bool _cashInActive;
        private float _cashInElapsed;
        private float _activeCashInDuration;
        private int _cashOuterRing;

        private bool _rewardImpactActive;
        private float _rewardImpactElapsed;
        private float _rewardImpactStrength;
        private float _rewardImpactScaleAdd;
        private float _rewardImpactKickX;

        private bool _penaltyActive;
        private float _penaltyElapsed;
        private float _penaltyExpandedWidth;
        private float _penaltyScaleAdd;
        private float _penaltyKickX;

        private Vector2 _penaltyBaseSize;
        private Vector3 _timerValueBaseScale = Vector3.one;
        private Vector2 _rootBasePosition;

        private bool _referencesReady;
        private bool _warnedMissingReferences;

        private void Awake()
        {
            ResolveReferences();
            EnsureComboWindowLine();
            CaptureBaseState();
            ApplyImmediateDefaultState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureComboWindowLine();
            CaptureBaseState();
            ApplyImmediateDefaultState();
            TryBindGameManager();
        }

        private void Start()
        {
            EnsureComboWindowLine();
            TryBindGameManager();
        }

        private void OnDisable()
        {
            UnbindGameManager();
            ApplyImmediateDefaultState();
        }

        private void Update()
        {
            if (!_referencesReady)
            {
                ResolveReferences();
                EnsureComboWindowLine();

                if (!_referencesReady)
                {
                    return;
                }

                CaptureBaseState();
                ApplyImmediateDefaultState();
            }

            if (_gameManager == null)
            {
                TryBindGameManager();
            }

            float dt = Time.unscaledDeltaTime;

            if (_cashInActive)
            {
                UpdateCashIn(dt);
            }
            else
            {
                UpdatePendingSegments(dt);
            }

            UpdateComboWindowLine();
            UpdatePenalty(dt);
            UpdateRewardImpact(dt);
            ApplyCombinedTimerTransform();
        }

        [ContextMenu("Auto Assign Timer V2 References")]
        private void ResolveReferences()
        {
            if (timerArtRoot == null)
            {
                Transform found = transform.Find("TimerArtRoot_1024");
                timerArtRoot = found as RectTransform;
            }

            if (rewardSegmentsRoot == null && timerArtRoot != null)
            {
                Transform found = timerArtRoot.Find("RewardSegmentsRoot");
                rewardSegmentsRoot = found as RectTransform;
            }

            if (rewardSegments == null || rewardSegments.Length != SegmentCount)
            {
                rewardSegments = new Image[SegmentCount];
            }

            if (rewardSegmentsRoot != null)
            {
                for (int i = 0; i < SegmentCount; i++)
                {
                    if (rewardSegments[i] != null)
                    {
                        continue;
                    }

                    string childName = $"RewardSegment_{i + 1:00}";
                    Transform child = rewardSegmentsRoot.Find(childName);
                    if (child != null)
                    {
                        rewardSegments[i] = child.GetComponent<Image>();
                    }
                }
            }

            if (topPenaltyLine == null && timerArtRoot != null)
            {
                Transform found = timerArtRoot.Find("06_TopPenaltyLine");
                if (found != null)
                {
                    topPenaltyLine = found.GetComponent<Image>();
                }
            }

            if (timerValue == null)
            {
                Transform found = transform.Find("TimerValue");
                if (found != null)
                {
                    timerValue = found.GetComponent<TMP_Text>();
                }
            }

            if (timerAdjustment == null)
            {
                Transform found = transform.Find("TimerAdjustment");
                if (found != null)
                {
                    timerAdjustment = found.GetComponent<TMP_Text>();
                }
            }

            if (comboWindowLine == null)
            {
                Transform found = transform.Find("ComboWindowLine");

                // Compatibility with the first V3 build, where the runtime line was
                // parented under TimerAdjustment and inherited its scale punch.
                if (found == null && timerAdjustment != null)
                {
                    found = timerAdjustment.transform.Find("ComboWindowLine");
                }

                if (found != null)
                {
                    comboWindowLine = found.GetComponent<Image>();
                }
            }

            _referencesReady = timerArtRoot != null
                && rewardSegmentsRoot != null
                && topPenaltyLine != null
                && timerValue != null
                && timerAdjustment != null
                && AllSegmentsAssigned();

            if (!_referencesReady && !_warnedMissingReferences)
            {
                _warnedMissingReferences = true;
                Debug.LogWarning(
                    "[AILURONE Timer V3] References are incomplete. " +
                    "Expected TimerArtRoot_1024/RewardSegmentsRoot, RewardSegment_01..09, " +
                    "06_TopPenaltyLine, TimerValue and TimerAdjustment.",
                    this);
            }
        }

        private void EnsureComboWindowLine()
        {
            if (timerAdjustment == null || !Application.isPlaying)
            {
                return;
            }

            if (comboWindowLine == null)
            {
                GameObject lineObject = new GameObject(
                    "ComboWindowLine",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

                lineObject.layer = timerAdjustment.gameObject.layer;
                comboWindowLine = lineObject.GetComponent<Image>();
                comboWindowLine.color = comboLineNormalColor;
                comboWindowLine.raycastTarget = false;
                comboWindowLine.maskable = true;
                comboWindowLine.type = Image.Type.Simple;
                comboWindowLine.gameObject.SetActive(false);
            }

            ConfigureComboWindowLineTransform();
        }

        private void ConfigureComboWindowLineTransform()
        {
            if (comboWindowLine == null || timerAdjustment == null)
            {
                return;
            }

            RectTransform lineRect = comboWindowLine.rectTransform;
            RectTransform adjustmentRect = timerAdjustment.rectTransform;

            // The underline must be a sibling of TimerAdjustment, not its child.
            // Otherwise every reward-number scale punch also enlarges the line.
            if (lineRect.parent != transform)
            {
                lineRect.SetParent(transform, false);
            }

            lineRect.anchorMin = adjustmentRect.anchorMin;
            lineRect.anchorMax = adjustmentRect.anchorMax;
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = adjustmentRect.anchoredPosition + new Vector2(0f, comboLineLocalY);
            lineRect.sizeDelta = new Vector2(comboLineFullWidth, comboLineHeight);
            lineRect.localScale = Vector3.one;
            lineRect.localRotation = Quaternion.identity;
            lineRect.SetAsLastSibling();
        }

        private bool AllSegmentsAssigned()
        {
            if (rewardSegments == null || rewardSegments.Length != SegmentCount)
            {
                return false;
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                if (rewardSegments[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void CaptureBaseState()
        {
            if (!_referencesReady)
            {
                return;
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                _segmentBaseScales[i] = rewardSegments[i].rectTransform.localScale;
            }

            _penaltyBaseSize = topPenaltyLine.rectTransform.sizeDelta;
            _timerValueBaseScale = timerValue.rectTransform.localScale;

            if (transform is RectTransform rootRect)
            {
                _rootBasePosition = rootRect.anchoredPosition;
            }
        }

        private void TryBindGameManager()
        {
            GameManager candidate = GameManager.Instance;
            if (candidate == null || candidate == _gameManager)
            {
                return;
            }

            UnbindGameManager();

            _gameManager = candidate;
            _gameManager.PendingTimeRewardChanged += HandlePendingTimeRewardChanged;
            _gameManager.TimeRewardCashInStarted += HandleTimeRewardCashInStarted;
            _gameManager.TimeRewardImpact += HandleTimeRewardImpact;
            _gameManager.TimePenaltyApplied += HandleTimePenaltyApplied;

            float currentPending = Mathf.Max(0f, _gameManager.PendingTimeReduction);
            _pendingSeconds = currentPending;
            ComputeFills(currentPending, _targetFills);

            for (int i = 0; i < SegmentCount; i++)
            {
                _displayFills[i] = _targetFills[i];
            }
        }

        private void UnbindGameManager()
        {
            if (_gameManager == null)
            {
                return;
            }

            _gameManager.PendingTimeRewardChanged -= HandlePendingTimeRewardChanged;
            _gameManager.TimeRewardCashInStarted -= HandleTimeRewardCashInStarted;
            _gameManager.TimeRewardImpact -= HandleTimeRewardImpact;
            _gameManager.TimePenaltyApplied -= HandleTimePenaltyApplied;
            _gameManager = null;
        }

        private void HandlePendingTimeRewardChanged(float previousSeconds, float currentSeconds)
        {
            if (_cashInActive && currentSeconds > previousSeconds + 0.001f)
            {
                CancelCashInForNewReward();
            }

            float[] previousFills = new float[SegmentCount];
            float[] currentFills = new float[SegmentCount];

            ComputeFills(previousSeconds, previousFills);
            ComputeFills(currentSeconds, currentFills);

            if (currentSeconds > previousSeconds + 0.001f)
            {
                bool punchedAnySegment = false;

                for (int i = 0; i < SegmentCount; i++)
                {
                    if (currentFills[i] > previousFills[i] + 0.001f)
                    {
                        _punchTimers[i] = newRewardPunchDuration;
                        punchedAnySegment = true;
                    }
                }

                // Once all nine bars are full, later rewards still need one clean feedback beat.
                if (!punchedAnySegment && currentSeconds > 9f)
                {
                    for (int i = 0; i < SegmentCount; i++)
                    {
                        if (currentFills[i] > 0.999f)
                        {
                            _punchTimers[i] = newRewardPunchDuration;
                        }
                    }
                }
            }

            _pendingSeconds = Mathf.Max(0f, currentSeconds);
            Array.Copy(currentFills, _targetFills, SegmentCount);
        }

        private void HandleTimeRewardCashInStarted(float seconds, float numberFlightDuration)
        {
            if (seconds <= 0f)
            {
                return;
            }

            _cashInActive = true;
            _cashInElapsed = 0f;
            _activeCashInDuration = Mathf.Max(0.12f, numberFlightDuration > 0f ? numberFlightDuration : cashInDuration);
            _pendingSeconds = 0f;

            for (int i = 0; i < SegmentCount; i++)
            {
                _cashStartFills[i] = Mathf.Max(_displayFills[i], _targetFills[i]);
                _targetFills[i] = 0f;
                _punchTimers[i] = 0f;
            }

            _cashOuterRing = FindOutermostActiveRing(_cashStartFills);

            if (comboWindowLine != null)
            {
                comboWindowLine.gameObject.SetActive(false);
            }
        }

        private void HandleTimeRewardImpact(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            _rewardImpactActive = true;
            _rewardImpactElapsed = 0f;
            _rewardImpactStrength = Mathf.Clamp01(
                (seconds - 1f) /
                Mathf.Max(0.001f, rewardImpactFullStrengthSeconds - 1f));
        }

        private void HandleTimePenaltyApplied(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            _penaltyActive = true;
            _penaltyElapsed = 0f;
            _penaltyExpandedWidth = Mathf.Clamp(
                Mathf.Max(
                    penaltyMinimumExpandedWidth,
                    _penaltyBaseSize.x + seconds * penaltyWidthPerSecond),
                _penaltyBaseSize.x,
                penaltyMaximumExpandedWidth);
        }

        private void UpdatePendingSegments(float dt)
        {
            float response = 1f - Mathf.Exp(-fillResponseSpeed * dt);

            for (int i = 0; i < SegmentCount; i++)
            {
                _displayFills[i] = Mathf.Lerp(_displayFills[i], _targetFills[i], response);

                float fill = Mathf.Clamp01(_displayFills[i]);
                rewardSegments[i].color = Color.Lerp(rewardDimColor, rewardBrightColor, fill);

                float punchEnvelope = UpdatePunchEnvelope(i, dt);
                float punchAmount = newRewardPunchScale;

                if (_pendingSeconds > 9f && fill > 0.999f)
                {
                    punchAmount += overflowRewardPunchScale;
                }

                rewardSegments[i].rectTransform.localScale =
                    _segmentBaseScales[i] * (1f + punchEnvelope * punchAmount);
            }
        }

        private float UpdatePunchEnvelope(int index, float dt)
        {
            if (_punchTimers[index] <= 0f || newRewardPunchDuration <= 0.001f)
            {
                _punchTimers[index] = 0f;
                return 0f;
            }

            _punchTimers[index] = Mathf.Max(0f, _punchTimers[index] - dt);
            float progress = 1f - _punchTimers[index] / newRewardPunchDuration;
            return Mathf.Sin(progress * Mathf.PI) * Mathf.Exp(-progress * 1.8f);
        }

        private void UpdateComboWindowLine()
        {
            if (comboWindowLine == null || _gameManager == null)
            {
                return;
            }

            bool shouldShow = !_cashInActive
                && _gameManager.PendingTimeReduction > 0f
                && timerAdjustment != null
                && timerAdjustment.gameObject.activeInHierarchy;

            if (!shouldShow)
            {
                comboWindowLine.gameObject.SetActive(false);
                return;
            }

            if (!comboWindowLine.gameObject.activeSelf)
            {
                comboWindowLine.gameObject.SetActive(true);
            }

            float remaining = Mathf.Clamp01(_gameManager.ComboWindowRemainingNormalized);
            float width = Mathf.Max(0f, comboLineFullWidth * remaining);

            ConfigureComboWindowLineTransform();

            RectTransform rect = comboWindowLine.rectTransform;
            rect.sizeDelta = new Vector2(width, comboLineHeight);
            rect.localScale = Vector3.one;

            float urgency = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.001f, comboLineUrgentThreshold));
            urgency = SmoothStep01(urgency);
            comboWindowLine.color = Color.Lerp(comboLineNormalColor, comboLineUrgentColor, urgency);
        }

        private void UpdateCashIn(float dt)
        {
            _cashInElapsed += dt;

            float duration = Mathf.Max(0.12f, _activeCashInDuration);
            float effectiveDelay = Mathf.Min(cashInRingDelay, duration * 0.10f);
            float perRingDuration = Mathf.Max(0.06f, duration - effectiveDelay * _cashOuterRing);

            for (int i = 0; i < SegmentCount; i++)
            {
                int ring = GetRingForSegment(i);
                float ringOrder = _cashOuterRing - ring;
                float startTime = Mathf.Max(0f, ringOrder) * effectiveDelay;
                float localT = Mathf.Clamp01((_cashInElapsed - startTime) / perRingDuration);

                if (_cashStartFills[i] <= 0.001f || ring > _cashOuterRing)
                {
                    rewardSegments[i].color = rewardDimColor;
                    rewardSegments[i].rectTransform.localScale = _segmentBaseScales[i];
                    _displayFills[i] = 0f;
                    continue;
                }

                if (_cashInElapsed < startTime)
                {
                    rewardSegments[i].color = Color.Lerp(
                        rewardDimColor,
                        rewardBrightColor,
                        Mathf.Clamp01(_cashStartFills[i]));
                    rewardSegments[i].rectTransform.localScale = _segmentBaseScales[i];
                    _displayFills[i] = _cashStartFills[i];
                    continue;
                }

                float flashEnvelope = Mathf.Sin(localT * Mathf.PI);
                float collapse = 1f - SmoothStep01(localT);
                float scale = Mathf.Lerp(cashInFlashScale, 0f, SmoothStep01(localT));

                Color activeBase = Color.Lerp(
                    rewardDimColor,
                    rewardBrightColor,
                    Mathf.Clamp01(_cashStartFills[i]));

                rewardSegments[i].color = Color.Lerp(activeBase, rewardFlashColor, flashEnvelope);
                rewardSegments[i].rectTransform.localScale = _segmentBaseScales[i] * scale;
                _displayFills[i] = _cashStartFills[i] * collapse;
            }

            if (_cashInElapsed >= duration)
            {
                _cashInActive = false;
                _cashInElapsed = 0f;

                for (int i = 0; i < SegmentCount; i++)
                {
                    _cashStartFills[i] = 0f;
                    _displayFills[i] = 0f;
                    _targetFills[i] = 0f;
                    rewardSegments[i].color = rewardDimColor;
                    rewardSegments[i].rectTransform.localScale = _segmentBaseScales[i];
                }
            }
        }

        private void CancelCashInForNewReward()
        {
            _cashInActive = false;
            _cashInElapsed = 0f;

            for (int i = 0; i < SegmentCount; i++)
            {
                _cashStartFills[i] = 0f;
                rewardSegments[i].rectTransform.localScale = _segmentBaseScales[i];
            }
        }

        private void UpdateRewardImpact(float dt)
        {
            if (!_rewardImpactActive)
            {
                _rewardImpactScaleAdd = 0f;
                _rewardImpactKickX = 0f;
                return;
            }

            _rewardImpactElapsed += dt;
            float t = Mathf.Clamp01(_rewardImpactElapsed / Mathf.Max(0.001f, rewardImpactDuration));

            float amplitude = Mathf.Lerp(
                rewardImpactMinimumScale,
                rewardImpactMaximumScale,
                _rewardImpactStrength);

            float kickAmplitude = Mathf.Lerp(
                rewardImpactMinimumKick,
                rewardImpactMaximumKick,
                _rewardImpactStrength);

            float punch;
            if (t < 0.18f)
            {
                punch = SmoothStep01(t / 0.18f);
            }
            else
            {
                float release = Mathf.Clamp01((t - 0.18f) / 0.82f);
                float decay = 1f - SmoothStep01(release);
                float rebound = 0.84f + 0.16f * Mathf.Cos(release * Mathf.PI * 4f);
                punch = decay * rebound;
            }

            _rewardImpactScaleAdd = punch * amplitude;
            _rewardImpactKickX =
                Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * kickAmplitude;

            if (_rewardImpactElapsed >= rewardImpactDuration)
            {
                _rewardImpactActive = false;
                _rewardImpactElapsed = 0f;
                _rewardImpactScaleAdd = 0f;
                _rewardImpactKickX = 0f;
            }
        }

        private void UpdatePenalty(float dt)
        {
            if (!_penaltyActive)
            {
                topPenaltyLine.color = penaltyIdleColor;
                SetPenaltyLineWidth(_penaltyBaseSize.x);
                _penaltyScaleAdd = 0f;
                _penaltyKickX = 0f;
                return;
            }

            _penaltyElapsed += dt;
            float t = Mathf.Clamp01(_penaltyElapsed / Mathf.Max(0.001f, penaltyDuration));

            float expand;
            if (t < 0.24f)
            {
                expand = SmoothStep01(t / 0.24f);
            }
            else if (t < 0.52f)
            {
                expand = 1f;
            }
            else
            {
                expand = 1f - SmoothStep01((t - 0.52f) / 0.48f);
            }

            float flash = Mathf.Sin(t * Mathf.PI);
            float width = Mathf.Lerp(_penaltyBaseSize.x, _penaltyExpandedWidth, expand);
            SetPenaltyLineWidth(width);
            topPenaltyLine.color = Color.Lerp(penaltyIdleColor, penaltyActiveColor, Mathf.Max(expand, flash));

            _penaltyScaleAdd = flash * penaltyTimerPunchScale;
            _penaltyKickX =
                Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * penaltyHorizontalKick;

            if (_penaltyElapsed >= penaltyDuration)
            {
                _penaltyActive = false;
                _penaltyElapsed = 0f;
                topPenaltyLine.color = penaltyIdleColor;
                SetPenaltyLineWidth(_penaltyBaseSize.x);
                _penaltyScaleAdd = 0f;
                _penaltyKickX = 0f;
            }
        }

        private void ApplyCombinedTimerTransform()
        {
            if (timerValue != null)
            {
                float combinedScale = 1f + _penaltyScaleAdd + _rewardImpactScaleAdd;
                timerValue.rectTransform.localScale = _timerValueBaseScale * combinedScale;
            }

            if (transform is RectTransform rootRect)
            {
                rootRect.anchoredPosition =
                    _rootBasePosition + new Vector2(_penaltyKickX + _rewardImpactKickX, 0f);
            }
        }

        private void SetPenaltyLineWidth(float width)
        {
            RectTransform rect = topPenaltyLine.rectTransform;
            Vector2 size = rect.sizeDelta;
            size.x = width;
            size.y = _penaltyBaseSize.y;
            rect.sizeDelta = size;
        }

        private void ApplyImmediateDefaultState()
        {
            if (!_referencesReady)
            {
                return;
            }

            _pendingSeconds = 0f;
            _cashInActive = false;
            _rewardImpactActive = false;
            _penaltyActive = false;
            _rewardImpactScaleAdd = 0f;
            _rewardImpactKickX = 0f;
            _penaltyScaleAdd = 0f;
            _penaltyKickX = 0f;

            for (int i = 0; i < SegmentCount; i++)
            {
                _displayFills[i] = 0f;
                _targetFills[i] = 0f;
                _cashStartFills[i] = 0f;
                _punchTimers[i] = 0f;
                rewardSegments[i].color = rewardDimColor;
                rewardSegments[i].rectTransform.localScale = _segmentBaseScales[i];
            }

            if (comboWindowLine != null)
            {
                comboWindowLine.gameObject.SetActive(false);
                comboWindowLine.color = comboLineNormalColor;
                comboWindowLine.rectTransform.anchoredPosition = new Vector2(0f, comboLineLocalY);
                comboWindowLine.rectTransform.sizeDelta = new Vector2(comboLineFullWidth, comboLineHeight);
            }

            topPenaltyLine.color = penaltyIdleColor;
            SetPenaltyLineWidth(_penaltyBaseSize.x);

            if (timerValue != null)
            {
                timerValue.rectTransform.localScale = _timerValueBaseScale;
            }

            if (transform is RectTransform rootRect)
            {
                rootRect.anchoredPosition = _rootBasePosition;
            }
        }

        private static void ComputeFills(float pendingSeconds, float[] output)
        {
            if (output == null || output.Length < SegmentCount)
            {
                return;
            }

            Array.Clear(output, 0, output.Length);
            float seconds = Mathf.Max(0f, pendingSeconds);

            // Centre segment: 0..1 second.
            output[4] = Mathf.Clamp01(seconds);

            // Symmetric pairs: each pair represents two seconds and both sides receive
            // the same brightness. At 2 seconds, segment 04 and 06 are both 50%.
            float firstPair = Mathf.Clamp01((seconds - 1f) / 2f);
            output[3] = firstPair;
            output[5] = firstPair;

            float secondPair = Mathf.Clamp01((seconds - 3f) / 2f);
            output[2] = secondPair;
            output[6] = secondPair;

            float thirdPair = Mathf.Clamp01((seconds - 5f) / 2f);
            output[1] = thirdPair;
            output[7] = thirdPair;

            float fourthPair = Mathf.Clamp01((seconds - 7f) / 2f);
            output[0] = fourthPair;
            output[8] = fourthPair;
        }

        private static int GetRingForSegment(int segmentIndex)
        {
            return Mathf.Abs(segmentIndex - 4);
        }

        private static int FindOutermostActiveRing(float[] fills)
        {
            int outerRing = 0;
            for (int i = 0; i < SegmentCount; i++)
            {
                if (fills[i] > 0.001f)
                {
                    outerRing = Mathf.Max(outerRing, GetRingForSegment(i));
                }
            }

            return outerRing;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
