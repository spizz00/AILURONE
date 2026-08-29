#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Straight, non-homing projectile used by Flying Bot's Phase 2B twin
/// predictive interception shot. The projectile is created procedurally so the
/// first gameplay pass does not require a dedicated projectile prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlyingBotInterceptProjectile : MonoBehaviour
{
    private const int HitBufferSize = 24;

    private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];

    private Transform _ownerRoot;
    private float _speed;
    private float _damage;
    private float _remainingLife;
    private float _collisionRadius;
    private LayerMask _collisionMask;
    private bool _launched;

    private TrailRenderer _trail;
    private Transform _core;
    private Renderer _coreRenderer;
    private Material _coreMaterial;
    private Material _trailMaterial;

    public static FlyingBotInterceptProjectile Spawn(
        Vector3 position,
        Vector3 direction,
        float speed,
        float damage,
        float lifeTime,
        float collisionRadius,
        LayerMask collisionMask,
        Transform ownerRoot,
        bool punishmentShot
    )
    {
        GameObject root = new GameObject("FlyingBot_InterceptProjectile");
        FlyingBotInterceptProjectile projectile =
            root.AddComponent<FlyingBotInterceptProjectile>();

        projectile.Launch(
            position,
            direction,
            speed,
            damage,
            lifeTime,
            collisionRadius,
            collisionMask,
            ownerRoot,
            punishmentShot
        );

        return projectile;
    }

    private void Awake()
    {
        BuildRuntimeVisual();
    }

    private void OnDestroy()
    {
        if (_coreMaterial != null)
        {
            Destroy(_coreMaterial);
        }

        if (_trailMaterial != null)
        {
            Destroy(_trailMaterial);
        }
    }

    private void Launch(
        Vector3 position,
        Vector3 direction,
        float speed,
        float damage,
        float lifeTime,
        float collisionRadius,
        LayerMask collisionMask,
        Transform ownerRoot,
        bool punishmentShot
    )
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        _ownerRoot = ownerRoot;
        _speed = Mathf.Max(0.1f, speed);
        _damage = Mathf.Max(0f, damage);
        _remainingLife = Mathf.Max(0.1f, lifeTime);
        _collisionRadius = Mathf.Max(0.01f, collisionRadius);
        _collisionMask = collisionMask.value != 0
            ? collisionMask
            : Physics.DefaultRaycastLayers;
        _launched = true;

        transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(direction, Vector3.up)
        );

        ApplyVisualStyle(punishmentShot);

        if (_trail != null)
        {
            _trail.Clear();
            _trail.emitting = true;
        }
    }

    private void Update()
    {
        if (!_launched)
        {
            return;
        }

        PlayerHealth playerHealth = PlayerHealth.Instance;

        if (playerHealth != null && playerHealth.IsRewinding)
        {
            Destroy(gameObject);
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        _remainingLife -= deltaTime;

        if (_remainingLife <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float distance = _speed * deltaTime;

        if (distance <= 0f)
        {
            return;
        }

        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        if (TryFindNearestValidHit(
                origin,
                direction,
                distance,
                out RaycastHit hit))
        {
            transform.position = hit.point;
            ResolveHit(hit.collider);
            return;
        }

        transform.position = origin + direction * distance;
    }

    private bool TryFindNearestValidHit(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit selectedHit
    )
    {
        selectedHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            _collisionRadius,
            direction,
            _hits,
            distance,
            _collisionMask,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit candidate = _hits[index];
            Collider candidateCollider = candidate.collider;

            if (!IsValidCollision(candidateCollider))
            {
                continue;
            }

            if (candidate.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = candidate.distance;
            selectedHit = candidate;
            found = true;
        }

        return found;
    }

    private bool IsValidCollision(Collider candidate)
    {
        if (candidate == null ||
            !candidate.enabled ||
            candidate.isTrigger)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;

        if (_ownerRoot != null &&
            (candidateTransform == _ownerRoot ||
             candidateTransform.IsChildOf(_ownerRoot)))
        {
            return false;
        }

        // The Flying Bot shot is not an enemy-friendly-fire weapon. Other
        // EnemyTarget hitboxes are transparent to it so allied enemies cannot
        // accidentally shield the player.
        EnemyTarget enemyTarget =
            candidate.GetComponentInParent<EnemyTarget>();

        if (enemyTarget != null)
        {
            return false;
        }

        return true;
    }

    private void ResolveHit(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            Destroy(gameObject);
            return;
        }

        PlayerHealth playerHealth =
            hitCollider.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            if (!playerHealth.IsRewinding)
            {
                playerHealth.TakeDamage(_damage);
            }

            Destroy(gameObject);
            return;
        }

        // Any other solid collider blocks the straight projectile.
        Destroy(gameObject);
    }

    private void BuildRuntimeVisual()
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(transform, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = new Vector3(0.13f, 0.13f, 0.24f);
        _core = core.transform;

        Collider primitiveCollider = core.GetComponent<Collider>();

        if (primitiveCollider != null)
        {
            primitiveCollider.enabled = false;
            Destroy(primitiveCollider);
        }

        _coreRenderer = core.GetComponent<Renderer>();

        Shader coreShader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (coreShader == null)
        {
            coreShader = Shader.Find("Unlit/Color");
        }

        if (coreShader == null)
        {
            coreShader = Shader.Find("Sprites/Default");
        }

        if (coreShader != null && _coreRenderer != null)
        {
            _coreMaterial = new Material(coreShader);
            _coreRenderer.sharedMaterial = _coreMaterial;
        }

        _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.time = 0.18f;
        _trail.minVertexDistance = 0.03f;
        _trail.widthMultiplier = 0.085f;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.alignment = LineAlignment.View;

        Shader trailShader = Shader.Find("Sprites/Default");

        if (trailShader == null)
        {
            trailShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (trailShader != null)
        {
            _trailMaterial = new Material(trailShader);
            _trail.sharedMaterial = _trailMaterial;
        }

        ApplyVisualStyle(false);
        _trail.Clear();
    }

    private void ApplyVisualStyle(bool punishmentShot)
    {
        Color coreColor = punishmentShot
            ? new Color(2.8f, 0.55f, 0.12f, 1f)
            : new Color(0.72f, 1.75f, 2.55f, 1f);

        if (_core != null)
        {
            _core.localScale = punishmentShot
                ? new Vector3(0.15f, 0.15f, 0.27f)
                : new Vector3(0.13f, 0.13f, 0.24f);
        }

        if (_coreMaterial != null)
        {
            if (_coreMaterial.HasProperty("_BaseColor"))
            {
                _coreMaterial.SetColor("_BaseColor", coreColor);
            }

            if (_coreMaterial.HasProperty("_Color"))
            {
                _coreMaterial.SetColor("_Color", coreColor);
            }
        }

        if (_trail == null)
        {
            return;
        }

        _trail.widthMultiplier = punishmentShot ? 0.102f : 0.085f;

        Color trailStart = punishmentShot
            ? new Color(2.6f, 0.85f, 0.20f)
            : new Color(0.82f, 1.7f, 2.5f);
        Color trailEnd = punishmentShot
            ? new Color(1.25f, 0.12f, 0.05f)
            : new Color(0.16f, 0.75f, 1.25f);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailStart, 0f),
                new GradientColorKey(trailEnd, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        _trail.colorGradient = gradient;
    }
}
