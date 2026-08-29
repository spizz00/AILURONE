#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections;
using UnityEngine;

/// <summary>
/// Provides smooth CanvasGroup alpha fading and pop transitions for UI panels.
/// Automatically handles interactable state and raycast blocking.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanelFader : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional Motion")]
    [SerializeField] private bool animateTransform;
    [SerializeField, Range(0.8f, 1f)] private float hiddenScale = 0.96f;
    [SerializeField] private Vector2 hiddenOffset = new Vector2(36f, 0f);

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Vector2 _visiblePosition;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = transform as RectTransform;
        if (_rectTransform != null)
        {
            _visiblePosition = _rectTransform.anchoredPosition;
        }
    }

    /// <summary>
    /// Smoothly transitions the panel visibility.
    /// </summary>
    public void SetVisible(bool visible, bool immediate = false)
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

        if (immediate)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
            SetMotionPose(visible ? 1f : 0f);
            gameObject.SetActive(visible);
            return;
        }

        if (!visible && !gameObject.activeSelf)
        {
            return;
        }

        if (visible)
        {
            gameObject.SetActive(true);
        }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(visible));
    }

    private IEnumerator FadeRoutine(bool show)
    {
        float startAlpha = _canvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        Vector2 startPosition = _rectTransform != null
            ? _rectTransform.anchoredPosition
            : Vector2.zero;
        Vector2 targetPosition = show
            ? _visiblePosition
            : _visiblePosition + hiddenOffset;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = Vector3.one * (show ? 1f : hiddenScale);
        float elapsed = 0f;

        _canvasGroup.interactable = show;
        _canvasGroup.blocksRaycasts = show;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float curveT = fadeCurve.Evaluate(t);

            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveT);
            if (animateTransform)
            {
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = Vector2.Lerp(
                        startPosition,
                        targetPosition,
                        curveT);
                }
                transform.localScale = Vector3.Lerp(
                    startScale,
                    targetScale,
                    curveT);
            }
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        SetMotionPose(show ? 1f : 0f);

        if (!show)
        {
            gameObject.SetActive(false);
        }

        _fadeCoroutine = null;
    }

    private void SetMotionPose(float visibleAmount)
    {
        if (!animateTransform)
        {
            return;
        }

        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = Vector2.Lerp(
                _visiblePosition + hiddenOffset,
                _visiblePosition,
                visibleAmount);
        }

        float scale = Mathf.Lerp(hiddenScale, 1f, visibleAmount);
        transform.localScale = Vector3.one * scale;
    }
}
