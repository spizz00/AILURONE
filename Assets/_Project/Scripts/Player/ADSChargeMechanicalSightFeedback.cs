#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ADSChargeMechanicalSightFeedback : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Usually the PlayerWeapon component on this object.")]
    public PlayerWeapon weapon;

    [Tooltip("The separate mechanical sight object named Middle.")]
    public Transform middle;

    [Tooltip("A dedicated thin mesh named ChargeGlow. Do not assign the Middle renderer here.")]
    public Renderer chargeGlowRenderer;

    [Header("Mechanical Movement (relative to the captured rest pose)")]
    public Vector3 adsReadyLocalOffset = new Vector3(0f, 0.08f, 0f);
    public Vector3 fullyChargedLocalOffset = new Vector3(0f, 0.22f, 0f);

    [Min(0.01f)] public float adsEnterDuration = 0.18f;
    [Min(0.01f)] public float adsExitDuration = 0.14f;
    [Min(0.01f)] public float chargeReturnDuration = 0.10f;

    [Header("Charge Glow Strip")]
    [Tooltip("Exact object name used when the renderer reference is empty.")]
    public string chargeGlowObjectName = "ChargeGlow";

    [ColorUsage(true, true)]
    public Color adsIdleGlowColor = new Color(0.12f, 0.002f, 0f, 1f);

    [ColorUsage(true, true)]
    public Color maximumChargeGlowColor = new Color(5f, 0.025f, 0.008f, 1f);

    [ColorUsage(true, true)]
    public Color fullChargePulseColor = new Color(9f, 1.1f, 0.25f, 1f);

    [Min(0.1f)]
    [Tooltip("Higher values keep early charge subtle and make the final part brighten faster.")]
    public float glowResponseExponent = 1.65f;

    [Range(0f, 1f)]
    public float fullChargePulseStrength = 0.8f;

    [Min(0.01f)]
    public float fullChargePulseDuration = 0.12f;

    [Header("Weak Auxiliary Point Light")]
    [Tooltip("Optional. The emissive strip remains the primary feedback.")]
    public Light chargeLight;

    public bool createLightIfMissing = true;
    public Vector3 chargeLightLocalOffset = new Vector3(0f, 0.035f, 0.015f);

    [ColorUsage(true, true)]
    public Color chargeLightColor = new Color(1f, 0.015f, 0.005f, 1f);

    [Min(0f)] public float adsIdleLightIntensity = 0.015f;
    [Min(0f)] public float maximumChargeLightIntensity = 0.22f;
    [Min(0f)] public float fullChargeLightPulseBoost = 0.15f;
    [Min(0.01f)] public float chargeLightRange = 0.35f;
    public bool limitLightToWeaponLayer = true;

    [Header("Runtime State (read only)")]
    [SerializeField] private float adsBlend;
    [SerializeField] private float displayedCharge01;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Vector3 _restLocalPosition;
    private bool _hasRestPose;
    private bool _wasFullyCharged;
    private float _pulseRemaining;
    private GameObject _runtimeLightObject;
    private MaterialPropertyBlock _glowProperties;

    private void Awake()
    {
        ResolveReferences();
        CaptureRestPose();
        EnsureChargeLight();
        _glowProperties = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (weapon == null || middle == null)
        {
            return;
        }

        if (!_hasRestPose)
        {
            CaptureRestPose();
        }

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);

        UpdateMechanicalPosition(deltaTime);
        UpdatePulse(deltaTime);
        UpdateChargeGlow();
        UpdateChargeLight();
    }

    private void UpdateMechanicalPosition(float deltaTime)
    {
        float targetAdsBlend = weapon.IsAiming ? 1f : 0f;
        float adsDuration = targetAdsBlend > adsBlend
            ? adsEnterDuration
            : adsExitDuration;

        adsBlend = Mathf.MoveTowards(
            adsBlend,
            targetAdsBlend,
            deltaTime / Mathf.Max(0.01f, adsDuration)
        );

        if (weapon.IsAdsCharging)
        {
            displayedCharge01 = Mathf.Clamp01(weapon.AdsCharge01);
        }
        else
        {
            displayedCharge01 = Mathf.MoveTowards(
                displayedCharge01,
                0f,
                deltaTime / Mathf.Max(0.01f, chargeReturnDuration)
            );
        }

        float curvedAds = Mathf.SmoothStep(0f, 1f, adsBlend);
        float curvedCharge = Mathf.SmoothStep(0f, 1f, displayedCharge01);
        Vector3 adsOffset = Vector3.Lerp(
            adsReadyLocalOffset,
            fullyChargedLocalOffset,
            curvedCharge
        );

        middle.localPosition = _restLocalPosition + adsOffset * curvedAds;
        UpdateRuntimeLightPosition();
    }

    private void UpdatePulse(float deltaTime)
    {
        bool fullyCharged = weapon.IsAdsChargeReady;
        if (fullyCharged && !_wasFullyCharged)
        {
            _pulseRemaining = fullChargePulseDuration;
        }

        _wasFullyCharged = fullyCharged;
        _pulseRemaining = Mathf.Max(0f, _pulseRemaining - deltaTime);
    }

    private void UpdateChargeGlow()
    {
        if (chargeGlowRenderer == null)
        {
            return;
        }

        if (_glowProperties == null)
        {
            _glowProperties = new MaterialPropertyBlock();
        }

        float chargeCurve = Mathf.Pow(
            Mathf.Clamp01(displayedCharge01),
            Mathf.Max(0.1f, glowResponseExponent)
        );

        Color glowColor = Color.Lerp(
            adsIdleGlowColor,
            maximumChargeGlowColor,
            chargeCurve
        ) * Mathf.SmoothStep(0f, 1f, adsBlend);

        float pulse01 = GetPulse01();
        glowColor = Color.Lerp(
            glowColor,
            fullChargePulseColor,
            pulse01 * Mathf.Clamp01(fullChargePulseStrength)
        );

        glowColor.a = Mathf.Clamp01(adsBlend);

        chargeGlowRenderer.GetPropertyBlock(_glowProperties);
        _glowProperties.SetColor(BaseColorId, glowColor);
        _glowProperties.SetColor(ColorId, glowColor);
        _glowProperties.SetColor(EmissionColorId, glowColor);
        chargeGlowRenderer.SetPropertyBlock(_glowProperties);
    }

    private void UpdateChargeLight()
    {
        EnsureChargeLight();

        if (chargeLight == null)
        {
            return;
        }

        float chargeCurve = Mathf.Pow(
            Mathf.Clamp01(displayedCharge01),
            Mathf.Max(0.1f, glowResponseExponent)
        );

        float intensity = Mathf.Lerp(
            adsIdleLightIntensity,
            maximumChargeLightIntensity,
            chargeCurve
        ) * adsBlend;

        intensity += GetPulse01() * fullChargeLightPulseBoost;

        chargeLight.color = chargeLightColor;
        chargeLight.range = Mathf.Max(0.01f, chargeLightRange);
        chargeLight.intensity = Mathf.Max(0f, intensity);
        chargeLight.enabled = chargeLight.intensity > 0.001f;
    }

    private float GetPulse01()
    {
        if (_pulseRemaining <= 0f)
        {
            return 0f;
        }

        float progress = 1f - _pulseRemaining /
            Mathf.Max(0.01f, fullChargePulseDuration);
        return Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
    }

    private void ResolveReferences()
    {
        if (weapon == null)
        {
            weapon = GetComponent<PlayerWeapon>();
        }

        if (middle == null)
        {
            middle = FindNamedTransform("Middle");
        }

        if (chargeGlowRenderer == null)
        {
            Transform glowTransform = FindNamedTransform(chargeGlowObjectName);
            if (glowTransform != null)
            {
                chargeGlowRenderer = glowTransform.GetComponent<Renderer>();
            }
        }
    }

    private Transform FindNamedTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private void CaptureRestPose()
    {
        if (middle == null)
        {
            return;
        }

        _restLocalPosition = middle.localPosition;
        _hasRestPose = true;
    }

    private void EnsureChargeLight()
    {
        if (chargeLight != null || !createLightIfMissing ||
            middle == null || !Application.isPlaying)
        {
            return;
        }

        Transform lightParent = middle.parent != null ? middle.parent : middle;
        _runtimeLightObject = new GameObject("MiddleChargeLight_Runtime");
        _runtimeLightObject.layer = middle.gameObject.layer;
        _runtimeLightObject.transform.SetParent(lightParent, false);

        chargeLight = _runtimeLightObject.AddComponent<Light>();
        chargeLight.type = LightType.Point;
        chargeLight.shadows = LightShadows.None;
        chargeLight.enabled = false;

        if (limitLightToWeaponLayer)
        {
            chargeLight.cullingMask = 1 << middle.gameObject.layer;
        }

        UpdateRuntimeLightPosition();
    }

    private void UpdateRuntimeLightPosition()
    {
        if (_runtimeLightObject == null || middle == null)
        {
            return;
        }

        _runtimeLightObject.transform.localPosition =
            middle.localPosition + chargeLightLocalOffset;
        _runtimeLightObject.transform.localRotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        if (_hasRestPose && middle != null)
        {
            middle.localPosition = _restLocalPosition;
        }

        adsBlend = 0f;
        displayedCharge01 = 0f;
        _wasFullyCharged = false;
        _pulseRemaining = 0f;

        if (chargeGlowRenderer != null)
        {
            chargeGlowRenderer.SetPropertyBlock(null);
        }

        if (chargeLight != null)
        {
            chargeLight.intensity = 0f;
            chargeLight.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_runtimeLightObject != null)
        {
            Destroy(_runtimeLightObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        adsEnterDuration = Mathf.Max(0.01f, adsEnterDuration);
        adsExitDuration = Mathf.Max(0.01f, adsExitDuration);
        chargeReturnDuration = Mathf.Max(0.01f, chargeReturnDuration);
        glowResponseExponent = Mathf.Max(0.1f, glowResponseExponent);
        fullChargePulseDuration = Mathf.Max(0.01f, fullChargePulseDuration);
        adsIdleLightIntensity = Mathf.Max(0f, adsIdleLightIntensity);
        maximumChargeLightIntensity = Mathf.Max(0f, maximumChargeLightIntensity);
        fullChargeLightPulseBoost = Mathf.Max(0f, fullChargeLightPulseBoost);
        chargeLightRange = Mathf.Max(0.01f, chargeLightRange);
    }
#endif
}
