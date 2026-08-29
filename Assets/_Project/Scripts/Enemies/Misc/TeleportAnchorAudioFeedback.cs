#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TeleportAnchorSystem))]
public sealed class TeleportAnchorAudioFeedback : MonoBehaviour
{
    private const int MaximumAnchorCount = 3;

    private sealed class AnchorAudio
    {
        public GameObject root;
        public AudioSource loopSource;
        public AudioSource oneShotSource;
        public float revealTime;
        public float expireTime;
        public float nextWarningTime;
        public bool revealed;
    }

    [Header("Core Reference")]
    public TeleportAnchorSystem anchorSystem;

    [Header("Audio Clips")]
    public AudioClip spawnClip;
    public AudioClip idleLoopClip;
    public AudioClip warningClip;
    public AudioClip expireClip;

    [Header("Volumes")]
    [Range(0f, 1f)] public float spawnVolume = 0.42f;
    [Range(0f, 1f)] public float idleLoopVolume = 0.055f;
    [Range(0f, 1f)] public float warningMinimumVolume = 0.18f;
    [Range(0f, 1f)] public float warningMaximumVolume = 0.28f;
    [Range(0f, 1f)] public float expireVolume = 0.34f;

    [Header("Expiry Warning")]
    [Min(0.1f)] public float warningDuration = 3f;
    [Min(0.05f)] public float warningMaximumInterval = 0.72f;
    [Min(0.05f)] public float warningMinimumInterval = 0.22f;
    [Range(0.1f, 3f)] public float warningMinimumPitch = 0.92f;
    [Range(0.1f, 3f)] public float warningMaximumPitch = 1.22f;

    [Header("Spatial Audio")]
    [Min(0f)] public float minimumDistance = 3f;
    [Min(0.1f)] public float maximumDistance = 36f;
    [Min(10f)] public float loopHighPassCutoff = 160f;

    private readonly AnchorAudio[] _anchors =
        new AnchorAudio[MaximumAnchorCount];

    private bool _subscribed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
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
        Subscribe();

        float now = Time.unscaledTime;

        for (int i = 0; i < _anchors.Length; i++)
        {
            AnchorAudio anchor = _anchors[i];

            if (anchor == null)
            {
                continue;
            }

            if (!anchor.revealed && now >= anchor.revealTime)
            {
                RevealAnchorAudio(anchor);
            }

            if (!anchor.revealed)
            {
                continue;
            }

            UpdateWarning(anchor, now);
        }
    }

    private void HandleAnchorCreated(
        int slotIndex,
        Vector3 position,
        float revealDelay,
        float lifetime
    )
    {
        if (!IsValidSlot(slotIndex))
        {
            return;
        }

        ReleaseSlot(slotIndex);

        GameObject root =
            new GameObject($"TeleportAnchorAudio_{slotIndex}");
        root.transform.position = position;

        GameObject loopObject = new GameObject("IdleLoop");
        loopObject.transform.SetParent(root.transform, false);
        AudioSource loopSource = loopObject.AddComponent<AudioSource>();
        ConfigureSource(loopSource);
        loopSource.loop = true;
        loopSource.clip = idleLoopClip;
        loopSource.volume = Mathf.Clamp01(idleLoopVolume);

        AudioHighPassFilter highPass =
            loopObject.AddComponent<AudioHighPassFilter>();
        highPass.cutoffFrequency =
            Mathf.Max(10f, loopHighPassCutoff);

        GameObject oneShotObject = new GameObject("OneShots");
        oneShotObject.transform.SetParent(root.transform, false);
        AudioSource oneShotSource =
            oneShotObject.AddComponent<AudioSource>();
        ConfigureSource(oneShotSource);

        float now = Time.unscaledTime;
        float safeRevealDelay = Mathf.Max(0f, revealDelay);
        float revealTime = now + safeRevealDelay;
        float expireTime = revealTime + Mathf.Max(0.5f, lifetime);

        _anchors[slotIndex] = new AnchorAudio
        {
            root = root,
            loopSource = loopSource,
            oneShotSource = oneShotSource,
            revealTime = revealTime,
            expireTime = expireTime,
            nextWarningTime = Mathf.Max(
                revealTime,
                expireTime - Mathf.Max(0.1f, warningDuration)
            )
        };
    }

    private void HandleAnchorRemoved(
        int slotIndex,
        Vector3 position,
        bool expiredNaturally
    )
    {
        if (!IsValidSlot(slotIndex))
        {
            return;
        }

        ReleaseSlot(slotIndex);

        if (expiredNaturally)
        {
            PlayTransientAt(position, expireClip, expireVolume);
        }
    }

    private void RevealAnchorAudio(AnchorAudio anchor)
    {
        anchor.revealed = true;

        if (anchor.oneShotSource != null && spawnClip != null)
        {
            anchor.oneShotSource.pitch = 1f;
            anchor.oneShotSource.PlayOneShot(
                spawnClip,
                Mathf.Clamp01(spawnVolume)
            );
        }

        if (anchor.loopSource != null && idleLoopClip != null)
        {
            anchor.loopSource.Play();
        }
    }

    private void UpdateWarning(AnchorAudio anchor, float now)
    {
        if (warningClip == null || anchor.oneShotSource == null)
        {
            return;
        }

        float safeDuration = Mathf.Max(0.1f, warningDuration);
        float remaining = anchor.expireTime - now;

        if (remaining > safeDuration || now < anchor.nextWarningTime)
        {
            return;
        }

        float urgency = 1f - Mathf.Clamp01(remaining / safeDuration);
        anchor.oneShotSource.pitch = Mathf.Lerp(
            warningMinimumPitch,
            warningMaximumPitch,
            urgency
        );
        anchor.oneShotSource.PlayOneShot(
            warningClip,
            Mathf.Lerp(
                warningMinimumVolume,
                warningMaximumVolume,
                urgency
            )
        );

        anchor.nextWarningTime = now + Mathf.Lerp(
            warningMaximumInterval,
            warningMinimumInterval,
            urgency
        );
    }

    private void ReleaseSlot(int slotIndex)
    {
        AnchorAudio anchor = _anchors[slotIndex];
        _anchors[slotIndex] = null;

        if (anchor != null && anchor.root != null)
        {
            Destroy(anchor.root);
        }
    }

    private void PlayTransientAt(
        Vector3 position,
        AudioClip clip,
        float volume
    )
    {
        if (clip == null || volume <= 0f)
        {
            return;
        }

        GameObject root = new GameObject("TeleportAnchorExpireAudio");
        root.transform.position = position;

        AudioSource source = root.AddComponent<AudioSource>();
        ConfigureSource(source);
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();

        Destroy(root, clip.length + 0.25f);
    }

    private void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.minDistance = Mathf.Max(0f, minimumDistance);
        source.maxDistance = Mathf.Max(
            source.minDistance + 0.1f,
            maximumDistance
        );
        source.SetCustomCurve(
            AudioSourceCurveType.CustomRolloff,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.35f, 0.45f),
                new Keyframe(1f, 0f)
            )
        );
        source.volume = 1f;
        source.pitch = 1f;
    }

    private void ResolveReferences()
    {
        if (anchorSystem == null)
        {
            anchorSystem = GetComponent<TeleportAnchorSystem>();
        }
    }

    private void Subscribe()
    {
        if (_subscribed || anchorSystem == null)
        {
            return;
        }

        anchorSystem.AnchorCreated += HandleAnchorCreated;
        anchorSystem.AnchorRemoved += HandleAnchorRemoved;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || anchorSystem == null)
        {
            return;
        }

        anchorSystem.AnchorCreated -= HandleAnchorCreated;
        anchorSystem.AnchorRemoved -= HandleAnchorRemoved;
        _subscribed = false;
    }

    private static bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < MaximumAnchorCount;
    }

    private void OnDisable()
    {
        Unsubscribe();

        for (int i = 0; i < _anchors.Length; i++)
        {
            ReleaseSlot(i);
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        warningDuration = Mathf.Max(0.1f, warningDuration);
        warningMaximumInterval = Mathf.Max(0.05f, warningMaximumInterval);
        warningMinimumInterval = Mathf.Clamp(
            warningMinimumInterval,
            0.05f,
            warningMaximumInterval
        );
        maximumDistance = Mathf.Max(minimumDistance + 0.1f, maximumDistance);
        loopHighPassCutoff = Mathf.Max(10f, loopHighPassCutoff);
    }
#endif
}
