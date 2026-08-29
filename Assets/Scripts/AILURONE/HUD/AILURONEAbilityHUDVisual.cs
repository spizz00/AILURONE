#pragma warning disable 0618
#pragma warning disable 0414
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Compact text-led ability presentation. Gameplay systems remain the
    /// authoritative source for jump charges, dash cooldown and overclock energy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AILURONEAbilityHUDVisual : MonoBehaviour
    {
        private const string CanonicalHudCanvasName = "HUD_Canvas_AILURONE";

        private sealed class AbilityModule
        {
            public RectTransform root;
            public CanvasGroup tutorialVisibilityGroup;
            public TMP_Text labelText;
            public TMP_Text valueText;
            public RectTransform progressFill;
            public Image progressImage;
            public float progressWidth;
            public Image[] chargePips = System.Array.Empty<Image>();
            public float displayedProgress;
            public float pulse;
        }

        private TimeManager _timeManager;
        private DashController _dashController;
        private Image _legacyJumpForeground;
        private Image _legacyOverclockForeground;
        private GameObject _legacyJumpRoot;
        private GameObject _legacyOverclockRoot;
        private RectTransform _hudRoot;

        private AbilityModule _jumpModule;
        private AbilityModule _dashModule;
        private AbilityModule _overclockModule;

        private readonly Vector2 _basePosition =
            new Vector2(-48f, 34f);

        private const float BaseGeometryLean = -0.35f;
        private const float TextCounterLean = 0.35f;

        private int _lastJumps = -1;
        private float _lastDashRemaining;
        private bool _lastDashReady;
        private bool _lastOverclockReady;
        private bool _lastOverclockActive;
        private bool _initialized;
        private bool _legacyVisibilityCaptured;
        private bool _legacyJumpWasActive;
        private bool _legacyOverclockWasActive;
        private float _nextInitializationAttemptAt;
        private float _tutorialJumpAlpha = 1f;
        private float _tutorialDashAlpha = 1f;
        private float _tutorialOverclockAlpha = 1f;

        public bool IsRuntimeHudReady => HasValidHUD();
        public RectTransform JumpModuleRect => _jumpModule != null ? _jumpModule.root : null;
        public RectTransform DashModuleRect => _dashModule != null ? _dashModule.root : null;
        public RectTransform OverclockModuleRect => _overclockModule != null ? _overclockModule.root : null;

        public void SetTutorialModuleVisibility(
            float jumpAlpha,
            float dashAlpha,
            float overclockAlpha)
        {
            _tutorialJumpAlpha = Mathf.Clamp01(jumpAlpha);
            _tutorialDashAlpha = Mathf.Clamp01(dashAlpha);
            _tutorialOverclockAlpha = Mathf.Clamp01(overclockAlpha);
            ApplyTutorialModuleVisibility();
        }

        public void Initialize(
            TimeManager timeManager,
            Image legacyJumpForeground,
            Image legacyOverclockForeground)
        {
            if (_initialized && HasValidHUD())
            {
                return;
            }

            _timeManager = timeManager;
            _dashController = DashController.Instance;

            _legacyJumpForeground = legacyJumpForeground;
            _legacyOverclockForeground = legacyOverclockForeground;

            _legacyJumpRoot =
                ResolveLegacyRoot(legacyJumpForeground);

            _legacyOverclockRoot =
                ResolveLegacyRoot(legacyOverclockForeground);

            CaptureLegacyVisibility();

            if (!TryBuildHUD())
            {
                Debug.LogWarning(
                    "[Ability HUD] No Canvas was found; the legacy skill HUD remains active.",
                    this);
            }
        }

        private void OnEnable()
        {
            if (HasValidHUD())
            {
                SetReplacementVisible(true);
            }
        }

        private void OnDisable()
        {
            SetReplacementVisible(false);
        }

        private void LateUpdate()
        {
            if (!HasValidHUD())
            {
                _initialized = false;
                SetReplacementVisible(false);

                if (Time.unscaledTime <
                    _nextInitializationAttemptAt)
                {
                    return;
                }

                _nextInitializationAttemptAt =
                    Time.unscaledTime + 0.5f;

                if (!TryBuildHUD())
                {
                    return;
                }
            }

            if (_dashController == null)
            {
                _dashController = DashController.Instance;
            }

            Refresh(false);
            ApplyTutorialModuleVisibility();

            if (_hudRoot != null)
            {
                _hudRoot.anchoredPosition =
                    _basePosition
                    + AILURONEHUDMotionSignal.GetOffset();

                float dashLean =
                    _dashController != null &&
                    _dashController.isDashing
                        ? -0.40f
                        : 0f;

                _hudRoot.localEulerAngles =
                    new Vector3(
                        0f,
                        0f,
                        BaseGeometryLean + dashLean);
            }
        }

        private void OnDestroy()
        {
            SetReplacementVisible(false);

            if (_hudRoot != null)
            {
                Destroy(_hudRoot.gameObject);
            }
        }

        private bool TryBuildHUD()
        {
            if (HasValidHUD())
            {
                _initialized = true;
                SetReplacementVisible(isActiveAndEnabled);
                return true;
            }

            Canvas canvas = ResolveCanvas(
                _legacyJumpForeground,
                _legacyOverclockForeground);

            RectTransform parent = ResolveHUDParent(canvas);

            if (parent == null)
            {
                return false;
            }

            if (_hudRoot != null)
            {
                Destroy(_hudRoot.gameObject);
            }

            BuildHUD(parent);

            _lastDashReady =
                _dashController != null &&
                _dashController.IsReady;

            _lastDashRemaining =
                _dashController != null
                    ? _dashController.CooldownRemaining
                    : 0f;

            _lastOverclockReady =
                _timeManager != null &&
                _timeManager.CanActivateAbility;

            _lastOverclockActive =
                _timeManager != null &&
                _timeManager.IsAbilityActive;

            _initialized = true;
            SetReplacementVisible(isActiveAndEnabled);
            Refresh(true);
            return true;
        }

        private bool HasValidHUD()
        {
            return
                _hudRoot != null &&
                _jumpModule != null &&
                _jumpModule.root != null &&
                _jumpModule.labelText != null &&
                _jumpModule.chargePips != null &&
                _jumpModule.chargePips.Length == 2 &&
                _jumpModule.chargePips[0] != null &&
                _jumpModule.chargePips[1] != null &&
                HasValidStatusModule(_dashModule) &&
                HasValidStatusModule(_overclockModule);
        }

        private static bool HasValidStatusModule(
            AbilityModule module)
        {
            return
                module != null &&
                module.root != null &&
                module.labelText != null &&
                module.valueText != null &&
                module.progressFill != null &&
                module.progressImage != null;
        }

        private void ApplyTutorialModuleVisibility()
        {
            ApplyTutorialModuleAlpha(_jumpModule, _tutorialJumpAlpha);
            ApplyTutorialModuleAlpha(_dashModule, _tutorialDashAlpha);
            ApplyTutorialModuleAlpha(_overclockModule, _tutorialOverclockAlpha);
        }

        private static void ApplyTutorialModuleAlpha(
            AbilityModule module,
            float alpha)
        {
            if (module == null || module.tutorialVisibilityGroup == null)
            {
                return;
            }

            module.tutorialVisibilityGroup.alpha = Mathf.Clamp01(alpha);
            module.tutorialVisibilityGroup.interactable = false;
            module.tutorialVisibilityGroup.blocksRaycasts = false;
        }

        private void CaptureLegacyVisibility()
        {
            if (_legacyVisibilityCaptured ||
                (_legacyJumpRoot == null &&
                 _legacyOverclockRoot == null))
            {
                return;
            }

            _legacyJumpWasActive =
                _legacyJumpRoot != null &&
                _legacyJumpRoot.activeSelf;

            _legacyOverclockWasActive =
                _legacyOverclockRoot != null &&
                _legacyOverclockRoot.activeSelf;

            _legacyVisibilityCaptured = true;
        }

        private void SetReplacementVisible(bool visible)
        {
            bool showReplacement =
                visible && HasValidHUD();

            if (_hudRoot != null)
            {
                _hudRoot.gameObject.SetActive(showReplacement);
            }

            if (_legacyJumpRoot != null)
            {
                _legacyJumpRoot.SetActive(
                    showReplacement
                        ? false
                        : _legacyJumpWasActive);
            }

            if (_legacyOverclockRoot != null)
            {
                _legacyOverclockRoot.SetActive(
                    showReplacement
                        ? false
                        : _legacyOverclockWasActive);
            }
        }

        private void Refresh(bool immediate)
        {
            float deltaTime = Time.unscaledDeltaTime;

            UpdateJump(deltaTime, immediate);
            UpdateDash(deltaTime, immediate);
            UpdateOverclock(deltaTime, immediate);
            UpdatePulses(deltaTime);
        }

        private void UpdateJump(float deltaTime, bool immediate)
        {
            int jumpsLeft = 0;
            int maxJumps = 0;

            if (StarterAssets.FirstPersonController.Instance != null)
            {
                jumpsLeft = Mathf.Max(
                    0,
                    StarterAssets.FirstPersonController.Instance
                        .GetCurrentJumps());

                maxJumps = Mathf.Max(
                    0,
                    StarterAssets.FirstPersonController.Instance
                        .GetMaxJumps());
            }

            jumpsLeft = Mathf.Min(jumpsLeft, maxJumps);

            if (!immediate &&
                _lastJumps >= 0 &&
                jumpsLeft > _lastJumps)
            {
                _jumpModule.pulse = 1f;
            }

            _lastJumps = jumpsLeft;

            for (int index = 0;
                index < _jumpModule.chargePips.Length;
                index++)
            {
                Image pip = _jumpModule.chargePips[index];

                if (pip == null)
                {
                    continue;
                }

                // With one charge remaining, only the lower pip stays lit.
                int requiredCharges =
                    _jumpModule.chargePips.Length - index;

                bool filled =
                    maxJumps > 0 &&
                    jumpsLeft >= requiredCharges;

                Color color = filled
                    ? AILURONEHUDRuntimeStyle.Cyan
                    : AILURONEHUDRuntimeStyle.MutedWhite;

                color.a = filled ? 1f : 0.24f;
                pip.color = color;
            }

            _jumpModule.labelText.color =
                jumpsLeft > 0
                    ? AILURONEHUDRuntimeStyle.White
                    : AILURONEHUDRuntimeStyle.MutedWhite;
        }

        private void UpdateDash(float deltaTime, bool immediate)
        {
            float progress = 0f;
            Color stateColor =
                AILURONEHUDRuntimeStyle.MutedWhite;

            bool dashReady = false;

            if (_dashController == null)
            {
                _dashModule.valueText.text = "--";
            }
            else if (_dashController.isDashing)
            {
                progress = 1f;
                stateColor = AILURONEHUDRuntimeStyle.Cyan;
                _dashModule.valueText.text = "RUN";
            }
            else if (!_dashController.enabled)
            {
                progress =
                    1f - _dashController.CooldownNormalized;

                _dashModule.valueText.text = "LCK";
            }
            else if (_dashController.IsReady)
            {
                progress = 1f;
                stateColor = AILURONEHUDRuntimeStyle.Cyan;
                dashReady = true;
                _dashModule.valueText.text = "RDY";
            }
            else
            {
                progress =
                    1f - _dashController.CooldownNormalized;

                stateColor = AILURONEHUDRuntimeStyle.White;

                _dashModule.valueText.text =
                    _dashController.CooldownRemaining
                        .ToString("0.0");
            }

            if (!immediate && _dashController != null)
            {
                float expectedRemaining = Mathf.Max(
                    0f,
                    _lastDashRemaining - Time.deltaTime);

                bool cooldownWasReduced =
                    _dashController.CooldownRemaining <
                    expectedRemaining - 0.08f;

                if (cooldownWasReduced ||
                    (dashReady && !_lastDashReady))
                {
                    _dashModule.pulse = 1f;
                }
            }

            _lastDashReady = dashReady;
            _lastDashRemaining =
                _dashController != null
                    ? _dashController.CooldownRemaining
                    : 0f;

            SetModuleState(
                _dashModule,
                progress,
                stateColor,
                deltaTime,
                immediate);
        }

        private void UpdateOverclock(float deltaTime, bool immediate)
        {
            if (_timeManager == null)
            {
                _overclockModule.valueText.text = "--";

                SetModuleState(
                    _overclockModule,
                    0f,
                    AILURONEHUDRuntimeStyle.MutedWhite,
                    deltaTime,
                    immediate);

                return;
            }

            float energy =
                Mathf.Clamp01(_timeManager.CurrentEnergy);

            bool isReady =
                _timeManager.CanActivateAbility;

            bool isActive =
                _timeManager.IsAbilityActive;

            Color stateColor;

            if (_timeManager.IsRewinding)
            {
                stateColor = AILURONEHUDRuntimeStyle.Red;
                _overclockModule.valueText.text = "ERR";
            }
            else if (isActive)
            {
                stateColor = AILURONEHUDRuntimeStyle.Red;
                _overclockModule.valueText.text = "ACT";
            }
            else if (_timeManager.RechargeDelayRemaining > 0f)
            {
                stateColor =
                    AILURONEHUDRuntimeStyle.MutedWhite;

                _overclockModule.valueText.text =
                    _timeManager.RechargeDelayRemaining
                        .ToString("0.0");
            }
            else
            {
                stateColor = isReady
                    ? AILURONEHUDRuntimeStyle.Cyan
                    : AILURONEHUDRuntimeStyle.White;

                _overclockModule.valueText.text =
                    Mathf.RoundToInt(energy * 100f)
                        .ToString("00");
            }

            if (!immediate &&
                ((isReady && !_lastOverclockReady) ||
                 (isActive && !_lastOverclockActive)))
            {
                _overclockModule.pulse = 1f;
            }

            _lastOverclockReady = isReady;
            _lastOverclockActive = isActive;

            SetModuleState(
                _overclockModule,
                energy,
                stateColor,
                deltaTime,
                immediate);
        }

        private static void SetModuleState(
            AbilityModule module,
            float targetProgress,
            Color stateColor,
            float deltaTime,
            bool immediate)
        {
            module.displayedProgress = immediate
                ? Mathf.Clamp01(targetProgress)
                : SmoothTowards(
                    module.displayedProgress,
                    targetProgress,
                    14f,
                    deltaTime);

            if (module.progressFill != null)
            {
                Vector2 size = module.progressFill.sizeDelta;
                size.x =
                    module.progressWidth *
                    module.displayedProgress;

                module.progressFill.sizeDelta = size;
            }

            if (module.progressImage != null)
            {
                module.progressImage.color = stateColor;
            }

            if (module.labelText != null)
            {
                module.labelText.color =
                    AILURONEHUDRuntimeStyle.White;
            }

            if (module.valueText != null)
            {
                module.valueText.color = stateColor;
            }
        }

        private void UpdatePulses(float deltaTime)
        {
            UpdateModulePulse(_jumpModule, deltaTime);
            UpdateModulePulse(_dashModule, deltaTime);
            UpdateModulePulse(_overclockModule, deltaTime);
        }

        private static void UpdateModulePulse(
            AbilityModule module,
            float deltaTime)
        {
            if (module == null || module.root == null)
            {
                return;
            }

            module.pulse = Mathf.Max(
                0f,
                module.pulse - deltaTime * 5.2f);

            float scale =
                1f + EaseOutQuadratic(module.pulse) * 0.045f;

            module.root.localScale = Vector3.one * scale;
        }

        private void BuildHUD(RectTransform parent)
        {
            TMP_FontAsset numericFont =
                AILURONEHUDRuntimeStyle.ResolveVT323(
                    TMP_Settings.defaultFontAsset);

            TMP_FontAsset headingFont =
                AILURONEHUDRuntimeStyle.ResolveHeadingFont(
                    TMP_Settings.defaultFontAsset != null
                        ? TMP_Settings.defaultFontAsset
                        : numericFont);

            _hudRoot = CreateRect(
                "AbilityHUD_AuxiliaryRail",
                parent,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                _basePosition,
                new Vector2(306f, 132f));

            _hudRoot.localEulerAngles =
                new Vector3(0f, 0f, BaseGeometryLean);

            _hudRoot.SetAsLastSibling();

            _jumpModule = BuildJumpModule(
                _hudRoot,
                88f,
                218f,
                headingFont,
                numericFont);

            _dashModule = BuildStatusModule(
                _hudRoot,
                "Dash",
                "DASH",
                "SHIFT",
                48f,
                258f,
                headingFont,
                numericFont,
                false);

            _overclockModule = BuildStatusModule(
                _hudRoot,
                "Overclock",
                "OVERCLOCK",
                "F",
                8f,
                298f,
                headingFont,
                numericFont,
                true);

            ApplyTutorialModuleVisibility();
        }

        private static AbilityModule BuildJumpModule(
            RectTransform parent,
            float x,
            float width,
            TMP_FontAsset headingFont,
            TMP_FontAsset numericFont)
        {
            AbilityModule module = new AbilityModule();

            module.root = CreateRect(
                "Jump_Module",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, 0f),
                new Vector2(width, 38f));

            module.tutorialVisibilityGroup =
                module.root.gameObject.AddComponent<CanvasGroup>();

            AddImage(
                module.root,
                "Panel",
                new Color(0.018f, 0.022f, 0.024f, 0.78f),
                Vector2.zero,
                Vector2.one,
                Vector2.one * 0.5f,
                Vector2.zero,
                Vector2.zero);

            AddImage(
                module.root,
                "LeftAccent",
                AILURONEHUDRuntimeStyle.Yellow,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(2f, -8f));

            module.labelText = AddText(
                module.root,
                "Label",
                "JUMP",
                headingFont,
                16f,
                AILURONEHUDRuntimeStyle.White,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -7f),
                new Vector2(82f, 22f),
                1.1f);

            module.chargePips = new Image[2];

            module.chargePips[0] = AddImage(
                module.root,
                "Charge_Upper",
                AILURONEHUDRuntimeStyle.Cyan,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 10f, -10f),
                new Vector2(5f, 9f));

            module.chargePips[1] = AddImage(
                module.root,
                "Charge_Lower",
                AILURONEHUDRuntimeStyle.Cyan,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 10f, -22f),
                new Vector2(5f, 9f));

            BuildKeycap(
                module.root,
                "SPACE",
                numericFont,
                new Vector2(width - 91f, -9f),
                47f);

            return module;
        }

        private static AbilityModule BuildStatusModule(
            RectTransform parent,
            string objectName,
            string label,
            string key,
            float x,
            float width,
            TMP_FontAsset headingFont,
            TMP_FontAsset numericFont,
            bool overclock)
        {
            AbilityModule module = new AbilityModule();

            float y = overclock ? -86f : -43f;

            module.root = CreateRect(
                objectName + "_Module",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, y),
                new Vector2(width, 38f));

            module.tutorialVisibilityGroup =
                module.root.gameObject.AddComponent<CanvasGroup>();

            AddImage(
                module.root,
                "Panel",
                new Color(0.018f, 0.022f, 0.024f, 0.78f),
                Vector2.zero,
                Vector2.one,
                Vector2.one * 0.5f,
                Vector2.zero,
                Vector2.zero);

            AddImage(
                module.root,
                "LeftAccent",
                overclock
                    ? AILURONEHUDRuntimeStyle.Red
                    : AILURONEHUDRuntimeStyle.Yellow,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(2f, -8f));

            module.labelText = AddText(
                module.root,
                "Label",
                label,
                headingFont,
                16f,
                AILURONEHUDRuntimeStyle.White,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -7f),
                new Vector2(108f, 22f),
                label.Length > 6 ? 0.6f : 1.1f);

            float keyWidth = key.Length > 1 ? 46f : 24f;

            BuildKeycap(
                module.root,
                key,
                numericFont,
                new Vector2(width - 105f, -9f),
                keyWidth);

            module.valueText = AddText(
                module.root,
                "Value",
                "--",
                numericFont,
                16f,
                AILURONEHUDRuntimeStyle.White,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineRight,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 52f, -8f),
                new Vector2(42f, 20f),
                0.5f);

            RectTransform progressRoot = CreateRect(
                "Progress",
                module.root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(10f, -34f),
                new Vector2(width - 20f, 3f));

            AddImage(
                progressRoot,
                "Track",
                new Color(1f, 1f, 1f, 0.26f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(width - 20f, 1.25f));

            module.progressImage = AddImage(
                progressRoot,
                "Fill",
                AILURONEHUDRuntimeStyle.Cyan,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(width - 20f, 2.25f));

            module.progressFill =
                module.progressImage.rectTransform;

            module.progressWidth = width - 20f;

            if (overclock)
            {
                AddImage(
                    progressRoot,
                    "Overclock_Notch",
                    AILURONEHUDRuntimeStyle.Red,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2((width - 20f) * 0.42f, 0f),
                    new Vector2(7f, 2.5f));
            }

            return module;
        }

        private static void BuildKeycap(
            RectTransform parent,
            string key,
            TMP_FontAsset font,
            Vector2 position,
            float width)
        {
            RectTransform keycap = CreateRect(
                "Keycap_" + key,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                position,
                new Vector2(width, 19f));

            Color outline =
                new Color(0.94f, 0.98f, 1f, 0.72f);

            AddImage(keycap, "Top", outline, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 1.5f));
            AddImage(keycap, "Bottom", outline, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1.5f));
            AddImage(keycap, "Left", outline, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1.5f, 0f));
            AddImage(keycap, "Right", outline, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1.5f, 0f));

            AddText(
                keycap,
                "Key",
                key,
                font,
                13f,
                AILURONEHUDRuntimeStyle.White,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.one * 0.5f,
                Vector2.zero,
                Vector2.zero,
                0.3f);
        }

        private static Canvas ResolveCanvas(
            Image legacyJumpForeground,
            Image legacyOverclockForeground)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Canvas[] canvases =
                Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);

            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas candidate = canvases[index];

                if (candidate != null &&
                    candidate.gameObject.name == CanonicalHudCanvasName &&
                    candidate.gameObject.scene == activeScene)
                {
                    return candidate;
                }
            }

            Canvas canvas =
                legacyJumpForeground != null
                    ? legacyJumpForeground.GetComponentInParent<Canvas>()
                    : null;

            if (canvas == null &&
                legacyOverclockForeground != null)
            {
                canvas =
                    legacyOverclockForeground
                        .GetComponentInParent<Canvas>();
            }

            return canvas;
        }

        private static RectTransform ResolveHUDParent(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            Transform safeArea =
                canvas.transform.Find("HUD_SafeArea");

            return safeArea is RectTransform safeRect
                ? safeRect
                : canvas.transform as RectTransform;
        }

        private static GameObject ResolveLegacyRoot(Image foreground)
        {
            return
                foreground != null &&
                foreground.transform.parent != null
                    ? foreground.transform.parent.gameObject
                    : null;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject gameObject =
                new GameObject(name, typeof(RectTransform));

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            return rect;
        }

        private static Image AddImage(
            RectTransform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return image;
        }

        private static TMP_Text AddText(
            RectTransform parent,
            string name,
            string value,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float characterSpacing)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI text =
                gameObject.GetComponent<TextMeshProUGUI>();

            if (font != null)
            {
                text.font = font;
            }

            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = characterSpacing;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            rect.localEulerAngles =
                new Vector3(0f, 0f, TextCounterLean);

            Shadow shadow =
                gameObject.AddComponent<Shadow>();

            shadow.effectColor =
                new Color(0.005f, 0.02f, 0.035f, 0.88f);

            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;

            return text;
        }

        private static float SmoothTowards(
            float current,
            float target,
            float sharpness,
            float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return Mathf.Clamp01(target);
            }

            float blend =
                1f - Mathf.Exp(-sharpness * deltaTime);

            return Mathf.Lerp(
                current,
                Mathf.Clamp01(target),
                blend);
        }

        private static float EaseOutQuadratic(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - (1f - value) * (1f - value);
        }
    }
}
