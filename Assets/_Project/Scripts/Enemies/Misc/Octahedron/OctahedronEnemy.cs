#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;

/// <summary>
/// Static contact-hazard enemy.
/// The gameplay root never moves; only the visual rotates and pulses.
/// </summary>
[DisallowMultipleComponent]
public sealed class OctahedronEnemy : MonoBehaviour
{
    [Header("Core References")]
    public EnemyTarget enemyTarget;
    public EnemyContactDamage contactDamage;
    public Transform visualRoot;

    [Header("Idle Presentation")]
    [Min(0f)]
    public float rotationSpeed = 18f;

    [Min(0f)]
    public float breathingFrequency = 0.75f;

    [Range(0f, 0.08f)]
    public float breathingScale = 0.018f;

    [Min(0f)]
    public float minimumEmissionMultiplier = 0.82f;

    [Min(0f)]
    public float maximumEmissionMultiplier = 1.22f;

    [Header("Proximity Warning")]
    [Min(0.2f)]
    public float warningOuterDistance = 4f;

    [Min(0.1f)]
    public float warningInnerDistance = 2f;

    [Min(1f)]
    public float warningEmissionMultiplier = 2.45f;

    [Min(1f)]
    public float warningRotationMultiplier = 1.85f;

    [ColorUsage(true, true)]
    public Color warningColor =
        new Color(1.35f, 0.24f, 0.025f, 1f);

    [Header("Contact Response")]
    [Min(0.03f)]
    public float contactResponseDuration = 0.18f;

    [Range(0f, 0.2f)]
    public float contactScaleKick = 0.075f;

    [Min(1f)]
    public float contactEmissionMultiplier = 2.1f;

    [Header("Ranged Hit Confirmation")]
    [Min(0.04f)]
    public float hipfireHitDuration = 0.10f;

    [Min(0.05f)]
    public float adsHitDuration = 0.16f;

    [Min(1f)]
    public float hipfireHitEmissionMultiplier = 2.8f;

    [Min(1f)]
    public float adsHitEmissionMultiplier = 3.65f;

    [ColorUsage(true, true)]
    public Color hipfireHitColor =
        new Color(0.10f, 1.15f, 1.75f, 1f);

    [ColorUsage(true, true)]
    public Color adsHitColor =
        new Color(2.0f, 0.08f, 1.15f, 1f);

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private Renderer[] _renderers = Array.Empty<Renderer>();
    private Color[] _baseEmissionColors = Array.Empty<Color>();
    private Color[] _baseColors = Array.Empty<Color>();
    private MaterialPropertyBlock _propertyBlock;
    private Vector3 _baseVisualScale = Vector3.one;
    private float _contactResponseRemaining;
    private Transform _playerTransform;
    private float _playerResolveCooldown;
    private float _hitResponseRemaining;
    private float _hitResponseDuration;
    private float _hitResponseStrength;
    private float _hitCharge01;
    private bool _hitFiredAsAds;

    private void Awake()
    {
        ResolveReferences();

        if (visualRoot != null)
        {
            _baseVisualScale = visualRoot.localScale;
        }

        CacheRenderers();

        if (contactDamage != null)
        {
            contactDamage.SetDamageEnabled(true);
            contactDamage.PlayerDamaged += HandlePlayerDamaged;
        }
    }

    private void OnDestroy()
    {
        if (contactDamage != null)
        {
            contactDamage.PlayerDamaged -= HandlePlayerDamaged;
        }
    }

    private void Update()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        UpdateIdlePresentation();
    }

    private void ResolveReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        if (contactDamage == null)
        {
            contactDamage =
                GetComponentInChildren<EnemyContactDamage>(true);
        }

        if (visualRoot == null)
        {
            Transform candidate = transform.Find("VisualRoot");
            visualRoot = candidate != null ? candidate : transform;
        }
    }

    private void CacheRenderers()
    {
        if (visualRoot == null)
        {
            return;
        }

        _renderers =
            visualRoot.GetComponentsInChildren<Renderer>(true);

        _baseEmissionColors = new Color[_renderers.Length];
        _baseColors = new Color[_renderers.Length];
        _propertyBlock = new MaterialPropertyBlock();

        for (int index = 0; index < _renderers.Length; index++)
        {
            Renderer targetRenderer = _renderers[index];

            if (targetRenderer == null ||
                targetRenderer.sharedMaterial == null)
            {
                _baseEmissionColors[index] = Color.black;
                _baseColors[index] = Color.white;
                continue;
            }

            Material material = targetRenderer.sharedMaterial;

            _baseEmissionColors[index] =
                material.HasProperty(EmissionColorId)
                    ? material.GetColor(EmissionColorId)
                    : Color.black;

            _baseColors[index] =
                material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.white;
        }
    }

    private void UpdateIdlePresentation()
    {
        if (visualRoot == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        float proximity01 =
            ResolveProximityWarning(deltaTime);

        float warningFrequency =
            Mathf.Lerp(2.6f, 8.5f, proximity01);
        float warningPulse =
            Mathf.Sin(Time.time * warningFrequency * Mathf.PI * 2f) *
            0.5f + 0.5f;
        float warning01 =
            proximity01 * Mathf.Lerp(0.42f, 1f, warningPulse);

        if (rotationSpeed > 0f)
        {
            float warningSpeedMultiplier = Mathf.Lerp(
                1f,
                warningRotationMultiplier,
                proximity01);

            visualRoot.Rotate(
                Vector3.up,
                rotationSpeed * warningSpeedMultiplier * deltaTime,
                Space.Self);
        }

        if (_contactResponseRemaining > 0f)
        {
            _contactResponseRemaining = Mathf.Max(
                0f,
                _contactResponseRemaining - deltaTime);
        }

        if (_hitResponseRemaining > 0f)
        {
            _hitResponseRemaining = Mathf.Max(
                0f,
                _hitResponseRemaining - deltaTime);
        }

        float contact01 = contactResponseDuration > 0.001f
            ? Mathf.Clamp01(
                _contactResponseRemaining /
                contactResponseDuration)
            : 0f;

        float contactKick =
            Mathf.Sin(contact01 * Mathf.PI) *
            contactScaleKick;

        float breathingPhase =
            Time.time * breathingFrequency * Mathf.PI * 2f;

        float breathing01 =
            Mathf.Sin(breathingPhase) * 0.5f + 0.5f;

        float scaleMultiplier =
            1f + breathing01 * breathingScale + contactKick;

        visualRoot.localScale =
            _baseVisualScale * scaleMultiplier;

        float emissionMultiplier = Mathf.Lerp(
            minimumEmissionMultiplier,
            maximumEmissionMultiplier,
            breathing01);

        emissionMultiplier = Mathf.Lerp(
            emissionMultiplier,
            contactEmissionMultiplier,
            contact01);

        Color presentationColor = Color.white;
        float bodyTintAmount = 0f;

        if (warning01 > 0f)
        {
            emissionMultiplier = Mathf.Max(
                emissionMultiplier,
                Mathf.Lerp(
                    1f,
                    warningEmissionMultiplier,
                    warning01));
            presentationColor = warningColor;
            bodyTintAmount = warning01 * 0.24f;
        }

        if (contact01 > 0f)
        {
            presentationColor = warningColor;
            bodyTintAmount = Mathf.Max(
                bodyTintAmount,
                contact01 * 0.36f);
        }

        if (_hitResponseRemaining > 0f &&
            _hitResponseDuration > 0.001f)
        {
            float hitRemaining01 = Mathf.Clamp01(
                _hitResponseRemaining / _hitResponseDuration);
            float hitEnvelope = Mathf.Pow(hitRemaining01, 0.62f);
            float hitProgress = 1f - hitRemaining01;
            float strengthMultiplier = Mathf.Lerp(
                0.82f,
                1.15f,
                _hitResponseStrength / 1.5f);
            float chargeMultiplier = _hitFiredAsAds
                ? Mathf.Lerp(0.86f, 1.06f, _hitCharge01)
                : 1f;
            float hitEmission =
                (_hitFiredAsAds
                    ? adsHitEmissionMultiplier
                    : hipfireHitEmissionMultiplier) *
                strengthMultiplier *
                chargeMultiplier;

            emissionMultiplier = Mathf.Max(
                emissionMultiplier,
                Mathf.Lerp(1f, hitEmission, hitEnvelope));

            presentationColor = _hitFiredAsAds
                ? Color.Lerp(
                    Color.white,
                    adsHitColor,
                    Mathf.SmoothStep(0f, 1f, hitProgress))
                : hipfireHitColor;
            bodyTintAmount = Mathf.Max(
                bodyTintAmount,
                hitEnvelope * (_hitFiredAsAds ? 0.70f : 0.56f));
        }

        ApplyPresentation(
            emissionMultiplier,
            presentationColor,
            bodyTintAmount);
    }

    private void ApplyPresentation(
        float emissionMultiplier,
        Color presentationColor,
        float bodyTintAmount)
    {
        if (_propertyBlock == null)
        {
            return;
        }

        for (int index = 0; index < _renderers.Length; index++)
        {
            Renderer targetRenderer = _renderers[index];

            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(_propertyBlock);

            if (_baseEmissionColors[index].maxColorComponent > 0f)
            {
                Color emissionTint = Color.Lerp(
                    Color.white,
                    presentationColor,
                    Mathf.Clamp01(bodyTintAmount * 1.4f));

                _propertyBlock.SetColor(
                    EmissionColorId,
                    _baseEmissionColors[index] *
                    emissionTint *
                    emissionMultiplier);
            }

            Color baseTint = Color.Lerp(
                Color.white,
                presentationColor,
                Mathf.Clamp01(bodyTintAmount));

            _propertyBlock.SetColor(
                BaseColorId,
                _baseColors[index] * baseTint);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    public void TriggerHitFeedback(
        bool firedAsAds,
        float charge01,
        float strength)
    {
        _hitFiredAsAds = firedAsAds;
        _hitCharge01 = Mathf.Clamp01(charge01);
        _hitResponseStrength = Mathf.Clamp(strength, 0.35f, 1.5f);
        _hitResponseDuration = firedAsAds
            ? adsHitDuration
            : hipfireHitDuration;
        _hitResponseRemaining = _hitResponseDuration;
    }

    private float ResolveProximityWarning(float deltaTime)
    {
        if (_playerTransform == null)
        {
            _playerResolveCooldown -= deltaTime;

            if (_playerResolveCooldown <= 0f)
            {
                PlayerHealth playerHealth = PlayerHealth.Instance;
                _playerTransform = playerHealth != null
                    ? playerHealth.transform
                    : null;
                _playerResolveCooldown = 0.5f;
            }
        }

        if (_playerTransform == null)
        {
            return 0f;
        }

        float distance = Vector3.Distance(
            visualRoot.position,
            _playerTransform.position);

        return 1f - Mathf.InverseLerp(
            warningInnerDistance,
            warningOuterDistance,
            distance);
    }

    private void HandlePlayerDamaged(GameObject playerObject)
    {
        _contactResponseRemaining =
            Mathf.Max(
                _contactResponseRemaining,
                contactResponseDuration);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        breathingFrequency = Mathf.Max(0f, breathingFrequency);
        breathingScale = Mathf.Clamp(breathingScale, 0f, 0.08f);
        minimumEmissionMultiplier =
            Mathf.Max(0f, minimumEmissionMultiplier);
        maximumEmissionMultiplier =
            Mathf.Max(
                minimumEmissionMultiplier,
                maximumEmissionMultiplier);
        warningInnerDistance =
            Mathf.Max(0.1f, warningInnerDistance);
        warningOuterDistance =
            Mathf.Max(
                warningInnerDistance + 0.1f,
                warningOuterDistance);
        warningEmissionMultiplier =
            Mathf.Max(1f, warningEmissionMultiplier);
        warningRotationMultiplier =
            Mathf.Max(1f, warningRotationMultiplier);
        contactResponseDuration =
            Mathf.Max(0.03f, contactResponseDuration);
        contactScaleKick =
            Mathf.Clamp(contactScaleKick, 0f, 0.2f);
        contactEmissionMultiplier =
            Mathf.Max(1f, contactEmissionMultiplier);
        hipfireHitDuration =
            Mathf.Max(0.04f, hipfireHitDuration);
        adsHitDuration =
            Mathf.Max(0.05f, adsHitDuration);
        hipfireHitEmissionMultiplier =
            Mathf.Max(1f, hipfireHitEmissionMultiplier);
        adsHitEmissionMultiplier =
            Mathf.Max(1f, adsHitEmissionMultiplier);
    }
#endif
}
