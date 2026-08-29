using System.Collections;
using AILURONE.HUD;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AILURONE.Tutorial
{
    [DefaultExecutionOrder(-31850)]
    [DisallowMultipleComponent]
    public sealed class TutorialHUDProgressionController : MonoBehaviour
    {
        public enum ModuleState
        {
            Locked,
            Reconstructing,
            Online
        }

        [Header("Feature Flag")]
        [SerializeField] private bool useProgressiveTutorialHUD;

        [Header("Tutorial")]
        [SerializeField] private TutorialTracker tutorialTracker;
        [SerializeField] private Canvas gameplayHudCanvas;

        [Header("Persistent HUD")]
        [SerializeField] private PlayerIntegrityHUD integrityHud;
        [SerializeField] private RectTransform timerRoot;
        [SerializeField] private RectTransform scoreRoot;
        [SerializeField] private AILURONECoreObjectiveHUD objectiveHud;
        [SerializeField] private CrosshairController crosshairController;
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private TutorialHUDReconstructionPresentation presentation;

        [Header("Runtime State")]
        [SerializeField] private ModuleState healthState;
        [SerializeField] private ModuleState timerState;
        [SerializeField] private ModuleState doubleJumpState;
        [SerializeField] private ModuleState dashState;
        [SerializeField] private ModuleState crosshairState;
        [SerializeField] private ModuleState timeSlowState;

        private CanvasGroup _timerVisibility;
        private CanvasGroup _scoreVisibility;
        private AILURONEAbilityHUDVisual _abilityHud;
        private bool _ownsVisibility;
        private bool _subscribed;
        private float _healthAlpha;
        private float _timerAlpha;
        private float _crosshairAlpha;
        private float _jumpAlpha;
        private float _dashAlpha;
        private float _timeSlowAlpha;
        private Coroutine _dynamicResolveRoutine;

        public bool UseProgressiveTutorialHUD => useProgressiveTutorialHUD;
        public bool OwnsVisibility => _ownsVisibility;
        public ModuleState HealthState => healthState;
        public ModuleState TimerState => timerState;
        public ModuleState DoubleJumpState => doubleJumpState;
        public ModuleState DashState => dashState;
        public ModuleState CrosshairState => crosshairState;
        public ModuleState TimeSlowState => timeSlowState;

        private void Awake()
        {
            if (!useProgressiveTutorialHUD || gameObject.scene.name != "Tutorial")
            {
                return;
            }

            ResolveReferences();
            ApplyInitialLock();
        }

        private void OnEnable()
        {
            if (!useProgressiveTutorialHUD)
            {
                return;
            }

            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_dynamicResolveRoutine != null)
            {
                StopCoroutine(_dynamicResolveRoutine);
                _dynamicResolveRoutine = null;
            }

            presentation?.CancelAndReset();
        }

        public bool PrepareForGateHandoff(Canvas canonicalHudCanvas)
        {
            if (!useProgressiveTutorialHUD || gameObject.scene.name != "Tutorial")
            {
                return false;
            }

            if (canonicalHudCanvas != null)
            {
                gameplayHudCanvas = canonicalHudCanvas;
            }

            ResolveReferences();
            if (tutorialTracker == null || gameplayHudCanvas == null ||
                integrityHud == null || timerRoot == null ||
                crosshairController == null || timeManager == null)
            {
                Debug.LogError(
                    "[Tutorial Progressive HUD] Required canonical HUD references were not resolved. Full-HUD fallback remains active.",
                    this);
                return false;
            }

            _abilityHud = timeManager.GetComponent<AILURONEAbilityHUDVisual>();
            if (_abilityHud == null)
            {
                Debug.LogError(
                    "[Tutorial Progressive HUD] Ability HUD owner was not ready during gate handoff.",
                    this);
                return false;
            }

            ApplyInitialLock();
            Subscribe();
            _ownsVisibility = true;

            if (_dynamicResolveRoutine != null)
            {
                StopCoroutine(_dynamicResolveRoutine);
            }

            _dynamicResolveRoutine = StartCoroutine(ResolveDynamicHudForTwoSeconds());
            return true;
        }

        public void NotifyGateReleased()
        {
            if (_ownsVisibility)
            {
                ApplyAllVisibility();
            }
        }

        private void ApplyInitialLock()
        {
            healthState = ModuleState.Locked;
            timerState = ModuleState.Locked;
            doubleJumpState = ModuleState.Locked;
            dashState = ModuleState.Locked;
            crosshairState = ModuleState.Locked;
            timeSlowState = ModuleState.Locked;

            _jumpAlpha = 0f;
            _dashAlpha = 0f;
            _timeSlowAlpha = 0f;
            _healthAlpha = 0f;
            _timerAlpha = 0f;
            _crosshairAlpha = 0f;

            EnsureStaticVisibilityGroups();
            ApplyAllVisibility();
        }

        private void ApplyAllVisibility()
        {
            integrityHud?.SetTutorialVisibilityAlpha(_healthAlpha);

            if (_timerVisibility != null)
            {
                _timerVisibility.alpha = _timerAlpha;
                _timerVisibility.interactable = false;
                _timerVisibility.blocksRaycasts = false;
            }

            if (_scoreVisibility != null)
            {
                _scoreVisibility.alpha = 0f;
                _scoreVisibility.interactable = false;
                _scoreVisibility.blocksRaycasts = false;
            }

            objectiveHud?.SetTutorialVisibilityAlpha(0f);
            crosshairController?.SetDeploymentAlpha(_crosshairAlpha);

            _abilityHud = _abilityHud != null
                ? _abilityHud
                : timeManager != null
                    ? timeManager.GetComponent<AILURONEAbilityHUDVisual>()
                    : null;
            _abilityHud?.SetTutorialModuleVisibility(
                _jumpAlpha,
                _dashAlpha,
                _timeSlowAlpha);
        }

        private void HandleStepCompleted(TutorialTracker.TutorialStep step)
        {
            switch (step)
            {
                case TutorialTracker.TutorialStep.OrientationMoveLook:
                    RevealHealth();
                    RevealTimer();
                    break;
                case TutorialTracker.TutorialStep.MobilityDoubleJump:
                    RevealDoubleJump();
                    break;
                case TutorialTracker.TutorialStep.MobilityDash:
                    RevealDash();
                    break;
                case TutorialTracker.TutorialStep.CombatFire:
                    RevealCrosshair();
                    break;
                case TutorialTracker.TutorialStep.TemporalTimeSlow:
                    RevealTimeSlow();
                    break;
            }
        }

        private void RevealHealth()
        {
            BeginReveal(
                ref healthState,
                integrityHud != null ? integrityHud.transform as RectTransform : null,
                "FRAME INTEGRITY // ONLINE",
                alpha =>
                {
                    _healthAlpha = alpha;
                    integrityHud?.SetTutorialVisibilityAlpha(alpha);
                });
        }

        private void RevealTimer()
        {
            BeginReveal(
                ref timerState,
                timerRoot,
                "SYSTEM CLOCK // ONLINE",
                alpha =>
                {
                    if (_timerVisibility != null)
                    {
                        _timerAlpha = alpha;
                        _timerVisibility.alpha = alpha;
                    }
                });
        }

        private void RevealDoubleJump()
        {
            BeginReveal(
                ref doubleJumpState,
                _abilityHud != null ? _abilityHud.JumpModuleRect : null,
                "DOUBLE JUMP // RESTORED",
                alpha =>
                {
                    _jumpAlpha = alpha;
                    ApplyAbilityVisibility();
                });
        }

        private void RevealDash()
        {
            BeginReveal(
                ref dashState,
                _abilityHud != null ? _abilityHud.DashModuleRect : null,
                "DASH // RESTORED",
                alpha =>
                {
                    _dashAlpha = alpha;
                    ApplyAbilityVisibility();
                });
        }

        private void RevealCrosshair()
        {
            BeginReveal(
                ref crosshairState,
                null,
                "TARGETING SYSTEM // ONLINE",
                alpha =>
                {
                    _crosshairAlpha = alpha;
                    crosshairController?.SetDeploymentAlpha(alpha);
                });
        }

        private void RevealTimeSlow()
        {
            BeginReveal(
                ref timeSlowState,
                _abilityHud != null ? _abilityHud.OverclockModuleRect : null,
                "TEMPORAL CONTROL // DETECTED",
                alpha =>
                {
                    _timeSlowAlpha = alpha;
                    ApplyAbilityVisibility();
                });
        }

        private void BeginReveal(
            ref ModuleState state,
            RectTransform target,
            string message,
            System.Action<float> setAlpha)
        {
            if (state != ModuleState.Locked)
            {
                return;
            }

            state = ModuleState.Reconstructing;
            if (presentation == null)
            {
                setAlpha(1f);
                state = ModuleState.Online;
                return;
            }

            presentation.QueueReveal(
                target,
                message,
                setAlpha,
                () => SetOnlineForMessage(message));
        }

        private void SetOnlineForMessage(string message)
        {
            switch (message)
            {
                case "FRAME INTEGRITY // ONLINE":
                    healthState = ModuleState.Online;
                    break;
                case "SYSTEM CLOCK // ONLINE":
                    timerState = ModuleState.Online;
                    break;
                case "DOUBLE JUMP // RESTORED":
                    doubleJumpState = ModuleState.Online;
                    break;
                case "DASH // RESTORED":
                    dashState = ModuleState.Online;
                    break;
                case "TARGETING SYSTEM // ONLINE":
                    crosshairState = ModuleState.Online;
                    break;
                case "TEMPORAL CONTROL // DETECTED":
                    timeSlowState = ModuleState.Online;
                    break;
            }

            ApplyAllVisibility();
        }

        private void ApplyAbilityVisibility()
        {
            _abilityHud?.SetTutorialModuleVisibility(
                _jumpAlpha,
                _dashAlpha,
                _timeSlowAlpha);
        }

        private void EnsureStaticVisibilityGroups()
        {
            if (timerRoot != null)
            {
                _timerVisibility = timerRoot.GetComponent<CanvasGroup>();
                if (_timerVisibility == null)
                {
                    _timerVisibility = timerRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (scoreRoot != null)
            {
                _scoreVisibility = scoreRoot.GetComponent<CanvasGroup>();
                if (_scoreVisibility == null)
                {
                    _scoreVisibility = scoreRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private IEnumerator ResolveDynamicHudForTwoSeconds()
        {
            float elapsed = 0f;
            while (elapsed < 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_abilityHud == null && timeManager != null)
                {
                    _abilityHud = timeManager.GetComponent<AILURONEAbilityHUDVisual>();
                }

                ApplyAllVisibility();
                if (_abilityHud != null && _abilityHud.IsRuntimeHudReady)
                {
                    _dynamicResolveRoutine = null;
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                "[Tutorial Progressive HUD] HUD MODULE RESOLVE FAILURE: runtime Ability HUD was not fully ready within 2 seconds.",
                this);
            _dynamicResolveRoutine = null;
        }

        private void ResolveReferences()
        {
            tutorialTracker = ResolveSceneComponent(tutorialTracker);
            integrityHud = ResolveSceneComponent(integrityHud);
            timerRoot = ResolveNamedRect(timerRoot, "TopCenter_Timer");
            scoreRoot = ResolveNamedRect(scoreRoot, "BottomCenter_Score");
            objectiveHud = ResolveSceneComponent(objectiveHud);
            crosshairController = ResolveSceneComponent(crosshairController);
            timeManager = ResolveSceneComponent(timeManager);
            presentation = ResolveSceneComponent(presentation);

            if (gameplayHudCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
                for (int index = 0; index < canvases.Length; index++)
                {
                    if (canvases[index] != null &&
                        canvases[index].gameObject.scene == gameObject.scene &&
                        canvases[index].gameObject.name == "HUD_Canvas_AILURONE")
                    {
                        gameplayHudCanvas = canvases[index];
                        break;
                    }
                }
            }

            _abilityHud = timeManager != null
                ? timeManager.GetComponent<AILURONEAbilityHUDVisual>()
                : null;
            EnsureStaticVisibilityGroups();
        }

        private T ResolveSceneComponent<T>(T current) where T : Component
        {
            if (current != null)
            {
                return current;
            }

            T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Include);
            Scene scene = gameObject.scene;
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] != null && candidates[index].gameObject.scene == scene)
                {
                    return candidates[index];
                }
            }

            return null;
        }

        private RectTransform ResolveNamedRect(RectTransform current, string objectName)
        {
            if (current != null)
            {
                return current;
            }

            RectTransform[] candidates =
                FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] != null &&
                    candidates[index].gameObject.scene == gameObject.scene &&
                    candidates[index].gameObject.name == objectName)
                {
                    return candidates[index];
                }
            }

            return null;
        }

        private void Subscribe()
        {
            if (_subscribed || tutorialTracker == null)
            {
                return;
            }

            tutorialTracker.StepCompleted += HandleStepCompleted;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (tutorialTracker != null)
            {
                tutorialTracker.StepCompleted -= HandleStepCompleted;
            }

            _subscribed = false;
        }
    }
}
