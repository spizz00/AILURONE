#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TeleportController))]
public sealed class TeleportAudioFeedback : MonoBehaviour
{
    [Header("Core Reference")]
    public TeleportController teleportController;

    [Header("Audio Clips")]
    public AudioClip channelStartClip;
    public AudioClip channelLoopClip;
    public AudioClip channelCancelClip;
    public AudioClip departureClip;
    public AudioClip arrivalClip;

    [Header("Audio Sources")]
    [Tooltip("Leave empty to create a 2D loop source at runtime.")]
    public AudioSource loopSource;

    [Tooltip("Leave empty to create a 2D one-shot source at runtime.")]
    public AudioSource oneShotSource;

    [Header("Channel Loop Response")]
    [Range(0f, 1f)] public float minimumLoopVolume = 0.05f;
    [Range(0f, 1f)] public float maximumLoopVolume = 0.34f;
    [Range(-3f, 3f)] public float minimumLoopPitch = 0.78f;
    [Range(-3f, 3f)] public float maximumLoopPitch = 1.22f;
    [Min(0.1f)] public float responseExponent = 1.35f;
    [Min(0.01f)] public float loopFadeInDuration = 0.08f;
    [Min(0.01f)] public float loopFadeOutDuration = 0.1f;

    [Header("One-Shot Volumes")]
    [Range(0f, 1f)] public float channelStartVolume = 0.5f;
    [Range(0f, 1f)] public float channelCancelVolume = 0.42f;
    [Range(0f, 1f)] public float departureVolume = 0.72f;
    [Range(0f, 1f)] public float arrivalVolume = 0.68f;

    [Header("Departure Timing")]
    [Range(0.7f, 1f)]
    [Tooltip("Plays the departure transient shortly before the position swap.")]
    public float departureProgress = 0.9f;

    [Header("Runtime State (read only)")]
    [SerializeField] private bool channeling;
    [SerializeField, Range(0f, 1f)] private float displayedProgress;
    [SerializeField] private float currentLoopVolume;

    private bool _departurePlayed;
    private bool _subscribed;
    private GameObject _runtimeLoopObject;
    private GameObject _runtimeOneShotObject;

    private void Awake()
    {
        ResolveReferences();
        EnsureAudioSources();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureAudioSources();
        Subscribe();
    }

    private void Start()
    {
        ResolveReferences();
        Subscribe();
    }

    private void Update()
    {
        ResolveReferences();
        EnsureAudioSources();
        Subscribe();

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        channeling = teleportController != null &&
            teleportController.IsChanneling;
        displayedProgress = channeling
            ? Mathf.Clamp01(teleportController.ChannelProgress)
            : 0f;

        if (!channeling)
        {
            FadeOutLoop(deltaTime);
            return;
        }

        UpdateChannelLoop(deltaTime);

        if (!_departurePlayed &&
            displayedProgress >= departureProgress)
        {
            _departurePlayed = true;
            StopLoop();
            PlayOneShot(departureClip, departureVolume);
        }
    }

    private void HandleChannelStarted(int slotIndex, Vector3 destination)
    {
        _departurePlayed = false;
        channeling = true;
        displayedProgress = 0f;
        PlayOneShot(channelStartClip, channelStartVolume);

        if (loopSource == null || channelLoopClip == null)
        {
            return;
        }

        loopSource.Stop();
        loopSource.clip = channelLoopClip;
        loopSource.loop = true;
        loopSource.volume = 0f;
        loopSource.pitch = minimumLoopPitch;
        currentLoopVolume = 0f;
        loopSource.Play();
    }

    private void HandleChannelCancelled(
        TeleportController.TeleportCancelReason reason
    )
    {
        channeling = false;
        displayedProgress = 0f;
        _departurePlayed = false;

        if (reason != TeleportController.TeleportCancelReason.ComponentDisabled)
        {
            PlayOneShot(channelCancelClip, channelCancelVolume);
        }
    }

    private void HandleTeleportCompleted(int slotIndex, Vector3 destination)
    {
        channeling = false;
        displayedProgress = 0f;
        _departurePlayed = false;
        StopLoop();
        PlayOneShot(arrivalClip, arrivalVolume);
    }

    private void UpdateChannelLoop(float deltaTime)
    {
        if (loopSource == null || channelLoopClip == null)
        {
            return;
        }

        if (!loopSource.isPlaying)
        {
            loopSource.clip = channelLoopClip;
            loopSource.loop = true;
            loopSource.Play();
        }

        float response = Mathf.Pow(
            displayedProgress,
            Mathf.Max(0.1f, responseExponent)
        );
        float targetVolume = Mathf.Lerp(
            minimumLoopVolume,
            maximumLoopVolume,
            response
        );

        currentLoopVolume = Mathf.MoveTowards(
            currentLoopVolume,
            targetVolume,
            deltaTime / Mathf.Max(0.01f, loopFadeInDuration)
        );

        loopSource.volume = currentLoopVolume;
        loopSource.pitch = Mathf.Lerp(
            minimumLoopPitch,
            maximumLoopPitch,
            Mathf.SmoothStep(0f, 1f, displayedProgress)
        );
    }

    private void FadeOutLoop(float deltaTime)
    {
        if (loopSource == null)
        {
            currentLoopVolume = 0f;
            return;
        }

        currentLoopVolume = Mathf.MoveTowards(
            currentLoopVolume,
            0f,
            deltaTime / Mathf.Max(0.01f, loopFadeOutDuration)
        );
        loopSource.volume = currentLoopVolume;

        if (currentLoopVolume <= 0.001f && loopSource.isPlaying)
        {
            loopSource.Stop();
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (oneShotSource == null || clip == null || volume <= 0f)
        {
            return;
        }

        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void StopLoop()
    {
        currentLoopVolume = 0f;

        if (loopSource == null)
        {
            return;
        }

        loopSource.Stop();
        loopSource.volume = 0f;
    }

    private void ResolveReferences()
    {
        if (teleportController == null)
        {
            teleportController = GetComponent<TeleportController>();
        }
    }

    private void Subscribe()
    {
        if (_subscribed || teleportController == null)
        {
            return;
        }

        teleportController.ChannelStarted += HandleChannelStarted;
        teleportController.ChannelCancelled += HandleChannelCancelled;
        teleportController.TeleportCompleted += HandleTeleportCompleted;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || teleportController == null)
        {
            return;
        }

        teleportController.ChannelStarted -= HandleChannelStarted;
        teleportController.ChannelCancelled -= HandleChannelCancelled;
        teleportController.TeleportCompleted -= HandleTeleportCompleted;
        _subscribed = false;
    }

    private void EnsureAudioSources()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (loopSource == null)
        {
            _runtimeLoopObject = new GameObject("TeleportLoopAudio_Runtime");
            _runtimeLoopObject.transform.SetParent(transform, false);
            loopSource = _runtimeLoopObject.AddComponent<AudioSource>();
            ConfigureRuntimeSource(loopSource);
        }

        if (oneShotSource == null)
        {
            _runtimeOneShotObject = new GameObject("TeleportOneShotAudio_Runtime");
            _runtimeOneShotObject.transform.SetParent(transform, false);
            oneShotSource = _runtimeOneShotObject.AddComponent<AudioSource>();
            ConfigureRuntimeSource(oneShotSource);
        }
    }

    private static void ConfigureRuntimeSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 1f;
        source.pitch = 1f;
    }

    private void OnDisable()
    {
        Unsubscribe();
        channeling = false;
        displayedProgress = 0f;
        _departurePlayed = false;
        StopLoop();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (_runtimeLoopObject != null)
        {
            Destroy(_runtimeLoopObject);
        }

        if (_runtimeOneShotObject != null)
        {
            Destroy(_runtimeOneShotObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        responseExponent = Mathf.Max(0.1f, responseExponent);
        loopFadeInDuration = Mathf.Max(0.01f, loopFadeInDuration);
        loopFadeOutDuration = Mathf.Max(0.01f, loopFadeOutDuration);
        departureProgress = Mathf.Clamp(departureProgress, 0.7f, 1f);
    }
#endif
}
