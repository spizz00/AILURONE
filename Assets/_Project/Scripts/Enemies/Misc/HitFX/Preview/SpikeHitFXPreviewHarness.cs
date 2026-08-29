#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Flat-Plate 隔离预览入口。
/// 1/2 只用于验证腰射 V3；3/4 保留 ADS 作为强度参照。
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikeHitFXPreviewHarness : MonoBehaviour
{
    public Transform hitAnchor;
    public Renderer[] stateRenderers;
    public SpikeHitFXPrototypeController prototype;

    [Header("装甲板状态通道")]
    public Color blueStateColor = new Color(0.03f, 0.42f, 1f, 1f);
    public float blueEmission = 2.0f;
    public Color magentaStateColor = new Color(1f, 0.02f, 0.48f, 1f);
    public float magentaEmission = 4.0f;

    [Header("自动循环")]
    public bool loopPreview;
    public float loopInterval = 1.15f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _block;
    private float _loopTimer;
    private int _loopIndex;
    private string _lastMode = "Blue Hipfire V3";

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        ShowBlueHipfire();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) ShowBlueHipfire();
            if (keyboard.digit2Key.wasPressedThisFrame) ShowMagentaHipfire();
            if (keyboard.digit3Key.wasPressedThisFrame) ShowBlueAds();
            if (keyboard.digit4Key.wasPressedThisFrame) ShowMagentaAds();
            if (keyboard.rKey.wasPressedThisFrame) loopPreview = !loopPreview;
            if (keyboard.cKey.wasPressedThisFrame && prototype != null) prototype.StopPreview();
        }

        if (!loopPreview)
        {
            return;
        }

        _loopTimer += Time.unscaledDeltaTime;
        if (_loopTimer < Mathf.Max(0.25f, loopInterval))
        {
            return;
        }

        _loopTimer = 0f;
        _loopIndex = (_loopIndex + 1) % 4;
        switch (_loopIndex)
        {
            case 0: ShowBlueHipfire(); break;
            case 1: ShowMagentaHipfire(); break;
            case 2: ShowBlueAds(); break;
            default: ShowMagentaAds(); break;
        }
    }

    [ContextMenu("Preview/1 Blue Hipfire V3")]
    public void ShowBlueHipfire()
    {
        _lastMode = "Blue Hipfire V3";
        Play(false, false);
    }

    [ContextMenu("Preview/2 Magenta Hipfire V3")]
    public void ShowMagentaHipfire()
    {
        _lastMode = "Magenta Hipfire V3";
        Play(false, true);
    }

    [ContextMenu("Preview/3 Blue ADS Reference")]
    public void ShowBlueAds()
    {
        _lastMode = "Blue ADS Reference";
        Play(true, false);
    }

    [ContextMenu("Preview/4 Magenta ADS Reference")]
    public void ShowMagentaAds()
    {
        _lastMode = "Magenta ADS Reference";
        Play(true, true);
    }

    private void Play(bool ads, bool magenta)
    {
        ApplyState(magenta ? magentaStateColor : blueStateColor, magenta ? magentaEmission : blueEmission);
        if (prototype == null || hitAnchor == null)
        {
            return;
        }

        prototype.transform.SetPositionAndRotation(hitAnchor.position, hitAnchor.rotation);
        prototype.Play(ads, magenta);
    }

    private void ApplyState(Color color, float emission)
    {
        if (stateRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in stateRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            _block.Clear();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _block.SetColor(EmissionColorId, color * emission);
            renderer.SetPropertyBlock(_block);
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(18, 18, 430, 174), "Hipfire V3 — Flat-Plate Preview");
        GUI.Label(new Rect(34, 48, 390, 22), "1  蓝色状态 / 腰射 V3（主要验收）");
        GUI.Label(new Rect(34, 70, 390, 22), "2  洋红状态 / 腰射 V3（主要验收）");
        GUI.Label(new Rect(34, 92, 390, 22), "3  蓝色状态 / ADS（仅作参照）");
        GUI.Label(new Rect(34, 114, 390, 22), "4  洋红状态 / ADS（仅作参照）");
        GUI.Label(new Rect(34, 136, 390, 22), "R 自动循环    C 清除");
        GUI.Label(new Rect(34, 158, 390, 22), "Current: " + _lastMode);
    }
}
