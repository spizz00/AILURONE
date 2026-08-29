#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Converts resolved hits into enemy-specific combat visuals.
/// PlayerWeapon and Ground Bot projectiles both route through this component.
/// This component never changes health, AI, movement, collision or death logic.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitFXReceiver : MonoBehaviour
{
    [Header("Core References")]
    public EnemyTarget enemyTarget;

    [Tooltip("The hit-effect Prefab Variant used by this enemy.")]
    public GameObject hitEffectPrefab;

    [Tooltip("Visual root followed by a non-fatal hit effect.")]
    public Transform attachmentRoot;

    [Tooltip("Optional Spike-specific body-light feedback.")]
    public SpikeVisualFeedback spikeVisualFeedback;

    [Tooltip("Optional production System Error feedback used by Spike enemies.")]
    public SpikeSystemErrorCombatFeedback systemErrorFeedback;

    [Tooltip("Optional production System Error feedback used by Ophanim enemies.")]
    public OphanimSystemErrorCombatFeedback ophanimSystemErrorFeedback;

    [Tooltip("Optional Ground Bot body-response feedback. Auto-resolved on Ground Bot enemies.")]
    public GroundBotEnemy groundBotEnemy;

    [Tooltip("Optional Octahedron-specific geometric surface feedback.")]
    public OctahedronHitFeedback octahedronHitFeedback;

    [Header("Legacy Hit Effect Compatibility")]
    [Tooltip("Prevents PlayerWeapon from also spawning its legacy enemyHitEffect.")]
    public bool suppressLegacyHitEffect = true;

    [Header("Hipfire Strength")]
    [Min(0.1f)]
    public float hipfireBaseStrength = 0.82f;

    [Min(0.1f)]
    public float hipfireMaximumStrength = 1.12f;

    [Header("ADS Charge Strength")]
    [Min(0.1f)]
    [Tooltip("Hit-feedback strength for a shot released immediately after charge begins.")]
    public float adsMinimumStrength = 0.88f;

    [Min(0.1f)]
    [Tooltip("Hit-feedback strength at 100% ADS charge.")]
    public float adsMaximumStrength = 1.45f;

    [Min(0.1f)]
    [Tooltip("Above 1 keeps early charge restrained and gives the final charge more emphasis.")]
    public float adsStrengthExponent = 1.35f;

    public bool SuppressLegacyHitEffect =>
        suppressLegacyHitEffect &&
        (systemErrorFeedback != null ||
         ophanimSystemErrorFeedback != null ||
         octahedronHitFeedback != null ||
         hitEffectPrefab != null);

    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Called by PlayerWeapon after all damage from one shot has been resolved.
    /// Existing player-weapon feedback behavior is preserved.
    /// </summary>
    public void PlayResolvedShot(
        EnemyShotResult result
    )
    {
        ResolveReferences();

        if (result.Target == null ||
            enemyTarget == null ||
            result.Target != enemyTarget ||
            result.TotalDamage <= 0f)
        {
            return;
        }

        PlayHitVisuals(
            result.HitPoint,
            result.HitNormal,
            result.FiredAsAds,
            result.Charge01,
            result.Killed,
            ResolveStrength(result)
        );
    }

    /// <summary>
    /// Called after a Ground Bot projectile has actually removed health.
    /// It reuses the target enemy's existing production hit-feedback system,
    /// including Spike and Ophanim System Error presentations.
    /// </summary>
    public void PlayGroundBotProjectileHit(
        Vector3 hitPoint,
        Vector3 hitNormal,
        float actualDamage,
        bool killed,
        float strength
    )
    {
        ResolveReferences();

        if (enemyTarget == null ||
            actualDamage <= 0f)
        {
            return;
        }

        PlayHitVisuals(
            hitPoint,
            hitNormal,
            false,
            0f,
            killed,
            Mathf.Clamp(strength, 0.35f, 1.5f)
        );
    }

    private void PlayHitVisuals(
        Vector3 hitPoint,
        Vector3 hitNormal,
        bool firedAsAds,
        float charge01,
        bool killed,
        float strength
    )
    {
        // Ground Bot keeps the existing surface hit graphic, but now also
        // reacts through its own mechanical body language. Lethal hits are
        // left to the dedicated death presentation.
        if (!killed && groundBotEnemy != null)
        {
            groundBotEnemy.PlayHitFeedback(
                hitPoint,
                hitNormal,
                strength
            );
        }

        if (octahedronHitFeedback != null)
        {
            if (!killed)
            {
                octahedronHitFeedback.PlayHit(
                    hitPoint,
                    hitNormal,
                    firedAsAds,
                    charge01,
                    strength
                );
            }

            return;
        }

        if (ophanimSystemErrorFeedback != null)
        {
            if (killed)
            {
                ophanimSystemErrorFeedback.RefineLethalHit(
                    hitPoint,
                    firedAsAds,
                    charge01,
                    strength
                );
            }
            else
            {
                ophanimSystemErrorFeedback.PlayNonFatalHit(
                    hitPoint,
                    firedAsAds,
                    charge01,
                    strength
                );
            }

            return;
        }

        if (killed &&
            systemErrorFeedback != null)
        {
            systemErrorFeedback.RefineLethalHit(
                hitPoint,
                firedAsAds,
                charge01,
                strength
            );

            return;
        }

        if (!killed &&
            systemErrorFeedback != null)
        {
            systemErrorFeedback.PlayNonFatalHit(
                hitPoint,
                firedAsAds,
                charge01,
                strength
            );

            return;
        }

        if (hitEffectPrefab == null)
        {
            return;
        }

        Color accentColor =
            spikeVisualFeedback != null
                ? spikeVisualFeedback.GetHitAccentColor()
                : new Color(0.10f, 0.92f, 1.15f, 1f);

        Transform followTarget =
            killed
                ? null
                : attachmentRoot;

        if (followTarget == null &&
            !killed)
        {
            followTarget = transform;
        }

        EnemyHitFXRequest request =
            new EnemyHitFXRequest(
                followTarget,
                hitPoint,
                hitNormal,
                accentColor,
                firedAsAds,
                killed,
                strength
            );

        EnemyHitFXPool.Play(
            hitEffectPrefab,
            request
        );

        if (!killed &&
            spikeVisualFeedback != null)
        {
            spikeVisualFeedback
                .TriggerEnergyDisruption(
                    strength,
                    firedAsAds
                );
        }
    }

    private float ResolveStrength(
        EnemyShotResult result
    )
    {
        if (result.FiredAsAds)
        {
            float chargeResponse =
                Mathf.Pow(
                    Mathf.Clamp01(result.Charge01),
                    Mathf.Max(0.1f, adsStrengthExponent)
                );

            float adsStrength =
                Mathf.Lerp(
                    adsMinimumStrength,
                    adsMaximumStrength,
                    chargeResponse
                );

            return Mathf.Clamp(
                adsStrength,
                0.35f,
                1.5f
            );
        }

        float pelletRatio =
            result.MaximumPellets > 0
                ? Mathf.Clamp01(
                    (float)result.PelletHits /
                    result.MaximumPellets
                )
                : 0f;

        float hipfireStrength =
            Mathf.Lerp(
                hipfireBaseStrength,
                hipfireMaximumStrength,
                Mathf.Sqrt(pelletRatio)
            );

        return Mathf.Clamp(
            hipfireStrength,
            0.35f,
            1.5f
        );
    }

    private void ResolveReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget =
                GetComponent<EnemyTarget>();
        }

        if (spikeVisualFeedback == null)
        {
            spikeVisualFeedback =
                GetComponent<SpikeVisualFeedback>();
        }

        if (systemErrorFeedback == null)
        {
            systemErrorFeedback =
                GetComponent<SpikeSystemErrorCombatFeedback>();
        }

        if (ophanimSystemErrorFeedback == null)
        {
            ophanimSystemErrorFeedback =
                GetComponent<OphanimSystemErrorCombatFeedback>();
        }

        if (groundBotEnemy == null)
        {
            groundBotEnemy =
                GetComponent<GroundBotEnemy>();
        }

        if (octahedronHitFeedback == null)
        {
            octahedronHitFeedback =
                GetComponent<OctahedronHitFeedback>();
        }

        if (attachmentRoot == null &&
            spikeVisualFeedback != null &&
            spikeVisualFeedback.targetRenderers != null)
        {
            foreach (Renderer targetRenderer
                     in spikeVisualFeedback.targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                attachmentRoot =
                    targetRenderer.transform;

                break;
            }
        }

        if (attachmentRoot == null &&
            ophanimSystemErrorFeedback != null)
        {
            attachmentRoot =
                ophanimSystemErrorFeedback.visualRoot;
        }

        if (attachmentRoot == null)
        {
            attachmentRoot = transform;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hipfireBaseStrength =
            Mathf.Max(0.1f, hipfireBaseStrength);

        hipfireMaximumStrength =
            Mathf.Max(
                hipfireBaseStrength,
                hipfireMaximumStrength
            );

        adsMinimumStrength =
            Mathf.Clamp(
                adsMinimumStrength,
                0.35f,
                1.5f
            );

        adsMaximumStrength =
            Mathf.Clamp(
                Mathf.Max(
                    adsMinimumStrength,
                    adsMaximumStrength
                ),
                0.35f,
                1.5f
            );

        adsStrengthExponent =
            Mathf.Max(0.1f, adsStrengthExponent);
    }
#endif
}
