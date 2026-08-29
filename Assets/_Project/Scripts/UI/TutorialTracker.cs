using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AILURONE.Tutorial
{
    [DisallowMultipleComponent]
    public class TutorialTracker : MonoBehaviour
    {
        public enum TutorialStep
        {
            OrientationMoveLook,
            MobilityJump,
            MobilityDoubleJump,
            MobilityDash,
            CombatFire,
            CombatAds,
            TemporalTimeSlow,
            Complete
        }

        [Header("Existing Instruction UI")]
        public TextMeshProUGUI moveLookText;
        public Image moveLookStrike;
        public TextMeshProUGUI jumpDashText;
        public Image jumpDashStrike;
        public TextMeshProUGUI shootText;
        public Image shootStrike;
        public TextMeshProUGUI timeSlowText;
        public Image timeSlowStrike;
        public CanvasGroup canvasGroup;

        [Header("Feature Flag")]
        [SerializeField] private bool useSequentialProgression;

        [Header("Animation Settings")]
        public float strikeAnimationDuration = 0.4f;
        public float fadeOutDelayAfterAllComplete = 2.0f;
        public float finalFadeDuration = 1.0f;
        public Color completedTextColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);

        [Header("Successful Action Sources")]
        [SerializeField] private StarterAssets.FirstPersonController firstPersonController;
        [SerializeField] private DashController dashController;
        [SerializeField] private PlayerWeapon playerWeapon;
        [SerializeField] private TimeManager timeManager;

        public TutorialStep CurrentStep { get; private set; } =
            TutorialStep.OrientationMoveLook;

        public bool IsComplete => CurrentStep == TutorialStep.Complete;
        public bool UseSequentialProgression => useSequentialProgression;

        public event Action<TutorialStep> StepChanged;
        public event Action<TutorialStep> StepCompleted;

        private bool _hasMoved;
        private bool _hasLooked;
        private bool _legacyMoveLookDone;
        private bool _legacyJumpDashDone;
        private bool _legacyShootDone;
        private bool _legacyTimeSlowDone;
        private bool _legacyAllDone;
        private bool _legacyHasJumped;
        private bool _legacyHasDashed;
        private bool _legacyHasLeftClicked;
        private bool _legacyHasRightClicked;
        private bool _subscribed;
        private Coroutine _fadeRoutine;

        private void Start()
        {
            if (!useSequentialProgression)
            {
                ResetLegacyStrike(moveLookStrike);
                ResetLegacyStrike(jumpDashStrike);
                ResetLegacyStrike(shootStrike);
                ResetLegacyStrike(timeSlowStrike);
                return;
            }

            HideLegacyStrikes();
            ResolveSources();
            Subscribe();
            ApplyStepPresentation();
        }

        private void OnEnable()
        {
            if (!useSequentialProgression)
            {
                return;
            }

            ResolveSources();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!useSequentialProgression)
            {
                UpdateLegacyChecklist();
                return;
            }

            if (CurrentStep != TutorialStep.OrientationMoveLook ||
                Keyboard.current == null ||
                Mouse.current == null)
            {
                return;
            }

            if (Keyboard.current.wKey.isPressed ||
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.sKey.isPressed ||
                Keyboard.current.dKey.isPressed)
            {
                _hasMoved = true;
            }

            if (Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f)
            {
                _hasLooked = true;
            }

            if (_hasMoved && _hasLooked)
            {
                CompleteCurrentStep(TutorialStep.OrientationMoveLook);
            }
        }

        private void UpdateLegacyChecklist()
        {
            if (_legacyAllDone || Keyboard.current == null || Mouse.current == null)
            {
                return;
            }

            if (!_legacyMoveLookDone)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
                    Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed)
                {
                    _hasMoved = true;
                }

                if (Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f)
                {
                    _hasLooked = true;
                }

                if (_hasMoved && _hasLooked)
                {
                    _legacyMoveLookDone = true;
                    StartCoroutine(LegacyCrossOutRoutine(moveLookText, moveLookStrike));
                }
            }

            if (!_legacyJumpDashDone)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    _legacyHasJumped = true;
                }

                if (Keyboard.current.shiftKey.wasPressedThisFrame)
                {
                    _legacyHasDashed = true;
                }

                if (_legacyHasJumped && _legacyHasDashed)
                {
                    _legacyJumpDashDone = true;
                    StartCoroutine(LegacyCrossOutRoutine(jumpDashText, jumpDashStrike));
                }
            }

            if (!_legacyShootDone)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    _legacyHasLeftClicked = true;
                }

                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    _legacyHasRightClicked = true;
                }

                if (_legacyHasLeftClicked && _legacyHasRightClicked)
                {
                    _legacyShootDone = true;
                    StartCoroutine(LegacyCrossOutRoutine(shootText, shootStrike));
                }
            }

            if (!_legacyTimeSlowDone && Keyboard.current.fKey.wasPressedThisFrame)
            {
                _legacyTimeSlowDone = true;
                StartCoroutine(LegacyCrossOutRoutine(timeSlowText, timeSlowStrike));
            }

            if (_legacyMoveLookDone && _legacyJumpDashDone &&
                _legacyShootDone && _legacyTimeSlowDone)
            {
                _legacyAllDone = true;
                StartCoroutine(LegacyFadeOutAllRoutine());
            }
        }

        private static void ResetLegacyStrike(Image strike)
        {
            if (strike == null)
            {
                return;
            }

            strike.gameObject.SetActive(true);
            strike.rectTransform.pivot = new Vector2(0f, 0.5f);
            strike.rectTransform.localScale = new Vector3(0f, 1f, 1f);
        }

        private IEnumerator LegacyCrossOutRoutine(TextMeshProUGUI text, Image strike)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, strikeAnimationDuration);
            Color startColor = text != null ? text.color : Color.white;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                if (strike != null)
                {
                    strike.rectTransform.localScale = new Vector3(t, 1f, 1f);
                }

                if (text != null)
                {
                    text.color = Color.Lerp(startColor, completedTextColor, t);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (strike != null)
            {
                strike.rectTransform.localScale = Vector3.one;
            }

            if (text != null)
            {
                text.color = completedTextColor;
            }
        }

        private IEnumerator LegacyFadeOutAllRoutine()
        {
            yield return new WaitForSeconds(fadeOutDelayAfterAllComplete);
            if (canvasGroup == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, finalFadeDuration);
            while (elapsed < duration)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        private void HandleJumpPerformed()
        {
            CompleteCurrentStep(TutorialStep.MobilityJump);
        }

        private void HandleDoubleJumpPerformed()
        {
            CompleteCurrentStep(TutorialStep.MobilityDoubleJump);
        }

        private void HandleDashPerformed()
        {
            CompleteCurrentStep(TutorialStep.MobilityDash);
        }

        private void HandleShotFiredSuccessfully()
        {
            CompleteCurrentStep(TutorialStep.CombatFire);
        }

        private void HandleAdsActivated()
        {
            CompleteCurrentStep(TutorialStep.CombatAds);
        }

        private void HandleTimeSlowActivated()
        {
            CompleteCurrentStep(TutorialStep.TemporalTimeSlow);
        }

        private void CompleteCurrentStep(TutorialStep expectedStep)
        {
            if (CurrentStep != expectedStep)
            {
                return;
            }

            StepCompleted?.Invoke(CurrentStep);
            CurrentStep = GetNextStep(CurrentStep);
            StepChanged?.Invoke(CurrentStep);
            ApplyStepPresentation();

            if (CurrentStep == TutorialStep.Complete)
            {
                if (_fadeRoutine != null)
                {
                    StopCoroutine(_fadeRoutine);
                }

                _fadeRoutine = StartCoroutine(FadeOutAllRoutine());
            }
        }

        private static TutorialStep GetNextStep(TutorialStep step)
        {
            int next = Mathf.Min(
                (int)TutorialStep.Complete,
                (int)step + 1);
            return (TutorialStep)next;
        }

        private void ApplyStepPresentation()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            string phase;
            string action;
            string instruction;
            string progress;

            switch (CurrentStep)
            {
                case TutorialStep.OrientationMoveLook:
                    phase = "PHASE 01 // ORIENTATION";
                    action = "MOVE / LOOK";
                    instruction = "WASD // MOVE    MOUSE // LOOK";
                    progress = "STEP 1 / 7";
                    break;
                case TutorialStep.MobilityJump:
                    phase = "PHASE 02 // MOBILITY";
                    action = "JUMP";
                    instruction = "SPACE // JUMP";
                    progress = "STEP 2 / 7";
                    break;
                case TutorialStep.MobilityDoubleJump:
                    phase = "PHASE 02 // MOBILITY";
                    action = "DOUBLE JUMP";
                    instruction = "SPACE // JUMP AGAIN WHILE AIRBORNE";
                    progress = "STEP 3 / 7";
                    break;
                case TutorialStep.MobilityDash:
                    phase = "PHASE 02 // MOBILITY";
                    action = "DASH";
                    instruction = "SHIFT // DASH";
                    progress = "STEP 4 / 7";
                    break;
                case TutorialStep.CombatFire:
                    phase = "PHASE 03 // COMBAT";
                    action = "FIRE";
                    instruction = "LMB // FIRE";
                    progress = "STEP 5 / 7";
                    break;
                case TutorialStep.CombatAds:
                    phase = "PHASE 03 // COMBAT";
                    action = "ADS";
                    instruction = "RMB // AIM DOWN SIGHTS";
                    progress = "STEP 6 / 7";
                    break;
                case TutorialStep.TemporalTimeSlow:
                    phase = "PHASE 04 // TEMPORAL CONTROL";
                    action = "TIME SLOW";
                    instruction = "F // ACTIVATE TIME SLOW";
                    progress = "STEP 7 / 7";
                    break;
                default:
                    phase = "COGNITIVE RECONSTRUCTION";
                    action = "CALIBRATION COMPLETE";
                    instruction = "ALL REQUIRED SYSTEMS // ONLINE";
                    progress = "7 / 7";
                    break;
            }

            SetText(moveLookText, phase);
            SetText(jumpDashText, action);
            SetText(shootText, instruction);
            SetText(timeSlowText, progress);
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
                target.color = Color.white;
            }
        }

        private void HideLegacyStrikes()
        {
            HideStrike(moveLookStrike);
            HideStrike(jumpDashStrike);
            HideStrike(shootStrike);
            HideStrike(timeSlowStrike);
        }

        private static void HideStrike(Image strike)
        {
            if (strike != null)
            {
                strike.gameObject.SetActive(false);
            }
        }

        private IEnumerator FadeOutAllRoutine()
        {
            float delay = Mathf.Max(0f, fadeOutDelayAfterAllComplete);
            while (delay > 0f)
            {
                delay -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (canvasGroup == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, finalFadeDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            _fadeRoutine = null;
        }

        private void ResolveSources()
        {
            firstPersonController = ResolveSceneComponent(firstPersonController);
            dashController = ResolveSceneComponent(dashController);
            playerWeapon = ResolveSceneComponent(playerWeapon);
            timeManager = ResolveSceneComponent(timeManager);
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

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (firstPersonController == null || dashController == null ||
                playerWeapon == null || timeManager == null)
            {
                return;
            }

            firstPersonController.JumpPerformed += HandleJumpPerformed;
            firstPersonController.DoubleJumpPerformed += HandleDoubleJumpPerformed;
            dashController.DashPerformed += HandleDashPerformed;
            playerWeapon.ShotFiredSuccessfully += HandleShotFiredSuccessfully;
            playerWeapon.AdsActivated += HandleAdsActivated;
            timeManager.TimeSlowActivated += HandleTimeSlowActivated;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (firstPersonController != null)
            {
                firstPersonController.JumpPerformed -= HandleJumpPerformed;
                firstPersonController.DoubleJumpPerformed -= HandleDoubleJumpPerformed;
            }

            if (dashController != null)
            {
                dashController.DashPerformed -= HandleDashPerformed;
            }

            if (playerWeapon != null)
            {
                playerWeapon.ShotFiredSuccessfully -= HandleShotFiredSuccessfully;
                playerWeapon.AdsActivated -= HandleAdsActivated;
            }

            if (timeManager != null)
            {
                timeManager.TimeSlowActivated -= HandleTimeSlowActivated;
            }

            _subscribed = false;
        }
    }
}
