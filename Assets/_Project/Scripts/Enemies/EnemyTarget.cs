#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;

public enum EnemyDeathCause
{
    PlayerDirectShot,
    Environment,
    LegacyDirect,

    // 玩家利用 Ground Bot 子弹完成的直接击杀。
    PlayerIndirectProjectile,

    // 敌人被玩家利用 Ground Bot 强化弹推出平台后，
    // 在归因窗口内由环境完成击杀。
    PlayerAttributedEnvironment
}

public enum EnemyDamageSource
{
    PlayerWeapon,
    GroundBotProjectile,
    Environment,
    Legacy,
    OctahedronBlast
}

public readonly struct EnemyDeathInfo
{
    public readonly EnemyTarget Target;
    public readonly EnemyDeathCause Cause;
    public readonly EnemyDamageSource DamageSource;
    public readonly Vector3 KillPosition;
    public readonly Vector3 ImpactPoint;
    public readonly bool RewardsGranted;

    public bool WasDirectPlayerShot =>
        Cause == EnemyDeathCause.PlayerDirectShot;

    public bool WasPlayerCredited =>
        Cause == EnemyDeathCause.PlayerDirectShot ||
        Cause == EnemyDeathCause.PlayerIndirectProjectile ||
        Cause == EnemyDeathCause.PlayerAttributedEnvironment;

    public EnemyDeathInfo(
        EnemyTarget target,
        EnemyDeathCause cause,
        EnemyDamageSource damageSource,
        Vector3 killPosition,
        Vector3 impactPoint,
        bool rewardsGranted
    )
    {
        Target = target;
        Cause = cause;
        DamageSource = damageSource;
        KillPosition = killPosition;
        ImpactPoint = impactPoint;
        RewardsGranted = rewardsGranted;
    }
}

[DisallowMultipleComponent]
public class EnemyTarget : MonoBehaviour
{
    [Header("📝 终端代号 (Terminal ID)")]
    [Tooltip("敌人死亡后显示的代号，例如 SPIKE、TURRET。")]
    public string targetCodeName = "CHASER";

    [Header("❤️ 生命值")]
    [Min(1f)]
    [Tooltip("敌人的最大生命值。")]
    public float maxHealth = 100f;

    [SerializeField]
    [Tooltip("当前生命值。运行时自动从 Max Health 开始。")]
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    public float HealthNormalized
    {
        get
        {
            if (maxHealth <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                currentHealth / maxHealth
            );
        }
    }

    [Header("📈 击杀奖励")]
    [Tooltip("击杀此敌人奖励的分数。")]
    public float scoreReward = 1500f;

    [Tooltip("击杀此敌人倒流的时间，单位为秒。")]
    public float timeReward = 0.5f;

    [Header("💥 死亡视觉")]
    [Tooltip(
        "枪械击杀时使用的专属死亡效果。" +
        "留空时使用武器提供的备用效果。"
    )]
    public GameObject specificDeathEffect;

    [Tooltip(
        "掉出平台或撞击场景死亡时使用的效果。" +
        "留空时退回使用枪械死亡效果。"
    )]
    public GameObject environmentalDeathEffect;

    [Header("环境击杀设置")]
    [Tooltip(
        "普通环境击杀是否也提供分数、减时和旧 Dash 奖励。" +
        "Ground Bot 强化弹造成的归因环境击杀不受此开关影响。"
    )]
    public bool rewardEnvironmentalKill = true;

    [Header("运行状态")]
    [SerializeField]
    private bool isDead;

    [SerializeField]
    private EnemyDeathCause lastDeathCause =
        EnemyDeathCause.LegacyDirect;

    [SerializeField]
    private EnemyDamageSource lastDamageSource =
        EnemyDamageSource.Legacy;

    [SerializeField]
    [Tooltip("仅用于运行时调试：玩家环境击杀归因窗口的结束时间。")]
    private float playerEnvironmentCreditExpiresAt = -1f;

    public bool IsDead => isDead;

    public EnemyDeathCause LastDeathCause =>
        lastDeathCause;

    public EnemyDamageSource LastDamageSource =>
        lastDamageSource;

    public bool WasKilledByDirectPlayerShot =>
        isDead &&
        lastDeathCause ==
        EnemyDeathCause.PlayerDirectShot;

    public bool HasActivePlayerEnvironmentCredit =>
        !isDead &&
        playerEnvironmentCreditExpiresAt >= Time.time;

    /// <summary>
    /// 参数：
    /// 实际伤害、剩余生命值、命中位置。
    /// </summary>
    public event Action<float, float, Vector3> Damaged;

    /// <summary>
    /// 当前敌人的死亡事件。
    /// </summary>
    public event Action<EnemyDeathInfo> Died;

    /// <summary>
    /// 全局敌人死亡事件。
    /// 后续 TP 系统可以直接订阅，并筛选 PlayerDirectShot。
    /// </summary>
    public static event Action<EnemyDeathInfo> AnyEnemyDied;

    private void Awake()
    {
        maxHealth =
            Mathf.Max(1f, maxHealth);

        currentHealth = maxHealth;
        isDead = false;
        playerEnvironmentCreditExpiresAt = -1f;

        // 为目前正式接入共享敌人框架的敌人自动补齐
        // 外部硬直与受控击退底层，避免影响其他旧 EnemyTarget。
        bool supportsExternalControl =
            GetComponent<GroundBotEnemy>() != null ||
            GetComponent<SpikeEnemy>() != null ||
            GetComponent<OphanimEnemy>() != null ||
            GetComponent<FlyingBotEnemy>() != null ||
            GetComponent<OctahedronEnemy>() != null;

        if (supportsExternalControl &&
            GetComponent<EnemyControlEffectController>() == null)
        {
            gameObject.AddComponent<EnemyControlEffectController>();
        }

        // Shared world-space health display for the currently supported
        // combat enemies, including Flying Bot Phase 2A.
        // F8 toggles all displays at runtime.
        if (supportsExternalControl &&
            GetComponent<EnemyDebugHealthDisplay>() == null)
        {
            gameObject.AddComponent<EnemyDebugHealthDisplay>();
        }
    }

    // =========================================================
    // 正常伤害
    // =========================================================

    /// <summary>
    /// 玩家枪械对敌人造成伤害。
    /// 保留原有方法签名，现有 PlayerWeapon 无需修改。
    /// 返回本次实际扣除的生命值。
    /// </summary>
    public float TakeDamage(
        float damage,
        Vector3 hitPoint,
        Vector3 hitNormal,
        GameObject fallbackHitEffect = null
    )
    {
        return ApplyDamage(
            damage,
            hitPoint,
            hitNormal,
            fallbackHitEffect,
            EnemyDamageSource.PlayerWeapon,
            true,
            EnemyDeathCause.PlayerDirectShot
        );
    }

    /// <summary>
    /// Ground Bot 子弹对任意敌人造成伤害时使用。
    /// 无论是否反弹，直接击杀都按玩家间接击杀结算完整奖励。
    /// </summary>
    public float TakeDamageFromGroundBotProjectile(
        float damage,
        Vector3 hitPoint,
        Vector3 hitNormal,
        GameObject fallbackHitEffect = null
    )
    {
        return ApplyDamage(
            damage,
            hitPoint,
            hitNormal,
            fallbackHitEffect,
            EnemyDamageSource.GroundBotProjectile,
            true,
            EnemyDeathCause.PlayerIndirectProjectile
        );
    }

    public float TakeDamageFromOctahedronBlast(
        float damage,
        Vector3 hitPoint,
        Vector3 hitNormal
    )
    {
        return ApplyDamage(
            damage,
            hitPoint,
            hitNormal,
            null,
            EnemyDamageSource.OctahedronBlast,
            true,
            EnemyDeathCause.PlayerAttributedEnvironment
        );
    }

    private float ApplyDamage(
        float damage,
        Vector3 hitPoint,
        Vector3 hitNormal,
        GameObject fallbackHitEffect,
        EnemyDamageSource damageSource,
        bool grantRewardsOnDeath,
        EnemyDeathCause deathCause
    )
    {
        if (isDead)
        {
            return 0f;
        }

        if (damage <= 0f)
        {
            return 0f;
        }

        lastDamageSource = damageSource;

        float healthBeforeDamage =
            currentHealth;

        currentHealth =
            Mathf.Max(
                0f,
                currentHealth - damage
            );

        float actualDamage =
            healthBeforeDamage -
            currentHealth;

        Damaged?.Invoke(
            actualDamage,
            currentHealth,
            hitPoint
        );

        if (currentHealth <= 0f)
        {
            CompleteDeath(
                hitPoint,
                hitNormal,
                specificDeathEffect,
                fallbackHitEffect,
                grantRewardsOnDeath,
                deathCause,
                damageSource
            );
        }
        else
        {
            // 非致命命中播放局部命中特效。
            // 头顶系统标记由 EnemyHitMarker 监听 Damaged 事件。
            SpawnEffect(
                fallbackHitEffect,
                hitPoint,
                hitNormal
            );
        }

        return actualDamage;
    }

    // =========================================================
    // 兼容旧代码的直接死亡接口
    // =========================================================

    /// <summary>
    /// 保留旧接口，避免其他旧脚本报错。
    /// 该接口不是经过 PlayerWeapon 的正式直接子弹击杀，
    /// 因此不会被 TP 识别为直接枪杀。
    /// </summary>
    public void Die(
        Vector3 hitPoint,
        Vector3 hitNormal,
        GameObject fallbackEffect
    )
    {
        if (isDead)
        {
            return;
        }

        currentHealth = 0f;
        lastDamageSource = EnemyDamageSource.Legacy;

        CompleteDeath(
            hitPoint,
            hitNormal,
            specificDeathEffect,
            fallbackEffect,
            true,
            EnemyDeathCause.LegacyDirect,
            EnemyDamageSource.Legacy
        );
    }

    // =========================================================
    // 玩家环境击杀归因
    // =========================================================

    /// <summary>
    /// Ground Bot 强化弹成功击退敌人时调用。
    /// 在该窗口内掉入虚空、KillZone 或其他环境死亡，
    /// 都按玩家制造的环境击杀结算完整奖励。
    /// 重复调用只会延长，不会缩短现有窗口。
    /// </summary>
    public void RegisterPlayerEnvironmentKillCredit(
        float duration = 3f
    )
    {
        if (isDead)
        {
            return;
        }

        float safeDuration =
            Mathf.Max(0f, duration);

        if (safeDuration <= 0f)
        {
            return;
        }

        playerEnvironmentCreditExpiresAt =
            Mathf.Max(
                playerEnvironmentCreditExpiresAt,
                Time.time + safeDuration
            );
    }

    public void ClearPlayerEnvironmentKillCredit()
    {
        playerEnvironmentCreditExpiresAt = -1f;
    }

    // =========================================================
    // 环境死亡
    // =========================================================

    /// <summary>
    /// Spike 掉下平台、撞到其他平台或进入 KillZone 时调用。
    /// 环境死亡无视剩余生命值。
    /// </summary>
    public void DieFromEnvironment(
        Vector3 deathPoint
    )
    {
        if (isDead)
        {
            return;
        }

        bool playerAttributed =
            HasActivePlayerEnvironmentCredit;

        currentHealth = 0f;

        GameObject selectedEffect =
            environmentalDeathEffect != null
                ? environmentalDeathEffect
                : specificDeathEffect;

        EnemyDeathCause deathCause =
            playerAttributed
                ? EnemyDeathCause.PlayerAttributedEnvironment
                : EnemyDeathCause.Environment;

        EnemyDamageSource damageSource =
            playerAttributed
                ? EnemyDamageSource.GroundBotProjectile
                : EnemyDamageSource.Environment;

        lastDamageSource = damageSource;

        CompleteDeath(
            deathPoint,
            Vector3.up,
            selectedEffect,
            null,
            playerAttributed || rewardEnvironmentalKill,
            deathCause,
            damageSource
        );
    }

    // =========================================================
    // 统一死亡结算
    // =========================================================

    private void CompleteDeath(
        Vector3 impactPoint,
        Vector3 impactNormal,
        GameObject preferredDeathEffect,
        GameObject fallbackEffect,
        bool grantRewards,
        EnemyDeathCause deathCause,
        EnemyDamageSource damageSource
    )
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHealth = 0f;
        lastDeathCause = deathCause;
        lastDamageSource = damageSource;
        playerEnvironmentCreditExpiresAt = -1f;

        Vector3 killPosition =
            transform.position;

        GameObject effectToPlay =
            preferredDeathEffect != null
                ? preferredDeathEffect
                : fallbackEffect;

        // Ground Bot 使用独立的“停机倾斜 -> 红色过载 -> 解体爆散”
        // 死亡代理。代理会在原敌人销毁前复制 VisualRoot，之后与
        // 伤害、AI、碰撞和奖励结算完全分离。
        bool customDeathHandled = false;

        GroundBotEnemy groundBot =
            GetComponent<GroundBotEnemy>();

        if (groundBot != null)
        {
            customDeathHandled =
                GroundBotDeathSequenceFX.TrySpawn(
                    groundBot,
                    impactPoint,
                    impactNormal
                );
        }

        if (!customDeathHandled)
        {
            FlyingBotEnemy flyingBot =
                GetComponent<FlyingBotEnemy>();

            if (flyingBot != null)
            {
                customDeathHandled =
                    FlyingBotDeathSequenceFX.TrySpawn(
                        flyingBot,
                        impactPoint,
                        impactNormal
                    );
            }
        }

        if (!customDeathHandled &&
            effectToPlay != null)
        {
            /*
             * 专属死亡 Prefab 放在敌人中心生成。
             * 普通备用命中特效才放在具体命中点。
             */
            Vector3 effectPosition =
                preferredDeathEffect != null
                    ? killPosition
                    : impactPoint;

            SpawnEffect(
                effectToPlay,
                effectPosition,
                impactNormal
            );
        }

        if (grantRewards)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddBonusScore(
                    scoreReward,
                    timeReward,
                    targetCodeName,
                    killPosition
                );
            }
        }

        EnemyDeathInfo deathInfo =
            new EnemyDeathInfo(
                this,
                deathCause,
                damageSource,
                killPosition,
                impactPoint,
                grantRewards
            );

        Died?.Invoke(deathInfo);
        AnyEnemyDied?.Invoke(deathInfo);

        PrepareForDestruction();
        Destroy(gameObject);
    }

    /// <summary>
    /// 销毁前立即关闭碰撞、刚体运动与敌人行为，
    /// 防止死亡结算后的同一帧继续造成伤害或移动。
    /// </summary>
    private void PrepareForDestruction()
    {
        Collider[] colliders =
            GetComponentsInChildren<
                Collider
            >(true);

        foreach (Collider enemyCollider
                 in colliders)
        {
            if (enemyCollider != null)
            {
                enemyCollider.enabled = false;
            }
        }

        Rigidbody[] rigidbodies =
            GetComponentsInChildren<
                Rigidbody
            >(true);

        foreach (Rigidbody body
                 in rigidbodies)
        {
            if (body == null)
            {
                continue;
            }

            // Unity 不允许给 Kinematic Rigidbody 设置线速度或角速度。
            // 仅动态刚体需要在切换为 Kinematic 前清零。
            if (!body.isKinematic)
            {
                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;
            }

            body.detectCollisions = false;
            body.isKinematic = true;
        }

        MonoBehaviour[] behaviours =
            GetComponentsInChildren<
                MonoBehaviour
            >(true);

        foreach (MonoBehaviour behaviour
                 in behaviours)
        {
            if (behaviour == null ||
                behaviour == this)
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private void SpawnEffect(
        GameObject effectPrefab,
        Vector3 spawnPosition,
        Vector3 direction
    )
    {
        if (effectPrefab == null)
        {
            return;
        }

        Quaternion rotation =
            Quaternion.identity;

        if (direction.sqrMagnitude > 0.0001f)
        {
            rotation =
                Quaternion.LookRotation(
                    direction.normalized
                );
        }

        Instantiate(
            effectPrefab,
            spawnPosition,
            rotation
        );
    }

    // =========================================================
    // 可选公共接口
    // =========================================================

    public void ResetHealthToFull()
    {
        if (isDead)
        {
            return;
        }

        currentHealth =
            Mathf.Max(1f, maxHealth);

        playerEnvironmentCreditExpiresAt = -1f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHealth =
            Mathf.Max(1f, maxHealth);

        if (!Application.isPlaying)
        {
            currentHealth = maxHealth;
            playerEnvironmentCreditExpiresAt = -1f;
        }
        else
        {
            currentHealth =
                Mathf.Clamp(
                    currentHealth,
                    0f,
                    maxHealth
                );
        }
    }
#endif
}
