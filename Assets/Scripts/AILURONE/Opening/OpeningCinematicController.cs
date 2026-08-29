using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AILURONE.Opening
{
    public enum OpeningCinematicSegmentType
    {
        Black,
        StillImage,
        Video
    }

    [Serializable]
    public sealed class OpeningCinematicSegment
    {
        [SerializeField] private string label = "Opening Segment";
        [SerializeField] private OpeningCinematicSegmentType type;
        [SerializeField] private Texture stillImage;
        [SerializeField] private VideoClip videoClip;
        [SerializeField] private AudioClip audioClip;

        [Tooltip("Total segment duration. Video segments use the clip length when this is zero.")]
        [SerializeField, Min(0f)] private float duration = 1f;

        [SerializeField, Min(0f)] private float fadeInDuration = 0.25f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;
        [SerializeField] private bool useEmbeddedVideoAudio = true;

        public string Label => string.IsNullOrWhiteSpace(label) ? type.ToString() : label;
        public OpeningCinematicSegmentType Type => type;
        public Texture StillImage => stillImage;
        public VideoClip VideoClip => videoClip;
        public AudioClip AudioClip => audioClip;
        public float Duration => Mathf.Max(0f, duration);
        public float FadeInDuration => Mathf.Max(0f, fadeInDuration);
        public float FadeOutDuration => Mathf.Max(0f, fadeOutDuration);
        public bool UseEmbeddedVideoAudio => useEmbeddedVideoAudio;
    }

    [DefaultExecutionOrder(-30000)]
    [DisallowMultipleComponent]
    public sealed class OpeningCinematicController : MonoBehaviour
    {
        private const string IntroSceneName = "IntroCutscene";
        private static OpeningCinematicController _activeInstance;

        [Header("Sequence")]
        [SerializeField] private List<OpeningCinematicSegment> segments = new();
        [SerializeField] private string nextSceneName = "Tutorial";

        [Header("Presentation")]
        [SerializeField] private CanvasGroup cinematicCanvasGroup;
        [SerializeField] private RawImage mediaSurface;
        [SerializeField] private AspectRatioFitter mediaAspectRatio;
        [SerializeField] private Image blackOverlay;
        [SerializeField] private Button skipButton;
        [SerializeField] private GameObject skipPromptRoot;

        [Header("Playback")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private AudioSource mediaAudioSource;
        [SerializeField] private OpeningStaticMotionPresentation staticMotionPresentation;
        [SerializeField] private OpeningUnifiedAnimaticPresentation unifiedAnimaticPresentation;
        [SerializeField] private OpeningSystemBootPresentation systemBootPresentation;
        [SerializeField] private OpeningNarrativeSubtitlePresentation narrativePresentation;
        [SerializeField] private OpeningHoldSkipPresentation holdSkipPresentation;

        [Header("Optional Input Features")]
        [SerializeField] private bool useNarrativeBeatAdvance;
        [SerializeField] private bool useHoldToSkip;
        [SerializeField, Min(0.1f)] private float skipHoldDuration = 0.90f;

        [Header("Safety / Fallback")]
        [SerializeField] private GameObject legacyFallbackRoot;
        [SerializeField, Min(0.1f)] private float videoPrepareTimeout = 8f;
        [SerializeField, Min(0.1f)] private float firstFrameTimeout = 2.5f;
        [SerializeField, Min(1f)] private float videoPlaybackTimeout = 180f;
        [SerializeField, Min(0f)] private float skipRevealDelay = 1.25f;
        [SerializeField, Min(0f)] private float segmentAdvanceCooldown = 0.35f;
        [SerializeField] private bool developmentLogging = true;

        private Coroutine _sequenceRoutine;
        private bool _finishing;
        private bool _usingLegacyFallback;
        private bool _videoPrepared;
        private bool _videoFirstFrameReady;
        private bool _videoEnded;
        private string _videoError;
        private bool _advanceRequested;
        private bool _acceptSegmentAdvance;
        private float _nextAdvanceAllowedAt;
        private float _skipHoldElapsed;
        private bool _skipHoldActive;
        private bool _skipHoldConfirmed;
        private bool _skipPromptRevealed;

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != IntroSceneName)
            {
                Debug.LogWarning(
                    "[Opening Cinematic] This controller is only valid in IntroCutscene.",
                    this);
                enabled = false;
                return;
            }

            if (_activeInstance != null && _activeInstance != this)
            {
                Debug.LogWarning(
                    "[Opening Cinematic] Duplicate controller removed.",
                    this);
                Destroy(gameObject);
                return;
            }

            _activeInstance = this;
            ConfigureRuntimeState();

            if (!ValidateRequiredReferences())
            {
                ActivateLegacyFallback("Required Phase 1 references are missing.");
                return;
            }

            if (legacyFallbackRoot != null)
            {
                legacyFallbackRoot.SetActive(false);
            }

            videoPlayer.prepareCompleted += HandleVideoPrepared;
            videoPlayer.frameReady += HandleVideoFrameReady;
            videoPlayer.loopPointReached += HandleVideoEnded;
            videoPlayer.errorReceived += HandleVideoError;

            skipButton.onClick.RemoveListener(SkipOpening);
            skipButton.onClick.RemoveListener(RequestSegmentAdvance);
            skipButton.onClick.AddListener(HandleAdvanceInput);
        }

        private IEnumerator Start()
        {
            if (_usingLegacyFallback || !enabled)
            {
                yield break;
            }

            if (segments == null || segments.Count == 0)
            {
                ActivateLegacyFallback("No Opening segments are configured.");
                yield break;
            }

            StartCoroutine(RevealSkipPromptRoutine());
            _sequenceRoutine = StartCoroutine(PlaySequenceRoutine());
        }

        private void Update()
        {
            if (_finishing || _usingLegacyFallback)
            {
                return;
            }

            UpdatePromptText();
            if (HandleSkipInput())
            {
                return;
            }

            bool advancePressed =
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame ||
                Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame);

            if (advancePressed && _acceptSegmentAdvance &&
                Time.unscaledTime >= _nextAdvanceAllowedAt)
            {
                HandleAdvanceInput();
            }
        }

        private bool HandleSkipInput()
        {
            if (Keyboard.current == null)
            {
                return false;
            }

            if (!useHoldToSkip)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    SkipOpening();
                    return true;
                }

                return false;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _skipHoldActive = true;
                _skipHoldConfirmed = false;
                _skipHoldElapsed = 0f;
                holdSkipPresentation?.SetHolding(true);
                holdSkipPresentation?.SetProgress(0f);
            }

            if (_skipHoldActive && Keyboard.current.escapeKey.isPressed)
            {
                _skipHoldElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    _skipHoldElapsed / Mathf.Max(0.1f, skipHoldDuration));
                holdSkipPresentation?.SetProgress(progress);
                if (progress >= 1f && !_skipHoldConfirmed)
                {
                    _skipHoldConfirmed = true;
                    SkipOpening();
                }

                return true;
            }

            if (_skipHoldActive && Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                ResetSkipHold(true);
                return true;
            }

            return _skipHoldActive;
        }

        private void HandleAdvanceInput()
        {
            if (!_acceptSegmentAdvance || _skipHoldActive ||
                Time.unscaledTime < _nextAdvanceAllowedAt)
            {
                return;
            }

            if (useNarrativeBeatAdvance && narrativePresentation != null &&
                narrativePresentation.TryConsumeNarrativeAdvance())
            {
                AcceptAdvanceCooldown();
                UpdatePromptText();
                return;
            }

            RequestSegmentAdvance();
        }

        private void RequestSegmentAdvance()
        {
            if (!_acceptSegmentAdvance ||
                Time.unscaledTime < _nextAdvanceAllowedAt)
            {
                return;
            }

            _advanceRequested = true;
            _acceptSegmentAdvance = false;
            AcceptAdvanceCooldown();
        }

        private void AcceptAdvanceCooldown()
        {
            _nextAdvanceAllowedAt = Time.unscaledTime + segmentAdvanceCooldown;
        }

#if UNITY_EDITOR
        private void RequestSegmentAdvanceForDevelopmentProbe()
        {
            RequestSegmentAdvance();
        }

        private void HandleAdvanceInputForDevelopmentProbe()
        {
            HandleAdvanceInput();
        }

        private void SetSkipHoldForDevelopmentProbe(bool held, float deltaTime)
        {
            if (!useHoldToSkip)
            {
                return;
            }

            if (held)
            {
                if (!_skipHoldActive)
                {
                    _skipHoldActive = true;
                    _skipHoldConfirmed = false;
                    _skipHoldElapsed = 0f;
                    holdSkipPresentation?.SetHolding(true);
                }

                _skipHoldElapsed += Mathf.Max(0f, deltaTime);
                float progress = Mathf.Clamp01(
                    _skipHoldElapsed / Mathf.Max(0.1f, skipHoldDuration));
                holdSkipPresentation?.SetProgress(progress);
                if (progress >= 1f && !_skipHoldConfirmed)
                {
                    _skipHoldConfirmed = true;
                    SkipOpening();
                }
            }
            else
            {
                ResetSkipHold(true);
            }
        }
#endif

        private void OnDisable()
        {
            ResetSkipHold(false);
            if (!_usingLegacyFallback)
            {
                StopMedia();
            }
        }

        private void OnDestroy()
        {
            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(SkipOpening);
                skipButton.onClick.RemoveListener(RequestSegmentAdvance);
                skipButton.onClick.RemoveListener(HandleAdvanceInput);
            }

            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= HandleVideoPrepared;
                videoPlayer.frameReady -= HandleVideoFrameReady;
                videoPlayer.loopPointReached -= HandleVideoEnded;
                videoPlayer.errorReceived -= HandleVideoError;
            }

            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
        }

        public void SkipOpening()
        {
            if (_finishing || _usingLegacyFallback)
            {
                return;
            }

            if (developmentLogging && Debug.isDebugBuild)
            {
                Debug.Log("[Opening Cinematic] Skip requested.", this);
            }

            ResetSkipHold(false);
            BeginSceneLoad();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetSkipHold(true);
            }
        }

        private IEnumerator PlaySequenceRoutine()
        {
            bool foundPlayableSegment = false;

            for (int index = 0; index < segments.Count; index++)
            {
                if (_finishing)
                {
                    yield break;
                }

                OpeningCinematicSegment segment = segments[index];
                if (!IsPlayable(segment))
                {
                    Debug.LogWarning(
                        "[Opening Cinematic] Segment " + index +
                        " is missing its media and was skipped.",
                        this);
                    continue;
                }

                foundPlayableSegment = true;
                _advanceRequested = false;
                _acceptSegmentAdvance = true;
                UpdatePromptText();

                if (developmentLogging && Debug.isDebugBuild)
                {
                    Debug.Log(
                        "[Opening Cinematic] Playing " + index + " // " + segment.Label,
                        this);
                }

                switch (segment.Type)
                {
                    case OpeningCinematicSegmentType.Black:
                        yield return PlayBlackSegmentRoutine(segment);
                        break;
                    case OpeningCinematicSegmentType.StillImage:
                        yield return PlayStillSegmentRoutine(index, segment);
                        break;
                    case OpeningCinematicSegmentType.Video:
                        yield return PlayVideoSegmentRoutine(segment);
                        break;
                }

                _acceptSegmentAdvance = false;
            }

            _sequenceRoutine = null;

            if (_finishing)
            {
                yield break;
            }

            if (!foundPlayableSegment)
            {
                ActivateLegacyFallback("No playable Opening segments remain.");
                yield break;
            }

            BeginSceneLoad();
        }

        private IEnumerator PlayBlackSegmentRoutine(OpeningCinematicSegment segment)
        {
            StopMedia();
            SetBlackAlpha(1f);
            PlayAuxiliaryAudio(segment.AudioClip);
            if (narrativePresentation != null)
            {
                narrativePresentation.EndSegment();
            }
            if (systemBootPresentation != null)
            {
                systemBootPresentation.BeginSegment(segment.Label, segment.Duration);
            }

            yield return WaitUnscaledRoutine(segment.Duration, () => _advanceRequested);
            if (systemBootPresentation != null)
            {
                systemBootPresentation.EndSegment();
            }
            StopAuxiliaryAudio();
        }

        private IEnumerator PlayStillSegmentRoutine(
            int segmentIndex,
            OpeningCinematicSegment segment)
        {
            StopMedia();
            ShowMedia(segment.StillImage);
            PlayAuxiliaryAudio(segment.AudioClip);

            float duration = Mathf.Max(0.05f, segment.Duration);
            bool unifiedActive = unifiedAnimaticPresentation != null &&
                                 unifiedAnimaticPresentation.BeginSegment(
                                     segmentIndex,
                                     segment.Label,
                                     duration);
            bool narrativeActive = useNarrativeBeatAdvance &&
                                   narrativePresentation != null &&
                                   narrativePresentation.BeginSegment(segmentIndex);
            UpdatePromptText();
            if (!unifiedActive && staticMotionPresentation != null)
            {
                staticMotionPresentation.BeginShot(duration);
            }

            yield return PlayTimedVisualRoutine(
                duration,
                segment.FadeInDuration,
                segment.FadeOutDuration,
                () => _advanceRequested);

            if (unifiedActive)
            {
                unifiedAnimaticPresentation.EndSegment();
            }
            if (narrativeActive)
            {
                narrativePresentation.EndSegment();
            }
            if (!unifiedActive && staticMotionPresentation != null)
            {
                staticMotionPresentation.EndShot();
            }
            StopMedia();
            ResetMediaTransform();
        }

        private IEnumerator PlayVideoSegmentRoutine(OpeningCinematicSegment segment)
        {
            StopMedia();
            ResetVideoSignals();
            ConfigureVideoAudio(segment);

            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = segment.VideoClip;
            if (segment.AudioClip != null && segment.AudioClip.loadState != AudioDataLoadState.Loaded)
            {
                segment.AudioClip.LoadAudioData();
            }
            videoPlayer.Prepare();

            float elapsed = 0f;
            while (!_videoPrepared && string.IsNullOrEmpty(_videoError) &&
                   elapsed < videoPrepareTimeout && !_finishing)
            {
                if (videoPlayer.isPrepared)
                {
                    _videoPrepared = true;
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_finishing)
            {
                yield break;
            }

            if (!_videoPrepared || !string.IsNullOrEmpty(_videoError))
            {
                WarnVideoFailure(segment, "prepare", elapsed);
                StopMedia();
                yield break;
            }

            elapsed = 0f;
            while (segment.AudioClip != null &&
                   segment.AudioClip.loadState == AudioDataLoadState.Loading &&
                   elapsed < videoPrepareTimeout && !_finishing)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (segment.AudioClip != null &&
                segment.AudioClip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogWarning(
                    "[Opening Cinematic] Separate audio failed to load for " +
                    segment.Label + "; video will continue silently.",
                    this);
            }

            videoPlayer.Play();
            videoPlayer.Pause();

            elapsed = 0f;
            while (!_videoFirstFrameReady && string.IsNullOrEmpty(_videoError) &&
                   elapsed < firstFrameTimeout && !_finishing)
            {
                if (videoPlayer.texture != null && videoPlayer.frame >= 0)
                {
                    _videoFirstFrameReady = true;
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_finishing)
            {
                yield break;
            }

            if (!_videoFirstFrameReady || !string.IsNullOrEmpty(_videoError))
            {
                WarnVideoFailure(segment, "first frame", elapsed);
                StopMedia();
                yield break;
            }

            ShowMedia(videoPlayer.texture);
            double playbackStartDspTime = AudioSettings.dspTime + 0.05d;
            PlayAuxiliaryAudio(segment.AudioClip, playbackStartDspTime);

            while (AudioSettings.dspTime < playbackStartDspTime && !_finishing)
            {
                yield return null;
            }

            if (_finishing)
            {
                yield break;
            }

            videoPlayer.Play();

            float clipLength = segment.VideoClip != null
                ? (float)segment.VideoClip.length
                : 0f;
            float duration = segment.Duration > 0f
                ? segment.Duration
                : (clipLength > 0f ? clipLength : videoPlaybackTimeout);
            duration = Mathf.Min(duration, videoPlaybackTimeout);

            yield return PlayTimedVisualRoutine(
                duration,
                segment.FadeInDuration,
                segment.FadeOutDuration,
                () => _advanceRequested ||
                      _videoEnded ||
                      !string.IsNullOrEmpty(_videoError));

            if (!string.IsNullOrEmpty(_videoError))
            {
                WarnVideoFailure(segment, "playback", duration);
            }

            StopMedia();
        }

        private IEnumerator PlayTimedVisualRoutine(
            float totalDuration,
            float fadeInDuration,
            float fadeOutDuration,
            Func<bool> shouldEndEarly)
        {
            float fadeIn = Mathf.Min(Mathf.Max(0f, fadeInDuration), totalDuration);
            float fadeOut = Mathf.Min(
                Mathf.Max(0f, fadeOutDuration),
                Mathf.Max(0f, totalDuration - fadeIn));
            float hold = Mathf.Max(0f, totalDuration - fadeIn - fadeOut);

            yield return FadeBlackRoutine(1f, 0f, fadeIn);
            if (_finishing || ShouldEndEarly(shouldEndEarly))
            {
                SetBlackAlpha(1f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < hold && !_finishing && !ShouldEndEarly(shouldEndEarly))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_finishing)
            {
                yield break;
            }

            yield return FadeBlackRoutine(blackOverlay.color.a, 1f, fadeOut);
        }

        private IEnumerator FadeBlackRoutine(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetBlackAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && !_finishing)
            {
                elapsed += Time.unscaledDeltaTime;
                SetBlackAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            if (!_finishing)
            {
                SetBlackAlpha(to);
            }
        }

        private IEnumerator WaitUnscaledRoutine(
            float duration,
            Func<bool> shouldEndEarly = null)
        {
            float elapsed = 0f;
            while (elapsed < duration && !_finishing &&
                   !ShouldEndEarly(shouldEndEarly))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator RevealSkipPromptRoutine()
        {
            if (skipPromptRoot == null)
            {
                yield break;
            }

            _skipPromptRevealed = false;
            ApplySkipPromptVisibility();
            float elapsed = 0f;

            while (elapsed < skipRevealDelay && !_finishing && !_usingLegacyFallback)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_finishing && !_usingLegacyFallback)
            {
                _skipPromptRevealed = true;
                ApplySkipPromptVisibility();
            }
        }

        private void BeginSceneLoad()
        {
            if (_finishing || _usingLegacyFallback)
            {
                return;
            }

            _finishing = true;
            ResetSkipHold(false);
            SetBlackAlpha(1f);
            StopMedia();

            if (skipPromptRoot != null)
            {
                _skipPromptRevealed = false;
                ApplySkipPromptVisibility();
            }

            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            StartCoroutine(LoadNextSceneRoutine());
        }

        private IEnumerator LoadNextSceneRoutine()
        {
            // Render one guaranteed black frame before activating the next scene.
            yield return null;

            if (string.IsNullOrWhiteSpace(nextSceneName) ||
                !Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                _finishing = false;
                ActivateLegacyFallback(
                    "Next scene is missing from Build Settings: " + nextSceneName);
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(
                nextSceneName,
                LoadSceneMode.Single);

            if (operation == null)
            {
                _finishing = false;
                ActivateLegacyFallback("SceneManager.LoadSceneAsync returned null.");
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private void ConfigureRuntimeState()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (cinematicCanvasGroup != null)
            {
                cinematicCanvasGroup.alpha = 1f;
                cinematicCanvasGroup.interactable = true;
                cinematicCanvasGroup.blocksRaycasts = true;
            }

            if (mediaSurface != null)
            {
                mediaSurface.texture = null;
                mediaSurface.gameObject.SetActive(false);
            }

            if (blackOverlay != null)
            {
                blackOverlay.raycastTarget = false;
                SetBlackAlpha(1f);
            }

            if (skipPromptRoot != null)
            {
                skipPromptRoot.SetActive(false);
            }

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = false;
                videoPlayer.renderMode = VideoRenderMode.APIOnly;
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.skipOnDrop = false;
                videoPlayer.sendFrameReadyEvents = true;
                videoPlayer.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
            }

            if (mediaAudioSource != null)
            {
                mediaAudioSource.playOnAwake = false;
                mediaAudioSource.loop = false;
                mediaAudioSource.spatialBlend = 0f;
            }
        }

        private void ConfigureVideoAudio(OpeningCinematicSegment segment)
        {
            // Opening video audio is imported as a separate preloaded AudioClip.
            // Keeping the VideoPlayer silent avoids AudioSampleProvider overflows
            // on scene-local VideoPlayers while the AudioSource remains mixer-routed.
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }

        private void ShowMedia(Texture texture)
        {
            if (mediaSurface == null)
            {
                return;
            }

            mediaSurface.texture = texture;
            mediaSurface.gameObject.SetActive(texture != null);

            if (texture != null && mediaAspectRatio != null && texture.height > 0)
            {
                mediaAspectRatio.aspectRatio = (float)texture.width / texture.height;
            }
        }

        private void ResetMediaTransform()
        {
            if (mediaSurface == null)
            {
                return;
            }

            RectTransform rect = mediaSurface.rectTransform;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
        }

        private void PlayAuxiliaryAudio(
            AudioClip clip,
            double scheduledDspTime = -1d)
        {
            if (mediaAudioSource == null || clip == null)
            {
                return;
            }

            mediaAudioSource.clip = clip;
            if (scheduledDspTime >= 0d)
            {
                mediaAudioSource.PlayScheduled(scheduledDspTime);
            }
            else
            {
                mediaAudioSource.Play();
            }
        }

        private void StopAuxiliaryAudio()
        {
            if (mediaAudioSource == null)
            {
                return;
            }

            mediaAudioSource.Stop();
            mediaAudioSource.clip = null;
        }

        private void StopMedia()
        {
            if (narrativePresentation != null)
            {
                narrativePresentation.EndSegment();
            }

            if (unifiedAnimaticPresentation != null)
            {
                unifiedAnimaticPresentation.EndSegment();
            }

            if (systemBootPresentation != null)
            {
                systemBootPresentation.EndSegment();
            }

            if (staticMotionPresentation != null)
            {
                staticMotionPresentation.EndShot();
            }

            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.clip = null;
            }

            StopAuxiliaryAudio();

            if (mediaSurface != null)
            {
                mediaSurface.texture = null;
                mediaSurface.gameObject.SetActive(false);
            }
        }

        private void SetBlackAlpha(float alpha)
        {
            if (blackOverlay == null)
            {
                return;
            }

            Color color = blackOverlay.color;
            color.a = Mathf.Clamp01(alpha);
            blackOverlay.color = color;
        }

        private bool ValidateRequiredReferences()
        {
            bool valid = true;
            valid &= ValidateRequired(cinematicCanvasGroup, nameof(cinematicCanvasGroup));
            valid &= ValidateRequired(mediaSurface, nameof(mediaSurface));
            valid &= ValidateRequired(mediaAspectRatio, nameof(mediaAspectRatio));
            valid &= ValidateRequired(blackOverlay, nameof(blackOverlay));
            valid &= ValidateRequired(skipButton, nameof(skipButton));
            valid &= ValidateRequired(skipPromptRoot, nameof(skipPromptRoot));
            valid &= ValidateRequired(videoPlayer, nameof(videoPlayer));
            valid &= ValidateRequired(mediaAudioSource, nameof(mediaAudioSource));
            return valid;
        }

        private bool ValidateRequired(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError(
                "[Opening Cinematic] Missing required reference: " + fieldName,
                this);
            return false;
        }

        private void ActivateLegacyFallback(string reason)
        {
            if (_usingLegacyFallback)
            {
                return;
            }

            Debug.LogWarning(
                "[Opening Cinematic] " + reason + " Falling back to the legacy Opening.",
                this);

            _usingLegacyFallback = true;
            _finishing = false;
            ResetSkipHold(false);
            StopAllCoroutines();
            StopMedia();

            if (cinematicCanvasGroup != null)
            {
                cinematicCanvasGroup.alpha = 0f;
                cinematicCanvasGroup.interactable = false;
                cinematicCanvasGroup.blocksRaycasts = false;
            }

            if (legacyFallbackRoot != null)
            {
                legacyFallbackRoot.SetActive(true);
            }
            else
            {
                Debug.LogError(
                    "[Opening Cinematic] Legacy fallback root is also missing.",
                    this);
            }

            enabled = false;
        }

        private void UpdatePromptText()
        {
            if (holdSkipPresentation == null)
            {
                return;
            }

            bool narrativePending = useNarrativeBeatAdvance &&
                                    narrativePresentation != null &&
                                    narrativePresentation.HasPendingNarrativeAdvance;
            holdSkipPresentation.SetNarrativePending(narrativePending);
        }

        private void ApplySkipPromptVisibility()
        {
            if (holdSkipPresentation != null && useHoldToSkip)
            {
                holdSkipPresentation.SetPromptRevealed(_skipPromptRevealed);
                return;
            }

            if (skipPromptRoot != null)
            {
                skipPromptRoot.SetActive(_skipPromptRevealed);
            }
        }

        private void ResetSkipHold(bool fadeVisual)
        {
            _skipHoldActive = false;
            _skipHoldConfirmed = false;
            _skipHoldElapsed = 0f;
            if (holdSkipPresentation == null)
            {
                return;
            }

            if (fadeVisual)
            {
                holdSkipPresentation.SetHolding(false);
            }
            else
            {
                holdSkipPresentation.ResetVisual();
                holdSkipPresentation.SetPromptRevealed(_skipPromptRevealed);
            }
        }

        private static bool IsPlayable(OpeningCinematicSegment segment)
        {
            if (segment == null)
            {
                return false;
            }

            return segment.Type switch
            {
                OpeningCinematicSegmentType.Black => true,
                OpeningCinematicSegmentType.StillImage => segment.StillImage != null,
                OpeningCinematicSegmentType.Video => segment.VideoClip != null,
                _ => false
            };
        }

        private static bool ShouldEndEarly(Func<bool> condition)
        {
            return condition != null && condition();
        }

        private void ResetVideoSignals()
        {
            _videoPrepared = false;
            _videoFirstFrameReady = false;
            _videoEnded = false;
            _videoError = null;
        }

        private void WarnVideoFailure(
            OpeningCinematicSegment segment,
            string stage,
            float elapsed)
        {
            string detail = string.IsNullOrEmpty(_videoError)
                ? "timeout after " + elapsed.ToString("F2") + " seconds"
                : _videoError;
            Debug.LogWarning(
                "[Opening Cinematic] Video segment '" + segment.Label +
                "' failed during " + stage + ": " + detail +
                ". Continuing with the next segment.",
                this);
        }

        private void HandleVideoPrepared(VideoPlayer source)
        {
            _videoPrepared = true;
        }

        private void HandleVideoFrameReady(VideoPlayer source, long frameIndex)
        {
            if (_videoFirstFrameReady)
            {
                return;
            }

            _videoFirstFrameReady = true;
            ShowMedia(source.texture);
        }

        private void HandleVideoEnded(VideoPlayer source)
        {
            _videoEnded = true;
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            _videoError = string.IsNullOrWhiteSpace(message)
                ? "Unknown VideoPlayer error"
                : message;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeInstance = null;
        }
    }
}
