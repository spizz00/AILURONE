#pragma warning disable 0618
#pragma warning disable 0414
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Stable standard-UGUI Integrity module.
    ///
    /// The approved portrait remains a normal Image. A second idempotent
    /// runtime Image uses AILURONE/UI/PortraitGlitch for low-health pixel
    /// corruption, RGB separation, horizontal tearing and impact bursts.
    ///
    /// No custom MaskableGraphic or runtime mesh generation is required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerIntegrityHUD : MonoBehaviour
    {
        private enum RebuildDisplayStage
        {
            None,
            SystemLost,
            Reconstructing,
            Restored
        }

        [Serializable]
        public struct BarSegment
        {
            public RectTransform currentFill;
            public RectTransform damageGhost;
            public Image currentImage;
            public Image ghostImage;

            [Range(0f, 1f)]
            public float startRatio;

            [Range(0f, 1f)]
            public float endRatio;

            [HideInInspector]
            public float maximumWidth;
        }

        [Header("Player")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Module")]
        [SerializeField] private CanvasGroup moduleCanvasGroup;
        [SerializeField] private RectTransform visualRoot;

        [Header("Text")]
        [SerializeField] private TMP_Text identityText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text coreSystemsText;
        [SerializeField] private TMP_Text onlineText;

        [Header("Standard UI Bar")]
        [SerializeField] private BarSegment[] barSegments =
            Array.Empty<BarSegment>();

        [SerializeField] private Image[] accentImages =
            Array.Empty<Image>();

        [SerializeField] private Image[] warningOnlyImages =
            Array.Empty<Image>();

        [SerializeField] private RectTransform rebootScan;

        [Header("State Colours")]
        [SerializeField] private Color normalFillColor =
            new Color(0.94f, 0.98f, 1.00f, 1f);

        [SerializeField] private Color normalAccentColor =
            new Color(0.18f, 0.89f, 1.00f, 1f);

        [SerializeField] private Color degradedAccentColor =
            new Color(1.00f, 0.54f, 0.20f, 1f);

        [SerializeField] private Color criticalFillColor =
            new Color(1.00f, 0.04f, 0.42f, 1f);

        [SerializeField] private Color damageGhostColor =
            new Color(1.00f, 0.035f, 0.16f, 0.94f);

        [SerializeField] private Color rebootColor =
            new Color(0.64f, 0.95f, 1.00f, 1f);

        [Header("Thresholds")]
        [Range(0.05f, 0.60f)]
        [SerializeField] private float criticalThreshold = 0.30f;

        [Range(0.20f, 0.90f)]
        [SerializeField] private float degradedThreshold = 0.60f;

        [Header("Portrait Glitch")]
        [Tooltip("Optional persistent material template. If empty, the HUD creates a private runtime material from the glitch shader.")]
        [SerializeField] private Material portraitGlitchMaterialTemplate;

        [Tooltip("Optional head-priority mask. If empty, the HUD also attempts to load AILURONE/HUD/IntegrityHUD/AILURONE_PortraitGlitchHeadMask from Resources.")]
        [SerializeField] private Texture2D portraitGlitchHeadMask;

        [Range(0.31f, 0.60f)]
        [SerializeField] private float portraitGlitchExitThreshold = 0.35f;

        [Min(0.10f)]
        [SerializeField] private float portraitGlitchFadeOutDuration = 0.90f;

        [Min(0.05f)]
        [SerializeField] private float portraitDamageBurstDuration = 0.21f;

        [Range(0f, 1f)]
        [SerializeField] private float portraitDamageBurstMinimum = 0.42f;

        [Range(0.1f, 3f)]
        [SerializeField] private float portraitHeadGlitchMultiplier = 1.90f;

        [Range(0.1f, 2f)]
        [SerializeField] private float portraitBodyGlitchMultiplier = 0.72f;

        [Range(0f, 1f)]
        [SerializeField] private float portraitDamageRedFlashStrength = 0.30f;

        [SerializeField] private Color portraitGlitchCyan =
            new Color(0.00f, 1.00f, 0.96f, 1f);

        [SerializeField] private Color portraitGlitchMagenta =
            new Color(1.00f, 0.00f, 0.76f, 1f);

        [Header("Animation - Unscaled Time")]
        [Min(0.1f)]
        [SerializeField] private float displayedHealthSpeed = 9.5f;

        [Min(0f)]
        [SerializeField] private float damageGhostHoldDuration = 0.17f;

        [Min(0.1f)]
        [SerializeField] private float damageGhostDrainSpeed = 1.35f;

        [Min(0f)]
        [SerializeField] private float damageKickDuration = 0.08f;

        [Range(0f, 10f)]
        [SerializeField] private float damageKickPixels = 1.5f;

        [Min(0f)]
        [SerializeField] private float impactStatusDuration = 0.22f;

        [Min(0f)]
        [SerializeField] private float valueFlashDuration = 0.16f;

        [Min(0f)]
        [SerializeField] private float systemLostDuration = 0.12f;

        [Min(0f)]
        [SerializeField] private float restoredStatusDuration = 0.26f;

        [Min(0.1f)]
        [SerializeField] private float rebootScanSpeed = 0.82f;

        [Header("Debug")]
        [SerializeField] private bool logMissingReferences;

        private bool _subscribed;
        private bool _isRewinding;
        private float _tutorialVisibilityAlpha = 1f;

        private float _displayedRatio = 1f;
        private float _ghostRatio = 1f;
        private float _ghostHoldTimer;

        private float _damageKickTimer;
        private float _impactTimer;
        private float _valueFlashTimer;

        private RebuildDisplayStage _rebuildStage;
        private float _rebuildStageTimer;

        private Vector2 _baseAnchoredPosition;
        private bool _basePositionCaptured;

        private float _scanMinimumX;
        private float _scanMaximumX;
        private bool _scanBoundsCaptured;

        private int _lastHealthValue = int.MinValue;
        private string _lastStatus;
        private Color _lastStatusColor;

        private Image _portraitMain;
        private Color _portraitMainBaseColor = Color.white;
        private AILURONEIntegrityPathGraphic _integrityPath;

        private const float ApprovedIntegrityFullWidth = 405f;

        private RectTransform _approvedCurrentClip;
        private RectTransform _approvedGhostClip;

        private Image[] _approvedCurrentImages =
            Array.Empty<Image>();

        private Image[] _approvedGhostImages =
            Array.Empty<Image>();

        private Image _approvedNodePlus;
        private Image _approvedPortrait;

        private Image _approvedFramePlusLeftHorizontal;
        private Image _approvedFramePlusLeftVertical;
        private Image _approvedFramePlusRightHorizontal;
        private Image _approvedFramePlusRightVertical;

        private Image _portraitGlitchOverlay;
        private Material _portraitGlitchMaterial;
        private Texture2D _resolvedPortraitGlitchHeadMask;

        private bool _portraitGlitchLatched;
        private float _portraitGlitchAmount;

        private float _damageGlitchBurstTimer;
        private float _damageGlitchBurstDuration;
        private float _damageGlitchBurstStrength;

        private float _randomGlitchBurstTimer;
        private float _randomGlitchBurstDuration;
        private float _randomGlitchBurstStrength;
        private float _nextRandomGlitchTime;

        private float _portraitGlitchSeed;
        private bool _missingGlitchShaderLogged;

        private const string RuntimePortraitGlitchOverlayName =
            "Runtime_PortraitGlitchOverlay";

        private const string PortraitGlitchShaderName =
            "AILURONE/UI/PortraitGlitch";

        private const string PortraitGlitchHeadMaskResourcePath =
            "AILURONE/HUD/IntegrityHUD/AILURONE_PortraitGlitchHeadMask";

        private static readonly int GlitchAmountProperty =
            Shader.PropertyToID("_GlitchAmount");

        private static readonly int BurstAmountProperty =
            Shader.PropertyToID("_BurstAmount");

        private static readonly int GlitchSeedProperty =
            Shader.PropertyToID("_GlitchSeed");

        private static readonly int HeadStrengthProperty =
            Shader.PropertyToID("_HeadStrength");

        private static readonly int BodyStrengthProperty =
            Shader.PropertyToID("_BodyStrength");

        private static readonly int CyanColorProperty =
            Shader.PropertyToID("_CyanColor");

        private static readonly int MagentaColorProperty =
            Shader.PropertyToID("_MagentaColor");

        private static readonly int HeadMaskProperty =
            Shader.PropertyToID("_HeadMask");

        public PlayerHealth PlayerHealth => playerHealth;

        public float TutorialVisibilityAlpha => _tutorialVisibilityAlpha;

        public void SetTutorialVisibilityAlpha(float alpha)
        {
            _tutorialVisibilityAlpha = Mathf.Clamp01(alpha);
            ApplyTutorialVisibility();
        }

        private void Awake()
        {
            ResolvePlayerHealth();
            ApplyApprovedPresentation();
            _basePositionCaptured = false;
            _scanBoundsCaptured = false;
            CaptureRuntimeGeometry();
            InitialiseDisplay();
        }

        private void OnEnable()
        {
            ResolvePlayerHealth();
            ApplyApprovedPresentation();
            _basePositionCaptured = false;
            _scanBoundsCaptured = false;
            CaptureRuntimeGeometry();
            Subscribe();
            InitialiseDisplay();
        }

        private void Update()
        {
            ResolvePlayerHealthIfNeeded();

            if (playerHealth == null)
            {
                return;
            }

            if (_impactTimer > 0f)
            {
                _impactTimer -= Time.unscaledDeltaTime;
            }

            if (_valueFlashTimer > 0f)
            {
                _valueFlashTimer -= Time.unscaledDeltaTime;
            }

            bool rewindState = playerHealth.IsRewinding;

            if (rewindState != _isRewinding)
            {
                _isRewinding = rewindState;
            }

            UpdateRebuildDisplayStage();

            float maximum =
                Mathf.Max(1f, playerHealth.maxHealth);

            float actualRatio =
                Mathf.Clamp01(
                    playerHealth.currentHealth / maximum
                );

            if (_isRewinding)
            {
                _displayedRatio =
                    Mathf.MoveTowards(
                        _displayedRatio,
                        actualRatio,
                        displayedHealthSpeed
                            * Time.unscaledDeltaTime
                    );

                _ghostRatio =
                    Mathf.Max(_ghostRatio, _displayedRatio);
            }
            else
            {
                _displayedRatio =
                    Mathf.MoveTowards(
                        _displayedRatio,
                        actualRatio,
                        displayedHealthSpeed
                            * Time.unscaledDeltaTime
                    );

                if (_ghostHoldTimer > 0f)
                {
                    _ghostHoldTimer -=
                        Time.unscaledDeltaTime;
                }
                else
                {
                    _ghostRatio =
                        Mathf.MoveTowards(
                            _ghostRatio,
                            actualRatio,
                            damageGhostDrainSpeed
                                * Time.unscaledDeltaTime
                        );
                }

                if (actualRatio > _ghostRatio)
                {
                    _ghostRatio = actualRatio;
                }
            }

            UpdateDamageKick();
            UpdateRebootScan();
            UpdatePortraitFeedback(actualRatio);

            Render(
                actualRatio,
                Mathf.RoundToInt(playerHealth.currentHealth)
            );

            ApplyTutorialVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetVisualRootPosition();

            _impactTimer = 0f;
            ResetPortraitFeedback();

            if (rebootScan != null)
            {
                rebootScan.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DestroyPortraitGlitchMaterial();
        }

        public void Configure(
            PlayerHealth health,
            CanvasGroup canvasGroup,
            RectTransform root,
            TMP_Text identity,
            TMP_Text value,
            TMP_Text integrityLabel,
            TMP_Text status,
            TMP_Text coreSystems,
            TMP_Text online,
            BarSegment[] segments,
            Image[] accents,
            Image[] warningImages,
            RectTransform scan
        )
        {
            Unsubscribe();

            playerHealth = health;
            moduleCanvasGroup = canvasGroup;
            visualRoot = root;

            identityText = identity;
            valueText = value;
            labelText = integrityLabel;
            statusText = status;
            coreSystemsText = coreSystems;
            onlineText = online;

            barSegments =
                segments ?? Array.Empty<BarSegment>();

            accentImages =
                accents ?? Array.Empty<Image>();

            warningOnlyImages =
                warningImages ?? Array.Empty<Image>();

            rebootScan = scan;

            _basePositionCaptured = false;
            _scanBoundsCaptured = false;

            ApplyApprovedPresentation();
            CaptureRuntimeGeometry();
            Subscribe();
            InitialiseDisplay();
        }

        private void ApplyApprovedPresentation()
        {
            normalFillColor =
                AILURONEHUDRuntimeStyle.White;

            normalAccentColor =
                AILURONEHUDRuntimeStyle.Cyan;

            degradedAccentColor =
                AILURONEHUDRuntimeStyle.Yellow;

            criticalFillColor =
                AILURONEHUDRuntimeStyle.Red;

            damageGhostColor =
                AILURONEHUDRuntimeStyle.Red;

            rebootColor =
                AILURONEHUDRuntimeStyle.Cyan;

            AILURONEHUDRuntimeStyle.ApplyIntegrity(
                visualRoot,
                identityText,
                valueText,
                labelText,
                statusText);

            CacheApprovedIntegrityVisuals();

            Transform path = visualRoot != null
                ? visualRoot.Find("Approved_IntegrityPath")
                : null;

            _integrityPath = path != null
                ? path.GetComponent<AILURONEIntegrityPathGraphic>()
                : null;

            CachePortraitVisuals();
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
                    "[PlayerIntegrityHUD] PlayerHealth was not found.",
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

            // Destroyed Unity objects compare equal to null. Their events no
            // longer need detaching, but a replacement source must be allowed
            // to establish a fresh subscription.
            _subscribed = false;
            ResolvePlayerHealth();
            Subscribe();
            InitialiseDisplay();
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

        private void HandleDamaged(
            float actualDamage,
            float remainingHealth
        )
        {
            if (playerHealth == null)
            {
                return;
            }

            float maximum =
                Mathf.Max(1f, playerHealth.maxHealth);

            float previousRatio =
                Mathf.Clamp01(
                    (remainingHealth + actualDamage)
                    / maximum
                );

            float remainingRatio =
                Mathf.Clamp01(
                    remainingHealth / maximum
                );

            _displayedRatio = remainingRatio;
            _ghostRatio =
                Mathf.Max(_ghostRatio, previousRatio);

            _ghostHoldTimer =
                damageGhostHoldDuration;

            _damageKickTimer =
                damageKickDuration;

            _impactTimer =
                impactStatusDuration;

            _valueFlashTimer =
                valueFlashDuration;

            TriggerPortraitDamageGlitch(
                actualDamage,
                maximum);
        }

        private void HandleRewindStarted()
        {
            _isRewinding = true;
            _rebuildStage =
                RebuildDisplayStage.SystemLost;

            _rebuildStageTimer =
                systemLostDuration;

            _ghostHoldTimer = 0f;
            _damageKickTimer = 0f;
            _impactTimer = 0f;
            _valueFlashTimer = 0f;

            ResetPortraitFeedback();

            // Death should never look like a remaining red health segment.
            _displayedRatio = 0f;
            _ghostRatio = 0f;

            ResetVisualRootPosition();

            if (rebootScan != null)
            {
                rebootScan.gameObject.SetActive(false);
            }

            _lastStatus = null;
        }

        private void HandleRewindCompleted()
        {
            _isRewinding = false;
            _rebuildStage =
                RebuildDisplayStage.Restored;

            _rebuildStageTimer =
                restoredStatusDuration;

            _impactTimer = 0f;
            _valueFlashTimer = 0f;

            ResetPortraitFeedback();

            if (playerHealth != null)
            {
                float maximum =
                    Mathf.Max(1f, playerHealth.maxHealth);

                float ratio =
                    Mathf.Clamp01(
                        playerHealth.currentHealth / maximum
                    );

                _displayedRatio = ratio;
                _ghostRatio = ratio;
            }

            if (rebootScan != null)
            {
                rebootScan.gameObject.SetActive(false);
            }

            _lastHealthValue = int.MinValue;
            _lastStatus = null;
        }

        private void UpdateRebuildDisplayStage()
        {
            if (_isRewinding
                && _rebuildStage
                    == RebuildDisplayStage.SystemLost)
            {
                _rebuildStageTimer -=
                    Time.unscaledDeltaTime;

                if (_rebuildStageTimer <= 0f)
                {
                    _rebuildStage =
                        RebuildDisplayStage.Reconstructing;

                    if (rebootScan != null)
                    {
                        rebootScan.gameObject.SetActive(true);
                    }

                    _lastStatus = null;
                }

                return;
            }

            if (!_isRewinding
                && _rebuildStage
                    == RebuildDisplayStage.Restored)
            {
                _rebuildStageTimer -=
                    Time.unscaledDeltaTime;

                if (_rebuildStageTimer <= 0f)
                {
                    _rebuildStage =
                        RebuildDisplayStage.None;

                    _lastStatus = null;
                }
            }
        }

        private void InitialiseDisplay()
        {
            if (moduleCanvasGroup != null)
            {
                moduleCanvasGroup.alpha = _tutorialVisibilityAlpha;
                moduleCanvasGroup.interactable = false;
                moduleCanvasGroup.blocksRaycasts = false;
            }

            if (identityText != null)
            {
                identityText.text = "FEI-A29";
            }

            if (labelText != null)
            {
                labelText.text = "FRAME INTEGRITY";
            }

            if (coreSystemsText != null)
            {
                coreSystemsText.text = string.Empty;
            }

            if (onlineText != null)
            {
                onlineText.text = string.Empty;
            }

            if (playerHealth != null)
            {
                float maximum =
                    Mathf.Max(1f, playerHealth.maxHealth);

                float ratio =
                    Mathf.Clamp01(
                        playerHealth.currentHealth / maximum
                    );

                _displayedRatio = ratio;
                _ghostRatio = ratio;
                _isRewinding = playerHealth.IsRewinding;

                _rebuildStage =
                    _isRewinding
                        ? RebuildDisplayStage.Reconstructing
                        : RebuildDisplayStage.None;
            }
            else
            {
                _displayedRatio = 1f;
                _ghostRatio = 1f;
            }

            if (rebootScan != null)
            {
                rebootScan.gameObject.SetActive(
                    _isRewinding
                );
            }

            _lastHealthValue = int.MinValue;
            _lastStatus = null;

            ResetPortraitFeedback();
            ResetVisualRootPosition();
            RenderBar(1f, 1f, normalFillColor);
        }

        private void ApplyTutorialVisibility()
        {
            if (moduleCanvasGroup != null)
            {
                moduleCanvasGroup.alpha = _tutorialVisibilityAlpha;
            }
        }

        private void Render(
            float actualRatio,
            int healthValue
        )
        {
            Color fillColor;
            Color accentColor;
            string stateText;

            if (_rebuildStage
                == RebuildDisplayStage.SystemLost)
            {
                fillColor = criticalFillColor;
                accentColor = criticalFillColor;
                stateText = "SYSTEM LOST";
            }
            else if (_isRewinding)
            {
                fillColor = rebootColor;
                accentColor = rebootColor;
                stateText = "RECONSTRUCTING";
            }
            else if (_rebuildStage
                == RebuildDisplayStage.Restored)
            {
                fillColor = rebootColor;
                accentColor = rebootColor;
                stateText = "RESTORED";
            }
            else if (actualRatio <= criticalThreshold)
            {
                fillColor = criticalFillColor;
                accentColor = criticalFillColor;
                stateText = "CRITICAL";
            }
            else if (_impactTimer > 0f)
            {
                fillColor =
                    ResolvePersistentHealthColor(
                        actualRatio
                    );

                accentColor = damageGhostColor;
                stateText = "IMPACT";
            }
            else if (actualRatio <= degradedThreshold)
            {
                fillColor = degradedAccentColor;
                accentColor = degradedAccentColor;
                stateText = "DEGRADED";
            }
            else if (actualRatio < 0.999f)
            {
                fillColor = normalAccentColor;
                accentColor = normalAccentColor;
                stateText = "NOMINAL";
            }
            else
            {
                fillColor = normalFillColor;
                accentColor = normalAccentColor;
                stateText = "NOMINAL";
            }

            RenderBar(
                _displayedRatio,
                _ghostRatio,
                fillColor
            );

            if (_approvedNodePlus != null)
            {
                _approvedNodePlus.color =
                    accentColor;
            }

            if (healthValue != _lastHealthValue)
            {
                if (valueText != null)
                {
                    // No leading zeroes: 65, 30, 7.
                    valueText.text =
                        Mathf.Max(0, healthValue)
                            .ToString();
                }

                _lastHealthValue = healthValue;
            }

            Color finalValueColor =
                ResolveFinalValueColor(actualRatio);

            if (valueText != null)
            {
                valueText.color = finalValueColor;
            }

            ApplyApprovedFramePlusColor(finalValueColor);

            if (_lastStatus != stateText
                || _lastStatusColor != accentColor)
            {
                if (statusText != null)
                {
                    statusText.text = stateText;
                    statusText.color = accentColor;
                }

                _lastStatus = stateText;
                _lastStatusColor = accentColor;
            }

            if (onlineText != null)
            {
                onlineText.color = accentColor;
            }

            for (int index = 0;
                index < accentImages.Length;
                index++)
            {
                Image accent = accentImages[index];

                if (accent != null)
                {
                    accent.color = accentColor;
                }
            }

            bool warning =
                !_isRewinding
                && actualRatio <= criticalThreshold;

            for (int index = 0;
                index < warningOnlyImages.Length;
                index++)
            {
                Image warningImage =
                    warningOnlyImages[index];

                if (warningImage == null)
                {
                    continue;
                }

                warningImage.enabled = warning;

                if (warning)
                {
                    warningImage.color =
                        criticalFillColor;
                }
            }
        }

        private Color ResolveFinalValueColor(
            float actualRatio)
        {
            Color persistentValueColor;

            if (_rebuildStage
                == RebuildDisplayStage.SystemLost)
            {
                persistentValueColor =
                    criticalFillColor;
            }
            else if (_isRewinding
                || _rebuildStage
                    == RebuildDisplayStage.Restored)
            {
                persistentValueColor =
                    rebootColor;
            }
            else
            {
                persistentValueColor =
                    ResolvePersistentHealthColor(
                        actualRatio
                    );
            }

            if (!_isRewinding
                && _rebuildStage
                    != RebuildDisplayStage.SystemLost
                && _valueFlashTimer > 0f
                && valueFlashDuration > 0.001f)
            {
                float flash =
                    Mathf.Clamp01(
                        _valueFlashTimer
                        / valueFlashDuration
                    );

                float easedFlash =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        flash
                    );

                return Color.Lerp(
                    persistentValueColor,
                    damageGhostColor,
                    easedFlash * 0.86f
                );
            }

            return persistentValueColor;
        }

        private void ApplyApprovedFramePlusColor(
            Color finalValueColor)
        {
            SetImageColor(
                _approvedFramePlusLeftHorizontal,
                finalValueColor);

            SetImageColor(
                _approvedFramePlusLeftVertical,
                finalValueColor);

            SetImageColor(
                _approvedFramePlusRightHorizontal,
                finalValueColor);

            SetImageColor(
                _approvedFramePlusRightVertical,
                finalValueColor);
        }

        private static void SetImageColor(
            Image image,
            Color color)
        {
            if (image != null)
            {
                image.color = color;
            }
        }

        private void CacheApprovedIntegrityVisuals()
        {
            _approvedCurrentClip = null;
            _approvedGhostClip = null;
            _approvedCurrentImages = Array.Empty<Image>();
            _approvedGhostImages = Array.Empty<Image>();
            _approvedNodePlus = null;
            _approvedPortrait = null;

            _approvedFramePlusLeftHorizontal = null;
            _approvedFramePlusLeftVertical = null;
            _approvedFramePlusRightHorizontal = null;
            _approvedFramePlusRightVertical = null;

            if (visualRoot == null)
            {
                return;
            }

            CacheApprovedFramePlusImages();

            Transform finalRoot =
                visualRoot.Find("AILURONE_IntegrityHUDVisual");

            if (finalRoot == null)
            {
                finalRoot =
                    visualRoot.Find("Approved_IntegrityFinal");
            }

            if (finalRoot == null)
            {
                return;
            }

            Transform portrait =
                finalRoot.Find("Approved_PlayerPortrait");

            if (portrait != null)
            {
                _approvedPortrait =
                    portrait.GetComponent<Image>();
            }

            Transform barGroup =
                finalRoot.Find("Approved_IntegrityBarGroup");

            if (barGroup == null)
            {
                return;
            }

            _approvedCurrentClip =
                barGroup.Find("Approved_IntegrityCurrentClip")
                    as RectTransform;

            _approvedGhostClip =
                barGroup.Find("Approved_IntegrityGhostClip")
                    as RectTransform;

            if (_approvedCurrentClip != null)
            {
                _approvedCurrentImages =
                    _approvedCurrentClip
                        .GetComponentsInChildren<Image>(true);
            }

            if (_approvedGhostClip != null)
            {
                _approvedGhostImages =
                    _approvedGhostClip
                        .GetComponentsInChildren<Image>(true);
            }

            Transform node =
                barGroup.Find("Approved_IntegrityBarNodePlus");

            if (node != null)
            {
                _approvedNodePlus =
                    node.GetComponent<Image>();
            }
        }

        private void CacheApprovedFramePlusImages()
        {
            if (visualRoot == null)
            {
                return;
            }

            Transform leftPlus =
                visualRoot.Find("Approved_FramePlusLeft");

            Transform rightPlus =
                visualRoot.Find("Approved_FramePlusRight");

            if (leftPlus != null)
            {
                Transform horizontal =
                    leftPlus.Find("Horizontal");

                Transform vertical =
                    leftPlus.Find("Vertical");

                _approvedFramePlusLeftHorizontal =
                    horizontal != null
                        ? horizontal.GetComponent<Image>()
                        : null;

                _approvedFramePlusLeftVertical =
                    vertical != null
                        ? vertical.GetComponent<Image>()
                        : null;
            }

            if (rightPlus != null)
            {
                Transform horizontal =
                    rightPlus.Find("Horizontal");

                Transform vertical =
                    rightPlus.Find("Vertical");

                _approvedFramePlusRightHorizontal =
                    horizontal != null
                        ? horizontal.GetComponent<Image>()
                        : null;

                _approvedFramePlusRightVertical =
                    vertical != null
                        ? vertical.GetComponent<Image>()
                        : null;
            }
        }

        private void CachePortraitVisuals()
        {
            // Reconfiguration can occur while feedback is active. Restore
            // the previous portrait before recaching so a temporary tint or
            // glitch material state is never captured as the new base state.
            ResetPortraitFeedback();

            _portraitMain = null;
            _portraitGlitchOverlay = null;

            if (visualRoot == null)
            {
                return;
            }

            // The approved portrait is the current production visual and
            // therefore takes priority over the legacy portrait hierarchy.
            if (_approvedPortrait != null)
            {
                _portraitMain = _approvedPortrait;
            }
            else
            {
                Transform portraitRoot =
                    visualRoot.Find("Approved_ReferencePortrait");

                if (portraitRoot != null)
                {
                    Transform main = portraitRoot.Find(
                        "Approved_PortraitViewport/Approved_PortraitMain");

                    _portraitMain = main != null
                        ? main.GetComponent<Image>()
                        : null;
                }
            }

            if (_portraitMain == null)
            {
                return;
            }

            _portraitMainBaseColor =
                _portraitMain.color;

            if (_portraitMain.GetComponent<
                    AILURONEPortraitBackdropClip>() == null)
            {
                _portraitMain.gameObject.AddComponent<
                    AILURONEPortraitBackdropClip>();
            }

            EnsurePortraitGlitchOverlay();
            ResetPortraitFeedback();
        }

        private void EnsurePortraitGlitchOverlay()
        {
            if (_portraitMain == null)
            {
                return;
            }

            RectTransform portraitRect =
                _portraitMain.rectTransform;

            RectTransform portraitParent =
                portraitRect.parent as RectTransform;

            if (portraitParent == null)
            {
                return;
            }

            Transform existingOverlay =
                portraitParent.Find(
                    RuntimePortraitGlitchOverlayName);

            if (existingOverlay != null)
            {
                _portraitGlitchOverlay =
                    existingOverlay.GetComponent<Image>();

                if (_portraitGlitchOverlay == null)
                {
                    _portraitGlitchOverlay =
                        existingOverlay.gameObject
                            .AddComponent<Image>();
                }
            }
            else
            {
                GameObject overlayObject =
                    new GameObject(
                        RuntimePortraitGlitchOverlayName,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));

                overlayObject.layer =
                    _portraitMain.gameObject.layer;

                RectTransform overlayRect =
                    overlayObject.GetComponent<RectTransform>();

                overlayRect.SetParent(
                    portraitParent,
                    false);

                _portraitGlitchOverlay =
                    overlayObject.GetComponent<Image>();
            }

            if (_portraitGlitchOverlay == null)
            {
                return;
            }

            _portraitGlitchOverlay.gameObject.layer =
                _portraitMain.gameObject.layer;

            CopyRectTransform(
                portraitRect,
                _portraitGlitchOverlay.rectTransform);

            CopyPortraitImage(
                _portraitMain,
                _portraitGlitchOverlay);

            // A full rectangular UI mesh is intentional. The portrait PNG
            // already contains transparent space around the character, so
            // shifted cyan/magenta samples can extend slightly beyond the
            // silhouette without leaking outside the HUD module.
            _portraitGlitchOverlay.useSpriteMesh = false;
            _portraitGlitchOverlay.color = Color.white;
            _portraitGlitchOverlay.raycastTarget = false;
            _portraitGlitchOverlay.maskable = true;

            int portraitSiblingIndex =
                portraitRect.GetSiblingIndex();

            _portraitGlitchOverlay.rectTransform.SetSiblingIndex(
                Mathf.Min(
                    portraitSiblingIndex + 1,
                    portraitParent.childCount - 1));

            DisableLegacyRuntimePortraitSlices(
                portraitParent);

            EnsurePortraitGlitchMaterial();

            if (_portraitGlitchMaterial == null)
            {
                _portraitGlitchOverlay.enabled = false;
                return;
            }

            _portraitGlitchOverlay.material =
                _portraitGlitchMaterial;

            ApplyPortraitGlitchMaterialState(
                0f,
                0f);

            _portraitGlitchOverlay.enabled = false;
        }

        private void EnsurePortraitGlitchMaterial()
        {
            if (_portraitGlitchMaterial != null)
            {
                return;
            }

            if (portraitGlitchMaterialTemplate != null)
            {
                _portraitGlitchMaterial =
                    new Material(
                        portraitGlitchMaterialTemplate);
            }
            else
            {
                Shader shader =
                    Shader.Find(
                        PortraitGlitchShaderName);

                if (shader == null)
                {
                    if (!_missingGlitchShaderLogged)
                    {
                        Debug.LogWarning(
                            "[PlayerIntegrityHUD] Portrait glitch shader "
                            + PortraitGlitchShaderName
                            + " was not found. Copy the shader into the "
                            + "project before testing the HUD.",
                            this);
                    }

                    _missingGlitchShaderLogged = true;
                    return;
                }

                _portraitGlitchMaterial =
                    new Material(shader);
            }

            _portraitGlitchMaterial.name =
                "AILURONE_PortraitGlitch_Runtime";

            _portraitGlitchMaterial.hideFlags =
                HideFlags.HideAndDontSave;

            Texture2D mask =
                ResolvePortraitGlitchHeadMask();

            if (mask != null)
            {
                _portraitGlitchMaterial.SetTexture(
                    HeadMaskProperty,
                    mask);
            }

            _portraitGlitchSeed =
                UnityEngine.Random.Range(
                    1f,
                    997f);

            _portraitGlitchMaterial.SetFloat(
                GlitchSeedProperty,
                _portraitGlitchSeed);

            _portraitGlitchMaterial.SetFloat(
                HeadStrengthProperty,
                portraitHeadGlitchMultiplier);

            _portraitGlitchMaterial.SetFloat(
                BodyStrengthProperty,
                portraitBodyGlitchMultiplier);

            _portraitGlitchMaterial.SetColor(
                CyanColorProperty,
                portraitGlitchCyan);

            _portraitGlitchMaterial.SetColor(
                MagentaColorProperty,
                portraitGlitchMagenta);
        }

        private Texture2D ResolvePortraitGlitchHeadMask()
        {
            if (portraitGlitchHeadMask != null)
            {
                _resolvedPortraitGlitchHeadMask =
                    portraitGlitchHeadMask;

                return _resolvedPortraitGlitchHeadMask;
            }

            if (_resolvedPortraitGlitchHeadMask == null)
            {
                _resolvedPortraitGlitchHeadMask =
                    Resources.Load<Texture2D>(
                        PortraitGlitchHeadMaskResourcePath);
            }

            return _resolvedPortraitGlitchHeadMask;
        }

        private void DestroyPortraitGlitchMaterial()
        {
            if (_portraitGlitchMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_portraitGlitchMaterial);
            }
            else
            {
                DestroyImmediate(_portraitGlitchMaterial);
            }

            _portraitGlitchMaterial = null;
        }

        private static void DisableLegacyRuntimePortraitSlices(
            RectTransform portraitParent)
        {
            if (portraitParent == null)
            {
                return;
            }

            DisableLegacyRuntimePortraitSlice(
                portraitParent,
                "Runtime_PortraitSliceUpperBand");

            DisableLegacyRuntimePortraitSlice(
                portraitParent,
                "Runtime_PortraitSliceLowerBand");
        }

        private static void DisableLegacyRuntimePortraitSlice(
            RectTransform portraitParent,
            string objectName)
        {
            Transform slice =
                portraitParent.Find(objectName);

            if (slice == null)
            {
                return;
            }

            RectTransform sliceRect =
                slice as RectTransform;

            if (sliceRect != null)
            {
                sliceRect.anchoredPosition =
                    Vector2.zero;
            }

            slice.gameObject.SetActive(false);
        }

        private static void CopyRectTransform(
            RectTransform source,
            RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition3D =
                source.anchoredPosition3D;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void CopyPortraitImage(
            Image source,
            Image target)
        {
            target.sprite = source.sprite;
            target.overrideSprite = source.overrideSprite;
            target.material = source.material;
            target.color = source.color;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
            target.fillMethod = source.fillMethod;
            target.fillAmount = source.fillAmount;
            target.fillClockwise = source.fillClockwise;
            target.fillOrigin = source.fillOrigin;
            target.useSpriteMesh = source.useSpriteMesh;
            target.pixelsPerUnitMultiplier =
                source.pixelsPerUnitMultiplier;
            target.maskable = true;
            target.raycastTarget = false;
        }

        private void TriggerPortraitDamageGlitch(
            float actualDamage,
            float maximumHealth)
        {
            float damageFraction =
                Mathf.Clamp01(
                    actualDamage
                    / Mathf.Max(
                        1f,
                        maximumHealth));

            // This remains valid after enemy damage is rebalanced:
            // small hits create a controlled burst, while the current
            // 35-45 damage contacts approach the high end.
            float scaledDamage =
                Mathf.InverseLerp(
                    0.05f,
                    0.45f,
                    damageFraction);

            float strength =
                Mathf.Lerp(
                    portraitDamageBurstMinimum,
                    1f,
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        scaledDamage));

            _damageGlitchBurstDuration =
                Mathf.Max(
                    0.05f,
                    portraitDamageBurstDuration);

            _damageGlitchBurstTimer =
                _damageGlitchBurstDuration;

            _damageGlitchBurstStrength =
                Mathf.Max(
                    _damageGlitchBurstStrength,
                    strength);

            // Changing the seed on impact prevents repeated hits from
            // producing the exact same tear pattern.
            _portraitGlitchSeed =
                Mathf.Repeat(
                    _portraitGlitchSeed
                    + UnityEngine.Random.Range(
                        17f,
                        113f),
                    997f);

            if (_portraitGlitchMaterial != null)
            {
                _portraitGlitchMaterial.SetFloat(
                    GlitchSeedProperty,
                    _portraitGlitchSeed);
            }
        }

        private void UpdatePortraitFeedback(float actualRatio)
        {
            if (_portraitMain == null)
            {
                CachePortraitVisuals();
            }

            if (_portraitMain == null)
            {
                return;
            }

            bool rewindGlitch =
                _isRewinding
                || _rebuildStage
                    == RebuildDisplayStage.SystemLost;

            float portraitTintAmount = 0f;

            if (_impactTimer > 0f
                && impactStatusDuration > 0.001f
                && !rewindGlitch)
            {
                float progress =
                    1f - Mathf.Clamp01(
                        _impactTimer
                        / impactStatusDuration);

                float envelope =
                    Mathf.Sin(
                        progress * Mathf.PI);

                // Ordinary damage remains a clean full-portrait red flash.
                // It does not create or move any slice objects.
                portraitTintAmount =
                    envelope
                    * portraitDamageRedFlashStrength;
            }

            Color targetTint =
                rewindGlitch
                    ? rebootColor
                    : criticalFillColor;

            float rewindTint =
                rewindGlitch
                    ? 0.08f
                    : 0f;

            Color mainColor =
                Color.Lerp(
                    _portraitMainBaseColor,
                    targetTint,
                    Mathf.Clamp01(
                        Mathf.Max(
                            portraitTintAmount,
                            rewindTint)));

            mainColor.a =
                _portraitMainBaseColor.a;

            _portraitMain.color = mainColor;

            UpdatePortraitGlitchState(
                actualRatio,
                rewindGlitch);
        }

        private void UpdatePortraitGlitchState(
            float actualRatio,
            bool rewindGlitch)
        {
            if (_portraitGlitchOverlay == null
                || _portraitGlitchMaterial == null)
            {
                EnsurePortraitGlitchOverlay();
            }

            float deltaTime =
                Time.unscaledDeltaTime;

            float exitThreshold =
                Mathf.Max(
                    criticalThreshold + 0.01f,
                    portraitGlitchExitThreshold);

            bool wasLatched =
                _portraitGlitchLatched;

            if (!rewindGlitch)
            {
                if (!_portraitGlitchLatched
                    && actualRatio <= criticalThreshold)
                {
                    _portraitGlitchLatched = true;
                }
                else if (_portraitGlitchLatched
                    && actualRatio >= exitThreshold)
                {
                    _portraitGlitchLatched = false;
                }
            }
            else
            {
                _portraitGlitchLatched = false;
            }

            if (!wasLatched
                && _portraitGlitchLatched)
            {
                _nextRandomGlitchTime =
                    Time.unscaledTime
                    + UnityEngine.Random.Range(
                        0.55f,
                        1.15f);
            }

            float targetGlitchAmount = 0f;

            if (_portraitGlitchLatched)
            {
                float normalizedDanger =
                    Mathf.InverseLerp(
                        exitThreshold,
                        0f,
                        actualRatio);

                float curvedDanger =
                    Mathf.Pow(
                        Mathf.Clamp01(
                            normalizedDanger),
                        1.28f);

                // A small baseline makes 30% visibly unstable. The
                // non-linear curve then accelerates strongly below 10%.
                targetGlitchAmount =
                    Mathf.Lerp(
                        0.09f,
                        1f,
                        curvedDanger);
            }

            if (rewindGlitch)
            {
                float rewindWave =
                    0.5f + 0.5f
                    * Mathf.Sin(
                        Time.unscaledTime * 17f);

                targetGlitchAmount =
                    Mathf.Lerp(
                        0.78f,
                        0.96f,
                        rewindWave);
            }

            if (targetGlitchAmount
                > _portraitGlitchAmount)
            {
                _portraitGlitchAmount =
                    Mathf.MoveTowards(
                        _portraitGlitchAmount,
                        targetGlitchAmount,
                        7.5f * deltaTime);
            }
            else
            {
                float releaseSpeed =
                    1f
                    / Mathf.Max(
                        0.10f,
                        portraitGlitchFadeOutDuration);

                _portraitGlitchAmount =
                    Mathf.MoveTowards(
                        _portraitGlitchAmount,
                        targetGlitchAmount,
                        releaseSpeed * deltaTime);
            }

            float damageBurst = 0f;

            if (_damageGlitchBurstTimer > 0f)
            {
                _damageGlitchBurstTimer =
                    Mathf.Max(
                        0f,
                        _damageGlitchBurstTimer
                        - deltaTime);

                damageBurst =
                    EvaluateGlitchBurst(
                        _damageGlitchBurstTimer,
                        _damageGlitchBurstDuration,
                        _damageGlitchBurstStrength);

                if (_damageGlitchBurstTimer <= 0f)
                {
                    _damageGlitchBurstStrength = 0f;
                }
            }

            float randomBurst = 0f;

            if (_portraitGlitchLatched
                && !rewindGlitch)
            {
                if (_randomGlitchBurstTimer > 0f)
                {
                    _randomGlitchBurstTimer =
                        Mathf.Max(
                            0f,
                            _randomGlitchBurstTimer
                            - deltaTime);

                    randomBurst =
                        EvaluateGlitchBurst(
                            _randomGlitchBurstTimer,
                            _randomGlitchBurstDuration,
                            _randomGlitchBurstStrength);
                }
                else if (Time.unscaledTime
                    >= _nextRandomGlitchTime)
                {
                    float danger =
                        Mathf.Clamp01(
                            _portraitGlitchAmount);

                    _randomGlitchBurstDuration =
                        Mathf.Lerp(
                            0.105f,
                            0.22f,
                            danger);

                    _randomGlitchBurstTimer =
                        _randomGlitchBurstDuration;

                    _randomGlitchBurstStrength =
                        Mathf.Lerp(
                            0.36f,
                            0.96f,
                            danger);

                    float interval =
                        Mathf.Lerp(
                            3.80f,
                            0.85f,
                            Mathf.Pow(
                                danger,
                                0.82f));

                    _nextRandomGlitchTime =
                        Time.unscaledTime
                        + interval
                        * UnityEngine.Random.Range(
                            0.86f,
                            1.14f);

                    _portraitGlitchSeed =
                        Mathf.Repeat(
                            _portraitGlitchSeed
                            + UnityEngine.Random.Range(
                                7f,
                                89f),
                            997f);

                    randomBurst =
                        0.02f;
                }
            }
            else
            {
                _randomGlitchBurstTimer = 0f;
                _randomGlitchBurstDuration = 0f;
                _randomGlitchBurstStrength = 0f;
                _nextRandomGlitchTime = 0f;
            }

            float rewindBurst =
                rewindGlitch
                    ? 0.92f
                    : 0f;

            float finalBurst =
                Mathf.Max(
                    rewindBurst,
                    Mathf.Max(
                        damageBurst,
                        randomBurst));

            ApplyPortraitGlitchMaterialState(
                _portraitGlitchAmount,
                finalBurst);
        }

        private static float EvaluateGlitchBurst(
            float remainingTime,
            float duration,
            float strength)
        {
            if (remainingTime <= 0f
                || duration <= 0.001f)
            {
                return 0f;
            }

            float progress =
                1f - Mathf.Clamp01(
                    remainingTime
                    / duration);

            float attack =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(
                        progress / 0.16f));

            float release =
                1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(
                        (progress - 0.18f)
                        / 0.82f));

            return Mathf.Clamp01(
                attack
                * release
                * strength);
        }

        private void ApplyPortraitGlitchMaterialState(
            float glitchAmount,
            float burstAmount)
        {
            if (_portraitGlitchOverlay == null
                || _portraitGlitchMaterial == null)
            {
                return;
            }

            float clampedGlitch =
                Mathf.Clamp01(
                    glitchAmount);

            float clampedBurst =
                Mathf.Clamp01(
                    burstAmount);

            _portraitGlitchMaterial.SetFloat(
                GlitchAmountProperty,
                clampedGlitch);

            _portraitGlitchMaterial.SetFloat(
                BurstAmountProperty,
                clampedBurst);

            _portraitGlitchMaterial.SetFloat(
                GlitchSeedProperty,
                _portraitGlitchSeed);

            _portraitGlitchMaterial.SetFloat(
                HeadStrengthProperty,
                portraitHeadGlitchMultiplier);

            _portraitGlitchMaterial.SetFloat(
                BodyStrengthProperty,
                portraitBodyGlitchMultiplier);

            _portraitGlitchMaterial.SetColor(
                CyanColorProperty,
                portraitGlitchCyan);

            _portraitGlitchMaterial.SetColor(
                MagentaColorProperty,
                portraitGlitchMagenta);

            Texture2D mask =
                ResolvePortraitGlitchHeadMask();

            if (mask != null)
            {
                _portraitGlitchMaterial.SetTexture(
                    HeadMaskProperty,
                    mask);
            }

            _portraitGlitchOverlay.enabled =
                clampedGlitch > 0.003f
                || clampedBurst > 0.003f;
        }

        private void ResetPortraitFeedback()
        {
            if (_portraitMain != null)
            {
                _portraitMain.color =
                    _portraitMainBaseColor;
            }

            _portraitGlitchLatched = false;
            _portraitGlitchAmount = 0f;

            _damageGlitchBurstTimer = 0f;
            _damageGlitchBurstDuration = 0f;
            _damageGlitchBurstStrength = 0f;

            _randomGlitchBurstTimer = 0f;
            _randomGlitchBurstDuration = 0f;
            _randomGlitchBurstStrength = 0f;
            _nextRandomGlitchTime = 0f;

            if (_portraitGlitchMaterial != null)
            {
                _portraitGlitchMaterial.SetFloat(
                    GlitchAmountProperty,
                    0f);

                _portraitGlitchMaterial.SetFloat(
                    BurstAmountProperty,
                    0f);
            }

            if (_portraitGlitchOverlay != null)
            {
                _portraitGlitchOverlay.enabled = false;
            }

            if (_portraitMain != null)
            {
                RectTransform portraitParent =
                    _portraitMain.rectTransform.parent
                        as RectTransform;

                DisableLegacyRuntimePortraitSlices(
                    portraitParent);
            }
        }

        private Color ResolvePersistentHealthColor(
            float actualRatio
        )
        {
            if (actualRatio <= criticalThreshold)
            {
                return criticalFillColor;
            }

            if (actualRatio <= degradedThreshold)
            {
                return degradedAccentColor;
            }

            return normalFillColor;
        }

        private void RenderBar(
            float currentRatio,
            float delayedGhostRatio,
            Color currentColor
        )
        {
            RenderApprovedIntegrityBar(
                currentRatio,
                delayedGhostRatio,
                currentColor);

            if (_integrityPath != null)
            {
                _integrityPath.SetVisualState(
                    currentRatio,
                    delayedGhostRatio,
                    currentColor,
                    damageGhostColor);
            }

            for (int index = 0;
                index < barSegments.Length;
                index++)
            {
                BarSegment segment =
                    barSegments[index];

                float currentCoverage =
                    EvaluateSegmentCoverage(
                        currentRatio,
                        segment.startRatio,
                        segment.endRatio
                    );

                float ghostCoverage =
                    EvaluateSegmentCoverage(
                        delayedGhostRatio,
                        segment.startRatio,
                        segment.endRatio
                    );

                float ghostOnlyCoverage =
                    Mathf.Max(
                        0f,
                        ghostCoverage - currentCoverage
                    );

                ApplySegmentWidth(
                    segment.currentFill,
                    segment.maximumWidth,
                    currentCoverage
                );

                ApplySegmentInterval(
                    segment.damageGhost,
                    segment.maximumWidth,
                    currentCoverage,
                    ghostCoverage
                );

                if (segment.currentImage != null)
                {
                    segment.currentImage.color =
                        currentColor;
                }

                if (segment.ghostImage != null)
                {
                    segment.ghostImage.color =
                        damageGhostColor;
                }

                if (segment.currentFill != null)
                {
                    segment.currentFill.gameObject.SetActive(
                        currentCoverage > 0.001f
                    );
                }

                if (segment.damageGhost != null)
                {
                    segment.damageGhost.gameObject.SetActive(
                        ghostOnlyCoverage > 0.001f
                    );
                }
            }
        }

        private void RenderApprovedIntegrityBar(
            float currentRatio,
            float delayedGhostRatio,
            Color currentColor)
        {
            SetApprovedClipWidth(
                _approvedCurrentClip,
                currentRatio);

            SetApprovedClipWidth(
                _approvedGhostClip,
                delayedGhostRatio);

            for (int index = 0;
                index < _approvedCurrentImages.Length;
                index++)
            {
                if (_approvedCurrentImages[index] != null)
                {
                    _approvedCurrentImages[index].color =
                        currentColor;
                }
            }

            for (int index = 0;
                index < _approvedGhostImages.Length;
                index++)
            {
                if (_approvedGhostImages[index] != null)
                {
                    _approvedGhostImages[index].color =
                        damageGhostColor;
                }
            }
        }

        private static void SetApprovedClipWidth(
            RectTransform clip,
            float ratio)
        {
            if (clip == null)
            {
                return;
            }

            ratio = Mathf.Clamp01(ratio);

            Vector2 size = clip.sizeDelta;
            size.x = ApprovedIntegrityFullWidth * ratio;
            size.y = 621f;
            clip.sizeDelta = size;

            clip.gameObject.SetActive(
                ratio > 0.001f);
        }

        private static float EvaluateSegmentCoverage(
            float globalRatio,
            float startRatio,
            float endRatio
        )
        {
            float range =
                Mathf.Max(0.0001f, endRatio - startRatio);

            return Mathf.Clamp01(
                (globalRatio - startRatio) / range
            );
        }

        private static void ApplySegmentWidth(
            RectTransform rect,
            float maximumWidth,
            float coverage
        )
        {
            if (rect == null)
            {
                return;
            }

            Vector2 size = rect.sizeDelta;
            size.x =
                Mathf.Max(0f, maximumWidth * coverage);

            rect.sizeDelta = size;

            Vector2 position =
                rect.anchoredPosition;

            position.x = 0f;
            rect.anchoredPosition = position;
        }

        private static void ApplySegmentInterval(
            RectTransform rect,
            float maximumWidth,
            float intervalStart,
            float intervalEnd
        )
        {
            if (rect == null)
            {
                return;
            }

            float start =
                Mathf.Clamp01(
                    Mathf.Min(intervalStart, intervalEnd)
                );

            float end =
                Mathf.Clamp01(
                    Mathf.Max(intervalStart, intervalEnd)
                );

            Vector2 size = rect.sizeDelta;
            size.x =
                Mathf.Max(
                    0f,
                    maximumWidth * (end - start)
                );

            rect.sizeDelta = size;

            Vector2 position =
                rect.anchoredPosition;

            position.x =
                maximumWidth * start;

            rect.anchoredPosition = position;
        }

        private void CaptureRuntimeGeometry()
        {
            CaptureBasePosition();
            CaptureBarWidths();
            CaptureScanBounds();
        }

        private void CaptureBarWidths()
        {
            for (int index = 0;
                index < barSegments.Length;
                index++)
            {
                BarSegment segment =
                    barSegments[index];

                if (segment.maximumWidth <= 0.001f)
                {
                    RectTransform source =
                        segment.currentFill != null
                            ? segment.currentFill
                            : segment.damageGhost;

                    if (source != null)
                    {
                        segment.maximumWidth =
                            source.sizeDelta.x;
                    }
                }

                barSegments[index] = segment;
            }
        }

        private void CaptureScanBounds()
        {
            if (rebootScan == null
                || _scanBoundsCaptured)
            {
                return;
            }

            RectTransform parent =
                rebootScan.parent as RectTransform;

            if (parent == null)
            {
                return;
            }

            float halfWidth =
                parent.rect.width * 0.5f;

            _scanMinimumX =
                -halfWidth + 4f;

            _scanMaximumX =
                halfWidth - 4f;

            _scanBoundsCaptured = true;
        }

        private void UpdateRebootScan()
        {
            if (rebootScan == null)
            {
                return;
            }

            if (!_isRewinding
                || _rebuildStage
                    == RebuildDisplayStage.SystemLost)
            {
                if (rebootScan.gameObject.activeSelf)
                {
                    rebootScan.gameObject.SetActive(false);
                }

                return;
            }

            if (!rebootScan.gameObject.activeSelf)
            {
                rebootScan.gameObject.SetActive(true);
            }

            CaptureScanBounds();

            float t =
                Mathf.Repeat(
                    Time.unscaledTime * rebootScanSpeed,
                    1f
                );

            Vector2 position =
                rebootScan.anchoredPosition;

            position.x =
                Mathf.Lerp(
                    _scanMinimumX,
                    _scanMaximumX,
                    t
                );

            rebootScan.anchoredPosition = position;
        }

        private void CaptureBasePosition()
        {
            if (visualRoot == null
                || _basePositionCaptured)
            {
                return;
            }

            _baseAnchoredPosition =
                visualRoot.anchoredPosition;

            _basePositionCaptured = true;
        }

        private void UpdateDamageKick()
        {
            if (visualRoot == null)
            {
                return;
            }

            CaptureBasePosition();

            if (_damageKickTimer <= 0f
                || damageKickDuration <= 0.001f)
            {
                ResetVisualRootPosition();
                return;
            }

            _damageKickTimer -=
                Time.unscaledDeltaTime;

            float progress =
                1f - Mathf.Clamp01(
                    _damageKickTimer
                    / damageKickDuration
                );

            float envelope =
                Mathf.Pow(1f - progress, 2f);

            float offset =
                Mathf.Sin(
                    progress * Mathf.PI * 6f
                )
                * damageKickPixels
                * envelope;

            visualRoot.anchoredPosition =
                _baseAnchoredPosition
                + new Vector2(offset, 0f)
                + AILURONEHUDMotionSignal.GetOffset();
        }

        private void ResetVisualRootPosition()
        {
            if (visualRoot == null)
            {
                return;
            }

            CaptureBasePosition();

            visualRoot.anchoredPosition =
                _baseAnchoredPosition
                + (Application.isPlaying
                    ? AILURONEHUDMotionSignal.GetOffset()
                    : Vector2.zero);
        }
    }
}
