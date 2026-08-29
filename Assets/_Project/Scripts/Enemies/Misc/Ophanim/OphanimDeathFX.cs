#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class OphanimDeathFX : MonoBehaviour
{
    [Header("基础设置")]
    [Tooltip("生成后脱离原层级，避免敌人销毁时特效一起消失。")]
    public bool detachOnSpawn = true;

    [Tooltip(
        "使用 M_OphanimMarker。" +
        "应为 URP/Particles/Unlit、Transparent、Additive。"
    )]
    public Material additiveMaterial;

    [Header("颜色")]
    public Color primaryColor =
        new Color(0.65f, 1f, 0.02f, 1f);

    public Color secondaryColor =
        new Color(0.1f, 0.95f, 1f, 1f);

    public Color flashColor =
        Color.white;

    [Header("核心塌缩")]
    public float collapseDuration = 0.16f;

    public float coreStartScale = 0.65f;
    public float coreCollapsedScale = 0.12f;

    [Tooltip("爆发后中心闪光扩张的最大尺寸。")]
    public float coreFlashEndScale = 2.4f;

    public float coreFlashDuration = 0.24f;

    [Header("四重圆环")]
    [Min(12)]
    public int ringSegments = 56;

    public float ringBaseRadius = 2.1f;

    public float ringStartScale = 1.05f;
    public float ringCollapsedScale = 0.18f;
    public float ringExpandedScale = 2.15f;

    public float ringStartWidth = 0.11f;
    public float ringBurstWidth = 0.18f;
    public float ringEndWidth = 0.012f;

    public float ringExpandDuration = 0.55f;

    [Header("爆发粒子")]
    [Min(1)]
    public int burstParticleCount = 34;

    public float burstParticleSpeed = 7f;
    public float burstParticleLifetime = 0.55f;
    public float burstParticleSize = 0.16f;

    [Header("故障碎片")]
    [Min(0)]
    public int glitchSliceCount = 12;

    public float glitchSpeedMinimum = 2.5f;
    public float glitchSpeedMaximum = 6f;

    public float glitchLifetimeMinimum = 0.24f;
    public float glitchLifetimeMaximum = 0.55f;

    [Header("总生命周期")]
    public float effectLifetime = 1.4f;

    [Header("音效（可选）")]
    public AudioClip deathSound;

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private sealed class RingRuntime
    {
        public Transform objectTransform;
        public LineRenderer lineRenderer;
        public float spinSpeed;
    }

    private sealed class GlitchRuntime
    {
        public Transform objectTransform;
        public Renderer objectRenderer;

        public Vector3 velocity;
        public Vector3 angularVelocity;
        public Vector3 originalScale;

        public Color color;

        public float age;
        public float lifetime;
        public float flickerSpeed;
        public float flickerOffset;
    }

    private readonly List<RingRuntime> _rings =
        new List<RingRuntime>();

    private readonly List<GlitchRuntime> _glitchSlices =
        new List<GlitchRuntime>();

    private Material _runtimeMaterial;
    private MaterialPropertyBlock _propertyBlock;

    private Transform _coreFlashTransform;
    private Renderer _coreFlashRenderer;

    private ParticleSystem _burstParticles;

    private float _age;
    private bool _burstTriggered;

    private void Awake()
    {
        if (detachOnSpawn)
        {
            transform.SetParent(null, true);
        }

        /*
         * EnemyTarget 可能根据命中法线旋转死亡 Prefab。
         * Ophanim 的四环效果需要始终以世界竖直方向显示。
         */
        transform.rotation =
            Quaternion.identity;

        _propertyBlock =
            new MaterialPropertyBlock();

        CreateRuntimeMaterial();
        CreateCoreFlash();
        CreateRings();
        CreateBurstParticleSystem();
        PlayDeathSound();
    }

    private void Update()
    {
        float deltaTime =
            Time.unscaledDeltaTime;

        _age += deltaTime;

        if (!_burstTriggered &&
            _age >= collapseDuration)
        {
            TriggerBurst();
        }

        UpdateCoreFlash();
        UpdateRings(deltaTime);
        UpdateGlitchSlices(deltaTime);

        if (_age >= effectLifetime)
        {
            Destroy(gameObject);
        }
    }

    // =========================================================
    // 材质
    // =========================================================

    private void CreateRuntimeMaterial()
    {
        if (additiveMaterial == null)
        {
            Debug.LogWarning(
                $"[OphanimDeathFX] {gameObject.name} " +
                "没有设置 Additive Material。"
            );

            return;
        }

        _runtimeMaterial =
            new Material(additiveMaterial);

        _runtimeMaterial.name =
            $"{additiveMaterial.name}_OphanimDeath_Runtime";

        if (_runtimeMaterial.HasProperty(
                BaseColorId
            ))
        {
            _runtimeMaterial.SetColor(
                BaseColorId,
                Color.white
            );
        }

        if (_runtimeMaterial.HasProperty(
                ColorId
            ))
        {
            _runtimeMaterial.SetColor(
                ColorId,
                Color.white
            );
        }
    }

    // =========================================================
    // 中心核心
    // =========================================================

    private void CreateCoreFlash()
    {
        GameObject coreObject =
            CreatePrimitiveWithoutCollider(
                PrimitiveType.Sphere,
                "OphanimDeath_CoreFlash"
            );

        coreObject.transform.localPosition =
            Vector3.zero;

        coreObject.transform.localRotation =
            Quaternion.identity;

        coreObject.transform.localScale =
            Vector3.one * coreStartScale;

        _coreFlashTransform =
            coreObject.transform;

        _coreFlashRenderer =
            coreObject.GetComponent<Renderer>();

        if (_coreFlashRenderer != null)
        {
            if (_runtimeMaterial != null)
            {
                _coreFlashRenderer.sharedMaterial =
                    _runtimeMaterial;
            }

            _coreFlashRenderer.shadowCastingMode =
                ShadowCastingMode.Off;

            _coreFlashRenderer.receiveShadows =
                false;
        }
    }

    private void UpdateCoreFlash()
    {
        if (_coreFlashTransform == null ||
            _coreFlashRenderer == null)
        {
            return;
        }

        if (_age < collapseDuration)
        {
            float progress =
                Mathf.Clamp01(
                    _age /
                    Mathf.Max(
                        0.01f,
                        collapseDuration
                    )
                );

            float scale =
                Mathf.Lerp(
                    coreStartScale,
                    coreCollapsedScale,
                    progress * progress
                );

            _coreFlashTransform.localScale =
                Vector3.one * scale;

            Color collapseColor =
                Color.Lerp(
                    primaryColor,
                    secondaryColor,
                    progress
                );

            SetRendererTint(
                _coreFlashRenderer,
                collapseColor,
                Mathf.Lerp(1.5f, 6f, progress),
                1f
            );

            return;
        }

        float burstAge =
            _age - collapseDuration;

        float burstProgress =
            Mathf.Clamp01(
                burstAge /
                Mathf.Max(
                    0.01f,
                    coreFlashDuration
                )
            );

        float easedProgress =
            1f -
            Mathf.Pow(
                1f - burstProgress,
                3f
            );

        float burstScale =
            Mathf.Lerp(
                coreCollapsedScale,
                coreFlashEndScale,
                easedProgress
            );

        _coreFlashTransform.localScale =
            Vector3.one * burstScale;

        float alpha =
            Mathf.Pow(
                1f - burstProgress,
                2f
            );

        Color currentColor =
            Color.Lerp(
                flashColor,
                secondaryColor,
                burstProgress
            );

        SetRendererTint(
            _coreFlashRenderer,
            currentColor,
            8f,
            alpha
        );

        if (burstProgress >= 1f)
        {
            _coreFlashRenderer.enabled =
                false;
        }
    }

    // =========================================================
    // 四个圆环
    // =========================================================

    private void CreateRings()
    {
        CreateRing(
            "DeathRing_01",
            Quaternion.identity,
            230f
        );

        CreateRing(
            "DeathRing_02",
            Quaternion.Euler(90f, 0f, 0f),
            -310f
        );

        CreateRing(
            "DeathRing_03",
            Quaternion.Euler(45f, 25f, 45f),
            390f
        );

        CreateRing(
            "DeathRing_04",
            Quaternion.Euler(-55f, 35f, -20f),
            -470f
        );
    }

    private void CreateRing(
        string objectName,
        Quaternion localRotation,
        float spinSpeed
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
            Vector3.one * ringStartScale;

        LineRenderer ring =
            ringObject.AddComponent<LineRenderer>();

        ring.useWorldSpace = false;
        ring.loop = true;

        ring.positionCount =
            Mathf.Max(
                12,
                ringSegments
            );

        ring.startWidth =
            ringStartWidth;

        ring.endWidth =
            ringStartWidth;

        ring.numCornerVertices = 4;
        ring.numCapVertices = 4;

        ring.alignment =
            LineAlignment.View;

        ring.textureMode =
            LineTextureMode.Stretch;

        ring.shadowCastingMode =
            ShadowCastingMode.Off;

        ring.receiveShadows =
            false;

        if (_runtimeMaterial != null)
        {
            ring.sharedMaterial =
                _runtimeMaterial;
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

        RingRuntime runtime =
            new RingRuntime
            {
                objectTransform =
                    ringObject.transform,

                lineRenderer =
                    ring,

                spinSpeed =
                    spinSpeed
            };

        _rings.Add(runtime);
    }

    private void UpdateRings(
        float deltaTime
    )
    {
        for (int i = 0;
             i < _rings.Count;
             i++)
        {
            RingRuntime ring =
                _rings[i];

            if (ring.objectTransform == null ||
                ring.lineRenderer == null)
            {
                continue;
            }

            ring.objectTransform.Rotate(
                Vector3.up,
                ring.spinSpeed *
                deltaTime,
                Space.Self
            );

            if (_age < collapseDuration)
            {
                float progress =
                    Mathf.Clamp01(
                        _age /
                        Mathf.Max(
                            0.01f,
                            collapseDuration
                        )
                    );

                float scale =
                    Mathf.Lerp(
                        ringStartScale,
                        ringCollapsedScale,
                        progress * progress
                    );

                ring.objectTransform.localScale =
                    Vector3.one * scale;

                float width =
                    Mathf.Lerp(
                        ringStartWidth,
                        ringStartWidth * 0.45f,
                        progress
                    );

                Color ringColor =
                    Color.Lerp(
                        primaryColor,
                        flashColor,
                        progress
                    );

                SetRingAppearance(
                    ring.lineRenderer,
                    ringColor,
                    1f,
                    width
                );

                continue;
            }

            float burstAge =
                _age - collapseDuration;

            float progressAfterBurst =
                Mathf.Clamp01(
                    burstAge /
                    Mathf.Max(
                        0.01f,
                        ringExpandDuration
                    )
                );

            float easedProgress =
                1f -
                Mathf.Pow(
                    1f - progressAfterBurst,
                    3f
                );

            float expandedScale =
                Mathf.Lerp(
                    ringCollapsedScale,
                    ringExpandedScale *
                    (1f + i * 0.08f),
                    easedProgress
                );

            ring.objectTransform.localScale =
                Vector3.one *
                expandedScale;

            float widthAfterBurst =
                Mathf.Lerp(
                    ringBurstWidth,
                    ringEndWidth,
                    progressAfterBurst
                );

            float alpha =
                Mathf.Pow(
                    1f - progressAfterBurst,
                    1.6f
                );

            Color burstColor =
                Color.Lerp(
                    i % 2 == 0
                        ? primaryColor
                        : secondaryColor,

                    flashColor,
                    Mathf.Max(
                        0f,
                        0.25f -
                        progressAfterBurst
                    ) * 4f
                );

            SetRingAppearance(
                ring.lineRenderer,
                burstColor,
                alpha,
                widthAfterBurst
            );

            if (progressAfterBurst >= 1f)
            {
                ring.lineRenderer.enabled =
                    false;
            }
        }
    }

    private void SetRingAppearance(
        LineRenderer ring,
        Color color,
        float alpha,
        float width
    )
    {
        Color finalColor =
            color;

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
    // 爆发粒子
    // =========================================================

    private void CreateBurstParticleSystem()
    {
        GameObject particleObject =
            new GameObject(
                "OphanimDeath_BurstParticles"
            );

        /*
         * 防止动态添加 ParticleSystem 后自动播放，
         * 避免修改 Duration 时产生 Unity 警告。
         */
        particleObject.SetActive(false);

        particleObject.transform.SetParent(
            transform,
            false
        );

        particleObject.transform.localPosition =
            Vector3.zero;

        ParticleSystem particleSystem =
            particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main =
            particleSystem.main;

        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;

        main.startLifetime =
            burstParticleLifetime;

        main.startSpeed = 0f;

        main.startSize =
            burstParticleSize;

        main.simulationSpace =
            ParticleSystemSimulationSpace.World;

        main.maxParticles =
            Mathf.Max(
                64,
                burstParticleCount * 2
            );

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
            ShadowCastingMode.Off;

        particleRenderer.receiveShadows =
            false;

        if (_runtimeMaterial != null)
        {
            particleRenderer.sharedMaterial =
                _runtimeMaterial;
        }

        particleObject.SetActive(true);

        particleSystem.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particleSystem.Clear(true);

        _burstParticles =
            particleSystem;
    }

    private void EmitBurstParticles()
    {
        if (_burstParticles == null)
        {
            return;
        }

        int safeCount =
            Mathf.Max(
                1,
                burstParticleCount
            );

        for (int i = 0;
             i < safeCount;
             i++)
        {
            Vector3 direction =
                Random.onUnitSphere;

            direction.y *= 0.65f;

            if (direction.sqrMagnitude <
                0.001f)
            {
                direction =
                    Vector3.up;
            }

            direction.Normalize();

            Color particleColor =
                Color.Lerp(
                    primaryColor,
                    secondaryColor,
                    Random.value
                );

            ParticleSystem.EmitParams emitParams =
                new ParticleSystem.EmitParams();

            emitParams.position =
                transform.position +
                Random.insideUnitSphere *
                0.25f;

            emitParams.velocity =
                direction *
                Random.Range(
                    burstParticleSpeed * 0.65f,
                    burstParticleSpeed * 1.2f
                );

            emitParams.startLifetime =
                burstParticleLifetime *
                Random.Range(
                    0.75f,
                    1.25f
                );

            emitParams.startSize =
                burstParticleSize *
                Random.Range(
                    0.65f,
                    1.5f
                );

            emitParams.startColor =
                particleColor;

            _burstParticles.Emit(
                emitParams,
                1
            );
        }
    }

    // =========================================================
    // Glitch 条带
    // =========================================================

    private void CreateGlitchSlices()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        int safeCount =
            Mathf.Max(
                0,
                glitchSliceCount
            );

        for (int i = 0;
             i < safeCount;
             i++)
        {
            GameObject sliceObject =
                CreatePrimitiveWithoutCollider(
                    PrimitiveType.Cube,
                    $"OphanimGlitchSlice_{i:00}"
                );

            sliceObject.transform.localPosition =
                Random.insideUnitSphere *
                0.45f;

            sliceObject.transform.localRotation =
                Random.rotation;

            Vector3 originalScale =
                new Vector3(
                    Random.Range(0.45f, 1.4f),
                    Random.Range(0.025f, 0.08f),
                    Random.Range(0.025f, 0.09f)
                );

            sliceObject.transform.localScale =
                originalScale;

            Renderer sliceRenderer =
                sliceObject.GetComponent<Renderer>();

            if (sliceRenderer != null)
            {
                sliceRenderer.sharedMaterial =
                    _runtimeMaterial;

                sliceRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                sliceRenderer.receiveShadows =
                    false;
            }

            Vector3 direction =
                Random.onUnitSphere;

            direction.y *= 0.45f;

            if (direction.sqrMagnitude <
                0.001f)
            {
                direction =
                    Vector3.right;
            }

            direction.Normalize();

            Color sliceColor =
                Color.Lerp(
                    primaryColor,
                    secondaryColor,
                    Random.value
                );

            GlitchRuntime runtime =
                new GlitchRuntime
                {
                    objectTransform =
                        sliceObject.transform,

                    objectRenderer =
                        sliceRenderer,

                    velocity =
                        direction *
                        Random.Range(
                            glitchSpeedMinimum,
                            glitchSpeedMaximum
                        ),

                    angularVelocity =
                        Random.onUnitSphere *
                        Random.Range(
                            180f,
                            560f
                        ),

                    originalScale =
                        originalScale,

                    color =
                        sliceColor,

                    age =
                        0f,

                    lifetime =
                        Random.Range(
                            glitchLifetimeMinimum,
                            glitchLifetimeMaximum
                        ),

                    flickerSpeed =
                        Random.Range(
                            20f,
                            45f
                        ),

                    flickerOffset =
                        Random.Range(
                            0f,
                            10f
                        )
                };

            _glitchSlices.Add(runtime);
        }
    }

    private void UpdateGlitchSlices(
        float deltaTime
    )
    {
        foreach (GlitchRuntime slice
                 in _glitchSlices)
        {
            if (slice.objectTransform == null ||
                slice.objectRenderer == null)
            {
                continue;
            }

            slice.age += deltaTime;

            if (slice.age >= slice.lifetime)
            {
                slice.objectRenderer.enabled =
                    false;

                continue;
            }

            float progress =
                slice.age /
                Mathf.Max(
                    0.01f,
                    slice.lifetime
                );

            slice.objectTransform.position +=
                slice.velocity *
                deltaTime;

            slice.objectTransform.Rotate(
                slice.angularVelocity *
                deltaTime,
                Space.Self
            );

            Vector3 scale =
                slice.originalScale;

            scale.x *=
                Mathf.Lerp(
                    0.7f,
                    2.5f,
                    progress
                );

            float shrink =
                Mathf.Max(
                    0.03f,
                    1f - progress
                );

            scale.y *= shrink;
            scale.z *= shrink;

            slice.objectTransform.localScale =
                scale;

            float flicker =
                Mathf.Sin(
                    (
                        slice.age *
                        slice.flickerSpeed +
                        slice.flickerOffset
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
                slice.objectRenderer,
                slice.color,
                4f,
                alpha
            );

            slice.objectRenderer.enabled =
                alpha > 0.02f;
        }
    }

    // =========================================================
    // 爆发入口
    // =========================================================

    private void TriggerBurst()
    {
        if (_burstTriggered)
        {
            return;
        }

        _burstTriggered = true;

        EmitBurstParticles();
        CreateGlitchSlices();
    }

    // =========================================================
    // Renderer 辅助
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
            color;

        baseColor.a =
            Mathf.Clamp01(alpha);

        Color brightColor =
            color *
            Mathf.Max(
                0f,
                intensity
            );

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
        if (deathSound == null)
        {
            return;
        }

        AudioSource source =
            gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 3f;
        source.maxDistance = 30f;

        source.PlayOneShot(
            deathSound,
            soundVolume
        );
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
        {
            Destroy(
                _runtimeMaterial
            );
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        collapseDuration =
            Mathf.Max(
                0.01f,
                collapseDuration
            );

        coreFlashDuration =
            Mathf.Max(
                0.01f,
                coreFlashDuration
            );

        ringExpandDuration =
            Mathf.Max(
                0.01f,
                ringExpandDuration
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
                collapseDuration +
                ringExpandDuration +
                0.15f
            );
    }
#endif
}
