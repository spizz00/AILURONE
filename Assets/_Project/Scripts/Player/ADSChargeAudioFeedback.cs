#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ADSChargeAudioFeedback : MonoBehaviour
{
    [Header("Core Reference")]
    [Tooltip("Usually the PlayerWeapon component on this object.")]
    public PlayerWeapon weapon;

    [Header("Audio Clips")]
    [Tooltip("Short transient played once when charging begins.")]
    public AudioClip chargeStartClip;

    [Tooltip("Seamless electrical or mechanical loop used during charging.")]
    public AudioClip chargeLoopClip;

    [Tooltip("Short lock-on click played once when charge reaches 100%.")]
    public AudioClip fullChargeClip;

    [Tooltip("Optional mechanical return sound played after a charged shot.")]
    public AudioClip shotReleaseClip;

    [Tooltip("Short energy discharge sound played only when ADS charge is cancelled.")]
    public AudioClip chargeCancelClip;

    [Header("Audio Sources")]
    [Tooltip("Dedicated source for the continuous loop. Leave empty to create one at runtime.")]
    public AudioSource loopSource;

    [Tooltip("Dedicated source for start, full-charge and end one-shots. Leave empty to create one at runtime.")]
    public AudioSource oneShotSource;

    [Header("Loop Response")]
    [Range(0f, 1f)] public float minimumLoopVolume = 0.04f;
    [Range(0f, 1f)] public float maximumLoopVolume = 0.28f;
    [Range(-3f, 3f)] public float minimumLoopPitch = 0.78f;
    [Range(-3f, 3f)] public float maximumLoopPitch = 1.16f;

    [Min(0.1f)]
    [Tooltip("Higher values make the final part of the charge intensify faster.")]
    public float responseExponent = 1.45f;

    [Min(0.01f)] public float loopFadeInDuration = 0.06f;
    [Min(0.01f)] public float loopFadeOutDuration = 0.08f;

    [Header("One-Shot Volumes")]
    [Range(0f, 1f)] public float chargeStartVolume = 0.42f;
    [Range(0f, 1f)] public float fullChargeVolume = 0.58f;
    [Range(0f, 1f)] public float shotReleaseVolume = 0.25f;
    [Range(0f, 1f)] public float chargeCancelVolume = 0.34f;

    [Range(0f, 1f)]
    [Tooltip("Prevents tiny accidental charge taps from playing a cancel sound.")]
    public float minimumCancelCharge01 = 0.08f;

    [Header("Runtime State (read only)")]
    [SerializeField] private bool charging;
    [SerializeField, Range(0f, 1f)] private float displayedCharge01;
    [SerializeField] private float currentLoopVolume;

    private bool _wasCharging;
    private bool _wasFullyCharged;
    private float _lastChargingCharge01;
    private GameObject _runtimeLoopObject;
    private GameObject _runtimeOneShotObject;

    private void Awake()
    {
        ResolveReferences();
        EnsureAudioSources();
    }

    private void Update()
    {
        ResolveReferences();
        EnsureAudioSources();

        if (weapon == null)
        {
            FadeOutLoop(Mathf.Max(0f, Time.unscaledDeltaTime));
            return;
        }

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        charging = weapon.IsAdsCharging;
        displayedCharge01 = charging
            ? Mathf.Clamp01(weapon.AdsCharge01)
            : 0f;

        if (charging && !_wasCharging)
        {
            BeginChargeAudio();
        }

        if (!charging && _wasCharging)
        {
            EndChargeAudio(_lastChargingCharge01);
        }

        bool fullyCharged = charging && weapon.IsAdsChargeReady;
        if (fullyCharged && !_wasFullyCharged)
        {
            PlayOneShot(fullChargeClip, fullChargeVolume);
        }

        if (charging)
        {
            UpdateChargingLoop(deltaTime);
            _lastChargingCharge01 = displayedCharge01;
        }
        else
        {
            FadeOutLoop(deltaTime);
            _lastChargingCharge01 = 0f;
        }

        _wasCharging = charging;
        _wasFullyCharged = fullyCharged;
    }

    private void BeginChargeAudio()
    {
        PlayOneShot(chargeStartClip, chargeStartVolume);

        if (loopSource == null || chargeLoopClip == null)
        {
            return;
        }

        loopSource.Stop();
        loopSource.clip = chargeLoopClip;
        loopSource.loop = true;
        loopSource.pitch = minimumLoopPitch;
        loopSource.volume = 0f;
        currentLoopVolume = 0f;
        loopSource.Play();
    }

    private void EndChargeAudio(float endingCharge01)
    {
        if (weapon.IsAiming)
        {
            PlayOneShot(shotReleaseClip, shotReleaseVolume);
            return;
        }

        if (endingCharge01 >= minimumCancelCharge01)
        {
            PlayOneShot(chargeCancelClip, chargeCancelVolume);
        }
    }

    private void UpdateChargingLoop(float deltaTime)
    {
        if (loopSource == null || chargeLoopClip == null)
        {
            return;
        }

        if (!loopSource.isPlaying)
        {
            loopSource.clip = chargeLoopClip;
            loopSource.loop = true;
            loopSource.Play();
        }

        float response = Mathf.Pow(
            displayedCharge01,
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
            Mathf.SmoothStep(0f, 1f, displayedCharge01)
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

    private void ResolveReferences()
    {
        if (weapon == null)
        {
            weapon = GetComponent<PlayerWeapon>();
        }
    }

    private void EnsureAudioSources()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (loopSource == null)
        {
            _runtimeLoopObject = new GameObject("ADSChargeLoopAudio_Runtime");
            _runtimeLoopObject.transform.SetParent(transform, false);
            loopSource = _runtimeLoopObject.AddComponent<AudioSource>();
            ConfigureRuntimeSource(loopSource);
        }

        if (oneShotSource == null)
        {
            _runtimeOneShotObject = new GameObject("ADSChargeOneShotAudio_Runtime");
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
        charging = false;
        displayedCharge01 = 0f;
        currentLoopVolume = 0f;
        _wasCharging = false;
        _wasFullyCharged = false;
        _lastChargingCharge01 = 0f;

        if (loopSource != null)
        {
            loopSource.Stop();
            loopSource.volume = 0f;
            loopSource.pitch = minimumLoopPitch;
        }
    }

    private void OnDestroy()
    {
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
    }
#endif
}
