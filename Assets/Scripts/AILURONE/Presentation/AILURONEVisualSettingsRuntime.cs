using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the persisted graphics preset and a restrained Level-only grade.
/// The grade is created at runtime, so scene assets and authored local volumes
/// remain untouched and continue to drive gameplay-specific effects.
/// </summary>
public sealed class AILURONEVisualSettingsRuntime : MonoBehaviour
{
    private const string LevelSceneName = "Level";
    private const string RuntimeVolumeName = "AILURONE_VisualGrade_Runtime";

    private static int _requestedQualityPreset = 2;
    private static float _requestedStyleStrength = 0.85f;

    private GameObject _volumeObject;
    private VolumeProfile _runtimeProfile;

    public static AILURONEVisualSettingsRuntime Instance { get; private set; }

    public static void Apply(int qualityPreset, float styleStrength)
    {
        _requestedQualityPreset = Mathf.Clamp(qualityPreset, 0, 3);
        _requestedStyleStrength = Mathf.Clamp01(styleStrength);

        if (Instance != null)
        {
            Instance.ApplyCurrentSettings();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyCurrentSettings();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        DestroyRuntimeVolume();
        Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        ApplyCurrentSettings();
    }

    private void ApplyCurrentSettings()
    {
        ApplyQualityPreset(_requestedQualityPreset);
        ApplyCameraQuality(_requestedQualityPreset);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == LevelSceneName)
        {
            EnsureRuntimeVolume(activeScene);
            Volume volume = _volumeObject != null
                ? _volumeObject.GetComponent<Volume>()
                : null;
            if (volume != null)
            {
                volume.weight = _requestedStyleStrength;
            }
        }
        else
        {
            DestroyRuntimeVolume();
        }
    }

    private static void ApplyQualityPreset(int preset)
    {
        switch (preset)
        {
            case 0:
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.anisotropicFiltering =
                    AnisotropicFiltering.Disable;
                QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.Low;
                QualitySettings.shadowDistance = 35f;
                QualitySettings.lodBias = 1f;
                QualitySettings.softParticles = false;
                QualitySettings.realtimeReflectionProbes = false;
                break;
            case 1:
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.anisotropicFiltering =
                    AnisotropicFiltering.Enable;
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.Medium;
                QualitySettings.shadowDistance = 50f;
                QualitySettings.lodBias = 1.5f;
                QualitySettings.softParticles = true;
                QualitySettings.realtimeReflectionProbes = false;
                break;
            case 3:
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.anisotropicFiltering =
                    AnisotropicFiltering.ForceEnable;
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.VeryHigh;
                QualitySettings.shadowDistance = 90f;
                QualitySettings.lodBias = 3f;
                QualitySettings.softParticles = true;
                QualitySettings.realtimeReflectionProbes = true;
                break;
            default:
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.anisotropicFiltering =
                    AnisotropicFiltering.ForceEnable;
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.High;
                QualitySettings.shadowDistance = 70f;
                QualitySettings.lodBias = 2f;
                QualitySettings.softParticles = true;
                QualitySettings.realtimeReflectionProbes = true;
                break;
        }
    }

    private static void ApplyCameraQuality(int preset)
    {
        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Include);
        foreach (Camera camera in cameras)
        {
            UniversalAdditionalCameraData cameraData =
                camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;

            if (preset == 0)
            {
                cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                continue;
            }

            cameraData.antialiasing =
                AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = preset == 1
                ? AntialiasingQuality.Low
                : preset == 2
                    ? AntialiasingQuality.Medium
                    : AntialiasingQuality.High;
        }
    }

    private void EnsureRuntimeVolume(Scene scene)
    {
        if (_volumeObject != null && _volumeObject.scene == scene)
        {
            return;
        }

        DestroyRuntimeVolume();

        _volumeObject = new GameObject(RuntimeVolumeName);
        SceneManager.MoveGameObjectToScene(_volumeObject, scene);

        Volume volume = _volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = _requestedStyleStrength;

        _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _runtimeProfile.name = RuntimeVolumeName + "_Profile";
        volume.profile = _runtimeProfile;

        Tonemapping tonemapping = _runtimeProfile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = _runtimeProfile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-0.05f);
        color.contrast.Override(12f);
        color.saturation.Override(-5f);
        color.colorFilter.Override(new Color(0.96f, 0.985f, 1f, 1f));

        WhiteBalance balance = _runtimeProfile.Add<WhiteBalance>(true);
        balance.temperature.Override(-5f);
        balance.tint.Override(-1f);

        Bloom bloom = _runtimeProfile.Add<Bloom>(true);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.38f);
        bloom.scatter.Override(0.55f);
        bloom.highQualityFiltering.Override(true);

        Vignette vignette = _runtimeProfile.Add<Vignette>(true);
        vignette.intensity.Override(0.14f);
        vignette.smoothness.Override(0.42f);
        vignette.rounded.Override(false);
    }

    private void DestroyRuntimeVolume()
    {
        if (_volumeObject != null)
        {
            Destroy(_volumeObject);
            _volumeObject = null;
        }

        if (_runtimeProfile != null)
        {
            Destroy(_runtimeProfile);
            _runtimeProfile = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        _requestedQualityPreset = 2;
        _requestedStyleStrength = 0.85f;
    }
}
