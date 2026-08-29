#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Stable membership record for an enemy that belongs to a CombatEncounter.
///
/// Membership intentionally survives temporary GameObject disable/enable cycles.
/// This prevents pooled, culled, or presentation-disabled enemies from reducing
/// the encounter's required-enemy count and accidentally clearing the encounter.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatEncounterMember : MonoBehaviour
{
    [Header("Encounter Membership")]
    [SerializeField]
    private CombatEncounter encounter;

    [SerializeField]
    private bool autoFindEncounterInParent = true;

    [SerializeField]
    private bool requiredForClear = true;

    [Header("Enemy State")]
    [SerializeField]
    private EnemyTarget enemyTarget;

    [SerializeField]
    private bool defeatReported;

    private bool _registered;

    public CombatEncounter Encounter => encounter;
    public bool RequiredForClear => requiredForClear;
    public bool IsDefeated =>
        defeatReported ||
        (enemyTarget != null && enemyTarget.IsDead);

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        BindDeathEvent();
        RegisterWithEncounter();
    }

    private void OnDisable()
    {
        // Do not unregister here. A disabled enemy is still a member of the
        // encounter and must not make RequiredMemberCount shrink temporarily.
        UnbindDeathEvent();
    }

    private void OnDestroy()
    {
        UnbindDeathEvent();

        if (_registered && encounter != null)
        {
            encounter.NotifyMemberDestroyed(this, IsDefeated);
        }

        _registered = false;
    }

    /// <summary>
    /// Runtime-safe configuration entry point. Ground Bot Phase 1 uses this to
    /// bind a scene-specific encounter while keeping the enemy prefab reusable.
    /// </summary>
    public void Configure(
        CombatEncounter targetEncounter,
        EnemyTarget targetEnemy,
        bool isRequiredForClear
    )
    {
        EnemyTarget resolvedEnemy = targetEnemy != null
            ? targetEnemy
            : GetComponent<EnemyTarget>();

        bool targetChanged = enemyTarget != resolvedEnemy;
        bool membershipChanged =
            encounter != targetEncounter ||
            requiredForClear != isRequiredForClear;

        if (_registered && membershipChanged && encounter != null)
        {
            encounter.UnregisterMember(this);
        }

        UnbindDeathEvent();

        encounter = targetEncounter;
        enemyTarget = resolvedEnemy;
        requiredForClear = isRequiredForClear;
        defeatReported = targetChanged
            ? enemyTarget != null && enemyTarget.IsDead
            : defeatReported ||
              (enemyTarget != null && enemyTarget.IsDead);

        if (isActiveAndEnabled)
        {
            BindDeathEvent();
        }

        RegisterWithEncounter();
    }

    private void HandleEnemyDied(EnemyDeathInfo deathInfo)
    {
        if (defeatReported)
        {
            return;
        }

        defeatReported = true;

        if (encounter != null)
        {
            encounter.NotifyMemberDefeated(this);
        }
    }

    internal void MarkRegistered(bool registered)
    {
        _registered = registered;
    }

    internal bool IsRegistered => _registered;

    private void RegisterWithEncounter()
    {
        if (_registered || encounter == null)
        {
            return;
        }

        encounter.RegisterMember(this);
    }

    private void BindDeathEvent()
    {
        if (enemyTarget == null)
        {
            return;
        }

        enemyTarget.Died -= HandleEnemyDied;
        enemyTarget.Died += HandleEnemyDied;

        if (enemyTarget.IsDead && !defeatReported)
        {
            defeatReported = true;

            // The enemy may have died while this component or its GameObject
            // was temporarily disabled. In that case the Died event was not
            // observed, so repair the encounter count when the member returns.
            if (_registered && encounter != null)
            {
                encounter.NotifyMemberDefeated(this);
            }
        }
    }

    private void UnbindDeathEvent()
    {
        if (enemyTarget != null)
        {
            enemyTarget.Died -= HandleEnemyDied;
        }
    }

    private void CacheReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        if (encounter == null && autoFindEncounterInParent)
        {
            encounter = GetComponentInParent<CombatEncounter>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheReferences();
    }
#endif
}
