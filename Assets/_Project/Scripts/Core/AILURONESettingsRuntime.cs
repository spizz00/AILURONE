using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Persistent audio router. It assigns ungrouped AudioSources to the settings
/// mixer, including sources created later at runtime by weapons and enemies.
/// </summary>
public sealed class AILURONESettingsRuntime : MonoBehaviour
{
    private const string MixerResourcePath =
        "AILURONE/Audio/AILURONE_SettingsMixer";
    private const float SourceScanInterval = 0.5f;

    private static readonly string[] MusicTokens =
    {
        "music",
        "bgm",
        "soundtrack",
        "theme",
        "mus_"
    };

    private static float _masterVolume = 1f;
    private static float _musicVolume = 0.3f;
    private static float _sfxVolume = 1f;

    private AudioMixer _mixer;
    private AudioMixerGroup _musicGroup;
    private AudioMixerGroup _sfxGroup;
    private float _nextSourceScan;
    private readonly HashSet<int> _routedSourceIds = new HashSet<int>();

    public static AILURONESettingsRuntime Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveMixer();
        AILURONEGameSettings.SettingsSnapshot saved =
            AILURONEGameSettings.SavedSnapshot;
        ApplyAudioLevels(
            saved.masterVolume,
            saved.musicVolume,
            saved.sfxVolume);
        RouteSceneAudioSources();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextSourceScan)
        {
            return;
        }

        _nextSourceScan = Time.unscaledTime + SourceScanInterval;
        RouteSceneAudioSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ApplyAudioLevels(float master, float music, float sfx)
    {
        _masterVolume = Mathf.Clamp01(master);
        _musicVolume = Mathf.Clamp01(music);
        _sfxVolume = Mathf.Clamp01(sfx);

        if (Instance == null)
        {
            AudioListener.volume = _masterVolume;
            return;
        }

        Instance.ApplyMixerLevels();
    }

    private void ResolveMixer()
    {
        _mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        if (_mixer == null)
        {
            return;
        }

        AudioMixerGroup[] musicGroups = _mixer.FindMatchingGroups("Music");
        AudioMixerGroup[] sfxGroups = _mixer.FindMatchingGroups("SFX");
        _musicGroup = musicGroups.Length > 0 ? musicGroups[0] : null;
        _sfxGroup = sfxGroups.Length > 0 ? sfxGroups[0] : null;
    }

    private void ApplyMixerLevels()
    {
        if (_mixer == null)
        {
            ResolveMixer();
        }

        if (_mixer == null)
        {
            AudioListener.volume = _masterVolume;
            return;
        }

        AudioListener.volume = 1f;
        _mixer.SetFloat("MasterVolume", LinearToDecibels(_masterVolume));
        _mixer.SetFloat("MusicVolume", LinearToDecibels(_musicVolume));
        _mixer.SetFloat("SFXVolume", LinearToDecibels(_sfxVolume));
    }

    private void RouteSceneAudioSources()
    {
        if (_mixer == null)
        {
            ResolveMixer();
            ApplyMixerLevels();
        }

        if (_mixer == null || _musicGroup == null || _sfxGroup == null)
        {
            return;
        }

        AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (AudioSource source in sources)
        {
            if (source == null)
            {
                continue;
            }

            int id = source.GetInstanceID();
            bool previouslyRouted = _routedSourceIds.Contains(id);
            if (source.outputAudioMixerGroup != null && !previouslyRouted)
            {
                continue;
            }

            source.outputAudioMixerGroup = IsMusic(source)
                ? _musicGroup
                : _sfxGroup;
            _routedSourceIds.Add(id);
        }
    }

    private static bool IsMusic(AudioSource source)
    {
        AILURONEAudioCategory explicitCategory =
            source.GetComponentInParent<AILURONEAudioCategory>();
        if (explicitCategory != null)
        {
            if (explicitCategory.category == AILURONEAudioCategory.Category.Music)
            {
                return true;
            }

            if (explicitCategory.category ==
                AILURONEAudioCategory.Category.SoundEffects)
            {
                return false;
            }
        }

        string searchable = source.name;
        if (source.clip != null)
        {
            searchable += " " + source.clip.name;
        }

        Transform parent = source.transform.parent;
        int parentDepth = 0;
        while (parent != null && parentDepth < 3)
        {
            searchable += " " + parent.name;
            parent = parent.parent;
            parentDepth++;
        }

        searchable = searchable.ToLowerInvariant();
        foreach (string token in MusicTokens)
        {
            if (searchable.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static float LinearToDecibels(float linear)
    {
        return linear <= 0.0001f
            ? -80f
            : Mathf.Log10(linear) * 20f;
    }
}
