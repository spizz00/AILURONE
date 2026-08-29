#pragma warning disable 0618
#pragma warning disable 0414
using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatEncounterState
{
    Dormant,
    Active,
    PendingExit,
    Suspended,
    Cleared
}

/// <summary>
/// Logical controller for one combat encounter.
///
/// Key rules:
/// - Any Activation Volume can start or resume the encounter.
/// - Activation and Retention Volumes can both keep an active encounter alive.
/// - Leaving all volumes starts a short exit grace period.
/// - Re-entering during the grace period cancels the exit without restarting AI.
/// - Retention Volumes never activate a dormant encounter by themselves.
/// - Enemy health, death state and world position are not reset here.
/// - Rewind clears stale trigger records and rebuilds them from the player's
///   actual post-rewind position.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatEncounter : MonoBehaviour
{
    [Header("Exit Behaviour")]
    [Min(0f)]
    [SerializeField]
    private float exitGraceTime = 0.45f;

    [Range(0.10f, 1f)]
    [SerializeField]
    private float reconciliationInterval = 0.25f;

    [Header("Automatic Registration")]
    [SerializeField]
    private bool autoCollectChildVolumes = true;

    [SerializeField]
    private bool autoCollectChildMembers = true;

    [Header("Runtime State")]
    [SerializeField]
    private CombatEncounterState currentState =
        CombatEncounterState.Dormant;

    [SerializeField]
    private Transform player;

    [SerializeField]
    private int occupiedActivationVolumeCount;

    [SerializeField]
    private int occupiedRetentionVolumeCount;

    [SerializeField]
    private int requiredMemberCount;

    [SerializeField]
    private int defeatedRequiredMemberCount;

    [SerializeField]
    private bool rewindSuspended;

    private readonly HashSet<CombatEncounterVolume> _volumes =
        new HashSet<CombatEncounterVolume>();

    private readonly HashSet<CombatEncounterVolume> _occupiedActivationVolumes =
        new HashSet<CombatEncounterVolume>();

    private readonly HashSet<CombatEncounterVolume> _occupiedRetentionVolumes =
        new HashSet<CombatEncounterVolume>();

    private readonly HashSet<CombatEncounterMember> _members =
        new HashSet<CombatEncounterMember>();

    private readonly HashSet<CombatEncounterMember> _defeatedRequiredMembers =
        new HashSet<CombatEncounterMember>();

    private PlayerHealth _playerHealth;
    private float _nextReconciliationTime;
    private float _exitDeadline;
    private float _nextPlayerSearchTime;
    private bool _hasEverActivated;
    private bool _wasCombatAllowedBeforeRewind;
    private bool _runtimeInitialized;

    public CombatEncounterState State => currentState;

    /// <summary>
    /// True during Active and PendingExit. Existing enemies should continue
    /// fighting during the grace period and only suspend after it expires.
    /// </summary>
    public bool AllowsCombat =>
        currentState == CombatEncounterState.Active ||
        currentState == CombatEncounterState.PendingExit;

    public bool IsCleared =>
        currentState == CombatEncounterState.Cleared;

    public bool IsRewindSuspended => rewindSuspended;
    public Transform Player => AllowsCombat ? player : null;
    public Transform TrackedPlayer => player;
    public float ExitGraceTime => exitGraceTime;
    public int RequiredMemberCount => requiredMemberCount;
    public int RemainingRequiredMemberCount =>
        Mathf.Max(
            0,
            requiredMemberCount - defeatedRequiredMemberCount
        );

    public event Action<CombatEncounter> EncounterActivated;
    public event Action<CombatEncounter> EncounterSuspended;
    public event Action<CombatEncounter> EncounterCleared;
    public event Action<CombatEncounter> EncounterReset;

    private void Awake()
    {
        EnsureRuntimeInitialized();

        if (autoCollectChildVolumes)
        {
            CollectChildVolumes();
        }

        if (autoCollectChildMembers)
        {
            CollectChildMembers();
        }
    }

    private void OnEnable()
    {
        EnsureRuntimeInitialized();
        ClearOccupancyRecords();
        _nextPlayerSearchTime = 0f;
        BindPlayerHealth();

        // OnDisable can occur in the middle of rewind. In that case this
        // component misses RewindCompleted while disabled, so rebuild the flag
        // from the authoritative PlayerHealth state instead of keeping a stale
        // serialized/runtime value forever.
        rewindSuspended =
            _playerHealth != null && _playerHealth.IsRewinding;

        _nextReconciliationTime = Time.unscaledTime;
    }

    private void OnDisable()
    {
        bool shouldNotifySuspension =
            currentState != CombatEncounterState.Cleared &&
            AllowsCombat;

        UnbindPlayerHealth();
        ClearOccupancyRecords();
        _exitDeadline = 0f;

        if (currentState != CombatEncounterState.Cleared)
        {
            currentState = _hasEverActivated
                ? CombatEncounterState.Suspended
                : CombatEncounterState.Dormant;
        }

        if (shouldNotifySuspension)
        {
            EncounterSuspended?.Invoke(this);
        }
    }

    private void Update()
    {
        BindPlayerHealth();

        if (rewindSuspended)
        {
            return;
        }

        float now = Time.unscaledTime;

        if (now >= _nextReconciliationTime)
        {
            _nextReconciliationTime =
                now + Mathf.Max(0.10f, reconciliationInterval);

            ReconcilePlayerPresence();
        }

        if (currentState == CombatEncounterState.PendingExit &&
            now >= _exitDeadline)
        {
            if (HasAnyOccupiedVolume())
            {
                CancelPendingExit();
            }
            else
            {
                SuspendEncounter();
            }
        }
    }

    public void RegisterVolume(CombatEncounterVolume volume)
    {
        EnsureRuntimeInitialized();

        if (volume == null)
        {
            return;
        }

        _volumes.Add(volume);
    }

    public void UnregisterVolume(CombatEncounterVolume volume)
    {
        EnsureRuntimeInitialized();

        if (volume == null)
        {
            return;
        }

        _volumes.Remove(volume);
        _occupiedActivationVolumes.Remove(volume);
        _occupiedRetentionVolumes.Remove(volume);
        RefreshDebugCounts();
        EvaluateExitRequirement();
    }

    public void RegisterMember(CombatEncounterMember member)
    {
        EnsureRuntimeInitialized();

        if (member == null || _members.Contains(member))
        {
            return;
        }

        _members.Add(member);
        member.MarkRegistered(true);

        if (member.RequiredForClear)
        {
            requiredMemberCount++;

            if (member.IsDefeated &&
                _defeatedRequiredMembers.Add(member))
            {
                defeatedRequiredMemberCount++;
            }

            if (currentState == CombatEncounterState.Cleared &&
                !member.IsDefeated)
            {
                Debug.LogWarning(
                    $"[CombatEncounter] {name} 已经 Cleared，但又注册了" +
                    $"仍存活的 Required 成员 {member.name}。" +
                    "Encounter 不会自动重新开启；请让动态生成器在清场前" +
                    "完成 Required 成员注册。",
                    member
                );
            }
        }

        EvaluateClearedState();
    }

    public void UnregisterMember(CombatEncounterMember member)
    {
        EnsureRuntimeInitialized();

        if (ReferenceEquals(member, null) ||
            !_members.Remove(member))
        {
            return;
        }

        member.MarkRegistered(false);

        if (member.RequiredForClear)
        {
            requiredMemberCount =
                Mathf.Max(0, requiredMemberCount - 1);

            if (_defeatedRequiredMembers.Remove(member))
            {
                defeatedRequiredMemberCount =
                    Mathf.Max(
                        0,
                        defeatedRequiredMemberCount - 1
                    );
            }
        }

        EvaluateClearedState();
    }

    /// <summary>
    /// Removes a destroyed component reference without losing the historical
    /// result of an enemy that was already defeated. This keeps the HashSets
    /// clean while preserving Required/Defeated counts used by Cleared state.
    /// </summary>
    internal void NotifyMemberDestroyed(
        CombatEncounterMember member,
        bool wasDefeated
    )
    {
        EnsureRuntimeInitialized();

        if (ReferenceEquals(member, null) ||
            !_members.Remove(member))
        {
            return;
        }

        member.MarkRegistered(false);

        if (!member.RequiredForClear)
        {
            return;
        }

        bool defeatWasAlreadyTracked =
            _defeatedRequiredMembers.Remove(member);

        if (wasDefeated)
        {
            if (!defeatWasAlreadyTracked)
            {
                defeatedRequiredMemberCount++;
            }
        }
        else
        {
            if (defeatWasAlreadyTracked)
            {
                defeatedRequiredMemberCount =
                    Mathf.Max(
                        0,
                        defeatedRequiredMemberCount - 1
                    );
            }

            requiredMemberCount =
                Mathf.Max(0, requiredMemberCount - 1);
        }

        EvaluateClearedState();
    }

    internal void NotifyMemberDefeated(
        CombatEncounterMember member
    )
    {
        EnsureRuntimeInitialized();

        if (member == null ||
            !member.RequiredForClear ||
            !_members.Contains(member) ||
            !_defeatedRequiredMembers.Add(member))
        {
            return;
        }

        defeatedRequiredMemberCount++;
        EvaluateClearedState();
    }

    internal void NotifyVolumePresence(
        CombatEncounterVolume volume,
        Transform playerRoot,
        bool isInside
    )
    {
        EnsureRuntimeInitialized();

        if (volume == null ||
            rewindSuspended ||
            !isActiveAndEnabled)
        {
            return;
        }

        RegisterVolume(volume);

        HashSet<CombatEncounterVolume> targetSet =
            volume.Role == CombatEncounterVolumeRole.Activation
                ? _occupiedActivationVolumes
                : _occupiedRetentionVolumes;

        bool changed = isInside
            ? targetSet.Add(volume)
            : targetSet.Remove(volume);

        if (isInside && playerRoot != null)
        {
            player = playerRoot;
            BindPlayerHealth();
        }

        RefreshDebugCounts();

        if (!changed)
        {
            return;
        }

        if (isInside)
        {
            if (volume.Role == CombatEncounterVolumeRole.Activation)
            {
                HandleActivationVolumeEntered();
            }
            else if (currentState ==
                     CombatEncounterState.PendingExit)
            {
                CancelPendingExit();
            }

            return;
        }

        EvaluateExitRequirement();
    }

    /// <summary>
    /// Manual clear entry point for scripted encounters. Automatic clear only
    /// occurs when at least one RequiredForClear member was registered.
    /// </summary>
    public void MarkCleared()
    {
        EnsureRuntimeInitialized();

        if (currentState == CombatEncounterState.Cleared)
        {
            return;
        }

        currentState = CombatEncounterState.Cleared;
        _exitDeadline = 0f;
        EncounterCleared?.Invoke(this);
    }

    /// <summary>
    /// Resets only encounter lifecycle state. It does not resurrect enemies,
    /// restore health, or move any member back to its spawn position.
    /// </summary>
    [ContextMenu("Reset Encounter Lifecycle")]
    public void ResetEncounterLifecycle()
    {
        EnsureRuntimeInitialized();
        ClearOccupancyRecords();
        currentState = CombatEncounterState.Dormant;
        _hasEverActivated = false;
        _exitDeadline = 0f;
        rewindSuspended = false;
        EncounterReset?.Invoke(this);
        EvaluateClearedState();
        _nextReconciliationTime = Time.unscaledTime;
    }

    [ContextMenu("Collect Child Volumes")]
    public void CollectChildVolumes()
    {
        if (!Application.isPlaying)
        {
            Debug.Log(
                "[CombatEncounter] Child Volume 注册属于运行时状态。" +
                "编辑模式请使用 Validate Encounter 检查层级。",
                this
            );
            return;
        }

        EnsureRuntimeInitialized();

        CombatEncounterVolume[] childVolumes =
            GetComponentsInChildren<CombatEncounterVolume>(true);

        for (int index = 0; index < childVolumes.Length; index++)
        {
            RegisterVolume(childVolumes[index]);
        }
    }

    [ContextMenu("Collect Child Members")]
    public void CollectChildMembers()
    {
        if (!Application.isPlaying)
        {
            Debug.Log(
                "[CombatEncounter] Child Member 注册属于运行时状态。" +
                "编辑模式请使用 Validate Encounter 检查层级。",
                this
            );
            return;
        }

        EnsureRuntimeInitialized();

        CombatEncounterMember[] childMembers =
            GetComponentsInChildren<CombatEncounterMember>(true);

        for (int index = 0; index < childMembers.Length; index++)
        {
            RegisterMember(childMembers[index]);
        }
    }

    [ContextMenu("Validate Encounter")]
    public void ValidateEncounter()
    {
        if (Application.isPlaying)
        {
            EnsureRuntimeInitialized();
            CollectChildVolumes();
            CollectChildMembers();
        }

        int activationCount = 0;
        int retentionCount = 0;

        Vector3 encounterScale = transform.lossyScale;

        if (!ApproximatelyOne(encounterScale.x) ||
            !ApproximatelyOne(encounterScale.y) ||
            !ApproximatelyOne(encounterScale.z))
        {
            Debug.LogWarning(
                $"[CombatEncounter] {name} 的世界缩放不是 (1,1,1)。" +
                "这会让新增 Volume 的尺寸难以预测。建议让 Encounter " +
                "位于未缩放层级，并直接修改 Collider Size。",
                this
            );
        }

        CombatEncounterVolume[] volumesToValidate =
            Application.isPlaying
                ? ToArrayWithoutDestroyedEntries(_volumes)
                : GetComponentsInChildren<CombatEncounterVolume>(true);

        for (int index = 0; index < volumesToValidate.Length; index++)
        {
            CombatEncounterVolume volume = volumesToValidate[index];

            if (volume == null)
            {
                continue;
            }

            if (volume.Role == CombatEncounterVolumeRole.Activation)
            {
                activationCount++;
            }
            else
            {
                retentionCount++;
            }

            Collider colliderItem = volume.VolumeCollider;

            if (colliderItem == null)
            {
                Debug.LogError(
                    $"[CombatEncounter] {volume.name} 缺少 Collider。",
                    volume
                );
            }
            else if (!colliderItem.isTrigger)
            {
                Debug.LogWarning(
                    $"[CombatEncounter] {volume.name} 未开启 Is Trigger。",
                    volume
                );
            }

            if (colliderItem is MeshCollider meshCollider &&
                !meshCollider.convex)
            {
                Debug.LogWarning(
                    $"[CombatEncounter] {volume.name} 使用非 Convex " +
                    "MeshCollider。建议改用多个 Box/Capsule/Sphere。",
                    volume
                );
            }

            Vector3 lossyScale = volume.transform.lossyScale;
            float minimumScale = Mathf.Min(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z)
            );
            float maximumScale = Mathf.Max(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z)
            );

            if (minimumScale > 0.0001f &&
                maximumScale / minimumScale > 8f)
            {
                Debug.LogWarning(
                    $"[CombatEncounter] {volume.name} 的非均匀缩放非常大。" +
                    "建议把尺寸写入 Collider，而不是极端缩放 Transform。",
                    volume
                );
            }
        }

        if (activationCount == 0)
        {
            Debug.LogError(
                $"[CombatEncounter] {name} 没有 Activation Volume。",
                this
            );
        }

        if (retentionCount == 0)
        {
            Debug.LogWarning(
                $"[CombatEncounter] {name} 没有 Retention Volume。" +
                "系统仍可运行，但边界体验会退化为 Activation Volume。",
                this
            );
        }

        int memberCount;
        int requiredCount;

        if (Application.isPlaying)
        {
            _members.RemoveWhere(item => item == null);
            memberCount = _members.Count;
            requiredCount = requiredMemberCount;
        }
        else
        {
            CombatEncounterMember[] childMembers =
                GetComponentsInChildren<CombatEncounterMember>(true);

            memberCount = childMembers.Length;
            requiredCount = 0;

            for (int index = 0; index < childMembers.Length; index++)
            {
                if (childMembers[index] != null &&
                    childMembers[index].RequiredForClear)
                {
                    requiredCount++;
                }
            }
        }

        Debug.Log(
            $"[CombatEncounter] {name} 验证完成：" +
            $"Activation={activationCount}, " +
            $"Retention={retentionCount}, " +
            $"Members={memberCount}, " +
            $"Required={requiredCount}。",
            this
        );
    }

    private void EnsureRuntimeInitialized()
    {
        if (_runtimeInitialized)
        {
            return;
        }

        _runtimeInitialized = true;

        _volumes.Clear();
        _occupiedActivationVolumes.Clear();
        _occupiedRetentionVolumes.Clear();
        _members.Clear();
        _defeatedRequiredMembers.Clear();

        currentState = CombatEncounterState.Dormant;
        player = null;
        occupiedActivationVolumeCount = 0;
        occupiedRetentionVolumeCount = 0;
        requiredMemberCount = 0;
        defeatedRequiredMemberCount = 0;
        rewindSuspended = false;

        _nextReconciliationTime = 0f;
        _exitDeadline = 0f;
        _nextPlayerSearchTime = 0f;
        _hasEverActivated = false;
        _wasCombatAllowedBeforeRewind = false;
    }

    private static T[] ToArrayWithoutDestroyedEntries<T>(
        HashSet<T> source
    ) where T : UnityEngine.Object
    {
        source.RemoveWhere(
            item => (UnityEngine.Object)item == null
        );

        T[] result = new T[source.Count];
        source.CopyTo(result);
        return result;
    }

    private static bool ApproximatelyOne(float value)
    {
        return Mathf.Abs(value - 1f) <= 0.001f;
    }

    private void HandleActivationVolumeEntered()
    {
        if (currentState == CombatEncounterState.Cleared)
        {
            return;
        }

        if (currentState == CombatEncounterState.PendingExit)
        {
            CancelPendingExit();
            return;
        }

        if (currentState == CombatEncounterState.Active)
        {
            return;
        }

        currentState = CombatEncounterState.Active;
        _hasEverActivated = true;
        _exitDeadline = 0f;
        EncounterActivated?.Invoke(this);
    }

    private void CancelPendingExit()
    {
        if (currentState != CombatEncounterState.PendingExit)
        {
            return;
        }

        currentState = CombatEncounterState.Active;
        _exitDeadline = 0f;
    }

    private void EvaluateExitRequirement()
    {
        if (!AllowsCombat || HasAnyOccupiedVolume())
        {
            return;
        }

        if (exitGraceTime <= 0f)
        {
            SuspendEncounter();
            return;
        }

        currentState = CombatEncounterState.PendingExit;
        _exitDeadline =
            Time.unscaledTime + Mathf.Max(0f, exitGraceTime);
    }

    private void SuspendEncounter()
    {
        if (currentState == CombatEncounterState.Cleared ||
            currentState == CombatEncounterState.Suspended)
        {
            return;
        }

        currentState = CombatEncounterState.Suspended;
        _exitDeadline = 0f;
        EncounterSuspended?.Invoke(this);
    }

    private bool HasAnyOccupiedVolume()
    {
        return
            _occupiedActivationVolumes.Count > 0 ||
            _occupiedRetentionVolumes.Count > 0;
    }

    private void EvaluateClearedState()
    {
        if (requiredMemberCount <= 0)
        {
            return;
        }

        if (defeatedRequiredMemberCount >= requiredMemberCount)
        {
            MarkCleared();
        }
    }

    private void ReconcilePlayerPresence()
    {
        ResolvePlayerReference();

        if (player == null)
        {
            ClearOccupancyRecords();
            EvaluateExitRequirement();
            return;
        }

        _volumes.RemoveWhere(item => item == null);

        foreach (CombatEncounterVolume volume in _volumes)
        {
            if (volume == null || !volume.isActiveAndEnabled)
            {
                continue;
            }

            bool isInside = volume.ContainsPlayerNow(player);
            volume.SynchronizeReportedPresence(player, isInside);
        }

        RefreshDebugCounts();
        EvaluateExitRequirement();
    }

    private void ResolvePlayerReference()
    {
        if (player != null && player.gameObject.activeInHierarchy)
        {
            return;
        }

        if (_playerHealth != null)
        {
            player = _playerHealth.transform;
            return;
        }

        if (Time.unscaledTime < _nextPlayerSearchTime)
        {
            return;
        }

        _nextPlayerSearchTime = Time.unscaledTime + 0.5f;

        PlayerHealth foundPlayer =
            PlayerHealth.Instance != null
                ? PlayerHealth.Instance
                : FindAnyObjectByType<PlayerHealth>();

        if (foundPlayer != null)
        {
            player = foundPlayer.transform;
        }
    }

    private void BindPlayerHealth()
    {
        PlayerHealth resolvedHealth = null;

        if (player != null)
        {
            resolvedHealth =
                player.GetComponentInParent<PlayerHealth>();
        }

        if (resolvedHealth == null)
        {
            if (Time.unscaledTime < _nextPlayerSearchTime)
            {
                return;
            }

            _nextPlayerSearchTime = Time.unscaledTime + 0.5f;

            resolvedHealth =
                PlayerHealth.Instance != null
                    ? PlayerHealth.Instance
                    : FindAnyObjectByType<PlayerHealth>();
        }

        if (resolvedHealth == _playerHealth)
        {
            return;
        }

        UnbindPlayerHealth();
        _playerHealth = resolvedHealth;

        if (_playerHealth == null)
        {
            return;
        }

        player = _playerHealth.transform;
        _playerHealth.RewindStarted += HandleRewindStarted;
        _playerHealth.RewindCompleted += HandleRewindCompleted;
    }

    private void UnbindPlayerHealth()
    {
        if (_playerHealth == null)
        {
            return;
        }

        _playerHealth.RewindStarted -= HandleRewindStarted;
        _playerHealth.RewindCompleted -= HandleRewindCompleted;
        _playerHealth = null;
    }

    private void HandleRewindStarted()
    {
        if (currentState == CombatEncounterState.Cleared)
        {
            return;
        }

        _wasCombatAllowedBeforeRewind = AllowsCombat;
        rewindSuspended = true;
        _exitDeadline = 0f;

        bool shouldNotifySuspension = AllowsCombat;
        ClearOccupancyRecords();

        if (shouldNotifySuspension)
        {
            currentState = CombatEncounterState.Suspended;
            EncounterSuspended?.Invoke(this);
        }
    }

    private void HandleRewindCompleted()
    {
        if (currentState == CombatEncounterState.Cleared)
        {
            rewindSuspended = false;
            return;
        }

        rewindSuspended = false;
        ClearOccupancyRecords();
        ResolvePlayerReference();
        ReconcilePlayerPresence();

        bool insideActivation =
            _occupiedActivationVolumes.Count > 0;

        bool insideRetention =
            _occupiedRetentionVolumes.Count > 0;

        if (insideActivation ||
            (_wasCombatAllowedBeforeRewind && insideRetention))
        {
            if (currentState != CombatEncounterState.Active)
            {
                currentState = CombatEncounterState.Active;
                _hasEverActivated = true;
                EncounterActivated?.Invoke(this);
            }

            return;
        }

        currentState = _hasEverActivated
            ? CombatEncounterState.Suspended
            : CombatEncounterState.Dormant;
    }

    private void ClearOccupancyRecords()
    {
        foreach (CombatEncounterVolume volume in _volumes)
        {
            if (volume != null)
            {
                volume.ForceClearOccupancy(false);
            }
        }

        ClearOccupiedSetsOnly();
    }

    private void ClearOccupiedSetsOnly()
    {
        _occupiedActivationVolumes.Clear();
        _occupiedRetentionVolumes.Clear();
        RefreshDebugCounts();
    }

    private void RefreshDebugCounts()
    {
        _occupiedActivationVolumes.RemoveWhere(item => item == null);
        _occupiedRetentionVolumes.RemoveWhere(item => item == null);

        occupiedActivationVolumeCount =
            _occupiedActivationVolumes.Count;

        occupiedRetentionVolumeCount =
            _occupiedRetentionVolumes.Count;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        exitGraceTime = Mathf.Max(0f, exitGraceTime);
        reconciliationInterval =
            Mathf.Clamp(reconciliationInterval, 0.10f, 1f);
    }
#endif
}
