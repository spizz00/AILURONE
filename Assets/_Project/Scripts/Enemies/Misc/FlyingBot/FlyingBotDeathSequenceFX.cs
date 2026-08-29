#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual-only mechanical death sequence for Flying Bot. The gameplay object
/// can disappear immediately while this proxy detaches its wings and falls.
/// </summary>
public sealed class FlyingBotDeathSequenceFX : MonoBehaviour
{
    private const float FailureDuration = 0.16f;
    private const float Gravity = 17.5f;
    private const float MaximumFallDuration = 3.6f;
    private const float AftershockDelay = 0.14f;
    private const float CleanupDelay = 1.35f;
    private const float WingCleanupDelay = 1.05f;
    private const float GroundNormalThreshold = 0.25f;

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private Transform _bodyProxy;
    private Transform _wingProxy;
    private Renderer[] _allRenderers;
    private Renderer[] _bodyRenderers;
    private Renderer[] _wingRenderers;
    private MaterialPropertyBlock _propertyBlock;
    private Quaternion _baseRotation;
    private Vector3 _impactDirection;
    private Vector3 _bodyVelocity;
    private Vector3 _bodyAngularVelocity;
    private Vector3 _wingVelocity;
    private Vector3 _wingAngularVelocity;
    private Color _primaryColor;
    private Color _secondaryColor;
    private LayerMask _groundMask;
    private float _bodyBottomOffset;
    private float _elapsed;
    private float _explosionElapsed;
    private float _wingSettledElapsed;
    private bool _wingsDetached;
    private bool _wingSettled;
    private bool _exploded;
    private bool _aftershockTriggered;

    public static bool TrySpawn(
        FlyingBotEnemy source,
        Vector3 impactPoint,
        Vector3 impactNormal
    )
    {
        if (source == null || source.visualRoot == null)
        {
            return false;
        }

        Renderer[] sourceRenderers =
            source.visualRoot.GetComponentsInChildren<Renderer>(true);

        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            return false;
        }

        GameObject root = new GameObject("FlyingBot_DeathSequenceFX");
        root.transform.SetPositionAndRotation(
            source.visualRoot.position,
            source.visualRoot.rotation
        );

        GameObject visualClone = Instantiate(
            source.visualRoot.gameObject,
            root.transform,
            false
        );
        visualClone.name = "FlyingBotDeathVisualProxy";
        visualClone.transform.localPosition = Vector3.zero;
        visualClone.transform.localRotation = Quaternion.identity;
        visualClone.transform.localScale = source.visualRoot.lossyScale;

        StripGameplayComponents(visualClone);

        FlyingBotDeathSequenceFX sequence =
            root.AddComponent<FlyingBotDeathSequenceFX>();
        sequence.Initialize(
            visualClone.transform,
            impactPoint,
            impactNormal,
            source.transform.forward,
            source.detectedRed,
            source.diveChargeOrange,
            source.obstacleMask
        );
        return true;
    }

    private static void StripGameplayComponents(GameObject visualClone)
    {
        MonoBehaviour[] behaviours =
            visualClone.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
                Destroy(behaviour);
            }
        }

        Collider[] colliders =
            visualClone.GetComponentsInChildren<Collider>(true);

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                Destroy(targetCollider);
            }
        }

        Rigidbody[] rigidbodies =
            visualClone.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody body in rigidbodies)
        {
            if (body != null)
            {
                Destroy(body);
            }
        }

        Animator[] animators =
            visualClone.GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        ParticleSystem[] particleSystems =
            visualClone.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem system in particleSystems)
        {
            if (system != null)
            {
                Destroy(system.gameObject);
            }
        }

        TrailRenderer[] trails =
            visualClone.GetComponentsInChildren<TrailRenderer>(true);

        foreach (TrailRenderer trail in trails)
        {
            if (trail != null)
            {
                Destroy(trail.gameObject);
            }
        }

        LineRenderer[] lines =
            visualClone.GetComponentsInChildren<LineRenderer>(true);

        foreach (LineRenderer line in lines)
        {
            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }

        AudioSource[] audioSources =
            visualClone.GetComponentsInChildren<AudioSource>(true);

        foreach (AudioSource audioSource in audioSources)
        {
            if (audioSource != null)
            {
                Destroy(audioSource);
            }
        }
    }

    private void Initialize(
        Transform bodyProxy,
        Vector3 impactPoint,
        Vector3 impactNormal,
        Vector3 fallbackForward,
        Color primaryColor,
        Color secondaryColor,
        LayerMask groundMask
    )
    {
        _bodyProxy = bodyProxy;
        _allRenderers = bodyProxy.GetComponentsInChildren<Renderer>(true);
        _propertyBlock = new MaterialPropertyBlock();
        _baseRotation = bodyProxy.rotation;
        _primaryColor = primaryColor;
        _secondaryColor = secondaryColor;
        _groundMask = groundMask.value != 0
            ? groundMask
            : Physics.DefaultRaycastLayers;

        _impactDirection = impactNormal.sqrMagnitude > 0.0001f
            ? -impactNormal.normalized
            : fallbackForward.normalized;

        if (_impactDirection.sqrMagnitude <= 0.0001f)
        {
            _impactDirection = Vector3.forward;
        }

        _wingProxy = FindDescendant(bodyProxy, "FlyingBotEnemyWings");
        BuildRendererGroups();
        _bodyBottomOffset = ResolveBodyBottomOffset();

        Vector3 horizontalImpact = Vector3.ProjectOnPlane(
            _impactDirection,
            Vector3.up
        );
        _bodyVelocity = horizontalImpact * 1.15f + Vector3.up * 0.35f;

        float turnSign = Mathf.Sign(Vector3.Dot(
            _impactDirection,
            _baseRotation * Vector3.right
        ));
        if (Mathf.Approximately(turnSign, 0f))
        {
            turnSign = 1f;
        }

        _bodyAngularVelocity = new Vector3(
            155f,
            105f * turnSign,
            -235f * turnSign
        );

        if ((impactPoint - ResolveBodyCenter()).sqrMagnitude <= 9f)
        {
            _bodyVelocity += Vector3.ProjectOnPlane(
                impactPoint - ResolveBodyCenter(),
                Vector3.up
            ).normalized * 0.25f;
        }
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant != null && descendant.name == targetName)
            {
                return descendant;
            }
        }

        return null;
    }

    private void BuildRendererGroups()
    {
        if (_wingProxy == null)
        {
            _wingRenderers = new Renderer[0];
            _bodyRenderers = _allRenderers;
            return;
        }

        _wingRenderers = _wingProxy.GetComponentsInChildren<Renderer>(true);
        HashSet<Renderer> wingSet = new HashSet<Renderer>(_wingRenderers);
        List<Renderer> bodyRenderers = new List<Renderer>();

        foreach (Renderer targetRenderer in _allRenderers)
        {
            if (targetRenderer != null && !wingSet.Contains(targetRenderer))
            {
                bodyRenderers.Add(targetRenderer);
            }
        }

        _bodyRenderers = bodyRenderers.ToArray();
    }

    private float ResolveBodyBottomOffset()
    {
        bool hasBounds = TryGetBounds(_bodyRenderers, out Bounds bounds);
        if (!hasBounds)
        {
            return 0.35f;
        }

        return Mathf.Clamp(_bodyProxy.position.y - bounds.min.y, 0.15f, 1.5f);
    }

    private Vector3 ResolveBodyCenter()
    {
        return TryGetBounds(_bodyRenderers, out Bounds bounds)
            ? bounds.center
            : _bodyProxy.position;
    }

    private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (renderers == null)
        {
            return false;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null ||
                targetRenderer is ParticleSystemRenderer ||
                targetRenderer is LineRenderer ||
                targetRenderer is TrailRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        _elapsed += deltaTime;

        if (!_wingsDetached)
        {
            UpdateFailureStage();

            if (_elapsed >= FailureDuration)
            {
                DetachWings();
            }
        }
        else
        {
            UpdateWings(deltaTime);

            if (!_exploded)
            {
                UpdateFallingBody(deltaTime);
            }
            else
            {
                UpdateExplosionCleanup(deltaTime);
            }
        }

        if (!_exploded && _elapsed >= MaximumFallDuration)
        {
            TriggerGroundExplosion(
                _bodyProxy != null ? _bodyProxy.position : transform.position,
                Vector3.up
            );
        }
    }

    private void UpdateFailureStage()
    {
        if (_bodyProxy == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(_elapsed / FailureDuration);
        float vibration = Mathf.Sin(_elapsed * 105f) * progress;
        _bodyProxy.position +=
            (_baseRotation * Vector3.right) * (vibration * 0.0025f);
        _bodyProxy.rotation = _baseRotation * Quaternion.Euler(
            vibration * 1.8f,
            0f,
            -vibration * 2.6f
        );

        float pulse = 0.5f + 0.5f * Mathf.Sin(_elapsed * 92f);
        ApplyEmission(
            _allRenderers,
            Color.Lerp(
                _primaryColor * 1.4f,
                Color.white * 3.4f,
                pulse * progress
            )
        );
    }

    private void DetachWings()
    {
        _wingsDetached = true;

        if (_bodyProxy != null)
        {
            _bodyProxy.rotation = _baseRotation;
        }

        if (_wingProxy == null)
        {
            return;
        }

        Vector3 side = _baseRotation * Vector3.right;
        float sideSign = Mathf.Sign(Vector3.Dot(_impactDirection, side));
        if (Mathf.Approximately(sideSign, 0f))
        {
            sideSign = 1f;
        }

        _wingProxy.SetParent(transform, true);
        _wingVelocity =
            _impactDirection * 1.85f +
            side * (-sideSign * 2.4f) +
            Vector3.up * 2.25f;
        _wingAngularVelocity = new Vector3(
            245f,
            -315f * sideSign,
            455f * sideSign
        );

        ApplyEmission(_wingRenderers, Color.white * 3.1f);
        ApplyEmission(_bodyRenderers, _primaryColor * 2.2f);
    }

    private void UpdateFallingBody(float deltaTime)
    {
        if (_bodyProxy == null)
        {
            TriggerGroundExplosion(transform.position, Vector3.up);
            return;
        }

        _bodyVelocity += Vector3.down * (Gravity * deltaTime);
        Vector3 nextPosition =
            _bodyProxy.position + _bodyVelocity * deltaTime;
        Quaternion nextRotation = Quaternion.Euler(
            _bodyAngularVelocity * deltaTime
        ) * _bodyProxy.rotation;

        float probeDistance = _bodyBottomOffset + 0.24f;
        Vector3 probeOrigin = nextPosition + Vector3.up * 0.12f;

        if (Physics.Raycast(
            probeOrigin,
            Vector3.down,
            out RaycastHit hit,
            probeDistance,
            _groundMask,
            QueryTriggerInteraction.Ignore
        ) && hit.normal.y >= GroundNormalThreshold)
        {
            nextPosition.y = hit.point.y + _bodyBottomOffset;
            _bodyProxy.SetPositionAndRotation(nextPosition, nextRotation);
            TriggerGroundExplosion(hit.point, hit.normal);
            return;
        }

        _bodyProxy.SetPositionAndRotation(nextPosition, nextRotation);

        float flicker = Mathf.PingPong(_elapsed * 11f, 1f);
        ApplyEmission(
            _bodyRenderers,
            Color.Lerp(_primaryColor * 1.6f, Color.black, flicker)
        );
    }

    private void UpdateWings(float deltaTime)
    {
        if (_wingProxy == null || _wingSettled)
        {
            if (_wingSettled)
            {
                _wingSettledElapsed += deltaTime;
                if (_wingSettledElapsed >= WingCleanupDelay)
                {
                    SetRenderersEnabled(_wingRenderers, false);
                }
            }
            return;
        }

        _wingVelocity += Vector3.down * (Gravity * 0.72f * deltaTime);
        Vector3 currentPosition = _wingProxy.position;
        Vector3 displacement = _wingVelocity * deltaTime;

        if (displacement.sqrMagnitude > 0.000001f &&
            Physics.SphereCast(
                currentPosition,
                0.12f,
                displacement.normalized,
                out RaycastHit hit,
                displacement.magnitude + 0.08f,
                _groundMask,
                QueryTriggerInteraction.Ignore
            ))
        {
            _wingProxy.position = hit.point + hit.normal * 0.08f;
            _wingSettled = true;
            _wingSettledElapsed = 0f;
            ApplyEmission(_wingRenderers, _secondaryColor * 0.35f);
            return;
        }

        _wingProxy.position = currentPosition + displacement;
        _wingProxy.rotation = Quaternion.Euler(
            _wingAngularVelocity * deltaTime
        ) * _wingProxy.rotation;

        float heat = Mathf.Clamp01(1f - (_elapsed - FailureDuration) * 0.65f);
        ApplyEmission(
            _wingRenderers,
            Color.Lerp(Color.black, _secondaryColor * 1.8f, heat)
        );
    }

    private void TriggerGroundExplosion(Vector3 point, Vector3 normal)
    {
        if (_exploded)
        {
            return;
        }

        _exploded = true;
        _explosionElapsed = 0f;
        SetRenderersEnabled(_bodyRenderers, false);

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : Vector3.up;
        FlyingBotCombatJuiceFX.SpawnDeath(
            point + safeNormal * 0.08f,
            safeNormal,
            Color.Lerp(Color.white, _primaryColor, 0.58f),
            false
        );
    }

    private void UpdateExplosionCleanup(float deltaTime)
    {
        _explosionElapsed += deltaTime;

        if (!_aftershockTriggered &&
            _explosionElapsed >= AftershockDelay)
        {
            _aftershockTriggered = true;
            Vector3 position = _bodyProxy != null
                ? _bodyProxy.position
                : transform.position;
            FlyingBotCombatJuiceFX.SpawnDeath(
                position,
                Vector3.up,
                Color.Lerp(Color.white, _secondaryColor, 0.52f),
                true
            );
        }

        if (_explosionElapsed >= CleanupDelay)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyEmission(Renderer[] renderers, Color emission)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, emission);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private static void SetRenderersEnabled(
        Renderer[] renderers,
        bool enabled
    )
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = enabled;
            }
        }
    }
}
