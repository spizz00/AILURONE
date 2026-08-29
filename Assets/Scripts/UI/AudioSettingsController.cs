using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared five-page settings editor used by both the main menu and the in-game
/// pause menu. Changes preview immediately; Apply persists them, while leaving
/// the panel without applying restores the last saved snapshot.
/// </summary>
public class AudioSettingsController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private Slider baseFovSlider;
    [SerializeField] private Slider dynamicFovSlider;
    [SerializeField] private Slider cameraShakeSlider;

    [Header("Controls")]
    [SerializeField] private TMP_InputField hipfireSensitivityInput;
    [SerializeField] private TMP_InputField adsSensitivityInput;
    [SerializeField] private Toggle invertVerticalToggle;

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Display")]
    [SerializeField] private TMP_Dropdown fullscreenModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private TMP_Dropdown graphicsQualityDropdown;
    [SerializeField] private Slider visualStyleSlider;

    [Header("Interface")]
    [SerializeField] private Toggle crosshairToggle;

    [Header("Actions")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetDefaultsButton;
    [SerializeField] private TextMeshProUGUI applyStatusText;

    private static readonly int[] FrameRateValues =
    {
        60,
        120,
        144,
        165,
        240,
        -1
    };

    private static readonly string[] FrameRateLabels =
    {
        "60 FPS",
        "120 FPS",
        "144 FPS",
        "165 FPS",
        "240 FPS",
        "UNLIMITED"
    };

    private static readonly string[] GraphicsQualityLabels =
    {
        "PERFORMANCE",
        "BALANCED",
        "HIGH",
        "ULTRA"
    };

    private readonly List<Vector2Int> _resolutionOptions =
        new List<Vector2Int>();

    private AILURONEGameSettings.SettingsSnapshot _openingSnapshot;
    private bool _syncingUi;
    private bool _isOpen;

    private void Awake()
    {
        ResolveReferences();
        ConfigureControls();
        BindEvents();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureControls();
        BindEvents();

        _openingSnapshot = AILURONEGameSettings.SavedSnapshot;
        PopulateDisplayOptions(_openingSnapshot);
        WriteSnapshotToUi(_openingSnapshot);
        SetStatus(string.Empty);
        _isOpen = true;
    }

    private void OnDisable()
    {
        if (!_isOpen)
        {
            return;
        }

        _isOpen = false;
        AILURONEGameSettings.RevertPreview();
    }

    public void OnApplyPressed()
    {
        AILURONEGameSettings.SettingsSnapshot snapshot = ReadSnapshotFromUi();
        AILURONEGameSettings.SaveAndApply(snapshot);
        _openingSnapshot = AILURONEGameSettings.SavedSnapshot;
        WriteSnapshotToUi(_openingSnapshot);
        SetStatus("SETTINGS APPLIED");
    }

    public void OnResetDefaultsPressed()
    {
        AILURONEGameSettings.SettingsSnapshot defaults =
            AILURONEGameSettings.CreateDefaults();
        PopulateDisplayOptions(defaults);
        WriteSnapshotToUi(defaults);
        AILURONEGameSettings.Preview(defaults);
        SetStatus("DEFAULTS PREVIEWED — APPLY TO SAVE");
    }

    private void ResolveReferences()
    {
        if (baseFovSlider == null)
            baseFovSlider = FindChild<Slider>("Slider_BaseFOV");
        if (dynamicFovSlider == null)
            dynamicFovSlider = FindChild<Slider>("Slider_DynamicFOV");
        if (cameraShakeSlider == null)
            cameraShakeSlider = FindChild<Slider>("Slider_CameraShake");

        if (hipfireSensitivityInput == null)
            hipfireSensitivityInput = FindChild<TMP_InputField>(
                "Input_HipfireSensitivity");
        if (adsSensitivityInput == null)
            adsSensitivityInput = FindChild<TMP_InputField>(
                "Input_ADSSensitivity");
        if (invertVerticalToggle == null)
            invertVerticalToggle = FindChild<Toggle>("Toggle_InvertVertical");

        if (masterVolumeSlider == null)
            masterVolumeSlider = FindChild<Slider>("Slider_MasterVolume");
        if (musicVolumeSlider == null)
            musicVolumeSlider = FindChild<Slider>("Slider_MusicVolume");
        if (sfxVolumeSlider == null)
            sfxVolumeSlider = FindChild<Slider>("Slider_SFXVolume");

        if (fullscreenModeDropdown == null)
            fullscreenModeDropdown = FindChild<TMP_Dropdown>(
                "Dropdown_FullscreenMode");
        if (resolutionDropdown == null)
            resolutionDropdown = FindChild<TMP_Dropdown>("Dropdown_Resolution");
        if (vSyncToggle == null)
            vSyncToggle = FindChild<Toggle>("Toggle_VSync");
        if (frameRateDropdown == null)
            frameRateDropdown = FindChild<TMP_Dropdown>("Dropdown_FrameRate");
        if (graphicsQualityDropdown == null)
            graphicsQualityDropdown = FindChild<TMP_Dropdown>(
                "Dropdown_GraphicsQuality");
        if (visualStyleSlider == null)
            visualStyleSlider = FindChild<Slider>("Slider_VisualStyle");

        if (crosshairToggle == null)
            crosshairToggle = FindChild<Toggle>("Toggle_Crosshair");

        if (applyButton == null)
            applyButton = FindChild<Button>("Button_ApplySettings");
        if (resetDefaultsButton == null)
            resetDefaultsButton = FindChild<Button>("Button_ResetDefaults");
        if (applyStatusText == null)
            applyStatusText = FindChild<TextMeshProUGUI>("Text_ApplyStatus");
    }

    private void ConfigureControls()
    {
        ConfigureSlider(
            baseFovSlider,
            AILURONEGameSettings.MinimumBaseFov,
            AILURONEGameSettings.MaximumBaseFov,
            true);
        ConfigureSlider(dynamicFovSlider, 0f, 1f, false);
        ConfigureSlider(cameraShakeSlider, 0f, 1f, false);
        ConfigureSlider(masterVolumeSlider, 0f, 1f, false);
        ConfigureSlider(musicVolumeSlider, 0f, 1f, false);
        ConfigureSlider(sfxVolumeSlider, 0f, 1f, false);
        ConfigureSlider(visualStyleSlider, 0f, 1f, false);

        ConfigureSensitivityInput(hipfireSensitivityInput);
        ConfigureSensitivityInput(adsSensitivityInput);

        if (fullscreenModeDropdown != null)
        {
            fullscreenModeDropdown.ClearOptions();
            fullscreenModeDropdown.AddOptions(new List<string>
            {
                "EXCLUSIVE FULLSCREEN",
                "BORDERLESS",
                "WINDOWED"
            });
        }

        if (frameRateDropdown != null)
        {
            frameRateDropdown.ClearOptions();
            frameRateDropdown.AddOptions(new List<string>(FrameRateLabels));
        }

        if (graphicsQualityDropdown != null)
        {
            graphicsQualityDropdown.ClearOptions();
            graphicsQualityDropdown.AddOptions(
                new List<string>(GraphicsQualityLabels));
        }
    }

    private void BindEvents()
    {
        BindSlider(baseFovSlider, OnSliderChanged);
        BindSlider(dynamicFovSlider, OnSliderChanged);
        BindSlider(cameraShakeSlider, OnSliderChanged);
        BindSlider(masterVolumeSlider, OnSliderChanged);
        BindSlider(musicVolumeSlider, OnSliderChanged);
        BindSlider(sfxVolumeSlider, OnSliderChanged);
        BindSlider(visualStyleSlider, OnSliderChanged);

        BindInput(hipfireSensitivityInput);
        BindInput(adsSensitivityInput);

        BindToggle(invertVerticalToggle, OnToggleChanged);
        BindToggle(vSyncToggle, OnToggleChanged);
        BindToggle(crosshairToggle, OnToggleChanged);

        BindDropdown(fullscreenModeDropdown, OnDropdownChanged);
        BindDropdown(resolutionDropdown, OnDropdownChanged);
        BindDropdown(frameRateDropdown, OnDropdownChanged);
        BindDropdown(graphicsQualityDropdown, OnDropdownChanged);

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(OnApplyPressed);
            applyButton.onClick.AddListener(OnApplyPressed);
        }

        if (resetDefaultsButton != null)
        {
            resetDefaultsButton.onClick.RemoveListener(OnResetDefaultsPressed);
            resetDefaultsButton.onClick.AddListener(OnResetDefaultsPressed);
        }
    }

    private void PopulateDisplayOptions(
        AILURONEGameSettings.SettingsSnapshot snapshot)
    {
        _resolutionOptions.Clear();
        Resolution[] resolutions = Screen.resolutions;

        foreach (Resolution resolution in resolutions)
        {
            Vector2Int option = new Vector2Int(
                resolution.width,
                resolution.height);
            if (!_resolutionOptions.Contains(option))
            {
                _resolutionOptions.Add(option);
            }
        }

        Vector2Int savedOption = new Vector2Int(
            snapshot.resolutionWidth,
            snapshot.resolutionHeight);
        if (!_resolutionOptions.Contains(savedOption))
        {
            _resolutionOptions.Add(savedOption);
        }

        _resolutionOptions.Sort((left, right) =>
        {
            int widthComparison = left.x.CompareTo(right.x);
            return widthComparison != 0
                ? widthComparison
                : left.y.CompareTo(right.y);
        });

        if (resolutionDropdown == null)
        {
            return;
        }

        List<string> labels = new List<string>(_resolutionOptions.Count);
        foreach (Vector2Int resolution in _resolutionOptions)
        {
            labels.Add($"{resolution.x} × {resolution.y}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
    }

    private void WriteSnapshotToUi(
        AILURONEGameSettings.SettingsSnapshot snapshot)
    {
        _syncingUi = true;

        SetSliderValue(baseFovSlider, snapshot.baseFov);
        SetSliderValue(dynamicFovSlider, snapshot.dynamicFovStrength);
        SetSliderValue(cameraShakeSlider, snapshot.cameraShakeStrength);
        SetSliderValue(masterVolumeSlider, snapshot.masterVolume);
        SetSliderValue(musicVolumeSlider, snapshot.musicVolume);
        SetSliderValue(sfxVolumeSlider, snapshot.sfxVolume);
        SetSliderValue(visualStyleSlider, snapshot.visualStyleStrength);

        SetInputValue(hipfireSensitivityInput, snapshot.hipfireSensitivity);
        SetInputValue(adsSensitivityInput, snapshot.adsSensitivity);

        SetToggleValue(invertVerticalToggle, snapshot.invertVerticalLook);
        SetToggleValue(vSyncToggle, snapshot.vSync);
        SetToggleValue(crosshairToggle, snapshot.crosshairVisible);

        SetDropdownValue(
            fullscreenModeDropdown,
            FullscreenModeToIndex(snapshot.fullscreenMode));
        SetDropdownValue(
            resolutionDropdown,
            FindResolutionIndex(
                snapshot.resolutionWidth,
                snapshot.resolutionHeight));
        SetDropdownValue(
            frameRateDropdown,
            FindFrameRateIndex(snapshot.frameRateLimit));
        SetDropdownValue(
            graphicsQualityDropdown,
            snapshot.graphicsQualityPreset);

        RefreshAllValueLabels();
        _syncingUi = false;
    }

    private AILURONEGameSettings.SettingsSnapshot ReadSnapshotFromUi()
    {
        AILURONEGameSettings.SettingsSnapshot snapshot = _openingSnapshot;

        if (baseFovSlider != null)
            snapshot.baseFov = baseFovSlider.value;
        if (dynamicFovSlider != null)
            snapshot.dynamicFovStrength = dynamicFovSlider.value;
        if (cameraShakeSlider != null)
            snapshot.cameraShakeStrength = cameraShakeSlider.value;

        snapshot.hipfireSensitivity = ReadSensitivity(
            hipfireSensitivityInput,
            snapshot.hipfireSensitivity);
        snapshot.adsSensitivity = ReadSensitivity(
            adsSensitivityInput,
            snapshot.adsSensitivity);
        if (invertVerticalToggle != null)
            snapshot.invertVerticalLook = invertVerticalToggle.isOn;

        if (masterVolumeSlider != null)
            snapshot.masterVolume = masterVolumeSlider.value;
        if (musicVolumeSlider != null)
            snapshot.musicVolume = musicVolumeSlider.value;
        if (sfxVolumeSlider != null)
            snapshot.sfxVolume = sfxVolumeSlider.value;
        if (visualStyleSlider != null)
            snapshot.visualStyleStrength = visualStyleSlider.value;

        if (fullscreenModeDropdown != null)
            snapshot.fullscreenMode = IndexToFullscreenMode(
                fullscreenModeDropdown.value);

        if (resolutionDropdown != null && _resolutionOptions.Count > 0)
        {
            int index = Mathf.Clamp(
                resolutionDropdown.value,
                0,
                _resolutionOptions.Count - 1);
            snapshot.resolutionWidth = _resolutionOptions[index].x;
            snapshot.resolutionHeight = _resolutionOptions[index].y;
        }

        if (vSyncToggle != null)
            snapshot.vSync = vSyncToggle.isOn;
        if (frameRateDropdown != null)
        {
            int index = Mathf.Clamp(
                frameRateDropdown.value,
                0,
                FrameRateValues.Length - 1);
            snapshot.frameRateLimit = FrameRateValues[index];
        }
        if (graphicsQualityDropdown != null)
        {
            snapshot.graphicsQualityPreset = Mathf.Clamp(
                graphicsQualityDropdown.value,
                0,
                GraphicsQualityLabels.Length - 1);
        }

        if (crosshairToggle != null)
            snapshot.crosshairVisible = crosshairToggle.isOn;

        return snapshot;
    }

    private void OnSliderChanged(float _)
    {
        if (_syncingUi)
        {
            return;
        }

        RefreshAllValueLabels();
        PreviewUiValues();
    }

    private void OnToggleChanged(bool _)
    {
        if (!_syncingUi)
        {
            PreviewUiValues();
        }
    }

    private void OnDropdownChanged(int _)
    {
        if (!_syncingUi)
        {
            PreviewUiValues();
        }
    }

    private void OnSensitivityChanged(string _)
    {
        if (!_syncingUi)
        {
            PreviewUiValues();
        }
    }

    private void OnSensitivityEndEdit(string _)
    {
        NormalizeSensitivityInputs();
        PreviewUiValues();
    }

    private void PreviewUiValues()
    {
        SetStatus("UNAPPLIED CHANGES");
        AILURONEGameSettings.Preview(ReadSnapshotFromUi());
    }

    private void NormalizeSensitivityInputs()
    {
        SetInputValue(
            hipfireSensitivityInput,
            ReadSensitivity(
                hipfireSensitivityInput,
                _openingSnapshot.hipfireSensitivity));
        SetInputValue(
            adsSensitivityInput,
            ReadSensitivity(
                adsSensitivityInput,
                _openingSnapshot.adsSensitivity));
    }

    private void RefreshAllValueLabels()
    {
        float baseFovValue = baseFovSlider != null
            ? baseFovSlider.value
            : 90f;
        SetSliderLabel(baseFovSlider, $"{baseFovValue:0}°");
        SetSliderLabel(
            dynamicFovSlider,
            FormatPercentage(dynamicFovSlider));
        SetSliderLabel(
            cameraShakeSlider,
            FormatPercentage(cameraShakeSlider));
        SetSliderLabel(
            masterVolumeSlider,
            FormatPercentage(masterVolumeSlider));
        SetSliderLabel(
            musicVolumeSlider,
            FormatPercentage(musicVolumeSlider));
        SetSliderLabel(
            sfxVolumeSlider,
            FormatPercentage(sfxVolumeSlider));
        SetSliderLabel(
            visualStyleSlider,
            FormatPercentage(visualStyleSlider));
    }

    private static string FormatPercentage(Slider slider)
    {
        return slider == null
            ? "0%"
            : $"{slider.value * 100f:0}%";
    }

    private static void SetSliderLabel(Slider slider, string value)
    {
        if (slider == null)
        {
            return;
        }

        Transform textTransform = slider.transform.Find("Text_Value");
        if (textTransform == null)
        {
            textTransform = slider.transform.Find("Text_Percentage");
        }

        TMP_Text label = textTransform != null
            ? textTransform.GetComponent<TMP_Text>()
            : null;
        if (label != null)
        {
            label.text = value;
        }
    }

    private void SetStatus(string value)
    {
        if (applyStatusText != null)
        {
            applyStatusText.text = value;
        }
    }

    private T FindChild<T>(string childName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name != childName)
            {
                continue;
            }

            T component = child.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static void ConfigureSlider(
        Slider slider,
        float minimum,
        float maximum,
        bool wholeNumbers)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = wholeNumbers;
    }

    private static void ConfigureSensitivityInput(TMP_InputField input)
    {
        if (input == null)
        {
            return;
        }

        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 5;
    }

    private void BindSlider(
        Slider slider,
        UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(callback);
        slider.onValueChanged.AddListener(callback);
    }

    private void BindInput(TMP_InputField input)
    {
        if (input == null)
        {
            return;
        }

        input.onValueChanged.RemoveListener(OnSensitivityChanged);
        input.onValueChanged.AddListener(OnSensitivityChanged);
        input.onEndEdit.RemoveListener(OnSensitivityEndEdit);
        input.onEndEdit.AddListener(OnSensitivityEndEdit);
    }

    private static void BindToggle(
        Toggle toggle,
        UnityEngine.Events.UnityAction<bool> callback)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveListener(callback);
        toggle.onValueChanged.AddListener(callback);
    }

    private static void BindDropdown(
        TMP_Dropdown dropdown,
        UnityEngine.Events.UnityAction<int> callback)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.onValueChanged.RemoveListener(callback);
        dropdown.onValueChanged.AddListener(callback);
    }

    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown == null || dropdown.options.Count == 0)
        {
            return;
        }

        dropdown.SetValueWithoutNotify(
            Mathf.Clamp(value, 0, dropdown.options.Count - 1));
        dropdown.RefreshShownValue();
    }

    private static void SetInputValue(TMP_InputField input, float value)
    {
        if (input != null)
        {
            input.SetTextWithoutNotify(
                MouseSensitivitySettings.Clamp(value).ToString(
                    "0.00",
                    CultureInfo.InvariantCulture));
        }
    }

    private static float ReadSensitivity(
        TMP_InputField input,
        float fallback)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.text))
        {
            return fallback;
        }

        string normalized = input.text.Trim().Replace(',', '.');
        if (!float.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value))
        {
            return fallback;
        }

        return MouseSensitivitySettings.Clamp(value);
    }

    private int FindResolutionIndex(int width, int height)
    {
        int index = _resolutionOptions.IndexOf(new Vector2Int(width, height));
        return index >= 0 ? index : Mathf.Max(0, _resolutionOptions.Count - 1);
    }

    private static int FindFrameRateIndex(int frameRate)
    {
        for (int i = 0; i < FrameRateValues.Length; i++)
        {
            if (FrameRateValues[i] == frameRate)
            {
                return i;
            }
        }

        return FrameRateValues.Length - 1;
    }

    private static int FullscreenModeToIndex(FullScreenMode mode)
    {
        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen:
                return 0;
            case FullScreenMode.Windowed:
                return 2;
            default:
                return 1;
        }
    }

    private static FullScreenMode IndexToFullscreenMode(int index)
    {
        switch (index)
        {
            case 0:
                return FullScreenMode.ExclusiveFullScreen;
            case 2:
                return FullScreenMode.Windowed;
            default:
                return FullScreenMode.FullScreenWindow;
        }
    }
}
