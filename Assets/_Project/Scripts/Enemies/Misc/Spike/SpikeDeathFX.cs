#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SpikeDeathFX : MonoBehaviour
{
    public enum DeathStyle
    {
        Weapon,
        Environment
    }

    [Header("死亡类型")]
    public DeathStyle deathStyle = DeathStyle.Weapon;

    [Tooltip(
        "启用后，特效生成时自动脱离父物体，" +
        "避免敌人销毁时特效一起消失。"
    )]
    public bool detachFromParentOnSpawn = true;

    [Header("Presentation Hierarchy")]
    [Min(0f)]
    [Tooltip("Lets the ADS tracer reach the target before the weapon-death burst begins.")]
    public float weaponPresentationDelay = 0.055f;

    [Min(0f)]
    public float environmentPresentationDelay = 0f;

    [Range(0.1f, 1f)]
    public float weaponVisualScaleMultiplier = 0.58f;

    [Range(0.1f, 1f)]
    public float environmentVisualScaleMultiplier = 1f;

    [Range(0.1f, 1f)]
    public float weaponDensityMultiplier = 0.62f;

    [Range(0.1f, 1f)]
    public float environmentDensityMultiplier = 1f;

    [Header("材质")]
    [Tooltip(
        "实体碎片使用的材质。" +
        "可以直接使用 M_SpikeEnemy。"
    )]
    public Material shardMaterial;

    [Tooltip(
        "圆环、闪光和 Glitch 条使用的透明 Additive 材质。" +
        "可以直接使用 M_Spike_ChargeTrail。"
    )]
    public Material additiveMaterial;

    [Header("枪械击杀颜色")]
    public Color weaponPrimaryColor =
        new Color(0.05f, 0.65f, 1f, 1f);

    public Color weaponSecondaryColor =
        new Color(1f, 0f, 1f, 1f);

    [Header("环境击杀颜色")]
    public Color environmentPrimaryColor =
        new Color(1f, 0f, 0.55f, 1f);

    public Color environmentSecondaryColor =
        new Color(0.3f, 0.02f, 0.65f, 1f);

    [Header("碎片")]
    [Min(1)]
    public int weaponShardCount = 22;

    [Min(1)]
    public int environmentShardCount = 16;

    public float weaponShardSpeed = 7f;
    public float environmentShardSpeed = 5f;

    [Tooltip("枪械击杀时，碎片沿特效物体 Forward 方向的偏移量。")]
    public float weaponDirectionalBias = 0.4f;

    public float weaponUpwardBias = 0.25f;
    public float environmentUpwardBias = 0.8f;

    public float weaponGravityMultiplier = 0.65f;
    public float environmentGravityMultiplier = 1f;

    public float shardLifetimeMinimum = 0.75f;
    public float shardLifetimeMaximum = 1.35f;

    public float shardSizeMinimum = 0.12f;
    public float shardSizeMaximum = 0.32f;

    [Min(0f)]
    public float shardEmissionIntensity = 5f;

    [Header("冲击圆环")]
    [Min(12)]
    public int ringSegments = 48;

    public float ringLifetime = 0.48f;

    public float ringStartScale = 0.25f;
    public float ringEndScale = 2.6f;

    public float ringStartWidth = 0.18f;
    public float ringEndWidth = 0.025f;

    public float ringBaseRadius = 1.7f;

    [Header("中心闪光")]
    public float flashLifetime = 0.18f;

    public float flashStartScale = 0.25f;
    public float flashEndScale = 2.5f;

    [Min(0f)]
    public float flashIntensity = 4f;

    [Header("Glitch 爆散")]
    [Min(0)]
    public int weaponGlitchSliceCount = 9;

    [Min(0)]
    public int environmentGlitchSliceCount = 6;

    public float glitchLifetimeMinimum = 0.22f;
    public float glitchLifetimeMaximum = 0.48f;

    public float glitchMoveSpeedMinimum = 2f;
    public float glitchMoveSpeedMaximum = 5f;

    [Min(0f)]
    public float glitchIntensity = 3f;

    [Header("总生命周期")]
    [Tooltip(
        "到达这个时间后，整个死亡特效物体自动销毁。" +
        "应当大于最长碎片生命周期。"
    )]
    public float effectLifetime = 1.6f;

    [Header("死亡音效（可选）")]
    public AudioSource audioSource;

    public AudioClip weaponDeathSound;
    public AudioClip environmentDeathSound;

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private sealed class ShardRuntime
    {
        public Transform objectTransform;
        public Renderer objectRenderer;

        public Vector3 velocity;
        public Vector3 angularVelocity;

        public Vector3 originalScale;

        public float age;
        public float lifetime;
    }

    private sealed class GlitchRuntime
    {
        public Transform objectTransform;
        public Renderer objectRenderer;

        public Vector3 velocity;
        public Vector3 originalScale;

        public Color color;

        public float age;
        public float lifetime;
        public float flickerFrequency;
        public float flickerOffset;
    }

    private readonly List<ShardRuntime> _shards =
        new List<ShardRuntime>();

    private readonly List<GlitchRuntime> _glitchSlices =
        new List<GlitchRuntime>();

    private MaterialPropertyBlock _propertyBlock;

    private Material _runtimeAdditiveMaterial;

    private LineRenderer _ringHorizontal;
    private LineRenderer _ringVertical;

    private Transform _flashTransform;
    private Renderer _flashRenderer;

    private Color _primaryColor;
    private Color _secondaryColor;

    private float _gravityMultiplier;
    private float _age;
    private float _presentationDelayRemaining;
    private float _densityMultiplier = 1f;
    private bool _presentationStarted;

    private void Awake()
    {
        if (detachFromParentOnSpawn)
        {
            transform.SetParent(null, true);
        }

        _propertyBlock =
            new MaterialPropertyBlock();

        ResolveStyleSettings();
        CreateRuntimeAdditiveMaterial();

        ResolvePresentationSettings();

        if (_presentationDelayRemaining <= 0f)
        {
            StartPresentation();
        }
    }

    private void StartPresentation()
    {
        if (_presentationStarted)
        {
            return;
        }

        _presentationStarted = true;

        CreateFlash();
        CreateRings();
        CreateShards();
        CreateGlitchSlices();

        PlayDeathSound();
    }

    private void Update()
    {
        float deltaTime =
            Time.deltaTime;

        if (!_presentationStarted)
        {
            _presentationDelayRemaining -= deltaTime;

            if (_presentationDelayRemaining > 0f)
            {
                return;
            }

            StartPresentation();
        }

        _age += deltaTime;

        UpdateFlash();
        UpdateRings();
        UpdateShards(deltaTime);
        UpdateGlitchSlices(deltaTime);

        if (_age >= effectLifetime)
        {
            Destroy(gameObject);
        }
    }

    // =========================================================
    // 类型设置
    // =========================================================

    private void ResolveStyleSettings()
    {
        if (deathStyle == DeathStyle.Weapon)
        {
            _primaryColor =
                weaponPrimaryColor;

            _secondaryColor =
                weaponSecondaryColor;

            _gravityMultiplier =
                weaponGravityMultiplier;
        }
        else
        {
            _primaryColor =
                environmentPrimaryColor;

            _secondaryColor =
                environmentSecondaryColor;

            _gravityMultiplier =
                environmentGravityMultiplier;
        }
    }

    private void ResolvePresentationSettings()
    {
        bool weaponDeath =
            deathStyle == DeathStyle.Weapon;

        _presentationDelayRemaining =
            Mathf.Max(
                0f,
                weaponDeath
                    ? weaponPresentationDelay
                    : environmentPresentationDelay
            );

        float visualScaleMultiplier =
            Mathf.Clamp(
                weaponDeath
                    ? weaponVisualScaleMultiplier
                    : environmentVisualScaleMultiplier,
                0.1f,
                1f
            );

        _densityMultiplier =
            Mathf.Clamp(
                weaponDeath
                    ? weaponDensityMultiplier
                    : environmentDensityMultiplier,
                0.1f,
                1f
            );

        transform.localScale *=
            visualScaleMultiplier;
    }

    // =========================================================
    // 运行时材质
    // =========================================================

    private void CreateRuntimeAdditiveMaterial()
    {
        if (additiveMaterial == null)
        {
            Debug.LogWarning(
                $"[SpikeDeathFX] {gameObject.name} " +
                "没有设置 Additive Material。"
            );

            return;
        }

        /*
         * 创建运行时副本，避免直接修改
         * M_Spike_ChargeTrail 这个共享材质。
         */
        _runtimeAdditiveMaterial =
            new Material(additiveMaterial);

        _runtimeAdditiveMaterial.name =
            $"{additiveMaterial.name}_RuntimeDeathFX";

        /*
         * 原来的拖尾材质是洋红色。
         * 这里把运行时副本变成白色，
         * 后续由代码为每个效果单独染色。
         */
        if (_runtimeAdditiveMaterial.HasProperty(
                BaseColorId
            ))
        {
            _runtimeAdditiveMaterial.SetColor(
                BaseColorId,
                Color.white
            );
        }

        if (_runtimeAdditiveMaterial.HasProperty(
                ColorId
            ))
        {
            _runtimeAdditiveMaterial.SetColor(
                ColorId,
                Color.white
            );
        }
    }

    // =========================================================
    // 中心闪光
    // =========================================================

    private void CreateFlash()
    {
        if (_runtimeAdditiveMaterial == null)
        {
            return;
        }

        GameObject flashObject =
            CreatePrimitiveWithoutCollider(
                PrimitiveType.Sphere,
                "DeathFlash"
            );

        flashObject.transform.localPosition =
            Vector3.zero;

        flashObject.transform.localRotation =
            Quaternion.identity;

        flashObject.transform.localScale =
            Vector3.one *
            flashStartScale;

        _flashTransform =
            flashObject.transform;

        _flashRenderer =
            flashObject.GetComponent<Renderer>();

        if (_flashRenderer != null)
        {
            _flashRenderer.sharedMaterial =
                _runtimeAdditiveMaterial;

            _flashRenderer.shadowCastingMode =
                ShadowCastingMode.Off;

            _flashRenderer.receiveShadows =
                false;
        }
    }

    private void UpdateFlash()
    {
        if (_flashTransform == null ||
            _flashRenderer == null)
        {
            return;
        }

        float progress =
            Mathf.Clamp01(
                _age /
                Mathf.Max(0.01f, flashLifetime)
            );

        float easedProgress =
            1f -
            Mathf.Pow(
                1f - progress,
                3f
            );

        float scale =
            Mathf.Lerp(
                flashStartScale,
                flashEndScale,
                easedProgress
            );

        _flashTransform.localScale =
            Vector3.one * scale;

        float alpha =
            Mathf.Pow(
                1f - progress,
                2f
            );

        Color flashColor =
            Color.Lerp(
                Color.white,
                _primaryColor,
                progress
            );

        SetRendererTint(
            _flashRenderer,
            flashColor,
            flashIntensity,
            alpha
        );

        if (progress >= 1f)
        {
            _flashRenderer.enabled =
                false;
        }
    }

    // =========================================================
    // 冲击圆环
    // =========================================================

    private void CreateRings()
    {
        if (_runtimeAdditiveMaterial == null)
        {
            return;
        }

        _ringHorizontal =
            CreateRing(
                "DeathRing_Horizontal",
                Quaternion.identity
            );

        _ringVertical =
            CreateRing(
                "DeathRing_Vertical",
                Quaternion.Euler(
                    90f,
                    0f,
                    0f
                )
            );
    }

    private LineRenderer CreateRing(
        string objectName,
        Quaternion localRotation
    )
    {
        GameObject ringObject =
            new GameObject(objectName);

        ringObject.transform.SetParent(
            transform,
            false
        );

        ringObject.transform.localPosition =
            Vector3.zero;

        ringObject.transform.localRotation =
            localRotation;

        ringObject.transform.localScale =
            Vector3.one *
            ringStartScale;

        LineRenderer lineRenderer =
            ringObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace =
            false;

        lineRenderer.loop =
            true;

        lineRenderer.positionCount =
            Mathf.Max(12, ringSegments);

        lineRenderer.startWidth =
            ringStartWidth;

        lineRenderer.endWidth =
            ringStartWidth;

        lineRenderer.numCornerVertices =
            4;

        lineRenderer.numCapVertices =
            4;

        lineRenderer.alignment =
            LineAlignment.View;

        lineRenderer.textureMode =
            LineTextureMode.Stretch;

        lineRenderer.shadowCastingMode =
            ShadowCastingMode.Off;

        lineRenderer.receiveShadows =
            false;

        lineRenderer.sharedMaterial =
            _runtimeAdditiveMaterial;

        for (int i = 0;
             i < lineRenderer.positionCount;
             i++)
        {
            float angle =
                (float)i /
                lineRenderer.positionCount *
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

            lineRenderer.SetPosition(
                i,
                point
            );
        }

        return lineRenderer;
    }

    private void UpdateRings()
    {
        if (_ringHorizontal == null ||
            _ringVertical == null)
        {
            return;
        }

        float progress =
            Mathf.Clamp01(
                _age /
                Mathf.Max(0.01f, ringLifetime)
            );

        float easedProgress =
            1f -
            Mathf.Pow(
                1f - progress,
                3f
            );

        float styleScale =
            deathStyle == DeathStyle.Weapon
                ? 1f
                : 0.82f;

        float scale =
            Mathf.Lerp(
                ringStartScale,
                ringEndScale * styleScale,
                easedProgress
            );

        _ringHorizontal.transform.localScale =
            Vector3.one * scale;

        _ringVertical.transform.localScale =
            Vector3.one *
            scale *
            0.85f;

        _ringHorizontal.transform.Rotate(
            Vector3.up,
            220f * Time.deltaTime,
            Space.Self
        );

        _ringVertical.transform.Rotate(
            Vector3.forward,
            -280f * Time.deltaTime,
            Space.Self
        );

        float width =
            Mathf.Lerp(
                ringStartWidth,
                ringEndWidth,
                progress
            );

        float alpha =
            Mathf.Pow(
                1f - progress,
                1.5f
            );

        SetRingAppearance(
            _ringHorizontal,
            _primaryColor,
            alpha,
            width
        );

        SetRingAppearance(
            _ringVertical,
            _secondaryColor,
            alpha * 0.8f,
            width * 0.8f
        );

        if (progress >= 1f)
        {
            _ringHorizontal.enabled =
                false;

            _ringVertical.enabled =
                false;
        }
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

        Color finalColor =
            color * 2f;

        finalColor.a =
            Mathf.Clamp01(alpha);

        ring.startColor =
            finalColor;

        ring.endColor =
            finalColor;

        ring.startWidth =
            width;

        ring.endWidth =
            width;
    }

    // =========================================================
    // 实体碎片
    // =========================================================

    private void CreateShards()
    {
        if (shardMaterial == null)
        {
            Debug.LogWarning(
                $"[SpikeDeathFX] {gameObject.name} " +
                "没有设置 Shard Material。"
            );

            return;
        }

        int shardCount =
            deathStyle == DeathStyle.Weapon
                ? weaponShardCount
                : environmentShardCount;

        shardCount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    shardCount *
                    _densityMultiplier
                )
            );

        float shardSpeed =
            deathStyle == DeathStyle.Weapon
                ? weaponShardSpeed
                : environmentShardSpeed;

        float upwardBias =
            deathStyle == DeathStyle.Weapon
                ? weaponUpwardBias
                : environmentUpwardBias;

        for (int i = 0;
             i < shardCount;
             i++)
        {
            GameObject shardObject =
                CreatePrimitiveWithoutCollider(
                    PrimitiveType.Cube,
                    $"DeathShard_{i:00}"
                );

            shardObject.transform.localPosition =
                Random.insideUnitSphere *
                0.3f;

            shardObject.transform.localRotation =
                Random.rotation;

            float randomSize =
                Random.Range(
                    shardSizeMinimum,
                    shardSizeMaximum
                );

            Vector3 shardScale =
                new Vector3(
                    randomSize *
                    Random.Range(0.55f, 1.4f),

                    randomSize *
                    Random.Range(0.22f, 0.65f),

                    randomSize *
                    Random.Range(0.35f, 1f)
                );

            shardObject.transform.localScale =
                shardScale;

            Renderer shardRenderer =
                shardObject.GetComponent<Renderer>();

            if (shardRenderer != null)
            {
                shardRenderer.sharedMaterial =
                    shardMaterial;

                shardRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                shardRenderer.receiveShadows =
                    false;
            }

            Vector3 direction =
                Random.onUnitSphere;

            if (deathStyle == DeathStyle.Weapon)
            {
                direction =
                    (
                        direction +
                        transform.forward *
                        weaponDirectionalBias +
                        Vector3.up *
                        upwardBias
                    ).normalized;
            }
            else
            {
                direction.y =
                    Mathf.Abs(direction.y) +
                    upwardBias;

                direction.Normalize();
            }

            float speed =
                shardSpeed *
                Random.Range(
                    0.65f,
                    1.25f
                );

            Color shardColor =
                Color.Lerp(
                    _primaryColor,
                    _secondaryColor,
                    Random.value
                );

            if (shardRenderer != null)
            {
                SetRendererTint(
                    shardRenderer,
                    shardColor,
                    shardEmissionIntensity,
                    1f
                );
            }

            ShardRuntime shard =
                new ShardRuntime
                {
                    objectTransform =
                        shardObject.transform,

                    objectRenderer =
                        shardRenderer,

                    velocity =
                        direction * speed,

                    angularVelocity =
                        Random.onUnitSphere *
                        Random.Range(
                            240f,
                            720f
                        ),

                    originalScale =
                        shardScale,

                    age =
                        0f,

                    lifetime =
                        Random.Range(
                            shardLifetimeMinimum,
                            shardLifetimeMaximum
                        )
                };

            _shards.Add(shard);
        }
    }

    private void UpdateShards(
        float deltaTime
    )
    {
        Vector3 gravity =
            Physics.gravity *
            _gravityMultiplier;

        foreach (ShardRuntime shard
                 in _shards)
        {
            if (shard.objectTransform == null)
            {
                continue;
            }

            shard.age +=
                deltaTime;

            if (shard.age >= shard.lifetime)
            {
                if (shard.objectRenderer != null)
                {
                    shard.objectRenderer.enabled =
                        false;
                }

                continue;
            }

            shard.velocity +=
                gravity * deltaTime;

            shard.objectTransform.position +=
                shard.velocity *
                deltaTime;

            shard.objectTransform.Rotate(
                shard.angularVelocity *
                deltaTime,
                Space.Self
            );

            float progress =
                shard.age /
                Mathf.Max(
                    0.01f,
                    shard.lifetime
                );

            float shrinkProgress =
                Mathf.InverseLerp(
                    0.58f,
                    1f,
                    progress
                );

            float scale =
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    shrinkProgress
                );

            shard.objectTransform.localScale =
                shard.originalScale *
                scale;
        }
    }

    // =========================================================
    // Glitch 条带
    // =========================================================

    private void CreateGlitchSlices()
    {
        if (_runtimeAdditiveMaterial == null)
        {
            return;
        }

        int sliceCount =
            deathStyle == DeathStyle.Weapon
                ? weaponGlitchSliceCount
                : environmentGlitchSliceCount;

        sliceCount =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    sliceCount *
                    _densityMultiplier
                )
            );

        for (int i = 0;
             i < sliceCount;
             i++)
        {
            GameObject sliceObject =
                CreatePrimitiveWithoutCollider(
                    PrimitiveType.Cube,
                    $"GlitchSlice_{i:00}"
                );

            sliceObject.transform.localPosition =
                Random.insideUnitSphere *
                0.65f;

            sliceObject.transform.localRotation =
                Quaternion.Euler(
                    Random.Range(-12f, 12f),
                    Random.Range(0f, 360f),
                    Random.Range(-12f, 12f)
                );

            Vector3 sliceScale =
                new Vector3(
                    Random.Range(0.7f, 2.1f),
                    Random.Range(0.025f, 0.09f),
                    Random.Range(0.025f, 0.1f)
                );

            sliceObject.transform.localScale =
                sliceScale;

            Renderer sliceRenderer =
                sliceObject.GetComponent<Renderer>();

            if (sliceRenderer != null)
            {
                sliceRenderer.sharedMaterial =
                    _runtimeAdditiveMaterial;

                sliceRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                sliceRenderer.receiveShadows =
                    false;
            }

            Vector3 moveDirection =
                new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-0.25f, 0.65f),
                    Random.Range(-1f, 1f)
                ).normalized;

            float moveSpeed =
                Random.Range(
                    glitchMoveSpeedMinimum,
                    glitchMoveSpeedMaximum
                );

            Color sliceColor =
                Color.Lerp(
                    _primaryColor,
                    _secondaryColor,
                    Random.value
                );

            GlitchRuntime glitchSlice =
                new GlitchRuntime
                {
                    objectTransform =
                        sliceObject.transform,

                    objectRenderer =
                        sliceRenderer,

                    velocity =
                        moveDirection *
                        moveSpeed,

                    originalScale =
                        sliceScale,

                    color =
                        sliceColor,

                    age =
                        0f,

                    lifetime =
                        Random.Range(
                            glitchLifetimeMinimum,
                            glitchLifetimeMaximum
                        ),

                    flickerFrequency =
                        Random.Range(
                            18f,
                            42f
                        ),

                    flickerOffset =
                        Random.Range(
                            0f,
                            10f
                        )
                };

            _glitchSlices.Add(
                glitchSlice
            );
        }
    }

    private void UpdateGlitchSlices(
        float deltaTime
    )
    {
        foreach (GlitchRuntime glitchSlice
                 in _glitchSlices)
        {
            if (glitchSlice.objectTransform == null ||
                glitchSlice.objectRenderer == null)
            {
                continue;
            }

            glitchSlice.age +=
                deltaTime;

            if (glitchSlice.age >=
                glitchSlice.lifetime)
            {
                glitchSlice.objectRenderer.enabled =
                    false;

                continue;
            }

            float progress =
                glitchSlice.age /
                Mathf.Max(
                    0.01f,
                    glitchSlice.lifetime
                );

            glitchSlice.objectTransform.localPosition +=
                glitchSlice.velocity *
                deltaTime;

            float horizontalStretch =
                Mathf.Lerp(
                    0.6f,
                    2.2f,
                    progress
                );

            float verticalShrink =
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

            Vector3 scale =
                glitchSlice.originalScale;

            scale.x *=
                horizontalStretch;

            scale.y *=
                Mathf.Max(
                    0.05f,
                    verticalShrink
                );

            scale.z *=
                Mathf.Max(
                    0.05f,
                    verticalShrink
                );

            glitchSlice.objectTransform.localScale =
                scale;

            float flicker =
                Mathf.Sin(
                    (
                        glitchSlice.age *
                        glitchSlice.flickerFrequency +
                        glitchSlice.flickerOffset
                    ) *
                    Mathf.PI *
                    2f
                ) > 0f
                    ? 1f
                    : 0.12f;

            float alpha =
                (1f - progress) *
                flicker;

            SetRendererTint(
                glitchSlice.objectRenderer,
                glitchSlice.color,
                glitchIntensity,
                alpha
            );

            glitchSlice.objectRenderer.enabled =
                alpha > 0.02f;
        }
    }

    // =========================================================
    // 公共辅助
    // =========================================================

    private GameObject CreatePrimitiveWithoutCollider(
        PrimitiveType primitiveType,
        string objectName
    )
    {
        GameObject createdObject =
            GameObject.CreatePrimitive(
                primitiveType
            );

        createdObject.name =
            objectName;

        createdObject.transform.SetParent(
            transform,
            false
        );

        Collider objectCollider =
            createdObject.GetComponent<Collider>();

        if (objectCollider != null)
        {
            objectCollider.enabled =
                false;

            Destroy(objectCollider);
        }

        return createdObject;
    }

    private void SetRendererTint(
        Renderer targetRenderer,
        Color color,
        float intensity,
        float alpha
    )
    {
        if (targetRenderer == null)
        {
            return;
        }

        _propertyBlock.Clear();

        targetRenderer.GetPropertyBlock(
            _propertyBlock
        );

        Color baseColor =
            new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(alpha)
            );

        Color brightColor =
            color *
            Mathf.Max(0f, intensity);

        brightColor.a =
            Mathf.Clamp01(alpha);

        _propertyBlock.SetColor(
            BaseColorId,
            baseColor
        );

        _propertyBlock.SetColor(
            ColorId,
            brightColor
        );

        _propertyBlock.SetColor(
            EmissionColorId,
            brightColor
        );

        targetRenderer.SetPropertyBlock(
            _propertyBlock
        );
    }

    // =========================================================
    // 音效
    // =========================================================

    private void PlayDeathSound()
    {
        AudioClip selectedClip =
            deathStyle == DeathStyle.Weapon
                ? weaponDeathSound
                : environmentDeathSound;

        if (selectedClip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource =
                gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake =
                false;

            audioSource.spatialBlend =
                1f;

            audioSource.minDistance =
                3f;

            audioSource.maxDistance =
                30f;
        }

        audioSource.PlayOneShot(
            selectedClip,
            soundVolume
        );
    }

    private void OnDestroy()
    {
        if (_runtimeAdditiveMaterial != null)
        {
            Destroy(
                _runtimeAdditiveMaterial
            );
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        shardLifetimeMinimum =
            Mathf.Max(
                0.05f,
                shardLifetimeMinimum
            );

        shardLifetimeMaximum =
            Mathf.Max(
                shardLifetimeMinimum,
                shardLifetimeMaximum
            );

        glitchLifetimeMinimum =
            Mathf.Max(
                0.05f,
                glitchLifetimeMinimum
            );

        glitchLifetimeMaximum =
            Mathf.Max(
                glitchLifetimeMinimum,
                glitchLifetimeMaximum
            );

        effectLifetime =
            Mathf.Max(
                effectLifetime,
                shardLifetimeMaximum + 0.1f
            );
    }
#endif
}
