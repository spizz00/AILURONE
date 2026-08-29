using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    [DisallowMultipleComponent]
    public sealed class AILURONELevelObjectivePresentation : MonoBehaviour
    {
        private enum ObjectivePhase
        {
            Recover,
            Link,
            EnterAperture
        }

        private const int MaximumVisibleSubtasks = 3;
        private const float SourceResolveInterval = 0.5f;
        private const float EncounterPollInterval = 0.1f;
        private const float HiddenRecallHold = 3f;
        private const string DeploymentBootOverlayName =
            "HUD_DeploymentBootOverlay";

        [Header("Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private FinalGateManager finalGateManager;
        [SerializeField] private LevelEntrySequenceController entrySequence;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Clear Room Task")]
        [SerializeField] private CombatEncounter clearRoomEncounter;
        [SerializeField] private CombatEncounter[] additionalSubtaskEncounters =
            Array.Empty<CombatEncounter>();
        [SerializeField] private GlitchDoor clearRoomGate;
        [SerializeField] private EnemyTarget[] clearRoomRequiredEnemies =
            Array.Empty<EnemyTarget>();

        [Header("Presentation Groups")]
        [SerializeField] private CanvasGroup presentationGroup;
        [SerializeField] private CanvasGroup diamondGroup;
        [SerializeField] private CanvasGroup lineGroup;
        [SerializeField] private CanvasGroup headerGroup;
        [SerializeField] private CanvasGroup objectiveGroup;
        [SerializeField] private CanvasGroup slotsGroup;
        [SerializeField] private CanvasGroup strikeGroup;
        [SerializeField] private CanvasGroup hintGroup;

        [Header("Presentation Geometry")]
        [SerializeField] private RectTransform diamondRoot;
        [SerializeField] private RectTransform verticalLine;
        [SerializeField] private RectTransform objectiveRoot;
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private RectTransform completionStrike;

        [Header("Text")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text hintText;

        [Header("Progress Slots")]
        [SerializeField] private RectTransform[] slotRoots =
            Array.Empty<RectTransform>();
        [SerializeField] private Image[] slotFills =
            Array.Empty<Image>();

        [Header("Diamond")]
        [SerializeField] private Graphic[] diamondGraphics =
            Array.Empty<Graphic>();

        [Header("Subtasks")]
        [SerializeField] private RectTransform[] subtaskRoots =
            Array.Empty<RectTransform>();
        [SerializeField] private CanvasGroup[] subtaskGroups =
            Array.Empty<CanvasGroup>();
        [SerializeField] private TMP_Text[] subtaskNames =
            Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] subtaskCounts =
            Array.Empty<TMP_Text>();
        [SerializeField] private Image[] subtaskLines =
            Array.Empty<Image>();

        [Header("Audio")]
        [SerializeField] private AudioClip revealSound;
        [SerializeField] private AudioClip progressSound;
        [SerializeField] private AudioClip completionSound;
        [SerializeField, Range(0f, 1f)] private float revealVolume = 0.28f;
        [SerializeField, Range(0f, 1f)] private float progressVolume = 0.34f;
        [SerializeField, Range(0f, 1f)] private float completionVolume = 0.38f;

        [Header("Colours")]
        [SerializeField] private Color primaryWhite =
            new Color(0.94f, 0.96f, 0.95f, 1f);
        [SerializeField] private Color mutedWhite =
            new Color(0.68f, 0.73f, 0.73f, 0.9f);
        [SerializeField] private Color cyan =
            new Color(0.34f, 0.85f, 0.91f, 1f);
        [SerializeField] private Color completionOrange =
            new Color(0.94f, 0.43f, 0.16f, 1f);

        private readonly List<CombatEncounter> _encounters =
            new List<CombatEncounter>();
        private readonly List<CombatEncounter> _activeEncounters =
            new List<CombatEncounter>();
        private readonly Dictionary<CombatEncounter, int> _lastRemaining =
            new Dictionary<CombatEncounter, int>();

        private bool _gameSubscribed;
        private bool _gateSubscribed;
        private bool _healthSubscribed;
        private bool _hasRevealed;
        private bool _isVisible;
        private bool _userWantsHidden;
        private bool _temporaryRecall;
        private bool _rewindInProgress;
        private bool _clearRoomGateTearTriggered;

        private int _carriedCount;
        private int _installedCount;
        private int _requiredCount = 3;
        private ObjectivePhase _phase;
        private ObjectivePhase _displayedPhase;

        private float _nextSourceResolveAt;
        private float _nextEncounterPollAt;
        private Coroutine _mainRoutine;
        private Coroutine _feedbackRoutine;

#if UNITY_EDITOR
        private bool _editorSimulationActive;
        private readonly bool[] _editorSubtaskVisible =
            new bool[MaximumVisibleSubtasks];
        private readonly int[] _editorSubtaskRemaining =
            new int[MaximumVisibleSubtasks];
        private float _editorAnimationSpeed = 1f;
        private bool _editorSavedHasRevealed;
        private bool _editorSavedIsVisible;
        private bool _editorSavedUserWantsHidden;
#endif

        private sealed class EnemyLocatorMarker
        {
            public RectTransform root;
            public CanvasGroup group;
            public RectTransform[] edges;
        }

        private readonly List<EnemyTarget> _remainingClearRoomEnemies =
            new List<EnemyTarget>();
        private readonly List<EnemyLocatorMarker> _enemyLocatorMarkers =
            new List<EnemyLocatorMarker>();
        private readonly Dictionary<EnemyTarget, Renderer[]>
            _enemyFrameRenderers =
                new Dictionary<EnemyTarget, Renderer[]>();
        private RectTransform _enemyLocatorLayer;
        private Canvas _enemyLocatorCanvas;

        public CombatEncounter ClearRoomEncounter => clearRoomEncounter;
        public GlitchDoor ClearRoomGate => clearRoomGate;
        public EnemyTarget[] ClearRoomRequiredEnemies =>
            clearRoomRequiredEnemies;

#if UNITY_EDITOR
        public bool EditorSimulationActive => _editorSimulationActive;

        public void EditorBeginSimulation()
        {
            if (_editorSimulationActive)
            {
                return;
            }

            _editorSavedHasRevealed = _hasRevealed;
            _editorSavedIsVisible = _isVisible;
            _editorSavedUserWantsHidden = _userWantsHidden;
            StopAnimations();
            UnsubscribeSources();
            UnsubscribeEncounters();
            _editorSimulationActive = true;
            Array.Clear(
                _editorSubtaskVisible,
                0,
                _editorSubtaskVisible.Length);
            Array.Clear(
                _editorSubtaskRemaining,
                0,
                _editorSubtaskRemaining.Length);
            _hasRevealed = true;
            _userWantsHidden = false;
            _temporaryRecall = false;
            _rewindInProgress = false;
            _requiredCount = 3;
            EditorPreviewObjective(0, 0, false, false);
        }

        public void EditorEndSimulation()
        {
            if (!_editorSimulationActive)
            {
                return;
            }

            StopAnimations();
            _editorSimulationActive = false;
            Array.Clear(
                _editorSubtaskVisible,
                0,
                _editorSubtaskVisible.Length);
            Array.Clear(
                _editorSubtaskRemaining,
                0,
                _editorSubtaskRemaining.Length);
            ResolveSources(true);
            DiscoverEncounters();
            PullCurrentState();
            RenderMainState();
            RefreshSubtasks(false);
            _hasRevealed = _editorSavedHasRevealed;
            _userWantsHidden = _editorSavedUserWantsHidden;
            _temporaryRecall = false;

            if (_editorSavedHasRevealed && _editorSavedIsVisible)
            {
                SetVisibleImmediate();
            }
            else
            {
                SetHiddenImmediate(
                    _editorSavedHasRevealed &&
                    _editorSavedUserWantsHidden);
            }
        }

        public void EditorSetAnimationSpeed(float speed)
        {
            _editorAnimationSpeed = Mathf.Clamp(speed, 1f, 4f);
        }

        public void EditorPreviewObjective(
            int phase,
            int progress,
            bool animateReveal,
            bool playSound)
        {
            EnsureEditorSimulation();
            StopAnimations();
            _requiredCount = 3;
            progress = Mathf.Clamp(progress, 0, _requiredCount);

            switch (Mathf.Clamp(phase, 0, 2))
            {
                case 0:
                    _carriedCount = progress;
                    _installedCount = 0;
                    _phase = ObjectivePhase.Recover;
                    break;
                case 1:
                    _installedCount = progress;
                    _carriedCount = _requiredCount - progress;
                    _phase = ObjectivePhase.Link;
                    break;
                default:
                    _carriedCount = 0;
                    _installedCount = _requiredCount;
                    _phase = ObjectivePhase.EnterAperture;
                    break;
            }

            _displayedPhase = _phase;
            _hasRevealed = true;
            _userWantsHidden = false;
            _temporaryRecall = false;
            RenderMainState();
            RefreshSubtasks(false);

            if (animateReveal)
            {
                StartMainAnimation(FullRevealRoutine(playSound));
            }
            else
            {
                SetVisibleImmediate();
                SetHint(true, "[Q] HIDE OBJECTIVES");
            }
        }

        public void EditorAnimateProgress(int phase, int progress)
        {
            EditorPreviewObjective(phase, progress, false, false);
            StartFeedbackAnimation(Mathf.Clamp(progress, 0, 3) - 1);
        }

        public void EditorAnimateTransition(int phase, int progress)
        {
            EnsureEditorSimulation();
            _requiredCount = 3;
            progress = Mathf.Clamp(progress, 0, _requiredCount);
            _phase = (ObjectivePhase)Mathf.Clamp(phase, 0, 2);
            if (_phase == ObjectivePhase.Recover)
            {
                _carriedCount = progress;
                _installedCount = 0;
            }
            else if (_phase == ObjectivePhase.Link)
            {
                _installedCount = progress;
                _carriedCount = _requiredCount - progress;
            }
            else
            {
                _installedCount = _requiredCount;
                _carriedCount = 0;
            }

            _hasRevealed = true;
            _userWantsHidden = false;
            StartMainAnimation(StageTransitionRoutine());
        }

        public void EditorSetClearRoomSubtask(
            int row,
            bool visible,
            int remaining)
        {
            EnsureEditorSimulation();
            int safeRow = Mathf.Clamp(
                row,
                0,
                MaximumVisibleSubtasks - 1);
            _editorSubtaskVisible[safeRow] = visible;
            _editorSubtaskRemaining[safeRow] = Mathf.Max(0, remaining);
            RefreshSubtasks(true);
            if (_isVisible)
            {
                SetSubtaskAlpha(1f);
            }
        }

        public void EditorPlayHide()
        {
            EnsureEditorSimulation();
            _userWantsHidden = true;
            StartMainAnimation(ArchiveHideRoutine());
        }

        public void EditorPlayReveal()
        {
            EnsureEditorSimulation();
            _userWantsHidden = false;
            _temporaryRecall = false;
            _hasRevealed = true;
            StartMainAnimation(FullRevealRoutine(true));
        }

        public void EditorPlayRewindReveal()
        {
            EnsureEditorSimulation();
            StopAnimations();
            SetHiddenImmediate(false);
            _userWantsHidden = false;
            _temporaryRecall = false;
            _hasRevealed = true;
            StartMainAnimation(FullRevealRoutine(true));
        }

        private void EnsureEditorSimulation()
        {
            if (!_editorSimulationActive)
            {
                EditorBeginSimulation();
            }
        }
#endif

        public bool ValidateConfiguration(out string reason)
        {
            if (presentationGroup == null || diamondGroup == null ||
                lineGroup == null || headerGroup == null ||
                objectiveGroup == null || slotsGroup == null ||
                strikeGroup == null || hintGroup == null)
            {
                reason = "One or more CanvasGroup references are missing.";
                return false;
            }

            if (diamondRoot == null || verticalLine == null ||
                objectiveRoot == null || slotsRoot == null ||
                completionStrike == null)
            {
                reason = "One or more RectTransform references are missing.";
                return false;
            }

            if (headerText == null || objectiveText == null || hintText == null)
            {
                reason = "One or more text references are missing.";
                return false;
            }

            if (clearRoomEncounter == null || clearRoomGate == null)
            {
                reason = "The explicit clear-room Encounter or Gate is missing.";
                return false;
            }

            if (clearRoomRequiredEnemies == null ||
                clearRoomRequiredEnemies.Length == 0)
            {
                reason = "The explicit clear-room enemy list is empty.";
                return false;
            }

            for (int index = 0;
                 index < clearRoomRequiredEnemies.Length;
                 index++)
            {
                if (clearRoomRequiredEnemies[index] == null)
                {
                    reason = "The explicit clear-room enemy list contains a missing reference.";
                    return false;
                }
            }

            if (slotRoots.Length != 3 || slotFills.Length != 3)
            {
                reason = "Exactly three Rewrite Node progress slots are required.";
                return false;
            }

            if (subtaskRoots.Length != MaximumVisibleSubtasks ||
                subtaskGroups.Length != MaximumVisibleSubtasks ||
                subtaskNames.Length != MaximumVisibleSubtasks ||
                subtaskCounts.Length != MaximumVisibleSubtasks ||
                subtaskLines.Length != MaximumVisibleSubtasks)
            {
                reason = "Exactly three active encounter subtask rows are required.";
                return false;
            }

            if (additionalSubtaskEncounters == null)
            {
                reason = "Additional subtask Encounter bindings are missing.";
                return false;
            }

            for (int index = 0;
                 index < additionalSubtaskEncounters.Length;
                 index++)
            {
                CombatEncounter encounter = additionalSubtaskEncounters[index];
                if (encounter == null || encounter == clearRoomEncounter)
                {
                    reason = "Additional subtask Encounter bindings contain a missing or duplicate primary Encounter.";
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (additionalSubtaskEncounters[previous] == encounter)
                    {
                        reason = "Additional subtask Encounter bindings contain duplicates.";
                        return false;
                    }
                }
            }

            reason = string.Empty;
            return true;
        }

        private void Awake()
        {
            ResolveSources(true);
            DiscoverEncounters();
            PullCurrentState();
            RenderMainState();
            RefreshSubtasks(false);
            SetHiddenImmediate(false);
        }

        private void OnEnable()
        {
            ResolveSources(true);
            DiscoverEncounters();
            SubscribeSources();
            PullCurrentState();
            RenderMainState();
            RefreshSubtasks(false);
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (_editorSimulationActive)
            {
                if (Keyboard.current != null &&
                    Keyboard.current.qKey.wasPressedThisFrame)
                {
                    if (_isVisible)
                    {
                        EditorPlayHide();
                    }
                    else
                    {
                        EditorPlayReveal();
                    }
                }
                return;
            }
#endif
            if (Time.unscaledTime >= _nextSourceResolveAt)
            {
                _nextSourceResolveAt =
                    Time.unscaledTime + SourceResolveInterval;
                ResolveSources(false);
            }

            if (Time.unscaledTime >= _nextEncounterPollAt)
            {
                _nextEncounterPollAt =
                    Time.unscaledTime + EncounterPollInterval;
                DiscoverEncounters();
                PollEncounterProgress();
            }

            if (!_hasRevealed && CanBeginInitialReveal())
            {
                _hasRevealed = true;
                _userWantsHidden = false;
                StartMainAnimation(FullRevealRoutine(true));
            }

            HandleToggleInput();
            UpdateRemainingEnemyMarkers();
        }

        private void OnDisable()
        {
            StopAnimations();
            UnsubscribeSources();
            UnsubscribeEncounters();
            SetEnemyLocatorMarkersVisible(0);
            _enemyFrameRenderers.Clear();
        }

        private void OnDestroy()
        {
            UnsubscribeSources();
            UnsubscribeEncounters();
            if (_enemyLocatorLayer != null)
            {
                Destroy(_enemyLocatorLayer.gameObject);
            }
        }

        private bool CanBeginInitialReveal()
        {
            return entrySequence != null &&
                   entrySequence.DeploymentStarted &&
                   AILURONEGameplayActionGate.AllowsGameplayActions &&
                   GameObject.Find(DeploymentBootOverlayName) == null &&
                   !_rewindInProgress &&
                   (playerHealth == null || !playerHealth.IsRewinding);
        }

        private void HandleToggleInput()
        {
            if (!_hasRevealed || Keyboard.current == null ||
                !Keyboard.current.qKey.wasPressedThisFrame ||
                !AILURONEGameplayActionGate.AllowsGameplayActions ||
                _rewindInProgress ||
                (playerHealth != null && playerHealth.IsRewinding))
            {
                return;
            }

            if (_temporaryRecall && _isVisible)
            {
                _temporaryRecall = false;
                _userWantsHidden = false;
                StopMainAnimation();
                SetVisibleImmediate();
                SetHint(true, "[Q] HIDE OBJECTIVES");
                return;
            }

            if (_isVisible)
            {
                _userWantsHidden = true;
                _temporaryRecall = false;
                AILURONEUIAudioFeedback.PlayGlobal(
                    revealSound,
                    revealVolume * 0.72f,
                    0.78f);
                StartMainAnimation(ArchiveHideRoutine());
                return;
            }

            _userWantsHidden = false;
            _temporaryRecall = false;
            StartMainAnimation(FullRevealRoutine(true));
        }

        private void ResolveSources(bool force)
        {
            GameManager resolvedGame = GameManager.Instance;
            if (resolvedGame == null)
            {
                resolvedGame = FindSceneComponent<GameManager>();
            }

            FinalGateManager resolvedGate =
                FindSceneComponent<FinalGateManager>();
            LevelEntrySequenceController resolvedEntry =
                FindSceneComponent<LevelEntrySequenceController>();
            PlayerHealth resolvedHealth = PlayerHealth.Instance != null
                ? PlayerHealth.Instance
                : FindSceneComponent<PlayerHealth>();

            bool changed = force ||
                           resolvedGame != gameManager ||
                           resolvedGate != finalGateManager ||
                           resolvedHealth != playerHealth;

            if (changed)
            {
                UnsubscribeSources();
                gameManager = resolvedGame;
                finalGateManager = resolvedGate;
                playerHealth = resolvedHealth;
                SubscribeSources();
                PullCurrentState();
            }

            entrySequence = resolvedEntry;
        }

        private T FindSceneComponent<T>() where T : Component
        {
            T[] candidates = FindObjectsByType<T>(
                FindObjectsInactive.Include);

            for (int index = 0; index < candidates.Length; index++)
            {
                T candidate = candidates[index];
                if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void SubscribeSources()
        {
            if (gameManager != null && !_gameSubscribed)
            {
                gameManager.CoreInventoryChanged += HandleInventoryChanged;
                _gameSubscribed = true;
            }

            if (finalGateManager != null && !_gateSubscribed)
            {
                finalGateManager.SocketProgressChanged += HandleSocketProgressChanged;
                finalGateManager.GateReleased += HandleGateReleased;
                _gateSubscribed = true;
            }

            if (playerHealth != null && !_healthSubscribed)
            {
                playerHealth.RewindStarted += HandleRewindStarted;
                playerHealth.RewindCompleted += HandleRewindCompleted;
                _healthSubscribed = true;
            }
        }

        private void UnsubscribeSources()
        {
            if (gameManager != null && _gameSubscribed)
            {
                gameManager.CoreInventoryChanged -= HandleInventoryChanged;
            }

            if (finalGateManager != null && _gateSubscribed)
            {
                finalGateManager.SocketProgressChanged -= HandleSocketProgressChanged;
                finalGateManager.GateReleased -= HandleGateReleased;
            }

            if (playerHealth != null && _healthSubscribed)
            {
                playerHealth.RewindStarted -= HandleRewindStarted;
                playerHealth.RewindCompleted -= HandleRewindCompleted;
            }

            _gameSubscribed = false;
            _gateSubscribed = false;
            _healthSubscribed = false;
        }

        private void PullCurrentState()
        {
            _carriedCount = gameManager != null
                ? Mathf.Max(0, gameManager.CurrentCoreCount)
                : 0;

            int gameRequirement = gameManager != null
                ? Mathf.Max(1, gameManager.TotalCoreRequirement)
                : 3;

            if (finalGateManager != null)
            {
                _requiredCount = Mathf.Max(
                    gameRequirement,
                    Mathf.Max(1, finalGateManager.TotalSocketsRequired));
                _installedCount = Mathf.Clamp(
                    finalGateManager.CurrentFilledSockets,
                    0,
                    _requiredCount);
            }
            else
            {
                _requiredCount = gameRequirement;
                _installedCount = 0;
            }

            _phase = ComputePhase();
            _displayedPhase = _phase;
        }

        private ObjectivePhase ComputePhase()
        {
            bool gateReleased = finalGateManager != null &&
                                finalGateManager.IsGateReleased;

            if (gateReleased || _installedCount >= _requiredCount)
            {
                return ObjectivePhase.EnterAperture;
            }

            int recovered = Mathf.Clamp(
                _carriedCount + _installedCount,
                0,
                _requiredCount);

            return recovered >= _requiredCount
                ? ObjectivePhase.Link
                : ObjectivePhase.Recover;
        }

        private int GetPhaseProgress(ObjectivePhase phase)
        {
            switch (phase)
            {
                case ObjectivePhase.Recover:
                    return Mathf.Clamp(
                        _carriedCount + _installedCount,
                        0,
                        _requiredCount);
                case ObjectivePhase.Link:
                    return Mathf.Clamp(
                        _installedCount,
                        0,
                        _requiredCount);
                default:
                    return 0;
            }
        }

        private void HandleInventoryChanged(int current, int configuredTotal)
        {
            ObjectivePhase oldPhase = _phase;
            int oldProgress = GetPhaseProgress(oldPhase);

            _carriedCount = Mathf.Max(0, current);
            _requiredCount = Mathf.Max(
                _requiredCount,
                Mathf.Max(1, configuredTotal));
            _phase = ComputePhase();

            HandleObjectiveStateChanged(oldPhase, oldProgress);
        }

        private void HandleSocketProgressChanged(int current, int required)
        {
            ObjectivePhase oldPhase = _phase;
            int oldProgress = GetPhaseProgress(oldPhase);

            _requiredCount = Mathf.Max(1, required);
            _installedCount = Mathf.Clamp(current, 0, _requiredCount);
            _phase = ComputePhase();

            HandleObjectiveStateChanged(oldPhase, oldProgress);
        }

        private void HandleGateReleased()
        {
            ObjectivePhase oldPhase = _phase;
            int oldProgress = GetPhaseProgress(oldPhase);

            _installedCount = Mathf.Max(_installedCount, _requiredCount);
            _phase = ObjectivePhase.EnterAperture;

            HandleObjectiveStateChanged(oldPhase, oldProgress);
        }

        private void HandleObjectiveStateChanged(
            ObjectivePhase oldPhase,
            int oldProgress)
        {
            int newProgress = GetPhaseProgress(_phase);
            bool phaseChanged = oldPhase != _phase;
            bool progressChanged = oldProgress != newProgress;

            if (!_hasRevealed)
            {
                _displayedPhase = _phase;
                RenderMainState();
                return;
            }

            if (phaseChanged &&
                ShouldAnimateCompletedPhase(oldPhase) &&
                _isVisible &&
                !_userWantsHidden)
            {
                StartMainAnimation(StageTransitionRoutine());
                return;
            }

            _displayedPhase = _phase;
            RenderMainState();

            if (phaseChanged || progressChanged)
            {
                RequestProgressFeedback(newProgress - 1);
            }
        }

        private bool ShouldAnimateCompletedPhase(ObjectivePhase oldPhase)
        {
            if (oldPhase == ObjectivePhase.Recover &&
                _phase == ObjectivePhase.Link)
            {
                return _carriedCount + _installedCount >= _requiredCount;
            }

            if (oldPhase == ObjectivePhase.Link &&
                _phase == ObjectivePhase.EnterAperture)
            {
                bool gateReleased = finalGateManager != null &&
                                    finalGateManager.IsGateReleased;
                return gateReleased || _installedCount >= _requiredCount;
            }

            return false;
        }

        private void RequestProgressFeedback(int slotIndex)
        {
            if (!_hasRevealed || _rewindInProgress)
            {
                return;
            }

            if (_userWantsHidden && !_isVisible)
            {
                _temporaryRecall = true;
                StartMainAnimation(UrgentRecallRoutine());
                return;
            }

            if (_isVisible)
            {
                StartFeedbackAnimation(slotIndex);
            }
        }

        private void HandleRewindStarted()
        {
            _rewindInProgress = true;
            StopAnimations();
            SetHiddenImmediate(false);
        }

        private void HandleRewindCompleted()
        {
            _rewindInProgress = false;
            _userWantsHidden = false;
            _temporaryRecall = false;
            _hasRevealed = true;

            PullCurrentState();
            RenderMainState();
            RefreshSubtasks(false);
            StartMainAnimation(FullRevealRoutine(true));
        }

        private void DiscoverEncounters()
        {
            CombatEncounter[] found = FindObjectsByType<CombatEncounter>(
                FindObjectsInactive.Include);

            for (int index = 0; index < found.Length; index++)
            {
                CombatEncounter encounter = found[index];
                if (encounter == null ||
                    encounter.gameObject.scene != gameObject.scene ||
                    !IsTrackedSubtaskEncounter(encounter) ||
                    _encounters.Contains(encounter))
                {
                    continue;
                }

                _encounters.Add(encounter);
                encounter.EncounterActivated += HandleEncounterActivated;
                encounter.EncounterSuspended += HandleEncounterSuspended;
                encounter.EncounterCleared += HandleEncounterCleared;
                encounter.EncounterReset += HandleEncounterReset;
                _lastRemaining[encounter] =
                    encounter.RemainingRequiredMemberCount;

                if ((encounter.State == CombatEncounterState.Active ||
                     encounter.State == CombatEncounterState.PendingExit) &&
                    encounter.RemainingRequiredMemberCount > 0)
                {
                    AddActiveEncounter(encounter);
                }
            }
        }

        private bool IsTrackedSubtaskEncounter(CombatEncounter encounter)
        {
            if (encounter == clearRoomEncounter)
            {
                return true;
            }

            if (additionalSubtaskEncounters == null)
            {
                return false;
            }

            for (int index = 0;
                 index < additionalSubtaskEncounters.Length;
                 index++)
            {
                if (additionalSubtaskEncounters[index] == encounter)
                {
                    return true;
                }
            }

            return false;
        }

        private void UnsubscribeEncounters()
        {
            for (int index = 0; index < _encounters.Count; index++)
            {
                CombatEncounter encounter = _encounters[index];
                if (encounter == null)
                {
                    continue;
                }

                encounter.EncounterActivated -= HandleEncounterActivated;
                encounter.EncounterSuspended -= HandleEncounterSuspended;
                encounter.EncounterCleared -= HandleEncounterCleared;
                encounter.EncounterReset -= HandleEncounterReset;
            }

            _encounters.Clear();
            _activeEncounters.Clear();
            _lastRemaining.Clear();
        }

        private void PollEncounterProgress()
        {
            bool changed = false;

            for (int index = _activeEncounters.Count - 1; index >= 0; index--)
            {
                CombatEncounter encounter = _activeEncounters[index];
                if (encounter == null)
                {
                    _activeEncounters.RemoveAt(index);
                    changed = true;
                    continue;
                }

                int remaining = encounter.RemainingRequiredMemberCount;
                bool isActive =
                    encounter.State == CombatEncounterState.Active ||
                    encounter.State == CombatEncounterState.PendingExit;
                if (!isActive || remaining <= 0)
                {
                    _activeEncounters.RemoveAt(index);
                    changed = true;
                    continue;
                }

                int previous = _lastRemaining.TryGetValue(encounter, out int value)
                    ? value
                    : remaining;

                if (remaining != previous)
                {
                    _lastRemaining[encounter] = remaining;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            RefreshSubtasks(true);
            RequestSubtaskRecallIfHidden();
        }

        private void HandleEncounterActivated(CombatEncounter encounter)
        {
            if (encounter == null || encounter.RemainingRequiredMemberCount <= 0)
            {
                return;
            }

            AddActiveEncounter(encounter);
            RefreshSubtasks(true);
            RequestSubtaskRecallIfHidden();
        }

        private void HandleEncounterSuspended(CombatEncounter encounter)
        {
            if (_activeEncounters.Remove(encounter))
            {
                RefreshSubtasks(true);
            }
        }

        private void HandleEncounterCleared(CombatEncounter encounter)
        {
            if (encounter != null)
            {
                _lastRemaining[encounter] = 0;
                _activeEncounters.Remove(encounter);
            }

            AILURONEUIAudioFeedback.PlayGlobal(
                completionSound,
                completionVolume * 0.72f);
            RefreshSubtasks(false);
            if (encounter == clearRoomEncounter)
            {
                TriggerClearRoomGateTear();
            }
        }

        private void TriggerClearRoomGateTear()
        {
            if (_clearRoomGateTearTriggered || clearRoomGate == null ||
                !clearRoomGate.gameObject.activeInHierarchy)
            {
                return;
            }

            _clearRoomGateTearTriggered = true;
            clearRoomGate.OpenDoor();
        }

        private void HandleEncounterReset(CombatEncounter encounter)
        {
            _activeEncounters.Remove(encounter);
            if (encounter != null)
            {
                _lastRemaining[encounter] =
                    encounter.RemainingRequiredMemberCount;
            }

            RefreshSubtasks(false);
        }

        private void AddActiveEncounter(CombatEncounter encounter)
        {
            if (encounter == null ||
                encounter.RemainingRequiredMemberCount <= 0 ||
                (encounter.State != CombatEncounterState.Active &&
                 encounter.State != CombatEncounterState.PendingExit) ||
                _activeEncounters.Contains(encounter))
            {
                return;
            }

            _activeEncounters.Add(encounter);
            _lastRemaining[encounter] =
                encounter.RemainingRequiredMemberCount;
        }

        private void RequestSubtaskRecallIfHidden()
        {
            if (!_hasRevealed || !_userWantsHidden || _isVisible)
            {
                return;
            }

            _temporaryRecall = true;
            StartMainAnimation(UrgentRecallRoutine());
        }

        private void RefreshSubtasks(bool highlightChange)
        {
#if UNITY_EDITOR
            if (_editorSimulationActive)
            {
                RenderEditorSubtask(highlightChange);
                return;
            }
#endif
            for (int row = 0; row < MaximumVisibleSubtasks; row++)
            {
                bool hasEncounter = row < _activeEncounters.Count &&
                                    _activeEncounters[row] != null;

                if (row < subtaskRoots.Length && subtaskRoots[row] != null)
                {
                    subtaskRoots[row].gameObject.SetActive(hasEncounter);
                }

                if (!hasEncounter)
                {
                    continue;
                }

                CombatEncounter encounter = _activeEncounters[row];
                int remaining = encounter.RemainingRequiredMemberCount;
                bool complete = remaining <= 0 || encounter.IsCleared;

                if (row < subtaskNames.Length && subtaskNames[row] != null)
                {
                    subtaskNames[row].text = "CLEAR THE ROOM";
                    subtaskNames[row].color = complete ? cyan : mutedWhite;
                }

                if (row < subtaskCounts.Length && subtaskCounts[row] != null)
                {
                    subtaskCounts[row].text =
                        $"{Mathf.Max(0, remaining):00} REMAINING";
                    subtaskCounts[row].color = cyan;
                }

                if (row < subtaskLines.Length && subtaskLines[row] != null)
                {
                    subtaskLines[row].color = cyan;
                }

                if (row < subtaskGroups.Length && subtaskGroups[row] != null &&
                    !_isVisible)
                {
                    subtaskGroups[row].alpha = 0f;
                }
                else if (row < subtaskGroups.Length && subtaskGroups[row] != null &&
                         highlightChange)
                {
                    subtaskGroups[row].alpha = 1f;
                }
            }
        }

#if UNITY_EDITOR
        private void RenderEditorSubtask(bool highlightChange)
        {
            for (int row = 0; row < MaximumVisibleSubtasks; row++)
            {
                bool visible = _editorSubtaskVisible[row];
                if (row < subtaskRoots.Length && subtaskRoots[row] != null)
                {
                    subtaskRoots[row].gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                if (row < subtaskNames.Length && subtaskNames[row] != null)
                {
                    subtaskNames[row].text = "CLEAR THE ROOM";
                    subtaskNames[row].color =
                        _editorSubtaskRemaining[row] == 0
                        ? cyan
                        : mutedWhite;
                }

                if (row < subtaskCounts.Length && subtaskCounts[row] != null)
                {
                    subtaskCounts[row].text =
                        $"{_editorSubtaskRemaining[row]:00} REMAINING";
                    subtaskCounts[row].color = cyan;
                }

                if (row < subtaskLines.Length && subtaskLines[row] != null)
                {
                    subtaskLines[row].color = cyan;
                }

                if (row < subtaskGroups.Length && subtaskGroups[row] != null)
                {
                    subtaskGroups[row].alpha = _isVisible || highlightChange
                        ? 1f
                        : 0f;
                }
            }
        }
#endif

        private void UpdateRemainingEnemyMarkers()
        {
            bool encounterActive = clearRoomEncounter != null &&
                (clearRoomEncounter.State == CombatEncounterState.Active ||
                 clearRoomEncounter.State == CombatEncounterState.PendingExit);

            if (!encounterActive || !_isVisible || _rewindInProgress ||
                clearRoomRequiredEnemies == null)
            {
                SetEnemyLocatorMarkersVisible(0);
                return;
            }

            _remainingClearRoomEnemies.Clear();
            for (int index = 0;
                 index < clearRoomRequiredEnemies.Length;
                 index++)
            {
                EnemyTarget enemy = clearRoomRequiredEnemies[index];
                if (enemy != null && !enemy.IsDead)
                {
                    _remainingClearRoomEnemies.Add(enemy);
                }
            }

            int remaining = _remainingClearRoomEnemies.Count;
            if (remaining == 0 || !EnsureEnemyLocatorLayer())
            {
                SetEnemyLocatorMarkersVisible(0);
                return;
            }

            EnsureEnemyLocatorMarkerCount(remaining);
            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                SetEnemyLocatorMarkersVisible(0);
                return;
            }

            Rect canvasRect = _enemyLocatorLayer.rect;
            float edgePadding = 18f;
            float baseAlpha = remaining <= 5 ? 0.88f : 0.68f;
            float pulse = remaining <= 5
                ? 0.94f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.06f
                : 1f;

            for (int index = 0; index < remaining; index++)
            {
                EnemyLocatorMarker marker = _enemyLocatorMarkers[index];
                EnemyTarget enemy = _remainingClearRoomEnemies[index];
                Bounds worldBounds = GetEnemyWorldBounds(enemy);
                Vector3 screenPoint = worldCamera.WorldToScreenPoint(
                    worldBounds.center);
                float frameDiameter = GetProjectedFrameDiameter(
                    worldCamera,
                    worldBounds,
                    canvasRect);

                bool behindCamera = screenPoint.z <= 0f;
                if (behindCamera)
                {
                    screenPoint.x = Screen.width - screenPoint.x;
                    screenPoint.y = Screen.height - screenPoint.y;
                }

                float normalizedX = Screen.width > 0
                    ? screenPoint.x / Screen.width
                    : 0.5f;
                float normalizedY = Screen.height > 0
                    ? screenPoint.y / Screen.height
                    : 0.5f;
                Vector2 unclamped = new Vector2(
                    (normalizedX - 0.5f) * canvasRect.width,
                    (normalizedY - 0.5f) * canvasRect.height);
                Vector2 clamped = new Vector2(
                    Mathf.Clamp(unclamped.x,
                        canvasRect.xMin + edgePadding,
                        canvasRect.xMax - edgePadding),
                    Mathf.Clamp(unclamped.y,
                        canvasRect.yMin + edgePadding,
                        canvasRect.yMax - edgePadding));
                bool edgeMarker = behindCamera ||
                    (clamped - unclamped).sqrMagnitude > 0.01f;

                marker.root.anchoredPosition = clamped;
                marker.root.localEulerAngles = Vector3.zero;
                UpdateEnemyFrameGeometry(
                    marker,
                    edgeMarker ? 22f : frameDiameter);
                marker.group.alpha = baseAlpha *
                    (edgeMarker ? 0.82f : 1f) * pulse;
                marker.root.gameObject.SetActive(true);
            }

            SetEnemyLocatorMarkersVisible(remaining);
        }

        private Bounds GetEnemyWorldBounds(EnemyTarget enemy)
        {
            if (!_enemyFrameRenderers.TryGetValue(
                    enemy,
                    out Renderer[] renderers))
            {
                renderers = enemy.GetComponentsInChildren<Renderer>(true);
                _enemyFrameRenderers[enemy] = renderers;
            }
            Bounds bounds = new Bounds(
                enemy.transform.position + Vector3.up,
                new Vector3(0.8f, 1.8f, 0.8f));
            bool hasVisualBounds = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer rendererItem = renderers[index];
                if (rendererItem == null ||
                    rendererItem is ParticleSystemRenderer ||
                    rendererItem is TrailRenderer ||
                    rendererItem is LineRenderer ||
                    !rendererItem.enabled)
                {
                    continue;
                }
                if (!hasVisualBounds)
                {
                    bounds = rendererItem.bounds;
                    hasVisualBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererItem.bounds);
                }
            }
            return bounds;
        }

        private float GetProjectedFrameDiameter(
            Camera worldCamera,
            Bounds bounds,
            Rect canvasRect)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            bool hasPoint = false;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                Vector3 screen = worldCamera.WorldToScreenPoint(point);
                if (screen.z <= 0f)
                {
                    continue;
                }
                hasPoint = true;
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxY = Mathf.Max(maxY, screen.y);
            }

            if (!hasPoint || Screen.width <= 0 || Screen.height <= 0)
            {
                return 32f;
            }

            float width = (maxX - minX) / Screen.width * canvasRect.width;
            float height = (maxY - minY) / Screen.height * canvasRect.height;
            return Mathf.Clamp((width + height) * 1.04f, 26f, 180f);
        }

        private bool EnsureEnemyLocatorLayer()
        {
            if (_enemyLocatorLayer != null && _enemyLocatorCanvas != null)
            {
                return true;
            }

            _enemyLocatorCanvas = presentationGroup != null
                ? presentationGroup.GetComponentInParent<Canvas>()
                : null;
            if (_enemyLocatorCanvas == null)
            {
                return false;
            }

            GameObject layerObject = new GameObject(
                "ClearRoomEnemyLocatorLayer",
                typeof(RectTransform));
            layerObject.layer = _enemyLocatorCanvas.gameObject.layer;
            _enemyLocatorLayer = layerObject.GetComponent<RectTransform>();
            _enemyLocatorLayer.SetParent(_enemyLocatorCanvas.transform, false);
            _enemyLocatorLayer.anchorMin = Vector2.zero;
            _enemyLocatorLayer.anchorMax = Vector2.one;
            _enemyLocatorLayer.offsetMin = Vector2.zero;
            _enemyLocatorLayer.offsetMax = Vector2.zero;
            _enemyLocatorLayer.SetAsLastSibling();
            return true;
        }

        private void EnsureEnemyLocatorMarkerCount(int count)
        {
            while (_enemyLocatorMarkers.Count < count)
            {
                int markerIndex = _enemyLocatorMarkers.Count;
                GameObject markerObject = new GameObject(
                    "EnemyLocator_" + (markerIndex + 1).ToString("00"),
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                markerObject.layer = _enemyLocatorLayer.gameObject.layer;

                RectTransform markerRoot =
                    markerObject.GetComponent<RectTransform>();
                markerRoot.SetParent(_enemyLocatorLayer, false);
                markerRoot.anchorMin = new Vector2(0.5f, 0.5f);
                markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
                markerRoot.pivot = new Vector2(0.5f, 0.5f);
                markerRoot.sizeDelta = new Vector2(22f, 22f);

                CanvasGroup markerGroup =
                    markerObject.GetComponent<CanvasGroup>();
                markerGroup.interactable = false;
                markerGroup.blocksRaycasts = false;

                RectTransform[] edges = new RectTransform[4];
                for (int edge = 0; edge < edges.Length; edge++)
                {
                    edges[edge] = CreateEnemyLocatorEdge(markerRoot);
                }

                EnemyLocatorMarker marker = new EnemyLocatorMarker
                {
                    root = markerRoot,
                    group = markerGroup,
                    edges = edges
                };
                UpdateEnemyFrameGeometry(marker, 22f);
                _enemyLocatorMarkers.Add(marker);
            }
        }

        private RectTransform CreateEnemyLocatorEdge(RectTransform parent)
        {
            GameObject edgeObject = new GameObject(
                "Edge",
                typeof(RectTransform),
                typeof(Image));
            edgeObject.layer = parent.gameObject.layer;
            RectTransform edge = edgeObject.GetComponent<RectTransform>();
            edge.SetParent(parent, false);
            edge.anchorMin = new Vector2(0.5f, 0.5f);
            edge.anchorMax = new Vector2(0.5f, 0.5f);
            edge.pivot = new Vector2(0.5f, 0.5f);

            Image image = edgeObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.96f);
            image.raycastTarget = false;
            return edge;
        }

        private void UpdateEnemyFrameGeometry(
            EnemyLocatorMarker marker,
            float diameter)
        {
            if (marker == null || marker.root == null ||
                marker.edges == null || marker.edges.Length < 4)
            {
                return;
            }

            float radius = Mathf.Max(10f, diameter * 0.5f);
            float halfRadius = radius * 0.5f;
            float edgeLength = radius * 1.41421356f;
            marker.root.sizeDelta = new Vector2(diameter, diameter);

            SetEnemyFrameEdge(marker.edges[0],
                new Vector2(halfRadius, halfRadius), edgeLength, -45f);
            SetEnemyFrameEdge(marker.edges[1],
                new Vector2(halfRadius, -halfRadius), edgeLength, 45f);
            SetEnemyFrameEdge(marker.edges[2],
                new Vector2(-halfRadius, -halfRadius), edgeLength, -45f);
            SetEnemyFrameEdge(marker.edges[3],
                new Vector2(-halfRadius, halfRadius), edgeLength, 45f);
        }

        private static void SetEnemyFrameEdge(
            RectTransform edge,
            Vector2 position,
            float length,
            float rotation)
        {
            edge.anchoredPosition = position;
            edge.sizeDelta = new Vector2(length, 1.35f);
            edge.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        private void SetEnemyLocatorMarkersVisible(int visibleCount)
        {
            for (int index = 0; index < _enemyLocatorMarkers.Count; index++)
            {
                if (_enemyLocatorMarkers[index].root != null)
                {
                    _enemyLocatorMarkers[index].root.gameObject.SetActive(
                        index < visibleCount);
                }
            }
        }

        private void RenderMainState()
        {
            if (headerText != null)
            {
                headerText.text = "OBJECTIVE";
                headerText.color = cyan;
                headerText.maxVisibleCharacters = int.MaxValue;
            }

            if (objectiveText != null)
            {
                switch (_displayedPhase)
                {
                    case ObjectivePhase.Recover:
                        objectiveText.text = "RECOVER REWRITE NODES";
                        break;
                    case ObjectivePhase.Link:
                        objectiveText.text = "LINK REWRITE NODES";
                        break;
                    default:
                        objectiveText.text = "ENTER THE CONVERGENCE APERTURE";
                        break;
                }

                objectiveText.color = primaryWhite;
            }

            bool showSlots = _displayedPhase != ObjectivePhase.EnterAperture;
            int progress = GetPhaseProgress(_displayedPhase);

            if (slotsRoot != null)
            {
                slotsRoot.gameObject.SetActive(showSlots);
            }

            for (int index = 0; index < slotRoots.Length; index++)
            {
                if (slotRoots[index] != null)
                {
                    slotRoots[index].localScale = Vector3.one;
                }

                if (index < slotFills.Length && slotFills[index] != null)
                {
                    slotFills[index].gameObject.SetActive(showSlots && index < progress);
                    slotFills[index].color = cyan;
                }
            }
        }

        private IEnumerator FullRevealRoutine(bool playSound)
        {
            _isVisible = true;
            _temporaryRecall = false;
            _displayedPhase = _phase;
            RenderMainState();
            RefreshSubtasks(false);

            presentationGroup.alpha = 1f;
            diamondGroup.alpha = 0f;
            lineGroup.alpha = 0f;
            headerGroup.alpha = 1f;
            objectiveGroup.alpha = 0f;
            slotsGroup.alpha = 0f;
            strikeGroup.alpha = 0f;

            diamondRoot.localScale = Vector3.one * 0.62f;
            diamondRoot.localEulerAngles = new Vector3(0f, 0f, -70f);
            verticalLine.localScale = new Vector3(1f, 0f, 1f);
            headerText.maxVisibleCharacters = 0;
            SetHint(false, string.Empty);
            SetSubtaskAlpha(0f);

            if (playSound)
            {
                AILURONEUIAudioFeedback.PlayGlobal(revealSound, revealVolume);
            }

            yield return AnimateDiamondIn(0.18f);
            yield return AnimateLineIn(0.16f);
            yield return TypeHeader(0.035f);
            yield return FadeGroup(objectiveGroup, 0f, 1f, 0.16f);

            if (_displayedPhase != ObjectivePhase.EnterAperture)
            {
                yield return FadeGroup(slotsGroup, 0f, 1f, 0.12f);
            }

            yield return RevealSubtasks();
            SetHint(true, "[Q] HIDE OBJECTIVES");
        }

        private IEnumerator UrgentRecallRoutine()
        {
            _isVisible = true;
            _temporaryRecall = true;
            _displayedPhase = _phase;
            RenderMainState();
            RefreshSubtasks(false);

            presentationGroup.alpha = 1f;
            diamondGroup.alpha = 0f;
            lineGroup.alpha = 0f;
            headerGroup.alpha = 0f;
            objectiveGroup.alpha = 0f;
            slotsGroup.alpha = 0f;
            strikeGroup.alpha = 0f;
            diamondRoot.localScale = Vector3.one * 0.72f;
            diamondRoot.localEulerAngles = new Vector3(0f, 0f, -80f);
            verticalLine.localScale = new Vector3(1f, 0f, 1f);
            headerText.maxVisibleCharacters = int.MaxValue;
            SetHint(false, string.Empty);
            SetSubtaskAlpha(0f);

            AILURONEUIAudioFeedback.PlayGlobal(progressSound, progressVolume);

            yield return AnimateDiamondIn(0.14f);
            yield return AnimateLineIn(0.09f);
            yield return FadeGroup(headerGroup, 0f, 1f, 0.08f);
            yield return FadeGroup(objectiveGroup, 0f, 1f, 0.1f);

            if (_displayedPhase != ObjectivePhase.EnterAperture)
            {
                slotsGroup.alpha = 1f;
            }

            SetSubtaskAlpha(1f);
            SetHint(true, "[Q] HIDE OBJECTIVES");

            yield return WaitUnpaused(HiddenRecallHold);

            if (_temporaryRecall && _userWantsHidden)
            {
                yield return ArchiveHideRoutine();
            }
        }

        private IEnumerator ArchiveHideRoutine()
        {
            _temporaryRecall = false;

            float duration = 0.14f;
            float elapsed = 0f;
            Vector2 startPosition = objectiveRoot.anchoredPosition;
            Vector2 endPosition = startPosition + new Vector2(18f, 0f);

            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                objectiveGroup.alpha = 1f - eased;
                slotsGroup.alpha = 1f - eased;
                objectiveRoot.anchoredPosition = Vector2.Lerp(
                    startPosition,
                    endPosition,
                    eased);
                SetSubtaskAlpha(1f - eased);
                yield return null;
            }

            objectiveRoot.anchoredPosition = startPosition;
            headerGroup.alpha = 0f;

            yield return AnimateLineOut(0.1f);
            yield return AnimateDiamondOut(0.12f);

            presentationGroup.alpha = 0f;
            _isVisible = false;
            SetHint(true, "[Q] SHOW OBJECTIVES");
        }

        private IEnumerator StageTransitionRoutine()
        {
            AILURONEUIAudioFeedback.PlayGlobal(
                completionSound,
                completionVolume);

            yield return FadeGroup(slotsGroup, slotsGroup.alpha, 0f, 0.12f);

            strikeGroup.alpha = 1f;
            completionStrike.localScale = new Vector3(0f, 1f, 1f);
            yield return AnimateScaleX(completionStrike, 0f, 1f, 0.22f);
            yield return WaitUnpaused(0.18f);

            float duration = 0.14f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = Mathf.Clamp01(elapsed / duration);
                objectiveGroup.alpha = 1f - t;
                strikeGroup.alpha = 1f - t;
                yield return null;
            }

            _displayedPhase = _phase;
            RenderMainState();
            objectiveGroup.alpha = 0f;
            strikeGroup.alpha = 0f;
            completionStrike.localScale = Vector3.one;

            yield return FadeGroup(objectiveGroup, 0f, 1f, 0.16f);
            if (_displayedPhase != ObjectivePhase.EnterAperture)
            {
                yield return FadeGroup(slotsGroup, 0f, 1f, 0.12f);
            }

            StartFeedbackAnimation(GetPhaseProgress(_displayedPhase) - 1);
        }

        private void StartFeedbackAnimation(int slotIndex)
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
            }

            _feedbackRoutine = StartCoroutine(FeedbackRoutine(slotIndex));
        }

        private IEnumerator FeedbackRoutine(int slotIndex)
        {
            AILURONEUIAudioFeedback.PlayGlobal(progressSound, progressVolume);

            Vector3 startRotation = diamondRoot.localEulerAngles;
            float duration = 0.32f;
            float elapsed = 0f;
            RectTransform pulseSlot = slotIndex >= 0 && slotIndex < slotRoots.Length
                ? slotRoots[slotIndex]
                : null;

            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = Mathf.Clamp01(elapsed / duration);
                float envelope = Mathf.Sin(t * Mathf.PI);
                diamondRoot.localEulerAngles = startRotation +
                    new Vector3(0f, 0f, 360f * t);

                for (int index = 0; index < diamondGraphics.Length; index++)
                {
                    if (diamondGraphics[index] != null)
                    {
                        diamondGraphics[index].color = Color.Lerp(
                            primaryWhite,
                            cyan,
                            envelope);
                    }
                }

                if (pulseSlot != null)
                {
                    pulseSlot.localScale =
                        Vector3.one * (1f + envelope * 0.16f);
                }

                yield return null;
            }

            diamondRoot.localEulerAngles = startRotation;
            for (int index = 0; index < diamondGraphics.Length; index++)
            {
                if (diamondGraphics[index] != null)
                {
                    diamondGraphics[index].color = primaryWhite;
                }
            }

            if (pulseSlot != null)
            {
                pulseSlot.localScale = Vector3.one;
            }

            _feedbackRoutine = null;
        }

        private IEnumerator AnimateDiamondIn(float duration)
        {
            Vector3 startScale = diamondRoot.localScale;
            float startRotation = diamondRoot.localEulerAngles.z;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                diamondGroup.alpha = t;
                diamondRoot.localScale = Vector3.Lerp(
                    startScale,
                    Vector3.one,
                    t);
                diamondRoot.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    Mathf.LerpAngle(startRotation, 0f, t));
                yield return null;
            }

            diamondGroup.alpha = 1f;
            diamondRoot.localScale = Vector3.one;
            diamondRoot.localEulerAngles = Vector3.zero;
        }

        private IEnumerator AnimateDiamondOut(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / duration));
                diamondGroup.alpha = 1f - t;
                diamondRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, t);
                diamondRoot.localEulerAngles = new Vector3(0f, 0f, -90f * t);
                yield return null;
            }

            diamondGroup.alpha = 0f;
        }

        private IEnumerator AnimateLineIn(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / duration));
                lineGroup.alpha = t;
                verticalLine.localScale = new Vector3(1f, t, 1f);
                yield return null;
            }

            lineGroup.alpha = 1f;
            verticalLine.localScale = Vector3.one;
        }

        private IEnumerator AnimateLineOut(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / duration));
                lineGroup.alpha = 1f - t;
                verticalLine.localScale = new Vector3(1f, 1f - t, 1f);
                yield return null;
            }

            lineGroup.alpha = 0f;
            verticalLine.localScale = new Vector3(1f, 0f, 1f);
        }

        private IEnumerator TypeHeader(float characterDelay)
        {
            headerText.ForceMeshUpdate();
            int count = headerText.textInfo.characterCount;

            for (int index = 0; index <= count; index++)
            {
                headerText.maxVisibleCharacters = index;
                yield return WaitUnpaused(characterDelay);
            }

            headerText.maxVisibleCharacters = int.MaxValue;
        }

        private IEnumerator RevealSubtasks()
        {
            for (int index = 0; index < subtaskGroups.Length; index++)
            {
                if (index >= subtaskRoots.Length || subtaskRoots[index] == null ||
                    !subtaskRoots[index].gameObject.activeSelf ||
                    subtaskGroups[index] == null)
                {
                    continue;
                }

                yield return FadeGroup(subtaskGroups[index], 0f, 1f, 0.08f);
            }
        }

        private IEnumerator FadeGroup(
            CanvasGroup group,
            float from,
            float to,
            float duration)
        {
            if (group == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            group.alpha = to;
        }

        private IEnumerator AnimateScaleX(
            RectTransform rect,
            float from,
            float to,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                rect.localScale = new Vector3(Mathf.Lerp(from, to, t), 1f, 1f);
                yield return null;
            }

            rect.localScale = new Vector3(to, 1f, 1f);
        }

        private IEnumerator WaitUnpaused(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!AILURONEGameplayActionGate.IsPaused)
                {
                    elapsed += GetPresentationDeltaTime();
                }

                yield return null;
            }
        }

        private float GetPresentationDeltaTime()
        {
#if UNITY_EDITOR
            return Time.unscaledDeltaTime *
                (_editorSimulationActive ? _editorAnimationSpeed : 1f);
#else
            return Time.unscaledDeltaTime;
#endif
        }

        private void StartMainAnimation(IEnumerator routine)
        {
            StopMainAnimation();
            _mainRoutine = StartCoroutine(RunMainAnimation(routine));
        }

        private IEnumerator RunMainAnimation(IEnumerator routine)
        {
            yield return routine;
            _mainRoutine = null;
        }

        private void StopMainAnimation()
        {
            if (_mainRoutine == null)
            {
                return;
            }

            StopCoroutine(_mainRoutine);
            _mainRoutine = null;
        }

        private void StopAnimations()
        {
            StopMainAnimation();
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }
        }

        private void SetHiddenImmediate(bool showHint)
        {
            presentationGroup.alpha = 0f;
            diamondGroup.alpha = 0f;
            lineGroup.alpha = 0f;
            headerGroup.alpha = 0f;
            objectiveGroup.alpha = 0f;
            slotsGroup.alpha = 0f;
            strikeGroup.alpha = 0f;
            _isVisible = false;
            SetSubtaskAlpha(0f);
            SetHint(showHint, showHint ? "[Q] SHOW OBJECTIVES" : string.Empty);
        }

        private void SetVisibleImmediate()
        {
            _displayedPhase = _phase;
            RenderMainState();
            RefreshSubtasks(false);
            presentationGroup.alpha = 1f;
            diamondGroup.alpha = 1f;
            lineGroup.alpha = 1f;
            headerGroup.alpha = 1f;
            objectiveGroup.alpha = 1f;
            slotsGroup.alpha = _displayedPhase == ObjectivePhase.EnterAperture
                ? 0f
                : 1f;
            strikeGroup.alpha = 0f;
            diamondRoot.localScale = Vector3.one;
            diamondRoot.localEulerAngles = Vector3.zero;
            verticalLine.localScale = Vector3.one;
            headerText.maxVisibleCharacters = int.MaxValue;
            SetSubtaskAlpha(1f);
            _isVisible = true;
        }

        private void SetSubtaskAlpha(float alpha)
        {
            for (int index = 0; index < subtaskGroups.Length; index++)
            {
                if (subtaskGroups[index] != null)
                {
                    subtaskGroups[index].alpha = alpha;
                }
            }
        }

        private void SetHint(bool visible, string content)
        {
            if (hintText != null)
            {
                hintText.text = content;
            }

            if (hintGroup != null)
            {
                hintGroup.alpha = visible ? 1f : 0f;
            }
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }
    }
}
