#pragma warning disable 0618
#pragma warning disable 0414
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Minimal Core objective tracker.
    ///
    /// Main progress:
    /// FinalGateManager.CurrentFilledSockets
    ///
    /// Inventory is shown persistently as a compact carry counter.
    /// On pickup, the same line briefly becomes an acquisition message.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONECoreObjectiveHUD : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private FinalGateManager finalGateManager;

        [Header("Root")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform visualRoot;

        [Header("Text")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text toastText;

        [Header("Progress Slots")]
        [SerializeField] private RectTransform[] slotRoots =
            Array.Empty<RectTransform>();

        [SerializeField] private CanvasGroup[] slotGroups =
            Array.Empty<CanvasGroup>();

        [SerializeField] private Image[] slotFillImages =
            Array.Empty<Image>();

        [Header("Gradient Accent")]
        [SerializeField] private RectTransform gradientRoot;
        [SerializeField] private Image[] gradientSegments =
            Array.Empty<Image>();

        [Header("Colours")]
        [SerializeField] private Color primaryWhite =
            new Color(0.95f, 0.98f, 1.00f, 1f);

        [SerializeField] private Color mutedWhite =
            new Color(0.63f, 0.72f, 0.77f, 0.78f);

        [SerializeField] private Color cyan =
            new Color(0.18f, 0.93f, 1.00f, 1f);

        [Header("Animation - Unscaled Time")]
        [Min(0.1f)]
        [SerializeField] private float slotPulseDuration = 0.38f;

        [Range(0f, 0.25f)]
        [SerializeField] private float slotPulseScale = 0.14f;

        [Min(0.1f)]
        [SerializeField] private float toastDuration = 0.72f;

        [Min(0f)]
        [SerializeField] private float toastRiseDistance = 5f;

        [Min(0.1f)]
        [SerializeField] private float accentPulseDuration = 0.38f;

        [Range(0f, 0.5f)]
        [SerializeField] private float accentWidthBoost = 0.26f;

        [Min(0.1f)]
        [SerializeField] private float completionHoldDuration = 1.15f;

        [Range(0f, 1f)]
        [SerializeField] private float completedIdleAlpha = 0.56f;

        [Min(0.1f)]
        [SerializeField] private float completionFadeDuration = 0.42f;

        [Header("Debug")]
        [SerializeField] private bool logMissingReferences;

        [SerializeField, HideInInspector]
        private bool suppressRuntimePresentationInitialization;

        private bool _gameSubscribed;
        private bool _gateSubscribed;

        private int _installedCount;
        private int _requiredCount = 3;
        private int _carriedCount;

        private bool _complete;
        private bool _completionSettled;

        private float _slotPulseTimer;
        private int _slotPulseIndex = -1;

        private float _toastTimer;
        private float _accentPulseTimer;
        private float _completionHoldTimer;
        private float _completionFadeTimer;

        private Vector3[] _slotBaseScales =
            Array.Empty<Vector3>();

        private Vector2 _toastBasePosition;
        private Vector2 _visualBasePosition;
        private Vector3 _gradientBaseScale =
            Vector3.one;

        private Color[] _gradientBaseColours =
            Array.Empty<Color>();

        private float _tutorialVisibilityAlpha = 1f;

        public float TutorialVisibilityAlpha => _tutorialVisibilityAlpha;

        public void SetTutorialVisibilityAlpha(float alpha)
        {
            _tutorialVisibilityAlpha = Mathf.Clamp01(alpha);
            ApplyTutorialVisibility();
        }

        private void ApplyTutorialVisibility()
        {
            if (canvasGroup != null && _tutorialVisibilityAlpha < 1f)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Awake()
        {
            if (suppressRuntimePresentationInitialization)
            {
                return;
            }

            ResolveSources();
            ApplyApprovedPresentation();
            CaptureBaseState();
            PullCurrentState();
            RenderImmediate();
        }

        private void OnEnable()
        {
            if (suppressRuntimePresentationInitialization)
            {
                return;
            }

            ResolveSources();
            ApplyApprovedPresentation();
            CaptureBaseState();
            Subscribe();
            PullCurrentState();
            RenderImmediate();
        }

        private void Update()
        {
            ResolveSourcesIfNeeded();
            UpdateSlotPulse();
            UpdateToast();
            UpdateAccentPulse();
            UpdateCompletionState();
            ApplyTutorialVisibility();

            if (visualRoot != null)
            {
                visualRoot.anchoredPosition =
                    _visualBasePosition
                    + AILURONEHUDMotionSignal.GetOffset(0.35f);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetAnimatedState();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void Configure(
            GameManager game,
            FinalGateManager gate,
            CanvasGroup group,
            RectTransform root,
            TMP_Text header,
            TMP_Text objective,
            TMP_Text progress,
            TMP_Text toast,
            RectTransform[] slots,
            CanvasGroup[] groups,
            Image[] fills,
            RectTransform accentRoot,
            Image[] accentSegments
        )
        {
            Unsubscribe();

            gameManager = game;
            finalGateManager = gate;

            canvasGroup = group;
            visualRoot = root;

            headerText = header;
            objectiveText = objective;
            progressText = progress;
            toastText = toast;

            slotRoots =
                slots ?? Array.Empty<RectTransform>();

            slotGroups =
                groups ?? Array.Empty<CanvasGroup>();

            slotFillImages =
                fills ?? Array.Empty<Image>();

            gradientRoot = accentRoot;
            gradientSegments =
                accentSegments ?? Array.Empty<Image>();

            ApplyApprovedPresentation();
            CaptureBaseState();
            Subscribe();
            PullCurrentState();
            RenderImmediate();
        }

        private void ApplyApprovedPresentation()
        {
            primaryWhite = AILURONEHUDRuntimeStyle.White;
            mutedWhite = AILURONEHUDRuntimeStyle.MutedWhite;
            cyan = AILURONEHUDRuntimeStyle.Yellow;

            AILURONEHUDRuntimeStyle.ApplyObjective(
                visualRoot,
                headerText,
                objectiveText,
                progressText,
                toastText,
                slotRoots,
                gradientRoot,
                gradientSegments);
        }

        private void ResolveSources()
        {
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;

                if (gameManager == null)
                {
                    GameManager[] games =
                        FindObjectsByType<GameManager>(
                            FindObjectsInactive.Exclude
                        );

                    if (games.Length > 0)
                    {
                        gameManager = games[0];
                    }
                }
            }

            if (finalGateManager == null)
            {
                FinalGateManager[] gates =
                    FindObjectsByType<FinalGateManager>(
                        FindObjectsInactive.Exclude
                    );

                if (gates.Length > 0)
                {
                    finalGateManager = gates[0];
                }
            }

            if (!logMissingReferences)
            {
                return;
            }

            if (gameManager == null)
            {
                Debug.LogWarning(
                    "[CoreObjectiveHUD] GameManager was not found.",
                    this
                );
            }

            if (finalGateManager == null)
            {
                Debug.LogWarning(
                    "[CoreObjectiveHUD] FinalGateManager was not found.",
                    this
                );
            }
        }

        private void ResolveSourcesIfNeeded()
        {
            bool missingGame =
                gameManager == null;

            bool missingGate =
                finalGateManager == null;

            if (!missingGame && !missingGate)
            {
                return;
            }

            ResolveSources();
            Subscribe();
            PullCurrentState();
            RenderImmediate();
        }

        private void Subscribe()
        {
            if (gameManager != null
                && !_gameSubscribed)
            {
                gameManager.CoreInventoryChanged +=
                    HandleInventoryChanged;

                _gameSubscribed = true;
            }

            if (finalGateManager != null
                && !_gateSubscribed)
            {
                finalGateManager.SocketProgressChanged +=
                    HandleSocketProgressChanged;

                finalGateManager.GateReleased +=
                    HandleGateReleased;

                _gateSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (gameManager != null
                && _gameSubscribed)
            {
                gameManager.CoreInventoryChanged -=
                    HandleInventoryChanged;
            }

            if (finalGateManager != null
                && _gateSubscribed)
            {
                finalGateManager.SocketProgressChanged -=
                    HandleSocketProgressChanged;

                finalGateManager.GateReleased -=
                    HandleGateReleased;
            }

            _gameSubscribed = false;
            _gateSubscribed = false;
        }

        private void PullCurrentState()
        {
            if (gameManager != null)
            {
                _carriedCount =
                    Mathf.Max(
                        0,
                        gameManager.CurrentCoreCount
                    );
            }

            if (finalGateManager != null)
            {
                _requiredCount =
                    Mathf.Max(
                        1,
                        finalGateManager.TotalSocketsRequired
                    );

                _installedCount =
                    Mathf.Clamp(
                        finalGateManager.CurrentFilledSockets,
                        0,
                        _requiredCount
                    );

                _complete =
                    finalGateManager.IsGateReleased
                    || _installedCount >= _requiredCount;
            }

            if (_complete)
            {
                _completionSettled = true;

                if (canvasGroup != null)
                {
                    canvasGroup.alpha =
                        completedIdleAlpha;
                }
            }
        }

        private void HandleInventoryChanged(
            int current,
            int configuredTotal
        )
        {
            int newCount =
                Mathf.Max(0, current);

            bool gainedCore =
                newCount > _carriedCount;

            if (gainedCore)
            {
                int gained =
                    newCount - _carriedCount;

                ShowToast(
                    gained == 1
                        ? "REWRITE NODE ACQUIRED  +1"
                        : $"REWRITE NODE ACQUIRED  +{gained}"
                );
            }

            bool countChanged =
                newCount != _carriedCount;

            _carriedCount = newCount;

            if (countChanged)
            {
                _accentPulseTimer =
                    accentPulseDuration;
            }

            if (!gainedCore)
            {
                RenderCarryText();
            }
        }

        private void HandleSocketProgressChanged(
            int current,
            int required
        )
        {
            int safeRequired =
                Mathf.Max(1, required);

            int newInstalled =
                Mathf.Clamp(
                    current,
                    0,
                    safeRequired
                );

            if (newInstalled > _installedCount)
            {
                _slotPulseIndex =
                    Mathf.Clamp(
                        newInstalled - 1,
                        0,
                        slotRoots.Length - 1
                    );

                _slotPulseTimer =
                    slotPulseDuration;

                _accentPulseTimer =
                    accentPulseDuration;
            }

            _installedCount = newInstalled;
            _requiredCount = safeRequired;

            if (_installedCount >= _requiredCount)
            {
                BeginCompletion();
            }

            RenderImmediate();
        }

        private void HandleGateReleased()
        {
            _installedCount =
                Mathf.Max(
                    _installedCount,
                    _requiredCount
                );

            BeginCompletion();
            RenderImmediate();
        }

        private void ShowToast(string content)
        {
            if (toastText == null)
            {
                return;
            }

            toastText.text = content;
            toastText.color = cyan;
            toastText.gameObject.SetActive(true);
            toastText.rectTransform.anchoredPosition =
                _toastBasePosition;

            _toastTimer =
                toastDuration;
        }

        private void BeginCompletion()
        {
            _complete = true;
            _completionSettled = false;

            _completionHoldTimer =
                completionHoldDuration;

            _completionFadeTimer =
                completionFadeDuration;

            _accentPulseTimer =
                accentPulseDuration;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private void UpdateSlotPulse()
        {
            if (_slotPulseTimer <= 0f
                || _slotPulseIndex < 0
                || _slotPulseIndex >= slotRoots.Length)
            {
                return;
            }

            _slotPulseTimer -=
                Time.unscaledDeltaTime;

            float progress =
                1f - Mathf.Clamp01(
                    _slotPulseTimer
                    / Mathf.Max(
                        0.1f,
                        slotPulseDuration
                    )
                );

            float envelope =
                Mathf.Sin(progress * Mathf.PI);

            RectTransform slot =
                slotRoots[_slotPulseIndex];

            if (slot != null)
            {
                slot.localScale =
                    GetSlotBaseScale(
                        _slotPulseIndex
                    )
                    * (1f + envelope * slotPulseScale);
            }

            if (_slotPulseTimer <= 0f)
            {
                ResetSlotScale(_slotPulseIndex);
                _slotPulseIndex = -1;
            }
        }

        private void UpdateToast()
        {
            if (toastText == null)
            {
                return;
            }

            if (_toastTimer <= 0f)
            {
                RenderCarryText();
                return;
            }

            _toastTimer -=
                Time.unscaledDeltaTime;

            float progress =
                1f - Mathf.Clamp01(
                    _toastTimer
                    / Mathf.Max(
                        0.1f,
                        toastDuration
                    )
                );

            float fade =
                1f - Mathf.SmoothStep(
                    0.58f,
                    1f,
                    progress
                );

            Color colour = cyan;
            colour.a = fade;
            toastText.color = colour;

            Vector2 position =
                _toastBasePosition;

            position.y +=
                progress * toastRiseDistance;

            toastText.rectTransform.anchoredPosition =
                position;

            if (_toastTimer <= 0f)
            {
                toastText.rectTransform.anchoredPosition =
                    _toastBasePosition;

                RenderCarryText();
            }
        }

        private void UpdateAccentPulse()
        {
            if (gradientRoot == null)
            {
                return;
            }

            if (_accentPulseTimer <= 0f)
            {
                gradientRoot.localScale =
                    _gradientBaseScale;

                RestoreGradientColours();
                return;
            }

            _accentPulseTimer -=
                Time.unscaledDeltaTime;

            float progress =
                1f - Mathf.Clamp01(
                    _accentPulseTimer
                    / Mathf.Max(
                        0.1f,
                        accentPulseDuration
                    )
                );

            float envelope =
                Mathf.Sin(progress * Mathf.PI);

            gradientRoot.localScale =
                new Vector3(
                    _gradientBaseScale.x
                        * (1f + envelope * accentWidthBoost),
                    _gradientBaseScale.y,
                    _gradientBaseScale.z
                );

            for (int index = 0;
                index < gradientSegments.Length;
                index++)
            {
                Image segment =
                    gradientSegments[index];

                if (segment == null)
                {
                    continue;
                }

                Color baseColour =
                    GetColour(
                        _gradientBaseColours,
                        index,
                        segment.color
                    );

                Color result =
                    Color.Lerp(
                        baseColour,
                        cyan,
                        envelope
                    );

                result.a =
                    Mathf.Lerp(
                        baseColour.a,
                        1f,
                        envelope
                    );

                segment.color = result;
            }

            if (_accentPulseTimer <= 0f)
            {
                gradientRoot.localScale =
                    _gradientBaseScale;

                RestoreGradientColours();
            }
        }

        private void UpdateCompletionState()
        {
            if (!_complete
                || _completionSettled)
            {
                return;
            }

            if (_completionHoldTimer > 0f)
            {
                _completionHoldTimer -=
                    Time.unscaledDeltaTime;

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }

                return;
            }

            _completionFadeTimer -=
                Time.unscaledDeltaTime;

            float progress =
                1f - Mathf.Clamp01(
                    _completionFadeTimer
                    / Mathf.Max(
                        0.1f,
                        completionFadeDuration
                    )
                );

            if (canvasGroup != null)
            {
                canvasGroup.alpha =
                    Mathf.Lerp(
                        1f,
                        completedIdleAlpha,
                        progress
                    );
            }

            if (_completionFadeTimer <= 0f)
            {
                _completionSettled = true;

                if (canvasGroup != null)
                {
                    canvasGroup.alpha =
                        completedIdleAlpha;
                }
            }
        }

        private void RenderImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                if (!_completionSettled)
                {
                    canvasGroup.alpha = 1f;
                }
            }

            if (headerText != null)
            {
                headerText.text = "PRIMARY OBJECTIVE";
                headerText.color =
                    AILURONEHUDRuntimeStyle.Yellow;
            }

            if (objectiveText != null)
            {
                objectiveText.text =
                    _complete
                        ? "ACCESS GRANTED"
                        : "LINK REWRITE NODES";

                objectiveText.color =
                    _complete ? cyan : primaryWhite;
            }

            if (progressText != null)
            {
                progressText.text =
                    $"REWRITE NODES LINKED   {Mathf.Clamp(_installedCount, 0, _requiredCount)} / {_requiredCount}";

                progressText.color =
                    _complete ? cyan : primaryWhite;
            }

            if (_toastTimer <= 0f)
            {
                RenderCarryText();
            }

            RenderSlots();
        }

        private void RenderCarryText()
        {
            if (toastText == null)
            {
                return;
            }

            if (_carriedCount <= 0)
            {
                toastText.gameObject.SetActive(false);
                return;
            }

            toastText.gameObject.SetActive(true);
            toastText.rectTransform.anchoredPosition =
                _toastBasePosition;

            toastText.text =
                $"CARRY  {_carriedCount}";

            toastText.color =
                _carriedCount > 0
                    ? cyan
                    : mutedWhite;

            Color colour =
                toastText.color;

            colour.a =
                _carriedCount > 0
                    ? 0.92f
                    : 0.58f;

            toastText.color = colour;
        }

        private void RenderSlots()
        {
            for (int index = 0;
                index < slotRoots.Length;
                index++)
            {
                bool filled =
                    index < _installedCount;

                if (index < slotGroups.Length
                    && slotGroups[index] != null)
                {
                    slotGroups[index].alpha =
                        filled ? 1f : 0.42f;
                }

                if (index < slotFillImages.Length
                    && slotFillImages[index] != null)
                {
                    slotFillImages[index]
                        .gameObject
                        .SetActive(filled);

                    slotFillImages[index].color =
                        cyan;
                }
            }
        }

        private void CaptureBaseState()
        {
            if (visualRoot != null)
            {
                _visualBasePosition =
                    visualRoot.anchoredPosition;
            }

            _slotBaseScales =
                new Vector3[slotRoots.Length];

            for (int index = 0;
                index < slotRoots.Length;
                index++)
            {
                RectTransform slot =
                    slotRoots[index];

                _slotBaseScales[index] =
                    slot != null
                        ? slot.localScale
                        : Vector3.one;
            }

            if (toastText != null)
            {
                _toastBasePosition =
                    toastText
                        .rectTransform
                        .anchoredPosition;
            }

            if (gradientRoot != null)
            {
                _gradientBaseScale =
                    gradientRoot.localScale;
            }

            _gradientBaseColours =
                CaptureColours(
                    gradientSegments
                );
        }

        private void ResetAnimatedState()
        {
            for (int index = 0;
                index < slotRoots.Length;
                index++)
            {
                ResetSlotScale(index);
            }

            if (toastText != null)
            {
                toastText.rectTransform.anchoredPosition =
                    _toastBasePosition;

                _toastTimer = 0f;
                RenderCarryText();
            }

            if (gradientRoot != null)
            {
                gradientRoot.localScale =
                    _gradientBaseScale;
            }

            RestoreGradientColours();
        }

        private void RestoreGradientColours()
        {
            RestoreColours(
                gradientSegments,
                _gradientBaseColours
            );
        }

        private Vector3 GetSlotBaseScale(
            int index
        )
        {
            if (_slotBaseScales != null
                && index >= 0
                && index < _slotBaseScales.Length)
            {
                return _slotBaseScales[index];
            }

            return Vector3.one;
        }

        private void ResetSlotScale(int index)
        {
            if (index < 0
                || index >= slotRoots.Length)
            {
                return;
            }

            RectTransform slot =
                slotRoots[index];

            if (slot != null)
            {
                slot.localScale =
                    GetSlotBaseScale(index);
            }
        }

        private static Color[] CaptureColours(
            Image[] images
        )
        {
            Color[] colours =
                new Color[images.Length];

            for (int index = 0;
                index < images.Length;
                index++)
            {
                colours[index] =
                    images[index] != null
                        ? images[index].color
                        : Color.white;
            }

            return colours;
        }

        private static void RestoreColours(
            Image[] images,
            Color[] colours
        )
        {
            for (int index = 0;
                index < images.Length;
                index++)
            {
                Image image =
                    images[index];

                if (image == null)
                {
                    continue;
                }

                image.color =
                    GetColour(
                        colours,
                        index,
                        image.color
                    );
            }
        }

        private static Color GetColour(
            Color[] colours,
            int index,
            Color fallback
        )
        {
            return colours != null
                && index >= 0
                && index < colours.Length
                    ? colours[index]
                    : fallback;
        }
    }
}
