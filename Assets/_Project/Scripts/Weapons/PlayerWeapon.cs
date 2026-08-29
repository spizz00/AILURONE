#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

using System.Collections;
using System.Collections.Generic;

using Random = UnityEngine.Random;
public readonly struct EnemyShotResult
{
    public readonly EnemyTarget Target;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;
    public readonly int PelletHits;
    public readonly int MaximumPellets;
    public readonly float TotalDamage;
    public readonly bool FiredAsAds;
    public readonly float Charge01;
    public readonly bool Killed;
    public readonly bool DirectPlayerKill;

    public bool WasCharged =>
        Charge01 > 0.001f;

    public bool WasFullyCharged =>
        Charge01 >= 0.999f;

    public EnemyShotResult(
        EnemyTarget target,
        Vector3 hitPoint,
        Vector3 hitNormal,
        int pelletHits,
        int maximumPellets,
        float totalDamage,
        bool firedAsAds,
        float charge01,
        bool killed,
        bool directPlayerKill
    )
    {
        Target = target;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        PelletHits = pelletHits;
        MaximumPellets = maximumPellets;
        TotalDamage = totalDamage;
        FiredAsAds = firedAsAds;
        Charge01 = Mathf.Clamp01(charge01);
        Killed = killed;
        DirectPlayerKill = directPlayerKill;
    }
}

[DisallowMultipleComponent]
public class PlayerWeapon : MonoBehaviour
{
    public event Action ShotFiredSuccessfully;
    public event Action AdsActivated;

    /// <summary>
    /// 当前武器每次对某个 EnemyTarget 完成一发射击结算后触发。
    /// 一发腰射对同一个敌人只触发一次。
    /// </summary>
    public event Action<EnemyShotResult> EnemyShotResolved;

    /// <summary>
    /// 全局射击结算事件。
    /// 后续 TP、受击 UI 或统计系统可以直接订阅。
    /// </summary>
    public static event Action<EnemyShotResult> AnyEnemyShotResolved;

    [Header("武器属性")]
    public int maxAmmo = 2;
    public int currentAmmo;

    [Header("🔫 双管散弹配置（腰射）")]
    public Transform leftBarrelPoint;
    public Transform rightBarrelPoint;

    [Tooltip(
        "散弹扩散系数。数值越大，弹丸分布越散。"
    )]
    public float spreadAngle = 0.04f;

    public float hipfireRange = 30f;

    [Min(1)]
    [Tooltip("每次腰射真正发出的独立弹丸数量。")]
    public int hipfirePelletCount = 10;

    [Min(0f)]
    [Tooltip("每颗散弹命中造成的伤害。")]
    public float pelletDamage = 12f;

    [Header("🎯 独头弹模式配置（右键瞄准）")]
    public Transform centerBarrelPoint;
    public float slugRange = 150f;

    [Min(0f)]
    [Tooltip("独头弹命中造成的伤害。")]
    public float slugDamage = 100f;

    [Header("ADS 蓄力")]
    [Min(0.05f)]
    [Tooltip("按住左键达到该真实时间后，松开才会发射满蓄力独头弹。")]
    public float adsChargeDuration = 0.65f;

    [Min(0f)]
    [Tooltip("满蓄力 ADS 独头弹的伤害。当前默认值可击杀 1200 生命的 Spike。")]
    public float chargedSlugDamage = 1200f;

    [Min(0.1f)]
    [Tooltip("伤害曲线指数。大于 1 时，中低蓄力的伤害增长更克制。")]
    public float adsChargeDamageExponent = 1.6f;

    public float slugTracerWidth = 0.1f;
    public float slugTracerSpeed = 800f;

    [Header("ADS Charge Tracer Visuals")]
    [Min(0.1f)]
    public float adsTracerMinimumWidthMultiplier = 0.95f;

    [Min(0.1f)]
    public float adsTracerMaximumWidthMultiplier = 2.05f;

    [Min(0.1f)]
    [Tooltip("Above 1 keeps early charge restrained and emphasizes the final charge.")]
    public float adsTracerResponseExponent = 1.20f;

    [ColorUsage(true, true)]
    public Color adsTracerLowChargeColor =
        new Color(1.10f, 0.025f, 0.008f, 1f);

    [ColorUsage(true, true)]
    public Color adsTracerFullChargeColor =
        new Color(4.20f, 0.18f, 0.035f, 1f);

    [Range(0f, 1f)]
    public float adsTracerHeadWhiteBlend = 0.78f;

    [Header("ADS Tracer Motion")]
    [Min(0f)]
    [Tooltip("Visual travel time of the ADS pulse. Kept short so it never reads as a stationary beam.")]
    public float adsTracerTravelDuration = 0.060f;

    [Min(0.05f)]
    public float adsTracerMinimumLength = 1.20f;

    [Min(0.05f)]
    public float adsTracerMaximumLength = 2.40f;

    [Header("Full Charge Tracer Shell")]
    [Range(0f, 1f)]
    public float fullChargeShellThreshold = 0.90f;

    [Min(1f)]
    public float fullChargeShellWidthMultiplier = 2.35f;

    [Range(0f, 1f)]
    public float fullChargeShellAlpha = 0.32f;

    [ColorUsage(true, true)]
    public Color fullChargeShellColor =
        new Color(1.60f, 0.025f, 0.006f, 1f);

    [Header("👊 程序化后坐力")]
    public Transform weaponVisualMesh;
    public float kickbackZ = 0.4f;
    public float kickbackRotX = 15f;
    public float recoilRecoverSpeed = 8f;

    [Header("🧱 动态防穿模")]
    [Tooltip("只选择实际环境墙体的 Layer。")]
    public LayerMask environmentLayer;

    [Tooltip("靠近墙壁多少米时开始抬枪。")]
    public float weaponLength = 1.2f;

    public Vector3 wallAvoidPosOffset =
        new Vector3(
            0f,
            -0.1f,
            -0.3f
        );

    public Vector3 wallAvoidRotOffset =
        new Vector3(
            50f,
            -20f,
            0f
        );

    public float avoidSmoothSpeed = 12f;

    [Header("基础射击配置")]
    [Tooltip(
        "子弹能够命中的 Layer。" +
        "至少应包含 Default、Environment、Enemy。"
    )]
    public LayerMask hitLayers;

    [Tooltip(
        "普通、非致命的敌人命中特效。" +
        "一发腰射对每个敌人最多生成一次。"
    )]
    public GameObject enemyHitEffect;

    public GameObject environmentHitEffect;

    public Material tracerMaterial;
    public float tracerWidth = 0.02f;
    public float tracerSpeed = 500f;
    public float tracerLength = 4f;

    public AudioClip shootSound;

    [Header("运行时伤害调试")]
    [Tooltip(
        "开启后，每次开枪会在 Console 显示命中弹丸数和实际伤害。"
    )]
    public bool logDamageResults = false;

    [Header("准星反馈")]
    [Tooltip(
        "开启后，把开枪、普通命中和击杀结果发送给 CrosshairController。"
    )]
    public bool sendCrosshairFeedback = true;

    [SerializeField]
    [Tooltip("上一发腰射有多少颗弹丸碰到了 EnemyTarget。")]
    private int lastHipfirePelletsHit;

    [SerializeField]
    [Tooltip("上一发腰射实际扣除了多少生命值。")]
    private float lastHipfireDamageDealt;

    [SerializeField]
    [Tooltip("上一发独头弹实际扣除了多少生命值。")]
    private float lastSlugDamageDealt;

    public bool IsAiming => _isAiming;

    public bool IsAdsCharging =>
        _isAdsCharging;

    public float AdsCharge01 =>
        _isAdsCharging
            ? Mathf.Clamp01(
                _adsChargeElapsed /
                Mathf.Max(0.05f, adsChargeDuration)
            )
            : 0f;

    public bool IsAdsChargeReady =>
        _isAdsCharging &&
        _adsChargeElapsed >= adsChargeDuration;

    public int LastHipfirePelletsHit =>
        lastHipfirePelletsHit;

    public float LastHipfireDamageDealt =>
        lastHipfireDamageDealt;

    public float LastSlugDamageDealt =>
        lastSlugDamageDealt;

    private float _currentWallWeight;

    private Vector3 _originalMeshPos;
    private Quaternion _originalMeshRot;

    private Vector3 _targetMeshPos;
    private Quaternion _targetMeshRot;

    private bool _isAiming;

    [SerializeField]
    private bool _isAdsCharging;

    [SerializeField]
    private float _adsChargeElapsed;

    private AudioSource _audioSource;
    private Camera _mainCamera;

    private readonly List<LineRenderer> _tracerPool =
        new List<LineRenderer>();

    private static readonly int TracerBaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int TracerColorId =
        Shader.PropertyToID("_Color");

    private static readonly int TracerEmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _tracerPropertyBlock;

    // Reused hitscan buffer. Weapon rays intentionally pass through
    // non-shootable gameplay trigger volumes (patrol/encounter/kill zones)
    // while still allowing EnemyTarget and TargetSwitch trigger colliders.
    private readonly RaycastHit[] _weaponRaycastBuffer =
        new RaycastHit[128];

    private const float GoldenAngleRadians =
        2.39996323f;

    /// <summary>
    /// 记录一发腰射对某个敌人的所有有效命中。
    /// 最后统一生成一次普通命中特效。
    /// </summary>
    private readonly struct HipfireHitSample
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;

        public HipfireHitSample(
            Vector3 point,
            Vector3 normal
        )
        {
            Point = point;
            Normal =
                normal.sqrMagnitude > 0.0001f
                    ? normal.normalized
                    : Vector3.up;
        }
    }

    private sealed class HipfireEnemyHitData
    {
        public Vector3 hitPointSum;
        public Vector3 hitNormalSum;

        public readonly List<HipfireHitSample> hitSamples =
            new List<HipfireHitSample>(10);

        public int damagingPelletCount;
        public float totalDamage;
        public bool killed;
    }

    private float _lastUnlockTime;

    private void Start()
    {
        currentAmmo = maxAmmo;
        _mainCamera = Camera.main;

        _audioSource =
            GetComponent<AudioSource>();

        if (_audioSource == null)
        {
            _audioSource =
                gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake =
                false;
        }

        if (weaponVisualMesh != null)
        {
            _originalMeshPos =
                weaponVisualMesh.localPosition;

            _originalMeshRot =
                weaponVisualMesh.localRotation;

            _targetMeshPos =
                _originalMeshPos;

            _targetMeshRot =
                _originalMeshRot;
        }
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _lastUnlockTime = Time.unscaledTime;
        }

        UpdateWallAvoidance();
        UpdateAimingState();
        UpdateAdsCharge();
    }

    // =========================================================
    // 动态防穿模
    // =========================================================

    private void UpdateWallAvoidance()
    {
        float targetWallWeight = 0f;

        if (_mainCamera != null)
        {
            Ray wallRay =
                new Ray(
                    _mainCamera.transform.position,
                    _mainCamera.transform.forward
                );

            if (Physics.Raycast(
                    wallRay,
                    out RaycastHit wallHit,
                    weaponLength,
                    environmentLayer,
                    QueryTriggerInteraction.Ignore
                ))
            {
                targetWallWeight =
                    1f -
                    wallHit.distance /
                    Mathf.Max(
                        0.01f,
                        weaponLength
                    );

                targetWallWeight =
                    Mathf.Clamp01(
                        targetWallWeight
                    );
            }
        }

        _currentWallWeight =
            Mathf.Lerp(
                _currentWallWeight,
                targetWallWeight,
                Time.deltaTime *
                avoidSmoothSpeed
            );

        if (weaponVisualMesh == null)
        {
            return;
        }

        Vector3 basePosition =
            _originalMeshPos +
            wallAvoidPosOffset *
            _currentWallWeight;

        Quaternion baseRotation =
            _originalMeshRot *
            Quaternion.Euler(
                wallAvoidRotOffset *
                _currentWallWeight
            );

        _targetMeshPos =
            Vector3.Lerp(
                _targetMeshPos,
                basePosition,
                Time.deltaTime *
                recoilRecoverSpeed
            );

        _targetMeshRot =
            Quaternion.Slerp(
                _targetMeshRot,
                baseRotation,
                Time.deltaTime *
                recoilRecoverSpeed
            );

        weaponVisualMesh.localPosition =
            _targetMeshPos;

        weaponVisualMesh.localRotation =
            _targetMeshRot;
    }

    private void UpdateAimingState()
    {
        bool wasAiming = _isAiming;

        if (AILURONEGameplayActionGate.AllowsGameplayActions &&
            transform.parent != null &&
            Mouse.current != null)
        {
            _isAiming =
                Mouse.current.rightButton.isPressed;
        }
        else
        {
            _isAiming = false;
        }

        if (!wasAiming && _isAiming)
        {
            AdsActivated?.Invoke();
        }
    }

    // =========================================================
    // 开火入口
    // =========================================================

    public void HandleFirePressed()
    {
        // Prevent accidental firing when clicking to regain focus (e.g. after pressing ESC in Editor)
        if (Cursor.lockState != CursorLockMode.Locked || Time.unscaledTime - _lastUnlockTime < 0.15f)
        {
            return;
        }

        if (_isAdsCharging)
        {
            return;
        }

        bool adsHeld =
            Mouse.current != null &&
            Mouse.current.rightButton.isPressed;

        if (!adsHeld)
        {
            TryShootInternal(
                false,
                0f
            );

            return;
        }

        if (currentAmmo <= 0)
        {
      Debug.Log("️ 没子弹了！");
            return;
        }

        _isAdsCharging = true;
        _adsChargeElapsed = 0f;
    }

    public void HandleFireReleased()
    {
        if (!_isAdsCharging)
        {
            return;
        }

        bool adsStillHeld =
            Mouse.current != null &&
            Mouse.current.rightButton.isPressed;

        float releasedCharge01 =
            AdsCharge01;

        _isAdsCharging = false;
        _adsChargeElapsed = 0f;

        if (adsStillHeld)
        {
            TryShootInternal(
                true,
                releasedCharge01
            );
        }
    }

    public void CancelAdsCharge()
    {
        _isAdsCharging = false;
        _adsChargeElapsed = 0f;
    }

    private void UpdateAdsCharge()
    {
        if (!_isAdsCharging)
        {
            return;
        }

        if (!_isAiming)
        {
            CancelAdsCharge();
            return;
        }

        _adsChargeElapsed =
            Mathf.Min(
                adsChargeDuration,
                _adsChargeElapsed +
                Time.unscaledDeltaTime
            );
    }

    public bool TryShoot()
    {
        return TryShootInternal(
            _isAiming,
            0f
        );
    }

    private bool TryShootInternal(
        bool firedAsAds,
        float charge01
    )
    {
        if (currentAmmo <= 0)
        {
      Debug.Log("️ 没子弹了！");
            return false;
        }

        currentAmmo--;

        /*
         * 现阶段仍保留原本的弹药逻辑。
         * 无限弹药、射速和输入缓冲会在下一阶段统一重构，
         * 这里暂时只接入准星反馈，避免同时改动太多系统。
         */
        PlayShootSound();
        ApplyWeaponRecoil();

        NotifyCrosshairShot(
            firedAsAds
        );

        ShotFiredSuccessfully?.Invoke();

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            Debug.LogWarning(
                "[PlayerWeapon] 没有找到 Main Camera，无法执行射线射击。"
            );

            return true;
        }

        if (firedAsAds)
        {
            FireSlug(
                charge01
            );
        }
        else
        {
            FireHipfire();
        }

        return true;
    }

    private void PlayShootSound()
    {
        if (shootSound == null ||
            _audioSource == null)
        {
            return;
        }

        _audioSource.pitch =
            Random.Range(
                0.9f,
                1.1f
            );

        _audioSource.PlayOneShot(
            shootSound
        );
    }

    private void ApplyWeaponRecoil()
    {
        if (weaponVisualMesh == null)
        {
            return;
        }

        _targetMeshPos +=
            new Vector3(
                0f,
                0f,
                -kickbackZ
            );

        _targetMeshRot *=
            Quaternion.Euler(
                -kickbackRotX,
                0f,
                Random.Range(
                    -2f,
                    2f
                )
            );
    }

    private void NotifyCrosshairShot(
        bool firedAsAds)
    {
        if (!sendCrosshairFeedback ||
            CrosshairController.Instance == null)
        {
            return;
        }

        CrosshairController.Instance
            .NotifyShotFired(
                firedAsAds
            );
    }

    private void NotifyCrosshairHit(
        int pelletCount,
        int maximumPellets,
        bool killed,
        bool firedAsAds)
    {
        if (!sendCrosshairFeedback ||
            CrosshairController.Instance == null)
        {
            return;
        }

        CrosshairController.Instance
            .NotifyHit(
                pelletCount,
                maximumPellets,
                killed,
                firedAsAds
            );
    }

    // =========================================================
    // 独头弹
    // =========================================================

    private void FireSlug(
        float charge01
    )
    {
        lastSlugDamageDealt = 0f;

        float resolvedCharge01 =
            Mathf.Clamp01(charge01);

        float damageInterpolation =
            Mathf.Pow(
                resolvedCharge01,
                Mathf.Max(
                    0.1f,
                    adsChargeDamageExponent
                )
            );

        float damageForThisShot =
            Mathf.Lerp(
                slugDamage,
                chargedSlugDamage,
                damageInterpolation
            );

        bool hitEnemy = false;
        bool killedEnemy = false;

        EnemyTarget resolvedTarget = null;
        Vector3 resolvedHitPoint = Vector3.zero;
        Vector3 resolvedHitNormal = Vector3.up;

        Vector3 rayOrigin =
            _mainCamera.ViewportToWorldPoint(
                new Vector3(
                    0.5f,
                    0.5f,
                    0f
                )
            );

        Vector3 shootDirection =
            _mainCamera.transform.forward;

        Ray ray =
            new Ray(
                rayOrigin,
                shootDirection
            );

        Vector3 endPoint;

        if (TryGetFirstWeaponHit(
                ray,
                slugRange,
                out RaycastHit hit
            ))
        {
            endPoint = hit.point;

            EnemyTarget enemyTarget =
                hit.collider.GetComponentInParent<EnemyTarget>();

            if (enemyTarget != null)
            {
                resolvedTarget = enemyTarget;
                resolvedHitPoint = hit.point;
                resolvedHitNormal = hit.normal;

                bool wasDeadBeforeHit =
                    enemyTarget.IsDead;

                EnemyHitFXReceiver hitFXReceiver =
                    enemyTarget.GetComponent<EnemyHitFXReceiver>();

                GameObject resolvedLegacyHitEffect =
                    hitFXReceiver != null &&
                    hitFXReceiver.SuppressLegacyHitEffect
                        ? null
                        : enemyHitEffect;

                lastSlugDamageDealt =
                    enemyTarget.TakeDamage(
                        damageForThisShot,
                        hit.point,
                        hit.normal,
                        resolvedLegacyHitEffect
                    );

                hitEnemy =
                    lastSlugDamageDealt > 0f;

                killedEnemy =
                    hitEnemy &&
                    !wasDeadBeforeHit &&
                    enemyTarget.IsDead;

                if (logDamageResults)
                {
                    Debug.Log(
                        $"[独头弹] 命中 {enemyTarget.targetCodeName}，" +
                        $"实际伤害：{lastSlugDamageDealt:F0}，" +
                        $"剩余生命：" +
                        $"{enemyTarget.CurrentHealth:F0}/" +
                        $"{enemyTarget.MaxHealth:F0}，" +
                        $"击杀：{killedEnemy}"
                    );
                }
            }
            else
            {
                bool isFallbackEnemy =
                    hit.collider.CompareTag(
                        "Enemy"
                    );

                HandleNonEnemyTargetHit(
                    hit,
                    null,
                    null
                );

                if (isFallbackEnemy)
                {
                    hitEnemy = true;
                    killedEnemy = true;
                }
            }
        }
        else
        {
            endPoint =
                ray.GetPoint(
                    slugRange
                );

            if (logDamageResults)
            {
                Debug.Log(
                    "[独头弹] 未命中敌人。"
                );
            }
        }

        Transform visualOrigin =
            centerBarrelPoint != null
                ? centerBarrelPoint
                : leftBarrelPoint;

        if (visualOrigin != null)
        {
            float tracerResponse =
                Mathf.Pow(
                    resolvedCharge01,
                    Mathf.Max(
                        0.1f,
                        adsTracerResponseExponent
                    )
                );

            float resolvedTracerWidth =
                slugTracerWidth *
                Mathf.Lerp(
                    adsTracerMinimumWidthMultiplier,
                    adsTracerMaximumWidthMultiplier,
                    tracerResponse
                );

            float resolvedTracerLength =
                Mathf.Lerp(
                    adsTracerMinimumLength,
                    adsTracerMaximumLength,
                    tracerResponse
                );

            Color resolvedTracerTint =
                Color.Lerp(
                    adsTracerLowChargeColor,
                    adsTracerFullChargeColor,
                    tracerResponse
                );

            Color tracerTailColor =
                new Color(
                    1f,
                    0.04f,
                    0.01f,
                    0.78f
                );

            Color tracerHeadColor =
                Color.Lerp(
                    new Color(
                        1f,
                        0.30f,
                        0.16f,
                        1f
                    ),
                    Color.white,
                    tracerResponse *
                    adsTracerHeadWhiteBlend
                );

            if (resolvedCharge01 >=
                fullChargeShellThreshold)
            {
                float shellProgress =
                    Mathf.InverseLerp(
                        fullChargeShellThreshold,
                        1f,
                        resolvedCharge01
                    );

                Color shellVertexColor =
                    new Color(
                        1f,
                        0.02f,
                        0.005f,
                        fullChargeShellAlpha *
                        Mathf.Lerp(
                            0.55f,
                            1f,
                            Mathf.SmoothStep(
                                0f,
                                1f,
                                shellProgress
                            )
                        )
                    );

                StartCoroutine(
                    SpawnTracerRoutine(
                        visualOrigin.position,
                        endPoint,
                        resolvedTracerWidth *
                        fullChargeShellWidthMultiplier,
                        slugTracerSpeed,
                        shellVertexColor,
                        shellVertexColor,
                        fullChargeShellColor,
                        0,
                        resolvedTracerLength * 1.10f,
                        adsTracerTravelDuration
                    )
                );
            }

            StartCoroutine(
                SpawnTracerRoutine(
                    visualOrigin.position,
                    endPoint,
                    resolvedTracerWidth,
                    slugTracerSpeed,
                    tracerTailColor,
                    tracerHeadColor,
                    resolvedTracerTint,
                    1,
                    resolvedTracerLength,
                    adsTracerTravelDuration
                )
            );
        }

        if (hitEnemy)
        {
            if (resolvedTarget != null)
            {
                EmitEnemyShotResult(
                    new EnemyShotResult(
                        resolvedTarget,
                        resolvedHitPoint,
                        resolvedHitNormal,
                        1,
                        1,
                        lastSlugDamageDealt,
                        true,
                        resolvedCharge01,
                        killedEnemy,
                        killedEnemy
                    )
                );
            }

            NotifyCrosshairHit(
                1,
                1,
                killedEnemy,
                true
            );
        }
    }

    // =========================================================
    // 腰射散弹
    // =========================================================

    private void FireHipfire()
    {
        lastHipfirePelletsHit = 0;
        lastHipfireDamageDealt = 0f;

        int safePelletCount =
            Mathf.Max(
                1,
                hipfirePelletCount
            );

        int damagingPelletsThisShot = 0;
        bool killedAnyEnemy = false;

        float randomPatternRotation =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        HashSet<TargetSwitch> switchesHitThisShot =
            new HashSet<TargetSwitch>();

        HashSet<GameObject> fallbackEnemiesKilled =
            new HashSet<GameObject>();

        /*
         * 每个 EnemyTarget 都有一份汇总记录。
         *
         * 多颗弹丸仍然逐颗伤害，
         * 但普通命中特效在所有弹丸结束后统一生成一次。
         */
        Dictionary<EnemyTarget, HipfireEnemyHitData>
            enemyHitsThisShot =
                new Dictionary<
                    EnemyTarget,
                    HipfireEnemyHitData
                >();

        for (int pelletIndex = 0;
             pelletIndex < safePelletCount;
             pelletIndex++)
        {
            Vector2 pelletOffset =
                CalculatePelletOffset(
                    pelletIndex,
                    safePelletCount,
                    randomPatternRotation
                );

            Vector3 rayOrigin =
                _mainCamera.ViewportToWorldPoint(
                    new Vector3(
                        0.5f,
                        0.5f,
                        0f
                    )
                );

            Vector3 spreadDirection =
                _mainCamera.transform.forward +
                _mainCamera.transform.right *
                pelletOffset.x *
                spreadAngle +
                _mainCamera.transform.up *
                pelletOffset.y *
                spreadAngle;

            spreadDirection.Normalize();

            Ray pelletRay =
                new Ray(
                    rayOrigin,
                    spreadDirection
                );

            Vector3 endPoint;

            if (TryGetFirstWeaponHit(
                    pelletRay,
                    hipfireRange,
                    out RaycastHit hit
                ))
            {
                endPoint = hit.point;

                EnemyTarget enemyTarget =
                    hit.collider.GetComponentInParent<EnemyTarget>();

                if (enemyTarget != null)
                {
                    lastHipfirePelletsHit++;

                    bool wasDeadBeforeHit =
                        enemyTarget.IsDead;

                    float actualDamage =
                        enemyTarget.TakeDamage(
                            pelletDamage,
                            hit.point,
                            hit.normal,
                            null
                        );

                    lastHipfireDamageDealt +=
                        actualDamage;

                    if (actualDamage > 0f)
                    {
                        damagingPelletsThisShot++;

                        if (!enemyHitsThisShot.TryGetValue(
                                enemyTarget,
                                out HipfireEnemyHitData hitData
                            ))
                        {
                            hitData =
                                new HipfireEnemyHitData();

                            enemyHitsThisShot.Add(
                                enemyTarget,
                                hitData
                            );
                        }

                        bool killedByThisPellet =
                            !wasDeadBeforeHit &&
                            enemyTarget.IsDead;

                        if (killedByThisPellet)
                        {
                            killedAnyEnemy = true;
                            hitData.killed = true;
                        }

                        hitData.hitPointSum +=
                            hit.point;

                        hitData.hitNormalSum +=
                            hit.normal;

                        hitData.hitSamples.Add(
                            new HipfireHitSample(
                                hit.point,
                                hit.normal
                            )
                        );

                        hitData.damagingPelletCount++;

                        hitData.totalDamage +=
                            actualDamage;
                    }
                }
                else
                {
                    bool isFallbackEnemy =
                        hit.collider.CompareTag(
                            "Enemy"
                        );

                    GameObject fallbackEnemyObject =
                        isFallbackEnemy
                            ? hit.collider.gameObject
                            : null;

                    bool isFirstFallbackEnemyHit =
                        fallbackEnemyObject != null &&
                        !fallbackEnemiesKilled.Contains(
                            fallbackEnemyObject
                        );

                    HandleNonEnemyTargetHit(
                        hit,
                        switchesHitThisShot,
                        fallbackEnemiesKilled
                    );

                    if (isFirstFallbackEnemyHit)
                    {
                        damagingPelletsThisShot++;
                        killedAnyEnemy = true;
                    }
                }
            }
            else
            {
                endPoint =
                    pelletRay.GetPoint(
                        hipfireRange
                    );
            }

            Transform visualOrigin =
                GetPelletVisualOrigin(
                    pelletIndex
                );

            if (visualOrigin != null)
            {
                StartCoroutine(
                    SpawnTracerRoutine(
                        visualOrigin.position,
                        endPoint,
                        tracerWidth,
                        tracerSpeed
                    )
                );
            }
        }

        SpawnConsolidatedHipfireHitEffects(
            enemyHitsThisShot
        );

        ResolveHipfireEnemyFeedback(
            enemyHitsThisShot,
            safePelletCount
        );

        if (damagingPelletsThisShot > 0)
        {
            NotifyCrosshairHit(
                damagingPelletsThisShot,
                safePelletCount,
                killedAnyEnemy,
                false
            );
        }

        if (logDamageResults)
        {
            Debug.Log(
                $"[腰射] 碰到敌人的弹丸：" +
                $"{lastHipfirePelletsHit}/{safePelletCount}，" +
                $"真正造成伤害的弹丸：" +
                $"{damagingPelletsThisShot}/{safePelletCount}，" +
                $"实际总伤害：" +
                $"{lastHipfireDamageDealt:F0}，" +
                $"击杀：{killedAnyEnemy}"
            );
        }
    }

    private void ResolveHipfireEnemyFeedback(
        Dictionary<
            EnemyTarget,
            HipfireEnemyHitData
        > enemyHits,
        int maximumPellets
    )
    {
        if (enemyHits == null ||
            enemyHits.Count == 0)
        {
            return;
        }

        foreach (
            KeyValuePair<
                EnemyTarget,
                HipfireEnemyHitData
            > pair in enemyHits
        )
        {
            EnemyTarget target =
                pair.Key;

            HipfireEnemyHitData hitData =
                pair.Value;

            if (target == null ||
                hitData == null ||
                hitData.damagingPelletCount <= 0)
            {
                continue;
            }

            ResolveRepresentativeHit(
                hitData,
                out Vector3 representativeHitPoint,
                out Vector3 representativeHitNormal
            );

            /*
             * 普通命中标记由 EnemyHitMarker 直接监听 EnemyTarget.Damaged。
             * 这里仅汇总整发射击结果，供准星、TP 与统计系统使用。
             */

            EmitEnemyShotResult(
                new EnemyShotResult(
                    target,
                    representativeHitPoint,
                    representativeHitNormal,
                    hitData.damagingPelletCount,
                    maximumPellets,
                    hitData.totalDamage,
                    false,
                    0f,
                    hitData.killed,
                    hitData.killed
                )
            );
        }
    }

    private void EmitEnemyShotResult(
        EnemyShotResult result
    )
    {
        if (result.Target != null)
        {
            EnemyHitFXReceiver hitFXReceiver =
                result.Target.GetComponent<EnemyHitFXReceiver>();

            if (hitFXReceiver != null)
            {
                /*
                 * 直接调用而不是只依赖事件。
                 * 致命命中时 EnemyTarget 会先禁用 MonoBehaviour，
                 * 但对象会到帧末才销毁，因此这里仍能生成世界空间冲击。
                 */
                hitFXReceiver.PlayResolvedShot(
                    result
                );
            }
        }

        EnemyShotResolved?.Invoke(result);
        AnyEnemyShotResolved?.Invoke(result);
    }

    private void SpawnConsolidatedHipfireHitEffects(
        Dictionary<
            EnemyTarget,
            HipfireEnemyHitData
        > enemyHits
    )
    {
        if (enemyHitEffect == null ||
            enemyHits == null ||
            enemyHits.Count == 0)
        {
            return;
        }

        foreach (
            KeyValuePair<
                EnemyTarget,
                HipfireEnemyHitData
            > pair in enemyHits
        )
        {
            EnemyTarget target =
                pair.Key;

            HipfireEnemyHitData hitData =
                pair.Value;

            if (target == null ||
                target.IsDead ||
                hitData == null ||
                hitData.damagingPelletCount <= 0)
            {
                continue;
            }

            EnemyHitFXReceiver hitFXReceiver =
                target.GetComponent<EnemyHitFXReceiver>();

            if (hitFXReceiver != null &&
                hitFXReceiver.SuppressLegacyHitEffect)
            {
                continue;
            }

            ResolveRepresentativeHit(
                hitData,
                out Vector3 representativeHitPoint,
                out Vector3 representativeHitNormal
            );

            SpawnImpactEffect(
                enemyHitEffect,
                representativeHitPoint,
                representativeHitNormal
            );
        }
    }

    /// <summary>
    /// 先计算本次腰射命中的平均中心，
    /// 再选择离该中心最近的真实命中样本。
    /// 避免特效落到尖刺之间或模型内部。
    /// </summary>
    private static void ResolveRepresentativeHit(
        HipfireEnemyHitData hitData,
        out Vector3 hitPoint,
        out Vector3 hitNormal
    )
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;

        if (hitData == null ||
            hitData.damagingPelletCount <= 0)
        {
            return;
        }

        Vector3 averagePoint =
            hitData.hitPointSum /
            hitData.damagingPelletCount;

        if (hitData.hitSamples == null ||
            hitData.hitSamples.Count == 0)
        {
            hitPoint = averagePoint;

            Vector3 averageNormal =
                hitData.hitNormalSum;

            hitNormal =
                averageNormal.sqrMagnitude > 0.0001f
                    ? averageNormal.normalized
                    : Vector3.up;

            return;
        }

        int bestIndex = 0;
        float bestDistance =
            float.PositiveInfinity;

        for (int index = 0;
             index < hitData.hitSamples.Count;
             index++)
        {
            float distance =
                (hitData.hitSamples[index].Point -
                 averagePoint).sqrMagnitude;

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = index;
        }

        HipfireHitSample selected =
            hitData.hitSamples[bestIndex];

        hitPoint = selected.Point;
        hitNormal =
            selected.Normal.sqrMagnitude > 0.0001f
                ? selected.Normal.normalized
                : Vector3.up;
    }

    /// <summary>
    /// 第一颗弹丸位于中心。
    /// 其他弹丸按照黄金角分布在圆形区域内。
    /// </summary>
    private Vector2 CalculatePelletOffset(
        int pelletIndex,
        int totalPellets,
        float rotationOffset
    )
    {
        if (pelletIndex <= 0 ||
            totalPellets <= 1)
        {
            return Vector2.zero;
        }

        int outerPelletCount =
            totalPellets - 1;

        int outerIndex =
            pelletIndex - 1;

        float normalizedRadius =
            (outerIndex + 1f) /
            Mathf.Max(
                1f,
                outerPelletCount
            );

        float radius =
            Mathf.Sqrt(
                normalizedRadius
            );

        float angle =
            rotationOffset +
            outerIndex *
            GoldenAngleRadians;

        return new Vector2(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius
        );
    }

    private Transform GetPelletVisualOrigin(
        int pelletIndex
    )
    {
        bool useLeftBarrel =
            pelletIndex % 2 == 0;

        Transform preferredOrigin =
            useLeftBarrel
                ? leftBarrelPoint
                : rightBarrelPoint;

        if (preferredOrigin != null)
        {
            return preferredOrigin;
        }

        if (leftBarrelPoint != null)
        {
            return leftBarrelPoint;
        }

        if (rightBarrelPoint != null)
        {
            return rightBarrelPoint;
        }

        return centerBarrelPoint;
    }

    private bool TryGetFirstWeaponHit(
        Ray ray,
        float range,
        out RaycastHit resolvedHit
    )
    {
        resolvedHit = default;

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            _weaponRaycastBuffer,
            range,
            hitLayers,
            QueryTriggerInteraction.Collide
        );

        if (hitCount <= 0)
        {
            return false;
        }

        bool found = false;
        float closestDistance = float.PositiveInfinity;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit candidate =
                _weaponRaycastBuffer[index];

            Collider candidateCollider =
                candidate.collider;

            if (candidateCollider == null ||
                ShouldWeaponRaycastPassThrough(candidateCollider))
            {
                continue;
            }

            if (candidate.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = candidate.distance;
            resolvedHit = candidate;
            found = true;
        }

        return found;
    }

    private static bool ShouldWeaponRaycastPassThrough(
        Collider hitCollider
    )
    {
        if (hitCollider == null ||
            !hitCollider.isTrigger)
        {
            return false;
        }

        // Shootable trigger colliders must still behave normally.
        if (hitCollider.GetComponentInParent<EnemyTarget>() != null ||
            hitCollider.GetComponentInParent<TargetSwitch>() != null ||
            hitCollider.CompareTag("Enemy"))
        {
            return false;
        }

        // Non-shootable gameplay volumes should never absorb a hitscan shot.
        // This fixes large authoring triggers such as FlyingPatrolVolume,
        // CombatEncounter volumes, kill zones and helper sockets blocking ADS.
        return true;
    }

    // =========================================================
    // 非 EnemyTarget 命中处理
    // =========================================================

    private void HandleNonEnemyTargetHit(
        RaycastHit hit,
        HashSet<TargetSwitch> switchesHitThisShot,
        HashSet<GameObject> fallbackEnemiesKilled
    )
    {
        TargetSwitch targetSwitch =
            hit.collider.GetComponentInParent<TargetSwitch>();

        if (targetSwitch != null)
        {
            if (switchesHitThisShot == null ||
                !switchesHitThisShot.Contains(
                    targetSwitch
                ))
            {
                targetSwitch.OnHit();

                switchesHitThisShot?.Add(
                    targetSwitch
                );
            }

            return;
        }

        if (hit.collider.CompareTag("Enemy"))
        {
            GameObject fallbackEnemy =
                hit.collider.gameObject;

            if (fallbackEnemiesKilled != null &&
                fallbackEnemiesKilled.Contains(
                    fallbackEnemy
                ))
            {
                return;
            }

            SpawnImpactEffect(
                enemyHitEffect,
                hit.point,
                hit.normal
            );

            fallbackEnemiesKilled?.Add(
                fallbackEnemy
            );

            Destroy(
                fallbackEnemy
            );

            return;
        }

        if (hit.collider.CompareTag("Environment") ||
            hit.collider.CompareTag("Untagged"))
        {
            SpawnImpactEffect(
                environmentHitEffect,
                hit.point,
                hit.normal
            );
        }
    }

    private void SpawnImpactEffect(
        GameObject effectPrefab,
        Vector3 position,
        Vector3 normal
    )
    {
        if (effectPrefab == null)
        {
            return;
        }

        Quaternion rotation =
            normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(
                    normal.normalized
                )
                : Quaternion.identity;

        Instantiate(
            effectPrefab,
            position,
            rotation
        );
    }

    // =========================================================
    // 曳光对象池
    // =========================================================

    private LineRenderer GetPooledTracer()
    {
        foreach (LineRenderer tracer
                 in _tracerPool)
        {
            if (tracer == null)
            {
                continue;
            }

            if (!tracer.gameObject.activeInHierarchy)
            {
                tracer.gameObject.SetActive(
                    true
                );

                return tracer;
            }
        }

        GameObject tracerObject =
            new GameObject(
                "BulletTracer"
            );

        tracerObject.transform.SetParent(
            transform
        );

        LineRenderer line =
            tracerObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;

        line.shadowCastingMode =
            ShadowCastingMode.Off;

        line.receiveShadows = false;

        if (tracerMaterial != null)
        {
            line.material =
                tracerMaterial;
        }
        else
        {
            Shader fallbackShader =
                Shader.Find(
                    "Sprites/Default"
                );

            if (fallbackShader != null)
            {
                line.material =
                    new Material(
                        fallbackShader
                    );
            }
        }

        _tracerPool.Add(
            line
        );

        return line;
    }

    private IEnumerator SpawnTracerRoutine(
        Vector3 startPosition,
        Vector3 endPosition,
        float width,
        float speed
    )
    {
        return SpawnTracerRoutineInternal(
            startPosition,
            endPosition,
            width,
            speed,
            false,
            Color.white,
            Color.white,
            Color.white,
            0,
            tracerLength,
            0f
        );
    }

    private IEnumerator SpawnTracerRoutine(
        Vector3 startPosition,
        Vector3 endPosition,
        float width,
        float speed,
        Color tailColor,
        Color headColor,
        Color materialTint,
        int sortingOrder,
        float visualLength,
        float travelDuration
    )
    {
        return SpawnTracerRoutineInternal(
            startPosition,
            endPosition,
            width,
            speed,
            true,
            tailColor,
            headColor,
            materialTint,
            sortingOrder,
            visualLength,
            travelDuration
        );
    }

    private IEnumerator SpawnTracerRoutineInternal(
        Vector3 startPosition,
        Vector3 endPosition,
        float width,
        float speed,
        bool useCustomAppearance,
        Color tailColor,
        Color headColor,
        Color materialTint,
        int sortingOrder,
        float visualLength,
        float travelDurationOverride
    )
    {
        LineRenderer line =
            GetPooledTracer();

        ConfigureTracerAppearance(
            line,
            useCustomAppearance,
            tailColor,
            headColor,
            materialTint,
            sortingOrder
        );

        line.startWidth =
            useCustomAppearance
                ? width * 0.86f
                : 0f;

        line.endWidth = width;
        line.numCapVertices =
            useCustomAppearance
                ? 4
                : 0;

        line.positionCount = 2;

        float totalDistance =
            Vector3.Distance(
                startPosition,
                endPosition
            );

        float safeSpeed =
            Mathf.Max(
                0.01f,
                speed
            );

        float travelDuration =
            travelDurationOverride > 0f
                ? travelDurationOverride
                : totalDistance / safeSpeed;

        travelDuration =
            Mathf.Max(
                0.0001f,
                travelDuration
            );

        float elapsed = 0f;

        while (elapsed < travelDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float travel01 =
                Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(
                        0.0001f,
                        travelDuration
                    )
                );

            float movedDistance =
                totalDistance * travel01;

            Vector3 headPosition =
                Vector3.MoveTowards(
                    startPosition,
                    endPosition,
                    movedDistance
                );

            Vector3 tailPosition =
                Vector3.MoveTowards(
                    startPosition,
                    endPosition,
                    Mathf.Max(
                        0f,
                        movedDistance -
                        Mathf.Max(
                            0.05f,
                            visualLength
                        )
                    )
                );

            line.SetPosition(
                0,
                tailPosition
            );

            line.SetPosition(
                1,
                headPosition
            );

            yield return null;
        }

        line.gameObject.SetActive(
            false
        );
    }

    private void ConfigureTracerAppearance(
        LineRenderer line,
        bool useCustomAppearance,
        Color tailColor,
        Color headColor,
        Color materialTint,
        int sortingOrder
    )
    {
        line.sortingOrder = sortingOrder;

        if (!useCustomAppearance)
        {
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.SetPropertyBlock(null);
            return;
        }

        line.startColor = tailColor;
        line.endColor = headColor;

        if (_tracerPropertyBlock == null)
        {
            _tracerPropertyBlock =
                new MaterialPropertyBlock();
        }

        _tracerPropertyBlock.Clear();

        _tracerPropertyBlock.SetColor(
            TracerBaseColorId,
            materialTint
        );

        _tracerPropertyBlock.SetColor(
            TracerColorId,
            materialTint
        );

        _tracerPropertyBlock.SetColor(
            TracerEmissionColorId,
            materialTint
        );

        line.SetPropertyBlock(
            _tracerPropertyBlock
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxAmmo =
            Mathf.Max(
                1,
                maxAmmo
            );

        hipfirePelletCount =
            Mathf.Max(
                1,
                hipfirePelletCount
            );

        pelletDamage =
            Mathf.Max(
                0f,
                pelletDamage
            );

        slugDamage =
            Mathf.Max(
                0f,
                slugDamage
            );

        adsChargeDuration =
            Mathf.Max(
                0.05f,
                adsChargeDuration
            );

        chargedSlugDamage =
            Mathf.Max(
                0f,
                chargedSlugDamage
            );

        adsChargeDamageExponent =
            Mathf.Max(
                0.1f,
                adsChargeDamageExponent
            );

        adsTracerMinimumWidthMultiplier =
            Mathf.Max(
                0.1f,
                adsTracerMinimumWidthMultiplier
            );

        adsTracerMaximumWidthMultiplier =
            Mathf.Max(
                adsTracerMinimumWidthMultiplier,
                adsTracerMaximumWidthMultiplier
            );

        adsTracerResponseExponent =
            Mathf.Max(
                0.1f,
                adsTracerResponseExponent
            );

        adsTracerTravelDuration =
            Mathf.Max(
                0f,
                adsTracerTravelDuration
            );

        adsTracerMinimumLength =
            Mathf.Max(
                0.05f,
                adsTracerMinimumLength
            );

        adsTracerMaximumLength =
            Mathf.Max(
                adsTracerMinimumLength,
                adsTracerMaximumLength
            );

        fullChargeShellThreshold =
            Mathf.Clamp01(
                fullChargeShellThreshold
            );

        fullChargeShellWidthMultiplier =
            Mathf.Max(
                1f,
                fullChargeShellWidthMultiplier
            );

        fullChargeShellAlpha =
            Mathf.Clamp01(
                fullChargeShellAlpha
            );

        spreadAngle =
            Mathf.Max(
                0f,
                spreadAngle
            );

        hipfireRange =
            Mathf.Max(
                0.1f,
                hipfireRange
            );

        slugRange =
            Mathf.Max(
                0.1f,
                slugRange
            );
    }
#endif
}
