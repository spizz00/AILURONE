#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;

public enum CombatEncounterVolumeRole
{
    Activation,
    Retention
}

/// <summary>
/// Base class shared by activation and retention volumes.
///
/// A volume resolves all player colliders back to one Player root, so a
/// CharacterController plus child colliders still counts as one player.
/// Trigger callbacks provide immediate response; CombatEncounter also performs
/// a low-frequency overlap reconciliation to repair missed exits caused by
/// teleporting, disabling colliders, or rewind.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public abstract class CombatEncounterVolume : MonoBehaviour
{
    [Header("Encounter")]
    [SerializeField]
    private CombatEncounter encounter;

    [SerializeField]
    private bool autoFindEncounterInParent = true;

    [Header("Runtime")]
    [SerializeField]
    private Collider volumeCollider;

    [SerializeField]
    private bool reportedPlayerInside;

    private readonly HashSet<Collider> _playerColliders =
        new HashSet<Collider>();

    private readonly List<Collider> _playerColliderBuffer =
        new List<Collider>(8);

    private Transform _reportedPlayerRoot;

    public CombatEncounter Encounter
    {
        get
        {
            CacheReferences();
            return encounter;
        }
    }
    public Collider VolumeCollider
    {
        get
        {
            CacheReferences();
            return volumeCollider;
        }
    }
    public bool ReportedPlayerInside => reportedPlayerInside;
    public abstract CombatEncounterVolumeRole Role { get; }

    protected virtual void Awake()
    {
        CacheReferences();
        EnsureTrigger();
    }

    protected virtual void OnEnable()
    {
        CacheReferences();
        EnsureTrigger();

        if (encounter != null)
        {
            encounter.RegisterVolume(this);
        }
    }

    protected virtual void OnDisable()
    {
        if (encounter != null)
        {
            encounter.UnregisterVolume(this);
        }

        ForceClearOccupancy(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterPlayerCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterPlayerCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        _playerColliders.Remove(other);
        RemoveInvalidPlayerColliders();

        if (_playerColliders.Count == 0)
        {
            ReportPresence(_reportedPlayerRoot, false);
        }
    }

    private void RegisterPlayerCollider(Collider other)
    {
        Transform playerRoot = ResolvePlayerRoot(other);

        if (playerRoot == null)
        {
            return;
        }

        if (_reportedPlayerRoot != null &&
            _reportedPlayerRoot != playerRoot)
        {
            _playerColliders.Clear();
        }

        _playerColliders.Add(other);
        ReportPresence(playerRoot, true);
    }

    internal void SynchronizeReportedPresence(
        Transform playerRoot,
        bool isInside
    )
    {
        if (isInside)
        {
            ReportPresence(playerRoot, true);
            return;
        }

        _playerColliders.Clear();
        ReportPresence(_reportedPlayerRoot != null
            ? _reportedPlayerRoot
            : playerRoot, false);
    }

    internal void ForceClearOccupancy(bool notifyEncounter)
    {
        Transform previousPlayer = _reportedPlayerRoot;

        _playerColliders.Clear();
        _reportedPlayerRoot = null;
        reportedPlayerInside = false;

        if (notifyEncounter && encounter != null)
        {
            encounter.NotifyVolumePresence(
                this,
                previousPlayer,
                false
            );
        }
    }

    internal bool ContainsPlayerNow(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return false;
        }

        CacheReferences();

        if (volumeCollider == null ||
            !volumeCollider.enabled ||
            !gameObject.activeInHierarchy)
        {
            return false;
        }

        _playerColliderBuffer.Clear();
        playerRoot.GetComponentsInChildren(
            true,
            _playerColliderBuffer
        );

        _playerColliders.Clear();
        bool foundColliderOverlap = false;

        for (int index = 0; index < _playerColliderBuffer.Count; index++)
        {
            Collider playerCollider = _playerColliderBuffer[index];

            if (playerCollider == null ||
                !playerCollider.enabled ||
                !playerCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!volumeCollider.bounds.Intersects(
                    playerCollider.bounds))
            {
                continue;
            }

            if (AreCollidersOverlapping(
                    volumeCollider,
                    playerCollider))
            {
                _playerColliders.Add(playerCollider);
                foundColliderOverlap = true;
            }
        }

        if (foundColliderOverlap)
        {
            return true;
        }

        return ContainsWorldPoint(playerRoot.position);
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        CacheReferences();

        if (volumeCollider == null)
        {
            return false;
        }

        if (volumeCollider is BoxCollider boxCollider)
        {
            Vector3 localPoint =
                boxCollider.transform.InverseTransformPoint(worldPoint);

            Vector3 difference =
                localPoint - boxCollider.center;

            Vector3 halfSize =
                boxCollider.size * 0.5f;

            return
                Mathf.Abs(difference.x) <= halfSize.x + 0.0001f &&
                Mathf.Abs(difference.y) <= halfSize.y + 0.0001f &&
                Mathf.Abs(difference.z) <= halfSize.z + 0.0001f;
        }

        Vector3 closestPoint =
            volumeCollider.ClosestPoint(worldPoint);

        return
            (closestPoint - worldPoint).sqrMagnitude <=
            0.0001f;
    }

    private static bool AreCollidersOverlapping(
        Collider first,
        Collider second
    )
    {
        if (first == null || second == null)
        {
            return false;
        }

        bool canUsePenetration =
            !(first is MeshCollider firstMesh) ||
            firstMesh.convex;

        canUsePenetration &=
            !(second is MeshCollider secondMesh) ||
            secondMesh.convex;

        if (canUsePenetration)
        {
            bool penetrating = Physics.ComputePenetration(
                first,
                first.transform.position,
                first.transform.rotation,
                second,
                second.transform.position,
                second.transform.rotation,
                out _,
                out _
            );

            if (penetrating)
            {
                return true;
            }
        }

        // Avoid the old AABB-only fallback. Rotated colliders can have
        // overlapping bounds while their actual shapes are still separated,
        // which would keep an encounter active outside its real volume.
        Vector3 pointOnFirst =
            first.ClosestPoint(second.bounds.center);
        Vector3 pointOnSecond =
            second.ClosestPoint(pointOnFirst);

        return
            (pointOnFirst - pointOnSecond).sqrMagnitude <=
            0.000001f;
    }

    private void ReportPresence(
        Transform playerRoot,
        bool isInside
    )
    {
        if (isInside)
        {
            if (playerRoot == null)
            {
                return;
            }

            bool changed =
                !reportedPlayerInside ||
                _reportedPlayerRoot != playerRoot;

            _reportedPlayerRoot = playerRoot;
            reportedPlayerInside = true;

            if (changed && encounter != null)
            {
                encounter.NotifyVolumePresence(
                    this,
                    playerRoot,
                    true
                );
            }

            return;
        }

        if (!reportedPlayerInside)
        {
            return;
        }

        Transform previousPlayer = _reportedPlayerRoot;
        _reportedPlayerRoot = null;
        reportedPlayerInside = false;

        if (encounter != null)
        {
            encounter.NotifyVolumePresence(
                this,
                previousPlayer,
                false
            );
        }
    }

    private void RemoveInvalidPlayerColliders()
    {
        _playerColliders.RemoveWhere(
            item =>
                item == null ||
                !item.enabled ||
                !item.gameObject.activeInHierarchy
        );
    }

    private static Transform ResolvePlayerRoot(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            return playerHealth.transform;
        }

        if (other.CompareTag("Player"))
        {
            return other.transform.root;
        }

        Transform root = other.transform.root;

        if (root != null && root.CompareTag("Player"))
        {
            return root;
        }

        return null;
    }

    private void CacheReferences()
    {
        if (volumeCollider == null)
        {
            volumeCollider = GetComponent<Collider>();
        }

        if (encounter == null && autoFindEncounterInParent)
        {
            encounter = GetComponentInParent<CombatEncounter>();
        }
    }

    private void EnsureTrigger()
    {
        if (volumeCollider != null && !volumeCollider.isTrigger)
        {
            volumeCollider.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    protected virtual Color GizmoColor =>
        Role == CombatEncounterVolumeRole.Activation
            ? new Color(1f, 0.45f, 0.05f, 0.24f)
            : new Color(1f, 0.82f, 0.15f, 0.14f);

    protected virtual void OnDrawGizmos()
    {
        CacheReferences();

        if (volumeCollider == null)
        {
            return;
        }

        Color fillColor = GizmoColor;
        Color wireColor = fillColor;
        wireColor.a = 0.85f;

        if (volumeCollider is BoxCollider boxCollider)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = boxCollider.transform.localToWorldMatrix;

            Gizmos.color = fillColor;
            Gizmos.DrawCube(
                boxCollider.center,
                boxCollider.size
            );

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(
                boxCollider.center,
                boxCollider.size
            );

            Gizmos.matrix = previousMatrix;
            return;
        }

        Bounds bounds = volumeCollider.bounds;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    protected virtual void OnValidate()
    {
        CacheReferences();
        EnsureTrigger();
    }
#endif
}
