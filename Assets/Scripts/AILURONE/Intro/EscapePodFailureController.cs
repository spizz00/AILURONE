using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EscapePodFailureController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject doorCollision;
    [SerializeField] private Light[] alarmLights = System.Array.Empty<Light>();
    [SerializeField] private Transform player;
    [SerializeField] private LevelEntrySequenceController entrySequence;
    [SerializeField] private Transform exitDirection;

    [Header("Failure Timing")]
    [SerializeField] private float doorDelay = 4.2f;
    [SerializeField] private float doorOpenDuration = 0.85f;
    [SerializeField] private float forcedEvacuationDelay = 9f;
    [SerializeField] private float doorBoneOpenDistance = 72f;

    [Header("Alarm Lighting")]
    [SerializeField] private Color alarmColor =
        new Color(1f, 0.025f, 0.015f);
    [SerializeField] private Color deployedLightColor =
        new Color(0.25f, 1f, 0.92f);
    [SerializeField, Min(0f)] private float alarmMinimumIntensity = 0.2f;
    [SerializeField, Min(0f)] private float alarmMaximumIntensity = 10f;
    [SerializeField, Min(0f)] private float deployedLightIntensity = 1.6f;
    [SerializeField, Min(0f)] private float alarmRampDelay = 2.2f;
    [SerializeField, Min(0.1f)] private float alarmRampDuration = 1.8f;

    [Header("Post-Deployment Departure")]
    [SerializeField, Min(0f)] private float departureDelay = 0.55f;
    [SerializeField, Min(0.1f)] private float breakawayDuration = 1.15f;
    [SerializeField, Min(0.1f)] private float fallDuration = 3.1f;
    [SerializeField] private Vector3 breakawayLocalOffset =
        new Vector3(-18f, 3f, -5f);
    [SerializeField] private Vector3 impactLocalOffset =
        new Vector3(-92f, -120f, 28f);
    [SerializeField] private AudioClip distantImpactSound;
    [SerializeField, Range(0f, 1f)] private float distantImpactVolume = 0.9f;
    [SerializeField, Range(0.25f, 1.5f)] private float distantImpactPitch = 0.68f;
    [SerializeField, Min(0f)] private float distantImpactSoundDelay = 0.65f;

    private Transform _outerDoorUp;
    private Transform _outerDoorDown;
    private Transform _innerDoorUp;
    private Transform _innerDoorDown;
    private Vector3 _outerDoorUpClosed;
    private Vector3 _outerDoorDownClosed;
    private Vector3 _innerDoorUpClosed;
    private Vector3 _innerDoorDownClosed;
    private Vector3 _visualBasePosition;
    private Quaternion _visualBaseRotation;
    private float _failureElapsed;
    private float _doorsOpenElapsed;
    private bool _doorsOpen;
    private bool _forcedEvacuationUsed;
    private bool _deploymentStarted;
    private bool _departureTriggered;
    private bool _departureStarted;
    private bool _departureCompleted;
    private AudioSource _impactAudioSource;
    private bool _impactAudioPausedByMenu;

    public bool DoorsOpen => _doorsOpen;
    public bool DeploymentStarted => _deploymentStarted;
    public float DoorDelay => doorDelay;
    public float DoorOpenDuration => doorOpenDuration;
    public bool DepartureStarted => _departureStarted;
    public bool DepartureCompleted => _departureCompleted;
    public AudioClip DistantImpactSound => distantImpactSound;

    public void Configure(
        Transform podVisualRoot,
        GameObject collisionAtDoor,
        Light[] failureLights,
        Transform playerTransform,
        LevelEntrySequenceController sequence,
        Transform exitTransform)
    {
        visualRoot = podVisualRoot;
        doorCollision = collisionAtDoor;
        alarmLights = failureLights;
        player = playerTransform;
        entrySequence = sequence;
        exitDirection = exitTransform;
    }

    public void ConfigureTiming(
        float openDelay,
        float openDuration,
        float forcedDelayAfterOpen)
    {
        doorDelay = Mathf.Max(0f, openDelay);
        doorOpenDuration = Mathf.Max(0.1f, openDuration);
        forcedEvacuationDelay = Mathf.Max(0f, forcedDelayAfterOpen);
    }

    public void ConfigureDeparture(AudioClip impactSound)
    {
        distantImpactSound = impactSound;
        departureDelay = 0.55f;
        breakawayDuration = 1.15f;
        fallDuration = 3.1f;
        breakawayLocalOffset = new Vector3(-18f, 3f, -5f);
        impactLocalOffset = new Vector3(-92f, -120f, 28f);
        distantImpactVolume = 0.9f;
        distantImpactPitch = 0.68f;
        distantImpactSoundDelay = 0.65f;
    }

    private void Start()
    {
        _failureElapsed = 0f;

        if (visualRoot != null)
        {
            _visualBasePosition = visualRoot.localPosition;
            _visualBaseRotation = visualRoot.localRotation;
        }

        ResolveDoorBones();
        StartCoroutine(OpenDoorsAfterDelay());
    }

    private void Update()
    {
        UpdateImpactAudioPauseState();

        if (AILURONEGameplayActionGate.IsPaused)
        {
            return;
        }

        float delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        _failureElapsed += delta;

        if (_doorsOpen && !_deploymentStarted)
        {
            _doorsOpenElapsed += delta;
        }

        UpdateFailureShake();
        UpdateAlarmLights();

        if (_deploymentStarted ||
            _forcedEvacuationUsed ||
            !_doorsOpen ||
            _doorsOpenElapsed < forcedEvacuationDelay)
        {
            return;
        }

        ForceEvacuation();
    }

    public void NotifyDeploymentStarted()
    {
        // A Level scene or Play Mode transition can disable this object before
        // LevelEntrySequenceController performs its final safety handoff.
        // Coroutines cannot run on an inactive MonoBehaviour, and an inactive
        // escape pod has no departure presentation left to play.
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (_deploymentStarted)
        {
            return;
        }

        _deploymentStarted = true;

        if (!_departureTriggered)
        {
            _departureTriggered = true;
            StartCoroutine(DepartureRoutine());
        }
    }

    private IEnumerator DepartureRoutine()
    {
        yield return WaitForUnpausedSeconds(departureDelay);

        _departureStarted = true;
        DisablePodColliders();

        EscapePodMalfunctionPresentation presentation =
            GetComponent<EscapePodMalfunctionPresentation>();

        if (presentation != null)
        {
            presentation.BeginDepartureEffects(visualRoot);
        }

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 breakawayPosition =
            startPosition + startRotation * breakawayLocalOffset;
        Vector3 impactPosition = ResolveImpactPosition(
            startPosition,
            startRotation);
        Quaternion breakawayRotation =
            startRotation * Quaternion.Euler(8f, -12f, 32f);

        float elapsed = 0f;
        float safeBreakawayDuration = Mathf.Max(0.1f, breakawayDuration);

        while (elapsed < safeBreakawayDuration)
        {
            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float progress = Mathf.Clamp01(elapsed / safeBreakawayDuration);
            float eased = SmoothStep(progress);
            Vector3 lift =
                startRotation * Vector3.up *
                (Mathf.Sin(progress * Mathf.PI) * 1.25f);

            transform.position =
                Vector3.Lerp(startPosition, breakawayPosition, eased) + lift;
            transform.rotation =
                Quaternion.Slerp(startRotation, breakawayRotation, eased);
            yield return null;
        }

        Vector3 controlOne =
            breakawayPosition +
            startRotation * new Vector3(-16f, -6f, 8f);
        Vector3 controlTwo =
            impactPosition +
            startRotation * new Vector3(22f, 24f, -10f);
        float safeFallDuration = Mathf.Max(0.1f, fallDuration);
        elapsed = 0f;

        while (elapsed < safeFallDuration)
        {
            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float progress = Mathf.Clamp01(elapsed / safeFallDuration);
            float eased = progress * progress;

            transform.position = CubicBezier(
                breakawayPosition,
                controlOne,
                controlTwo,
                impactPosition,
                eased);

            transform.rotation =
                startRotation * Quaternion.Euler(
                    Mathf.Lerp(8f, 82f, eased),
                    Mathf.Lerp(-12f, -48f, eased),
                    32f + eased * 520f);
            yield return null;
        }

        transform.position = impactPosition;
        CompleteDeparture(presentation, impactPosition);
    }

    private void CompleteDeparture(
        EscapePodMalfunctionPresentation presentation,
        Vector3 impactPosition)
    {
        _departureCompleted = true;
        HidePodModel();

        for (int index = 0; index < alarmLights.Length; index++)
        {
            if (alarmLights[index] != null)
            {
                alarmLights[index].enabled = false;
            }
        }

        if (presentation != null)
        {
            presentation.CompleteDepartureEffects(impactPosition);
        }

        if (VisualFeedbackController.Instance != null)
        {
            VisualFeedbackController.Instance
                .TriggerDistantImpactFeedback(0.55f);
        }

        StartCoroutine(PlayDistantImpactSound(impactPosition));
        StartCoroutine(DeactivateAfterDeparture());
    }

    private IEnumerator PlayDistantImpactSound(Vector3 impactPosition)
    {
        yield return WaitForUnpausedSeconds(distantImpactSoundDelay);

        if (distantImpactSound == null)
        {
            yield break;
        }

        GameObject audioObject = new GameObject(
            "EscapePod_DistantImpactAudio",
            typeof(AudioSource),
            typeof(AudioLowPassFilter));
        audioObject.transform.position = impactPosition;

        _impactAudioSource = audioObject.GetComponent<AudioSource>();
        _impactAudioSource.clip = distantImpactSound;
        _impactAudioSource.playOnAwake = false;
        _impactAudioSource.loop = false;
        _impactAudioSource.volume = distantImpactVolume;
        _impactAudioSource.pitch = distantImpactPitch;
        _impactAudioSource.spatialBlend = 1f;
        _impactAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _impactAudioSource.minDistance = 28f;
        _impactAudioSource.maxDistance = 260f;
        _impactAudioSource.dopplerLevel = 0f;
        _impactAudioSource.priority = 70;

        AudioLowPassFilter lowPass =
            audioObject.GetComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 4800f;
        lowPass.lowpassResonanceQ = 1.1f;

        _impactAudioSource.Play();

        float cleanupDelay = Mathf.Max(
            4f,
            distantImpactSound.length /
            Mathf.Max(0.1f, distantImpactPitch) + 1f);
        Destroy(audioObject, cleanupDelay);
    }

    private IEnumerator DeactivateAfterDeparture()
    {
        yield return WaitForUnpausedSeconds(5.5f);
        gameObject.SetActive(false);
    }

    private IEnumerator WaitForUnpausedSeconds(float duration)
    {
        float elapsed = 0f;

        while (elapsed < Mathf.Max(0f, duration))
        {
            if (!AILURONEGameplayActionGate.IsPaused)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            }

            yield return null;
        }
    }

    private void DisablePodColliders()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        for (int index = 0; index < colliders.Length; index++)
        {
            colliders[index].enabled = false;
        }
    }

    private void HidePodModel()
    {
        if (visualRoot == null)
        {
            return;
        }

        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<Renderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            if (!(renderers[index] is ParticleSystemRenderer))
            {
                renderers[index].enabled = false;
            }
        }
    }

    private void UpdateImpactAudioPauseState()
    {
        if (_impactAudioSource == null)
        {
            _impactAudioPausedByMenu = false;
            return;
        }

        if (AILURONEGameplayActionGate.IsPaused)
        {
            if (!_impactAudioPausedByMenu && _impactAudioSource.isPlaying)
            {
                _impactAudioSource.Pause();
                _impactAudioPausedByMenu = true;
            }

            return;
        }

        if (_impactAudioPausedByMenu)
        {
            _impactAudioSource.UnPause();
            _impactAudioPausedByMenu = false;
        }
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private Vector3 ResolveImpactPosition(
        Vector3 startPosition,
        Quaternion startRotation)
    {
        Vector3 horizontalOffset = new Vector3(
            impactLocalOffset.x,
            0f,
            impactLocalOffset.z);
        Vector3 horizontalTarget =
            startPosition + startRotation * horizontalOffset;
        Vector3 rayOrigin = horizontalTarget + Vector3.up * 160f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                360f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) &&
            hit.collider != null &&
            !hit.collider.transform.IsChildOf(transform))
        {
            return hit.point + Vector3.up * 0.6f;
        }

        return startPosition + startRotation * impactLocalOffset;
    }

    private static Vector3 CubicBezier(
        Vector3 pointZero,
        Vector3 pointOne,
        Vector3 pointTwo,
        Vector3 pointThree,
        float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;

        return
            inverse * inverse * inverse * pointZero +
            3f * inverse * inverse * t * pointOne +
            3f * inverse * t * t * pointTwo +
            t * t * t * pointThree;
    }

    private IEnumerator OpenDoorsAfterDelay()
    {
        while (_failureElapsed < Mathf.Max(0f, doorDelay))
        {
            yield return null;
        }

        float duration = Mathf.Max(0.1f, doorOpenDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            SetDoorProgress(eased);

            if (doorCollision != null && progress >= 0.58f)
            {
                doorCollision.SetActive(false);
            }

            yield return null;
        }

        while (AILURONEGameplayActionGate.IsPaused)
        {
            yield return null;
        }

        SetDoorProgress(1f);

        if (doorCollision != null)
        {
            doorCollision.SetActive(false);
        }

        _doorsOpen = true;
        _doorsOpenElapsed = 0f;
    }

    private void ResolveDoorBones()
    {
        if (visualRoot == null)
        {
            return;
        }

        Transform[] transforms =
            visualRoot.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];

            switch (candidate.name)
            {
                case "OuterDoorUp":
                    _outerDoorUp = candidate;
                    _outerDoorUpClosed = candidate.localPosition;
                    break;
                case "OuterDoorDown":
                    _outerDoorDown = candidate;
                    _outerDoorDownClosed = candidate.localPosition;
                    break;
                case "InnerDoorUp":
                    _innerDoorUp = candidate;
                    _innerDoorUpClosed = candidate.localPosition;
                    break;
                case "InnerDoorDown":
                    _innerDoorDown = candidate;
                    _innerDoorDownClosed = candidate.localPosition;
                    break;
            }
        }
    }

    private void SetDoorProgress(float progress)
    {
        float distance = doorBoneOpenDistance * Mathf.Clamp01(progress);
        Vector3 offset = Vector3.forward * distance;

        if (_outerDoorUp != null)
        {
            _outerDoorUp.localPosition = _outerDoorUpClosed + offset;
        }

        if (_innerDoorUp != null)
        {
            _innerDoorUp.localPosition = _innerDoorUpClosed + offset;
        }

        if (_outerDoorDown != null)
        {
            _outerDoorDown.localPosition = _outerDoorDownClosed - offset;
        }

        if (_innerDoorDown != null)
        {
            _innerDoorDown.localPosition = _innerDoorDownClosed - offset;
        }
    }

    private void UpdateFailureShake()
    {
        if (visualRoot == null)
        {
            return;
        }

        float strength =
            _departureStarted
                ? 0.018f
                : (_deploymentStarted ? 0.002f : 0.012f);
        float time = _failureElapsed;

        Vector3 positionNoise = new Vector3(
            Mathf.PerlinNoise(time * 18f, 0.1f) - 0.5f,
            Mathf.PerlinNoise(0.2f, time * 21f) - 0.5f,
            Mathf.PerlinNoise(time * 16f, 0.3f) - 0.5f);

        visualRoot.localPosition =
            _visualBasePosition + positionNoise * strength;

        float roll =
            (Mathf.PerlinNoise(time * 13f, 0.7f) - 0.5f) *
            strength * 120f;

        visualRoot.localRotation =
            _visualBaseRotation * Quaternion.Euler(0f, 0f, roll);
    }

    private void UpdateAlarmLights()
    {
        float time = _failureElapsed;
        float elapsed = _failureElapsed;
        float alarmStrength = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(
                alarmRampDelay,
                alarmRampDelay + Mathf.Max(0.1f, alarmRampDuration),
                elapsed));
        float pulse = Mathf.Clamp01(
            Mathf.Sin(time * 10.5f) * 0.55f +
            Mathf.Sin(time * 23f) * 0.25f +
            0.45f);

        for (int index = 0; index < alarmLights.Length; index++)
        {
            Light light = alarmLights[index];

            if (light == null)
            {
                continue;
            }

            if (_departureStarted)
            {
                light.color = Color.Lerp(
                    alarmColor,
                    new Color(1f, 0.32f, 0.04f),
                    pulse);
                light.intensity = Mathf.Lerp(2.5f, 8f, pulse);
            }
            else if (_deploymentStarted)
            {
                light.color = deployedLightColor;
                light.intensity = deployedLightIntensity;
            }
            else
            {
                light.color = alarmColor;
                light.intensity = Mathf.Lerp(
                    alarmMinimumIntensity,
                    Mathf.Lerp(
                        alarmMinimumIntensity,
                        Mathf.Max(alarmMinimumIntensity, alarmMaximumIntensity),
                        alarmStrength),
                    pulse);
            }
        }
    }

    private void ForceEvacuation()
    {
        _forcedEvacuationUsed = true;

        if (player == null)
        {
            return;
        }

        StarterAssets.FirstPersonController controller =
            player.GetComponent<StarterAssets.FirstPersonController>();

        if (controller == null)
        {
            return;
        }

        Vector3 forward =
            exitDirection != null
                ? exitDirection.forward
                : transform.forward;

        controller.ApplyJumpPadForce(forward * 11f + Vector3.up * 2.5f);
    }
}
