#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Short-lived, presentation-only hit and muzzle feedback for Flying Bot.
/// Combat decisions, damage and movement remain owned by FlyingBotEnemy.
/// </summary>
public sealed class FlyingBotCombatJuiceFX : MonoBehaviour
{
    private LineRenderer _ring;
    private Transform _core;
    private Material _lineMaterial;
    private Material _coreMaterial;
    private Material _particleMaterial;
    private Vector3 _position;
    private Vector3 _normal;
    private Color _color;
    private float _startRadius;
    private float _endRadius;
    private float _startWidth;
    private float _duration;
    private float _age;
    private float _coreStartSize;

    public static void SpawnHit(
        Vector3 position,
        Vector3 normal,
        Color color,
        float strength
    )
    {
        float safeStrength = Mathf.Clamp(strength, 0.65f, 1.35f);
        Spawn(
            "FlyingBot_HitJuiceFX",
            position,
            normal,
            color,
            0.07f,
            0.52f * safeStrength,
            0.055f * safeStrength,
            0.26f,
            0.12f * safeStrength,
            Mathf.RoundToInt(14f * safeStrength),
            2.4f * safeStrength,
            0.28f,
            52f
        );
    }

    public static void SpawnMuzzle(
        Vector3 position,
        Vector3 direction,
        Color color,
        bool strong
    )
    {
        Spawn(
            strong
                ? "FlyingBot_MuzzleFX_Strong"
                : "FlyingBot_MuzzleFX_Probe",
            position,
            direction,
            color,
            0.05f,
            strong ? 0.62f : 0.44f,
            strong ? 0.075f : 0.05f,
            strong ? 0.22f : 0.16f,
            strong ? 0.17f : 0.12f,
            strong ? 14 : 9,
            strong ? 3.3f : 2.5f,
            strong ? 0.26f : 0.20f,
            strong ? 28f : 22f
        );
    }

    public static void SpawnDeath(
        Vector3 position,
        Vector3 normal,
        Color color,
        bool aftershock
    )
    {
        Spawn(
            aftershock
                ? "FlyingBot_DeathFX_Aftershock"
                : "FlyingBot_DeathFX_Primary",
            position,
            normal,
            color,
            aftershock ? 0.12f : 0.16f,
            aftershock ? 1.25f : 1.65f,
            aftershock ? 0.10f : 0.15f,
            aftershock ? 0.60f : 0.72f,
            aftershock ? 0.32f : 0.46f,
            aftershock ? 22 : 34,
            aftershock ? 4.2f : 5.2f,
            aftershock ? 0.52f : 0.68f,
            aftershock ? 72f : 92f
        );
    }

    private static void Spawn(
        string objectName,
        Vector3 position,
        Vector3 normal,
        Color color,
        float startRadius,
        float endRadius,
        float width,
        float duration,
        float coreSize,
        int particleCount,
        float particleSpeed,
        float particleLifetime,
        float particleConeAngle
    )
    {
        GameObject root = new GameObject(objectName);
        FlyingBotCombatJuiceFX fx =
            root.AddComponent<FlyingBotCombatJuiceFX>();
        fx.Initialize(
            position,
            normal,
            color,
            startRadius,
            endRadius,
            width,
            duration,
            coreSize,
            particleCount,
            particleSpeed,
            particleLifetime,
            particleConeAngle
        );
    }

    private void Initialize(
        Vector3 position,
        Vector3 normal,
        Color color,
        float startRadius,
        float endRadius,
        float width,
        float duration,
        float coreSize,
        int particleCount,
        float particleSpeed,
        float particleLifetime,
        float particleConeAngle
    )
    {
        _position = position;
        _normal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : Vector3.forward;
        _color = color;
        _startRadius = startRadius;
        _endRadius = endRadius;
        _startWidth = width;
        _duration = Mathf.Max(0.05f, duration);
        _coreStartSize = coreSize;

        BuildMaterials();
        BuildRing();
        BuildCore();
        BuildParticles(
            particleCount,
            particleSpeed,
            particleLifetime,
            particleConeAngle
        );
        RefreshVisuals(0f);
    }

    private void BuildMaterials()
    {
        Shader spriteShader = Shader.Find("Sprites/Default");
        Shader unlitShader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Unlit/Color");
        }

        if (spriteShader != null)
        {
            _lineMaterial = new Material(spriteShader);
            _particleMaterial = new Material(spriteShader);
            SetMaterialColor(_lineMaterial, Color.white);
            SetMaterialColor(_particleMaterial, Color.white);
        }

        if (unlitShader != null)
        {
            _coreMaterial = new Material(unlitShader);
            SetMaterialColor(_coreMaterial, _color);
        }
    }

    private void BuildRing()
    {
        GameObject ringObject = new GameObject("ImpactRing");
        ringObject.transform.SetParent(transform, false);
        _ring = ringObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace = true;
        _ring.loop = true;
        _ring.positionCount = 34;
        _ring.numCapVertices = 2;
        _ring.numCornerVertices = 2;
        _ring.alignment = LineAlignment.View;
        _ring.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;

        if (_lineMaterial != null)
        {
            _ring.sharedMaterial = _lineMaterial;
        }
    }

    private void BuildCore()
    {
        GameObject coreObject =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreObject.name = "ImpactCore";
        coreObject.transform.SetParent(transform, false);
        coreObject.transform.position = _position + _normal * 0.025f;
        _core = coreObject.transform;

        Collider coreCollider = coreObject.GetComponent<Collider>();

        if (coreCollider != null)
        {
            Destroy(coreCollider);
        }

        Renderer coreRenderer = coreObject.GetComponent<Renderer>();

        if (coreRenderer != null)
        {
            if (_coreMaterial != null)
            {
                coreRenderer.sharedMaterial = _coreMaterial;
            }

            coreRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;
        }
    }

    private void BuildParticles(
        int particleCount,
        float particleSpeed,
        float particleLifetime,
        float particleConeAngle
    )
    {
        GameObject particleObject = new GameObject("ImpactSparks");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.position = _position;
        particleObject.transform.rotation =
            Quaternion.LookRotation(_normal);

        ParticleSystem system =
            particleObject.AddComponent<ParticleSystem>();
        system.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.05f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            particleLifetime * 0.55f,
            particleLifetime
        );
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            particleSpeed * 0.45f,
            particleSpeed
        );
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.10f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            Color.white,
            _color
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(8, particleCount);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = particleConeAngle;
        shape.radius = 0.045f;

        ParticleSystemRenderer renderer =
            system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.16f;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (_particleMaterial != null)
        {
            renderer.sharedMaterial = _particleMaterial;
        }

        system.Play(true);
        system.Emit(Mathf.Max(1, particleCount));
        system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float progress = Mathf.Clamp01(_age / _duration);
        RefreshVisuals(progress);

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void RefreshVisuals(float progress)
    {
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        float radius = Mathf.Lerp(_startRadius, _endRadius, eased);
        Color fadedColor = _color;
        fadedColor.a *= 1f - progress;

        if (_ring != null)
        {
            _ring.startColor = fadedColor;
            _ring.endColor = fadedColor;
            float width = _startWidth * Mathf.Lerp(1f, 0.35f, progress);
            _ring.startWidth = width;
            _ring.endWidth = width;
            SetCircleGeometry(_ring, radius);
        }

        if (_core != null)
        {
            float coreScale = _coreStartSize *
                Mathf.Pow(1f - progress, 1.6f);
            _core.localScale = Vector3.one * coreScale;
        }
    }

    private void SetCircleGeometry(LineRenderer line, float radius)
    {
        Vector3 axisA = Vector3.Cross(_normal, Vector3.up);

        if (axisA.sqrMagnitude <= 0.0001f)
        {
            axisA = Vector3.Cross(_normal, Vector3.right);
        }

        axisA.Normalize();
        Vector3 axisB = Vector3.Cross(_normal, axisA).normalized;

        for (int index = 0; index < line.positionCount; index++)
        {
            float angle = index /
                (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(
                index,
                _position +
                (axisA * Mathf.Cos(angle) +
                 axisB * Mathf.Sin(angle)) * radius
            );
        }
    }

    private static void SetMaterialColor(Material material, Color color)
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
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
        }

        if (_coreMaterial != null)
        {
            Destroy(_coreMaterial);
        }

        if (_particleMaterial != null)
        {
            Destroy(_particleMaterial);
        }
    }
}
