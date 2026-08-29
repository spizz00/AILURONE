using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExitPortalFlowVisualController : MonoBehaviour
{
    private const int RingCount = 3;
    private const int DashesPerRing = 12;
    private const float PulseDuration = 0.72f;
    private const float ActivationDelay = 0.95f;
    private const float PullRadius = 7.25f;
    private const float CaptureRadius = 2.15f;
    private const float CaptureDuration = 0.68f;

    private static readonly Color DormantCyan =
        new Color(0.015f, 0.11f, 0.14f, 1f);
    private static readonly Color ActiveCyan =
        new Color(0.12f, 1.15f, 1.35f, 1f);
    private static readonly Color WhiteHot =
        new Color(1.7f, 2.05f, 2.2f, 1f);

    private sealed class RingVisual
    {
        public Transform Root;
        public LineRenderer[] Dashes;
        public Quaternion BaseRotation;
        public float Speed;
        public float Angle;
    }

    private readonly List<Material> _runtimeMaterials =
        new List<Material>();
    private readonly RingVisual[] _rings =
        new RingVisual[RingCount];

    private Transform _visualRoot;
    private Transform _coreSphere;
    private Transform _haloSphere;
    private Transform _auraSphere;
    private Material _coreMaterial;
    private Material _haloMaterial;
    private Material _auraMaterial;
    private ParticleSystem _flowParticles;
    private Light _portalLight;
    private AudioSource _portalAudio;
    private Collider[] _portalColliders;
    private Renderer _legacyRenderer;
    private LevelExitPortal _legacyExitTrigger;

    private bool _initialized;
    private int _currentStage;
    private int _maximumStage = RingCount;
    private float _progress;
    private float _targetProgress;
    private float _pulseRemaining;
    private float _activationElapsed;
    private float _animationClock;
    private bool _fieldArmed;

    private Transform _playerTransform;
    private StarterAssets.FirstPersonController _playerController;
    private CharacterController _characterController;
    private float _playerSearchCooldown;

    private bool _capturing;
    private bool _transitionTriggered;
    private bool _ownsActionLock;
    private float _captureElapsed;
    private Vector3 _captureStartPosition;
    private Vector3 _captureRootOffset;
    private Vector3 _captureSideOffset;

    public int CurrentStage => _currentStage;
    public bool IsCalibrated => _currentStage >= _maximumStage;
    public bool IsFieldArmed => _fieldArmed;
    public bool IsCapturing => _capturing;
    public float CalibrationProgress => _progress;

    public void Initialize(
        Vector3 fixedScale,
        Light portalLight,
        AudioSource portalAudio,
        int maximumStage
    )
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _maximumStage = Mathf.Max(1, maximumStage);
        _portalLight = portalLight;
        _portalAudio = portalAudio;
        _portalColliders = GetComponents<Collider>();
        _legacyRenderer = GetComponent<Renderer>();
        _legacyExitTrigger = GetComponent<LevelExitPortal>();

        // fixedScale remains part of the legacy GlitchPortal API. The new field
        // uses world-space dimensions so a non-uniform portal scale cannot turn
        // the energy sphere into a cube or flattened disc.
        _ = fixedScale;

        if (_legacyRenderer != null)
        {
            _legacyRenderer.enabled = false;
        }

        foreach (Collider portalCollider in _portalColliders)
        {
            if (portalCollider != null)
            {
                portalCollider.enabled = false;
            }
        }

        if (_legacyExitTrigger != null)
        {
            _legacyExitTrigger.enabled = false;
        }

        if (_portalLight != null)
        {
            _portalLight.color = ActiveCyan;
            _portalLight.range = Mathf.Max(14f, _portalLight.range);
        }

        if (_portalAudio != null)
        {
            _portalAudio.loop = true;
            _portalAudio.spatialBlend = 1f;
            _portalAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            _portalAudio.minDistance = 2f;
            _portalAudio.maxDistance = 18f;
            _portalAudio.pitch = 0.68f;
            _portalAudio.volume = 0.035f;

            if (_portalAudio.clip != null && !_portalAudio.isPlaying)
            {
                _portalAudio.Play();
            }
        }

        BuildVisuals();
        SetStage(0, true);
    }

    public void SetStage(int stage, bool instant)
    {
        if (!_initialized)
        {
            return;
        }

        int previousStage = _currentStage;
        int nextStage = Mathf.Clamp(stage, 0, _maximumStage);

        if (nextStage > previousStage)
        {
            _pulseRemaining = PulseDuration;
            if (_flowParticles != null)
            {
                _flowParticles.Emit(14 + nextStage * 10);
            }
        }

        _currentStage = nextStage;
        _targetProgress =
            _currentStage / (float)_maximumStage;

        if (!IsCalibrated)
        {
            _fieldArmed = false;
            _activationElapsed = 0f;
        }
        else if (instant)
        {
            _activationElapsed = ActivationDelay;
            ArmField();
        }
        else if (previousStage < _maximumStage)
        {
            _activationElapsed = 0f;
            _fieldArmed = false;
        }

        if (instant)
        {
            _progress = _targetProgress;
            ApplyVisualState(0f);
        }
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        UpdateVisualRootPose();

        float delta = AILURONEGameplayActionGate.IsPaused
            ? 0f
            : Mathf.Max(0f, Time.deltaTime);

        _animationClock += delta;
        _progress = Mathf.MoveTowards(
            _progress,
            _targetProgress,
            delta * 1.55f
        );
        _pulseRemaining = Mathf.Max(
            0f,
            _pulseRemaining - delta
        );

        if (IsCalibrated && !_fieldArmed)
        {
            _activationElapsed += delta;
            if (_activationElapsed >= ActivationDelay)
            {
                ArmField();
            }
        }

        float pulse = 0f;
        if (_pulseRemaining > 0f)
        {
            float pulseTime =
                1f - _pulseRemaining / PulseDuration;
            pulse = Mathf.Sin(pulseTime * Mathf.PI);
        }

        UpdateRingMotion(delta);
        ApplyVisualState(pulse);
        UpdateAttraction(delta);
    }

    private void LateUpdate()
    {
        if (!_capturing || _transitionTriggered)
        {
            return;
        }

        if (AILURONEGameplayActionGate.IsPaused)
        {
            return;
        }

        if (_playerTransform == null)
        {
            CancelCapture();
            return;
        }

        _captureElapsed += Mathf.Max(0f, Time.deltaTime);
        float normalized = Mathf.Clamp01(
            _captureElapsed / CaptureDuration
        );
        float eased = normalized * normalized *
            (3f - 2f * normalized);

        Vector3 targetPosition =
            GetPortalCenter() + _captureRootOffset;
        Vector3 spiral =
            _captureSideOffset *
            Mathf.Sin(normalized * Mathf.PI * 2f) *
            (1f - normalized);
        Vector3 lift =
            Vector3.up * Mathf.Sin(normalized * Mathf.PI) * 0.18f;
        Vector3 desiredPosition = Vector3.Lerp(
            _captureStartPosition,
            targetPosition,
            eased
        ) + spiral + lift;

        Vector3 displacement =
            desiredPosition - _playerTransform.position;

        if (_characterController != null &&
            _characterController.enabled)
        {
            _characterController.Move(displacement);
        }
        else
        {
            _playerTransform.position = desiredPosition;
        }

        if (_playerController != null)
        {
            _playerController.SetCinematicInputControl(
                0f,
                Mathf.Lerp(0.18f, 0f, normalized),
                true,
                -2.5f * eased,
                Mathf.Sin(normalized * Mathf.PI) * 3.2f
            );
        }

        if (normalized >= 1f)
        {
            CompleteCapture();
        }
    }

    private void BuildVisuals()
    {
        GameObject rootObject = new GameObject(
            "ExitPortal_ChannelField_Runtime"
        );
        rootObject.layer = gameObject.layer;
        _visualRoot = rootObject.transform;
        UpdateVisualRootPose();

        _coreMaterial = CreateUnlitMaterial(WhiteHot, true);
        _haloMaterial = CreateUnlitMaterial(ActiveCyan, true);
        _auraMaterial = CreateUnlitMaterial(ActiveCyan, true);

        _coreSphere = CreateSphere(
            "WhiteHot_Core",
            _coreMaterial
        );
        _haloSphere = CreateSphere(
            "Cyan_EnergyShell",
            _haloMaterial
        );
        _auraSphere = CreateSphere(
            "Cyan_OuterAura",
            _auraMaterial
        );

        Material ringMaterial =
            CreateUnlitMaterial(Color.white, true);

        _rings[0] = CreateRing(
            "CoreChannel_Ring_A",
            1.65f,
            Quaternion.Euler(12f, 68f, 4f),
            43f,
            ringMaterial
        );
        _rings[1] = CreateRing(
            "CoreChannel_Ring_B",
            2.25f,
            Quaternion.Euler(72f, 8f, 34f),
            -32f,
            ringMaterial
        );
        _rings[2] = CreateRing(
            "CoreChannel_Ring_C",
            2.85f,
            Quaternion.Euler(108f, 52f, -8f),
            24f,
            ringMaterial
        );

        BuildFlowParticles();
    }

    private Transform CreateSphere(
        string objectName,
        Material material
    )
    {
        GameObject sphere = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );
        sphere.name = objectName;
        sphere.layer = gameObject.layer;
        sphere.transform.SetParent(_visualRoot, false);

        Collider sphereCollider = sphere.GetComponent<Collider>();
        if (sphereCollider != null)
        {
            Destroy(sphereCollider);
        }

        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        sphereRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        sphereRenderer.receiveShadows = false;
        sphereRenderer.sharedMaterial = material;

        return sphere.transform;
    }

    private RingVisual CreateRing(
        string objectName,
        float radius,
        Quaternion baseRotation,
        float speed,
        Material material
    )
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.layer = gameObject.layer;
        ringObject.transform.SetParent(_visualRoot, false);

        RingVisual ring = new RingVisual
        {
            Root = ringObject.transform,
            Dashes = new LineRenderer[DashesPerRing],
            BaseRotation = baseRotation,
            Speed = speed,
            Angle = 0f
        };

        float slotDegrees = 360f / DashesPerRing;
        float dashDegrees = slotDegrees * 0.66f;

        for (int index = 0; index < DashesPerRing; index++)
        {
            ring.Dashes[index] = CreateRingDash(
                $"ChannelDash_{index + 1:00}",
                ring.Root,
                radius,
                index * slotDegrees,
                dashDegrees,
                material
            );
        }

        ring.Root.localRotation = baseRotation;
        return ring;
    }

    private LineRenderer CreateRingDash(
        string objectName,
        Transform parent,
        float radius,
        float startDegrees,
        float arcDegrees,
        Material material
    )
    {
        const int PointCount = 7;

        GameObject dashObject = new GameObject(
            objectName,
            typeof(LineRenderer)
        );
        dashObject.layer = gameObject.layer;
        dashObject.transform.SetParent(parent, false);

        LineRenderer line = dashObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = PointCount;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.sharedMaterial = material;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        for (int index = 0; index < PointCount; index++)
        {
            float normalized =
                index / (float)(PointCount - 1);
            float radians =
                (startDegrees + arcDegrees * normalized) *
                Mathf.Deg2Rad;
            line.SetPosition(
                index,
                new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f
                )
            );
        }

        return line;
    }

    private void BuildFlowParticles()
    {
        GameObject particleObject = new GameObject(
            "Inward_ChannelStreaks",
            typeof(ParticleSystem)
        );
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(_visualRoot, false);

        _flowParticles = particleObject.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = _flowParticles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 260;
        main.startLifetime = 1.15f;
        main.startSpeed = 0f;
        main.startSize = 0.035f;
        main.startColor = ActiveCyan;

        ParticleSystem.EmissionModule emission =
            _flowParticles.emission;
        emission.rateOverTime = 3f;

        ParticleSystem.ShapeModule shape = _flowParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 3.35f;
        shape.radiusThickness = 0.18f;

        ParticleSystem.VelocityOverLifetimeModule velocity =
            _flowParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = -0.45f;

        ParticleSystem.NoiseModule noise = _flowParticles.noise;
        noise.enabled = true;
        noise.strength = 0.16f;
        noise.frequency = 0.42f;
        noise.scrollSpeed = 0.35f;

        ParticleSystemRenderer particleRenderer =
            particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode =
            ParticleSystemRenderMode.Stretch;
        particleRenderer.lengthScale = 3f;
        particleRenderer.velocityScale = 0.25f;
        particleRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.sharedMaterial =
            CreateUnlitMaterial(Color.white, true);

        _flowParticles.Play();
    }

    private void UpdateVisualRootPose()
    {
        if (_visualRoot == null)
        {
            return;
        }

        _visualRoot.position = transform.position;
        _visualRoot.rotation = transform.rotation;
        _visualRoot.localScale = Vector3.one;
    }

    private void UpdateRingMotion(float delta)
    {
        for (int index = 0; index < _rings.Length; index++)
        {
            RingVisual ring = _rings[index];
            if (ring == null || ring.Root == null)
            {
                continue;
            }

            float ringActivation = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(_progress * RingCount - index)
            );
            float speedMultiplier = Mathf.Lerp(
                0.12f,
                _fieldArmed ? 1.35f : 1f,
                ringActivation
            );

            ring.Angle += ring.Speed * speedMultiplier * delta;
            ring.Root.localRotation =
                ring.BaseRotation *
                Quaternion.AngleAxis(ring.Angle, Vector3.forward);
        }
    }

    private void ApplyVisualState(float pulse)
    {
        if (_coreSphere == null)
        {
            return;
        }

        float activationCharge = IsCalibrated
            ? Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(_activationElapsed / ActivationDelay)
            )
            : 0f;
        float idlePulse =
            0.5f + 0.5f * Mathf.Sin(_animationClock * 2.35f);

        float coreDiameter =
            Mathf.Lerp(0.55f, 1.38f, _progress) *
            (1f + idlePulse * 0.035f + pulse * 0.12f);
        float haloDiameter =
            Mathf.Lerp(1.05f, 2.15f, _progress) *
            (1f + idlePulse * 0.055f + pulse * 0.16f);
        float auraDiameter =
            Mathf.Lerp(1.55f, 3.15f, _progress) *
            (1f + idlePulse * 0.075f + activationCharge * 0.08f);

        _coreSphere.localScale = Vector3.one * coreDiameter;
        _haloSphere.localScale = Vector3.one * haloDiameter;
        _auraSphere.localScale = Vector3.one * auraDiameter;

        Color coreColor = Color.Lerp(
            new Color(0.08f, 0.42f, 0.5f, 0.35f),
            WhiteHot,
            Mathf.Clamp01(_progress + activationCharge * 0.25f)
        );
        coreColor.a = Mathf.Lerp(0.28f, 0.96f, _progress);
        SetMaterialColor(_coreMaterial, coreColor);

        Color haloColor = Color.Lerp(
            DormantCyan,
            ActiveCyan,
            _progress
        );
        haloColor.a = Mathf.Lerp(0.035f, 0.28f, _progress);
        SetMaterialColor(_haloMaterial, haloColor);

        Color auraColor = Color.Lerp(
            DormantCyan,
            ActiveCyan,
            Mathf.Clamp01(_progress + activationCharge * 0.2f)
        );
        auraColor.a = Mathf.Lerp(0.012f, 0.11f, _progress);
        SetMaterialColor(_auraMaterial, auraColor);

        for (int ringIndex = 0; ringIndex < _rings.Length; ringIndex++)
        {
            RingVisual ring = _rings[ringIndex];
            if (ring == null)
            {
                continue;
            }

            float ringActivation = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(_progress * RingCount - ringIndex)
            );
            ring.Root.localScale = Vector3.one *
                (1f + pulse * 0.035f + activationCharge * 0.025f);

            for (int dashIndex = 0;
                dashIndex < ring.Dashes.Length;
                dashIndex++)
            {
                LineRenderer dash = ring.Dashes[dashIndex];
                if (dash == null)
                {
                    continue;
                }

                float chase = 0.78f + 0.22f * Mathf.Sin(
                    _animationClock * (3.2f + _progress * 3.8f) +
                    dashIndex * 0.72f +
                    ringIndex * 1.9f
                );
                Color dashColor = Color.Lerp(
                    DormantCyan,
                    ActiveCyan,
                    ringActivation
                );
                dashColor.a = Mathf.Lerp(
                    0.025f,
                    0.92f,
                    ringActivation
                ) * chase;

                dash.startColor = dashColor;
                dash.endColor = dashColor;
                dash.widthMultiplier = Mathf.Lerp(
                    0.022f,
                    0.078f,
                    ringActivation
                ) * (1f + pulse * 0.24f);
            }
        }

        if (_flowParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                _flowParticles.emission;
            emission.rateOverTime = Mathf.Lerp(
                3f,
                _fieldArmed ? 82f : 58f,
                _progress
            );

            ParticleSystem.MainModule main = _flowParticles.main;
            main.startSize = Mathf.Lerp(0.018f, 0.055f, _progress);
            main.startColor = Color.Lerp(
                new Color(0.04f, 0.28f, 0.34f, 0.18f),
                new Color(0.62f, 1.25f, 1.45f, 0.95f),
                _progress
            );

            ParticleSystem.VelocityOverLifetimeModule velocity =
                _flowParticles.velocityOverLifetime;
            velocity.radial = -Mathf.Lerp(
                0.45f,
                _fieldArmed ? 4.2f : 2.8f,
                _progress
            );
        }

        if (_portalLight != null)
        {
            _portalLight.intensity =
                Mathf.Lerp(0.35f, 18f, _progress * _progress) +
                activationCharge * 4f +
                pulse * 5f;
        }

        if (_portalAudio != null)
        {
            _portalAudio.pitch =
                Mathf.Lerp(0.68f, 1.08f, _progress) +
                activationCharge * 0.08f +
                pulse * 0.06f;
            _portalAudio.volume = Mathf.Lerp(
                0.035f,
                _fieldArmed ? 0.34f : 0.25f,
                _progress
            );
        }
    }

    private void ArmField()
    {
        if (_fieldArmed || !IsCalibrated)
        {
            return;
        }

        _fieldArmed = true;
        _pulseRemaining = PulseDuration;

        if (_flowParticles != null)
        {
            _flowParticles.Emit(84);
        }
    }

    private void UpdateAttraction(float delta)
    {
        if (!_fieldArmed ||
            _capturing ||
            _transitionTriggered ||
            delta <= 0f ||
            !AILURONEGameplayActionGate.AllowsGameplayActions)
        {
            return;
        }

        if (!TryResolvePlayer(delta))
        {
            return;
        }

        Vector3 playerCenter = GetPlayerCenter();
        Vector3 toPortal = GetPortalCenter() - playerCenter;
        float distance = toPortal.magnitude;

        if (distance > PullRadius)
        {
            return;
        }

        if (distance <= CaptureRadius)
        {
            BeginCapture(playerCenter, toPortal);
            return;
        }

        float proximity = 1f - Mathf.InverseLerp(
            CaptureRadius,
            PullRadius,
            distance
        );
        float acceleration = Mathf.Lerp(
            3.5f,
            25f,
            proximity * proximity
        );

        _playerController.AddExternalVelocity(
            toPortal.normalized * acceleration * delta,
            8.5f,
            proximity > 0.78f
        );
    }

    private bool TryResolvePlayer(float delta)
    {
        if (_playerTransform != null &&
            _playerController != null &&
            _playerTransform.gameObject.activeInHierarchy)
        {
            return true;
        }

        _playerSearchCooldown -= delta;
        if (_playerSearchCooldown > 0f)
        {
            return false;
        }

        _playerSearchCooldown = 0.5f;

        StarterAssets.FirstPersonController candidate =
            StarterAssets.FirstPersonController.Instance;
        if (candidate == null)
        {
            candidate = FindFirstObjectByType<
                StarterAssets.FirstPersonController
            >();
        }

        if (candidate == null)
        {
            return false;
        }

        _playerController = candidate;
        _playerTransform = candidate.transform;
        _characterController =
            candidate.GetComponent<CharacterController>();
        return _characterController != null;
    }

    private Vector3 GetPlayerCenter()
    {
        if (_characterController != null &&
            _characterController.enabled)
        {
            return _characterController.bounds.center;
        }

        return _playerTransform != null
            ? _playerTransform.position + Vector3.up
            : Vector3.zero;
    }

    private Vector3 GetPortalCenter()
    {
        return _visualRoot != null
            ? _visualRoot.position
            : transform.position;
    }

    private void BeginCapture(
        Vector3 playerCenter,
        Vector3 toPortal
    )
    {
        if (_capturing || _transitionTriggered)
        {
            return;
        }

        _capturing = true;
        _captureElapsed = 0f;
        _captureStartPosition = _playerTransform.position;
        _captureRootOffset =
            _playerTransform.position - playerCenter;

        Vector3 direction = toPortal.sqrMagnitude > 0.0001f
            ? toPortal.normalized
            : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        if (side.sqrMagnitude < 0.001f)
        {
            side = transform.right;
        }

        _captureSideOffset = side.normalized * Mathf.Min(
            0.52f,
            toPortal.magnitude * 0.18f
        );

        AILURONEGameplayActionGate.SetDeploymentLocked(true);
        _ownsActionLock = true;

        if (_playerController != null)
        {
            _playerController.SetCinematicInputControl(
                0f,
                0.18f,
                true
            );
        }
    }

    private void CompleteCapture()
    {
        _capturing = false;

        if (GameManager.Instance == null)
        {
            Debug.LogWarning(
                "[ExitPortal] Capture completed, but GameManager was not found."
            );
            _fieldArmed = false;
            CancelCapture();
            return;
        }

        _transitionTriggered = true;
        GameManager.Instance.TriggerWin();
    }

    private void CancelCapture()
    {
        _capturing = false;

        if (_playerController != null && !_transitionTriggered)
        {
            _playerController.ClearCinematicInputControl();
        }

        if (_ownsActionLock && !_transitionTriggered)
        {
            AILURONEGameplayActionGate.SetDeploymentLocked(false);
            _ownsActionLock = false;
        }
    }

    private Material CreateUnlitMaterial(
        Color color,
        bool additive
    )
    {
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        material.name = additive
            ? "PortalChannel_Additive_Runtime"
            : "PortalChannel_Surface_Runtime";
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt(
            "_SrcBlend",
            (int)UnityEngine.Rendering.BlendMode.SrcAlpha
        );
        material.SetInt(
            "_DstBlend",
            additive
                ? (int)UnityEngine.Rendering.BlendMode.One
                : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
        );
        material.SetInt("_ZWrite", 0);

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", additive ? 2f : 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 2f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        SetMaterialColor(material, color);
        _runtimeMaterials.Add(material);
        return material;
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

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else
        {
            material.color = color;
        }
    }

    private void OnEnable()
    {
        if (_initialized && _visualRoot != null)
        {
            _visualRoot.gameObject.SetActive(true);
            UpdateVisualRootPose();
        }
    }

    private void OnDisable()
    {
        if (_visualRoot != null)
        {
            _visualRoot.gameObject.SetActive(false);
        }

        if (!_transitionTriggered)
        {
            CancelCapture();
        }
    }

    private void OnDestroy()
    {
        if (!_transitionTriggered)
        {
            CancelCapture();
        }

        if (_visualRoot != null)
        {
            Destroy(_visualRoot.gameObject);
        }

        foreach (Material material in _runtimeMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
