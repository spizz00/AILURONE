using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Independent death proxy for the Octahedron enemy. It telegraphs a delayed
/// blast, expands a visible wavefront, applies non-lethal damage and adds a
/// movement-friendly launch velocity when the wave reaches the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class OctahedronDeathBlast : MonoBehaviour
{
    [Header("Visual References")]
    public Transform deathVisual;
    public Transform chargeCore;
    public Transform flashCore;
    public Renderer flashRenderer;
    public Transform shockwaveShell;
    public Renderer shockwaveRenderer;
    public Transform echoShockwaveShell;
    public Renderer echoShockwaveRenderer;
    public LineRenderer maximumRangeRing;
    public LineRenderer sweetSpotRing;
    public LineRenderer coreZoneRing;
    public LineRenderer expandingWaveRing;
    public Light blastLight;
    public ParticleSystem chargeParticles;
    public ParticleSystem blastParticles;

    [Header("Audio References")]
    public AudioSource chargeAudioSource;
    public AudioClip chargeLoopClip;
    public AudioClip detonationClip;
    public AudioClip shockwaveHitClip;

    [Header("Timing")]
    [Min(0.2f)]
    public float chargeDuration = 1.1f;

    [Min(0.1f)]
    public float expansionDuration = 0.4f;

    [Min(0f)]
    public float lingerDuration = 0.35f;

    [Header("Blast Geometry")]
    public Vector3 blastCenterOffset = new Vector3(0f, 1.2f, 0f);

    [Min(1f)]
    public float maximumRadius = 13f;

    [Min(0.1f)]
    public float waveThickness = 1.25f;

    [Min(0.1f)]
    public float coreZoneRadius = 3f;

    [Min(0.2f)]
    public float sweetSpotRadius = 7f;

    [Header("Non-Lethal Damage")]
    [Min(0f)]
    public float coreDamage = 70f;

    [Min(0f)]
    public float sweetSpotDamage = 55f;

    [Min(0f)]
    public float edgeDamage = 40f;

    [Header("Enemy Blast Damage")]
    [Min(0f)]
    public float enemyCoreDamage = 360f;

    [Min(0f)]
    public float enemySweetSpotDamage = 240f;

    [Min(0f)]
    public float enemyEdgeDamage = 120f;

    [Min(1f)]
    public float octahedronDamageMultiplier = 4.2f;

    [Header("Enemy Blast Knockback")]
    [Min(0f)]
    public float enemyCoreKnockbackDistance = 8f;

    [Min(0f)]
    public float enemySweetSpotKnockbackDistance = 14f;

    [Min(0f)]
    public float enemyEdgeKnockbackDistance = 7f;

    [Header("Blast Launch")]
    [Min(0f)]
    public float coreHorizontalSpeed = 14f;

    [Min(0f)]
    public float sweetSpotHorizontalSpeed = 38f;

    [Min(0f)]
    public float edgeHorizontalSpeed = 21f;

    [Min(0f)]
    public float coreUpwardSpeed = 22f;

    [Min(0f)]
    public float sweetSpotUpwardSpeed = 14f;

    [Min(0f)]
    public float edgeUpwardSpeed = 9f;

    [Min(0f)]
    public float launchGraceTime = 0.22f;

    [Min(0f)]
    public float noDragDuration = 0.55f;

    [Header("Charge Presentation")]
    [Min(0f)]
    public float startingSpinSpeed = 24f;

    [Min(0f)]
    public float finalSpinSpeed = 260f;

    [Min(1f)]
    public float finalEmissionMultiplier = 6f;

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");
    private static readonly int EffectColorId =
        Shader.PropertyToID("_Color");

    private Renderer[] _deathRenderers = Array.Empty<Renderer>();
    private Color[] _baseEmissionColors = Array.Empty<Color>();
    private MaterialPropertyBlock _propertyBlock;
    private Vector3 _baseVisualScale = Vector3.one;
    private Vector3 _baseCoreScale = Vector3.one;
    private float _elapsed;
    private float _previousWaveRadius;
    private bool _detonated;
    private bool _playerHit;
    private int _enemyLayerMask;

    private readonly Collider[] _enemyOverlapBuffer = new Collider[128];
    private readonly HashSet<EnemyTarget> _hitEnemies =
        new HashSet<EnemyTarget>();

    private const float OrdinaryEnemyKnockbackMultiplier = 0.7f;
    private const float HeavyEnemyKnockbackMultiplier = 0.18f;
    private const float OrdinaryEnemyUpwardBias = 0.55f;
    private const float HeavyEnemyUpwardBias = 0.08f;
    private const float EnemyStunDuration = 0.65f;
    private const float EnemyKnockbackDuration = 0.4f;
    private const float EnemyEnvironmentCreditDuration = 5f;

    private void Awake()
    {
        _enemyLayerMask = LayerMask.GetMask("Enemy");
        ResolveReferences();
        CacheDeathVisual();
        InitializePresentation();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (!_detonated)
        {
            float charge01 = Mathf.Clamp01(
                _elapsed / Mathf.Max(0.01f, chargeDuration));

            UpdateCharge(charge01);

            if (charge01 >= 1f)
            {
                Detonate();
            }

            return;
        }

        float expansionElapsed = _elapsed - chargeDuration;
        float expansion01 = Mathf.Clamp01(
            expansionElapsed / Mathf.Max(0.01f, expansionDuration));

        UpdateExpansion(expansion01);

        if (expansionElapsed >= expansionDuration + lingerDuration)
        {
            Destroy(gameObject);
        }
    }

    private void ResolveReferences()
    {
        if (deathVisual == null)
        {
            deathVisual = transform.Find("DeathVisual");
        }

        if (chargeCore == null)
        {
            chargeCore = transform.Find("ChargeCore");
        }

        if (flashCore == null)
        {
            flashCore = transform.Find("FlashCore");
        }

        if (flashRenderer == null && flashCore != null)
        {
            flashRenderer = flashCore.GetComponent<Renderer>();
        }

        if (shockwaveShell == null)
        {
            shockwaveShell = transform.Find("ShockwaveShell");
        }

        if (shockwaveRenderer == null && shockwaveShell != null)
        {
            shockwaveRenderer = shockwaveShell.GetComponent<Renderer>();
        }

        if (echoShockwaveShell == null)
        {
            echoShockwaveShell = transform.Find("EchoShockwaveShell");
        }

        if (echoShockwaveRenderer == null && echoShockwaveShell != null)
        {
            echoShockwaveRenderer =
                echoShockwaveShell.GetComponent<Renderer>();
        }

        if (blastLight == null)
        {
            blastLight = GetComponentInChildren<Light>(true);
        }

        if (chargeParticles == null)
        {
            Transform chargeParticleTransform =
                transform.Find("ChargeParticles");

            if (chargeParticleTransform != null)
            {
                chargeParticles =
                    chargeParticleTransform.GetComponent<ParticleSystem>();
            }
        }

        if (blastParticles == null)
        {
            Transform blastParticleTransform =
                transform.Find("BlastParticles");

            if (blastParticleTransform != null)
            {
                blastParticles =
                    blastParticleTransform.GetComponent<ParticleSystem>();
            }
        }

        if (chargeAudioSource == null)
        {
            Transform chargeAudioTransform = transform.Find("ChargeAudio");

            if (chargeAudioTransform != null)
            {
                chargeAudioSource =
                    chargeAudioTransform.GetComponent<AudioSource>();
            }
        }

    }

    private void CacheDeathVisual()
    {
        if (deathVisual == null)
        {
            return;
        }

        _baseVisualScale = deathVisual.localScale;
        _deathRenderers =
            deathVisual.GetComponentsInChildren<Renderer>(true);
        _baseEmissionColors = new Color[_deathRenderers.Length];
        _propertyBlock = new MaterialPropertyBlock();

        for (int index = 0; index < _deathRenderers.Length; index++)
        {
            Renderer targetRenderer = _deathRenderers[index];

            if (targetRenderer == null ||
                targetRenderer.sharedMaterial == null ||
                !targetRenderer.sharedMaterial.HasProperty(EmissionColorId))
            {
                _baseEmissionColors[index] = Color.black;
                continue;
            }

            _baseEmissionColors[index] =
                targetRenderer.sharedMaterial.GetColor(EmissionColorId);
        }

        if (chargeCore != null)
        {
            _baseCoreScale = chargeCore.localScale;
        }
    }

    private void InitializePresentation()
    {
        SetRingRadius(maximumRangeRing, maximumRadius);
        SetRingRadius(sweetSpotRing, sweetSpotRadius);
        SetRingRadius(coreZoneRing, coreZoneRadius);
        SetRingRadius(expandingWaveRing, 0.01f);

        SetRingAlpha(maximumRangeRing, 0f);
        SetRingAlpha(sweetSpotRing, 0f);
        SetRingAlpha(coreZoneRing, 0f);
        SetRingAlpha(expandingWaveRing, 0f);

        if (shockwaveShell != null)
        {
            shockwaveShell.gameObject.SetActive(false);
        }

        if (echoShockwaveShell != null)
        {
            echoShockwaveShell.gameObject.SetActive(false);
        }

        if (flashCore != null)
        {
            flashCore.gameObject.SetActive(false);
        }

        if (chargeParticles != null)
        {
            chargeParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            chargeParticles.Play(true);
        }

        blastParticles?.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        if (chargeAudioSource != null && chargeLoopClip != null)
        {
            chargeAudioSource.clip = chargeLoopClip;
            chargeAudioSource.loop = false;
            chargeAudioSource.pitch = 1f;
            chargeAudioSource.volume = 0.95f;
            chargeAudioSource.Play();
        }

        if (blastLight != null)
        {
            blastLight.intensity = 0f;
        }
    }

    private void UpdateCharge(float charge01)
    {
        float easedCharge = charge01 * charge01;

        if (deathVisual != null)
        {
            float spinSpeed = Mathf.Lerp(
                startingSpinSpeed,
                finalSpinSpeed,
                easedCharge);

            deathVisual.Rotate(
                Vector3.up,
                spinSpeed * Time.deltaTime,
                Space.Self);

            float pulse =
                Mathf.Sin(Time.time * Mathf.Lerp(8f, 28f, charge01)) *
                Mathf.Lerp(0.015f, 0.055f, charge01);

            deathVisual.localScale = _baseVisualScale *
                (1f + pulse - easedCharge * 0.28f);
        }

        if (chargeCore != null)
        {
            float corePulse =
                1f + Mathf.Sin(Time.time * 34f) * 0.07f * charge01;
            float coreScale =
                Mathf.Lerp(0.08f, 1.35f, easedCharge) * corePulse;
            chargeCore.localScale = _baseCoreScale * coreScale;
        }

        if (chargeParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                chargeParticles.emission;
            emission.rateOverTime = Mathf.Lerp(22f, 88f, easedCharge);
            chargeParticles.transform.Rotate(
                Vector3.up,
                Mathf.Lerp(45f, 190f, easedCharge) * Time.deltaTime,
                Space.Self);
        }

        ApplyDeathEmission(
            Mathf.Lerp(1f, finalEmissionMultiplier, easedCharge));

        SetRingAlpha(
            maximumRangeRing,
            Mathf.Lerp(0.08f, 0.36f, charge01));
        SetRingAlpha(
            sweetSpotRing,
            Mathf.Lerp(0.12f, 0.72f, charge01));
        SetRingAlpha(
            coreZoneRing,
            Mathf.Lerp(0.16f, 0.88f, charge01));

        if (blastLight != null)
        {
            float lightPulse =
                0.84f + Mathf.Sin(Time.time * 31f) * 0.16f * charge01;
            blastLight.intensity =
                Mathf.Lerp(0f, 12f, easedCharge) * lightPulse;
        }
    }

    private void Detonate()
    {
        if (_detonated)
        {
            return;
        }

        _detonated = true;
        _previousWaveRadius = 0f;

        if (deathVisual != null)
        {
            deathVisual.gameObject.SetActive(false);
        }

        if (chargeCore != null)
        {
            chargeCore.localScale = _baseCoreScale * 1.65f;
        }

        if (shockwaveShell != null)
        {
            shockwaveShell.gameObject.SetActive(true);
            shockwaveShell.localScale = Vector3.zero;
        }

        if (echoShockwaveShell != null)
        {
            echoShockwaveShell.gameObject.SetActive(true);
            echoShockwaveShell.localScale = Vector3.zero;
        }

        if (flashCore != null)
        {
            flashCore.gameObject.SetActive(true);
            flashCore.localScale = Vector3.one * 0.08f;
        }

        chargeParticles?.Stop(
            true,
            ParticleSystemStopBehavior.StopEmitting);

        if (chargeAudioSource != null)
        {
            chargeAudioSource.Stop();
        }

        PlayDetachedSpatialClip(
            detonationClip,
            transform.TransformPoint(blastCenterOffset),
            1f,
            1f,
            3f,
            34f);

        SetEffectColor(
            shockwaveRenderer,
            new Color(2.5f, 0.06f, 0.9f, 0.52f));
        SetEffectColor(
            echoShockwaveRenderer,
            new Color(0.08f, 2.2f, 2.6f, 0.34f));
        SetEffectColor(
            flashRenderer,
            new Color(3.2f, 2.2f, 2.8f, 1f));

        SetRingAlpha(maximumRangeRing, 0.18f);
        SetRingAlpha(sweetSpotRing, 0.28f);
        SetRingAlpha(coreZoneRing, 0.38f);
        SetRingAlpha(expandingWaveRing, 1f);

        if (blastLight != null)
        {
            blastLight.intensity = 34f;
        }

        blastParticles?.Play(true);
    }

    private void UpdateExpansion(float expansion01)
    {
        float easedExpansion =
            1f - Mathf.Pow(1f - expansion01, 2f);

        float currentRadius = maximumRadius * easedExpansion;

        if (shockwaveShell != null)
        {
            shockwaveShell.localScale =
                Vector3.one * currentRadius * 2f;
        }

        float echoExpansion01 = Mathf.Clamp01(
            (expansion01 - 0.08f) / 0.92f);
        float easedEchoExpansion =
            1f - Mathf.Pow(1f - echoExpansion01, 2f);

        if (echoShockwaveShell != null)
        {
            echoShockwaveShell.localScale =
                Vector3.one * maximumRadius * easedEchoExpansion * 2f;
        }

        SetRingRadius(expandingWaveRing, currentRadius);
        SetRingAlpha(expandingWaveRing, 1f - expansion01 * 0.7f);
        if (expandingWaveRing != null)
        {
            float waveWidth = Mathf.Lerp(0.46f, 0.08f, expansion01);
            expandingWaveRing.startWidth = waveWidth;
            expandingWaveRing.endWidth = waveWidth;
        }
        SetRingAlpha(maximumRangeRing, 0.18f * (1f - expansion01));
        SetRingAlpha(sweetSpotRing, 0.28f * (1f - expansion01));
        SetRingAlpha(coreZoneRing, 0.38f * (1f - expansion01));

        SetEffectAlpha(
            shockwaveRenderer,
            Mathf.Lerp(0.52f, 0f, expansion01));
        SetEffectAlpha(
            echoShockwaveRenderer,
            Mathf.Lerp(0.34f, 0f, echoExpansion01));

        float flash01 = Mathf.Clamp01(expansion01 / 0.28f);

        if (flashCore != null)
        {
            float flashScale =
                Mathf.Sin(flash01 * Mathf.PI) * 3.8f;
            flashCore.localScale = Vector3.one * flashScale;
        }

        SetEffectAlpha(flashRenderer, 1f - flash01);

        if (chargeCore != null)
        {
            chargeCore.localScale =
                _baseCoreScale * Mathf.Lerp(1.65f, 0f, expansion01);
        }

        if (blastLight != null)
        {
            blastLight.intensity =
                Mathf.Lerp(34f, 0f, Mathf.Sqrt(expansion01));
        }

        TryHitPlayer(_previousWaveRadius, currentRadius);
        TryHitEnemies(_previousWaveRadius, currentRadius);
        _previousWaveRadius = currentRadius;
    }

    private void TryHitEnemies(
        float previousRadius,
        float currentRadius)
    {
        if (_enemyLayerMask == 0)
        {
            return;
        }

        Vector3 blastCenter = transform.TransformPoint(blastCenterOffset);
        float halfThickness = waveThickness * 0.5f;
        int overlapCount = Physics.OverlapSphereNonAlloc(
            blastCenter,
            currentRadius + halfThickness,
            _enemyOverlapBuffer,
            _enemyLayerMask,
            QueryTriggerInteraction.Collide);

        float minimumRadius = Mathf.Max(
            0f,
            previousRadius - halfThickness);
        float maximumWaveRadius = currentRadius + halfThickness;

        for (int index = 0; index < overlapCount; index++)
        {
            Collider enemyCollider = _enemyOverlapBuffer[index];

            if (enemyCollider == null)
            {
                continue;
            }

            EnemyTarget enemyTarget =
                enemyCollider.GetComponentInParent<EnemyTarget>();

            if (enemyTarget == null ||
                enemyTarget.IsDead ||
                _hitEnemies.Contains(enemyTarget))
            {
                continue;
            }

            Vector3 targetCenter = enemyCollider.bounds.center;
            Vector3 hitPoint = enemyCollider.ClosestPoint(blastCenter);
            float distance = Vector3.Distance(blastCenter, hitPoint);

            if (distance < minimumRadius ||
                distance > maximumWaveRadius)
            {
                continue;
            }

            _hitEnemies.Add(enemyTarget);
            HitEnemy(
                enemyTarget,
                blastCenter,
                targetCenter,
                hitPoint,
                distance);
        }
    }

    private void HitEnemy(
        EnemyTarget enemyTarget,
        Vector3 blastCenter,
        Vector3 targetCenter,
        Vector3 hitPoint,
        float distance)
    {
        ResolveEnemyBlastValues(
            distance,
            out float damage,
            out float knockbackDistance);

        bool isOctahedron =
            enemyTarget.GetComponent<OctahedronEnemy>() != null;

        if (isOctahedron)
        {
            damage *= octahedronDamageMultiplier;
        }

        Vector3 outwardDirection = targetCenter - blastCenter;

        if (outwardDirection.sqrMagnitude <= 0.0001f)
        {
            outwardDirection = enemyTarget.transform.forward;
        }

        outwardDirection.Normalize();

        float actualDamage = enemyTarget.TakeDamageFromOctahedronBlast(
            damage,
            hitPoint,
            -outwardDirection);

        if (!enemyTarget.IsDead)
        {
            ApplyEnemyKnockback(
                enemyTarget,
                outwardDirection,
                knockbackDistance);
        }

        Debug.Log(
            $"[OctahedronDeathBlast] Enemy {enemyTarget.name} hit at " +
            $"{distance:F1}m. Damage={actualDamage:F0}.",
            this);
    }

    private static void ApplyEnemyKnockback(
        EnemyTarget enemyTarget,
        Vector3 outwardDirection,
        float knockbackDistance)
    {
        EnemyControlEffectController controller =
            enemyTarget.GetComponent<EnemyControlEffectController>();

        if (controller == null)
        {
            return;
        }

        bool isHeavyEnemy =
            enemyTarget.GetComponent<GroundBotEnemy>() != null ||
            enemyTarget.GetComponent<OphanimEnemy>() != null;

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            outwardDirection,
            Vector3.up);

        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            horizontalDirection = Vector3.forward;
        }

        float upwardBias = isHeavyEnemy
            ? HeavyEnemyUpwardBias
            : OrdinaryEnemyUpwardBias;
        float distanceMultiplier = isHeavyEnemy
            ? HeavyEnemyKnockbackMultiplier
            : OrdinaryEnemyKnockbackMultiplier;
        Vector3 launchDirection =
            (horizontalDirection.normalized + Vector3.up * upwardBias)
            .normalized;

        controller.ApplyStunAndKnockback(
            EnemyStunDuration,
            launchDirection,
            knockbackDistance * distanceMultiplier,
            EnemyKnockbackDuration,
            EnemyEnvironmentCreditDuration);
    }

    private void TryHitPlayer(
        float previousRadius,
        float currentRadius)
    {
        if (_playerHit)
        {
            return;
        }

        PlayerHealth playerHealth = PlayerHealth.Instance;

        if (playerHealth == null || playerHealth.IsRewinding)
        {
            return;
        }

        Vector3 blastCenter = transform.TransformPoint(blastCenterOffset);
        CharacterController characterController =
            playerHealth.GetComponentInParent<CharacterController>();

        Vector3 playerCenter = characterController != null
            ? characterController.bounds.center
            : playerHealth.transform.position + Vector3.up;

        float playerDistance = Vector3.Distance(blastCenter, playerCenter);
        float halfThickness = waveThickness * 0.5f;
        float innerRadius = Mathf.Max(0f, previousRadius - halfThickness);
        float outerRadius = currentRadius + halfThickness;

        if (playerDistance < innerRadius || playerDistance > outerRadius)
        {
            return;
        }

        _playerHit = true;
        ApplyBlastToPlayer(playerHealth, playerCenter, playerDistance);
    }

    private void ApplyBlastToPlayer(
        PlayerHealth playerHealth,
        Vector3 playerCenter,
        float playerDistance)
    {
        ResolveBlastValues(
            playerDistance,
            out float damage,
            out float horizontalSpeed,
            out float upwardSpeed);

        float maximumSafeDamage =
            Mathf.Max(0f, playerHealth.currentHealth - 1f);
        float actualDamage = Mathf.Min(damage, maximumSafeDamage);

        if (actualDamage > 0f)
        {
            playerHealth.TakeDamage(actualDamage);
        }

        FirstPersonController controller =
            playerHealth.GetComponentInParent<FirstPersonController>();

        if (controller == null)
        {
            controller =
                playerHealth.GetComponentInChildren<FirstPersonController>();
        }

        if (controller == null)
        {
            return;
        }

        Vector3 blastCenter = transform.TransformPoint(blastCenterOffset);
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            playerCenter - blastCenter,
            Vector3.up);

        if (horizontalDirection.sqrMagnitude <= 0.16f)
        {
            horizontalDirection = Vector3.ProjectOnPlane(
                playerHealth.transform.forward,
                Vector3.up);
        }

        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            horizontalDirection = Vector3.forward;
        }

        Vector3 launchVelocity =
            horizontalDirection.normalized * horizontalSpeed +
            Vector3.up * upwardSpeed;

        controller.ApplyGravityLiftExitBoost(
            launchVelocity,
            launchGraceTime,
            noDragDuration);

        float feedbackStrength = Mathf.Lerp(
            1f,
            0.65f,
            Mathf.Clamp01(playerDistance / maximumRadius));

        controller.RequestExternalFOV(
            Mathf.Lerp(108f, 116f, feedbackStrength),
            28f,
            0.32f);

        if (VisualFeedbackController.Instance != null)
        {
            VisualFeedbackController.Instance
                .TriggerOctahedronBlastFeedback(feedbackStrength);
        }

        PlayDetachedSpatialClip(
            shockwaveHitClip,
            playerCenter,
            0.62f * feedbackStrength,
            1f,
            1f,
            12f);

        Debug.Log(
            $"[OctahedronDeathBlast] Player hit at {playerDistance:F1}m. " +
            $"Damage={actualDamage:F0}, Launch={launchVelocity.magnitude:F1}.",
            this);
    }

    private void ResolveBlastValues(
        float distance,
        out float damage,
        out float horizontalSpeed,
        out float upwardSpeed)
    {
        if (distance <= coreZoneRadius)
        {
            float ratio = Mathf.InverseLerp(0f, coreZoneRadius, distance);
            damage = Mathf.Lerp(coreDamage, sweetSpotDamage, ratio);
            horizontalSpeed = Mathf.Lerp(
                coreHorizontalSpeed,
                sweetSpotHorizontalSpeed,
                ratio);
            upwardSpeed = Mathf.Lerp(
                coreUpwardSpeed,
                sweetSpotUpwardSpeed,
                ratio);
            return;
        }

        if (distance <= sweetSpotRadius)
        {
            damage = sweetSpotDamage;
            horizontalSpeed = sweetSpotHorizontalSpeed;
            upwardSpeed = sweetSpotUpwardSpeed;
            return;
        }

        float edgeRatio = Mathf.InverseLerp(
            sweetSpotRadius,
            maximumRadius,
            distance);

        damage = Mathf.Lerp(sweetSpotDamage, edgeDamage, edgeRatio);
        horizontalSpeed = Mathf.Lerp(
            sweetSpotHorizontalSpeed,
            edgeHorizontalSpeed,
            edgeRatio);
        upwardSpeed = Mathf.Lerp(
            sweetSpotUpwardSpeed,
            edgeUpwardSpeed,
            edgeRatio);
    }

    private void ResolveEnemyBlastValues(
        float distance,
        out float damage,
        out float knockbackDistance)
    {
        if (distance <= coreZoneRadius)
        {
            float ratio = Mathf.InverseLerp(0f, coreZoneRadius, distance);
            damage = Mathf.Lerp(
                enemyCoreDamage,
                enemySweetSpotDamage,
                ratio);
            knockbackDistance = Mathf.Lerp(
                enemyCoreKnockbackDistance,
                enemySweetSpotKnockbackDistance,
                ratio);
            return;
        }

        if (distance <= sweetSpotRadius)
        {
            damage = enemySweetSpotDamage;
            knockbackDistance = enemySweetSpotKnockbackDistance;
            return;
        }

        float edgeRatio = Mathf.InverseLerp(
            sweetSpotRadius,
            maximumRadius,
            distance);
        damage = Mathf.Lerp(
            enemySweetSpotDamage,
            enemyEdgeDamage,
            edgeRatio);
        knockbackDistance = Mathf.Lerp(
            enemySweetSpotKnockbackDistance,
            enemyEdgeKnockbackDistance,
            edgeRatio);
    }

    private void ApplyDeathEmission(float multiplier)
    {
        if (_propertyBlock == null)
        {
            return;
        }

        for (int index = 0; index < _deathRenderers.Length; index++)
        {
            Renderer targetRenderer = _deathRenderers[index];

            if (targetRenderer == null ||
                _baseEmissionColors[index].maxColorComponent <= 0f)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(
                EmissionColorId,
                _baseEmissionColors[index] * multiplier);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private static void SetEffectColor(
        Renderer targetRenderer,
        Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Material material = targetRenderer.material;

        if (material != null && material.HasProperty(EffectColorId))
        {
            material.SetColor(EffectColorId, color);
        }
    }

    private static void PlayDetachedSpatialClip(
        AudioClip clip,
        Vector3 position,
        float volume,
        float pitch,
        float minimumDistance,
        float maximumDistance)
    {
        if (clip == null || volume <= 0f)
        {
            return;
        }

        GameObject audioObject = new GameObject("OctahedronBlastAudio");
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minimumDistance;
        source.maxDistance = maximumDistance;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        source.Play();

        float lifetime = clip.length / source.pitch + 0.1f;
        UnityEngine.Object.Destroy(audioObject, lifetime);
    }

    private static void SetEffectAlpha(
        Renderer targetRenderer,
        float alpha)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Material material = targetRenderer.material;

        if (material == null || !material.HasProperty(EffectColorId))
        {
            return;
        }

        Color color = material.GetColor(EffectColorId);
        color.a = Mathf.Clamp01(alpha);
        material.SetColor(EffectColorId, color);
    }

    private static void SetRingRadius(
        LineRenderer ring,
        float radius)
    {
        if (ring == null)
        {
            return;
        }

        const int segmentCount = 72;
        ring.positionCount = segmentCount + 1;
        ring.loop = false;

        for (int index = 0; index <= segmentCount; index++)
        {
            float angle = index / (float)segmentCount * Mathf.PI * 2f;
            ring.SetPosition(
                index,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
        }
    }

    private static void SetRingAlpha(
        LineRenderer ring,
        float alpha)
    {
        if (ring == null)
        {
            return;
        }

        Color start = ring.startColor;
        Color end = ring.endColor;
        start.a = alpha;
        end.a = alpha;
        ring.startColor = start;
        ring.endColor = end;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        chargeDuration = Mathf.Max(0.2f, chargeDuration);
        expansionDuration = Mathf.Max(0.1f, expansionDuration);
        lingerDuration = Mathf.Max(0f, lingerDuration);
        maximumRadius = Mathf.Max(1f, maximumRadius);
        waveThickness = Mathf.Max(0.1f, waveThickness);
        coreZoneRadius = Mathf.Clamp(
            coreZoneRadius,
            0.1f,
            maximumRadius);
        sweetSpotRadius = Mathf.Clamp(
            sweetSpotRadius,
            coreZoneRadius,
            maximumRadius);
        launchGraceTime = Mathf.Max(0f, launchGraceTime);
        noDragDuration = Mathf.Max(0f, noDragDuration);
        enemyCoreDamage = Mathf.Max(0f, enemyCoreDamage);
        enemySweetSpotDamage = Mathf.Max(0f, enemySweetSpotDamage);
        enemyEdgeDamage = Mathf.Max(0f, enemyEdgeDamage);
        octahedronDamageMultiplier =
            Mathf.Max(1f, octahedronDamageMultiplier);
        enemyCoreKnockbackDistance =
            Mathf.Max(0f, enemyCoreKnockbackDistance);
        enemySweetSpotKnockbackDistance =
            Mathf.Max(0f, enemySweetSpotKnockbackDistance);
        enemyEdgeKnockbackDistance =
            Mathf.Max(0f, enemyEdgeKnockbackDistance);
    }
#endif
}
