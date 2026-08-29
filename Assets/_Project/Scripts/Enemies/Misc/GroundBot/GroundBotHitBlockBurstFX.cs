#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ground Bot 普通受击时的白色块体爆裂。
///
/// 完整流程：
/// 1. 命中瞬间出现非常短的白色核心闪光和少量冲击线；
/// 2. 大小不同的白色块体从命中点爆开；
/// 3. 块体先保留明确的冲击方向，随后减速并随机散落飞行；
/// 4. 在生命末段才快速淡出并缩小。
///
/// 块体使用低过曝材质，核心闪光单独使用高亮材质，避免所有方块糊成白团。
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundBotHitBlockBurstFX : MonoBehaviour
{
    private const string RuntimeObjectName =
        "GroundBotHitBlockBurst_Runtime";

    private static Material _sharedSolidBlockMaterial;
    private static Material _sharedImpactGlowMaterial;
    private static Mesh _sharedCubeMesh;

    private float _destroyAt;

    public static void Spawn(
        Vector3 hitPoint,
        Vector3 outwardDirection,
        float strength,
        Color particleColor,
        Vector2Int mediumCountRange,
        Vector2Int smallCountRange,
        Vector2Int largeCountRange,
        float lifetime,
        float overallScale
    )
    {
        Vector3 safeDirection =
            outwardDirection.sqrMagnitude > 0.0001f
                ? outwardDirection.normalized
                : Vector3.forward;

        float safeStrength =
            Mathf.Clamp(strength, 0.35f, 1.5f);

        float strength01 =
            Mathf.InverseLerp(0.35f, 1.5f, safeStrength);

        float safeLifetime =
            Mathf.Clamp(lifetime, 0.62f, 1.20f);

        float safeScale =
            Mathf.Clamp(overallScale, 0.80f, 1.35f);

        GameObject root =
            new GameObject(RuntimeObjectName);

        root.transform.SetPositionAndRotation(
            hitPoint,
            Quaternion.LookRotation(safeDirection)
        );

        GroundBotHitBlockBurstFX controller =
            root.AddComponent<GroundBotHitBlockBurstFX>();

        controller._destroyAt =
            Time.time + safeLifetime + 0.28f;

        // 少量但层级分明：1 大、3~4 中、4~5 小。
        int largeCount = 1;
        int mediumCount =
            Mathf.Clamp(
                ResolveCount(mediumCountRange, strength01),
                3,
                4
            );
        int smallCount =
            Mathf.Clamp(
                ResolveCount(smallCountRange, strength01),
                4,
                5
            );

        Material solidMaterial =
            GetOrCreateSolidBlockMaterial(particleColor);
        Material glowMaterial =
            GetOrCreateImpactGlowMaterial();

        // 命中瞬间：很短的中心闪光，不承担后续可读性。
        ParticleSystem coreFlash =
            CreateCoreFlashSystem(
                root.transform,
                glowMaterial,
                safeScale,
                safeStrength
            );

        // 只保留少量短冲击线，提供“击中”的瞬时张力。
        ParticleSystem impactStreaks =
            CreateImpactStreakSystem(
                root.transform,
                glowMaterial,
                4,
                safeScale,
                safeStrength
            );

        // 视觉锚点：只有 1 个大块，寿命最长，后期有明显漂移。
        ParticleSystem largeBlocks =
            CreateBlockSystem(
                root.transform,
                "LargeFlyingBlock",
                solidMaterial,
                largeCount,
                safeLifetime * 1.22f,
                safeScale,
                new Vector2(0.145f, 0.215f),
                new Vector2(0.120f, 0.185f),
                new Vector2(0.100f, 0.165f),
                new Vector2(2.8f, 4.1f),
                18f,
                0.012f,
                0.18f,
                0.10f,
                0.95f
            );

        // 主体：3~4 个中块，先爆开，再减速和随机散落。
        ParticleSystem mediumBlocks =
            CreateBlockSystem(
                root.transform,
                "MediumFlyingBlocks",
                solidMaterial,
                mediumCount,
                safeLifetime * 1.06f,
                safeScale,
                new Vector2(0.095f, 0.145f),
                new Vector2(0.074f, 0.118f),
                new Vector2(0.062f, 0.105f),
                new Vector2(3.1f, 4.8f),
                28f,
                0.018f,
                0.26f,
                0.08f,
                1.15f
            );

        // 补充层：5~7 个小块，散射角更大，轨迹更随机。
        ParticleSystem smallBlocks =
            CreateBlockSystem(
                root.transform,
                "SmallFlyingBlocks",
                solidMaterial,
                smallCount,
                safeLifetime * 0.92f,
                safeScale,
                new Vector2(0.050f, 0.085f),
                new Vector2(0.040f, 0.072f),
                new Vector2(0.036f, 0.068f),
                new Vector2(3.0f, 5.1f),
                42f,
                0.022f,
                0.34f,
                0.05f,
                1.42f
            );

        coreFlash.Play(true);
        impactStreaks.Play(true);
        largeBlocks.Play(true);
        mediumBlocks.Play(true);
        smallBlocks.Play(true);

        coreFlash.Emit(1);
        impactStreaks.Emit(4);
        largeBlocks.Emit(largeCount);
        mediumBlocks.Emit(mediumCount);
        smallBlocks.Emit(smallCount);
    }

    private void Update()
    {
        if (Time.time >= _destroyAt)
        {
            Destroy(gameObject);
        }
    }

    private static ParticleSystem CreateCoreFlashSystem(
        Transform parent,
        Material material,
        float overallScale,
        float strength
    )
    {
        ParticleSystem system =
            CreateInactiveParticleSystem(parent, "ImpactCoreFlash");

        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.055f, 0.075f);
        main.startSpeed = 0f;
        main.startSize =
            0.105f * overallScale * Mathf.Lerp(0.92f, 1.15f, strength / 1.5f);
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.15f, 1f),
                new Keyframe(1f, 0f)
            )
        );

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color =
            new ParticleSystem.MinMaxGradient(CreateFastFlashGradient());

        ParticleSystemRenderer renderer =
            system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetOrCreateCubeMesh();
        renderer.sharedMaterial = material;
        ConfigureParticleRenderer(renderer);

        system.gameObject.SetActive(true);
        return system;
    }

    private static ParticleSystem CreateImpactStreakSystem(
        Transform parent,
        Material material,
        int particleCount,
        float overallScale,
        float strength
    )
    {
        ParticleSystem system =
            CreateInactiveParticleSystem(parent, "ImpactRadialStreaks");

        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = particleCount;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.09f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            7.2f * overallScale,
            10.4f * overallScale
        );
        main.startSize = new ParticleSystem.MinMaxCurve(
            0.010f * overallScale,
            0.018f * overallScale
        );
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 52f;
        shape.radius = 0.012f;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color =
            new ParticleSystem.MinMaxGradient(CreateFastFlashGradient());

        ParticleSystemRenderer renderer =
            system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = Mathf.Lerp(1.5f, 2.1f, strength / 1.5f);
        renderer.velocityScale = 0.14f;
        renderer.cameraVelocityScale = 0f;
        renderer.sharedMaterial = material;
        ConfigureParticleRenderer(renderer);

        system.gameObject.SetActive(true);
        return system;
    }

    private static ParticleSystem CreateBlockSystem(
        Transform parent,
        string objectName,
        Material material,
        int particleCount,
        float lifetime,
        float overallScale,
        Vector2 sizeX,
        Vector2 sizeY,
        Vector2 sizeZ,
        Vector2 speed,
        float coneAngle,
        float coneRadius,
        float noiseStrength,
        float gravity,
        float rotationSpeed
    )
    {
        ParticleSystem system =
            CreateInactiveParticleSystem(parent, objectName);

        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.06f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(1, particleCount);
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            lifetime * 0.96f,
            lifetime * 1.12f
        );
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            speed.x * overallScale,
            speed.y * overallScale
        );
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(
            sizeX.x * overallScale,
            sizeX.y * overallScale
        );
        main.startSizeY = new ParticleSystem.MinMaxCurve(
            sizeY.x * overallScale,
            sizeY.y * overallScale
        );
        main.startSizeZ = new ParticleSystem.MinMaxCurve(
            sizeZ.x * overallScale,
            sizeZ.y * overallScale
        );
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = Color.white;
        main.gravityModifier = new ParticleSystem.MinMaxCurve(
            gravity * 0.72f,
            gravity * 1.28f
        );

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = coneRadius;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = Mathf.InverseLerp(18f, 45f, coneAngle) * 0.16f;

        // 前段高速爆开，随后逐步减速，让方块能被看清并继续散落飞行。
        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime =
            system.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.speedModifier = new ParticleSystem.MinMaxCurve(
            1f,
            CreateSpeedModifierCurve()
        );

        // 随机轨迹不是从第 1 帧就出现，而是在爆开后逐渐增强。
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        AnimationCurve noiseCurve = CreateNoiseStrengthCurve();
        noise.strengthX = new ParticleSystem.MinMaxCurve(noiseStrength, noiseCurve);
        noise.strengthY = new ParticleSystem.MinMaxCurve(noiseStrength * 0.82f, noiseCurve);
        noise.strengthZ = new ParticleSystem.MinMaxCurve(noiseStrength, noiseCurve);
        noise.frequency = 0.48f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.scrollSpeed = 0.20f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color =
            new ParticleSystem.MinMaxGradient(CreateBlockLifetimeGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.separateAxes = false;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateBlockSizeCurve()
        );

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime =
            system.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.separateAxes = true;
        rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(
            -rotationSpeed,
            rotationSpeed
        );
        rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(
            -rotationSpeed * 0.82f,
            rotationSpeed * 0.82f
        );
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(
            -rotationSpeed,
            rotationSpeed
        );

        ParticleSystemRenderer renderer =
            system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetOrCreateCubeMesh();
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.sharedMaterial = material;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.minParticleSize = 0.0001f;
        renderer.maxParticleSize = 0.8f;
        ConfigureParticleRenderer(renderer);

        system.gameObject.SetActive(true);
        return system;
    }

    private static ParticleSystem CreateInactiveParticleSystem(
        Transform parent,
        string objectName
    )
    {
        GameObject child = new GameObject(objectName);
        child.SetActive(false);
        child.transform.SetParent(parent, false);
        return child.AddComponent<ParticleSystem>();
    }

    private static void ConfigureParticleRenderer(
        ParticleSystemRenderer renderer
    )
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static int ResolveCount(
        Vector2Int countRange,
        float strength01
    )
    {
        int minimum = Mathf.Max(0, countRange.x);
        int maximum = Mathf.Max(minimum, countRange.y);
        return Mathf.RoundToInt(
            Mathf.Lerp(minimum, maximum, Mathf.Clamp01(strength01))
        );
    }

    private static AnimationCurve CreateSpeedModifierCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.32f, 0.60f),
            new Keyframe(0.72f, 0.24f),
            new Keyframe(1f, 0.10f)
        );
    }

    private static AnimationCurve CreateNoiseStrengthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.18f, 0.03f),
            new Keyframe(0.42f, 0.62f),
            new Keyframe(0.78f, 1f),
            new Keyframe(1f, 0.72f)
        );
    }

    private static Gradient CreateBlockLifetimeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.80f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.84f),
                new GradientAlphaKey(0.92f, 0.92f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    private static Gradient CreateFastFlashGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.86f, 0.30f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    private static AnimationCurve CreateBlockSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.72f),
            new Keyframe(0.10f, 1f),
            new Keyframe(0.82f, 1f),
            new Keyframe(0.94f, 0.88f),
            new Keyframe(0.985f, 0.30f),
            new Keyframe(1f, 0f)
        );
    }

    private static Mesh GetOrCreateCubeMesh()
    {
        if (_sharedCubeMesh != null)
        {
            return _sharedCubeMesh;
        }

        GameObject temporaryCube =
            GameObject.CreatePrimitive(PrimitiveType.Cube);
        temporaryCube.hideFlags = HideFlags.HideAndDontSave;

        MeshFilter filter =
            temporaryCube.GetComponent<MeshFilter>();

        if (filter != null && filter.sharedMesh != null)
        {
            _sharedCubeMesh = Object.Instantiate(filter.sharedMesh);
            _sharedCubeMesh.name = "Runtime_GroundBotHitBlockCube";
            _sharedCubeMesh.hideFlags = HideFlags.HideAndDontSave;
        }

        Object.Destroy(temporaryCube);
        return _sharedCubeMesh;
    }

    private static Material GetOrCreateSolidBlockMaterial(
        Color requestedColor
    )
    {
        if (_sharedSolidBlockMaterial == null)
        {
            Shader shader = FindSolidBlockShader();
            if (shader == null)
            {
                Debug.LogWarning(
                    "[GroundBotHitBlockBurstFX] 找不到可用的块体 Shader。"
                );
                return null;
            }

            _sharedSolidBlockMaterial = new Material(shader)
            {
                name = "Runtime_GroundBotSolidWhiteBlocks",
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true
            };

            ConfigureOpaqueBlockMaterial(_sharedSolidBlockMaterial);
        }

        // 方块本体以实体可读性为优先：主体是纯白块面，叠加轻微发光。
        Color solidColor = requestedColor;
        solidColor.r = Mathf.Clamp(solidColor.r, 0.97f, 1.02f);
        solidColor.g = Mathf.Clamp(solidColor.g, 0.97f, 1.02f);
        solidColor.b = Mathf.Clamp(solidColor.b, 0.97f, 1.02f);
        solidColor.a = 1f;

        SetMaterialColor(
            _sharedSolidBlockMaterial,
            solidColor,
            true
        );

        return _sharedSolidBlockMaterial;
    }

    private static Material GetOrCreateImpactGlowMaterial()
    {
        if (_sharedImpactGlowMaterial != null)
        {
            return _sharedImpactGlowMaterial;
        }

        Shader shader = FindParticleShader();
        if (shader == null)
        {
            return null;
        }

        _sharedImpactGlowMaterial = new Material(shader)
        {
            name = "Runtime_GroundBotImpactGlow",
            hideFlags = HideFlags.HideAndDontSave,
            enableInstancing = true
        };

        ConfigureAdditiveMaterial(_sharedImpactGlowMaterial);
        SetMaterialColor(
            _sharedImpactGlowMaterial,
            new Color(2.35f, 2.35f, 2.35f, 1f),
            true
        );

        return _sharedImpactGlowMaterial;
    }

    private static Shader FindSolidBlockShader()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = FindParticleShader();
        }

        return shader;
    }

    private static Shader FindParticleShader()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return shader;
    }

    private static void ConfigureOpaqueBlockMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
        }
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }
        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 1f);
        }
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Back);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.10f);
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Geometry;
    }

    private static void ConfigureAlphaMaterial(Material material)
    {
        ConfigureCommonMaterial(material);

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
    }

    private static void ConfigureAdditiveMaterial(Material material)
    {
        ConfigureCommonMaterial(material);

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 1f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.One);
        }
    }

    private static void ConfigureCommonMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
        }
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetMaterialColor(
        Material material,
        Color color,
        bool enableEmission
    )
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            if (enableEmission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.55f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
