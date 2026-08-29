#pragma warning disable 0618
#pragma warning disable 0414
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Bottom-centre score HUD with:
    /// - minimal TOTAL SCORE label
    /// - main score value
    /// - optional combo multiplier text
    /// - centre-out energy bar that heats to red on scoring,
    ///   cools toward cyan/blue, then returns to idle grey.
    ///
    /// GameManager remains the authoritative gameplay source.
    /// This component only reads:
    /// - scoreTextUI output (already written by GameManager)
    /// - currentComboTier / comboMultipliers for display.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONEScoreHUDVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private RectTransform comboRoot;
        [SerializeField] private Image idleTrack;
        [SerializeField] private Image[] leftSegments = System.Array.Empty<Image>();
        [SerializeField] private Image[] rightSegments = System.Array.Empty<Image>();

        [Header("Score Roll")]
        [SerializeField, Min(0.05f)] private float rollDuration = 0.22f;

        [Header("Score Pulse")]
        [SerializeField, Min(0.05f)] private float scorePulseDuration = 0.20f;
        [SerializeField, Range(0f, 0.15f)] private float scorePulseScale = 0.05f;

        [Header("Bar Behaviour")]
        [SerializeField, Min(0.05f)] private float hotHoldDuration = 1.50f;
        [SerializeField, Min(0.05f)] private float coolToBlueDuration = 2.20f;
        [SerializeField, Min(0.05f)] private float blueHoldDuration = 1.20f;
        [SerializeField, Min(0.05f)] private float idleReturnDuration = 1.60f;
        [SerializeField, Range(0f, 1f)] private float idleFill = 0.04f;
        [SerializeField, Range(0f, 1f)] private float blueHoldFill = 0.24f;
        [SerializeField, Range(0f, 1f)] private float scoreGainToFill = 0.11f;
        [SerializeField, Range(0f, 1f)] private float comboGainBonus = 0.08f;
        [SerializeField, Range(0f, 1f)] private float maxVisualFill = 1f;
        [SerializeField, Range(0f, 0.5f)] private float segmentHeightPulse = 0.24f;

        [Header("Colours")]
        [SerializeField] private Color idleTrackColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color idleLabelColor = new Color(1f, 1f, 1f, 0.58f);
        [SerializeField] private Color idleScoreColor = new Color(1f, 1f, 1f, 0.98f);
        [SerializeField] private Color hotColor = new Color(1f, 0.23f, 0.34f, 1f);
        [SerializeField] private Color coolColor = new Color(0.22f, 0.88f, 1f, 1f);
        [SerializeField] private Color comboTextColor = new Color(0.82f, 0.95f, 1f, 0.88f);

        private enum BarState
        {
            Idle,
            Hot,
            CoolingToBlue,
            BlueHold,
            ReturningToIdle
        }

        private GameManager _gameManager;

        private bool _initialized;
        private int _targetScore;
        private float _displayedScore;
        private float _rollStartScore;
        private float _rollTimer;
        private float _scorePulseTimer;

        private int _lastScoreValue;
        private float _barPeakFill;
        private float _currentBarFill;
        private float _barStateTimer;
        private BarState _barState = BarState.Idle;

        private Vector3 _rootBaseScale = Vector3.one;
        private Vector2 _rootBasePosition;
        private Vector2[] _leftBaseSizes = System.Array.Empty<Vector2>();
        private Vector2[] _rightBaseSizes = System.Array.Empty<Vector2>();
        private AILURONEContinuousArcGraphic _idleArc;

        public void Configure(
            RectTransform root,
            TMP_Text label,
            TMP_Text value,
            TMP_Text combo,
            RectTransform comboBadge,
            Image baseTrack,
            Image[] left,
            Image[] right)
        {
            visualRoot = root;
            scoreLabel = label;
            scoreText = value;
            comboText = combo;
            comboRoot = comboBadge;
            idleTrack = baseTrack;
            leftSegments = left ?? System.Array.Empty<Image>();
            rightSegments = right ?? System.Array.Empty<Image>();

            ApplyApprovedPresentation();
            CacheBaseState();
            ResetRuntimeState();
            ApplyVisualState(immediate: true);
        }

        private void Awake()
        {
            ApplyApprovedPresentation();
            CacheBaseState();
            ResetRuntimeState();
            ApplyVisualState(immediate: true);
        }

        private void OnEnable()
        {
            ApplyApprovedPresentation();
            CacheBaseState();
            ResetRuntimeState();
            ApplyVisualState(immediate: true);
        }

        private void ApplyApprovedPresentation()
        {
            idleTrackColor =
                new Color(1f, 1f, 1f, 0.20f);

            idleLabelColor =
                AILURONEHUDRuntimeStyle.MutedWhite;

            idleScoreColor =
                AILURONEHUDRuntimeStyle.White;

            hotColor = AILURONEHUDRuntimeStyle.Yellow;
            coolColor = AILURONEHUDRuntimeStyle.Cyan;
            comboTextColor = AILURONEHUDRuntimeStyle.White;

            AILURONEHUDRuntimeStyle.ApplyScore(
                visualRoot,
                scoreLabel,
                scoreText,
                comboText,
                idleTrack,
                leftSegments,
                rightSegments);

            Transform idleArcTransform = visualRoot != null
                ? visualRoot.Find("Approved_ScoreIdleArc")
                : null;

            _idleArc = idleArcTransform != null
                ? idleArcTransform
                    .GetComponent<AILURONEContinuousArcGraphic>()
                : null;
        }

        private void OnDisable()
        {
            RestoreBaseSegmentSizes();
        }

        private void LateUpdate()
        {
            if (scoreText == null)
            {
                return;
            }

            ResolveGameManager();
            ReadAuthoritativeScore();
            UpdateScoreRoll();
            UpdateBarState();
            UpdateComboText();
            WriteFormattedScore();
            ApplyVisualState(immediate: false);
        }

        private void ResolveGameManager()
        {
            if (_gameManager == null)
            {
                _gameManager = GameManager.Instance != null
                    ? GameManager.Instance
                    : FindAnyObjectByType<GameManager>();
            }
        }

        private void ReadAuthoritativeScore()
        {
            if (!TryParseScore(scoreText.text, out int parsedScore))
            {
                return;
            }

            if (!_initialized)
            {
                _initialized = true;
                _targetScore = parsedScore;
                _displayedScore = parsedScore;
                _rollStartScore = parsedScore;
                _lastScoreValue = parsedScore;
                _currentBarFill = idleFill;
                _barPeakFill = idleFill;
                return;
            }

            if (parsedScore != _targetScore)
            {
                int delta = parsedScore - _targetScore;

                _rollStartScore = _displayedScore;
                _targetScore = parsedScore;
                _rollTimer = rollDuration;
                _scorePulseTimer = scorePulseDuration;

                if (delta > 0)
                {
                    RegisterPositiveScoreGain(delta);
                }
            }

            _lastScoreValue = parsedScore;
        }

        private void RegisterPositiveScoreGain(int delta)
        {
            int comboTier = 0;

            if (_gameManager != null)
            {
                comboTier = Mathf.Max(0, _gameManager.currentComboTier);

                if (_gameManager.comboMultipliers != null && _gameManager.comboMultipliers.Length > 0)
                {
                }
            }

            float normalizedGain = Mathf.Clamp01(delta / 150f);
            float gainFill = 0.12f + normalizedGain * scoreGainToFill + comboTier * comboGainBonus;

            _barPeakFill = Mathf.Clamp(
                Mathf.Max(_barPeakFill * 0.72f, _currentBarFill) + gainFill,
                0.18f,
                maxVisualFill
            );

            _barState = BarState.Hot;
            _barStateTimer = 0f;
        }

        private void UpdateScoreRoll()
        {
            if (!_initialized)
            {
                return;
            }

            if (_rollTimer <= 0f)
            {
                _displayedScore = _targetScore;
                return;
            }

            _rollTimer -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(_rollTimer / Mathf.Max(0.05f, rollDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _displayedScore = Mathf.Lerp(_rollStartScore, _targetScore, eased);

            if (_rollTimer <= 0f)
            {
                _displayedScore = _targetScore;
            }
        }

        private void UpdateBarState()
        {
            _barStateTimer += Time.unscaledDeltaTime;

            switch (_barState)
            {
                case BarState.Idle:
                    _currentBarFill = Mathf.Lerp(_currentBarFill, idleFill, Time.unscaledDeltaTime * 5f);
                    break;

                case BarState.Hot:
                    _currentBarFill = Mathf.Lerp(_currentBarFill, _barPeakFill, Time.unscaledDeltaTime * 10f);
                    if (_barStateTimer >= hotHoldDuration)
                    {
                        _barState = BarState.CoolingToBlue;
                        _barStateTimer = 0f;
                    }
                    break;

                case BarState.CoolingToBlue:
                {
                    float t = Mathf.Clamp01(_barStateTimer / Mathf.Max(0.05f, coolToBlueDuration));
                    float target = Mathf.Lerp(_barPeakFill, blueHoldFill, t);
                    _currentBarFill = Mathf.Lerp(_currentBarFill, target, Time.unscaledDeltaTime * 7.5f);
                    if (t >= 1f)
                    {
                        _barState = BarState.BlueHold;
                        _barStateTimer = 0f;
                    }
                    break;
                }

                case BarState.BlueHold:
                    _currentBarFill = Mathf.Lerp(_currentBarFill, blueHoldFill, Time.unscaledDeltaTime * 6f);
                    if (_barStateTimer >= blueHoldDuration)
                    {
                        _barState = BarState.ReturningToIdle;
                        _barStateTimer = 0f;
                    }
                    break;

                case BarState.ReturningToIdle:
                {
                    float t = Mathf.Clamp01(_barStateTimer / Mathf.Max(0.05f, idleReturnDuration));
                    float target = Mathf.Lerp(blueHoldFill, idleFill, t);
                    _currentBarFill = Mathf.Lerp(_currentBarFill, target, Time.unscaledDeltaTime * 6f);
                    if (t >= 1f)
                    {
                        _barState = BarState.Idle;
                        _barStateTimer = 0f;
                        _barPeakFill = idleFill;
                    }
                    break;
                }
            }

            _currentBarFill = Mathf.Clamp01(_currentBarFill);

            if (_scorePulseTimer > 0f)
            {
                _scorePulseTimer -= Time.unscaledDeltaTime;
            }
        }

        private void UpdateComboText()
        {
            if (comboText == null)
            {
                return;
            }

            float multiplier = 1f;
            bool show = false;

            if (_gameManager != null && _gameManager.comboMultipliers != null && _gameManager.comboMultipliers.Length > 0)
            {
                int tier = Mathf.Clamp(_gameManager.currentComboTier, 0, _gameManager.comboMultipliers.Length - 1);
                multiplier = _gameManager.comboMultipliers[tier];
                show = multiplier > 1.01f;
            }

            GameObject comboDisplay =
                comboRoot != null
                    ? comboRoot.gameObject
                    : comboText.gameObject;

            comboDisplay.SetActive(show);

            if (show)
            {
                string accentHex = ColorUtility.ToHtmlStringRGB(hotColor);
                comboText.text = "<color=#" + accentHex + ">|</color> ×"
                    + multiplier.ToString("F1", CultureInfo.InvariantCulture);
                comboText.color = comboTextColor;
            }
        }

        private void WriteFormattedScore()
        {
            if (!_initialized || scoreText == null)
            {
                return;
            }

            int rounded = Mathf.RoundToInt(_displayedScore);
            scoreText.text = rounded.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void ApplyVisualState(bool immediate)
        {
            if (scoreLabel != null)
            {
                scoreLabel.color = idleLabelColor;
            }

            if (scoreText != null)
            {
                float pulseT = scorePulseDuration > 0f
                    ? Mathf.Clamp01(_scorePulseTimer / scorePulseDuration)
                    : 0f;
                float pulseEnvelope = Mathf.Sin((1f - pulseT) * Mathf.PI);
                scoreText.color = Color.Lerp(idleScoreColor, Color.white, pulseEnvelope * 0.18f);

                if (visualRoot != null)
                {
                    float scale = 1f + pulseEnvelope * scorePulseScale;
                    visualRoot.localScale = _rootBaseScale * scale;

                    Vector2 motion = Application.isPlaying
                        ? AILURONEHUDMotionSignal.GetOffset(0.55f)
                        : Vector2.zero;

                    visualRoot.anchoredPosition =
                        _rootBasePosition + motion;
                }
            }

            if (idleTrack != null)
            {
                idleTrack.color = idleTrackColor;
            }

            if (_idleArc != null)
            {
                _idleArc.color = idleTrackColor;
                _idleArc.enabled = true;
            }

            Color currentBarColor = EvaluateBarColor();
            float pulseHeightMul = 1f + EvaluatePulseEnvelope() * segmentHeightPulse;
            ApplySegments(leftSegments, _leftBaseSizes, currentBarColor, _currentBarFill, pulseHeightMul, invertAlphaGradient: false);
            ApplySegments(rightSegments, _rightBaseSizes, currentBarColor, _currentBarFill, pulseHeightMul, invertAlphaGradient: false);
        }

        private Color EvaluateBarColor()
        {
            switch (_barState)
            {
                default:
                case BarState.Idle:
                    return Color.Lerp(idleTrackColor, Color.white, 0.18f);

                case BarState.Hot:
                    return hotColor;

                case BarState.CoolingToBlue:
                {
                    float t = Mathf.Clamp01(_barStateTimer / Mathf.Max(0.05f, coolToBlueDuration));
                    return Color.Lerp(hotColor, coolColor, t);
                }

                case BarState.BlueHold:
                    return coolColor;

                case BarState.ReturningToIdle:
                {
                    float t = Mathf.Clamp01(_barStateTimer / Mathf.Max(0.05f, idleReturnDuration));
                    return Color.Lerp(coolColor, Color.Lerp(idleTrackColor, Color.white, 0.18f), t);
                }
            }
        }

        private float EvaluatePulseEnvelope()
        {
            if (_scorePulseTimer <= 0f || scorePulseDuration <= 0f)
            {
                return 0f;
            }

            float t = 1f - Mathf.Clamp01(_scorePulseTimer / scorePulseDuration);
            return Mathf.Sin(t * Mathf.PI);
        }

        private void ApplySegments(Image[] segments, Vector2[] baseSizes, Color color, float fill, float pulseHeightMul, bool invertAlphaGradient)
        {
            if (segments == null || segments.Length == 0)
            {
                return;
            }

            float scaled = Mathf.Clamp01(fill) * segments.Length;
            int fullSegments = Mathf.FloorToInt(scaled);
            float partial = scaled - fullSegments;

            for (int index = 0; index < segments.Length; index++)
            {
                Image segment = segments[index];
                if (segment == null)
                {
                    continue;
                }

                float localFill = 0f;
                if (index < fullSegments)
                {
                    localFill = 1f;
                }
                else if (index == fullSegments)
                {
                    localFill = partial;
                }

                float gradientAlpha = Mathf.Lerp(1f, 0.18f, index / Mathf.Max(1f, segments.Length - 1f));
                if (invertAlphaGradient)
                {
                    gradientAlpha = Mathf.Lerp(0.18f, 1f, index / Mathf.Max(1f, segments.Length - 1f));
                }

                Color segmentColor = color;
                segmentColor.a = localFill <= 0.001f
                    ? 0f
                    : Mathf.Lerp(0.08f, 0.98f, localFill) * gradientAlpha;
                segment.color = segmentColor;

                RectTransform rect = segment.rectTransform;
                Vector2 baseSize = (baseSizes != null && index < baseSizes.Length && baseSizes[index] != Vector2.zero)
                    ? baseSizes[index]
                    : rect.sizeDelta;

                rect.sizeDelta = new Vector2(baseSize.x, baseSize.y * Mathf.Lerp(1f, pulseHeightMul, localFill));
            }
        }

        private void CacheBaseState()
        {
            if (visualRoot != null)
            {
                _rootBaseScale = visualRoot.localScale;
                _rootBasePosition = visualRoot.anchoredPosition;
            }

            _leftBaseSizes = CaptureSizes(leftSegments);
            _rightBaseSizes = CaptureSizes(rightSegments);
        }

        private static Vector2[] CaptureSizes(Image[] images)
        {
            if (images == null)
            {
                return System.Array.Empty<Vector2>();
            }

            Vector2[] sizes = new Vector2[images.Length];
            for (int i = 0; i < images.Length; i++)
            {
                sizes[i] = images[i] != null ? images[i].rectTransform.sizeDelta : Vector2.zero;
            }
            return sizes;
        }

        private void ResetRuntimeState()
        {
            _initialized = false;
            _targetScore = 0;
            _displayedScore = 0f;
            _rollStartScore = 0f;
            _rollTimer = 0f;
            _scorePulseTimer = 0f;
            _lastScoreValue = 0;
            _barPeakFill = idleFill;
            _currentBarFill = idleFill;
            _barStateTimer = 0f;
            _barState = BarState.Idle;
        }

        private void RestoreBaseSegmentSizes()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = _rootBaseScale;
                visualRoot.anchoredPosition = _rootBasePosition;
            }

            RestoreSizes(leftSegments, _leftBaseSizes);
            RestoreSizes(rightSegments, _rightBaseSizes);
        }

        private static void RestoreSizes(Image[] images, Vector2[] sizes)
        {
            if (images == null || sizes == null)
            {
                return;
            }

            for (int i = 0; i < images.Length && i < sizes.Length; i++)
            {
                if (images[i] != null)
                {
                    images[i].rectTransform.sizeDelta = sizes[i];
                }
            }
        }

        private static bool TryParseScore(string text, out int score)
        {
            score = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string cleaned = text.Replace(",", string.Empty).Trim();
            if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out score))
            {
                return true;
            }

            if (float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatScore))
            {
                score = Mathf.RoundToInt(floatScore);
                return true;
            }

            return false;
        }
    }
}
