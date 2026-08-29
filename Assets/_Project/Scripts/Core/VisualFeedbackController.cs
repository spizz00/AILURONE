#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class VisualFeedbackController : MonoBehaviour
{
    public static VisualFeedbackController Instance;

    private static readonly int AshDashIntensityId =
        Shader.PropertyToID("_AshDashIntensity");

    private static readonly int AshDashDirectionId =
        Shader.PropertyToID("_AshDashDirection");

    private static readonly int AshDashPhaseId =
        Shader.PropertyToID("_AshDashPhase");

    [Header("后期处理配置")]
    public Volume globalVolume;

    [Header("跑酷粒子特效")]
    public ParticleSystem speedLines;
    public float particleFadeSpeed = 15f;

    [Header("🚀 1. 跳板过曝闪光特效 (Flash)")]
    public float maxFlashExposure = 2.5f; 
    public float flashDecaySpeed = 5f; 
    private float _currentExposure = 0f;

    [Header("💥 2. 镜头起跳震动 (Camera Shake)")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.2f;
    private bool _isShaking = false;

    [Header("🌌 3. 黑洞跃迁空间扭曲 (Warp)")]
    public float jumpPadDistortionPunch = -0.85f; 
    public float tunnelSustainedDistortion = -0.3f; 
    public float paniniDistance = 1f; 
    public float distortionSpeed = 10f;
    private float _currentDistortion = 0f;

    [Header("🎧 4. 深海听觉隔离 (Audio Low Pass)")]
    public float muffledFrequency = 1500f; 
    public float normalFrequency = 22000f; 
    public float audioTransitionSpeed = 10f;
    private AudioLowPassFilter _lowPassFilter;

    [Header("👁️ 5. 极速失焦黑视 (Tunnel Vision)")]
    public float jumpPadVignettePunch = 0.85f;
    public float jumpPadVignetteSmoothness = 1f;
    public float vignetteDecaySpeed = 2.5f;

    private float _currentVignettePunch = 0f;
    private float _currentVignetteSmoothness = 0.2f;
    private float _baseVignetteSmoothness = 0.2f; 

    [Header("视觉参数：慢动作时 (超频爆发)")]
    public float slowSaturation = -100f; 
    public float slowExposure = -1.5f;   
    public float slowChromatic = 1f;     
    public float slowVignette = 0.5f;  

    [Header("视觉参数：正常速度时 (跑动)")]
    public float normalSaturation = 0f; 
    public float normalExposure = 0f;    
    public float normalChromatic = 0f;
    public float normalVignette = 0.15f; 

    [Header("Visor Lens Baseline")]
    [Range(0f, 0.1f)]
    public float visorBaseChromatic = 0.018f;

    [Header("视觉参数：死亡时 (被回溯接管)")]
    public float deathSaturation = -100f; 
    public float deathVignette = 0.6f;    
    public float deathTransitionSpeed = 5f; 

    [Header("隧道色差")]
    [FormerlySerializedAs("dashChromatic")]
    public float tunnelChromatic = 1f;
    public float chromaticSpeed = 15f;

    [Header("Ash 风格 Dash 相位脉冲")]
    public float ashDashVisualDuration = 0.28f;
    [Range(0f, 1f)] public float ashDashChromatic = 0.32f;
    [Range(-1f, 1f)] public float ashDashLensDistortion = -0.08f;
    [Range(0f, 1f)] public float ashDashVignette = 0.12f;
    public float ashDashExposure = -0.08f;
    [FormerlySerializedAs("ashDashOverlayIntensity")]
    [Range(0f, 2f)] public float ashDashWarpIntensity = 1f;
    public Color ashDashTint = new Color(0.62f, 0.82f, 1f, 1f);
    public Color ashDashVignetteColor = new Color(0.025f, 0.02f, 0.12f, 1f);
    public Vector3 ashDashWeaponPositionOffset =
        new Vector3(0f, -0.035f, -0.24f);
    public Vector3 ashDashWeaponEulerOffset =
        new Vector3(4f, 0f, 0f);

    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private ChromaticAberration _chromaticAberration;
    private LensDistortion _lensDistortion;
    private PaniniProjection _paniniProjection;

    private float _tunnelEffectTimer = 0f;
    private float _dashPhaseTimer = 0f;
    private float _dashPhaseDuration = 0.28f;
    private Vector2 _dashScreenDirection = Vector2.up;
    private AlwaysEquippedWeaponController _dashWeaponController;
    private bool _dashWeaponOffsetActive;
    private Color _baseColorFilter = Color.white;
    private Color _baseVignetteColor = Color.black;
    private float _baseContrast;
    private float _originalEmissionRate;
    private float _originalSimSpeed;
    private float _currentParticleIntensity = 0f;

    private bool _isRebooting = false; 

    public bool SuppressVisorOverlay =>
        _isRebooting ||
        _tunnelEffectTimer > 0f ||
        _currentVignettePunch > 0.2f;

    void Awake()
    {
        Instance = this; 
    }

    void Start()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _colorAdjustments);
            globalVolume.profile.TryGet(out _vignette);
            globalVolume.profile.TryGet(out _chromaticAberration);
            globalVolume.profile.TryGet(out _lensDistortion);
            globalVolume.profile.TryGet(out _paniniProjection);

            if (_vignette != null)
            {
                _baseVignetteSmoothness = _vignette.smoothness.value;
                _currentVignetteSmoothness = _baseVignetteSmoothness;
                _baseVignetteColor = _vignette.color.value;
            }

            if (_colorAdjustments != null)
            {
                _baseColorFilter =
                    _colorAdjustments.colorFilter.value;

                _baseContrast =
                    _colorAdjustments.contrast.value;
            }

            EnsureVolumeOverrides();
        }

        SetDashWarp(0f, 0f);

        if (speedLines != null)
        {
            _originalEmissionRate = speedLines.emission.rateOverTimeMultiplier;
            _originalSimSpeed = speedLines.main.simulationSpeed;
        }

        if (Camera.main != null)
        {
            _lowPassFilter = Camera.main.GetComponent<AudioLowPassFilter>();
            if (_lowPassFilter == null) _lowPassFilter = Camera.main.gameObject.AddComponent<AudioLowPassFilter>();
            _lowPassFilter.cutoffFrequency = normalFrequency;
        }
    }

    void Update()
    {
        if (AILURONEGameplayActionGate.IsPaused)
        {
            return;
        }

        float dashIntensity = UpdateDashPhase();

        if (_colorAdjustments == null || _vignette == null) return;

        _currentExposure = Mathf.Lerp(_currentExposure, 0f, Time.unscaledDeltaTime * flashDecaySpeed);
        _currentVignettePunch = Mathf.Lerp(_currentVignettePunch, 0f, Time.unscaledDeltaTime * vignetteDecaySpeed);
        _currentVignetteSmoothness = Mathf.Lerp(_currentVignetteSmoothness, _baseVignetteSmoothness, Time.unscaledDeltaTime * vignetteDecaySpeed);

        // 🚨 核心阻断：如果正在回溯，锁死所有的常规视角更新！
        if (_isRebooting) return;

        float tunnelIntensity = 0f;
        float targetDistortion = 0f;
        float targetPanini = 0f;
        float targetFrequency = normalFrequency;

        if (_tunnelEffectTimer > 0f)
        {
            _tunnelEffectTimer -= Time.unscaledDeltaTime;
            tunnelIntensity = 1f;
            targetDistortion = tunnelSustainedDistortion; 
            targetPanini = paniniDistance;                
            targetFrequency = muffledFrequency;           
        }
        else
        {
            tunnelIntensity = 0f;
            targetDistortion =
                ashDashLensDistortion *
                dashIntensity;
            targetPanini = 0f;
            targetFrequency = normalFrequency;   
        }

        _currentDistortion = Mathf.Lerp(_currentDistortion, targetDistortion, Time.unscaledDeltaTime * distortionSpeed);
        if (_lensDistortion != null)
            _lensDistortion.intensity.value = _currentDistortion;
            
        if (_paniniProjection != null)
            _paniniProjection.distance.value = Mathf.Lerp(_paniniProjection.distance.value, targetPanini, Time.unscaledDeltaTime * distortionSpeed);

        if (_lowPassFilter != null)
            _lowPassFilter.cutoffFrequency = Mathf.Lerp(_lowPassFilter.cutoffFrequency, targetFrequency, Time.unscaledDeltaTime * audioTransitionSpeed);

        if (speedLines != null)
        {
            _currentParticleIntensity = Mathf.Lerp(_currentParticleIntensity, tunnelIntensity, Time.unscaledDeltaTime * particleFadeSpeed);
            var emission = speedLines.emission;
            var main = speedLines.main;

            emission.rateOverTimeMultiplier = _originalEmissionRate * _currentParticleIntensity;
            main.simulationSpeed = Mathf.Max(_originalSimSpeed * 0.8f, _originalSimSpeed * _currentParticleIntensity);

            if (_currentParticleIntensity > 0.05f && !speedLines.isPlaying) speedLines.Play(); 
            else if (_currentParticleIntensity <= 0.05f && speedLines.isPlaying) speedLines.Stop(); 
        }

        if (Time.timeScale == 0f)
        {
            _colorAdjustments.saturation.value = Mathf.Lerp(_colorAdjustments.saturation.value, deathSaturation, Time.unscaledDeltaTime * deathTransitionSpeed);
            _vignette.intensity.value = Mathf.Clamp01(Mathf.Lerp(_vignette.intensity.value, deathVignette, Time.unscaledDeltaTime * deathTransitionSpeed) + _currentVignettePunch);
            _vignette.smoothness.value = _currentVignetteSmoothness;
            return; 
        }

        float t = Mathf.Clamp01((Time.timeScale - 0.05f) / (1f - 0.05f));
        
        float timeExposure = Mathf.Lerp(slowExposure, normalExposure, t);
        _colorAdjustments.postExposure.value =
            _currentExposure +
            timeExposure +
            ashDashExposure * dashIntensity;
        _colorAdjustments.saturation.value = Mathf.Lerp(slowSaturation, normalSaturation, t);

        _colorAdjustments.colorFilter.value =
            Color.Lerp(
                _baseColorFilter,
                ashDashTint,
                dashIntensity * 0.35f
            );
        
        float baseVignette = Mathf.Lerp(slowVignette, normalVignette, t);
        _vignette.intensity.value =
            Mathf.Clamp01(
                baseVignette +
                _currentVignettePunch +
                ashDashVignette * dashIntensity
            );
        _vignette.smoothness.value = _currentVignetteSmoothness;
        _vignette.color.value =
            Color.Lerp(
                _baseVignetteColor,
                ashDashVignetteColor,
                dashIntensity
            );

        float timeChromatic = Mathf.Lerp(slowChromatic, normalChromatic, t);
        float finalChromaticTarget =
            Mathf.Clamp01(
                Mathf.Max(
                    visorBaseChromatic,
                    tunnelIntensity * tunnelChromatic +
                    dashIntensity * ashDashChromatic +
                    timeChromatic
                )
            );
        
        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = Mathf.Lerp(_chromaticAberration.intensity.value, finalChromaticTarget, Time.unscaledDeltaTime * chromaticSpeed);
        }
    }

    public void TriggerDashEffect(
        Vector3 worldDirection
    )
    {
        _dashPhaseDuration =
            Mathf.Max(
                0.05f,
                ashDashVisualDuration
            );

        _dashPhaseTimer =
            _dashPhaseDuration;

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 localDirection =
                mainCamera.transform
                    .InverseTransformDirection(
                        worldDirection
                    );

            _dashScreenDirection =
                new Vector2(
                    localDirection.x,
                    localDirection.z
                );

            if (_dashScreenDirection.sqrMagnitude > 0.001f)
            {
                _dashScreenDirection.Normalize();
            }
        }

        SetDashWarp(
            ashDashWarpIntensity * 0.35f,
            0f
        );

        ApplyDashWeaponLag();
    }

    public void TriggerTunnelEffect(float duration)
    {
        _tunnelEffectTimer =
            Mathf.Max(
                _tunnelEffectTimer,
                duration
            );
    }

    private float UpdateDashPhase()
    {
        if (_dashPhaseTimer <= 0f)
        {
            SetDashWarp(0f, 1f);
            ReleaseDashWeaponLag();
            return 0f;
        }

        _dashPhaseTimer =
            Mathf.Max(
                0f,
                _dashPhaseTimer -
                Time.unscaledDeltaTime
            );

        float progress =
            1f -
            _dashPhaseTimer /
            Mathf.Max(0.05f, _dashPhaseDuration);

        float attack =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0f,
                    0.08f,
                    progress
                )
            );

        float release =
            1f -
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0.45f,
                    1f,
                    progress
                )
            );

        float intensity =
            attack * release;

        SetDashWarp(
            intensity *
            ashDashWarpIntensity,
            progress
        );

        if (progress >= 0.55f)
        {
            ReleaseDashWeaponLag();
        }

        return intensity;
    }

    private void SetDashWarp(
        float intensity,
        float progress
    )
    {
        Shader.SetGlobalFloat(
            AshDashIntensityId,
            Mathf.Max(0f, intensity)
        );

        Shader.SetGlobalVector(
            AshDashDirectionId,
            new Vector4(
                _dashScreenDirection.x,
                _dashScreenDirection.y,
                0f,
                0f
            )
        );

        Shader.SetGlobalFloat(
            AshDashPhaseId,
            Mathf.Clamp01(progress)
        );
    }

    private void ApplyDashWeaponLag()
    {
        if (_dashWeaponController == null &&
            DashController.Instance != null)
        {
            _dashWeaponController =
                DashController.Instance.GetComponent<
                    AlwaysEquippedWeaponController
                >();
        }

        if (_dashWeaponController == null)
        {
            return;
        }

        Vector3 positionOffset =
            ashDashWeaponPositionOffset;

        positionOffset.x -=
            _dashScreenDirection.x * 0.12f;

        positionOffset.z *=
            Mathf.Lerp(
                0.72f,
                1f,
                Mathf.Abs(_dashScreenDirection.y)
            );

        Vector3 eulerOffset =
            ashDashWeaponEulerOffset +
            new Vector3(
                0f,
                -_dashScreenDirection.x * 6f,
                _dashScreenDirection.x * 5f
            );

        _dashWeaponController.SetExternalVisualOffset(
            positionOffset,
            eulerOffset,
            60f
        );

        _dashWeaponOffsetActive = true;
    }

    private void ReleaseDashWeaponLag()
    {
        if (!_dashWeaponOffsetActive ||
            _dashWeaponController == null)
        {
            return;
        }

        _dashWeaponController.ClearExternalVisualOffset(
            18f
        );

        _dashWeaponOffsetActive = false;
    }

    public void TriggerJumpPadFeedback()
    {
        _currentExposure = maxFlashExposure; 
        _currentDistortion = jumpPadDistortionPunch; 
        _currentVignettePunch = jumpPadVignettePunch;
        _currentVignetteSmoothness = jumpPadVignetteSmoothness;

        if (!_isShaking && Camera.main != null)
        {
            StartCoroutine(ShakeCameraRoutine(shakeDuration, shakeMagnitude));
        }
    }

    public void TriggerOctahedronBlastFeedback(float strength)
    {
        float safeStrength = Mathf.Clamp01(strength);
        _currentExposure = Mathf.Max(
            _currentExposure,
            0.75f * safeStrength);
        _currentDistortion = Mathf.Min(
            _currentDistortion,
            -0.2f * safeStrength);
        _currentVignettePunch = Mathf.Max(
            _currentVignettePunch,
            0.24f * safeStrength);
        _currentVignetteSmoothness = Mathf.Max(
            _currentVignetteSmoothness,
            0.8f);

        if (!_isShaking && Camera.main != null)
        {
            StartCoroutine(ShakeCameraRoutine(
                0.18f,
                0.13f * safeStrength));
        }
    }

    public void TriggerDistantImpactFeedback(float strength)
    {
        float safeStrength = Mathf.Clamp01(strength);

        if (!_isShaking && Camera.main != null)
        {
            StartCoroutine(ShakeCameraRoutine(
                0.20f,
                0.045f * safeStrength));
        }
    }

    private IEnumerator ShakeCameraRoutine(
        float duration,
        float magnitude)
    {
        magnitude *= AILURONEGameSettings.CameraShakeStrength;
        _isShaking = true;
        GameObject cameraRoot = GameObject.Find("PlayerCameraRoot");
        Transform targetTransform = cameraRoot != null ? cameraRoot.transform : Camera.main.transform;
        
        Vector3 originalPos = targetTransform.localPosition;
        float elapsed = 0f;

        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            float currentMagnitude = Mathf.Lerp(
                magnitude,
                0f,
                elapsed / safeDuration);
            float x = Random.Range(-1f, 1f) * currentMagnitude;
            float y = Random.Range(-1f, 1f) * currentMagnitude;
            
            targetTransform.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.unscaledDeltaTime; 
            yield return null;
        }

        targetTransform.localPosition = originalPos;
        _isShaking = false;
    }

    // ==========================================
    // 🚨 阶段 3：高维白光 CRT 回溯 (Tracer Flashback)
    // ==========================================
    public void TriggerRebootVisuals(float duration)
    {
        if (!_isRebooting) StartCoroutine(RebootVisualRoutine(duration));
    }

    private IEnumerator RebootVisualRoutine(float duration)
    {
        _isRebooting = true; 

        EnsureVolumeOverrides();

        if (_colorAdjustments != null)
        {
            _colorAdjustments.colorFilter.value = _baseColorFilter;
        }

        if (_vignette != null)
        {
            _vignette.color.value = _baseVignetteColor;
        }

        if (_lowPassFilter != null) _lowPassFilter.cutoffFrequency = 400f; // 听觉瞬间坠入深海

        // 💥 阶段 A：起步爆发 (0.2秒) —— 瞬间闪白与空间向后塌陷
        float flashTime = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < flashTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / flashTime;

            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = Mathf.Lerp(normalSaturation, -100f, t);
                _colorAdjustments.postExposure.value = Mathf.Lerp(normalExposure, 3.5f, t); // 极其刺眼的高维白光
                _colorAdjustments.contrast.value = Mathf.Lerp(_baseContrast, 40f, t);
            }
            if (_lensDistortion != null) _lensDistortion.intensity.value = Mathf.Lerp(0f, -0.6f, t); // 空间后置拉扯
            if (_chromaticAberration != null) _chromaticAberration.intensity.value = Mathf.Lerp(visorBaseChromatic, 1f, t);
            if (_vignette != null) _vignette.intensity.value = Mathf.Lerp(normalVignette, 0.5f, t);

            yield return null;
        }

        // 🚀 阶段 B：高维白光滑行 (匹配物理回溯的耗时，预留最后0.3秒恢复)
        float sustainTime = Mathf.Max(0f, duration - flashTime - 0.3f);
        elapsed = 0f;

        while (elapsed < sustainTime)
        {
            elapsed += Time.unscaledDeltaTime;
            
            // 维持高维白光，伴随极其微弱的电压频闪和强烈的色散
            if (_colorAdjustments != null)
                _colorAdjustments.postExposure.value = Random.Range(3.0f, 3.8f);
            
            if (_chromaticAberration != null) 
                _chromaticAberration.intensity.value = Random.Range(0.8f, 1.2f);
            
            if (_lensDistortion != null)
                _lensDistortion.intensity.value = Random.Range(-0.65f, -0.55f);

            yield return null;
        }

        // 🌊 阶段 C：现实重连 (0.3秒) —— 白光如潮水般褪去，色彩瞬间涌入
        float recoverTime = 0.3f;
        elapsed = 0f;

        while (elapsed < recoverTime)
        {
            elapsed += Time.unscaledDeltaTime;
            // 采用平滑曲线(SmoothStep)，让落地感更扎实
            float t = elapsed / recoverTime;
            float smoothT = t * t * (3f - 2f * t);

            if (_colorAdjustments != null)
            {
                _colorAdjustments.postExposure.value = Mathf.Lerp(3.5f, normalExposure, smoothT);
                _colorAdjustments.saturation.value = Mathf.Lerp(-100f, normalSaturation, smoothT);
                _colorAdjustments.contrast.value = Mathf.Lerp(40f, _baseContrast, smoothT);
            }
            if (_lensDistortion != null) _lensDistortion.intensity.value = Mathf.Lerp(-0.6f, 0f, smoothT);
            if (_chromaticAberration != null) _chromaticAberration.intensity.value = Mathf.Lerp(1f, visorBaseChromatic, smoothT);
            if (_vignette != null) _vignette.intensity.value = Mathf.Lerp(0.5f, normalVignette, smoothT);
            if (_lowPassFilter != null) _lowPassFilter.cutoffFrequency = Mathf.Lerp(400f, normalFrequency, smoothT);

            yield return null;
        }

        // 兜底重置
        if (_lowPassFilter != null) _lowPassFilter.cutoffFrequency = normalFrequency;
        if (_colorAdjustments != null) 
        {
            _colorAdjustments.colorFilter.value = _baseColorFilter;
            _colorAdjustments.postExposure.value = normalExposure;
            _colorAdjustments.contrast.value = _baseContrast;
            _colorAdjustments.saturation.value = normalSaturation;
        }
        if (_chromaticAberration != null) _chromaticAberration.intensity.value = visorBaseChromatic;
        if (_lensDistortion != null) _lensDistortion.intensity.value = 0f;
        if (_vignette != null)
        {
            _vignette.intensity.value = normalVignette;
            _vignette.smoothness.value = _baseVignetteSmoothness;
            _vignette.color.value = _baseVignetteColor;
        }

        EnsureVolumeOverrides();
        
        _isRebooting = false; 
    }

    private void EnsureVolumeOverrides()
    {
        if (_colorAdjustments != null)
        {
            _colorAdjustments.active = true;
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.saturation.overrideState = true;
            _colorAdjustments.colorFilter.overrideState = true;
            _colorAdjustments.contrast.overrideState = true;
        }

        if (_vignette != null)
        {
            _vignette.active = true;
            _vignette.intensity.overrideState = true;
            _vignette.smoothness.overrideState = true;
            _vignette.color.overrideState = true;
        }

        if (_chromaticAberration != null)
        {
            _chromaticAberration.active = true;
            _chromaticAberration.intensity.overrideState = true;
        }

        if (_lensDistortion != null)
        {
            _lensDistortion.active = true;
            _lensDistortion.intensity.overrideState = true;
        }

        if (_paniniProjection != null)
        {
            _paniniProjection.active = true;
            _paniniProjection.distance.overrideState = true;
        }
    }

    private void OnDestroy()
    {
        SetDashWarp(0f, 1f);
        ReleaseDashWeaponLag();

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
