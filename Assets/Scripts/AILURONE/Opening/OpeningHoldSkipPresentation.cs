using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.Opening
{
    [DisallowMultipleComponent]
    public sealed class OpeningHoldSkipPresentation : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Image promptBackground;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private CanvasGroup ringGroup;
        [SerializeField] private OpeningHoldSkipRingGraphic progressRing;
        [SerializeField, Min(0.05f)] private float cancelFadeDuration = 0.12f;
        [SerializeField] private string continuePrompt =
            "LMB / SPACE  CONTINUE        HOLD ESC  SKIP";
        [SerializeField] private string nextPrompt =
            "LMB / SPACE  NEXT            HOLD ESC  SKIP";

        private Color _backgroundColor;
        private Color _textColor;
        private float _fadeElapsed;
        private bool _promptRevealed;
        private bool _holding;
        private bool _fading;
        private bool _initialized;

        public float Progress => progressRing != null ? progressRing.Progress : 0f;
        public bool IsHolding => _holding;

        private void Awake()
        {
            if (promptBackground != null)
            {
                _backgroundColor = promptBackground.color;
            }

            if (promptText != null)
            {
                _textColor = promptText.color;
            }

            _initialized = true;
            ResetVisual();
        }

        private void Update()
        {
            if (!_fading || ringGroup == null)
            {
                return;
            }

            _fadeElapsed += Time.unscaledDeltaTime;
            ringGroup.alpha = 1f - Mathf.Clamp01(_fadeElapsed / cancelFadeDuration);
            if (_fadeElapsed < cancelFadeDuration)
            {
                return;
            }

            _fading = false;
            ringGroup.alpha = 0f;
            progressRing.SetProgress(0f);
            ApplyRootVisibility();
        }

        public void SetPromptRevealed(bool revealed)
        {
            _promptRevealed = revealed;
            ApplyPromptVisibility();
            ApplyRootVisibility();
        }

        public void SetNarrativePending(bool narrativePending)
        {
            if (promptText != null)
            {
                promptText.text = narrativePending ? continuePrompt : nextPrompt;
            }
        }

        public void SetHolding(bool holding)
        {
            if (holding)
            {
                _holding = true;
                _fading = false;
                _fadeElapsed = 0f;
                promptRoot.SetActive(true);
                ringGroup.alpha = 1f;
                return;
            }

            if (!_holding)
            {
                return;
            }

            _holding = false;
            _fading = true;
            _fadeElapsed = 0f;
        }

        public void SetProgress(float progress)
        {
            progressRing.SetProgress(progress);
            if (progress >= 1f)
            {
                ringGroup.alpha = 1f;
            }
        }

        public void ResetVisual()
        {
            EnsureInitialized();
            _holding = false;
            _fading = false;
            _fadeElapsed = 0f;
            _promptRevealed = false;

            if (progressRing != null)
            {
                progressRing.SetProgress(0f);
            }

            if (ringGroup != null)
            {
                ringGroup.alpha = 0f;
            }

            ApplyPromptVisibility();
            ApplyRootVisibility();
        }

        private void ApplyPromptVisibility()
        {
            EnsureInitialized();
            if (promptBackground != null)
            {
                Color color = _backgroundColor;
                color.a = _promptRevealed ? _backgroundColor.a : 0f;
                promptBackground.color = color;
            }

            if (promptText != null)
            {
                Color color = _textColor;
                color.a = _promptRevealed ? _textColor.a : 0f;
                promptText.color = color;
            }
        }

        private void ApplyRootVisibility()
        {
            if (promptRoot != null)
            {
                promptRoot.SetActive(_promptRevealed || _holding || _fading);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (promptBackground != null)
            {
                _backgroundColor = promptBackground.color;
            }

            if (promptText != null)
            {
                _textColor = promptText.color;
            }

            _initialized = true;
        }
    }
}
