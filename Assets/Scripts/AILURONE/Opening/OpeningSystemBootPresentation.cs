using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.Opening
{
    [DisallowMultipleComponent]
    public sealed class OpeningSystemBootPresentation : MonoBehaviour
    {
        [Serializable]
        private sealed class TimedLine
        {
            [SerializeField] private CanvasGroup group;
            [SerializeField, Min(0f)] private float revealTime;

            private Vector2 _finalPosition;

            public CanvasGroup Group => group;
            public float RevealTime => revealTime;

            public void CachePosition()
            {
                _finalPosition = group != null && group.transform is RectTransform rect
                    ? rect.anchoredPosition
                    : Vector2.zero;
            }

            public void Evaluate(float elapsed, float revealDuration, float brightness = 1f)
            {
                if (group == null)
                {
                    return;
                }

                float alpha = Mathf.Clamp01(
                    (elapsed - revealTime) / Mathf.Max(0.05f, revealDuration));
                alpha = Smooth(alpha) * Mathf.Clamp01(brightness);
                group.alpha = alpha;
                if (group.transform is RectTransform rect)
                {
                    rect.anchoredPosition =
                        _finalPosition + new Vector2(-12f * (1f - alpha), 0f);
                }
            }

            public void Hide()
            {
                if (group == null)
                {
                    return;
                }

                group.alpha = 0f;
                if (group.transform is RectTransform rect)
                {
                    rect.anchoredPosition = _finalPosition + new Vector2(-12f, 0f);
                }
            }

            private static float Smooth(float value)
            {
                value = Mathf.Clamp01(value);
                return value * value * (3f - 2f * value);
            }
        }

        [Header("Root")]
        [SerializeField] private GameObject presentationRoot;
        [SerializeField] private CanvasGroup presentationGroup;
        [SerializeField] private CanvasGroup gridGroup;

        [Header("Timed Diagnostics")]
        [SerializeField] private TimedLine header;
        [SerializeField] private TimedLine unitIdentity;
        [SerializeField] private TimedLine[] diagnostics = Array.Empty<TimedLine>();
        [SerializeField] private TimedLine warning;
        [SerializeField] private TimedLine recovery;
        [SerializeField] private TimedLine finalTitle;
        [SerializeField] private TimedLine finalStatus;

        [Header("Dropout")]
        [SerializeField] private GameObject dropoutRoot;
        [SerializeField] private Image[] dropoutBands = Array.Empty<Image>();
        [SerializeField, Min(0.05f)] private float revealDuration = 0.14f;

        private float _elapsed;
        private float _duration;
        private bool _active;

        public bool IsActive => _active;
        public float Elapsed => _elapsed;

        private void Awake()
        {
            CachePositions();
            ResetPresentation();
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            UpdateDropout();

            float diagnosticsBrightness = _elapsed < 2.90f
                ? 1f
                : Mathf.Lerp(1f, 0.32f, Mathf.Clamp01((_elapsed - 2.90f) / 0.22f));
            float diagnosticFade = _elapsed < 4.25f
                ? 1f
                : 1f - Mathf.Clamp01((_elapsed - 4.25f) / 0.22f);

            header.Evaluate(_elapsed, revealDuration, diagnosticsBrightness * diagnosticFade);
            unitIdentity.Evaluate(_elapsed, revealDuration, diagnosticsBrightness * diagnosticFade);
            for (int index = 0; index < diagnostics.Length; index++)
            {
                diagnostics[index]?.Evaluate(
                    _elapsed,
                    revealDuration,
                    diagnosticsBrightness * diagnosticFade);
            }

            warning.Evaluate(_elapsed, revealDuration, diagnosticFade);
            recovery.Evaluate(_elapsed, revealDuration, diagnosticFade);
            finalTitle.Evaluate(_elapsed, revealDuration);
            finalStatus.Evaluate(_elapsed, revealDuration);

            if (gridGroup != null)
            {
                gridGroup.alpha = Mathf.Lerp(0f, 0.07f, Mathf.Clamp01((_elapsed - 0.30f) / 0.40f));
            }

            float coverFade = _elapsed < 4.55f
                ? 1f
                : 1f - Mathf.Clamp01((_elapsed - 4.55f) /
                                      Mathf.Max(0.05f, _duration - 4.55f));
            presentationGroup.alpha = coverFade;
        }

        public bool BeginSegment(string segmentLabel, float duration)
        {
            EndSegment();
            if (string.IsNullOrWhiteSpace(segmentLabel) ||
                !segmentLabel.StartsWith("06 //", StringComparison.Ordinal))
            {
                return false;
            }

            _active = true;
            _elapsed = 0f;
            _duration = Mathf.Max(4.56f, duration);
            presentationRoot.SetActive(true);
            presentationGroup.alpha = 1f;
            HideAllLines();
            if (gridGroup != null)
            {
                gridGroup.alpha = 0f;
            }

            SetDropoutVisible(false, 0f);
            return true;
        }

        public void EndSegment()
        {
            if (!_active && (presentationRoot == null || !presentationRoot.activeSelf))
            {
                return;
            }

            ResetPresentation();
        }

        private void CachePositions()
        {
            header?.CachePosition();
            unitIdentity?.CachePosition();
            for (int index = 0; index < diagnostics.Length; index++)
            {
                diagnostics[index]?.CachePosition();
            }

            warning?.CachePosition();
            recovery?.CachePosition();
            finalTitle?.CachePosition();
            finalStatus?.CachePosition();
        }

        private void HideAllLines()
        {
            header?.Hide();
            unitIdentity?.Hide();
            for (int index = 0; index < diagnostics.Length; index++)
            {
                diagnostics[index]?.Hide();
            }

            warning?.Hide();
            recovery?.Hide();
            finalTitle?.Hide();
            finalStatus?.Hide();
        }

        private void UpdateDropout()
        {
            bool visible = _elapsed >= 0.08f && _elapsed <= 0.24f;
            float alpha = visible
                ? Mathf.Sin(Mathf.InverseLerp(0.08f, 0.24f, _elapsed) * Mathf.PI) * 0.09f
                : 0f;
            SetDropoutVisible(visible, alpha);
        }

        private void SetDropoutVisible(bool visible, float alpha)
        {
            if (dropoutRoot != null)
            {
                dropoutRoot.SetActive(visible);
            }

            for (int index = 0; index < dropoutBands.Length; index++)
            {
                Image band = dropoutBands[index];
                if (band == null)
                {
                    continue;
                }

                Color color = band.color;
                color.a = alpha * (1f - index * 0.18f);
                band.color = color;
                if (band.transform is RectTransform rect)
                {
                    Vector2 position = rect.anchoredPosition;
                    position.x = (index % 2 == 0 ? 1f : -1f) * alpha * 120f;
                    rect.anchoredPosition = position;
                }
            }
        }

        private void ResetPresentation()
        {
            _active = false;
            _elapsed = 0f;
            _duration = 0f;
            HideAllLines();
            SetDropoutVisible(false, 0f);

            if (gridGroup != null)
            {
                gridGroup.alpha = 0f;
            }

            if (presentationGroup != null)
            {
                presentationGroup.alpha = 0f;
            }

            if (presentationRoot != null)
            {
                presentationRoot.SetActive(false);
            }
        }
    }
}
