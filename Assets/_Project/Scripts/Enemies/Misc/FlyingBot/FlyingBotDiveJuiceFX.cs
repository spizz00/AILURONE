#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only feedback for the Flying Bot dive attack.
/// It owns no combat decisions: the enemy state machine tells it when the
/// wind-up, lock, launch and impact events happen.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlyingBotDiveJuiceFX : MonoBehaviour
{
    private sealed class RingPulse
    {
        public LineRenderer Line;
        public Vector3 Position;
        public Vector3 Normal;
        public Color Color;
        public float StartRadius;
        public float EndRadius;
        public float Duration;
        public float Age;
    }

    private readonly List<RingPulse> _ringPulses =
        new List<RingPulse>(3);

    private Transform _anchor;
    private Transform _visualRoot;
    private Transform _chargeCore;
    private ParticleSystem _chargeParticles;
    private LineRenderer _chargeRing;
    private TrailRenderer _leftTrail;
    private TrailRenderer _rightTrail;

    private Material _coreMaterial;
    private Material _lineMaterial;
    private Material _particleMaterial;
    private Material _trailMaterial;

    private Color _chargeColor;
    private float _windup01;
    private float _lockFlashRemaining;
    private float _wingOffset = 0.42f;
    private float _finishRemaining;
    private bool _locked;
    private bool _launched;
    private bool _finishing;

    public static FlyingBotDiveJuiceFX Spawn(
        Transform anchor,
        Transform visualRoot,
        Color chargeColor
    )
    {
        if (anchor == null)
        {
            return null;
        }

        GameObject root = new GameObject("FlyingBot_DiveJuiceFX");
        FlyingBotDiveJuiceFX fx =
            root.AddComponent<FlyingBotDiveJuiceFX>();
        fx.Initialize(anchor, visualRoot, chargeColor);
        return fx;
    }

    private void Initialize(
        Transform anchor,
        Transform visualRoot,
        Color chargeColor
    )
    {
        _anchor = anchor;
        _visualRoot = visualRoot != null ? visualRoot : anchor;
        _chargeColor = chargeColor;
        _wingOffset = EstimateWingOffset(_visualRoot);

        BuildMaterials();
        BuildChargeCore();
        BuildChargeRing();
        BuildChargeParticles();
        RefreshWindupVisuals();
    }

    private void BuildMaterials()
    {
        Shader unlitShader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Unlit/Color");
        }

        Shader spriteShader = Shader.Find("Sprites/Default");

        if (spriteShader == null)
        {
            spriteShader = unlitShader;
        }

        if (unlitShader != null)
        {
            _coreMaterial = new Material(unlitShader);
        }

        if (spriteShader != null)
        {
            _lineMaterial = new Material(spriteShader);
            _particleMaterial = new Material(spriteShader);
            _trailMaterial = new Material(spriteShader);
        }

        SetMaterialColor(_coreMaterial, _chargeColor);
        SetMaterialColor(_lineMaterial, Color.white);
        SetMaterialColor(_particleMaterial, Color.white);
        SetMaterialColor(_trailMaterial, Color.white);
    }

    private void BuildChargeCore()
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "ChargeCore";
        core.transform.SetParent(transform, false);
        _chargeCore = core.transform;

        Collider coreCollider = core.GetComponent<Collider>();

        if (coreCollider != null)
        {
            coreCollider.enabled = false;
            Destroy(coreCollider);
        }

        Renderer coreRenderer = core.GetComponent<Renderer>();

        if (coreRenderer != null && _coreMaterial != null)
        {
            coreRenderer.sharedMaterial = _coreMaterial;
            coreRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;
        }
    }

    private void BuildChargeRing()
    {
        _chargeRing = CreateLineRenderer("ChargeRing", 34, true);
        _chargeRing.startWidth = 0.025f;
        _chargeRing.endWidth = 0.025f;
    }

    private void BuildChargeParticles()
    {
        GameObject particleObject = new GameObject("ChargeMotes");
        particleObject.transform.SetParent(transform, false);
        _chargeParticles = particleObject.AddComponent<ParticleSystem>();
        _chargeParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _chargeParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.26f, 0.46f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-0.92f, -0.38f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.115f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            Color.Lerp(Color.white, _chargeColor, 0.35f),
            _chargeColor
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 72;

        ParticleSystem.EmissionModule emission = _chargeParticles.emission;
        emission.rateOverTime = 34f;

        ParticleSystem.ShapeModule shape = _chargeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.58f;
        shape.radiusThickness = 1f;

        ParticleSystemRenderer particleRenderer =
            _chargeParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        if (_particleMaterial != null)
        {
            particleRenderer.sharedMaterial = _particleMaterial;
        }

        _chargeParticles.Play(true);
    }

    public void SetWindupProgress(float normalizedProgress)
    {
        if (_launched || _finishing)
        {
            return;
        }

        _windup01 = Mathf.Clamp01(normalizedProgress);
    }

    public void LockFlash()
    {
        if (_launched || _finishing)
        {
            return;
        }

        _locked = true;
        _windup01 = 1f;
        _lockFlashRemaining = 0.20f;

        if (_anchor != null)
        {
            AddRingPulse(
                _anchor.position,
                _anchor.forward,
                Color.Lerp(Color.white, _chargeColor, 0.30f),
                0.12f,
                0.82f,
                0.22f,
                0.075f
            );
        }
    }

    public void Launch(Vector3 direction)
    {
        if (_launched || _finishing)
        {
            return;
        }

        _launched = true;
        Vector3 launchDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : (_anchor != null ? _anchor.forward : Vector3.forward);
        Vector3 launchPosition = _anchor != null
            ? _anchor.position
            : transform.position;

        if (_chargeCore != null)
        {
            _chargeCore.gameObject.SetActive(false);
        }

        if (_chargeRing != null)
        {
            _chargeRing.enabled = false;
        }

        if (_chargeParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                _chargeParticles.emission;
            emission.enabled = false;
        }

        BuildDiveTrails();
        AddRingPulse(
            launchPosition,
            launchDirection,
            Color.Lerp(Color.white, _chargeColor, 0.42f),
            0.18f,
            1.55f,
            0.27f,
            0.105f
        );
        SpawnBurst(launchPosition, launchDirection, 26, 2.5f, 0.34f);
    }

    public void PlayerImpact(Vector3 position, Vector3 direction)
    {
        Vector3 normal = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.up;

        AddRingPulse(
            position,
            normal,
            Color.Lerp(Color.white, _chargeColor, 0.25f),
            0.14f,
            1.15f,
            0.24f,
            0.11f
        );
        SpawnBurst(position, normal, 34, 3.4f, 0.42f);
    }

    public void Complete(
        bool crashed,
        Vector3 position,
        Vector3 direction
    )
    {
        if (_finishing)
        {
            return;
        }

        _finishing = true;
        _finishRemaining = crashed ? 0.60f : 0.42f;
        StopTrailEmission();

        if (!crashed)
        {
            return;
        }

        Vector3 normal = direction.sqrMagnitude > 0.0001f
            ? -direction.normalized
            : Vector3.up;

        AddRingPulse(
            position,
            normal,
            Color.Lerp(Color.white, _chargeColor, 0.18f),
            0.18f,
            1.75f,
            0.34f,
            0.13f
        );
        SpawnBurst(position, normal, 46, 4.2f, 0.52f);
    }

    public void CancelImmediate()
    {
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        UpdateRingPulses(Time.deltaTime);

        if (_finishing)
        {
            _finishRemaining -= Time.deltaTime;

            if (_finishRemaining <= 0f)
            {
                Destroy(gameObject);
            }

            return;
        }

        if (_anchor == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!_launched)
        {
            RefreshWindupVisuals();
        }
        else
        {
            RefreshTrailAnchors();
        }
    }

    private void RefreshWindupVisuals()
    {
        if (_anchor == null)
        {
            return;
        }

        float time = Time.time;
        float pulseSpeed = Mathf.Lerp(9f, 24f, _windup01);
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseSpeed);
        float flash01 = _lockFlashRemaining > 0f
            ? Mathf.Clamp01(_lockFlashRemaining / 0.20f)
            : 0f;
        _lockFlashRemaining = Mathf.Max(
            0f,
            _lockFlashRemaining - Time.deltaTime
        );

        Vector3 position = _anchor.position + _anchor.forward * 0.28f;
        float coreSize = Mathf.Lerp(0.18f, 0.42f, _windup01) *
            Mathf.Lerp(0.88f, 1.18f, pulse);
        coreSize *= 1f + flash01 * 0.55f;

        if (_chargeCore != null)
        {
            _chargeCore.position = position;
            _chargeCore.localScale = Vector3.one * coreSize;
        }

        if (_chargeParticles != null)
        {
            _chargeParticles.transform.position = position;
            ParticleSystem.EmissionModule emission =
                _chargeParticles.emission;
            emission.rateOverTime = Mathf.Lerp(26f, 68f, _windup01);
        }

        if (_chargeRing != null)
        {
            float ringRadius = Mathf.Lerp(0.72f, 0.34f, _windup01);
            ringRadius *= Mathf.Lerp(0.96f, 1.05f, pulse);
            SetCircleGeometry(
                _chargeRing,
                position,
                _anchor.forward,
                ringRadius
            );

            Color ringColor = Color.Lerp(
                new Color(_chargeColor.r, _chargeColor.g, _chargeColor.b, 0.25f),
                new Color(_chargeColor.r, _chargeColor.g, _chargeColor.b, 0.92f),
                _windup01
            );
            ringColor = Color.Lerp(ringColor, Color.white, flash01 * 0.80f);
            _chargeRing.startColor = ringColor;
            _chargeRing.endColor = ringColor;
            float width = _locked ? 0.095f : Mathf.Lerp(0.042f, 0.072f, _windup01);
            _chargeRing.startWidth = width;
            _chargeRing.endWidth = width;
        }

        Color coreColor = Color.Lerp(
            _chargeColor,
            Color.white * 1.8f,
            Mathf.Clamp01(_windup01 * 0.48f + flash01 * 0.80f)
        );
        SetMaterialColor(_coreMaterial, coreColor);
    }

    private void BuildDiveTrails()
    {
        _leftTrail = CreateTrailRenderer("LeftWingTrail");
        _rightTrail = CreateTrailRenderer("RightWingTrail");
        RefreshTrailAnchors();
        _leftTrail.Clear();
        _rightTrail.Clear();
    }

    private TrailRenderer CreateTrailRenderer(string trailName)
    {
        GameObject trailObject = new GameObject(trailName);
        trailObject.transform.SetParent(transform, false);
        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = 0.32f;
        trail.minVertexDistance = 0.025f;
        trail.widthMultiplier = 0.16f;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        if (_trailMaterial != null)
        {
            trail.sharedMaterial = _trailMaterial;
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white * 1.4f, 0f),
                new GradientColorKey(_chargeColor, 0.28f),
                new GradientColorKey(new Color(1.5f, 0.12f, 0.025f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.86f, 0.28f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;
        return trail;
    }

    private void RefreshTrailAnchors()
    {
        if (_visualRoot == null)
        {
            return;
        }

        Vector3 center = _visualRoot.position -
            _visualRoot.forward * 0.10f;
        Vector3 side = _visualRoot.right * _wingOffset;

        if (_leftTrail != null)
        {
            _leftTrail.transform.position = center - side;
        }

        if (_rightTrail != null)
        {
            _rightTrail.transform.position = center + side;
        }
    }

    private void StopTrailEmission()
    {
        if (_leftTrail != null)
        {
            _leftTrail.emitting = false;
        }

        if (_rightTrail != null)
        {
            _rightTrail.emitting = false;
        }
    }

    private void SpawnBurst(
        Vector3 position,
        Vector3 direction,
        int count,
        float speed,
        float lifetime
    )
    {
        GameObject burstObject = new GameObject("DiveImpactBurst");
        burstObject.transform.SetParent(transform, false);
        burstObject.transform.position = position;

        ParticleSystem system = burstObject.AddComponent<ParticleSystem>();
        system.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.05f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            lifetime * 0.55f,
            lifetime
        );
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            speed * 0.45f,
            speed
        );
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            Color.white,
            _chargeColor
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = Mathf.Max(8, count);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 48f;
        shape.radius = 0.14f;
        burstObject.transform.rotation = Quaternion.LookRotation(
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.up
        );

        ParticleSystemRenderer renderer =
            system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.8f;
        renderer.velocityScale = 0.18f;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (_particleMaterial != null)
        {
            renderer.sharedMaterial = _particleMaterial;
        }

        system.Play(true);
        system.Emit(Mathf.Max(1, count));
        system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void AddRingPulse(
        Vector3 position,
        Vector3 normal,
        Color color,
        float startRadius,
        float endRadius,
        float duration,
        float width
    )
    {
        LineRenderer line = CreateLineRenderer(
            "DiveShockRing",
            42,
            true
        );
        line.startWidth = width;
        line.endWidth = width;

        RingPulse pulse = new RingPulse
        {
            Line = line,
            Position = position,
            Normal = normal.sqrMagnitude > 0.0001f
                ? normal.normalized
                : Vector3.up,
            Color = color,
            StartRadius = startRadius,
            EndRadius = endRadius,
            Duration = Mathf.Max(0.01f, duration),
            Age = 0f
        };

        _ringPulses.Add(pulse);
        SetCircleGeometry(line, position, pulse.Normal, startRadius);
    }

    private void UpdateRingPulses(float deltaTime)
    {
        for (int index = _ringPulses.Count - 1; index >= 0; index--)
        {
            RingPulse pulse = _ringPulses[index];
            pulse.Age += deltaTime;
            float progress = Mathf.Clamp01(pulse.Age / pulse.Duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float radius = Mathf.Lerp(
                pulse.StartRadius,
                pulse.EndRadius,
                eased
            );
            Color color = pulse.Color;
            color.a *= 1f - progress;

            if (pulse.Line != null)
            {
                pulse.Line.startColor = color;
                pulse.Line.endColor = color;
                SetCircleGeometry(
                    pulse.Line,
                    pulse.Position,
                    pulse.Normal,
                    radius
                );
            }

            if (progress < 1f)
            {
                continue;
            }

            if (pulse.Line != null)
            {
                Destroy(pulse.Line.gameObject);
            }

            _ringPulses.RemoveAt(index);
        }
    }

    private LineRenderer CreateLineRenderer(
        string lineName,
        int positionCount,
        bool loop
    )
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = Mathf.Max(3, positionCount);
        line.loop = loop;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        if (_lineMaterial != null)
        {
            line.sharedMaterial = _lineMaterial;
        }

        return line;
    }

    private static void SetCircleGeometry(
        LineRenderer line,
        Vector3 center,
        Vector3 normal,
        float radius
    )
    {
        if (line == null)
        {
            return;
        }

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : Vector3.forward;
        Vector3 axisA = Vector3.Cross(safeNormal, Vector3.up);

        if (axisA.sqrMagnitude <= 0.0001f)
        {
            axisA = Vector3.Cross(safeNormal, Vector3.right);
        }

        axisA.Normalize();
        Vector3 axisB = Vector3.Cross(safeNormal, axisA).normalized;
        int count = line.positionCount;

        for (int index = 0; index < count; index++)
        {
            float angle = index / (float)count * Mathf.PI * 2f;
            Vector3 point = center +
                (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) *
                Mathf.Max(0f, radius);
            line.SetPosition(index, point);
        }
    }

    private static float EstimateWingOffset(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return 0.42f;
        }

        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return 0.42f;
        }

        Bounds bounds = renderers[0].bounds;

        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        Vector3 right = visualRoot.right;
        Vector3 extents = bounds.extents;
        float projectedExtent =
            Mathf.Abs(right.x) * extents.x +
            Mathf.Abs(right.y) * extents.y +
            Mathf.Abs(right.z) * extents.z;
        return Mathf.Clamp(projectedExtent * 0.62f, 0.24f, 0.90f);
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
        if (_coreMaterial != null)
        {
            Destroy(_coreMaterial);
        }

        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
        }

        if (_particleMaterial != null)
        {
            Destroy(_particleMaterial);
        }

        if (_trailMaterial != null)
        {
            Destroy(_trailMaterial);
        }
    }
}
