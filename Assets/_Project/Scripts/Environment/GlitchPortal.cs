#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.SceneManagement;

public class GlitchPortal : MonoBehaviour
{
    [System.Serializable]
    public class PortalStageSettings
    {
        [Header("基础物理/视听")]
        public Vector3 portalScale = new Vector3(0.1f, 1f, 1f);
        public float lightIntensity = 10f;
        public float audioPitch = 0.8f;
        public float audioVolume = 1f; 

        [Header("新增：色差重影撕裂 (Ghosting)")]
        [Tooltip("红蓝重影的错位距离 (0为完全重合)")]
        public float ghostSpread = 0f;
        [Tooltip("重影抽搐的暴力程度 (0为不抽搐)")]
        public float ghostJitter = 0f;
        [Tooltip("中心红光频闪强度 (0-1)")]
        public float lightFlicker = 0f; 
    }

    [Header("阶段参数配置")]
    public PortalStageSettings[] stages = new PortalStageSettings[4];
    public float transitionSpeed = 5f;

    [Header("关联组件")]
    public Light portalLight;
    public AudioSource portalAudioSource;

    private int _currentStage = 0;
    
    // 平滑目标
    private Vector3 _targetScale;
    private float _targetLightIntensity;
    private float _targetPitch;
    private float _targetVolume;
    private float _targetGhostSpread;
    private float _targetGhostJitter;
    private float _targetLightFlicker;

    // 当前值
    private float _currentGhostSpread;
    private float _currentGhostJitter;
    private float _baseLightIntensity;
    private float _currentFlickerAmount;

    // 自动生成的重影切片
    private GameObject _redGhost;
    private GameObject _cyanGhost;
    private ExitPortalFlowVisualController _flowVisual;

    void Awake()
    {
        if (SceneManager.GetActiveScene().name != "Tutorial")
        {
            return;
        }

        _flowVisual = GetComponent<ExitPortalFlowVisualController>();
        if (_flowVisual == null)
        {
            _flowVisual = gameObject.AddComponent<ExitPortalFlowVisualController>();
        }

        Vector3 fixedScale = transform.localScale;
        if (stages != null &&
            stages.Length > 0 &&
            stages[stages.Length - 1] != null)
        {
            fixedScale = stages[stages.Length - 1].portalScale;
        }

        _flowVisual.Initialize(
            fixedScale,
            portalLight,
            portalAudioSource,
            Mathf.Max(1, stages != null ? stages.Length - 1 : 3)
        );
    }

    void Start()
    {
        if (_flowVisual != null)
        {
            _flowVisual.SetStage(0, true);
            return;
        }

        CreateGhostSlices();
        ApplyStageSettings(0, true);
    }

    // ==========================================
    // 💡 黑魔法：自动克隆网格并生成红蓝切片
    // ==========================================
    private void CreateGhostSlices()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) return;

        // 创建红色重影 (微微靠前防穿模)
        _redGhost = new GameObject("RedGhost");
        _redGhost.transform.SetParent(transform);
        _redGhost.transform.localPosition = new Vector3(0, 0, 0.05f); 
        _redGhost.transform.localRotation = Quaternion.identity;
        _redGhost.transform.localScale = Vector3.one;

        MeshFilter rMf = _redGhost.AddComponent<MeshFilter>();
        rMf.sharedMesh = mf.sharedMesh;
        MeshRenderer rMr = _redGhost.AddComponent<MeshRenderer>();
        rMr.material = CreateUnlitMaterial(new Color(1f, 0f, 0.2f, 0.8f)); // 赛博红

        // 创建青色重影 (微微靠后防穿模)
        _cyanGhost = new GameObject("CyanGhost");
        _cyanGhost.transform.SetParent(transform);
        _cyanGhost.transform.localPosition = new Vector3(0, 0, -0.05f); 
        _cyanGhost.transform.localRotation = Quaternion.identity;
        _cyanGhost.transform.localScale = Vector3.one;

        MeshFilter cMf = _cyanGhost.AddComponent<MeshFilter>();
        cMf.sharedMesh = mf.sharedMesh;
        MeshRenderer cMr = _cyanGhost.AddComponent<MeshRenderer>();
        cMr.material = CreateUnlitMaterial(new Color(0f, 1f, 1f, 0.8f)); // 赛博青
        
        _redGhost.SetActive(false);
        _cyanGhost.SetActive(false);
    }

    private Material CreateUnlitMaterial(Color color)
    {
        // 自动兼容你的 URP 渲染管线
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
        
        Material mat = new Material(unlitShader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        
        // 开启透明渲染模式防止黑边
        mat.SetFloat("_Surface", 1); 
        return mat;
    }

    void Update()
    {
        if (_flowVisual != null)
        {
            return;
        }

        // 1. 基础物理视听过渡
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * transitionSpeed);
        
        if (portalAudioSource != null && portalAudioSource.isPlaying)
        {
            portalAudioSource.pitch = Mathf.Lerp(portalAudioSource.pitch, _targetPitch, Time.deltaTime * transitionSpeed);
            portalAudioSource.volume = Mathf.Lerp(portalAudioSource.volume, _targetVolume, Time.deltaTime * transitionSpeed);
        }

        // 2. 报错灯光频闪
        if (portalLight != null)
        {
            _baseLightIntensity = Mathf.Lerp(_baseLightIntensity, _targetLightIntensity, Time.deltaTime * transitionSpeed);
            _currentFlickerAmount = Mathf.Lerp(_currentFlickerAmount, _targetLightFlicker, Time.deltaTime * transitionSpeed);

            if (_currentFlickerAmount > 0.01f)
            {
                float noise = Mathf.PerlinNoise(Time.time * 30f, 0f); 
                portalLight.intensity = _baseLightIntensity * Mathf.Lerp(1f - _currentFlickerAmount, 1f, noise);
            }
            else
            {
                portalLight.intensity = _baseLightIntensity;
            }
        }

        // 3. 红蓝错位重影引擎 (最核心视觉)
        if (_redGhost != null && _cyanGhost != null)
        {
            _currentGhostSpread = Mathf.Lerp(_currentGhostSpread, _targetGhostSpread, Time.deltaTime * transitionSpeed);
            _currentGhostJitter = Mathf.Lerp(_currentGhostJitter, _targetGhostJitter, Time.deltaTime * transitionSpeed);

            if (_currentGhostSpread > 0.01f || _currentGhostJitter > 0.01f)
            {
                _redGhost.SetActive(true);
                _cyanGhost.SetActive(true);

                // 生成极高频的水平神经质抽搐
                float jitterX = (Mathf.PerlinNoise(Time.time * 50f, 0f) - 0.5f) * 2f * _currentGhostJitter;
                float jitterY = (Mathf.PerlinNoise(0f, Time.time * 50f) - 0.5f) * 2f * (_currentGhostJitter * 0.1f); // Y轴抖动弱一点

                // 向两边撕裂
                _redGhost.transform.localPosition = new Vector3(_currentGhostSpread + jitterX, jitterY, 0.05f);
                _cyanGhost.transform.localPosition = new Vector3(-_currentGhostSpread - jitterX, -jitterY, -0.05f);
                
                // 偶尔随机隐藏一帧，模拟极度接触不良 (频闪断层)
                bool glitchFlicker = Random.value > (_currentGhostJitter * 0.4f);
                _redGhost.SetActive(glitchFlicker);
                _cyanGhost.SetActive(glitchFlicker);
            }
            else
            {
                _redGhost.SetActive(false);
                _cyanGhost.SetActive(false);
            }
        }
    }

    public void SetStage(int stageIndex)
    {
        if (stageIndex < 0 || stages == null || stageIndex >= stages.Length) return;
        _currentStage = stageIndex;

        if (_flowVisual != null)
        {
            _flowVisual.SetStage(_currentStage, false);
            return;
        }

        ApplyStageSettings(_currentStage, false);
    }

    private void ApplyStageSettings(int index, bool instant)
    {
        if (index >= stages.Length || stages[index] == null) return;

        _targetScale = stages[index].portalScale;
        _targetLightIntensity = stages[index].lightIntensity;
        _targetPitch = stages[index].audioPitch;
        _targetVolume = stages[index].audioVolume;
        
        _targetGhostSpread = stages[index].ghostSpread;
        _targetGhostJitter = stages[index].ghostJitter;
        _targetLightFlicker = stages[index].lightFlicker;

        if (instant)
        {
            transform.localScale = _targetScale;
            _baseLightIntensity = _targetLightIntensity;
            if (portalLight != null) portalLight.intensity = _targetLightIntensity;
            
            if (portalAudioSource != null)
            {
                portalAudioSource.pitch = _targetPitch;
                portalAudioSource.volume = _targetVolume;
            }
        }
    }
}
