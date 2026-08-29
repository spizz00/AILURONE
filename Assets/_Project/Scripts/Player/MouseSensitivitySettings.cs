#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

/// <summary>
/// Shared, persisted mouse-look sensitivity used by gameplay and Settings UI.
/// A value of 1.00 preserves the project's existing mouse sensitivity.
/// </summary>
public static class MouseSensitivitySettings
{
    private const string HipfirePrefKey =
        "Settings.MouseSensitivity.Hipfire";
    private const string AdsPrefKey =
        "Settings.MouseSensitivity.ADS";

    public const float MinimumSensitivity = 0.05f;
    public const float MaximumSensitivity = 10f;
    public const float DefaultHipfireSensitivity = 1f;
    public const float DefaultAdsSensitivity = 1f;

    private static float _hipfireSensitivity;
    private static float _adsSensitivity;

    public static float HipfireSensitivity => _hipfireSensitivity;
    public static float AdsSensitivity => _adsSensitivity;

    static MouseSensitivitySettings()
    {
        ReloadFromPlayerPrefs();
    }

    public static void ReloadFromPlayerPrefs()
    {
        _hipfireSensitivity = Clamp(
            PlayerPrefs.GetFloat(
                HipfirePrefKey,
                DefaultHipfireSensitivity));
        _adsSensitivity = Clamp(
            PlayerPrefs.GetFloat(
                AdsPrefKey,
                DefaultAdsSensitivity));
    }

    public static void Preview(
        float hipfireSensitivity,
        float adsSensitivity)
    {
        _hipfireSensitivity = Clamp(hipfireSensitivity);
        _adsSensitivity = Clamp(adsSensitivity);
    }

    public static void Save(
        float hipfireSensitivity,
        float adsSensitivity)
    {
        Preview(hipfireSensitivity, adsSensitivity);
        PlayerPrefs.SetFloat(HipfirePrefKey, _hipfireSensitivity);
        PlayerPrefs.SetFloat(AdsPrefKey, _adsSensitivity);
        PlayerPrefs.Save();
    }

    public static float Clamp(float value)
    {
        return Mathf.Clamp(
            value,
            MinimumSensitivity,
            MaximumSensitivity);
    }
}
