#pragma warning disable 0618
#pragma warning disable 0414
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

/// <summary>
/// Ground Bot 专属死亡流程。
///
/// 流程：
/// 1. 致命命中后立即复制纯视觉模型，原敌人仍可正常结算并销毁；
/// 2. 视觉代理沿命中方向失衡、倾斜并轻微下沉；
/// 3. 机体与核心经历越来越快的红色过载脉冲；
/// 4. 峰值时隐藏完整机体，从核心位置爆出灰白机械块与少量红色核心块；
/// 5. 块体旋转、下坠、与 Environment 发生有限反弹，最后快速缩小并销毁。
///
/// 该效果不参与伤害、碰撞、AI 或奖励结算。
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundBotDeathSequenceFX : MonoBehaviour
{
    private const string RuntimeObjectName =
        "GroundBotDeathSequence_Runtime";

    private const float ExplosionTime = 0.62f;
    private const float SequenceLifetime = 2.05f;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private static readonly int SurfaceId =
        Shader.PropertyToID("_Surface");

    private static readonly int SrcBlendId =
        Shader.PropertyToID("_SrcBlend");

    private static readonly int DstBlendId =
        Shader.PropertyToID("_DstBlend");

    private static readonly int ZWriteId =
        Shader.PropertyToID("_ZWrite");

    private static readonly int SmoothnessId =
        Shader.PropertyToID("_Smoothness");

    private static readonly int MetallicId =
        Shader.PropertyToID("_Metallic");

    private sealed class MaterialState
    {
        public bool HasBaseColor;
        public bool HasColor;
        public bool HasEmission;
        public Color BaseColor;
        public Color EmissionColor;
        public MaterialPropertyBlock Block;
    }

    private sealed class RendererState
    {
        public Renderer Renderer;
        public MaterialState[] Materials;
    }

    private static Mesh _sharedCubeMesh;
    private static Material _mechanicalFragmentMaterial;
    private static Material _coreFragmentMaterial;
    private static Material _coreGlowMaterial;

    private readonly List<RendererState> _rendererStates =
        new List<RendererState>();

    private Transform _visualProxy;
    private Renderer[] _visualRenderers;
    private Transform _coreTransform;
    private Renderer _coreRenderer;
    private Light _overloadLight;

    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private Vector3 _impactDirectionWorld;
    private Vector3 _tiltAxisLocal;
    private Vector3 _effectCenterWorld;
    private Vector3 _effectCenterLocal;
    private Vector3 _coreBaseScale;
    private LayerMask _environmentMask;

    private float _elapsed;
    private bool _exploded;

    /// <summary>
    /// 在 Ground Bot 本体销毁前创建一个完全独立的死亡视觉代理。
    /// 返回 false 时 EnemyTarget 会退回旧死亡 Prefab。
    /// </summary>
    public static bool TrySpawn(
        GroundBotEnemy source,
        Vector3 impactPoint,
        Vector3 impactNormal
    )
    {
        if (source == null ||
            source.visualRoot == null)
        {
            return false;
        }

        Renderer[] sourceRenderers =
            source.visualRoot.GetComponentsInChildren<Renderer>(true);

        if (sourceRenderers == null ||
            sourceRenderers.Length == 0)
        {
            return false;
        }

        GameObject root =
            new GameObject(RuntimeObjectName);

        root.transform.SetPositionAndRotation(
            source.visualRoot.position,
            source.visualRoot.rotation
        );

        GameObject visualClone =
            Instantiate(
                source.visualRoot.gameObject,
                root.transform,
                false
            );

        visualClone.name =
            "GroundBotDeathVisualProxy";

        visualClone.transform.localPosition =
            Vector3.zero;

        visualClone.transform.localRotation =
            Quaternion.identity;

        visualClone.transform.localScale =
            source.visualRoot.lossyScale;

        StripGameplayComponents(visualClone);

        GroundBotDeathSequenceFX sequence =
            root.AddComponent<GroundBotDeathSequenceFX>();

        sequence.Initialize(
            visualClone.transform,
            impactPoint,
            impactNormal,
            source.transform.forward
        );

        return true;
    }

    private static void StripGameplayComponents(
        GameObject visualClone
    )
    {
        if (visualClone == null)
        {
            return;
        }

        MonoBehaviour[] behaviours =
            visualClone.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            behaviour.enabled = false;
            Destroy(behaviour);
        }

        Collider[] colliders =
            visualClone.GetComponentsInChildren<Collider>(true);

        foreach (Collider enemyCollider in colliders)
        {
            if (enemyCollider != null)
            {
                Destroy(enemyCollider);
            }
        }

        Rigidbody[] rigidbodies =
            visualClone.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody body in rigidbodies)
        {
            if (body != null)
            {
                Destroy(body);
            }
        }

        ParticleSystem[] particleSystems =
            visualClone.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem system in particleSystems)
        {
            if (system == null)
            {
                continue;
            }

            system.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            Destroy(system.gameObject);
        }
    }

    private void Initialize(
        Transform visualProxy,
        Vector3 impactPoint,
        Vector3 impactNormal,
        Vector3 fallbackForward
    )
    {
        _visualProxy = visualProxy;
        _basePosition = transform.position;
        _baseRotation = transform.rotation;

        Vector3 safeImpactDirection =
            impactNormal.sqrMagnitude > 0.0001f
                ? -impactNormal.normalized
                : fallbackForward.normalized;

        if (safeImpactDirection.sqrMagnitude < 0.0001f)
        {
            safeImpactDirection = transform.forward;
        }

        safeImpactDirection.y *= 0.18f;
        safeImpactDirection.Normalize();
        _impactDirectionWorld = safeImpactDirection;

        Vector3 tiltAxisWorld =
            Vector3.Cross(
                Vector3.up,
                _impactDirectionWorld
            );

        if (tiltAxisWorld.sqrMagnitude < 0.0001f)
        {
            tiltAxisWorld = transform.right;
        }

        _tiltAxisLocal =
            Quaternion.Inverse(_baseRotation) *
            tiltAxisWorld.normalized;

        int environmentLayer =
            LayerMask.NameToLayer("Environment");

        _environmentMask =
            environmentLayer >= 0
                ? 1 << environmentLayer
                : 1 << 6;

        CacheVisualRenderers();
        ResolveEffectCenter(impactPoint);
        CreateCoreVisual();
        CreateOverloadLight();

        ApplyOverloadVisual(0f, 0f);
    }

    private void CacheVisualRenderers()
    {
        _rendererStates.Clear();

        if (_visualProxy == null)
        {
            _visualRenderers = Array.Empty<Renderer>();
            return;
        }

        _visualRenderers =
            _visualProxy.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in _visualRenderers)
        {
            if (targetRenderer == null ||
                targetRenderer is ParticleSystemRenderer ||
                targetRenderer is LineRenderer ||
                targetRenderer is TrailRenderer)
            {
                continue;
            }

            Material[] materials =
                targetRenderer.sharedMaterials;

            RendererState rendererState =
                new RendererState
                {
                    Renderer = targetRenderer,
                    Materials =
                        new MaterialState[materials.Length]
                };

            for (int index = 0;
                 index < materials.Length;
                 index++)
            {
                Material material = materials[index];

                MaterialState materialState =
                    new MaterialState
                    {
                        Block =
                            new MaterialPropertyBlock(),
                        BaseColor = Color.white,
                        EmissionColor = Color.black
                    };

                if (material != null)
                {
                    materialState.HasBaseColor =
                        material.HasProperty(BaseColorId);

                    materialState.HasColor =
                        material.HasProperty(ColorId);

                    materialState.HasEmission =
                        material.HasProperty(EmissionColorId);

                    if (materialState.HasBaseColor)
                    {
                        materialState.BaseColor =
                            material.GetColor(BaseColorId);
                    }
                    else if (materialState.HasColor)
                    {
                        materialState.BaseColor =
                            material.GetColor(ColorId);
                    }

                    if (materialState.HasEmission)
                    {
                        materialState.EmissionColor =
                            material.GetColor(EmissionColorId);
                    }
                }

                rendererState.Materials[index] =
                    materialState;
            }

            _rendererStates.Add(rendererState);
        }
    }

    private void ResolveEffectCenter(
        Vector3 fallbackPoint
    )
    {
        bool hasBounds = false;
        Bounds combinedBounds =
            new Bounds(transform.position, Vector3.zero);

        foreach (Renderer targetRenderer in _visualRenderers)
        {
            if (targetRenderer == null ||
                !targetRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(
                    targetRenderer.bounds
                );
            }
        }

        _effectCenterWorld =
            hasBounds
                ? combinedBounds.center
                : fallbackPoint;

        _effectCenterLocal =
            transform.InverseTransformPoint(
                _effectCenterWorld
            );
    }

    private void CreateCoreVisual()
    {
        GameObject core =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        core.name = "GroundBotOverloadCore";
        core.transform.SetParent(transform, true);
        core.transform.position = _effectCenterWorld;
        core.transform.rotation = transform.rotation;
        core.transform.localScale =
            new Vector3(0.08f, 0.08f, 0.08f);

        Collider coreCollider =
            core.GetComponent<Collider>();

        if (coreCollider != null)
        {
            Destroy(coreCollider);
        }

        _coreTransform = core.transform;
        _coreBaseScale =
            _coreTransform.localScale;

        _coreRenderer =
            core.GetComponent<Renderer>();

        if (_coreRenderer != null)
        {
            _coreRenderer.sharedMaterial =
                GetOrCreateCoreGlowMaterial();

            _coreRenderer.shadowCastingMode =
                ShadowCastingMode.Off;

            _coreRenderer.receiveShadows = false;
        }
    }

    private void CreateOverloadLight()
    {
        GameObject lightObject =
            new GameObject("GroundBotOverloadLight");

        lightObject.transform.SetParent(transform, true);
        lightObject.transform.position =
            _effectCenterWorld;

        _overloadLight =
            lightObject.AddComponent<Light>();

        _overloadLight.type = LightType.Point;
        _overloadLight.color =
            new Color(1f, 0.035f, 0.015f, 1f);

        _overloadLight.range = 2.4f;
        _overloadLight.intensity = 0f;
        _overloadLight.shadows = LightShadows.None;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (!_exploded)
        {
            UpdatePreExplosion();

            if (_elapsed >= ExplosionTime)
            {
                TriggerExplosion();
            }
        }
        else
        {
            UpdateExplosionFlash();
        }

        if (_elapsed >= SequenceLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void UpdatePreExplosion()
    {
        float impactT =
            SmoothStep01(
                Mathf.InverseLerp(
                    0f,
                    0.13f,
                    _elapsed
                )
            );

        float overloadT =
            SmoothStep01(
                Mathf.InverseLerp(
                    0.08f,
                    ExplosionTime,
                    _elapsed
                )
            );

        float pulse =
            EvaluateOverloadPulse(_elapsed);

        Vector3 recoilOffset =
            _impactDirectionWorld *
            (0.045f * impactT);

        Vector3 sinkOffset =
            Vector3.down *
            (0.075f * overloadT);

        transform.position =
            _basePosition +
            recoilOffset +
            sinkOffset;

        float tiltAngle =
            Mathf.Lerp(0f, 18f, overloadT) +
            3.5f * impactT;

        transform.rotation =
            _baseRotation *
            Quaternion.AngleAxis(
                tiltAngle,
                _tiltAxisLocal
            );

        float overloadStrength =
            Mathf.Clamp01(
                0.22f * overloadT +
                pulse
            );

        ApplyOverloadVisual(
            overloadT,
            overloadStrength
        );

        UpdateCoreBeforeExplosion(
            overloadT,
            pulse
        );

        if (_overloadLight != null)
        {
            _overloadLight.transform.position =
                transform.TransformPoint(
                    _effectCenterLocal
                );

            _overloadLight.intensity =
                Mathf.Lerp(0.25f, 3.2f, overloadT) *
                Mathf.Lerp(0.55f, 1.35f, pulse);

            _overloadLight.range =
                Mathf.Lerp(1.2f, 2.8f, overloadT);
        }
    }

    private void UpdateCoreBeforeExplosion(
        float overloadT,
        float pulse
    )
    {
        if (_coreTransform == null)
        {
            return;
        }

        _coreTransform.position =
            transform.TransformPoint(
                _effectCenterLocal
            );

        float scaleMultiplier =
            Mathf.Lerp(0.78f, 1.55f, overloadT) *
            Mathf.Lerp(0.94f, 1.22f, pulse);

        _coreTransform.localScale =
            _coreBaseScale *
            scaleMultiplier;

        Color coreColor =
            Color.Lerp(
                new Color(0.75f, 0.015f, 0.008f, 1f),
                new Color(4.2f, 0.12f, 0.04f, 1f),
                Mathf.Clamp01(
                    overloadT * 0.72f + pulse
                )
            );

        ApplyRendererColor(
            _coreRenderer,
            coreColor,
            coreColor
        );
    }

    private void ApplyOverloadVisual(
        float overloadT,
        float overloadStrength
    )
    {
        Color overloadBase =
            new Color(1.05f, 0.055f, 0.025f, 1f);

        Color overloadEmission =
            new Color(
                4.3f,
                0.08f,
                0.025f,
                1f
            ) *
            Mathf.Lerp(
                0.35f,
                1.35f,
                overloadStrength
            );

        float baseBlend =
            Mathf.Clamp01(
                overloadT * 0.42f +
                overloadStrength * 0.28f
            );

        foreach (RendererState rendererState
                 in _rendererStates)
        {
            if (rendererState == null ||
                rendererState.Renderer == null)
            {
                continue;
            }

            for (int index = 0;
                 index < rendererState.Materials.Length;
                 index++)
            {
                MaterialState materialState =
                    rendererState.Materials[index];

                if (materialState == null)
                {
                    continue;
                }

                Renderer targetRenderer =
                    rendererState.Renderer;

                targetRenderer.GetPropertyBlock(
                    materialState.Block,
                    index
                );

                Color resolvedBase =
                    Color.Lerp(
                        materialState.BaseColor,
                        overloadBase,
                        baseBlend
                    );

                Color resolvedEmission =
                    materialState.EmissionColor +
                    overloadEmission;

                if (materialState.HasBaseColor)
                {
                    materialState.Block.SetColor(
                        BaseColorId,
                        resolvedBase
                    );
                }

                if (materialState.HasColor)
                {
                    materialState.Block.SetColor(
                        ColorId,
                        resolvedBase
                    );
                }

                if (materialState.HasEmission)
                {
                    materialState.Block.SetColor(
                        EmissionColorId,
                        resolvedEmission
                    );
                }

                targetRenderer.SetPropertyBlock(
                    materialState.Block,
                    index
                );
            }
        }
    }

    private void TriggerExplosion()
    {
        if (_exploded)
        {
            return;
        }

        _exploded = true;

        foreach (Renderer targetRenderer
                 in _visualRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = false;
            }
        }

        Vector3 explosionOrigin =
            _coreTransform != null
                ? _coreTransform.position
                : _effectCenterWorld;

        SpawnMechanicalFragments(
            explosionOrigin
        );

        SpawnCoreFragments(
            explosionOrigin
        );

        if (_coreTransform != null)
        {
            _coreTransform.position =
                explosionOrigin;

            _coreTransform.localScale =
                _coreBaseScale * 1.9f;
        }

        if (_overloadLight != null)
        {
            _overloadLight.transform.position =
                explosionOrigin;

            _overloadLight.intensity = 7.5f;
            _overloadLight.range = 4.2f;
        }
    }

    private void UpdateExplosionFlash()
    {
        float flashT =
            Mathf.Clamp01(
                (_elapsed - ExplosionTime) /
                0.19f
            );

        if (_coreTransform != null)
        {
            float expand =
                1f - Mathf.Pow(1f - flashT, 3f);

            float fade =
                1f -
                SmoothStep01(
                    Mathf.InverseLerp(
                        0.28f,
                        1f,
                        flashT
                    )
                );

            float scale =
                Mathf.Lerp(1.9f, 6.2f, expand) *
                Mathf.Max(0.02f, fade);

            _coreTransform.localScale =
                _coreBaseScale * scale;

            Color flashColor =
                Color.Lerp(
                    new Color(5f, 1.2f, 0.7f, 1f),
                    new Color(3.8f, 0.03f, 0.01f, 1f),
                    flashT
                );

            flashColor.a = fade;

            ApplyRendererColor(
                _coreRenderer,
                flashColor,
                flashColor
            );

            if (flashT >= 1f &&
                _coreRenderer != null)
            {
                _coreRenderer.enabled = false;
            }
        }

        if (_overloadLight != null)
        {
            float lightFade =
                1f -
                SmoothStep01(
                    Mathf.InverseLerp(
                        0f,
                        1f,
                        flashT
                    )
                );

            _overloadLight.intensity =
                7.5f * lightFade;

            _overloadLight.range =
                Mathf.Lerp(4.2f, 1.4f, flashT);
        }
    }

    private void SpawnMechanicalFragments(
        Vector3 origin
    )
    {
        // 三块较大、四块中等、三块较小：比上一版更有份量，
        // 但仍保持块体清晰，避免变成密集碎屑雨。
        for (int index = 0;
             index < 10;
             index++)
        {
            Vector3 size;

            if (index < 3)
            {
                size = RandomBlockScale(
                    0.24f,
                    0.36f
                );
            }
            else if (index < 7)
            {
                size = RandomBlockScale(
                    0.15f,
                    0.24f
                );
            }
            else
            {
                size = RandomBlockScale(
                    0.085f,
                    0.15f
                );
            }

            Color gray =
                Color.Lerp(
                    new Color(0.32f, 0.34f, 0.37f, 1f),
                    new Color(0.92f, 0.94f, 0.96f, 1f),
                    Random.Range(0.25f, 1f)
                );

            Vector3 direction =
                ResolveFragmentDirection(
                    0.34f,
                    0.22f
                );

            SpawnFragment(
                origin,
                size,
                direction *
                Random.Range(2.8f, 5.8f),
                Random.onUnitSphere *
                Random.Range(180f, 430f),
                Random.Range(1.05f, 1.48f),
                gray,
                Color.black,
                false
            );
        }
    }

    private void SpawnCoreFragments(
        Vector3 origin
    )
    {
        for (int index = 0;
             index < 4;
             index++)
        {
            Vector3 size =
                RandomBlockScale(
                    0.075f,
                    0.14f
                );

            Vector3 direction =
                ResolveFragmentDirection(
                    0.50f,
                    0.28f
                );

            Color redBase =
                new Color(
                    1.25f,
                    0.025f,
                    0.012f,
                    1f
                );

            Color redEmission =
                new Color(
                    4.8f,
                    0.075f,
                    0.025f,
                    1f
                );

            SpawnFragment(
                origin,
                size,
                direction *
                Random.Range(3.5f, 6.5f),
                Random.onUnitSphere *
                Random.Range(270f, 540f),
                Random.Range(0.82f, 1.16f),
                redBase,
                redEmission,
                true
            );
        }
    }

    private Vector3 ResolveFragmentDirection(
        float upwardBias,
        float impactBias
    )
    {
        Vector3 randomDirection =
            Random.onUnitSphere;

        Vector3 direction =
            randomDirection +
            Vector3.up * upwardBias +
            _impactDirectionWorld * impactBias;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.up;
        }

        return direction.normalized;
    }

    private void SpawnFragment(
        Vector3 origin,
        Vector3 scale,
        Vector3 velocity,
        Vector3 angularVelocity,
        float lifetime,
        Color baseColor,
        Color emissionColor,
        bool coreFragment
    )
    {
        GameObject fragment =
            new GameObject(
                coreFragment
                    ? "GroundBotCoreFragment"
                    : "GroundBotMechanicalFragment"
            );

        fragment.transform.SetParent(
            transform,
            true
        );

        fragment.transform.position =
            origin +
            Random.insideUnitSphere * 0.075f;

        fragment.transform.rotation =
            Random.rotation;

        fragment.transform.localScale = scale;

        MeshFilter filter =
            fragment.AddComponent<MeshFilter>();

        filter.sharedMesh =
            GetOrCreateCubeMesh();

        MeshRenderer renderer =
            fragment.AddComponent<MeshRenderer>();

        renderer.sharedMaterial =
            coreFragment
                ? GetOrCreateCoreFragmentMaterial()
                : GetOrCreateMechanicalFragmentMaterial();

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;

        renderer.receiveShadows = false;
        renderer.lightProbeUsage =
            LightProbeUsage.Off;

        renderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;

        MaterialPropertyBlock block =
            new MaterialPropertyBlock();

        block.SetColor(
            BaseColorId,
            baseColor
        );

        block.SetColor(
            ColorId,
            baseColor
        );

        block.SetColor(
            EmissionColorId,
            emissionColor
        );

        renderer.SetPropertyBlock(block);

        GroundBotDeathFragmentFX fragmentFX =
            fragment.AddComponent<GroundBotDeathFragmentFX>();

        fragmentFX.Initialize(
            velocity,
            angularVelocity,
            lifetime,
            _environmentMask
        );
    }

    private static Vector3 RandomBlockScale(
        float minimum,
        float maximum
    )
    {
        float baseSize =
            Random.Range(minimum, maximum);

        return new Vector3(
            baseSize *
            Random.Range(0.72f, 1.45f),
            baseSize *
            Random.Range(0.62f, 1.28f),
            baseSize *
            Random.Range(0.68f, 1.38f)
        );
    }

    private static float EvaluateOverloadPulse(
        float time
    )
    {
        float pulseA =
            GaussianPulse(time, 0.20f, 0.036f) *
            0.42f;

        float pulseB =
            GaussianPulse(time, 0.37f, 0.032f) *
            0.68f;

        float pulseC =
            GaussianPulse(time, 0.50f, 0.026f) *
            0.92f;

        float pulseD =
            GaussianPulse(time, 0.585f, 0.018f) *
            1.12f;

        return Mathf.Clamp01(
            pulseA + pulseB + pulseC + pulseD
        );
    }

    private static float GaussianPulse(
        float value,
        float center,
        float width
    )
    {
        float safeWidth =
            Mathf.Max(0.001f, width);

        float normalized =
            (value - center) /
            safeWidth;

        return Mathf.Exp(
            -normalized * normalized
        );
    }

    private static void ApplyRendererColor(
        Renderer targetRenderer,
        Color baseColor,
        Color emissionColor
    )
    {
        if (targetRenderer == null)
        {
            return;
        }

        MaterialPropertyBlock block =
            new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(block);

        block.SetColor(BaseColorId, baseColor);
        block.SetColor(ColorId, baseColor);
        block.SetColor(
            EmissionColorId,
            emissionColor
        );

        targetRenderer.SetPropertyBlock(block);
    }

    private static Mesh GetOrCreateCubeMesh()
    {
        if (_sharedCubeMesh != null)
        {
            return _sharedCubeMesh;
        }

        GameObject temporaryCube =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        temporaryCube.hideFlags =
            HideFlags.HideAndDontSave;

        MeshFilter filter =
            temporaryCube.GetComponent<MeshFilter>();

        if (filter != null &&
            filter.sharedMesh != null)
        {
            _sharedCubeMesh =
                Instantiate(filter.sharedMesh);

            _sharedCubeMesh.name =
                "Runtime_GroundBotDeathCube";

            _sharedCubeMesh.hideFlags =
                HideFlags.HideAndDontSave;
        }

        Destroy(temporaryCube);
        return _sharedCubeMesh;
    }

    private static Material GetOrCreateMechanicalFragmentMaterial()
    {
        if (_mechanicalFragmentMaterial != null)
        {
            return _mechanicalFragmentMaterial;
        }

        Shader shader = FindLitShader();

        if (shader == null)
        {
            return null;
        }

        _mechanicalFragmentMaterial =
            new Material(shader)
            {
                name =
                    "Runtime_GroundBotMechanicalDeathFragment",
                hideFlags =
                    HideFlags.HideAndDontSave,
                enableInstancing = true
            };

        ConfigureOpaqueLitMaterial(
            _mechanicalFragmentMaterial,
            false
        );

        return _mechanicalFragmentMaterial;
    }

    private static Material GetOrCreateCoreFragmentMaterial()
    {
        if (_coreFragmentMaterial != null)
        {
            return _coreFragmentMaterial;
        }

        Shader shader = FindLitShader();

        if (shader == null)
        {
            return null;
        }

        _coreFragmentMaterial =
            new Material(shader)
            {
                name =
                    "Runtime_GroundBotCoreDeathFragment",
                hideFlags =
                    HideFlags.HideAndDontSave,
                enableInstancing = true
            };

        ConfigureOpaqueLitMaterial(
            _coreFragmentMaterial,
            true
        );

        return _coreFragmentMaterial;
    }

    private static Material GetOrCreateCoreGlowMaterial()
    {
        if (_coreGlowMaterial != null)
        {
            return _coreGlowMaterial;
        }

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Unlit"
            );

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = FindLitShader();
        }

        if (shader == null)
        {
            return null;
        }

        _coreGlowMaterial =
            new Material(shader)
            {
                name =
                    "Runtime_GroundBotDeathCoreGlow",
                hideFlags =
                    HideFlags.HideAndDontSave,
                enableInstancing = true
            };

        if (_coreGlowMaterial.HasProperty(SurfaceId))
        {
            _coreGlowMaterial.SetFloat(
                SurfaceId,
                0f
            );
        }

        if (_coreGlowMaterial.HasProperty(ZWriteId))
        {
            _coreGlowMaterial.SetFloat(
                ZWriteId,
                1f
            );
        }

        _coreGlowMaterial.EnableKeyword("_EMISSION");
        _coreGlowMaterial.renderQueue =
            (int)RenderQueue.Geometry;

        return _coreGlowMaterial;
    }

    private static Shader FindLitShader()
    {
        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static void ConfigureOpaqueLitMaterial(
        Material material,
        bool enableEmission
    )
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(SurfaceId))
        {
            material.SetFloat(SurfaceId, 0f);
        }

        if (material.HasProperty(SrcBlendId))
        {
            material.SetFloat(
                SrcBlendId,
                (float)BlendMode.One
            );
        }

        if (material.HasProperty(DstBlendId))
        {
            material.SetFloat(
                DstBlendId,
                (float)BlendMode.Zero
            );
        }

        if (material.HasProperty(ZWriteId))
        {
            material.SetFloat(ZWriteId, 1f);
        }

        if (material.HasProperty(SmoothnessId))
        {
            material.SetFloat(
                SmoothnessId,
                enableEmission ? 0.22f : 0.16f
            );
        }

        if (material.HasProperty(MetallicId))
        {
            material.SetFloat(
                MetallicId,
                enableEmission ? 0.08f : 0.18f
            );
        }

        if (enableEmission)
        {
            material.EnableKeyword("_EMISSION");
        }
        else
        {
            material.DisableKeyword("_EMISSION");
        }

        material.DisableKeyword(
            "_SURFACE_TYPE_TRANSPARENT"
        );

        material.renderQueue =
            (int)RenderQueue.Geometry;
    }

    private static float SmoothStep01(
        float value
    )
    {
        value = Mathf.Clamp01(value);
        return value * value *
               (3f - 2f * value);
    }
}
