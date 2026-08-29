using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-30500)]
[DisallowMultipleComponent]
public sealed class EscapePodMalfunctionPresentation : MonoBehaviour
{
    [Header("Sequence References")]
    [SerializeField] private EscapePodFailureController failure;
    [SerializeField] private AudioSource awakeningSource;
    [SerializeField] private AudioSource alarmSource;
    [SerializeField] private AudioLowPassFilter alarmLowPass;
    [SerializeField] private Transform[] sparkAnchors =
        System.Array.Empty<Transform>();
    [SerializeField] private Transform ventAnchor;
    [SerializeField] private TMP_FontAsset promptFont;

    [Header("Awakening Timeline")]
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.4f;
    [SerializeField, Min(0.1f)] private float firstVisionTime = 0.72f;
    [SerializeField, Min(0.1f)] private float partialLookTime = 1.3f;
    [SerializeField, Min(0.1f)] private float alarmDelay = 2.2f;
    [SerializeField, Min(0.1f)] private float fullAwakeTime = 4f;

    [Header("Awakening Mix")]
    [SerializeField, Min(10f)] private float muffledCutoff = 480f;
    [SerializeField, Min(1000f)] private float clearCutoff = 22000f;
    [SerializeField, Range(0f, 1f)] private float alarmStartVolume = 0.025f;
    [SerializeField, Range(0f, 1f)] private float alarmClearVolume = 0.42f;
    [SerializeField, Min(0.1f)] private float alarmDeploymentFade = 1.2f;

    private ParticleSystem[] _sparkSystems =
        System.Array.Empty<ParticleSystem>();
    private ParticleSystem[] _pressureJets =
        System.Array.Empty<ParticleSystem>();
    private ParticleSystem[] _leakSmoke =
        System.Array.Empty<ParticleSystem>();
    private ParticleSystem _thresholdCloud;
    private ParticleSystem _departureSmoke;
    private ParticleSystem _departureFire;
    private ParticleSystem[] _impactSystems =
        System.Array.Empty<ParticleSystem>();
    private Material _sparkMaterial;
    private Material _smokeMaterial;

    private FirstPersonController _playerController;
    private CanvasGroup _awakeningOverlay;
    private Image _blackout;
    private Image _vignette;
    private Image _haze;
    private Image[] _chromaticEdges = System.Array.Empty<Image>();
    private RectTransform _upperEyelid;
    private RectTransform _lowerEyelid;
    private Texture2D _vignetteTexture;
    private Sprite _vignetteSprite;

    private CanvasGroup _promptGroup;
    private float _sequenceElapsed;
    private float _nextSparkAt;
    private float _promptFade;
    private float _deploymentElapsed = -1f;
    private float _deploymentAlarmVolume;
    private bool _releaseStarted;
    private bool _impactTriggered;
    private bool _inputRestored;
    private bool _awakeningPausedByMenu;
    private bool _alarmPausedByMenu;
    private bool _departureEffectsActive;

    public bool AlarmPlaying =>
        alarmSource != null && alarmSource.isPlaying;
    public bool AwakeningPlaying =>
        awakeningSource != null && awakeningSource.isPlaying;
    public float AlarmCutoff =>
        alarmLowPass != null ? alarmLowPass.cutoffFrequency : 0f;
    public float AlarmVolume =>
        alarmSource != null ? alarmSource.volume : 0f;
    public int SparkSystemCount => _sparkSystems.Length;
    public int SteamSystemCount =>
        _pressureJets.Length + _leakSmoke.Length +
        (_thresholdCloud != null ? 1 : 0);
    public bool PromptCreated => _promptGroup != null;
    public bool ReleaseStarted => _releaseStarted;
    public bool InputRestored => _inputRestored;
    public float SequenceElapsed =>
        _sequenceElapsed;
    public bool DepartureEffectsActive => _departureEffectsActive;
    public bool ImpactEffectCreated => _impactSystems.Length == 3;

    public void Configure(
        EscapePodFailureController failureController,
        AudioSource wakeAudio,
        AudioSource alarmAudio,
        AudioLowPassFilter lowPass,
        Transform[] electricalAnchors,
        Transform pressureVent,
        TMP_FontAsset font)
    {
        failure = failureController;
        awakeningSource = wakeAudio;
        alarmSource = alarmAudio;
        alarmLowPass = lowPass;
        sparkAnchors = electricalAnchors;
        ventAnchor = pressureVent;
        promptFont = font;

        blackHoldDuration = 0.4f;
        firstVisionTime = 0.72f;
        partialLookTime = 1.3f;
        alarmDelay = 2.2f;
        fullAwakeTime = 4f;
        muffledCutoff = 480f;
        clearCutoff = 22000f;
        alarmStartVolume = 0.025f;
        alarmClearVolume = 0.42f;
        alarmDeploymentFade = 1.2f;
    }

    private void Awake()
    {
        ResolvePlayerController();
        CreateMalfunctionEffects();
        CreateAwakeningOverlay();
        CreateEmergencyPrompt();
        ApplyCinematicControl(0f);
    }

    private void Start()
    {
        _sequenceElapsed = 0f;
        _nextSparkAt = 0.55f;

        if (alarmLowPass != null)
        {
            alarmLowPass.cutoffFrequency = muffledCutoff;
        }

        if (alarmSource != null)
        {
            alarmSource.Stop();
            alarmSource.volume = 0f;
        }

        if (awakeningSource != null && awakeningSource.clip != null)
        {
            awakeningSource.volume = 1f;
            awakeningSource.Play();
        }
    }

    private void Update()
    {
        if (AILURONEGameplayActionGate.IsPaused)
        {
            PauseDepartureEffects();
            PauseSequenceAudio();
            return;
        }

        ResumeDepartureEffects();
        ResumeSequenceAudio();

        float delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        _sequenceElapsed += delta;
        float elapsed = _sequenceElapsed;
        bool deployed = failure != null && failure.DeploymentStarted;

        if (deployed && _deploymentElapsed < 0f)
        {
            _deploymentElapsed = 0f;
            _deploymentAlarmVolume =
                alarmSource != null ? alarmSource.volume : 0f;
        }
        else if (deployed)
        {
            _deploymentElapsed += delta;
        }

        UpdateAwakeningPresentation(elapsed, deployed);
        UpdateAudio(elapsed, deployed);
        UpdateSparks();

        float impactTime = failure != null ? failure.DoorDelay : 4.2f;

        if (!_impactTriggered && elapsed >= impactTime)
        {
            TriggerDoorImpact();
        }

        float releaseDelay =
            impactTime +
            (failure != null ? failure.DoorOpenDuration * 0.42f : 0.36f);

        if (!_releaseStarted && elapsed >= releaseDelay)
        {
            BeginPressureRelease();
        }

        bool showPrompt =
            !deployed && failure != null && failure.DoorsOpen;

        UpdateEmergencyPrompt(showPrompt);

        if (deployed &&
            _deploymentElapsed >= 2.2f)
        {
            StopSmokeEmission();
        }
    }

    private void UpdateAwakeningPresentation(float elapsed, bool deployed)
    {
        if (deployed)
        {
            RestoreInputControl();
            SetAwakeningOverlayAlpha(0f);
            return;
        }

        ApplyCinematicControl(elapsed);

        if (_awakeningOverlay == null)
        {
            return;
        }

        float initialOpen = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(blackHoldDuration, firstVisionTime, elapsed));
        float blink = CalculateBlink(elapsed, 1.02f, 0.30f);
        float eyeOpen = initialOpen * (1f - blink);
        float eyelidHeight = Mathf.Lerp(540f, 0f, eyeOpen);

        if (_upperEyelid != null)
        {
            _upperEyelid.sizeDelta = new Vector2(0f, eyelidHeight);
        }

        if (_lowerEyelid != null)
        {
            _lowerEyelid.sizeDelta = new Vector2(0f, eyelidHeight);
        }

        if (_blackout != null)
        {
            float baseBlack = 1f - initialOpen * 0.96f;
            Color color = _blackout.color;
            color.a = Mathf.Max(baseBlack, blink);
            _blackout.color = color;
        }

        float recovery = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(firstVisionTime, fullAwakeTime, elapsed));

        if (_vignette != null)
        {
            Color color = _vignette.color;
            color.a = Mathf.Lerp(0.88f, 0f, recovery);
            _vignette.color = color;
        }

        if (_haze != null)
        {
            float hazeRecovery = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(firstVisionTime, 2.65f, elapsed));
            Color color = _haze.color;
            color.a = Mathf.Lerp(0.18f, 0f, hazeRecovery);
            _haze.color = color;
        }

        for (int index = 0; index < _chromaticEdges.Length; index++)
        {
            Image edge = _chromaticEdges[index];

            if (edge == null)
            {
                continue;
            }

            Color color = edge.color;
            color.a = Mathf.Lerp(0.12f, 0f, recovery);
            edge.color = color;
            edge.rectTransform.anchoredPosition = new Vector2(
                Mathf.Sin(elapsed * (8f + index * 1.7f)) * 5f,
                0f);
        }

        float overlayFade =
            1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(fullAwakeTime - 0.25f, fullAwakeTime, elapsed));
        SetAwakeningOverlayAlpha(overlayFade);

        if (elapsed >= fullAwakeTime + 0.1f)
        {
            DestroyAwakeningOverlay();
        }
    }

    private void ApplyCinematicControl(float elapsed)
    {
        if (_playerController == null)
        {
            ResolvePlayerController();
        }

        if (_playerController == null)
        {
            return;
        }

        float lookScale;

        if (elapsed < blackHoldDuration)
        {
            lookScale = 0f;
        }
        else if (elapsed < partialLookTime)
        {
            lookScale = Mathf.Lerp(
                0.03f,
                0.15f,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(blackHoldDuration, partialLookTime, elapsed)));
        }
        else
        {
            lookScale = Mathf.Lerp(
                0.15f,
                1f,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(partialLookTime, fullAwakeTime, elapsed)));
        }

        float settle = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(blackHoldDuration, fullAwakeTime, elapsed));
        float tremor =
            elapsed >= blackHoldDuration && elapsed < fullAwakeTime
                ? Mathf.Sin(elapsed * 9.5f) * (1f - settle)
                : 0f;
        float pitchOffset = Mathf.Lerp(-4.5f, 0f, settle) + tremor * 0.35f;
        float rollOffset = Mathf.Lerp(7.5f, 0f, settle) + tremor * 0.55f;

        float impactTime = failure != null ? failure.DoorDelay : 4.2f;
        float impact = CalculateOneShotEnvelope(elapsed, impactTime, 0.28f);
        pitchOffset -= impact * 1.8f;
        rollOffset += impact * 2.8f;

        bool awake = elapsed >= fullAwakeTime;
        _playerController.SetCinematicInputControl(
            awake ? 1f : 0f,
            lookScale,
            !awake,
            pitchOffset,
            rollOffset);

        _inputRestored = awake && impact <= 0f;
    }

    private void RestoreInputControl()
    {
        if (_playerController != null)
        {
            _playerController.ClearCinematicInputControl();
        }

        _inputRestored = true;
    }

    private void PauseSequenceAudio()
    {
        if (!_awakeningPausedByMenu &&
            awakeningSource != null &&
            awakeningSource.isPlaying)
        {
            awakeningSource.Pause();
            _awakeningPausedByMenu = true;
        }

        if (!_alarmPausedByMenu &&
            alarmSource != null &&
            alarmSource.isPlaying)
        {
            alarmSource.Pause();
            _alarmPausedByMenu = true;
        }
    }

    private void ResumeSequenceAudio()
    {
        if (_awakeningPausedByMenu)
        {
            if (awakeningSource != null)
            {
                awakeningSource.UnPause();
            }

            _awakeningPausedByMenu = false;
        }

        if (_alarmPausedByMenu)
        {
            if (alarmSource != null)
            {
                alarmSource.UnPause();
            }

            _alarmPausedByMenu = false;
        }
    }

    private void UpdateAudio(float elapsed, bool deployed)
    {
        if (alarmSource == null || alarmSource.clip == null)
        {
            return;
        }

        if (deployed)
        {
            float fade = Mathf.Clamp01(
                _deploymentElapsed /
                Mathf.Max(0.1f, alarmDeploymentFade));
            alarmSource.volume = Mathf.Lerp(
                _deploymentAlarmVolume,
                0f,
                Mathf.SmoothStep(0f, 1f, fade));

            if (fade >= 1f && alarmSource.isPlaying)
            {
                alarmSource.Stop();
            }

            return;
        }

        if (elapsed < alarmDelay)
        {
            alarmSource.volume = 0f;
            return;
        }

        if (!alarmSource.isPlaying)
        {
            alarmSource.Play();
        }

        float awareness = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(alarmDelay, fullAwakeTime, elapsed));

        if (alarmLowPass != null)
        {
            alarmLowPass.cutoffFrequency = Mathf.Lerp(
                muffledCutoff,
                clearCutoff,
                awareness);
        }

        alarmSource.volume = Mathf.Lerp(
            alarmStartVolume,
            alarmClearVolume,
            awareness);
    }

    private void UpdateSparks()
    {
        if (_sparkSystems.Length == 0 ||
            (failure != null && failure.DeploymentStarted) ||
            _sequenceElapsed < _nextSparkAt)
        {
            return;
        }

        ParticleSystem sparks =
            _sparkSystems[Random.Range(0, _sparkSystems.Length)];

        if (sparks != null)
        {
            sparks.Emit(Random.Range(12, 27));
        }

        _nextSparkAt =
            _sequenceElapsed + Random.Range(0.42f, 1.35f);
    }

    private void TriggerDoorImpact()
    {
        _impactTriggered = true;

        for (int index = 0; index < _sparkSystems.Length; index++)
        {
            if (_sparkSystems[index] != null)
            {
                _sparkSystems[index].Emit(34);
            }
        }
    }

    private void BeginPressureRelease()
    {
        _releaseStarted = true;

        for (int index = 0; index < _pressureJets.Length; index++)
        {
            if (_pressureJets[index] != null)
            {
                _pressureJets[index].Emit(76);
            }
        }

        for (int index = 0; index < _leakSmoke.Length; index++)
        {
            if (_leakSmoke[index] != null)
            {
                _leakSmoke[index].Play();
            }
        }

        if (_thresholdCloud != null)
        {
            _thresholdCloud.Emit(62);
        }
    }

    private void StopSmokeEmission()
    {
        for (int index = 0; index < _leakSmoke.Length; index++)
        {
            ParticleSystem smoke = _leakSmoke[index];

            if (smoke != null && smoke.isPlaying)
            {
                smoke.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    public void BeginDepartureEffects(Transform podVisualRoot)
    {
        if (_departureEffectsActive || podVisualRoot == null)
        {
            return;
        }

        Transform trailAnchor = CreateEffectAnchor(
            podVisualRoot,
            "EscapePod_DepartureTrailAnchor",
            new Vector3(1.35f, 0.55f, -3.65f),
            Vector3.back);

        _departureSmoke = CreateDepartureSmoke(trailAnchor);
        _departureFire = CreateDepartureFire(trailAnchor);
        _departureSmoke.Play();
        _departureFire.Play();
        _departureEffectsActive = true;

        for (int index = 0; index < _sparkSystems.Length; index++)
        {
            if (_sparkSystems[index] != null)
            {
                _sparkSystems[index].Emit(18);
            }
        }
    }

    public void CompleteDepartureEffects(Vector3 impactPosition)
    {
        _departureEffectsActive = false;
        StopEmitting(_departureSmoke);
        StopEmitting(_departureFire);

        GameObject impactRoot = new GameObject(
            "EscapePod_DistantImpactFX");
        impactRoot.transform.position = impactPosition;

        ParticleSystem flash = CreateImpactFlash(impactRoot.transform);
        ParticleSystem sparks = CreateImpactSparks(impactRoot.transform);
        ParticleSystem smoke = CreateImpactSmoke(impactRoot.transform);
        _impactSystems = new[] { flash, sparks, smoke };

        flash.Emit(9);
        sparks.Emit(82);
        smoke.Emit(48);
        Destroy(impactRoot, 9f);
    }

    private ParticleSystem CreateDepartureSmoke(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem(
            parent,
            "EscapePod_DepartureSmoke");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.8f);
        main.maxParticles = 320;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.16f, 0.18f, 0.20f, 0.72f),
            new Color(0.035f, 0.045f, 0.055f, 0.30f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 24f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.28f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.32f;
        noise.scrollSpeed = 0.45f;

        ConfigureSmokeRenderer(particles);
        return particles;
    }

    private ParticleSystem CreateDepartureFire(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem(
            parent,
            "EscapePod_DepartureFire");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.maxParticles = 180;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.94f, 0.42f, 1f),
            new Color(1f, 0.12f, 0.01f, 0.82f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 38f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.12f;

        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.8f;
        renderer.velocityScale = 0.15f;
        renderer.sharedMaterial = _sparkMaterial;
        renderer.sortingOrder = 82;
        return particles;
    }

    private ParticleSystem CreateImpactFlash(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem(
            parent,
            "DistantImpact_Flash");
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.96f, 0.70f, 1f),
            new Color(1f, 0.28f, 0.02f, 0.82f));
        main.maxParticles = 16;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;

        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = _sparkMaterial;
        renderer.sortingOrder = 90;
        return particles;
    }

    private ParticleSystem CreateImpactSparks(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem(
            parent,
            "DistantImpact_Sparks");
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
        main.gravityModifier = 1.1f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.88f, 0.38f, 1f),
            new Color(1f, 0.08f, 0.01f, 1f));
        main.maxParticles = 110;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.2f;

        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.12f;
        renderer.sharedMaterial = _sparkMaterial;
        renderer.sortingOrder = 88;
        return particles;
    }

    private ParticleSystem CreateImpactSmoke(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem(
            parent,
            "DistantImpact_Smoke");
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.17f, 0.18f, 0.19f, 0.78f),
            new Color(0.035f, 0.04f, 0.045f, 0.35f));
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.2f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.28f;
        noise.scrollSpeed = 0.35f;

        ConfigureSmokeRenderer(particles);
        return particles;
    }

    private void PauseDepartureEffects()
    {
        PauseSystem(_departureSmoke);
        PauseSystem(_departureFire);

        for (int index = 0; index < _impactSystems.Length; index++)
        {
            PauseSystem(_impactSystems[index]);
        }
    }

    private void ResumeDepartureEffects()
    {
        ResumeSystem(_departureSmoke);
        ResumeSystem(_departureFire);

        for (int index = 0; index < _impactSystems.Length; index++)
        {
            ResumeSystem(_impactSystems[index]);
        }
    }

    private static void PauseSystem(ParticleSystem particles)
    {
        if (particles != null && particles.isPlaying)
        {
            particles.Pause(true);
        }
    }

    private static void ResumeSystem(ParticleSystem particles)
    {
        if (particles != null && particles.isPaused)
        {
            particles.Play(true);
        }
    }

    private static void StopEmitting(ParticleSystem particles)
    {
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void CreateMalfunctionEffects()
    {
        _sparkMaterial = CreateParticleMaterial(
            "EscapePod_Sparks_Runtime",
            Color.white,
            true);
        _smokeMaterial = CreateParticleMaterial(
            "EscapePod_Steam_Runtime",
            Color.white,
            false);

        _sparkSystems = new ParticleSystem[sparkAnchors.Length];

        for (int index = 0; index < sparkAnchors.Length; index++)
        {
            if (sparkAnchors[index] != null)
            {
                _sparkSystems[index] = CreateSparkSystem(
                    sparkAnchors[index],
                    "MalfunctionSparks_" + index);
            }
        }

        if (ventAnchor == null)
        {
            return;
        }

        Transform left = CreateEffectAnchor(
            ventAnchor,
            "DoorSteamAnchor_Left",
            new Vector3(-1.08f, 0.34f, -1.18f),
            new Vector3(0.58f, 0.12f, -1f));
        Transform right = CreateEffectAnchor(
            ventAnchor,
            "DoorSteamAnchor_Right",
            new Vector3(1.08f, 0.34f, -1.18f),
            new Vector3(-0.58f, 0.12f, -1f));
        Transform cloud = CreateEffectAnchor(
            ventAnchor,
            "DoorSteamAnchor_Threshold",
            new Vector3(0f, 0.16f, -1.55f),
            new Vector3(0f, 0.16f, -1f));

        _pressureJets = new[]
        {
            CreatePressureJet(left, "DoorPressureJet_Left"),
            CreatePressureJet(right, "DoorPressureJet_Right")
        };
        _leakSmoke = new[]
        {
            CreateLeakSmoke(left, "DoorSteam_Left"),
            CreateLeakSmoke(right, "DoorSteam_Right")
        };
        _thresholdCloud = CreateThresholdCloud(cloud);
    }

    private ParticleSystem CreateSparkSystem(
        Transform parent,
        string objectName)
    {
        ParticleSystem particles = CreateParticleSystem(parent, objectName);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.8f, 7.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.052f);
        main.gravityModifier = 1.1f;
        main.maxParticles = 100;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.52f, 1f),
            new Color(1f, 0.18f, 0.015f, 1f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.06f;

        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.4f;
        renderer.velocityScale = 0.09f;
        renderer.sharedMaterial = _sparkMaterial;
        return particles;
    }

    private ParticleSystem CreatePressureJet(
        Transform parent,
        string objectName)
    {
        ParticleSystem particles = CreateParticleSystem(parent, objectName);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 5.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.52f);
        main.maxParticles = 140;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.90f, 0.97f, 1f, 0.62f),
            new Color(0.68f, 0.82f, 0.88f, 0.22f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.2f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 0.55f;

        ConfigureSmokeRenderer(particles);
        return particles;
    }

    private ParticleSystem CreateLeakSmoke(
        Transform parent,
        string objectName)
    {
        ParticleSystem particles = CreateParticleSystem(parent, objectName);
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.25f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.72f);
        main.maxParticles = 160;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.86f, 0.94f, 0.98f, 0.42f),
            new Color(0.50f, 0.62f, 0.68f, 0.10f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 18f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 26f;
        shape.radius = 0.22f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.38f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.32f;

        ConfigureSmokeRenderer(particles);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private ParticleSystem CreateThresholdCloud(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem(
            parent,
            "DoorThresholdSteamCloud");
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.48f, 1.15f);
        main.maxParticles = 100;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.88f, 0.95f, 0.98f, 0.36f),
            new Color(0.54f, 0.64f, 0.68f, 0.08f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.55f, 0.72f, 0.28f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.28f;
        noise.frequency = 0.38f;

        ConfigureSmokeRenderer(particles);
        return particles;
    }

    private void ConfigureSmokeRenderer(ParticleSystem particles)
    {
        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = _smokeMaterial;
        renderer.sortingOrder = 80;
    }

    private static Transform CreateEffectAnchor(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector3 localForward)
    {
        GameObject anchorObject = new GameObject(objectName);
        Transform anchor = anchorObject.transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = localPosition;
        anchor.localRotation = Quaternion.LookRotation(localForward.normalized);
        return anchor;
    }

    private static ParticleSystem CreateParticleSystem(
        Transform parent,
        string objectName)
    {
        GameObject effectObject = new GameObject(
            objectName,
            typeof(ParticleSystem));
        effectObject.transform.SetParent(parent, false);
        return effectObject.GetComponent<ParticleSystem>();
    }

    private void CreateAwakeningOverlay()
    {
        GameObject canvasObject = new GameObject(
            "EscapePod_AwakeningOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _awakeningOverlay = canvasObject.GetComponent<CanvasGroup>();
        _awakeningOverlay.interactable = false;
        _awakeningOverlay.blocksRaycasts = false;
        _awakeningOverlay.alpha = 1f;

        _blackout = CreateImage(canvasObject.transform, "AwakeningBlackout");
        StretchToParent(_blackout.rectTransform);
        _blackout.color = Color.black;

        _haze = CreateImage(canvasObject.transform, "AwakeningHaze");
        StretchToParent(_haze.rectTransform);
        _haze.color = new Color(0.64f, 0.77f, 0.79f, 0.18f);

        _vignetteSprite = CreateVignetteSprite();
        _vignette = CreateImage(canvasObject.transform, "AwakeningVignette");
        StretchToParent(_vignette.rectTransform);
        _vignette.sprite = _vignetteSprite;
        _vignette.color = new Color(0f, 0f, 0f, 0.88f);

        _chromaticEdges = new[]
        {
            CreateChromaticEdge(canvasObject.transform, "AwakeningEdge_Cyan", true),
            CreateChromaticEdge(canvasObject.transform, "AwakeningEdge_Red", false)
        };

        Image upper = CreateImage(canvasObject.transform, "AwakeningEyelid_Upper");
        upper.color = Color.black;
        _upperEyelid = upper.rectTransform;
        _upperEyelid.anchorMin = new Vector2(0f, 1f);
        _upperEyelid.anchorMax = new Vector2(1f, 1f);
        _upperEyelid.pivot = new Vector2(0.5f, 1f);
        _upperEyelid.anchoredPosition = Vector2.zero;

        Image lower = CreateImage(canvasObject.transform, "AwakeningEyelid_Lower");
        lower.color = Color.black;
        _lowerEyelid = lower.rectTransform;
        _lowerEyelid.anchorMin = new Vector2(0f, 0f);
        _lowerEyelid.anchorMax = new Vector2(1f, 0f);
        _lowerEyelid.pivot = new Vector2(0.5f, 0f);
        _lowerEyelid.anchoredPosition = Vector2.zero;
    }

    private Sprite CreateVignetteSprite()
    {
        const int size = 128;
        _vignetteTexture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "EscapePod_AwakeningVignette_Runtime",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs(x / (size - 1f) * 2f - 1f);
                float ny = Mathf.Abs(y / (size - 1f) * 2f - 1f);
                float edge = Mathf.Clamp01(
                    Mathf.Max(nx * 0.92f, ny) - 0.26f);
                float alpha = Mathf.SmoothStep(0f, 1f, edge);
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        _vignetteTexture.SetPixels(pixels);
        _vignetteTexture.Apply(false, true);

        _vignetteSprite = Sprite.Create(
            _vignetteTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        _vignetteSprite.name = "EscapePod_AwakeningVignetteSprite_Runtime";
        _vignetteSprite.hideFlags = HideFlags.DontSave;
        return _vignetteSprite;
    }

    private static Image CreateChromaticEdge(
        Transform parent,
        string objectName,
        bool left)
    {
        Image edge = CreateImage(parent, objectName);
        RectTransform rect = edge.rectTransform;
        rect.anchorMin = new Vector2(left ? 0f : 1f, 0.08f);
        rect.anchorMax = new Vector2(left ? 0f : 1f, 0.92f);
        rect.pivot = new Vector2(left ? 0f : 1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(18f, 0f);
        edge.color = left
            ? new Color(0.08f, 0.92f, 1f, 0.12f)
            : new Color(1f, 0.08f, 0.03f, 0.12f);
        return edge;
    }

    private void CreateEmergencyPrompt()
    {
        GameObject canvasObject = new GameObject(
            "EscapePod_EmergencyPrompt",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _promptGroup = canvasObject.GetComponent<CanvasGroup>();
        _promptGroup.alpha = 0f;
        _promptGroup.interactable = false;
        _promptGroup.blocksRaycasts = false;

        Image panel = CreateImage(canvasObject.transform, "PromptPanel");
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.22f);
        panelRect.anchorMax = new Vector2(0.5f, 0.22f);
        panelRect.sizeDelta = new Vector2(620f, 92f);
        panel.color = new Color(0.018f, 0.012f, 0.015f, 0.74f);

        Image accent = CreateImage(panel.transform, "WarningAccent");
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(8f, 0f);
        accent.color = new Color(1f, 0.075f, 0.035f, 1f);

        CreateLabel(
            panel.transform,
            "EmergencyEgressLabel",
            "EMERGENCY EGRESS",
            28f,
            new Vector2(0f, 12f),
            new Color(1f, 0.18f, 0.10f, 1f),
            7f);
        CreateLabel(
            panel.transform,
            "ManualExitLabel",
            "MANUAL EXIT AVAILABLE",
            14f,
            new Vector2(0f, -24f),
            new Color(0.78f, 0.72f, 0.72f, 1f),
            4f);
    }

    private void UpdateEmergencyPrompt(bool visible)
    {
        if (_promptGroup == null)
        {
            return;
        }

        _promptFade = Mathf.MoveTowards(
            _promptFade,
            visible ? 1f : 0f,
            Time.unscaledDeltaTime * 4.5f);

        float pulse = visible
            ? 0.82f + Mathf.Sin(_sequenceElapsed * 7.5f) * 0.18f
            : 1f;

        _promptGroup.alpha = _promptFade * pulse;
    }

    private void CreateLabel(
        Transform parent,
        string objectName,
        string text,
        float fontSize,
        Vector2 position,
        Color color,
        float spacing)
    {
        GameObject labelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = promptFont != null
            ? promptFont
            : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = spacing;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(580f, 42f);
    }

    private static Image CreateImage(
        Transform parent,
        string objectName)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static float CalculateBlink(
        float elapsed,
        float centre,
        float duration)
    {
        float half = Mathf.Max(0.01f, duration * 0.5f);
        return Mathf.Clamp01(1f - Mathf.Abs(elapsed - centre) / half);
    }

    private static float CalculateOneShotEnvelope(
        float elapsed,
        float start,
        float duration)
    {
        float progress = Mathf.InverseLerp(
            start,
            start + Mathf.Max(0.01f, duration),
            elapsed);
        return elapsed < start || elapsed > start + duration
            ? 0f
            : Mathf.Sin(progress * Mathf.PI);
    }

    private void SetAwakeningOverlayAlpha(float alpha)
    {
        if (_awakeningOverlay != null)
        {
            _awakeningOverlay.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void DestroyAwakeningOverlay()
    {
        if (_awakeningOverlay != null)
        {
            Destroy(_awakeningOverlay.gameObject);
            _awakeningOverlay = null;
        }
    }

    private void ResolvePlayerController()
    {
        FirstPersonController[] controllers =
            Object.FindObjectsByType<FirstPersonController>(
                FindObjectsInactive.Include);
        Scene scene = gameObject.scene;

        for (int index = 0; index < controllers.Length; index++)
        {
            FirstPersonController candidate = controllers[index];

            if (candidate != null && candidate.gameObject.scene == scene)
            {
                _playerController = candidate;
                return;
            }
        }
    }

    private static Material CreateParticleMaterial(
        string materialName,
        Color color,
        bool additive)
    {
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            throw new System.InvalidOperationException(
                "No compatible particle shader was found.");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSave,
            renderQueue = 3000
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", additive ? 2f : 0f);
        }

        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt(
            "_DstBlend",
            additive
                ? (int)BlendMode.One
                : (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private void OnDestroy()
    {
        RestoreInputControl();

        if (_vignetteSprite != null)
        {
            Destroy(_vignetteSprite);
        }

        if (_vignetteTexture != null)
        {
            Destroy(_vignetteTexture);
        }

        if (_sparkMaterial != null)
        {
            Destroy(_sparkMaterial);
        }

        if (_smokeMaterial != null)
        {
            Destroy(_smokeMaterial);
        }
    }
}
