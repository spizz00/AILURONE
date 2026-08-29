using UnityEngine;

namespace AILURONE.Opening
{
    [DisallowMultipleComponent]
    public sealed class OpeningStaticMotionPresentation : MonoBehaviour
    {
        [SerializeField] private RectTransform mediaRect;
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private RectTransform scanLine;

        [Header("Camera Motion")]
        [SerializeField] private float startScale = 1.085f;
        [SerializeField] private float endScale = 1.018f;
        [SerializeField] private Vector2 startOffset = new(-20f, 9f);
        [SerializeField] private Vector2 endOffset = new(14f, -6f);

        [Header("Overlay")]
        [SerializeField, Min(0.01f)] private float overlayFadeIn = 0.75f;
        [SerializeField, Min(0.01f)] private float overlayFadeOut = 0.60f;
        [SerializeField, Min(0.01f)] private float scanCycleDuration = 3.6f;

        private Vector3 _originalScale;
        private Vector2 _originalPosition;
        private bool _originalStateCached;
        private float _elapsed;
        private float _duration;
        private bool _active;

        private void Awake()
        {
            CacheOriginalState();
            ResetPresentation();
        }

        private void LateUpdate()
        {
            if (!_active)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(_elapsed / _duration);
            float eased = normalized * normalized * (3f - 2f * normalized);

            if (mediaRect != null)
            {
                float scale = Mathf.Lerp(startScale, endScale, eased);
                mediaRect.localScale = new Vector3(scale, scale, 1f);
                mediaRect.anchoredPosition = Vector2.Lerp(startOffset, endOffset, eased);
            }

            if (overlayGroup != null)
            {
                float fadeIn = Mathf.Clamp01(_elapsed / overlayFadeIn);
                float fadeOut = Mathf.Clamp01((_duration - _elapsed) / overlayFadeOut);
                overlayGroup.alpha = Mathf.Min(fadeIn, fadeOut);
            }

            UpdateScanLine();
        }

        public void BeginShot(float duration)
        {
            CacheOriginalState();
            _duration = Mathf.Max(0.1f, duration);
            _elapsed = 0f;
            _active = true;

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
            }

            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
            }

            if (mediaRect != null)
            {
                mediaRect.localScale = new Vector3(startScale, startScale, 1f);
                mediaRect.anchoredPosition = startOffset;
            }

            UpdateScanLine();
        }

        public void EndShot()
        {
            ResetPresentation();
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        private void CacheOriginalState()
        {
            if (mediaRect == null || _originalStateCached)
            {
                return;
            }

            _originalScale = mediaRect.localScale;
            _originalPosition = mediaRect.anchoredPosition;
            _originalStateCached = true;
        }

        private void UpdateScanLine()
        {
            if (scanLine == null || scanLine.parent is not RectTransform parentRect)
            {
                return;
            }

            float cycle = Mathf.Repeat(
                _elapsed / Mathf.Max(0.01f, scanCycleDuration),
                1f);
            float height = parentRect.rect.height;
            Vector2 position = scanLine.anchoredPosition;
            position.y = -height * cycle;
            scanLine.anchoredPosition = position;
        }

        private void ResetPresentation()
        {
            _active = false;
            _elapsed = 0f;

            if (mediaRect != null)
            {
                mediaRect.localScale = _originalScale;
                mediaRect.anchoredPosition = _originalPosition;
            }

            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }
        }
    }
}
