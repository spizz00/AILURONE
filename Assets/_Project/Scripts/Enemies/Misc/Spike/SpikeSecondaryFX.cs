#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
public class SpikeSecondaryFX : MonoBehaviour
{
    [Header("核心引用")]
    [Tooltip("Enemy_Spike_Root 上的 SpikeEnemy 组件。")]
    public SpikeEnemy spikeEnemy;

    [Tooltip(
        "效果生成位置。通常拖入 Enemy_Spike_Root 自己。" +
        "留空时自动使用当前物体。"
    )]
    public Transform fxAnchor;

    [Tooltip(
        "用于圆环和粒子的 Additive 材质。" +
        "可以直接复用 M_Spike_ChargeTrail。"
    )]
    public Material additiveMaterial;

    [Header("蓄力圆环")]
    [Min(12)]
    public int ringSegments = 48;

    [Tooltip("圆环基础半径。Spike 实体半径约 1.7 时，建议 2.1～2.3。")]
    public float ringBaseRadius = 2.2f;

    public float ringWidth = 0.08f;

    [Tooltip("蓄力刚开始时圆环的缩放。")]
    public float ringStartScale = 1.45f;

    [Tooltip("蓄力结束时圆环的缩放。")]
    public float ringEndScale = 0.55f;

    public float ringSpinSpeedA = 140f;
    public float ringSpinSpeedB = -190f;

    public Color windupRingColor =
        new Color(1f, 0f, 1f, 1f);

    [Header("冲锋爆发")]
    [Min(1)]
    public int chargeBurstCount = 26;

    public float chargeBurstSpeed = 5f;
    public float chargeBurstLifetime = 0.28f;
    public float chargeBurstSize = 0.16f;

    public Color chargeBurstColor =
        new Color(1f, 0f, 1f, 1f);

    [Header("撞墙眩晕爆发")]
    [Tooltip("粒子生成位置相对敌人中心向前偏移多少。")]
    public float stunImpactForwardOffset = 1.35f;

    [Min(1)]
    public int stunBurstCount = 20;

    public float stunBurstSpeed = 3.5f;
    public float stunBurstLifetime = 0.4f;
    public float stunBurstSize = 0.2f;

    public Color stunBurstColor =
        new Color(0.85f, 0.95f, 1f, 1f);

    [Header("眩晕光环")]
    public float stunnedHaloScale = 0.9f;
    public float stunnedHaloPulseAmount = 0.12f;
    public float stunnedHaloPulseSpeed = 9f;

    [Header("音效接口（可选）")]
    public AudioSource audioSource;

    public AudioClip windupSound;
    public AudioClip chargeSound;
    public AudioClip stunnedSound;
    public AudioClip fallingSound;

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    private Transform _runtimeFxRoot;

    private LineRenderer _ringA;
    private LineRenderer _ringB;

    private ParticleSystem _burstParticles;

    private SpikeEnemy.SpikeState _lastState;
    private bool _stateInitialized;

    private void Awake()
    {
        if (spikeEnemy == null)
        {
            spikeEnemy = GetComponent<SpikeEnemy>();
        }

        if (fxAnchor == null)
        {
            fxAnchor = transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        CreateRuntimeEffects();
    }

    private void Start()
    {
        if (spikeEnemy == null)
        {
            Debug.LogError(
                $"[SpikeSecondaryFX] {gameObject.name} 没有找到 SpikeEnemy。"
            );

            enabled = false;
            return;
        }

        if (additiveMaterial == null)
        {
            Debug.LogWarning(
                $"[SpikeSecondaryFX] {gameObject.name} 没有设置 Additive Material。" +
                "圆环和粒子可能显示为默认材质。"
            );
        }

        _lastState = spikeEnemy.CurrentState;
        _stateInitialized = true;

        HandleStateEntered(_lastState);
        UpdateRingEffects(_lastState);
    }

    private void Update()
    {
        if (spikeEnemy == null)
        {
            return;
        }

        SpikeEnemy.SpikeState currentState =
            spikeEnemy.CurrentState;

        if (!_stateInitialized ||
            currentState != _lastState)
        {
            _lastState = currentState;
            _stateInitialized = true;

            HandleStateEntered(currentState);
        }

        UpdateRingEffects(currentState);
    }

    // =========================================================
    // 创建运行时效果
    // =========================================================

    private void CreateRuntimeEffects()
    {
        GameObject rootObject =
            new GameObject("SpikeFX_Runtime");

        rootObject.transform.SetParent(
            fxAnchor,
            false
        );

        rootObject.transform.localPosition =
            Vector3.zero;

        rootObject.transform.localRotation =
            Quaternion.identity;

        rootObject.transform.localScale =
            Vector3.one;

        _runtimeFxRoot =
            rootObject.transform;

        _ringA = CreateRing(
            "WindupRing_A",
            Quaternion.identity
        );

        _ringB = CreateRing(
            "WindupRing_B",
            Quaternion.Euler(90f, 0f, 0f)
        );

        _burstParticles =
            CreateBurstParticleSystem(
                "SpikeBurstParticles"
            );

        SetRingsEnabled(false);
    }

    private LineRenderer CreateRing(
        string objectName,
        Quaternion localRotation
    )
    {
        GameObject ringObject =
            new GameObject(objectName);

        ringObject.transform.SetParent(
            _runtimeFxRoot,
            false
        );

        ringObject.transform.localPosition =
            Vector3.zero;

        ringObject.transform.localRotation =
            localRotation;

        ringObject.transform.localScale =
            Vector3.one;

        LineRenderer ring =
            ringObject.AddComponent<LineRenderer>();

        ring.useWorldSpace = false;
        ring.loop = true;

        ring.positionCount =
            Mathf.Max(12, ringSegments);

        ring.startWidth = ringWidth;
        ring.endWidth = ringWidth;

        ring.numCornerVertices = 3;
        ring.numCapVertices = 3;

        ring.alignment =
            LineAlignment.View;

        ring.textureMode =
            LineTextureMode.Stretch;

        ring.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        ring.receiveShadows = false;

        if (additiveMaterial != null)
        {
            ring.sharedMaterial =
                additiveMaterial;
        }

        for (int i = 0;
             i < ring.positionCount;
             i++)
        {
            float angle =
                (float)i /
                ring.positionCount *
                Mathf.PI *
                2f;

            Vector3 point =
                new Vector3(
                    Mathf.Cos(angle) *
                    ringBaseRadius,
                    0f,
                    Mathf.Sin(angle) *
                    ringBaseRadius
                );

            ring.SetPosition(i, point);
        }

        return ring;
    }

    private ParticleSystem CreateBurstParticleSystem(
        string objectName
    )
    {
        GameObject particleObject =
            new GameObject(objectName);

        /*
         * 关键修复：
         * 先禁用 GameObject，再添加并配置 ParticleSystem。
         * 这样 Unity 不会在配置 duration 之前自动开始播放。
         */
        particleObject.SetActive(false);

        particleObject.transform.SetParent(
            _runtimeFxRoot,
            false
        );

        particleObject.transform.localPosition =
            Vector3.zero;

        particleObject.transform.localRotation =
            Quaternion.identity;

        particleObject.transform.localScale =
            Vector3.one;

        ParticleSystem particleSystem =
            particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main =
            particleSystem.main;

        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;

        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 0.15f;

        main.simulationSpace =
            ParticleSystemSimulationSpace.World;

        main.maxParticles = 128;

        ParticleSystem.EmissionModule emission =
            particleSystem.emission;

        emission.enabled = false;

        ParticleSystem.ShapeModule shape =
            particleSystem.shape;

        shape.enabled = false;

        ParticleSystemRenderer particleRenderer =
            particleObject.GetComponent<ParticleSystemRenderer>();

        particleRenderer.renderMode =
            ParticleSystemRenderMode.Billboard;

        particleRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        particleRenderer.receiveShadows = false;

        if (additiveMaterial != null)
        {
            particleRenderer.sharedMaterial =
                additiveMaterial;
        }

        /*
         * 所有参数配置完毕后再启用。
         */
        particleObject.SetActive(true);

        particleSystem.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particleSystem.Clear(true);

        return particleSystem;
    }

    // =========================================================
    // 状态切换
    // =========================================================

    private void HandleStateEntered(
        SpikeEnemy.SpikeState newState
    )
    {
        switch (newState)
        {
            case SpikeEnemy.SpikeState.Idle:
            case SpikeEnemy.SpikeState.Tracking:
            case SpikeEnemy.SpikeState.Recovering:
            {
                SetRingsEnabled(false);
                break;
            }

            case SpikeEnemy.SpikeState.Windup:
            {
                SetRingsEnabled(true);
                PlaySound(windupSound);
                break;
            }

            case SpikeEnemy.SpikeState.Charging:
            {
                SetRingsEnabled(false);

                EmitBurst(
                    transform.position,
                    chargeBurstColor,
                    chargeBurstCount,
                    chargeBurstSpeed,
                    chargeBurstLifetime,
                    chargeBurstSize,
                    0.45f
                );

                PlaySound(chargeSound);
                break;
            }

            case SpikeEnemy.SpikeState.Stunned:
            {
                SetRingsEnabled(true);

                Vector3 impactPosition =
                    transform.position +
                    transform.forward *
                    stunImpactForwardOffset;

                EmitBurst(
                    impactPosition,
                    stunBurstColor,
                    stunBurstCount,
                    stunBurstSpeed,
                    stunBurstLifetime,
                    stunBurstSize,
                    0.8f
                );

                PlaySound(stunnedSound);
                break;
            }

            case SpikeEnemy.SpikeState.Falling:
            {
                SetRingsEnabled(false);
                PlaySound(fallingSound);
                break;
            }
        }
    }

    // =========================================================
    // 圆环动画
    // =========================================================

    private void UpdateRingEffects(
        SpikeEnemy.SpikeState state
    )
    {
        if (_ringA == null ||
            _ringB == null)
        {
            return;
        }

        if (state ==
            SpikeEnemy.SpikeState.Windup)
        {
            SetRingsEnabled(true);

            float progress =
                spikeEnemy.WindupProgress;

            float scale =
                Mathf.Lerp(
                    ringStartScale,
                    ringEndScale,
                    progress
                );

            float pulse =
                1f +
                Mathf.Sin(Time.time * 24f) *
                0.05f *
                progress;

            Vector3 finalScale =
                Vector3.one *
                scale *
                pulse;

            _ringA.transform.localScale =
                finalScale;

            _ringB.transform.localScale =
                finalScale * 0.88f;

            _ringA.transform.Rotate(
                Vector3.up,
                ringSpinSpeedA *
                Time.deltaTime,
                Space.Self
            );

            _ringB.transform.Rotate(
                Vector3.forward,
                ringSpinSpeedB *
                Time.deltaTime,
                Space.Self
            );

            float alpha =
                Mathf.Lerp(
                    0.2f,
                    1f,
                    progress
                );

            float width =
                Mathf.Lerp(
                    ringWidth * 0.65f,
                    ringWidth * 1.35f,
                    progress
                );

            SetRingAppearance(
                _ringA,
                windupRingColor,
                alpha,
                width
            );

            SetRingAppearance(
                _ringB,
                windupRingColor,
                alpha * 0.75f,
                width * 0.85f
            );

            return;
        }

        if (state ==
            SpikeEnemy.SpikeState.Stunned)
        {
            _ringA.enabled = true;
            _ringB.enabled = false;

            float pulse =
                1f +
                Mathf.Sin(
                    Time.time *
                    stunnedHaloPulseSpeed
                ) *
                stunnedHaloPulseAmount;

            _ringA.transform.localScale =
                Vector3.one *
                stunnedHaloScale *
                pulse;

            _ringA.transform.Rotate(
                Vector3.up,
                80f *
                Time.deltaTime,
                Space.Self
            );

            float alpha =
                0.55f +
                Mathf.PingPong(
                    Time.time * 5f,
                    0.35f
                );

            SetRingAppearance(
                _ringA,
                stunBurstColor,
                alpha,
                ringWidth * 0.85f
            );

            return;
        }

        SetRingsEnabled(false);
    }

    private void SetRingAppearance(
        LineRenderer ring,
        Color color,
        float alpha,
        float width
    )
    {
        if (ring == null)
        {
            return;
        }

        Color finalColor = color;
        finalColor.a =
            Mathf.Clamp01(alpha);

        ring.startColor = finalColor;
        ring.endColor = finalColor;

        ring.startWidth = width;
        ring.endWidth = width;
    }

    private void SetRingsEnabled(
        bool enabledState
    )
    {
        if (_ringA != null)
        {
            _ringA.enabled =
                enabledState;
        }

        if (_ringB != null)
        {
            _ringB.enabled =
                enabledState;
        }
    }

    // =========================================================
    // 粒子爆发
    // =========================================================

    private void EmitBurst(
        Vector3 worldPosition,
        Color color,
        int count,
        float speed,
        float lifetime,
        float size,
        float verticalSpread
    )
    {
        if (_burstParticles == null)
        {
            return;
        }

        int safeCount =
            Mathf.Max(1, count);

        for (int i = 0;
             i < safeCount;
             i++)
        {
            Vector3 direction =
                Random.onUnitSphere;

            direction.y *=
                Mathf.Clamp01(
                    verticalSpread
                );

            if (direction.sqrMagnitude <
                0.001f)
            {
                direction = Vector3.up;
            }

            direction.Normalize();

            ParticleSystem.EmitParams emitParams =
                new ParticleSystem.EmitParams();

            emitParams.position =
                worldPosition;

            emitParams.velocity =
                direction *
                Random.Range(
                    speed * 0.65f,
                    speed * 1.15f
                );

            emitParams.startLifetime =
                lifetime *
                Random.Range(
                    0.8f,
                    1.2f
                );

            emitParams.startSize =
                size *
                Random.Range(
                    0.7f,
                    1.35f
                );

            emitParams.startColor =
                color;

            _burstParticles.Emit(
                emitParams,
                1
            );
        }
    }

    // =========================================================
    // 音效
    // =========================================================

    private void PlaySound(
        AudioClip clip
    )
    {
        if (audioSource == null ||
            clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            clip,
            soundVolume
        );
    }

    private void OnDisable()
    {
        SetRingsEnabled(false);

        if (_burstParticles != null)
        {
            _burstParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            _burstParticles.Clear(true);
        }
    }
}
