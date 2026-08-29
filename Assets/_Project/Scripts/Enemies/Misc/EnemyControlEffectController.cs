#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;

/// <summary>
/// 让不同敌人共享“外部硬直 + 受控击退”底层。
///
/// Phase 1.1 修正：
/// 1. 硬直期间临时替换为专用冷白色 Unlit 材质，确保全身清晰闪白。
/// 2. Ground Bot 与 Spike 被推出平台后切换为受重力影响的坠落状态。
/// 3. 落到下层 Environment 时恢复原 Rigidbody 设置并重新交还 AI。
/// </summary>
public interface IEnemyExternalControlReceiver
{
    bool IsExternalControlActive { get; }

    void BeginExternalControl();

    void EndExternalControl();
}

[DisallowMultipleComponent]
public class EnemyControlEffectController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField]
    private EnemyTarget enemyTarget;

    [SerializeField]
    private Rigidbody controlledRigidbody;

    [Header("碰撞检测")]
    [Tooltip("受控击退只会被这些 Layer 阻挡。默认自动使用 Environment。")]
    public LayerMask environmentMask;

    [Min(0f)]
    [Tooltip("停止在墙面前保留的微小距离，防止击退后嵌入墙体。")]
    public float collisionSkin = 0.025f;

    [Range(0.5f, 0.98f)]
    [Tooltip("用于 BoxCast 的包围盒缩放。缩小一点可避免站在地面时被地面误挡。")]
    public float castBoundsScale = 0.86f;

    [Header("通用全身白色硬直表现")]
    [Tooltip("低亮阶段仍保持明显的冷白色，不再依赖原模型贴图与 Emission Map。")]
    public Color stunnedBaseColor =
        new Color(0.58f, 0.78f, 1f, 1f);

    [Min(0f)]
    [Tooltip("白色覆盖的额外亮度。默认值会产生明显但短促的高亮闪烁。")]
    public float stunnedEmission = 1.6f;

    [Min(0f)]
    [Tooltip("每秒闪烁次数。4.5 表示 0.6 秒内大约闪 2 到 3 次。")]
    public float stunnedFlashSpeed = 4.5f;

    [Min(0f)]
    [Tooltip("保留用于 Inspector 兼容。Phase 1.1 使用直接脉冲，不再依赖缓慢插值。")]
    public float stunnedTransitionSpeed = 10f;

    [Header("离地坠落")]
    [Tooltip("Ground Bot 与 Spike 被推出平台后自动启用重力。Ophanim 不使用此规则。")]
    public bool enableGroundEnemyFall = true;

    [Min(0.02f)]
    [Tooltip("从敌人包围盒底部向下检查支撑面的距离。")]
    public float supportProbeDistance = 0.28f;

    [Range(0.02f, 0.45f)]
    [Tooltip("支撑检测球半径相对敌人水平包围盒的比例。数值越小越容易从平台边缘掉落。")]
    public float supportProbeRadiusScale = 0.14f;

    [Min(0f)]
    [Tooltip("开始坠落后，等待这段时间才允许判定已经落地，避免刚离开边缘就被误判。")]
    public float landingCheckDelay = 0.12f;

    [Header("环境击杀归因")]
    [Min(0f)]
    public float defaultEnvironmentCreditDuration = 3f;

    [Header("运行状态")]
    [SerializeField]
    private bool stunActive;

    [SerializeField]
    private float stunRemaining;

    [SerializeField]
    private bool knockbackActive;

    [SerializeField]
    private float knockbackDistance;

    [SerializeField]
    private float knockbackDuration;

    [SerializeField]
    private float knockbackElapsed;

    [SerializeField]
    private bool fallingActive;

    [SerializeField]
    private float fallingElapsed;

    private Vector3 _knockbackDirection;
    private float _previousKnockbackTravel;
    private float _stunVisualElapsed;

    private IEnemyExternalControlReceiver[] _receivers =
        Array.Empty<IEnemyExternalControlReceiver>();

    private EnemyContactDamage[] _contactDamages =
        Array.Empty<EnemyContactDamage>();

    private Collider[] _bodyColliders =
        Array.Empty<Collider>();

    private Renderer[] _bodyRenderers =
        Array.Empty<Renderer>();

    private MaterialPropertyBlock _propertyBlock;
    private MaterialPropertyBlock[] _originalPropertyBlocks =
        Array.Empty<MaterialPropertyBlock>();
    private Material[][] _originalSharedMaterials =
        Array.Empty<Material[]>();
    private Material _stunOverrideMaterial;
    private bool _stunVisualApplied;

    private bool _externalControlEngaged;
    private bool _environmentMaskWarningLogged;
    private bool _isSupportedGroundEnemy;

    private bool _rigidbodyStateCached;
    private bool _originalUseGravity;
    private bool _originalIsKinematic;
    private RigidbodyConstraints _originalConstraints;
    private CollisionDetectionMode _originalCollisionDetectionMode;
    private RigidbodyInterpolation _originalInterpolation;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    public bool IsStunned => stunActive;
    public bool IsKnockbackActive => knockbackActive;
    public bool IsFalling => fallingActive;
    public float StunRemaining => Mathf.Max(0f, stunRemaining);

    private void Awake()
    {
        ResolveReferences();

        if (environmentMask.value == 0)
        {
            environmentMask =
                LayerMask.GetMask("Environment");
        }

        _propertyBlock = new MaterialPropertyBlock();

        CacheRigidbodyState();
        CreateStunOverrideMaterial();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheRigidbodyState();

        if (enemyTarget != null)
        {
            enemyTarget.Died += HandleEnemyDied;
        }
    }

    private void OnDisable()
    {
        if (enemyTarget != null)
        {
            enemyTarget.Died -= HandleEnemyDied;
        }

        ClearRuntimeState(false);
    }

    private void OnDestroy()
    {
        RestoreStunVisual();

        if (_stunOverrideMaterial != null)
        {
            Destroy(_stunOverrideMaterial);
            _stunOverrideMaterial = null;
        }
    }

    private void Update()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            return;
        }

        if (stunActive)
        {
            stunRemaining -= Time.deltaTime;

            if (stunRemaining <= 0f)
            {
                stunActive = false;
                stunRemaining = 0f;
                RestoreStunVisual();
                TryReleaseExternalControl();
            }
        }
    }

    private void FixedUpdate()
    {
        if (fallingActive)
        {
            UpdateUnsupportedFall();
            return;
        }

        if (!knockbackActive)
        {
            return;
        }

        if (enemyTarget != null && enemyTarget.IsDead)
        {
            StopKnockback(false);
            return;
        }

        float safeDuration =
            Mathf.Max(0.01f, knockbackDuration);

        knockbackElapsed += Time.fixedDeltaTime;

        float normalizedTime =
            Mathf.Clamp01(
                knockbackElapsed / safeDuration
            );

        // EaseOutCubic：前段冲击强，后段自然收住。
        float easedProgress =
            1f - Mathf.Pow(1f - normalizedTime, 3f);

        float targetTravel =
            knockbackDistance * easedProgress;

        float stepDistance =
            Mathf.Max(
                0f,
                targetTravel - _previousKnockbackTravel
            );

        _previousKnockbackTravel = targetTravel;

        if (stepDistance > 0.00001f)
        {
            MoveWithEnvironmentCollision(
                _knockbackDirection,
                stepDistance
            );
        }

        if (normalizedTime >= 1f)
        {
            StopKnockback(true);
        }
    }

    private void LateUpdate()
    {
        if (!stunActive)
        {
            return;
        }

        ApplySharedStunVisual();
    }

    /// <summary>
    /// 施加或刷新硬直。
    /// 新硬直从当前时刻重新计完整时长，不与旧剩余时间相加。
    /// </summary>
    public void ApplyStun(float duration)
    {
        if (!CanReceiveControl())
        {
            return;
        }

        float safeDuration =
            Mathf.Max(0f, duration);

        if (safeDuration <= 0f)
        {
            return;
        }

        EngageExternalControl();

        stunActive = true;
        stunRemaining = safeDuration;
        _stunVisualElapsed = 0f;

        BeginStunVisual();
        ApplySharedStunVisual();
    }

    /// <summary>
    /// 施加受控击退。
    /// 新击退会替换当前未完成的击退，不会叠加位移。
    /// </summary>
    public void ApplyControlledKnockback(
        Vector3 worldDirection,
        float distance,
        float duration,
        bool registerPlayerEnvironmentCredit = true,
        float environmentCreditDuration = -1f
    )
    {
        if (!CanReceiveControl())
        {
            return;
        }

        float safeDistance = Mathf.Max(0f, distance);
        float safeDuration = Mathf.Max(0.01f, duration);

        if (safeDistance <= 0f ||
            worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (fallingActive)
        {
            return;
        }

        EngageExternalControl();

        _knockbackDirection =
            worldDirection.normalized;

        knockbackDistance = safeDistance;
        knockbackDuration = safeDuration;
        knockbackElapsed = 0f;
        _previousKnockbackTravel = 0f;
        knockbackActive = true;

        if (registerPlayerEnvironmentCredit &&
            enemyTarget != null)
        {
            float creditDuration =
                environmentCreditDuration >= 0f
                    ? environmentCreditDuration
                    : defaultEnvironmentCreditDuration;

            enemyTarget.RegisterPlayerEnvironmentKillCredit(
                creditDuration
            );
        }
    }

    public void ApplyStunAndKnockback(
        float stunDuration,
        Vector3 worldDirection,
        float distance,
        float knockbackTime,
        float environmentCreditDuration = -1f
    )
    {
        ApplyStun(stunDuration);

        ApplyControlledKnockback(
            worldDirection,
            distance,
            knockbackTime,
            true,
            environmentCreditDuration
        );
    }

    public void CancelAllControlEffects()
    {
        ClearRuntimeState(true);
    }

    private bool CanReceiveControl()
    {
        return
            enabled &&
            gameObject.activeInHierarchy &&
            (enemyTarget == null || !enemyTarget.IsDead);
    }

    private void EngageExternalControl()
    {
        if (_externalControlEngaged)
        {
            DisableAllContactDamage();
            return;
        }

        _externalControlEngaged = true;

        ResolveReferences();

        foreach (IEnemyExternalControlReceiver receiver
                 in _receivers)
        {
            receiver?.BeginExternalControl();
        }

        DisableAllContactDamage();
    }

    private void TryReleaseExternalControl()
    {
        if (stunActive || knockbackActive || fallingActive)
        {
            return;
        }

        if (!_externalControlEngaged)
        {
            return;
        }

        _externalControlEngaged = false;

        foreach (IEnemyExternalControlReceiver receiver
                 in _receivers)
        {
            receiver?.EndExternalControl();
        }

        SetContactDamageSuppressed(false);
    }

    private void StopKnockback(bool checkForUnsupportedFall)
    {
        knockbackActive = false;
        knockbackElapsed = 0f;
        knockbackDistance = 0f;
        knockbackDuration = 0f;
        _previousKnockbackTravel = 0f;
        _knockbackDirection = Vector3.zero;

        if (checkForUnsupportedFall &&
            TryBeginUnsupportedFall())
        {
            return;
        }

        TryReleaseExternalControl();
    }

    private void MoveWithEnvironmentCollision(
        Vector3 direction,
        float requestedDistance
    )
    {
        if (requestedDistance <= 0f)
        {
            return;
        }

        if (environmentMask.value == 0)
        {
            if (!_environmentMaskWarningLogged)
            {
                Debug.LogWarning(
                    $"[EnemyControlEffectController] {gameObject.name} " +
                    "没有找到 Environment Layer。击退将执行，但无法阻挡墙体。"
                );

                _environmentMaskWarningLogged = true;
            }

            MoveRoot(direction * requestedDistance);
            return;
        }

        Bounds castBounds;

        if (!TryGetBodyBounds(out castBounds))
        {
            MoveRoot(direction * requestedDistance);
            return;
        }

        Vector3 halfExtents =
            castBounds.extents * castBoundsScale;

        halfExtents.x = Mathf.Max(0.03f, halfExtents.x);
        halfExtents.y = Mathf.Max(0.03f, halfExtents.y);
        halfExtents.z = Mathf.Max(0.03f, halfExtents.z);

        float castDistance =
            requestedDistance + collisionSkin;

        bool blocked =
            Physics.BoxCast(
                castBounds.center,
                halfExtents,
                direction,
                out RaycastHit hit,
                Quaternion.identity,
                castDistance,
                environmentMask,
                QueryTriggerInteraction.Ignore
            );

        float allowedDistance = requestedDistance;

        if (blocked)
        {
            allowedDistance =
                Mathf.Max(
                    0f,
                    hit.distance - collisionSkin
                );
        }

        if (allowedDistance > 0.00001f)
        {
            MoveRoot(direction * allowedDistance);
        }
    }

    private void MoveRoot(Vector3 displacement)
    {
        if (controlledRigidbody != null)
        {
            controlledRigidbody.MovePosition(
                controlledRigidbody.position + displacement
            );
        }
        else
        {
            transform.position += displacement;
        }
    }

    private bool TryGetBodyBounds(out Bounds bounds)
    {
        bool initialized = false;
        bounds = new Bounds(transform.position, Vector3.zero);

        foreach (Collider bodyCollider in _bodyColliders)
        {
            if (bodyCollider == null ||
                !bodyCollider.enabled ||
                bodyCollider.isTrigger)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = bodyCollider.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(bodyCollider.bounds);
            }
        }

        return initialized;
    }

    // =========================================================
    // 被推出平台后的重力坠落
    // =========================================================

    private bool TryBeginUnsupportedFall()
    {
        if (!enableGroundEnemyFall ||
            !_isSupportedGroundEnemy ||
            controlledRigidbody == null ||
            environmentMask.value == 0)
        {
            return false;
        }

        if (HasSupportBelow())
        {
            return false;
        }

        BeginUnsupportedFall();
        return true;
    }

    private void BeginUnsupportedFall()
    {
        if (fallingActive || controlledRigidbody == null)
        {
            return;
        }

        CacheRigidbodyState();

        fallingActive = true;
        fallingElapsed = 0f;

        if (controlledRigidbody.isKinematic)
        {
            controlledRigidbody.isKinematic = false;
        }

        controlledRigidbody.useGravity = true;
        controlledRigidbody.constraints =
            _originalConstraints &
            ~RigidbodyConstraints.FreezePositionY;
        controlledRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;
        controlledRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        controlledRigidbody.linearVelocity = Vector3.zero;
        controlledRigidbody.angularVelocity = Vector3.zero;
        controlledRigidbody.WakeUp();
    }

    private void UpdateUnsupportedFall()
    {
        if (enemyTarget != null && enemyTarget.IsDead)
        {
            fallingActive = false;
            return;
        }

        if (controlledRigidbody == null)
        {
            fallingActive = false;
            TryReleaseExternalControl();
            return;
        }

        fallingElapsed += Time.fixedDeltaTime;

        if (fallingElapsed < landingCheckDelay)
        {
            return;
        }

        if (controlledRigidbody.linearVelocity.y > 0.1f)
        {
            return;
        }

        if (!HasSupportBelow())
        {
            return;
        }

        EndUnsupportedFall();
    }

    private void EndUnsupportedFall()
    {
        if (!fallingActive)
        {
            return;
        }

        fallingActive = false;
        fallingElapsed = 0f;

        RestoreRigidbodyState();
        TryReleaseExternalControl();
    }

    private bool HasSupportBelow()
    {
        if (environmentMask.value == 0)
        {
            return true;
        }

        if (!TryGetBodyBounds(out Bounds bounds))
        {
            return true;
        }

        float horizontalExtent =
            Mathf.Min(bounds.extents.x, bounds.extents.z);

        float probeRadius =
            Mathf.Clamp(
                horizontalExtent * supportProbeRadiusScale,
                0.025f,
                0.12f
            );

        // 让检测球的底部略高于敌人包围盒底面，
        // 避免检测球初始就与地面重叠而漏检。
        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + probeRadius + 0.035f,
            bounds.center.z
        );

        return Physics.SphereCast(
            origin,
            probeRadius,
            Vector3.down,
            out _,
            Mathf.Max(0.02f, supportProbeDistance),
            environmentMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void CacheRigidbodyState()
    {
        if (_rigidbodyStateCached ||
            controlledRigidbody == null)
        {
            return;
        }

        _originalUseGravity = controlledRigidbody.useGravity;
        _originalIsKinematic = controlledRigidbody.isKinematic;
        _originalConstraints = controlledRigidbody.constraints;
        _originalCollisionDetectionMode =
            controlledRigidbody.collisionDetectionMode;
        _originalInterpolation =
            controlledRigidbody.interpolation;
        _rigidbodyStateCached = true;
    }

    private void RestoreRigidbodyState()
    {
        if (!_rigidbodyStateCached ||
            controlledRigidbody == null)
        {
            return;
        }

        if (!controlledRigidbody.isKinematic)
        {
            controlledRigidbody.linearVelocity = Vector3.zero;
            controlledRigidbody.angularVelocity = Vector3.zero;
        }

        controlledRigidbody.useGravity = _originalUseGravity;
        controlledRigidbody.constraints = _originalConstraints;
        controlledRigidbody.collisionDetectionMode =
            _originalCollisionDetectionMode;
        controlledRigidbody.interpolation =
            _originalInterpolation;
        controlledRigidbody.isKinematic = _originalIsKinematic;
    }

    // =========================================================
    // 接触伤害
    // =========================================================

    private void DisableAllContactDamage()
    {
        SetContactDamageSuppressed(true);
    }

    private void SetContactDamageSuppressed(bool suppressed)
    {
        foreach (EnemyContactDamage contactDamage
                 in _contactDamages)
        {
            if (contactDamage != null)
            {
                contactDamage.SetExternalControlSuppressed(
                    suppressed
                );
            }
        }
    }

    // =========================================================
    // 专用全身白色覆盖
    // =========================================================

    private void CreateStunOverrideMaterial()
    {
        if (_stunOverrideMaterial != null)
        {
            return;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            Debug.LogWarning(
                $"[EnemyControlEffectController] {gameObject.name} " +
                "找不到可用的白色硬直 Shader，将回退到原材质颜色覆盖。",
                this
            );
            return;
        }

        _stunOverrideMaterial = new Material(shader)
        {
            name = $"Runtime_StunWhite_{GetEntityId()}",
            hideFlags = HideFlags.HideAndDontSave,
            enableInstancing = true
        };

        if (_stunOverrideMaterial.HasProperty("_BaseMap"))
        {
            _stunOverrideMaterial.SetTexture("_BaseMap", null);
        }

        if (_stunOverrideMaterial.HasProperty("_MainTex"))
        {
            _stunOverrideMaterial.SetTexture("_MainTex", null);
        }
    }

    private void BeginStunVisual()
    {
        if (_stunVisualApplied)
        {
            return;
        }

        ResolveReferences();
        CreateStunOverrideMaterial();

        _originalSharedMaterials =
            new Material[_bodyRenderers.Length][];
        _originalPropertyBlocks =
            new MaterialPropertyBlock[_bodyRenderers.Length];

        for (int index = 0;
             index < _bodyRenderers.Length;
             index++)
        {
            Renderer bodyRenderer = _bodyRenderers[index];

            if (bodyRenderer == null)
            {
                continue;
            }

            Material[] originalMaterials =
                bodyRenderer.sharedMaterials;

            _originalSharedMaterials[index] = originalMaterials;

            MaterialPropertyBlock originalBlock =
                new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(originalBlock);
            _originalPropertyBlocks[index] = originalBlock;

            if (_stunOverrideMaterial == null ||
                originalMaterials == null ||
                originalMaterials.Length == 0)
            {
                continue;
            }

            Material[] overrideMaterials =
                new Material[originalMaterials.Length];

            for (int materialIndex = 0;
                 materialIndex < overrideMaterials.Length;
                 materialIndex++)
            {
                overrideMaterials[materialIndex] =
                    _stunOverrideMaterial;
            }

            bodyRenderer.sharedMaterials = overrideMaterials;
        }

        _stunVisualApplied = true;
    }

    private void ApplySharedStunVisual()
    {
        if (_propertyBlock == null)
        {
            return;
        }

        _stunVisualElapsed += Time.deltaTime;

        float pulse =
            0.5f +
            0.5f * Mathf.Cos(
                _stunVisualElapsed *
                Mathf.Max(0f, stunnedFlashSpeed) *
                Mathf.PI * 2f
            );

        Color pulseColor =
            Color.Lerp(
                stunnedBaseColor,
                Color.white,
                pulse
            );

        float intensity =
            1f +
            Mathf.Max(0f, stunnedEmission) *
            Mathf.Lerp(0.35f, 1f, pulse);

        Color outputColor = pulseColor * intensity;
        outputColor.a = 1f;

        if (_stunOverrideMaterial != null)
        {
            SetMaterialColor(
                _stunOverrideMaterial,
                outputColor
            );
        }

        foreach (Renderer bodyRenderer in _bodyRenderers)
        {
            if (bodyRenderer == null ||
                !bodyRenderer.enabled ||
                !bodyRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            _propertyBlock.Clear();
            bodyRenderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetColor(
                BaseColorId,
                outputColor
            );
            _propertyBlock.SetColor(
                ColorId,
                outputColor
            );
            _propertyBlock.SetColor(
                EmissionColorId,
                outputColor
            );

            bodyRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private static void SetMaterialColor(
        Material material,
        Color color
    )
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }

        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, color);
        }
    }

    private void RestoreStunVisual()
    {
        if (!_stunVisualApplied)
        {
            return;
        }

        int count = Mathf.Min(
            _bodyRenderers.Length,
            _originalSharedMaterials.Length
        );

        for (int index = 0; index < count; index++)
        {
            Renderer bodyRenderer = _bodyRenderers[index];

            if (bodyRenderer == null)
            {
                continue;
            }

            Material[] originalMaterials =
                _originalSharedMaterials[index];

            if (originalMaterials != null)
            {
                bodyRenderer.sharedMaterials = originalMaterials;
            }

            if (index < _originalPropertyBlocks.Length &&
                _originalPropertyBlocks[index] != null)
            {
                bodyRenderer.SetPropertyBlock(
                    _originalPropertyBlocks[index]
                );
            }
            else
            {
                bodyRenderer.SetPropertyBlock(null);
            }
        }

        _originalSharedMaterials = Array.Empty<Material[]>();
        _originalPropertyBlocks =
            Array.Empty<MaterialPropertyBlock>();
        _stunVisualApplied = false;
        _stunVisualElapsed = 0f;
    }

    private void ResolveReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        if (controlledRigidbody == null)
        {
            controlledRigidbody = GetComponent<Rigidbody>();
        }

        _isSupportedGroundEnemy =
            GetComponent<GroundBotEnemy>() != null ||
            GetComponent<SpikeEnemy>() != null;

        MonoBehaviour[] behaviours =
            GetComponents<MonoBehaviour>();

        int receiverCount = 0;

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IEnemyExternalControlReceiver)
            {
                receiverCount++;
            }
        }

        _receivers =
            new IEnemyExternalControlReceiver[receiverCount];

        int receiverIndex = 0;

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IEnemyExternalControlReceiver receiver)
            {
                _receivers[receiverIndex] = receiver;
                receiverIndex++;
            }
        }

        _contactDamages =
            GetComponentsInChildren<EnemyContactDamage>(true);

        _bodyColliders =
            GetComponentsInChildren<Collider>(true);

        Renderer[] allRenderers =
            GetComponentsInChildren<Renderer>(true);

        int bodyRendererCount = 0;

        foreach (Renderer candidate in allRenderers)
        {
            if (!IsBodyRenderer(candidate))
            {
                continue;
            }

            bodyRendererCount++;
        }

        _bodyRenderers =
            new Renderer[bodyRendererCount];

        int bodyRendererIndex = 0;

        foreach (Renderer candidate in allRenderers)
        {
            if (!IsBodyRenderer(candidate))
            {
                continue;
            }

            _bodyRenderers[bodyRendererIndex] = candidate;
            bodyRendererIndex++;
        }
    }

    private bool IsBodyRenderer(Renderer candidate)
    {
        if (candidate == null ||
            !(candidate is MeshRenderer) &&
            !(candidate is SkinnedMeshRenderer))
        {
            return false;
        }

        GroundBotProjectile projectileOwner =
            candidate.GetComponentInParent<GroundBotProjectile>();

        if (projectileOwner != null)
        {
            return false;
        }

        // Ground Bot 普通命中白闪使用独立的运行时覆盖 Renderer。
        // 它不是敌人真实机体，不能被白色硬直系统再次缓存或替换材质。
        if (candidate.gameObject.name.StartsWith(
                "GroundBotHitFlashOverlay_Runtime"
            ))
        {
            return false;
        }

        return true;
    }

    private void HandleEnemyDied(EnemyDeathInfo deathInfo)
    {
        ClearRuntimeState(false);
    }

    private void ClearRuntimeState(bool notifyReceivers)
    {
        RestoreStunVisual();

        bool enemyIsDead =
            enemyTarget != null && enemyTarget.IsDead;

        if (fallingActive && !enemyIsDead)
        {
            RestoreRigidbodyState();
        }

        stunActive = false;
        stunRemaining = 0f;
        knockbackActive = false;
        knockbackDistance = 0f;
        knockbackDuration = 0f;
        knockbackElapsed = 0f;
        _previousKnockbackTravel = 0f;
        _knockbackDirection = Vector3.zero;
        fallingActive = false;
        fallingElapsed = 0f;

        if (notifyReceivers && _externalControlEngaged)
        {
            foreach (IEnemyExternalControlReceiver receiver
                     in _receivers)
            {
                receiver?.EndExternalControl();
            }

            SetContactDamageSuppressed(false);
        }

        _externalControlEngaged = false;
    }

    // =========================================================
    // Play Mode 调试入口
    // =========================================================

    [ContextMenu("DEBUG/Apply Full Stun 0.6s")]
    private void DebugApplyFullStun()
    {
        ApplyStun(0.6f);
    }

    [ContextMenu("DEBUG/Apply Half Stun 0.3s")]
    private void DebugApplyHalfStun()
    {
        ApplyStun(0.3f);
    }

    [ContextMenu("DEBUG/Apply Full Stun + 2m Knockback")]
    private void DebugApplyFullControl()
    {
        Vector3 direction = -transform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.back;
        }

        ApplyStunAndKnockback(
            0.6f,
            direction.normalized,
            2f,
            0.18f,
            3f
        );
    }

    [ContextMenu("DEBUG/Apply Half Stun + 1m Knockback")]
    private void DebugApplyHalfControl()
    {
        Vector3 direction = -transform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.back;
        }

        ApplyStunAndKnockback(
            0.3f,
            direction.normalized,
            1f,
            0.12f,
            3f
        );
    }
}
