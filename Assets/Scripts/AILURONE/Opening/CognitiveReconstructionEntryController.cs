using System.Collections;
using AILURONE.Tutorial;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AILURONE.Opening
{
    [DefaultExecutionOrder(-31900)]
    [DisallowMultipleComponent]
    public sealed class CognitiveReconstructionEntryController : MonoBehaviour
    {
        private const string TutorialSceneName = "Tutorial";
        private static CognitiveReconstructionEntryController _activeInstance;
        [Header("Required References")]
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private StarterAssetsInputs starterAssetsInputs;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private AlwaysEquippedWeaponController weaponController;
        [SerializeField] private TutorialTracker tutorialTracker;

        [Header("HUD")]
        [SerializeField] private Canvas gameplayHudCanvas;

        [Header("Progressive Tutorial HUD")]
        [SerializeField] private bool useProgressiveHudHandoff;
        [SerializeField] private TutorialHUDProgressionController progressiveHudController;

        [Header("Audio")]
        [SerializeField] private AudioSource tutorialBgm;
        [SerializeField] private bool pauseTutorialBgmDuringGate = true;

        [Header("Phase 0 Pass-through")]
        [SerializeField, Range(1, 2)] private int lockedFrames = 2;
        [SerializeField] private bool developmentLogging = true;

        private bool _gateActive;
        private bool _restoring;

        private bool _weaponControllerWasEnabled;
        private bool _tutorialTrackerWasEnabled;
        private float _cinematicMovementInputScale;
        private float _cinematicLookInputScale;
        private bool _cinematicActionsWereLocked;
        private bool _cursorLockedSetting;
        private bool _cursorInputForLook;
        private CursorLockMode _cursorLockMode;
        private bool _cursorVisible;

        private bool _gameplayHudWasActive;
        private bool _gameplayHudCanvasWasEnabled;
        private CanvasGroup _gameplayHudCanvasGroup;
        private bool _gameplayHudCanvasGroupWasAdded;
        private float _gameplayHudAlpha;
        private bool _gameplayHudInteractable;
        private bool _gameplayHudBlocksRaycasts;

        private bool _tutorialBgmWasPlaying;
        private bool _tutorialBgmWasScheduled;
        private bool _tutorialBgmWasPaused;
        private float _tutorialBgmTime;

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != TutorialSceneName)
            {
                Debug.LogWarning(
                    "[Cognitive Entry Gate] The controller is only valid in Tutorial. Disabling it.",
                    this);
                enabled = false;
                return;
            }

            if (_activeInstance != null && _activeInstance != this)
            {
                Debug.LogWarning(
                    "[Cognitive Entry Gate] A duplicate controller was found and disabled.",
                    this);
                enabled = false;
                return;
            }

            _activeInstance = this;

            ResolveOptionalReferences();

            if (!ValidateRequiredReferences())
            {
                AILURONEGameplayActionGate.SetDeploymentLocked(false);
                _activeInstance = null;
                enabled = false;
                return;
            }

            if (tutorialBgm == null)
            {
                Debug.LogWarning(
                    "[Cognitive Entry Gate] Optional Tutorial BGM was not found; audio gating is skipped.",
                    this);
            }

            EnterGate();
        }

        private IEnumerator Start()
        {
            if (!_gateActive)
            {
                yield break;
            }

            // Let the existing Start methods create their normal runtime HUD first.
            yield return null;

            StopTimerAtZero();
            CaptureAndHideRuntimeHud();

            int framesToHold = Mathf.Clamp(lockedFrames, 1, 2);
            for (int frame = 1; frame < framesToHold; frame++)
            {
                yield return null;
                StopTimerAtZero();
            }

            ExitGate();
        }

        private void LateUpdate()
        {
            if (!_gateActive)
            {
                return;
            }

            ClearInputCache();
            ForceLockedCursor();
            StopTimerAtZero();
            PauseTutorialBgm();
        }

        private void OnDisable()
        {
            ExitGate();
        }

        private void OnDestroy()
        {
            ExitGate();

            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
        }

        private void EnterGate()
        {
            CacheState();
            _gateActive = true;

            AILURONEGameplayActionGate.SetDeploymentLocked(true);
            firstPersonController.SetCinematicInputControl(0f, 0f, true);

            weaponController.GetWeapon()?.CancelAdsCharge();
            weaponController.enabled = false;
            tutorialTracker.enabled = false;
            HideGameplayHud();

            ClearInputCache();
            ForceLockedCursor();

            PauseTutorialBgm();

            if (developmentLogging && Debug.isDebugBuild)
            {
                Debug.Log(
                    "[Cognitive Entry Gate] Locked for Phase 0 pass-through verification.",
                    this);
            }
        }

        private void ExitGate()
        {
            if (!_gateActive || _restoring)
            {
                return;
            }

            _restoring = true;

            try
            {
                bool progressiveHandoffReady =
                    useProgressiveHudHandoff &&
                    progressiveHudController != null &&
                    progressiveHudController.PrepareForGateHandoff(gameplayHudCanvas);

                if (gameplayHudCanvas != null)
                {
                    RestoreRuntimeHud();
                }

                if (firstPersonController != null)
                {
                    firstPersonController.ClearCinematicInputControl();

                    if (!Mathf.Approximately(_cinematicMovementInputScale, 1f) ||
                        !Mathf.Approximately(_cinematicLookInputScale, 1f) ||
                        _cinematicActionsWereLocked)
                    {
                        firstPersonController.SetCinematicInputControl(
                            _cinematicMovementInputScale,
                            _cinematicLookInputScale,
                            _cinematicActionsWereLocked);
                    }
                }

                ClearInputCache();

                if (weaponController != null)
                {
                    weaponController.enabled = _weaponControllerWasEnabled;
                }

                if (tutorialTracker != null)
                {
                    tutorialTracker.enabled = _tutorialTrackerWasEnabled;
                }

                RestoreTutorialBgm();
                RestoreCursorState();

                AILURONEGameplayActionGate.SetDeploymentLocked(false);
                if (gameManager != null)
                {
                    gameManager.StartTimer();
                }

                if (progressiveHandoffReady)
                {
                    progressiveHudController.NotifyGateReleased();
                }

                if (developmentLogging && Debug.isDebugBuild)
                {
                    Debug.Log(
                        "[Cognitive Entry Gate] Restored Tutorial state and started the timer from zero.",
                        this);
                }
            }
            finally
            {
                _gateActive = false;
                _restoring = false;
            }
        }

        private void CacheState()
        {
            _weaponControllerWasEnabled = weaponController.enabled;
            _tutorialTrackerWasEnabled = tutorialTracker.enabled;
            _cinematicMovementInputScale =
                firstPersonController.CinematicMovementInputScale;
            _cinematicLookInputScale =
                firstPersonController.CinematicLookInputScale;
            _cinematicActionsWereLocked =
                firstPersonController.CinematicActionsLocked;

            _cursorLockedSetting = starterAssetsInputs.cursorLocked;
            _cursorInputForLook = starterAssetsInputs.cursorInputForLook;
            _cursorLockMode = Cursor.lockState;
            _cursorVisible = Cursor.visible;

            _gameplayHudWasActive = gameplayHudCanvas.gameObject.activeSelf;
            _gameplayHudCanvasWasEnabled = gameplayHudCanvas.enabled;

            _gameplayHudCanvasGroup = gameplayHudCanvas.GetComponent<CanvasGroup>();
            if (_gameplayHudCanvasGroup == null)
            {
                _gameplayHudCanvasGroup = gameplayHudCanvas.gameObject.AddComponent<CanvasGroup>();
                _gameplayHudCanvasGroupWasAdded = true;
            }

            _gameplayHudAlpha = _gameplayHudCanvasGroup.alpha;
            _gameplayHudInteractable = _gameplayHudCanvasGroup.interactable;
            _gameplayHudBlocksRaycasts = _gameplayHudCanvasGroup.blocksRaycasts;

            if (tutorialBgm != null)
            {
                _tutorialBgmWasPlaying = tutorialBgm.isPlaying;
                _tutorialBgmWasScheduled =
                    tutorialBgm.playOnAwake &&
                    tutorialBgm.enabled &&
                    tutorialBgm.gameObject.activeInHierarchy;
                _tutorialBgmWasPaused = false;
                _tutorialBgmTime = tutorialBgm.time;
            }
        }

        private void CaptureAndHideRuntimeHud()
        {
            if (!_gateActive)
            {
                return;
            }

            HideGameplayHud();
        }

        private void HideGameplayHud()
        {
            _gameplayHudCanvasGroup.alpha = 0f;
            _gameplayHudCanvasGroup.interactable = false;
            _gameplayHudCanvasGroup.blocksRaycasts = false;
        }

        private void RestoreRuntimeHud()
        {
            if (_gameplayHudCanvasGroup != null)
            {
                _gameplayHudCanvasGroup.alpha = _gameplayHudAlpha;
                _gameplayHudCanvasGroup.interactable = _gameplayHudInteractable;
                _gameplayHudCanvasGroup.blocksRaycasts = _gameplayHudBlocksRaycasts;

                if (_gameplayHudCanvasGroupWasAdded)
                {
                    Destroy(_gameplayHudCanvasGroup);
                    _gameplayHudCanvasGroup = null;
                }

            }

            gameplayHudCanvas.enabled = _gameplayHudCanvasWasEnabled;
            gameplayHudCanvas.gameObject.SetActive(_gameplayHudWasActive);
        }

        private void StopTimerAtZero()
        {
            if (gameManager == null)
            {
                return;
            }

            gameManager.StartTimer();
            gameManager.StopTimer();

            if (gameManager.timerTextUI != null)
            {
                gameManager.timerTextUI.text = "00:00.00";
            }
        }

        private void PauseTutorialBgm()
        {
            if (!pauseTutorialBgmDuringGate || tutorialBgm == null)
            {
                return;
            }

            if (tutorialBgm.isPlaying)
            {
                tutorialBgm.Pause();
                _tutorialBgmWasPaused = true;
            }
        }

        private void RestoreTutorialBgm()
        {
            if (!pauseTutorialBgmDuringGate || tutorialBgm == null)
            {
                return;
            }

            if (_tutorialBgmWasPaused && _tutorialBgmWasPlaying)
            {
                tutorialBgm.UnPause();
                return;
            }

            if (_tutorialBgmWasScheduled && tutorialBgm.clip != null)
            {
                tutorialBgm.time = Mathf.Clamp(
                    _tutorialBgmTime,
                    0f,
                    tutorialBgm.clip.length);
                tutorialBgm.Play();
                return;
            }

            if (!_tutorialBgmWasPlaying)
            {
                tutorialBgm.Stop();
                tutorialBgm.time = Mathf.Clamp(
                    _tutorialBgmTime,
                    0f,
                    Mathf.Max(0f, tutorialBgm.clip != null ? tutorialBgm.clip.length : 0f));
            }
        }

        private void ClearInputCache()
        {
            if (starterAssetsInputs == null)
            {
                return;
            }

            starterAssetsInputs.MoveInput(Vector2.zero);
            starterAssetsInputs.LookInput(Vector2.zero);
            starterAssetsInputs.JumpInput(false);
            starterAssetsInputs.SprintInput(false);
        }

        private void ForceLockedCursor()
        {
            if (starterAssetsInputs == null)
            {
                return;
            }

            starterAssetsInputs.cursorLocked = true;
            starterAssetsInputs.cursorInputForLook = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void RestoreCursorState()
        {
            if (starterAssetsInputs == null)
            {
                return;
            }

            starterAssetsInputs.cursorLocked = _cursorLockedSetting;
            starterAssetsInputs.cursorInputForLook = _cursorInputForLook;
            Cursor.lockState = _cursorLockedSetting
                ? CursorLockMode.Locked
                : _cursorLockMode;
            Cursor.visible = _cursorLockedSetting
                ? false
                : _cursorVisible;
        }

        private bool ValidateRequiredReferences()
        {
            bool valid = true;

            valid &= ValidateRequired(firstPersonController, nameof(firstPersonController));
            valid &= ValidateRequired(starterAssetsInputs, nameof(starterAssetsInputs));
            valid &= ValidateRequired(playerInput, nameof(playerInput));
            valid &= ValidateRequired(gameManager, nameof(gameManager));
            valid &= ValidateRequired(weaponController, nameof(weaponController));
            valid &= ValidateRequired(tutorialTracker, nameof(tutorialTracker));
            valid &= ValidateRequired(gameplayHudCanvas, nameof(gameplayHudCanvas));

            return valid;
        }

        private bool ValidateRequired(Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError(
                "[Cognitive Entry Gate] Missing required reference: " + fieldName +
                ". The fail-safe left gameplay unlocked.",
                this);
            return false;
        }

        private void ResolveOptionalReferences()
        {
            if (progressiveHudController == null)
            {
                progressiveHudController =
                    FindSceneComponent<TutorialHUDProgressionController>();
            }

            if (tutorialBgm == null)
            {
                AudioSource[] sources = FindSceneComponents<AudioSource>();
                for (int index = 0; index < sources.Length; index++)
                {
                    AudioSource source = sources[index];
                    if (source != null && source.gameObject.name == "AudioSource_BGM")
                    {
                        tutorialBgm = source;
                        break;
                    }
                }
            }
        }

        private T FindSceneComponent<T>() where T : Component
        {
            T[] candidates = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            Scene activeScene = gameObject.scene;

            for (int index = 0; index < candidates.Length; index++)
            {
                T candidate = candidates[index];
                if (candidate != null && candidate.gameObject.scene == activeScene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private T[] FindSceneComponents<T>() where T : Component
        {
            T[] candidates = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            System.Collections.Generic.List<T> results = new System.Collections.Generic.List<T>();
            Scene activeScene = gameObject.scene;

            for (int index = 0; index < candidates.Length; index++)
            {
                T candidate = candidates[index];
                if (candidate != null && candidate.gameObject.scene == activeScene)
                {
                    results.Add(candidate);
                }
            }

            return results.ToArray();
        }
    }
}
