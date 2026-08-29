#pragma warning disable 0618
#pragma warning disable 0414
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Reference Timer v1.1 response layer.
    ///
    /// GameManager remains authoritative for time, reward cash-in, penalties,
    /// number formatting and adjustment-text movement.
    ///
    /// This component detects real jumps in the displayed timer:
    /// - backward jump = time reward
    /// - large forward jump = time penalty
    ///
    /// It then animates the reference-style frame without changing timing logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONETimerHUDVisual : MonoBehaviour
    {
        private enum SignalType
        {
            None,
            Reward,
            Penalty
        }

        [Header("References")]
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text adjustmentText;

        [SerializeField] private Image[] outerFrameLines =
            Array.Empty<Image>();

        [SerializeField] private Image[] innerFrameLines =
            Array.Empty<Image>();

        [SerializeField] private Image[] cyanTicks =
            Array.Empty<Image>();

        [SerializeField] private Image magentaMark;

        private Graphic _timerMainArc;
        private Graphic _timerStateArc;

        private Color _timerMainArcBaseColor =
            new Color(1f, 1f, 1f, 0.48f);

        private Color _timerStateArcBaseColor =
            new Color(0.18f, 0.93f, 1f, 0.30f);

        [Header("Signal Colours")]
        [SerializeField] private Color rewardColor =
            new Color(0.18f, 0.93f, 1.00f, 1f);

        [SerializeField] private Color penaltyColor =
            new Color(1.00f, 0.08f, 0.40f, 1f);

        [Header("Reward Response")]
        [Min(0.1f)]
        [SerializeField] private float rewardDuration = 0.42f;

        [Range(0f, 1.2f)]
        [SerializeField] private float rewardTickHeightBoost = 0.72f;

        [Range(0f, 0.15f)]
        [SerializeField] private float rewardTimerScale = 0.060f;

        [Range(0f, 1f)]
        [SerializeField] private float rewardInnerLineTint = 0.86f;

        [Header("Penalty Response")]
        [Min(0.1f)]
        [SerializeField] private float penaltyDuration = 0.50f;

        [Min(0f)]
        [SerializeField] private float penaltyMarkWidthBoost = 32f;

        [Min(0f)]
        [SerializeField] private float penaltyMarkHeightBoost = 1.4f;

        [Range(0f, 0.15f)]
        [SerializeField] private float penaltyTimerScale = 0.050f;

        [Range(0f, 4f)]
        [SerializeField] private float penaltyHorizontalKick = 1.5f;

        [Range(0f, 1f)]
        [SerializeField] private float penaltyInnerLineTint = 0.90f;

        [Header("Pending Reward Charge")]
        [Range(0f, 0.5f)]
        [SerializeField] private float pendingTickHeightBoost = 0.14f;

        [Range(0f, 1f)]
        [SerializeField] private float pendingTickBrightness = 0.58f;

        [Min(0.1f)]
        [SerializeField] private float pendingPulseSpeed = 6.5f;

        [Header("Detection")]
        [Min(0.01f)]
        [SerializeField] private float rewardJumpThreshold = 0.04f;

        [Min(0.05f)]
        [SerializeField] private float minimumPenaltyJump = 0.20f;

        [Min(0f)]
        [SerializeField] private float penaltyFrameTolerance = 0.12f;

        private Color[] _outerBaseColors =
            Array.Empty<Color>();

        private Color[] _innerBaseColors =
            Array.Empty<Color>();

        private Color[] _tickBaseColors =
            Array.Empty<Color>();

        private float[] _tickBaseHeights =
            Array.Empty<float>();

        private Vector2 _markBaseSize;
        private Color _markBaseColor;

        private Vector2 _rootBasePosition;
        private Vector3 _timerBaseScale = Vector3.one;

        private SignalType _signalType = SignalType.None;
        private float _signalTimer;
        private float _signalDuration;

        private bool _hasPreviousTime;
        private float _previousDisplayedTime;

        private float _pendingAmount;

        public void Configure(
            RectTransform root,
            TMP_Text value,
            TMP_Text adjustment,
            Image[] outerLines,
            Image[] innerLines,
            Image[] ticks,
            Image topMark
        )
        {
            visualRoot = root;
            timerText = value;
            adjustmentText = adjustment;

            outerFrameLines =
                outerLines ?? Array.Empty<Image>();

            innerFrameLines =
                innerLines ?? Array.Empty<Image>();

            cyanTicks =
                ticks ?? Array.Empty<Image>();

            magentaMark = topMark;

            ApplyApprovedPresentation();
            CaptureBaseGeometry();
            ResetDetection();
            ApplyImmediateNormalState();
        }

        private void Awake()
        {
            ApplyApprovedPresentation();
            CaptureBaseGeometry();
            ResetDetection();
            ApplyImmediateNormalState();
        }

        private void OnEnable()
        {
            ApplyApprovedPresentation();
            CaptureBaseGeometry();
            ResetDetection();
            ApplyImmediateNormalState();
        }

        private void ApplyApprovedPresentation()
        {
            rewardColor = AILURONEHUDRuntimeStyle.Cyan;
            penaltyColor = AILURONEHUDRuntimeStyle.Red;

            AILURONEHUDRuntimeStyle.ApplyTimer(
                visualRoot,
                timerText,
                adjustmentText,
                outerFrameLines,
                innerFrameLines,
                cyanTicks,
                magentaMark);

            ResolveApprovedArcs();
        }

        private void Update()
        {
            if (timerText == null)
            {
                return;
            }

            DetectTimerJump();
            UpdatePendingReward();

            if (_signalTimer > 0f)
            {
                _signalTimer -=
                    Time.unscaledDeltaTime;

                if (_signalTimer <= 0f)
                {
                    _signalTimer = 0f;
                    _signalType = SignalType.None;
                }
            }

            Render();
        }

        private void OnDisable()
        {
            _signalType = SignalType.None;
            _signalTimer = 0f;
            _pendingAmount = 0f;

            ApplyImmediateNormalState();
        }

        private void DetectTimerJump()
        {
            if (!TryParseTimer(
                    timerText.text,
                    out float displayedTime
                ))
            {
                return;
            }

            if (!_hasPreviousTime)
            {
                _previousDisplayedTime = displayedTime;
                _hasPreviousTime = true;
                return;
            }

            float delta =
                displayedTime - _previousDisplayedTime;

            float expectedFrameAdvance =
                Mathf.Max(
                    0f,
                    Time.unscaledDeltaTime
                );

            float penaltyThreshold =
                Mathf.Max(
                    minimumPenaltyJump,
                    expectedFrameAdvance
                        + penaltyFrameTolerance
                );

            if (delta <= -rewardJumpThreshold)
            {
                TriggerSignal(SignalType.Reward);
            }
            else if (delta >= penaltyThreshold)
            {
                TriggerSignal(SignalType.Penalty);
            }

            _previousDisplayedTime = displayedTime;
        }

        private void UpdatePendingReward()
        {
            bool pendingReward =
                adjustmentText != null
                && adjustmentText.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(
                    adjustmentText.text
                )
                && adjustmentText.text.Contains("-");

            float target =
                pendingReward ? 1f : 0f;

            float blend =
                1f - Mathf.Exp(
                    -9f * Time.unscaledDeltaTime
                );

            _pendingAmount =
                Mathf.Lerp(
                    _pendingAmount,
                    target,
                    blend
                );
        }

        private void TriggerSignal(
            SignalType type
        )
        {
            if (type == SignalType.None)
            {
                return;
            }

            _signalType = type;

            _signalDuration =
                type == SignalType.Reward
                    ? rewardDuration
                    : penaltyDuration;

            _signalTimer =
                _signalDuration;
        }

        private void Render()
        {
            float progress =
                _signalDuration <= 0.001f
                    ? 1f
                    : 1f - Mathf.Clamp01(
                        _signalTimer
                        / _signalDuration
                    );

            float envelope =
                _signalType == SignalType.None
                    ? 0f
                    : Mathf.Sin(
                        progress * Mathf.PI
                    );

            float rewardEnvelope =
                _signalType == SignalType.Reward
                    ? envelope
                    : 0f;

            float penaltyEnvelope =
                _signalType == SignalType.Penalty
                    ? envelope
                    : 0f;

            RenderOuterFrame(
                rewardEnvelope,
                penaltyEnvelope
            );

            RenderInnerFrame(
                rewardEnvelope,
                penaltyEnvelope
            );

            RenderTicks(
                progress,
                rewardEnvelope,
                penaltyEnvelope
            );

            RenderTopMark(
                penaltyEnvelope
            );

            RenderTimerScale(
                rewardEnvelope,
                penaltyEnvelope
            );

            RenderPenaltyKick(
                progress,
                penaltyEnvelope
            );

            RenderApprovedArcs(
                rewardEnvelope,
                penaltyEnvelope
            );
        }

        private void ResolveApprovedArcs()
        {
            _timerMainArc =
                FindApprovedArc("Approved_TimerMainArc");

            _timerStateArc =
                FindApprovedArc("Approved_TimerStateArc");

            _timerMainArcBaseColor =
                new Color(1f, 1f, 1f, 0.48f);

            _timerStateArcBaseColor =
                new Color(
                    rewardColor.r,
                    rewardColor.g,
                    rewardColor.b,
                    0.30f
                );

            RestoreApprovedArcs();
        }

        private Graphic FindApprovedArc(
            string childName
        )
        {
            if (visualRoot == null)
            {
                return null;
            }

            Transform child =
                visualRoot.Find(childName);

            return child != null
                ? child.GetComponent<Graphic>()
                : null;
        }

        private void RenderApprovedArcs(
            float rewardEnvelope,
            float penaltyEnvelope
        )
        {
            float eventEnvelope =
                Mathf.Max(
                    rewardEnvelope,
                    penaltyEnvelope
                );

            if (_timerMainArc != null)
            {
                Color mainColor =
                    _timerMainArcBaseColor;

                mainColor.a =
                    Mathf.Lerp(
                        _timerMainArcBaseColor.a,
                        0.62f,
                        eventEnvelope
                    );

                _timerMainArc.color = mainColor;
            }

            if (_timerStateArc == null)
            {
                return;
            }

            float pendingWave =
                0.5f
                + 0.5f
                * Mathf.Sin(
                    Time.unscaledTime
                    * pendingPulseSpeed
                );

            float pendingPulse =
                _pendingAmount
                * Mathf.Lerp(
                    0.04f,
                    0.12f,
                    pendingWave
                );

            Color eventColor =
                penaltyEnvelope > rewardEnvelope
                    ? penaltyColor
                    : rewardColor;

            Color stateColor =
                Color.Lerp(
                    _timerStateArcBaseColor,
                    eventColor,
                    eventEnvelope
                );

            stateColor.a =
                Mathf.Clamp01(
                    Mathf.Lerp(
                        _timerStateArcBaseColor.a,
                        0.88f,
                        eventEnvelope
                    )
                    + pendingPulse
                );

            _timerStateArc.color = stateColor;
        }

        private void RestoreApprovedArcs()
        {
            if (_timerMainArc != null)
            {
                _timerMainArc.color =
                    _timerMainArcBaseColor;
            }

            if (_timerStateArc != null)
            {
                _timerStateArc.color =
                    _timerStateArcBaseColor;
            }
        }

        private void RenderOuterFrame(
            float rewardEnvelope,
            float penaltyEnvelope
        )
        {
            float brightness =
                Mathf.Max(
                    rewardEnvelope * 0.18f,
                    penaltyEnvelope * 0.14f
                );

            for (int index = 0;
                index < outerFrameLines.Length;
                index++)
            {
                Image line =
                    outerFrameLines[index];

                if (line == null)
                {
                    continue;
                }

                Color baseColor =
                    GetColor(
                        _outerBaseColors,
                        index,
                        line.color
                    );

                Color result = baseColor;
                result.a =
                    Mathf.Lerp(
                        baseColor.a,
                        Mathf.Min(1f, baseColor.a + 0.16f),
                        brightness
                    );

                line.color = result;
            }
        }

        private void RenderInnerFrame(
            float rewardEnvelope,
            float penaltyEnvelope
        )
        {
            Color eventColor =
                penaltyEnvelope > rewardEnvelope
                    ? penaltyColor
                    : rewardColor;

            float tint =
                Mathf.Max(
                    rewardEnvelope
                        * rewardInnerLineTint,
                    penaltyEnvelope
                        * penaltyInnerLineTint
                );

            for (int index = 0;
                index < innerFrameLines.Length;
                index++)
            {
                Image line =
                    innerFrameLines[index];

                if (line == null)
                {
                    continue;
                }

                Color baseColor =
                    GetColor(
                        _innerBaseColors,
                        index,
                        line.color
                    );

                Color result =
                    Color.Lerp(
                        baseColor,
                        eventColor,
                        tint
                    );

                result.a =
                    Mathf.Lerp(
                        baseColor.a,
                        1f,
                        tint * 0.86f
                    );

                line.color = result;
            }
        }

        private void RenderTicks(
            float progress,
            float rewardEnvelope,
            float penaltyEnvelope
        )
        {
            int centreIndex =
                cyanTicks.Length / 2;

            float pendingWave =
                0.5f
                + 0.5f
                * Mathf.Sin(
                    Time.unscaledTime
                        * pendingPulseSpeed
                );

            for (int index = 0;
                index < cyanTicks.Length;
                index++)
            {
                Image tick = cyanTicks[index];

                if (tick == null)
                {
                    continue;
                }

                float baseHeight =
                    GetFloat(
                        _tickBaseHeights,
                        index,
                        tick.rectTransform.sizeDelta.y
                    );

                Color baseColor =
                    GetColor(
                        _tickBaseColors,
                        index,
                        tick.color
                    );

                float distance =
                    Mathf.Abs(
                        index - centreIndex
                    );

                float localRewardTime =
                    progress * 1.48f
                    - distance * 0.12f;

                float rewardWave = 0f;

                if (_signalType == SignalType.Reward
                    && localRewardTime > 0f
                    && localRewardTime < 1f)
                {
                    rewardWave =
                        Mathf.Sin(
                            localRewardTime * Mathf.PI
                        );
                }

                float pendingBoost =
                    _pendingAmount
                    * pendingTickHeightBoost
                    * Mathf.Lerp(
                        0.60f,
                        1f,
                        pendingWave
                    );

                float heightMultiplier =
                    1f
                    + pendingBoost
                    + rewardWave
                        * rewardTickHeightBoost;

                Vector2 size =
                    tick.rectTransform.sizeDelta;

                size.y =
                    baseHeight * heightMultiplier;

                tick.rectTransform.sizeDelta = size;

                Color result = baseColor;

                float pendingBrightnessAmount =
                    _pendingAmount
                    * pendingTickBrightness
                    * Mathf.Lerp(
                        0.62f,
                        1f,
                        pendingWave
                    );

                result.a =
                    Mathf.Lerp(
                        baseColor.a,
                        0.78f,
                        pendingBrightnessAmount
                    );

                if (rewardWave > 0f)
                {
                    result =
                        Color.Lerp(
                            result,
                            rewardColor,
                            rewardWave
                        );

                    result.a =
                        Mathf.Lerp(
                            result.a,
                            1f,
                            rewardWave
                        );
                }

                if (penaltyEnvelope > 0f)
                {
                    result =
                        Color.Lerp(
                            result,
                            penaltyColor,
                            penaltyEnvelope * 0.26f
                        );

                    result.a =
                        Mathf.Lerp(
                            result.a,
                            0.72f,
                            penaltyEnvelope
                        );
                }

                tick.color = result;
            }
        }

        private void RenderTopMark(
            float penaltyEnvelope
        )
        {
            if (magentaMark == null)
            {
                return;
            }

            RectTransform markRect =
                magentaMark.rectTransform;

            Vector2 size =
                _markBaseSize;

            size.x +=
                penaltyMarkWidthBoost
                * penaltyEnvelope;

            size.y +=
                penaltyMarkHeightBoost
                * penaltyEnvelope;

            markRect.sizeDelta = size;

            Color result =
                Color.Lerp(
                    _markBaseColor,
                    penaltyColor,
                    penaltyEnvelope
                );

            result.a =
                Mathf.Lerp(
                    _markBaseColor.a,
                    1f,
                    penaltyEnvelope
                );

            magentaMark.color = result;
        }

        private void RenderTimerScale(
            float rewardEnvelope,
            float penaltyEnvelope
        )
        {
            if (timerText == null)
            {
                return;
            }

            float scaleBoost =
                rewardEnvelope * rewardTimerScale
                + penaltyEnvelope * penaltyTimerScale;

            timerText.rectTransform.localScale =
                _timerBaseScale
                * (1f + scaleBoost);
        }

        private void RenderPenaltyKick(
            float progress,
            float penaltyEnvelope
        )
        {
            if (visualRoot == null)
            {
                return;
            }

            float offset = 0f;

            if (_signalType == SignalType.Penalty)
            {
                offset =
                    Mathf.Sin(
                        progress * Mathf.PI * 7f
                    )
                    * penaltyHorizontalKick
                    * penaltyEnvelope;
            }

            visualRoot.anchoredPosition =
                _rootBasePosition
                + new Vector2(offset, 0f);
        }

        private void CaptureBaseGeometry()
        {
            _outerBaseColors =
                CaptureColors(outerFrameLines);

            _innerBaseColors =
                CaptureColors(innerFrameLines);

            _tickBaseColors =
                CaptureColors(cyanTicks);

            _tickBaseHeights =
                new float[cyanTicks.Length];

            for (int index = 0;
                index < cyanTicks.Length;
                index++)
            {
                Image tick = cyanTicks[index];

                _tickBaseHeights[index] =
                    tick != null
                        ? tick.rectTransform.sizeDelta.y
                        : 0f;
            }

            if (magentaMark != null)
            {
                _markBaseSize =
                    magentaMark.rectTransform.sizeDelta;

                _markBaseColor =
                    magentaMark.color;
            }

            if (visualRoot != null)
            {
                _rootBasePosition =
                    visualRoot.anchoredPosition;
            }

            if (timerText != null)
            {
                _timerBaseScale =
                    timerText.rectTransform.localScale;
            }
        }

        private void ResetDetection()
        {
            _hasPreviousTime = false;
            _previousDisplayedTime = 0f;
        }

        private void ApplyImmediateNormalState()
        {
            if (visualRoot != null)
            {
                visualRoot.anchoredPosition =
                    _rootBasePosition;
            }

            if (timerText != null)
            {
                timerText.rectTransform.localScale =
                    _timerBaseScale;
            }

            RestoreImages(
                outerFrameLines,
                _outerBaseColors
            );

            RestoreImages(
                innerFrameLines,
                _innerBaseColors
            );

            RestoreImages(
                cyanTicks,
                _tickBaseColors
            );

            for (int index = 0;
                index < cyanTicks.Length;
                index++)
            {
                Image tick = cyanTicks[index];

                if (tick == null)
                {
                    continue;
                }

                Vector2 size =
                    tick.rectTransform.sizeDelta;

                size.y =
                    GetFloat(
                        _tickBaseHeights,
                        index,
                        size.y
                    );

                tick.rectTransform.sizeDelta = size;
            }

            if (magentaMark != null)
            {
                magentaMark.rectTransform.sizeDelta =
                    _markBaseSize;

                magentaMark.color =
                    _markBaseColor;
            }

            RestoreApprovedArcs();
        }

        private static bool TryParseTimer(
            string text,
            out float seconds
        )
        {
            seconds = 0f;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string cleaned =
                text.Trim();

            string[] minuteSplit =
                cleaned.Split(':');

            if (minuteSplit.Length != 2)
            {
                return false;
            }

            string[] secondSplit =
                minuteSplit[1].Split('.');

            if (secondSplit.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(
                    minuteSplit[0],
                    out int minutes
                )
                || !int.TryParse(
                    secondSplit[0],
                    out int wholeSeconds
                )
                || !int.TryParse(
                    secondSplit[1],
                    out int hundredths
                ))
            {
                return false;
            }

            seconds =
                minutes * 60f
                + wholeSeconds
                + hundredths * 0.01f;

            return true;
        }

        private static Color[] CaptureColors(
            Image[] images
        )
        {
            Color[] colors =
                new Color[images.Length];

            for (int index = 0;
                index < images.Length;
                index++)
            {
                colors[index] =
                    images[index] != null
                        ? images[index].color
                        : Color.white;
            }

            return colors;
        }

        private static void RestoreImages(
            Image[] images,
            Color[] colors
        )
        {
            for (int index = 0;
                index < images.Length;
                index++)
            {
                Image image = images[index];

                if (image == null)
                {
                    continue;
                }

                image.color =
                    GetColor(
                        colors,
                        index,
                        image.color
                    );
            }
        }

        private static Color GetColor(
            Color[] values,
            int index,
            Color fallback
        )
        {
            return values != null
                && index >= 0
                && index < values.Length
                    ? values[index]
                    : fallback;
        }

        private static float GetFloat(
            float[] values,
            int index,
            float fallback
        )
        {
            return values != null
                && index >= 0
                && index < values.Length
                    ? values[index]
                    : fallback;
        }
    }
}
