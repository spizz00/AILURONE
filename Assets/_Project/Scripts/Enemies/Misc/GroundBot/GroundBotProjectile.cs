#pragma warning disable 0618
#pragma warning disable 0414
using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GroundBotProjectile : MonoBehaviour
{
    [Header("碰撞检测")]
    [Tooltip("高速弹丸使用 SphereCast 检测，避免穿过玩家、敌人或薄墙。")]
    [Min(0.01f)]
    public float collisionRadius = 0.07f;

    [Tooltip("默认检测全部可射线层。")]
    public LayerMask collisionMask = ~0;

    [Tooltip("第一次命中该 Layer 时反弹并强化；第二次命中时销毁。默认自动使用 Environment。")]
    public LayerMask environmentMask;

    [Min(0f)]
    [Tooltip("反弹后离开表面的安全距离，防止同一面被立即重复命中。")]
    public float bounceSurfaceSeparation = 0.045f;

    [Min(0f)]
    [Tooltip("穿透第一目标后沿飞行方向前移的距离，避免重复命中同一组 Collider。")]
    public float penetrationSeparation = 0.16f;

    [Header("普通弹伤害")]
    [Tooltip("Launch 传入的伤害用于玩家；当前 Ground Bot 默认值为 15。")]
    [SerializeField]
    private float normalPlayerDamage = 15f;

    [Min(0f)]
    public float normalEnemyDamage = 60f;

    [Header("反弹强化：第一目标")]
    [Min(0f)]
    public float firstPlayerDamage = 30f;

    [Min(0f)]
    public float firstEnemyDamage = 400f;

    [Min(0f)]
    public float firstEnemyStunDuration = 0.6f;

    [Min(0f)]
    public float firstEnemyKnockbackDistance = 2f;

    [Min(0.01f)]
    public float firstEnemyKnockbackDuration = 0.18f;

    [Min(0f)]
    public float firstPlayerKnockbackDistance = 1f;

    [Min(0.01f)]
    public float firstPlayerKnockbackDuration = 0.15f;

    [Header("反弹强化：第二目标")]
    [Min(0f)]
    public float secondPlayerDamage = 15f;

    [Min(0f)]
    public float secondEnemyDamage = 200f;

    [Min(0f)]
    public float secondEnemyStunDuration = 0.3f;

    [Min(0f)]
    public float secondEnemyKnockbackDistance = 1f;

    [Min(0.01f)]
    public float secondEnemyKnockbackDuration = 0.12f;

    [Min(0f)]
    public float secondPlayerKnockbackDistance = 0.5f;

    [Min(0.01f)]
    public float secondPlayerKnockbackDuration = 0.10f;

    [Header("敌人命中反馈强度")]
    [Min(0.1f)]
    [Tooltip("普通 Ground Bot 子弹命中敌人时，调用目标现有受击反馈的强度。")]
    public float normalEnemyHitFeedbackStrength = 0.92f;

    [Min(0.1f)]
    [Tooltip("反弹强化弹命中第一目标时的受击反馈强度。")]
    public float firstEnemyHitFeedbackStrength = 1.35f;

    [Min(0.1f)]
    [Tooltip("反弹强化弹穿透后命中第二目标时的受击反馈强度。")]
    public float secondEnemyHitFeedbackStrength = 1.08f;

    [Header("强化状态")]
    [Min(0.05f)]
    [Tooltip("第一次反弹后重新获得的寿命。")]
    public float empoweredLifeTime = 3f;

    [Min(0f)]
    public float environmentKillCreditDuration = 3f;

    [Header("命中特效")]
    [Tooltip("命中 Environment、墙体或其他非玩家实体时生成。")]
    public GroundBotCombatFX environmentImpactPrefab;

    [Tooltip("命中玩家时生成。")]
    public GroundBotCombatFX playerImpactPrefab;

    [Tooltip("让特效沿表面法线稍微离开碰撞面，避免 Z-Fighting。")]
    [Min(0f)]
    public float impactSurfaceOffset = 0.025f;

    [Header("强化弹视觉：核心与主拖尾")]
    [Tooltip("第一次反弹后使用近白核心，与普通红橙弹形成明确区分。")]
    public Color empoweredCoreColor =
        new Color(1f, 0.96f, 0.92f, 1f);

    [Tooltip("高动态范围发光颜色。需要场景 Bloom 时会出现更明显的白热光晕。")]
    public Color empoweredEmissionColor =
        new Color(14f, 2.6f, 1.25f, 1f);

    public Color weakenedCoreColor =
        new Color(1f, 0.66f, 0.48f, 1f);

    public Color weakenedEmissionColor =
        new Color(7f, 0.85f, 0.34f, 1f);

    [Min(1f)]
    public float empoweredScaleMultiplier = 1.65f;

    [Min(1f)]
    public float weakenedScaleMultiplier = 1.34f;

    [Min(1f)]
    public float empoweredTrailTimeMultiplier = 2.35f;

    [Min(1f)]
    public float weakenedTrailTimeMultiplier = 1.65f;

    [Min(1f)]
    public float empoweredTrailWidthMultiplier = 1.55f;

    [Min(1f)]
    public float weakenedTrailWidthMultiplier = 1.22f;

    [Header("强化弹视觉：外层能量拖尾")]
    [Tooltip("反弹后自动生成第二层宽拖尾，不需要手动修改 Projectile Prefab 层级。")]
    public Color empoweredAuraStartColor =
        new Color(1f, 0.08f, 0.22f, 0.58f);

    public Color empoweredAuraMiddleColor =
        new Color(1f, 0.28f, 0.08f, 0.34f);

    public Color weakenedAuraStartColor =
        new Color(1f, 0.12f, 0.06f, 0.34f);

    public Color weakenedAuraMiddleColor =
        new Color(1f, 0.30f, 0.08f, 0.20f);

    [Min(1f)]
    public float empoweredAuraTrailTimeMultiplier = 2.9f;

    [Min(1f)]
    public float weakenedAuraTrailTimeMultiplier = 1.9f;

    [Min(1f)]
    public float empoweredAuraTrailWidthMultiplier = 3.0f;

    [Min(1f)]
    public float weakenedAuraTrailWidthMultiplier = 1.85f;

    [Header("强化弹视觉：脉冲与局部照明")]
    [Range(0f, 0.25f)]
    public float empoweredPulseAmplitude = 0.10f;

    [Range(0f, 0.25f)]
    public float weakenedPulseAmplitude = 0.055f;

    [Min(0f)]
    public float empoweredPulseFrequency = 7.5f;

    public Color empoweredLightColor =
        new Color(1f, 0.18f, 0.06f, 1f);

    [Min(0f)]
    public float empoweredLightIntensity = 3.8f;

    [Min(0f)]
    public float weakenedLightIntensity = 2.0f;

    [Min(0f)]
    public float empoweredLightRange = 1.8f;

    [Min(0f)]
    public float weakenedLightRange = 1.15f;

    [Header("运行状态")]
    [SerializeField]
    private float currentSpeed;

    [SerializeField]
    private float remainingLife;

    [SerializeField]
    private bool launched;

    [SerializeField]
    private bool hasBounced;

    [SerializeField]
    private int characterHitCount;

    private Transform _ownerRoot;
    private Action<GroundBotProjectile> _returnToPool;
    private TrailRenderer _trailRenderer;
    private TrailRenderer _empoweredAuraTrail;
    private Renderer _coreRenderer;
    private Transform _coreTransform;
    private Light _empoweredLight;

    private Vector3 _originalCoreScale = Vector3.one;
    private float _originalTrailTime;
    private float _originalTrailWidthMultiplier = 1f;
    private Gradient _originalTrailGradient;
    private MaterialPropertyBlock _corePropertyBlock;
    private MaterialPropertyBlock _originalCorePropertyBlock;
    private Material _runtimeCoreMaterial;
    private float _activeScaleMultiplier = 1f;
    private float _activePulseAmplitude;
    private float _activeLightBaseIntensity;
    private float _activeAuraWidthMultiplier;
    private float _visualPulseTime;

    private readonly RaycastHit[] _hits = new RaycastHit[24];
    private readonly HashSet<int> _hitCharacterIds = new HashSet<int>();

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    public bool HasBounced => hasBounced;
    public int CharacterHitCount => characterHitCount;

    private void Awake()
    {
        _trailRenderer = GetComponentInChildren<TrailRenderer>(true);
        _coreRenderer = GetComponentInChildren<MeshRenderer>(true);

        if (_coreRenderer != null)
        {
            _coreTransform = _coreRenderer.transform;
            _originalCoreScale = _coreTransform.localScale;
            _corePropertyBlock = new MaterialPropertyBlock();
            _originalCorePropertyBlock = new MaterialPropertyBlock();
            _coreRenderer.GetPropertyBlock(_originalCorePropertyBlock);

            Material sourceMaterial =
                _coreRenderer.sharedMaterial;

            if (sourceMaterial != null)
            {
                _runtimeCoreMaterial =
                    new Material(sourceMaterial)
                    {
                        name =
                            sourceMaterial.name +
                            "_RuntimeProjectile"
                    };

                // 每颗池化弹丸使用自己的材质实例。
                // 普通状态保持原状，第一次反弹后才启用 HDR Emission。
                _runtimeCoreMaterial.DisableKeyword("_EMISSION");
                _coreRenderer.sharedMaterial =
                    _runtimeCoreMaterial;
            }
        }

        if (_trailRenderer != null)
        {
            _originalTrailTime = _trailRenderer.time;
            _originalTrailWidthMultiplier =
                _trailRenderer.widthMultiplier;
            _originalTrailGradient =
                CloneGradient(_trailRenderer.colorGradient);
        }

        CreateRuntimeVisualHelpers();

        if (environmentMask.value == 0)
        {
            environmentMask = LayerMask.GetMask("Environment");
        }
    }

    public void Launch(
        Vector3 position,
        Vector3 direction,
        float speed,
        float damage,
        float lifeTime,
        Transform ownerRoot,
        Action<GroundBotProjectile> returnToPool
    )
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        _ownerRoot = ownerRoot;
        _returnToPool = returnToPool;
        currentSpeed = Mathf.Max(0f, speed);
        normalPlayerDamage = Mathf.Max(0f, damage);
        remainingLife = Mathf.Max(0.05f, lifeTime);
        launched = true;
        hasBounced = false;
        characterHitCount = 0;
        _hitCharacterIds.Clear();

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(direction, Vector3.up)
        );

        RestoreNormalVisual();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
            _trailRenderer.emitting = true;
        }
    }

    private void Update()
    {
        if (!launched)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        UpdateEmpoweredVisualPulse(deltaTime);

        remainingLife -= deltaTime;

        if (remainingLife <= 0f)
        {
            Despawn();
            return;
        }

        float distance = currentSpeed * deltaTime;

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
                out RaycastHit hit
            ))
        {
            transform.position = hit.point;
            ResolveHit(hit);
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

        int mask = collisionMask.value != 0
            ? collisionMask.value
            : Physics.DefaultRaycastLayers;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            collisionRadius,
            direction,
            _hits,
            distance,
            mask,
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

        // 第一次反弹之前，弹丸只忽略自己的发射者。
        // 反弹之后发射者也成为合法目标，可完成自伤玩法。
        if (!hasBounced &&
            _ownerRoot != null &&
            (candidateTransform == _ownerRoot ||
             candidateTransform.IsChildOf(_ownerRoot)))
        {
            return false;
        }

        int characterId = GetCharacterId(candidate);

        if (characterId != 0 &&
            _hitCharacterIds.Contains(characterId))
        {
            return false;
        }

        return true;
    }

    private void ResolveHit(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;

        if (hitCollider == null)
        {
            Despawn();
            return;
        }

        if (IsEnvironment(hitCollider))
        {
            ResolveEnvironmentHit(hit);
            return;
        }

        PlayerHealth playerHealth =
            hitCollider.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            ResolvePlayerHit(hit, playerHealth);
            return;
        }

        EnemyTarget enemyTarget =
            hitCollider.GetComponentInParent<EnemyTarget>();

        if (enemyTarget != null)
        {
            ResolveEnemyHit(hit, enemyTarget);
            return;
        }

        // 未分类实体视作普通阻挡物，不允许弹丸穿过去。
        SpawnImpactEffect(environmentImpactPrefab, hit);
        Despawn();
    }

    private void ResolveEnvironmentHit(RaycastHit hit)
    {
        SpawnImpactEffect(environmentImpactPrefab, hit);

        if (hasBounced)
        {
            Despawn();
            return;
        }

        Vector3 incomingDirection = transform.forward;
        Vector3 surfaceNormal = hit.normal;

        if (surfaceNormal.sqrMagnitude <= 0.0001f)
        {
            surfaceNormal = -incomingDirection;
        }

        surfaceNormal.Normalize();

        Vector3 reflectedDirection =
            Vector3.Reflect(
                incomingDirection,
                surfaceNormal
            ).normalized;

        if (reflectedDirection.sqrMagnitude <= 0.0001f)
        {
            Despawn();
            return;
        }

        hasBounced = true;
        characterHitCount = 0;
        remainingLife = Mathf.Max(0.05f, empoweredLifeTime);

        Vector3 safePosition =
            hit.point +
            surfaceNormal *
            (
                collisionRadius +
                Mathf.Max(0f, bounceSurfaceSeparation)
            );

        transform.SetPositionAndRotation(
            safePosition,
            Quaternion.LookRotation(
                reflectedDirection,
                Vector3.up
            )
        );

        GroundBotProjectileBounceFX.Spawn(
            hit.point + surfaceNormal * 0.02f,
            surfaceNormal
        );

        ApplyEmpoweredVisual();
    }

    private void ResolvePlayerHit(
        RaycastHit hit,
        PlayerHealth playerHealth
    )
    {
        int characterId = playerHealth.gameObject.GetEntityId();

        if (characterId != 0)
        {
            _hitCharacterIds.Add(characterId);
        }

        SpawnImpactEffect(playerImpactPrefab, hit);

        if (!hasBounced)
        {
            if (!playerHealth.IsRewinding)
            {
                playerHealth.TakeDamage(normalPlayerDamage);
            }

            Despawn();
            return;
        }

        bool firstTarget = characterHitCount == 0;
        float damage = firstTarget
            ? firstPlayerDamage
            : secondPlayerDamage;

        if (!playerHealth.IsRewinding)
        {
            playerHealth.TakeDamage(damage);

            ApplyPlayerKnockback(
                playerHealth,
                transform.forward,
                firstTarget
                    ? firstPlayerKnockbackDistance
                    : secondPlayerKnockbackDistance,
                firstTarget
                    ? firstPlayerKnockbackDuration
                    : secondPlayerKnockbackDuration
            );
        }

        CompleteCharacterPenetration(firstTarget);
    }

    private void ResolveEnemyHit(
        RaycastHit hit,
        EnemyTarget enemyTarget
    )
    {
        int characterId = enemyTarget.gameObject.GetEntityId();

        if (characterId != 0)
        {
            _hitCharacterIds.Add(characterId);
        }

        SpawnImpactEffect(environmentImpactPrefab, hit);

        if (!hasBounced)
        {
            bool wasDeadBeforeHit =
                enemyTarget.IsDead;

            float actualDamage =
                enemyTarget.TakeDamageFromGroundBotProjectile(
                    normalEnemyDamage,
                    hit.point,
                    hit.normal,
                    null
                );

            bool killed =
                actualDamage > 0f &&
                !wasDeadBeforeHit &&
                enemyTarget.IsDead;

            PlayEnemyHitFeedback(
                enemyTarget,
                hit,
                actualDamage,
                killed,
                normalEnemyHitFeedbackStrength
            );

            Despawn();
            return;
        }

        bool firstTarget = characterHitCount == 0;
        float damage = firstTarget
            ? firstEnemyDamage
            : secondEnemyDamage;

        bool wasDeadBeforeEmpoweredHit =
            enemyTarget.IsDead;

        float empoweredActualDamage =
            enemyTarget.TakeDamageFromGroundBotProjectile(
                damage,
                hit.point,
                hit.normal,
                null
            );

        bool empoweredKilled =
            empoweredActualDamage > 0f &&
            !wasDeadBeforeEmpoweredHit &&
            enemyTarget.IsDead;

        PlayEnemyHitFeedback(
            enemyTarget,
            hit,
            empoweredActualDamage,
            empoweredKilled,
            firstTarget
                ? firstEnemyHitFeedbackStrength
                : secondEnemyHitFeedbackStrength
        );

        if (!enemyTarget.IsDead)
        {
            ApplyEnemyControl(
                enemyTarget,
                transform.forward,
                firstTarget
            );
        }

        CompleteCharacterPenetration(firstTarget);
    }

    private static void PlayEnemyHitFeedback(
        EnemyTarget enemyTarget,
        RaycastHit hit,
        float actualDamage,
        bool killed,
        float strength
    )
    {
        if (enemyTarget == null ||
            actualDamage <= 0f)
        {
            return;
        }

        EnemyHitFXReceiver receiver =
            enemyTarget.GetComponent<EnemyHitFXReceiver>();

        if (receiver == null)
        {
            return;
        }

        receiver.PlayGroundBotProjectileHit(
            hit.point,
            hit.normal,
            actualDamage,
            killed,
            strength
        );
    }

    private void CompleteCharacterPenetration(bool firstTarget)
    {
        characterHitCount++;

        if (!firstTarget || characterHitCount >= 2)
        {
            Despawn();
            return;
        }

        ApplyWeakenedVisual();

        transform.position +=
            transform.forward *
            Mathf.Max(
                penetrationSeparation,
                collisionRadius * 2f + 0.02f
            );
    }

    private void ApplyEnemyControl(
        EnemyTarget enemyTarget,
        Vector3 projectileDirection,
        bool firstTarget
    )
    {
        if (enemyTarget == null)
        {
            return;
        }

        EnemyControlEffectController controller =
            enemyTarget.GetComponent<EnemyControlEffectController>();

        if (controller == null)
        {
            return;
        }

        Vector3 knockbackDirection =
            BuildEnemyKnockbackDirection(
                enemyTarget,
                projectileDirection
            );

        controller.ApplyStunAndKnockback(
            firstTarget
                ? firstEnemyStunDuration
                : secondEnemyStunDuration,
            knockbackDirection,
            firstTarget
                ? firstEnemyKnockbackDistance
                : secondEnemyKnockbackDistance,
            firstTarget
                ? firstEnemyKnockbackDuration
                : secondEnemyKnockbackDuration,
            environmentKillCreditDuration
        );
    }

    private static Vector3 BuildEnemyKnockbackDirection(
        EnemyTarget enemyTarget,
        Vector3 projectileDirection
    )
    {
        if (projectileDirection.sqrMagnitude <= 0.0001f)
        {
            projectileDirection = Vector3.forward;
        }

        // Ophanim 保留完整三维方向。
        if (enemyTarget.GetComponent<OphanimEnemy>() != null)
        {
            return projectileDirection.normalized;
        }

        // Ground Bot 与 Spike 主要水平位移，只保留很小的垂直分量。
        Vector3 groundDirection = projectileDirection;
        groundDirection.y = Mathf.Clamp(groundDirection.y, -0.04f, 0.10f);

        if (groundDirection.sqrMagnitude <= 0.0001f)
        {
            groundDirection =
                Vector3.ProjectOnPlane(
                    projectileDirection,
                    Vector3.up
                );
        }

        return groundDirection.normalized;
    }

    private static void ApplyPlayerKnockback(
        PlayerHealth playerHealth,
        Vector3 direction,
        float distance,
        float duration
    )
    {
        if (playerHealth == null ||
            distance <= 0f ||
            duration <= 0f)
        {
            return;
        }

        FirstPersonController controller =
            playerHealth.GetComponentInParent<FirstPersonController>();

        if (controller == null)
        {
            controller =
                playerHealth.GetComponentInChildren<FirstPersonController>();
        }

        controller?.ApplyControlledKnockback(
            direction,
            distance,
            duration
        );
    }

    private bool IsEnvironment(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        int mask = environmentMask.value;

        if (mask == 0)
        {
            return candidate.gameObject.layer ==
                   LayerMask.NameToLayer("Environment");
        }

        return (mask & (1 << candidate.gameObject.layer)) != 0;
    }

    private static int GetCharacterId(Collider candidate)
    {
        if (candidate == null)
        {
            return 0;
        }

        PlayerHealth player =
            candidate.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            return player.gameObject.GetEntityId();
        }

        EnemyTarget enemy =
            candidate.GetComponentInParent<EnemyTarget>();

        return enemy != null
            ? enemy.gameObject.GetEntityId()
            : 0;
    }

    private void SpawnImpactEffect(
        GroundBotCombatFX effectPrefab,
        RaycastHit hit
    )
    {
        if (effectPrefab == null)
        {
            return;
        }

        Vector3 normal = hit.normal;

        if (normal.sqrMagnitude <= 0.0001f)
        {
            normal = -transform.forward;
        }

        normal.Normalize();

        Vector3 position =
            hit.point +
            normal * Mathf.Max(0f, impactSurfaceOffset);

        Instantiate(
            effectPrefab,
            position,
            Quaternion.LookRotation(normal, Vector3.up)
        );
    }

    private void CreateRuntimeVisualHelpers()
    {
        if (_trailRenderer != null)
        {
            GameObject auraObject =
                new GameObject(
                    "Runtime_EmpoweredAuraTrail"
                );

            auraObject.transform.SetParent(transform, false);

            _empoweredAuraTrail =
                auraObject.AddComponent<TrailRenderer>();

            CopyTrailRendererSettings(
                _trailRenderer,
                _empoweredAuraTrail
            );

            _empoweredAuraTrail.enabled = false;
            _empoweredAuraTrail.emitting = false;
            _empoweredAuraTrail.Clear();
        }

        GameObject lightObject =
            new GameObject(
                "Runtime_EmpoweredProjectileLight"
            );

        lightObject.transform.SetParent(transform, false);

        _empoweredLight =
            lightObject.AddComponent<Light>();

        _empoweredLight.type = LightType.Point;
        _empoweredLight.shadows = LightShadows.None;
        _empoweredLight.renderMode = LightRenderMode.Auto;
        _empoweredLight.intensity = 0f;
        _empoweredLight.range = 0f;
        _empoweredLight.enabled = false;
    }

    private static void CopyTrailRendererSettings(
        TrailRenderer source,
        TrailRenderer destination
    )
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        destination.receiveShadows = false;
        destination.sharedMaterials =
            source.sharedMaterials;
        destination.time = source.time;
        destination.minVertexDistance =
            source.minVertexDistance;
        destination.widthMultiplier =
            source.widthMultiplier;
        destination.widthCurve =
            CloneAnimationCurve(source.widthCurve);
        destination.colorGradient =
            CloneGradient(source.colorGradient);
        destination.numCornerVertices =
            Mathf.Max(2, source.numCornerVertices);
        destination.numCapVertices =
            Mathf.Max(2, source.numCapVertices);
        destination.alignment = source.alignment;
        destination.textureMode = source.textureMode;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.autodestruct = false;
        destination.emitting = false;
    }

    private void RestoreNormalVisual()
    {
        _activeScaleMultiplier = 1f;
        _activePulseAmplitude = 0f;
        _activeLightBaseIntensity = 0f;
        _activeAuraWidthMultiplier = 0f;
        _visualPulseTime = 0f;

        if (_coreTransform != null)
        {
            _coreTransform.localScale = _originalCoreScale;
        }

        if (_runtimeCoreMaterial != null)
        {
            _runtimeCoreMaterial.DisableKeyword("_EMISSION");
        }

        if (_coreRenderer != null &&
            _originalCorePropertyBlock != null)
        {
            _coreRenderer.SetPropertyBlock(
                _originalCorePropertyBlock
            );
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.time = _originalTrailTime;
            _trailRenderer.widthMultiplier =
                _originalTrailWidthMultiplier;

            if (_originalTrailGradient != null)
            {
                _trailRenderer.colorGradient =
                    CloneGradient(_originalTrailGradient);
            }
        }

        if (_empoweredAuraTrail != null)
        {
            _empoweredAuraTrail.emitting = false;
            _empoweredAuraTrail.Clear();
            _empoweredAuraTrail.enabled = false;
        }

        if (_empoweredLight != null)
        {
            _empoweredLight.intensity = 0f;
            _empoweredLight.range = 0f;
            _empoweredLight.enabled = false;
        }
    }

    private void ApplyEmpoweredVisual()
    {
        _visualPulseTime = 0f;
        _activeScaleMultiplier =
            Mathf.Max(1f, empoweredScaleMultiplier);
        _activePulseAmplitude =
            Mathf.Clamp(empoweredPulseAmplitude, 0f, 0.25f);
        _activeLightBaseIntensity =
            Mathf.Max(0f, empoweredLightIntensity);
        _activeAuraWidthMultiplier =
            Mathf.Max(
                1f,
                empoweredAuraTrailWidthMultiplier
            );

        ApplyCoreVisual(
            empoweredCoreColor,
            empoweredEmissionColor,
            _activeScaleMultiplier
        );

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
        }

        ApplyTrailVisual(
            new Color(1f, 1f, 0.98f, 1f),
            new Color(1f, 0.20f, 0.11f, 0.88f),
            new Color(0.92f, 0.02f, 0.10f, 0f),
            empoweredTrailTimeMultiplier,
            empoweredTrailWidthMultiplier
        );

        ApplyAuraTrailVisual(
            empoweredAuraStartColor,
            empoweredAuraMiddleColor,
            new Color(
                empoweredAuraMiddleColor.r,
                empoweredAuraMiddleColor.g,
                empoweredAuraMiddleColor.b,
                0f
            ),
            empoweredAuraTrailTimeMultiplier,
            _activeAuraWidthMultiplier,
            true
        );

        SetEmpoweredLight(
            empoweredLightColor,
            empoweredLightIntensity,
            empoweredLightRange
        );
    }

    private void ApplyWeakenedVisual()
    {
        _activeScaleMultiplier =
            Mathf.Max(1f, weakenedScaleMultiplier);
        _activePulseAmplitude =
            Mathf.Clamp(weakenedPulseAmplitude, 0f, 0.25f);
        _activeLightBaseIntensity =
            Mathf.Max(0f, weakenedLightIntensity);
        _activeAuraWidthMultiplier =
            Mathf.Max(
                1f,
                weakenedAuraTrailWidthMultiplier
            );

        ApplyCoreVisual(
            weakenedCoreColor,
            weakenedEmissionColor,
            _activeScaleMultiplier
        );

        ApplyTrailVisual(
            new Color(1f, 0.86f, 0.72f, 0.96f),
            new Color(1f, 0.26f, 0.08f, 0.70f),
            new Color(0.88f, 0.04f, 0.02f, 0f),
            weakenedTrailTimeMultiplier,
            weakenedTrailWidthMultiplier
        );

        ApplyAuraTrailVisual(
            weakenedAuraStartColor,
            weakenedAuraMiddleColor,
            new Color(
                weakenedAuraMiddleColor.r,
                weakenedAuraMiddleColor.g,
                weakenedAuraMiddleColor.b,
                0f
            ),
            weakenedAuraTrailTimeMultiplier,
            _activeAuraWidthMultiplier,
            false
        );

        SetEmpoweredLight(
            empoweredLightColor,
            weakenedLightIntensity,
            weakenedLightRange
        );
    }

    private void ApplyCoreVisual(
        Color baseColor,
        Color emissionColor,
        float scaleMultiplier
    )
    {
        if (_coreTransform != null)
        {
            _coreTransform.localScale =
                _originalCoreScale *
                Mathf.Max(1f, scaleMultiplier);
        }

        if (_coreRenderer == null)
        {
            return;
        }

        if (_runtimeCoreMaterial != null)
        {
            _runtimeCoreMaterial.EnableKeyword("_EMISSION");
        }

        if (_corePropertyBlock == null)
        {
            _corePropertyBlock = new MaterialPropertyBlock();
        }

        _coreRenderer.GetPropertyBlock(_corePropertyBlock);
        _corePropertyBlock.SetColor(BaseColorId, baseColor);
        _corePropertyBlock.SetColor(ColorId, baseColor);
        _corePropertyBlock.SetColor(EmissionColorId, emissionColor);
        _coreRenderer.SetPropertyBlock(_corePropertyBlock);
    }

    private void ApplyTrailVisual(
        Color startColor,
        Color middleColor,
        Color endColor,
        float timeMultiplier,
        float widthMultiplier
    )
    {
        if (_trailRenderer == null)
        {
            return;
        }

        _trailRenderer.time =
            _originalTrailTime *
            Mathf.Max(1f, timeMultiplier);

        _trailRenderer.widthMultiplier =
            _originalTrailWidthMultiplier *
            Mathf.Max(1f, widthMultiplier);

        _trailRenderer.colorGradient =
            BuildThreeColorGradient(
                startColor,
                middleColor,
                endColor
            );
    }

    private void ApplyAuraTrailVisual(
        Color startColor,
        Color middleColor,
        Color endColor,
        float timeMultiplier,
        float widthMultiplier,
        bool clearExistingTrail
    )
    {
        if (_empoweredAuraTrail == null)
        {
            return;
        }

        _empoweredAuraTrail.enabled = true;
        _empoweredAuraTrail.time =
            _originalTrailTime *
            Mathf.Max(1f, timeMultiplier);
        _empoweredAuraTrail.widthMultiplier =
            _originalTrailWidthMultiplier *
            Mathf.Max(1f, widthMultiplier);
        _empoweredAuraTrail.colorGradient =
            BuildThreeColorGradient(
                startColor,
                middleColor,
                endColor
            );

        if (clearExistingTrail)
        {
            _empoweredAuraTrail.Clear();
        }

        _empoweredAuraTrail.emitting = true;
    }

    private void SetEmpoweredLight(
        Color color,
        float intensity,
        float range
    )
    {
        if (_empoweredLight == null)
        {
            return;
        }

        _empoweredLight.color = color;
        _empoweredLight.intensity =
            Mathf.Max(0f, intensity);
        _empoweredLight.range =
            Mathf.Max(0f, range);
        _empoweredLight.enabled =
            _empoweredLight.intensity > 0f &&
            _empoweredLight.range > 0f;
    }

    private void UpdateEmpoweredVisualPulse(float deltaTime)
    {
        if (!hasBounced)
        {
            return;
        }

        _visualPulseTime += Mathf.Max(0f, deltaTime);

        float frequency =
            Mathf.Max(0f, empoweredPulseFrequency);

        float wave = frequency > 0f
            ? Mathf.Sin(
                _visualPulseTime *
                frequency *
                Mathf.PI *
                2f
            )
            : 0f;

        float pulseScale =
            1f + wave * _activePulseAmplitude;

        if (_coreTransform != null)
        {
            _coreTransform.localScale =
                _originalCoreScale *
                Mathf.Max(1f, _activeScaleMultiplier) *
                pulseScale;
        }

        if (_empoweredAuraTrail != null &&
            _empoweredAuraTrail.enabled)
        {
            float auraPulse =
                1f +
                wave *
                _activePulseAmplitude *
                0.55f;

            _empoweredAuraTrail.widthMultiplier =
                _originalTrailWidthMultiplier *
                Mathf.Max(
                    1f,
                    _activeAuraWidthMultiplier
                ) *
                auraPulse;
        }

        if (_empoweredLight != null &&
            _empoweredLight.enabled)
        {
            float lightPulse =
                Mathf.Lerp(
                    0.78f,
                    1.08f,
                    wave * 0.5f + 0.5f
                );

            _empoweredLight.intensity =
                _activeLightBaseIntensity *
                lightPulse;
        }
    }

    private static Gradient BuildThreeColorGradient(
        Color startColor,
        Color middleColor,
        Color endColor
    )
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(middleColor, 0.28f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(middleColor.a, 0.28f),
                new GradientAlphaKey(endColor.a, 1f)
            }
        );

        return gradient;
    }

    private static AnimationCurve CloneAnimationCurve(
        AnimationCurve source
    )
    {
        if (source == null)
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
        }

        AnimationCurve clone =
            new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };

        return clone;
    }

    private static Gradient CloneGradient(Gradient source)
    {
        if (source == null)
        {
            return null;
        }

        Gradient clone = new Gradient();
        clone.mode = source.mode;
        clone.SetKeys(source.colorKeys, source.alphaKeys);
        return clone;
    }

    public void Despawn()
    {
        if (!launched && !gameObject.activeSelf)
        {
            return;
        }

        launched = false;
        currentSpeed = 0f;
        remainingLife = 0f;
        hasBounced = false;
        characterHitCount = 0;
        _ownerRoot = null;
        _hitCharacterIds.Clear();

        RestoreNormalVisual();

        if (_trailRenderer != null)
        {
            _trailRenderer.emitting = false;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }

        Action<GroundBotProjectile> returnCallback =
            _returnToPool;

        _returnToPool = null;
        returnCallback?.Invoke(this);
    }

    public void DestroyWithOwner()
    {
        launched = false;
        _ownerRoot = null;
        _returnToPool = null;
        _hitCharacterIds.Clear();
        RestoreNormalVisual();

        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (_runtimeCoreMaterial != null)
        {
            Destroy(_runtimeCoreMaterial);
            _runtimeCoreMaterial = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        collisionRadius = Mathf.Max(0.01f, collisionRadius);
        bounceSurfaceSeparation =
            Mathf.Max(0f, bounceSurfaceSeparation);
        penetrationSeparation =
            Mathf.Max(0f, penetrationSeparation);
        impactSurfaceOffset =
            Mathf.Max(0f, impactSurfaceOffset);
        empoweredLifeTime =
            Mathf.Max(0.05f, empoweredLifeTime);
        empoweredScaleMultiplier =
            Mathf.Max(1f, empoweredScaleMultiplier);
        weakenedScaleMultiplier =
            Mathf.Max(1f, weakenedScaleMultiplier);
        empoweredTrailTimeMultiplier =
            Mathf.Max(1f, empoweredTrailTimeMultiplier);
        weakenedTrailTimeMultiplier =
            Mathf.Max(1f, weakenedTrailTimeMultiplier);
        empoweredTrailWidthMultiplier =
            Mathf.Max(1f, empoweredTrailWidthMultiplier);
        weakenedTrailWidthMultiplier =
            Mathf.Max(1f, weakenedTrailWidthMultiplier);
        empoweredAuraTrailTimeMultiplier =
            Mathf.Max(1f, empoweredAuraTrailTimeMultiplier);
        weakenedAuraTrailTimeMultiplier =
            Mathf.Max(1f, weakenedAuraTrailTimeMultiplier);
        empoweredAuraTrailWidthMultiplier =
            Mathf.Max(1f, empoweredAuraTrailWidthMultiplier);
        weakenedAuraTrailWidthMultiplier =
            Mathf.Max(1f, weakenedAuraTrailWidthMultiplier);
        empoweredPulseFrequency =
            Mathf.Max(0f, empoweredPulseFrequency);
        empoweredLightIntensity =
            Mathf.Max(0f, empoweredLightIntensity);
        weakenedLightIntensity =
            Mathf.Max(0f, weakenedLightIntensity);
        empoweredLightRange =
            Mathf.Max(0f, empoweredLightRange);
        weakenedLightRange =
            Mathf.Max(0f, weakenedLightRange);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = hasBounced
            ? new Color(1f, 0.88f, 0.8f, 0.9f)
            : new Color(1f, 0.2f, 0.08f, 0.8f);

        Gizmos.DrawWireSphere(
            transform.position,
            collisionRadius
        );
    }
#endif
}
