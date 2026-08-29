using System;
using UnityEngine;

/// <summary>
/// One persisted settings source shared by the main menu, pause menu, and gameplay.
/// Runtime-facing systems read the lightweight static properties below instead of
/// maintaining scene-local copies of the same preference.
/// </summary>
public static class AILURONEGameSettings
{
    public const float MinimumBaseFov = 70f;
    public const float MaximumBaseFov = 120f;

    private const string BaseFovKey = "Settings.Gameplay.BaseFOV";
    private const string DynamicFovKey = "Settings.Gameplay.DynamicFOV";
    private const string CameraShakeKey = "Settings.Gameplay.CameraShake";
    private const string InvertYKey = "Settings.Controls.InvertY";
    private const string MasterVolumeKey = "Settings.MasterVolume";
    private const string MusicVolumeKey = "Settings.MusicVolume";
    private const string SfxVolumeKey = "Settings.SFXVolume";
    private const string FullscreenModeKey = "Settings.Display.FullscreenMode";
    private const string ResolutionWidthKey = "Settings.Display.ResolutionWidth";
    private const string ResolutionHeightKey = "Settings.Display.ResolutionHeight";
    private const string VSyncKey = "Settings.Display.VSync";
    private const string FrameRateKey = "Settings.Display.FrameRate";
    private const string GraphicsQualityKey = "Settings.Display.GraphicsQuality";
    private const string VisualStyleKey = "Settings.Display.VisualStyle";
    private const string CrosshairKey = "Settings.Interface.Crosshair";

    private static bool _initialized;
    private static SettingsSnapshot _saved;
    private static SettingsSnapshot _current;

    [Serializable]
    public struct SettingsSnapshot
    {
        public float baseFov;
        public float dynamicFovStrength;
        public float cameraShakeStrength;
        public float hipfireSensitivity;
        public float adsSensitivity;
        public bool invertVerticalLook;
        public float masterVolume;
        public float musicVolume;
        public float sfxVolume;
        public FullScreenMode fullscreenMode;
        public int resolutionWidth;
        public int resolutionHeight;
        public bool vSync;
        public int frameRateLimit;
        public int graphicsQualityPreset;
        public float visualStyleStrength;
        public bool crosshairVisible;
    }

    public static float BaseFov
    {
        get { EnsureInitialized(); return _current.baseFov; }
    }

    public static float DynamicFovStrength
    {
        get { EnsureInitialized(); return _current.dynamicFovStrength; }
    }

    public static float CameraShakeStrength
    {
        get { EnsureInitialized(); return _current.cameraShakeStrength; }
    }

    public static bool InvertVerticalLook
    {
        get { EnsureInitialized(); return _current.invertVerticalLook; }
    }

    public static bool CrosshairVisible
    {
        get { EnsureInitialized(); return _current.crosshairVisible; }
    }

    public static SettingsSnapshot SavedSnapshot
    {
        get { EnsureInitialized(); return _saved; }
    }

    public static SettingsSnapshot CurrentSnapshot
    {
        get { EnsureInitialized(); return _current; }
    }

    public static SettingsSnapshot CreateDefaults()
    {
        Resolution currentResolution = Screen.currentResolution;
        int width = currentResolution.width > 0
            ? currentResolution.width
            : Mathf.Max(1280, Screen.width);
        int height = currentResolution.height > 0
            ? currentResolution.height
            : Mathf.Max(720, Screen.height);

        return new SettingsSnapshot
        {
            baseFov = 90f,
            dynamicFovStrength = 1f,
            cameraShakeStrength = 1f,
            hipfireSensitivity = MouseSensitivitySettings.DefaultHipfireSensitivity,
            adsSensitivity = MouseSensitivitySettings.DefaultAdsSensitivity,
            invertVerticalLook = false,
            masterVolume = 1f,
            musicVolume = 0.3f,
            sfxVolume = 1f,
            fullscreenMode = FullScreenMode.FullScreenWindow,
            resolutionWidth = width,
            resolutionHeight = height,
            vSync = false,
            frameRateLimit = -1,
            graphicsQualityPreset = 2,
            visualStyleStrength = 0.85f,
            crosshairVisible = true
        };
    }

    public static void Preview(SettingsSnapshot snapshot)
    {
        EnsureInitialized();
        _current = Sanitize(snapshot);
        ApplyNonDisplaySettings(_current);
    }

    public static void RevertPreview()
    {
        EnsureInitialized();
        _current = _saved;
        ApplyNonDisplaySettings(_current);
        ApplyDisplaySettings(_current);
    }

    public static void SaveAndApply(SettingsSnapshot snapshot)
    {
        EnsureInitialized();
        _saved = Sanitize(snapshot);
        _current = _saved;

        PlayerPrefs.SetFloat(BaseFovKey, _saved.baseFov);
        PlayerPrefs.SetFloat(DynamicFovKey, _saved.dynamicFovStrength);
        PlayerPrefs.SetFloat(CameraShakeKey, _saved.cameraShakeStrength);
        PlayerPrefs.SetInt(InvertYKey, _saved.invertVerticalLook ? 1 : 0);
        PlayerPrefs.SetFloat(MasterVolumeKey, _saved.masterVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, _saved.musicVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, _saved.sfxVolume);
        PlayerPrefs.SetInt(FullscreenModeKey, (int)_saved.fullscreenMode);
        PlayerPrefs.SetInt(ResolutionWidthKey, _saved.resolutionWidth);
        PlayerPrefs.SetInt(ResolutionHeightKey, _saved.resolutionHeight);
        PlayerPrefs.SetInt(VSyncKey, _saved.vSync ? 1 : 0);
        PlayerPrefs.SetInt(FrameRateKey, _saved.frameRateLimit);
        PlayerPrefs.SetInt(
            GraphicsQualityKey,
            _saved.graphicsQualityPreset);
        PlayerPrefs.SetFloat(VisualStyleKey, _saved.visualStyleStrength);
        PlayerPrefs.SetInt(CrosshairKey, _saved.crosshairVisible ? 1 : 0);

        MouseSensitivitySettings.Save(
            _saved.hipfireSensitivity,
            _saved.adsSensitivity);

        PlayerPrefs.Save();
        ApplyNonDisplaySettings(_saved);
        ApplyDisplaySettings(_saved);
    }

    public static void ReloadSavedSettings()
    {
        _initialized = false;
        EnsureInitialized();
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        SettingsSnapshot defaults = CreateDefaults();

        MouseSensitivitySettings.ReloadFromPlayerPrefs();

        _saved = Sanitize(new SettingsSnapshot
        {
            baseFov = PlayerPrefs.GetFloat(BaseFovKey, defaults.baseFov),
            dynamicFovStrength = PlayerPrefs.GetFloat(
                DynamicFovKey,
                defaults.dynamicFovStrength),
            cameraShakeStrength = PlayerPrefs.GetFloat(
                CameraShakeKey,
                defaults.cameraShakeStrength),
            hipfireSensitivity = MouseSensitivitySettings.HipfireSensitivity,
            adsSensitivity = MouseSensitivitySettings.AdsSensitivity,
            invertVerticalLook = PlayerPrefs.GetInt(
                InvertYKey,
                defaults.invertVerticalLook ? 1 : 0) != 0,
            masterVolume = PlayerPrefs.GetFloat(
                MasterVolumeKey,
                defaults.masterVolume),
            musicVolume = PlayerPrefs.GetFloat(
                MusicVolumeKey,
                defaults.musicVolume),
            sfxVolume = PlayerPrefs.GetFloat(
                SfxVolumeKey,
                defaults.sfxVolume),
            fullscreenMode = (FullScreenMode)PlayerPrefs.GetInt(
                FullscreenModeKey,
                (int)defaults.fullscreenMode),
            resolutionWidth = PlayerPrefs.GetInt(
                ResolutionWidthKey,
                defaults.resolutionWidth),
            resolutionHeight = PlayerPrefs.GetInt(
                ResolutionHeightKey,
                defaults.resolutionHeight),
            vSync = PlayerPrefs.GetInt(
                VSyncKey,
                defaults.vSync ? 1 : 0) != 0,
            frameRateLimit = PlayerPrefs.GetInt(
                FrameRateKey,
                defaults.frameRateLimit),
            graphicsQualityPreset = PlayerPrefs.GetInt(
                GraphicsQualityKey,
                defaults.graphicsQualityPreset),
            visualStyleStrength = PlayerPrefs.GetFloat(
                VisualStyleKey,
                defaults.visualStyleStrength),
            crosshairVisible = PlayerPrefs.GetInt(
                CrosshairKey,
                defaults.crosshairVisible ? 1 : 0) != 0
        });

        _current = _saved;
        ApplyNonDisplaySettings(_current);
    }

    private static SettingsSnapshot Sanitize(SettingsSnapshot snapshot)
    {
        SettingsSnapshot defaults = CreateDefaults();

        snapshot.baseFov = Mathf.Clamp(
            snapshot.baseFov,
            MinimumBaseFov,
            MaximumBaseFov);
        snapshot.dynamicFovStrength = Mathf.Clamp01(snapshot.dynamicFovStrength);
        snapshot.cameraShakeStrength = Mathf.Clamp01(snapshot.cameraShakeStrength);
        snapshot.hipfireSensitivity = MouseSensitivitySettings.Clamp(
            snapshot.hipfireSensitivity);
        snapshot.adsSensitivity = MouseSensitivitySettings.Clamp(
            snapshot.adsSensitivity);
        snapshot.masterVolume = Mathf.Clamp01(snapshot.masterVolume);
        snapshot.musicVolume = Mathf.Clamp01(snapshot.musicVolume);
        snapshot.sfxVolume = Mathf.Clamp01(snapshot.sfxVolume);

        if (!Enum.IsDefined(typeof(FullScreenMode), snapshot.fullscreenMode))
        {
            snapshot.fullscreenMode = defaults.fullscreenMode;
        }

        if (snapshot.resolutionWidth < 640 || snapshot.resolutionHeight < 360)
        {
            snapshot.resolutionWidth = defaults.resolutionWidth;
            snapshot.resolutionHeight = defaults.resolutionHeight;
        }

        if (snapshot.frameRateLimit != -1)
        {
            snapshot.frameRateLimit = Mathf.Clamp(snapshot.frameRateLimit, 30, 360);
        }

        snapshot.graphicsQualityPreset = Mathf.Clamp(
            snapshot.graphicsQualityPreset,
            0,
            3);
        snapshot.visualStyleStrength = Mathf.Clamp01(
            snapshot.visualStyleStrength);

        return snapshot;
    }

    private static void ApplyNonDisplaySettings(SettingsSnapshot snapshot)
    {
        MouseSensitivitySettings.Preview(
            snapshot.hipfireSensitivity,
            snapshot.adsSensitivity);

        AILURONESettingsRuntime.ApplyAudioLevels(
            snapshot.masterVolume,
            snapshot.musicVolume,
            snapshot.sfxVolume);
        AILURONEVisualSettingsRuntime.Apply(
            snapshot.graphicsQualityPreset,
            snapshot.visualStyleStrength);
    }

    private static void ApplyDisplaySettings(SettingsSnapshot snapshot)
    {
        QualitySettings.vSyncCount = snapshot.vSync ? 1 : 0;
        Application.targetFrameRate = snapshot.frameRateLimit;

        if (Screen.width != snapshot.resolutionWidth ||
            Screen.height != snapshot.resolutionHeight ||
            Screen.fullScreenMode != snapshot.fullscreenMode)
        {
            Screen.SetResolution(
                snapshot.resolutionWidth,
                snapshot.resolutionHeight,
                snapshot.fullscreenMode);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _initialized = false;
        _saved = default;
        _current = default;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRuntimeHost()
    {
        if (AILURONESettingsRuntime.Instance != null)
        {
            return;
        }

        GameObject host = new GameObject("AILURONE_SettingsRuntime");
        host.AddComponent<AILURONESettingsRuntime>();
        host.AddComponent<AILURONEVisualSettingsRuntime>();
        UnityEngine.Object.DontDestroyOnLoad(host);
        EnsureInitialized();
        ApplyDisplaySettings(_saved);
    }
}
