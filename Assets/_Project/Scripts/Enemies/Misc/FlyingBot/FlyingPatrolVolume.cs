#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Editable 3D movement volume used by Flying Bot.
///
/// This component is deliberately independent from CombatEncounter:
/// - CombatEncounter decides whether combat is allowed.
/// - FlyingPatrolVolume decides where a flying enemy is allowed to move.
///
/// The BoxCollider is used as an authoring shape only. It is kept as a trigger
/// so it never blocks the player or the enemy physically.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public sealed class FlyingPatrolVolume : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField]
    private BoxCollider volumeCollider;

    [Header("Scene View")]
    [SerializeField]
    private bool drawGizmo = true;

    [SerializeField]
    private Color gizmoColor = new Color(0.10f, 0.88f, 1f, 0.18f);

    public BoxCollider VolumeCollider
    {
        get
        {
            CacheReferences();
            return volumeCollider;
        }
    }

    public Vector3 WorldCenter
    {
        get
        {
            CacheReferences();
            return volumeCollider != null
                ? volumeCollider.transform.TransformPoint(volumeCollider.center)
                : transform.position;
        }
    }

    private void Awake()
    {
        CacheReferences();
        EnsureAuthoringCollider();
    }

    private void OnValidate()
    {
        CacheReferences();
        EnsureAuthoringCollider();
    }

    public bool ContainsWorldPoint(Vector3 worldPoint, float worldPadding = 0f)
    {
        CacheReferences();

        if (volumeCollider == null)
        {
            return false;
        }

        Vector3 localPoint =
            volumeCollider.transform.InverseTransformPoint(worldPoint) -
            volumeCollider.center;

        Vector3 halfSize = GetPaddedLocalHalfSize(worldPadding);

        return Mathf.Abs(localPoint.x) <= halfSize.x + 0.0001f &&
               Mathf.Abs(localPoint.y) <= halfSize.y + 0.0001f &&
               Mathf.Abs(localPoint.z) <= halfSize.z + 0.0001f;
    }

    public Vector3 ClampWorldPoint(Vector3 worldPoint, float worldPadding = 0f)
    {
        CacheReferences();

        if (volumeCollider == null)
        {
            return worldPoint;
        }

        Vector3 localPoint =
            volumeCollider.transform.InverseTransformPoint(worldPoint) -
            volumeCollider.center;

        Vector3 halfSize = GetPaddedLocalHalfSize(worldPadding);

        localPoint.x = Mathf.Clamp(localPoint.x, -halfSize.x, halfSize.x);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfSize.y, halfSize.y);
        localPoint.z = Mathf.Clamp(localPoint.z, -halfSize.z, halfSize.z);

        return volumeCollider.transform.TransformPoint(
            volumeCollider.center + localPoint
        );
    }

    public Vector3 GetRandomWorldPoint(float worldPadding = 0f)
    {
        CacheReferences();

        if (volumeCollider == null)
        {
            return transform.position;
        }

        Vector3 halfSize = GetPaddedLocalHalfSize(worldPadding);

        Vector3 localOffset = new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            Random.Range(-halfSize.z, halfSize.z)
        );

        return volumeCollider.transform.TransformPoint(
            volumeCollider.center + localOffset
        );
    }

    /// <summary>
    /// Returns a normalized 0..1 position inside the authoring box. Useful for
    /// debugging and later spawn/patrol logic without exposing collider math to
    /// the AI itself.
    /// </summary>
    public Vector3 GetNormalizedPosition(Vector3 worldPoint)
    {
        CacheReferences();

        if (volumeCollider == null)
        {
            return Vector3.one * 0.5f;
        }

        Vector3 localPoint =
            volumeCollider.transform.InverseTransformPoint(worldPoint) -
            volumeCollider.center;

        Vector3 safeSize = volumeCollider.size;
        safeSize.x = Mathf.Max(0.0001f, safeSize.x);
        safeSize.y = Mathf.Max(0.0001f, safeSize.y);
        safeSize.z = Mathf.Max(0.0001f, safeSize.z);

        return new Vector3(
            Mathf.Clamp01(localPoint.x / safeSize.x + 0.5f),
            Mathf.Clamp01(localPoint.y / safeSize.y + 0.5f),
            Mathf.Clamp01(localPoint.z / safeSize.z + 0.5f)
        );
    }

    private Vector3 GetPaddedLocalHalfSize(float worldPadding)
    {
        Vector3 halfSize = volumeCollider != null
            ? volumeCollider.size * 0.5f
            : Vector3.one * 0.5f;

        float safePadding = Mathf.Max(0f, worldPadding);
        Vector3 scale = volumeCollider != null
            ? volumeCollider.transform.lossyScale
            : transform.lossyScale;

        Vector3 localPadding = new Vector3(
            safePadding / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            safePadding / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            safePadding / Mathf.Max(0.0001f, Mathf.Abs(scale.z))
        );

        halfSize.x = Mathf.Max(0.01f, halfSize.x - localPadding.x);
        halfSize.y = Mathf.Max(0.01f, halfSize.y - localPadding.y);
        halfSize.z = Mathf.Max(0.01f, halfSize.z - localPadding.z);

        return halfSize;
    }

    private void CacheReferences()
    {
        if (volumeCollider == null)
        {
            volumeCollider = GetComponent<BoxCollider>();
        }
    }

    private void EnsureAuthoringCollider()
    {
        if (volumeCollider == null)
        {
            return;
        }

        volumeCollider.isTrigger = true;

        int ignoreRaycastLayer =
            LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer >= 0 &&
            gameObject.layer == 0)
        {
            gameObject.layer = ignoreRaycastLayer;
        }

        Vector3 safeSize = volumeCollider.size;
        safeSize.x = Mathf.Max(0.05f, safeSize.x);
        safeSize.y = Mathf.Max(0.05f, safeSize.y);
        safeSize.z = Mathf.Max(0.05f, safeSize.z);
        volumeCollider.size = safeSize;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmo)
        {
            return;
        }

        CacheReferences();

        if (volumeCollider == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = volumeCollider.transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(volumeCollider.center, volumeCollider.size);

        Color outline = gizmoColor;
        outline.a = Mathf.Max(0.55f, outline.a);
        Gizmos.color = outline;
        Gizmos.DrawWireCube(volumeCollider.center, volumeCollider.size);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
#endif
}
