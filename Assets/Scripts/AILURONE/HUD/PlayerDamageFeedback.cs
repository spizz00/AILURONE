#pragma warning disable 0618
#pragma warning disable 0414
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Subtle full-screen pulse feedback.
    ///
    /// This replaces the old long red/blue screen-edge bars with one
    /// low-opacity tint that fades quickly and never forms a frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDamageFeedback : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Feedback")]
        [SerializeField] private CanvasGroup feedbackCanvasGroup;
        [SerializeField] private RectTransform feedbackRoot;
        [SerializeField] private Image feedbackTint;

        [Header("Damage Pulse")]
        [SerializeField] private Color damagePulseColor =
            new Color(1.00f, 0.03f, 0.14f, 1f);

        [Range(0f, 0.30f)]
        [SerializeField] private float damagePeakAlpha = 0.115f;

        [Min(0.05f)]
        [SerializeField] private float damageDuration = 0.19f;

        [Header("Rewind Pulse")]
        [SerializeField] private Color rewindStartColor =
            new Color(0.62f, 0.94f, 1.00f, 1f);

        [Range(0f, 0.30f)]
        [SerializeField] private float rewindStartPeakAlpha = 0.145f;

        [Min(0.05f)]
        [SerializeField] private float rewindStartDuration = 0.18f;

        [SerializeField] private Color rewindCompleteColor =
            new Color(0.74f, 0.98f, 1.00f, 1f);

        [Range(0f, 0.30f)]
        [SerializeField] private float rewindCompletePeakAlpha = 0.085f;

        [Min(0.05f)]
        [SerializeField] private float rewindCompleteDuration = 0.13f;

        [Header("Debug")]
        [SerializeField] private bool logMissingReferences;

        private bool _subscribed;
        private bool _pulseActive;
        private float _pulseTime;
        private float _pulseDuration;
        private float _pulsePeakAlpha;
        private Color _pulseColor;

        private void Awake()
        {
            ResolvePlayerHealth();
            HideImmediately();
        }

        private void OnEnable()
        {
            ResolvePlayerHealth();
            Subscribe();
            HideImmediately();
        }

        private void Update()
        {
            ResolvePlayerHealthIfNeeded();

            if (!_pulseActive)
            {
                return;
            }

            _pulseTime += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    _pulseTime
                    / Mathf.Max(0.05f, _pulseDuration)
                );

            float fade =
                Mathf.Pow(1f - progress, 2.35f);

            if (feedbackCanvasGroup != null)
            {
                feedbackCanvasGroup.alpha =
                    _pulsePeakAlpha * fade;
            }

            if (progress >= 1f)
            {
                HideImmediately();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideImmediately();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void Configure(
            PlayerHealth health,
            CanvasGroup canvasGroup,
            RectTransform root,
            Image tint
        )
        {
            Unsubscribe();

            playerHealth = health;
            feedbackCanvasGroup = canvasGroup;
            feedbackRoot = root;
            feedbackTint = tint;

            Subscribe();
            HideImmediately();
        }

        /// <summary>
        /// Legacy overload retained so older HUD installers still compile.
        /// The old edge fragments and label are deliberately ignored.
        /// </summary>
        public void Configure(
            PlayerHealth health,
            CanvasGroup canvasGroup,
            RectTransform root,
            Image[] fragments,
            TMP_Text label
        )
        {
            Image legacyTint = null;

            if (fragments != null
                && fragments.Length > 0)
            {
                legacyTint = fragments[0];
            }

            Configure(
                health,
                canvasGroup,
                root,
                legacyTint
            );

            if (fragments != null)
            {
                for (int index = 0;
                    index < fragments.Length;
                    index++)
                {
                    Image fragment = fragments[index];

                    if (fragment != null
                        && fragment != feedbackTint)
                    {
                        fragment.enabled = false;
                    }
                }
            }

            if (label != null)
            {
                label.enabled = false;
            }
        }

        private void HandleDamaged(
            float actualDamage,
            float remainingHealth
        )
        {
            if (playerHealth != null
                && playerHealth.IsRewinding)
            {
                return;
            }

            PlayPulse(
                damagePulseColor,
                damagePeakAlpha,
                damageDuration
            );
        }

        private void HandleRewindStarted()
        {
            PlayPulse(
                rewindStartColor,
                rewindStartPeakAlpha,
                rewindStartDuration
            );
        }

        private void HandleRewindCompleted()
        {
            PlayPulse(
                rewindCompleteColor,
                rewindCompletePeakAlpha,
                rewindCompleteDuration
            );
        }

        private void PlayPulse(
            Color color,
            float peakAlpha,
            float duration
        )
        {
            _pulseActive = true;
            _pulseTime = 0f;
            _pulseDuration =
                Mathf.Max(0.05f, duration);
            _pulsePeakAlpha =
                Mathf.Clamp01(peakAlpha);
            _pulseColor = color;

            if (feedbackTint != null)
            {
                feedbackTint.enabled = true;
                feedbackTint.color = _pulseColor;
            }

            if (feedbackCanvasGroup != null)
            {
                feedbackCanvasGroup.alpha =
                    _pulsePeakAlpha;

                feedbackCanvasGroup.interactable = false;
                feedbackCanvasGroup.blocksRaycasts = false;
            }
        }

        private void ResolvePlayerHealth()
        {
            if (playerHealth != null)
            {
                return;
            }

            if (PlayerHealth.Instance != null)
            {
                playerHealth = PlayerHealth.Instance;
                return;
            }

            PlayerHealth[] candidates =
                FindObjectsByType<PlayerHealth>(
                    FindObjectsInactive.Exclude
                );

            if (candidates.Length > 0)
            {
                playerHealth = candidates[0];
            }

            if (playerHealth == null
                && logMissingReferences)
            {
                Debug.LogWarning(
                    "[PlayerDamageFeedback] PlayerHealth was not found.",
                    this
                );
            }
        }

        private void ResolvePlayerHealthIfNeeded()
        {
            if (playerHealth != null)
            {
                return;
            }

            ResolvePlayerHealth();
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || playerHealth == null)
            {
                return;
            }

            playerHealth.Damaged += HandleDamaged;
            playerHealth.RewindStarted += HandleRewindStarted;
            playerHealth.RewindCompleted += HandleRewindCompleted;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || playerHealth == null)
            {
                _subscribed = false;
                return;
            }

            playerHealth.Damaged -= HandleDamaged;
            playerHealth.RewindStarted -= HandleRewindStarted;
            playerHealth.RewindCompleted -= HandleRewindCompleted;

            _subscribed = false;
        }

        private void HideImmediately()
        {
            _pulseActive = false;
            _pulseTime = 0f;

            if (feedbackCanvasGroup != null)
            {
                feedbackCanvasGroup.alpha = 0f;
                feedbackCanvasGroup.interactable = false;
                feedbackCanvasGroup.blocksRaycasts = false;
            }

            if (feedbackRoot != null)
            {
                feedbackRoot.localScale = Vector3.one;
            }

            if (feedbackTint != null)
            {
                feedbackTint.enabled = true;
            }
        }
    }
}
