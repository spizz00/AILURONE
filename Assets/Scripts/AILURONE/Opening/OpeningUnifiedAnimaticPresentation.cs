using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.Opening
{
    [DisallowMultipleComponent]
    public sealed class OpeningUnifiedAnimaticPresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class ShotSettings
        {
            [SerializeField] private string archiveTag;
            [SerializeField] private string title;
            [SerializeField, TextArea] private string statusLine;
            [SerializeField] private float archiveRevealTime = 0.58f;
            [SerializeField] private float titleRevealTime = 0.90f;
            [SerializeField] private float statusRevealTime = 1.22f;
            [SerializeField] private float tearStartTime = 4.65f;
            [SerializeField] private float tearDuration = 0.08f;
            [SerializeField] private float instabilityStartTime = -1f;
            [SerializeField] private float startScale = 1f;
            [SerializeField] private float endScale = 1.04f;
            [SerializeField] private Vector2 startOffset;
            [SerializeField] private Vector2 endOffset;

            public string ArchiveTag => archiveTag;
            public string Title => title;
            public string StatusLine => statusLine;
            public float ArchiveRevealTime => archiveRevealTime;
            public float TitleRevealTime => titleRevealTime;
            public float StatusRevealTime => statusRevealTime;
            public float TearStartTime => tearStartTime;
            public float TearDuration => Mathf.Max(0.01f, tearDuration);
            public float InstabilityStartTime => instabilityStartTime;
            public float StartScale => Mathf.Max(1f, startScale);
            public float EndScale => Mathf.Max(1f, endScale);
            public Vector2 StartOffset => startOffset;
            public Vector2 EndOffset => endOffset;
        }

        [Header("Media")]
        [SerializeField] private RectTransform mediaRect;
        [SerializeField] private AspectRatioFitter mediaAspectRatio;

        [Header("Archive UI")]
        [SerializeField] private GameObject presentationRoot;
        [SerializeField] private CanvasGroup presentationGroup;
        [SerializeField] private TMP_Text archiveTag;
        [SerializeField] private CanvasGroup archiveTagGroup;
        [SerializeField] private TMP_Text mainTitle;
        [SerializeField] private CanvasGroup mainTitleGroup;
        [SerializeField] private TMP_Text statusLine;
        [SerializeField] private CanvasGroup statusLineGroup;
        [SerializeField] private CanvasGroup accentRuleGroup;

        [Header("Light FX")]
        [SerializeField] private RectTransform scanBand;
        [SerializeField] private Image scanBandImage;
        [SerializeField] private GameObject digitalTearRoot;
        [SerializeField] private RectTransform[] tearStrips = Array.Empty<RectTransform>();
        [SerializeField] private Image[] tearStripImages = Array.Empty<Image>();
        [SerializeField, Range(0f, 0.03f)] private float scanBandAlpha = 0.018f;
        [SerializeField, Range(0f, 0.12f)] private float tearAlpha = 0.075f;
        [SerializeField, Min(0.05f)] private float textRevealDuration = 0.15f;
        [SerializeField, Range(0f, 24f)] private float textSlideDistance = 14f;

        [Header("Shots")]
        [SerializeField] private ShotSettings[] shots = Array.Empty<ShotSettings>();

        private readonly Vector2[] _lineFinalPositions = new Vector2[4];
        private Vector3 _mediaBaselineScale = Vector3.one;
        private Vector2 _mediaBaselinePosition;
        private AspectRatioFitter.AspectMode _baselineAspectMode;
        private ShotSettings _activeShot;
        private float _elapsed;
        private float _duration;
        private int _activeShotIndex = -1;
        private bool _active;

        public bool IsActive => _active;
        public int ActiveShotIndex => _activeShotIndex;
        public float Elapsed => _elapsed;
        public float TextRevealDuration => textRevealDuration;
        public float TextSlideDistance => textSlideDistance;

        private void Awake()
        {
            CacheLinePositions();
            ResetPresentation(false);
        }

        private void Update()
        {
            if (!_active || _activeShot == null)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.05f, _duration));
            float eased = Smooth(progress);

            float scale = Mathf.Lerp(
                _activeShot.StartScale,
                _activeShot.EndScale,
                eased);
            mediaRect.localScale = new Vector3(scale, scale, 1f);
            mediaRect.anchoredPosition = Vector2.Lerp(
                _activeShot.StartOffset,
                _activeShot.EndOffset,
                eased);

            EvaluateReveal(archiveTagGroup, 0, _activeShot.ArchiveRevealTime);
            EvaluateReveal(mainTitleGroup, 1, _activeShot.TitleRevealTime);
            EvaluateReveal(statusLineGroup, 2, _activeShot.StatusRevealTime);
            EvaluateReveal(accentRuleGroup, 3, _activeShot.ArchiveRevealTime - 0.06f);
            UpdateScanBand(progress);
            UpdateDigitalTear();
            UpdateSignalInstability();
        }

        public bool BeginSegment(int segmentIndex, string segmentLabel, float duration)
        {
            EndSegment();

            if (segmentIndex < 0 || segmentIndex >= shots.Length ||
                segmentIndex >= 5 || shots[segmentIndex] == null)
            {
                return false;
            }

            _activeShot = shots[segmentIndex];
            _activeShotIndex = segmentIndex;
            _duration = Mathf.Max(0.05f, duration);
            _elapsed = 0f;
            _active = true;

            _mediaBaselineScale = mediaRect.localScale;
            _mediaBaselinePosition = mediaRect.anchoredPosition;
            _baselineAspectMode = mediaAspectRatio.aspectMode;
            mediaAspectRatio.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            mediaRect.localScale = new Vector3(
                _activeShot.StartScale,
                _activeShot.StartScale,
                1f);
            mediaRect.anchoredPosition = _activeShot.StartOffset;

            archiveTag.text = _activeShot.ArchiveTag;
            mainTitle.text = _activeShot.Title;
            statusLine.text = _activeShot.StatusLine;
            presentationRoot.SetActive(true);
            presentationGroup.alpha = 1f;
            SetRevealImmediate(archiveTagGroup, 0, 0f);
            SetRevealImmediate(mainTitleGroup, 1, 0f);
            SetRevealImmediate(statusLineGroup, 2, 0f);
            SetRevealImmediate(accentRuleGroup, 3, 0f);
            ResetTear();
            UpdateScanBand(0f);
            return true;
        }

        public void EndSegment()
        {
            if (!_active && (presentationRoot == null || !presentationRoot.activeSelf))
            {
                return;
            }

            ResetPresentation(true);
        }

        private void ResetPresentation(bool restoreMedia)
        {
            _active = false;
            _activeShot = null;
            _activeShotIndex = -1;
            _elapsed = 0f;
            _duration = 0f;

            if (restoreMedia && mediaRect != null)
            {
                mediaRect.localScale = _mediaBaselineScale;
                mediaRect.anchoredPosition = _mediaBaselinePosition;
            }

            if (restoreMedia && mediaAspectRatio != null)
            {
                mediaAspectRatio.aspectMode = _baselineAspectMode;
            }

            ResetTear();
            if (presentationGroup != null)
            {
                presentationGroup.alpha = 0f;
            }

            if (presentationRoot != null)
            {
                presentationRoot.SetActive(false);
            }
        }

        private void CacheLinePositions()
        {
            _lineFinalPositions[0] = GetAnchoredPosition(archiveTagGroup);
            _lineFinalPositions[1] = GetAnchoredPosition(mainTitleGroup);
            _lineFinalPositions[2] = GetAnchoredPosition(statusLineGroup);
            _lineFinalPositions[3] = GetAnchoredPosition(accentRuleGroup);
        }

        private void EvaluateReveal(CanvasGroup group, int lineIndex, float revealTime)
        {
            float alpha = Mathf.Clamp01(
                (_elapsed - Mathf.Max(0f, revealTime)) /
                Mathf.Max(0.05f, textRevealDuration));
            SetRevealImmediate(group, lineIndex, Smooth(alpha));
        }

        private void SetRevealImmediate(CanvasGroup group, int lineIndex, float alpha)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = Mathf.Clamp01(alpha);
            if (group.transform is RectTransform rect)
            {
                Vector2 final = _lineFinalPositions[lineIndex];
                rect.anchoredPosition = final +
                    new Vector2(-textSlideDistance * (1f - alpha), 0f);
            }
        }

        private void UpdateScanBand(float progress)
        {
            if (scanBand == null || scanBandImage == null)
            {
                return;
            }

            float height = 1080f;
            if (presentationRoot != null &&
                presentationRoot.transform is RectTransform rootRect &&
                rootRect.rect.height > 1f)
            {
                height = rootRect.rect.height;
            }

            scanBand.anchoredPosition = new Vector2(
                0f,
                Mathf.Lerp(height * 0.48f, -height * 0.48f, progress));
            Color color = scanBandImage.color;
            color.a = scanBandAlpha;
            scanBandImage.color = color;
        }

        private void UpdateDigitalTear()
        {
            float local = _elapsed - _activeShot.TearStartTime;
            bool visible = local >= 0f && local <= _activeShot.TearDuration;
            if (digitalTearRoot == null)
            {
                return;
            }

            digitalTearRoot.SetActive(visible);
            if (!visible)
            {
                return;
            }

            float phase = Mathf.Clamp01(local / _activeShot.TearDuration);
            float envelope = Mathf.Sin(phase * Mathf.PI);
            int count = Mathf.Min(tearStrips.Length, tearStripImages.Length);
            for (int index = 0; index < count; index++)
            {
                RectTransform strip = tearStrips[index];
                Image image = tearStripImages[index];
                if (strip == null || image == null)
                {
                    continue;
                }

                float direction = index % 2 == 0 ? 1f : -1f;
                strip.anchoredPosition = new Vector2(
                    direction * (10f + index * 5f) * envelope,
                    strip.anchoredPosition.y);
                Color color = image.color;
                color.a = tearAlpha * envelope;
                image.color = color;
            }
        }

        private void UpdateSignalInstability()
        {
            if (_activeShot.InstabilityStartTime < 0f ||
                _elapsed < _activeShot.InstabilityStartTime)
            {
                return;
            }

            float pulse = Mathf.Sin(_elapsed * 71f) > 0.72f ? 0.82f : 1f;
            presentationGroup.alpha = pulse;
        }

        private void ResetTear()
        {
            if (digitalTearRoot != null)
            {
                digitalTearRoot.SetActive(false);
            }

            int count = Mathf.Min(tearStrips.Length, tearStripImages.Length);
            for (int index = 0; index < count; index++)
            {
                RectTransform strip = tearStrips[index];
                Image image = tearStripImages[index];
                if (strip != null)
                {
                    Vector2 position = strip.anchoredPosition;
                    position.x = 0f;
                    strip.anchoredPosition = position;
                }

                if (image != null)
                {
                    Color color = image.color;
                    color.a = 0f;
                    image.color = color;
                }
            }
        }

        private static Vector2 GetAnchoredPosition(CanvasGroup group)
        {
            return group != null && group.transform is RectTransform rect
                ? rect.anchoredPosition
                : Vector2.zero;
        }

        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
