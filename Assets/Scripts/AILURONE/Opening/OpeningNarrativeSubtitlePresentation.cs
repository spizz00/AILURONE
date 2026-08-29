using System;
using TMPro;
using UnityEngine;

namespace AILURONE.Opening
{
    [DisallowMultipleComponent]
    public sealed class OpeningNarrativeSubtitlePresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class NarrativeBeat
        {
            [SerializeField, TextArea] private string text;
            [SerializeField, Min(0f)] private float automaticRevealTime;

            public string Text => text;
            public float AutomaticRevealTime => Mathf.Max(0f, automaticRevealTime);
        }

        [Serializable]
        public sealed class ShotNarrative
        {
            [SerializeField] private NarrativeBeat[] beats = Array.Empty<NarrativeBeat>();
            [SerializeField, Min(0f)] private float endingFadeTime = 4.45f;

            public NarrativeBeat[] Beats => beats;
            public float EndingFadeTime => Mathf.Max(0f, endingFadeTime);
        }

        private enum TransitionState
        {
            Hidden,
            FadingIn,
            Visible,
            CrossfadeOut,
            EndingFadeOut
        }

        [Header("UI")]
        [SerializeField] private GameObject presentationRoot;
        [SerializeField] private CanvasGroup presentationGroup;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private OpeningSoftPanelGraphic readabilityGradient;
        [SerializeField] private OpeningSoftPanelGraphic subtitleBackdrop;

        [Header("Typewriter Effect")]
        [SerializeField, Min(1f)] private float charactersPerSecond = 55f;
        [SerializeField] private AudioClip continuousTypingClip;
        [SerializeField] private AudioSource typewriterAudioSource;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float beatFadeInDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float beatCrossfadeDuration = 0.12f;
        [SerializeField, Min(0.05f)] private float shotEndingFadeOutDuration = 0.18f;
        [SerializeField, Min(0f)] private float finalBeatMinimumVisibleTime = 0.35f;

        [Header("Narrative")]
        [SerializeField] private ShotNarrative[] shots = Array.Empty<ShotNarrative>();

        private ShotNarrative _activeShot;
        private TransitionState _state;
        private float _shotElapsed;
        private float _transitionElapsed;
        private float _finalBeatVisibleElapsed;
        private int _activeShotIndex = -1;
        private int _currentBeatIndex = -1;
        private int _pendingBeatIndex = -1;
        private bool _active;
        private bool _endingFadeStarted;
        private bool _finalBeatVisibilityStarted;

        private float _typewriterElapsed;
        private int _lastVisibleCharacterCount;

        public bool IsActive => _active;
        public int ActiveShotIndex => _activeShotIndex;
        public int CurrentBeatIndex => _currentBeatIndex;
        public bool IsTransitioning =>
            _state == TransitionState.FadingIn ||
            _state == TransitionState.CrossfadeOut ||
            _state == TransitionState.EndingFadeOut;
        public float CurrentAlpha => presentationGroup != null ? presentationGroup.alpha : 0f;
        public float FinalBeatVisibleElapsed => _finalBeatVisibleElapsed;
        public OpeningSoftPanelGraphic ReadabilityGradient => readabilityGradient;
        public OpeningSoftPanelGraphic SubtitleBackdrop => subtitleBackdrop;
        public bool HasReadabilityLayers =>
            readabilityGradient != null && subtitleBackdrop != null;
        public bool HasPendingNarrativeAdvance =>
            _active && _activeShot != null &&
            (_currentBeatIndex < _activeShot.Beats.Length - 1 ||
             _state != TransitionState.Visible ||
             _finalBeatVisibleElapsed < finalBeatMinimumVisibleTime);

        private void Awake()
        {
            ResetPresentation();
        }

        private void Update()
        {
            if (!_active || _activeShot == null)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            _shotElapsed += delta;
            UpdateTransition(delta);
            UpdateAutomaticBeats();
            UpdateTypewriter(delta);

            if (_state == TransitionState.Visible && IsFinalBeat())
            {
                if (_finalBeatVisibilityStarted)
                {
                    _finalBeatVisibleElapsed += delta;
                }
                else
                {
                    _finalBeatVisibilityStarted = true;
                }
            }

            if (_shotElapsed >= _activeShot.EndingFadeTime)
            {
                BeginEndingFade();
            }
        }

        public bool BeginSegment(int segmentIndex)
        {
            EndSegment();
            if (segmentIndex < 0 || segmentIndex >= shots.Length ||
                segmentIndex >= 5 || shots[segmentIndex] == null ||
                shots[segmentIndex].Beats == null ||
                shots[segmentIndex].Beats.Length == 0)
            {
                return false;
            }

            _active = true;
            _activeShotIndex = segmentIndex;
            _activeShot = shots[segmentIndex];
            _state = TransitionState.Hidden;
            _shotElapsed = 0f;
            _transitionElapsed = 0f;
            _finalBeatVisibleElapsed = 0f;
            _currentBeatIndex = -1;
            _pendingBeatIndex = -1;
            _endingFadeStarted = false;
            _finalBeatVisibilityStarted = false;
            subtitleText.text = string.Empty;
            presentationGroup.alpha = 0f;
            presentationRoot.SetActive(true);
            return true;
        }

        public void EndSegment()
        {
            ResetPresentation();
        }

        public bool TryConsumeNarrativeAdvance()
        {
            if (!_active || _activeShot == null)
            {
                return false;
            }

            if (_state == TransitionState.FadingIn)
            {
                FinishFadeIn();
                return true;
            }

            if (_state == TransitionState.CrossfadeOut)
            {
                if (_pendingBeatIndex >= 0)
                {
                    _currentBeatIndex = _pendingBeatIndex;
                    _pendingBeatIndex = -1;
                    subtitleText.text = _activeShot.Beats[_currentBeatIndex].Text;
                }

                presentationGroup.alpha = 1f;
                _transitionElapsed = 0f;
                _state = TransitionState.FadingIn;
                return true;
            }

            if (_state == TransitionState.EndingFadeOut)
            {
                return false;
            }

            if (_currentBeatIndex < 0)
            {
                RevealBeat(0, false);
                return true;
            }

            if (_currentBeatIndex < _activeShot.Beats.Length - 1)
            {
                RevealBeat(_currentBeatIndex + 1, true);
                return true;
            }

            if (_finalBeatVisibleElapsed < finalBeatMinimumVisibleTime)
            {
                return true;
            }

            return false;
        }

        public void BeginEndingFade()
        {
            if (!_active || _endingFadeStarted ||
                _state == TransitionState.Hidden ||
                _state == TransitionState.EndingFadeOut)
            {
                return;
            }

            _endingFadeStarted = true;
            _state = TransitionState.EndingFadeOut;
            _transitionElapsed = 0f;
        }

        private void UpdateAutomaticBeats()
        {
            NarrativeBeat[] beats = _activeShot.Beats;
            for (int index = 0; index < beats.Length; index++)
            {
                if (index <= _currentBeatIndex || index == _pendingBeatIndex ||
                    _shotElapsed < beats[index].AutomaticRevealTime)
                {
                    continue;
                }

                RevealBeat(index, _currentBeatIndex >= 0);
                break;
            }
        }

        private void UpdateTypewriter(float delta)
        {
            if (subtitleText == null || _currentBeatIndex < 0 || _state == TransitionState.Hidden)
            {
                return;
            }

            int totalChars = subtitleText.text.Length;
            if (subtitleText.maxVisibleCharacters < totalChars)
            {
                _typewriterElapsed += delta;
                int targetCharCount = Mathf.FloorToInt(_typewriterElapsed * charactersPerSecond);
                
                if (targetCharCount > _lastVisibleCharacterCount)
                {
                    subtitleText.maxVisibleCharacters = targetCharCount;
                    _lastVisibleCharacterCount = targetCharCount;
                    
                    if (targetCharCount >= totalChars)
                    {
                        if (typewriterAudioSource != null)
                        {
                            typewriterAudioSource.Stop();
                        }
                    }
                }
            }
        }

        private void RevealBeat(int beatIndex, bool crossfade)
        {
            if (_activeShot == null || beatIndex < 0 ||
                beatIndex >= _activeShot.Beats.Length ||
                beatIndex <= _currentBeatIndex || beatIndex == _pendingBeatIndex)
            {
                return;
            }

            _finalBeatVisibleElapsed = 0f;
            _finalBeatVisibilityStarted = false;
            _transitionElapsed = 0f;
            _typewriterElapsed = 0f;
            _lastVisibleCharacterCount = 0;
            
            if (crossfade && _currentBeatIndex >= 0)
            {
                _pendingBeatIndex = beatIndex;
                _state = TransitionState.CrossfadeOut;
                return;
            }

            _currentBeatIndex = beatIndex;
            _pendingBeatIndex = -1;
            subtitleText.text = _activeShot.Beats[beatIndex].Text;
            subtitleText.maxVisibleCharacters = 0;
            presentationGroup.alpha = 0f;
            _state = TransitionState.FadingIn;
            
            if (typewriterAudioSource != null && continuousTypingClip != null)
            {
                typewriterAudioSource.clip = continuousTypingClip;
                typewriterAudioSource.loop = true;
                typewriterAudioSource.Play();
            }
        }

        private void UpdateTransition(float delta)
        {
            _transitionElapsed += delta;
            switch (_state)
            {
                case TransitionState.FadingIn:
                    presentationGroup.alpha = Mathf.Clamp01(
                        _transitionElapsed / beatFadeInDuration);
                    if (_transitionElapsed >= beatFadeInDuration)
                    {
                        FinishFadeIn();
                    }
                    break;

                case TransitionState.CrossfadeOut:
                    float half = beatCrossfadeDuration * 0.5f;
                    if (_transitionElapsed < half)
                    {
                        presentationGroup.alpha = 1f - Mathf.Clamp01(
                            _transitionElapsed / Mathf.Max(0.01f, half));
                    }
                    else
                    {
                        if (_pendingBeatIndex >= 0)
                        {
                            _currentBeatIndex = _pendingBeatIndex;
                            _pendingBeatIndex = -1;
                            subtitleText.text = _activeShot.Beats[_currentBeatIndex].Text;
                            subtitleText.maxVisibleCharacters = 0;
                            _typewriterElapsed = 0f;
                            _lastVisibleCharacterCount = 0;
                            
                            if (typewriterAudioSource != null && continuousTypingClip != null)
                            {
                                typewriterAudioSource.clip = continuousTypingClip;
                                typewriterAudioSource.loop = true;
                                typewriterAudioSource.Play();
                            }
                        }

                        presentationGroup.alpha = Mathf.Clamp01(
                            (_transitionElapsed - half) / Mathf.Max(0.01f, half));
                    }

                    if (_transitionElapsed >= beatCrossfadeDuration)
                    {
                        CompleteCrossfadeImmediately();
                    }
                    break;

                case TransitionState.EndingFadeOut:
                    presentationGroup.alpha = 1f - Mathf.Clamp01(
                        _transitionElapsed / shotEndingFadeOutDuration);
                    if (_transitionElapsed >= shotEndingFadeOutDuration)
                    {
                        presentationGroup.alpha = 0f;
                        _state = TransitionState.Hidden;
                    }
                    break;
            }
        }

        private void FinishFadeIn()
        {
            presentationGroup.alpha = 1f;
            _transitionElapsed = 0f;
            _state = TransitionState.Visible;
            if (IsFinalBeat())
            {
                _finalBeatVisibleElapsed = 0f;
                _finalBeatVisibilityStarted = false;
            }
        }

        private void CompleteCrossfadeImmediately()
        {
            if (_pendingBeatIndex >= 0)
            {
                _currentBeatIndex = _pendingBeatIndex;
                _pendingBeatIndex = -1;
                subtitleText.text = _activeShot.Beats[_currentBeatIndex].Text;
                subtitleText.maxVisibleCharacters = 0;
                _typewriterElapsed = 0f;
                _lastVisibleCharacterCount = 0;
                
                if (typewriterAudioSource != null && continuousTypingClip != null)
                {
                    typewriterAudioSource.clip = continuousTypingClip;
                    typewriterAudioSource.loop = true;
                    typewriterAudioSource.Play();
                }
            }

            presentationGroup.alpha = 1f;
            _transitionElapsed = 0f;
            _state = TransitionState.Visible;
            if (IsFinalBeat())
            {
                _finalBeatVisibleElapsed = 0f;
                _finalBeatVisibilityStarted = false;
            }
        }

        private bool IsFinalBeat()
        {
            return _activeShot != null && _currentBeatIndex >= 0 &&
                   _currentBeatIndex == _activeShot.Beats.Length - 1;
        }

        private void ResetPresentation()
        {
            _active = false;
            _activeShot = null;
            _activeShotIndex = -1;
            _currentBeatIndex = -1;
            _pendingBeatIndex = -1;
            _shotElapsed = 0f;
            _transitionElapsed = 0f;
            _finalBeatVisibleElapsed = 0f;
            _typewriterElapsed = 0f;
            _lastVisibleCharacterCount = 0;
            _endingFadeStarted = false;
            _finalBeatVisibilityStarted = false;
            _state = TransitionState.Hidden;

            if (typewriterAudioSource != null)
            {
                typewriterAudioSource.Stop();
            }

            if (subtitleText != null)
            {
                subtitleText.text = string.Empty;
                subtitleText.maxVisibleCharacters = 99999;
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
