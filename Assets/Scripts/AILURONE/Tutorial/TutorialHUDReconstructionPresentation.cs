using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialHUDReconstructionPresentation : MonoBehaviour
    {
        private sealed class RevealRequest
        {
            public RectTransform target;
            public string message;
            public Action<float> setAlpha;
            public Action completed;
        }

        [SerializeField] private Canvas presentationCanvas;
        [SerializeField] private RectTransform effectsRoot;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Image[] fragments = Array.Empty<Image>();
        [SerializeField, Min(0.01f)] private float revealDuration = 0.52f;
        [SerializeField, Min(0.01f)] private float toastDuration = 0.55f;

        private readonly Queue<RevealRequest> _queue = new Queue<RevealRequest>();
        private Coroutine _queueRoutine;

        public int CompletedRevealCount { get; private set; }
        public int CompletedToastCount { get; private set; }
        public float RevealDuration => revealDuration;

        public void Configure(
            Canvas canvas,
            RectTransform root,
            CanvasGroup messageGroup,
            TMP_Text messageText,
            Image[] fragmentImages)
        {
            presentationCanvas = canvas;
            effectsRoot = root;
            toastGroup = messageGroup;
            toastText = messageText;
            fragments = fragmentImages ?? Array.Empty<Image>();
            ResetPresentation();
        }

        public void QueueReveal(
            RectTransform target,
            string message,
            Action<float> setAlpha,
            Action completed)
        {
            if (setAlpha == null)
            {
                completed?.Invoke();
                return;
            }

            _queue.Enqueue(new RevealRequest
            {
                target = target,
                message = message ?? string.Empty,
                setAlpha = setAlpha,
                completed = completed
            });

            if (_queueRoutine == null && isActiveAndEnabled)
            {
                _queueRoutine = StartCoroutine(ProcessQueue());
            }
        }

        public void CancelAndReset()
        {
            if (_queueRoutine != null)
            {
                StopCoroutine(_queueRoutine);
                _queueRoutine = null;
            }

            _queue.Clear();
            ResetPresentation();
        }

        private IEnumerator ProcessQueue()
        {
            while (_queue.Count > 0)
            {
                RevealRequest request = _queue.Dequeue();
                yield return RunReveal(request);
            }

            _queueRoutine = null;
        }

        private IEnumerator RunReveal(RevealRequest request)
        {
            request.setAlpha(0f);
            Vector2 targetPosition = ResolveTargetPosition(request.target);
            PrepareFragments(targetPosition);

            if (toastText != null)
            {
                toastText.text = request.message;
            }

            if (toastGroup != null)
            {
                toastGroup.alpha = string.IsNullOrEmpty(request.message) ? 0f : 1f;
            }

            float duration = Mathf.Max(0.01f, revealDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                request.setAlpha(smooth);
                UpdateFragments(targetPosition, smooth);
                yield return null;
            }

            request.setAlpha(1f);
            HideFragments();
            CompletedRevealCount++;
            request.completed?.Invoke();

            if (!string.IsNullOrEmpty(request.message))
            {
                float toastElapsed = 0f;
                float hold = Mathf.Max(0.01f, toastDuration);
                while (toastElapsed < hold)
                {
                    toastElapsed += Time.unscaledDeltaTime;
                    if (toastGroup != null)
                    {
                        float fadeStart = hold * 0.58f;
                        toastGroup.alpha = toastElapsed <= fadeStart
                            ? 1f
                            : 1f - Mathf.InverseLerp(fadeStart, hold, toastElapsed);
                    }

                    yield return null;
                }

                CompletedToastCount++;
            }

            if (toastGroup != null)
            {
                toastGroup.alpha = 0f;
            }
        }

        private Vector2 ResolveTargetPosition(RectTransform target)
        {
            if (effectsRoot == null || target == null)
            {
                return Vector2.zero;
            }

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
            Camera camera = presentationCanvas != null &&
                presentationCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? presentationCanvas.worldCamera
                    : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldCenter);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectsRoot,
                screenPoint,
                null,
                out Vector2 localPoint);
            return localPoint;
        }

        private void PrepareFragments(Vector2 targetPosition)
        {
            for (int index = 0; index < fragments.Length; index++)
            {
                Image fragment = fragments[index];
                if (fragment == null)
                {
                    continue;
                }

                fragment.gameObject.SetActive(true);
                RectTransform rect = fragment.rectTransform;
                float side = index % 2 == 0 ? -1f : 1f;
                float row = index / 2f;
                rect.anchoredPosition = targetPosition + new Vector2(
                    side * (18f + row * 8f),
                    (row - 1.5f) * 7f);
                rect.sizeDelta = new Vector2(10f + index * 2f, index % 3 == 0 ? 2f : 1f);
                Color color = index % 3 == 0
                    ? Color.white
                    : new Color(0.18f, 0.89f, 1f, 1f);
                color.a = 0f;
                fragment.color = color;
            }
        }

        private void UpdateFragments(Vector2 targetPosition, float progress)
        {
            for (int index = 0; index < fragments.Length; index++)
            {
                Image fragment = fragments[index];
                if (fragment == null || !fragment.gameObject.activeSelf)
                {
                    continue;
                }

                float side = index % 2 == 0 ? -1f : 1f;
                float row = index / 2f;
                Vector2 start = targetPosition + new Vector2(
                    side * (18f + row * 8f),
                    (row - 1.5f) * 7f);
                Vector2 end = targetPosition + new Vector2(
                    side * 3f,
                    (row - 1.5f) * 2f);
                fragment.rectTransform.anchoredPosition = Vector2.Lerp(start, end, progress);
                Color color = fragment.color;
                color.a = Mathf.Sin(progress * Mathf.PI) * 0.82f;
                fragment.color = color;
            }
        }

        private void HideFragments()
        {
            for (int index = 0; index < fragments.Length; index++)
            {
                if (fragments[index] != null)
                {
                    fragments[index].gameObject.SetActive(false);
                }
            }
        }

        private void ResetPresentation()
        {
            HideFragments();
            if (toastGroup != null)
            {
                toastGroup.alpha = 0f;
                toastGroup.interactable = false;
                toastGroup.blocksRaycasts = false;
            }
        }

        private void OnDisable()
        {
            CancelAndReset();
        }
    }
}
