#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;

/// <summary>
/// CICADAMATA-like high-contrast combat feedback study for AILURONE.
/// This is an isolated preview only. It does not deal damage and does not touch Spike AI.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikeSystemErrorPreviewController : MonoBehaviour
{
    public enum PreviewMode
    {
        Hipfire,
        Ads,
        Kill
    }

    [Serializable]
    public sealed class BlockSlot
    {
        public MeshRenderer renderer;
        [HideInInspector] public Vector3 direction;
        [HideInInspector] public float distance;
        [HideInInspector] public float startDelay;
        [HideInInspector] public float spin;
        [HideInInspector] public Vector2 size;
        [HideInInspector] public int colorRole;
    }

    [Header("Target Renderers")]
    public Renderer[] bodyRenderers;
    public Renderer[] whiteGhostRenderers;
    public Renderer[] darkGhostRenderers;
    public Transform bodyRoot;
    public Transform whiteGhostRoot;
    public Transform darkGhostRoot;

    [Header("Impact Graphics")]
    public MeshRenderer impactWhite;
    public MeshRenderer impactBlack;
    public MeshRenderer impactAccent;
    public MeshRenderer halftoneResidue;
    public BlockSlot[] blocks;

    [Header("Timing")]
    [Min(0.08f)] public float hipfireDuration = 0.16f;
    [Min(0.12f)] public float adsDuration = 0.29f;
    [Min(0.25f)] public float killDuration = 0.56f;

    [Header("State Colors")]
    [ColorUsage(true, true)] public Color blueState = new Color(0.02f, 0.42f, 1.00f, 1f);
    [ColorUsage(true, true)] public Color magentaStateColor = new Color(1.00f, 0.015f, 0.50f, 1f);
    [ColorUsage(true, true)] public Color yellowAccent = new Color(1.00f, 0.86f, 0.10f, 1f);
    [ColorUsage(true, true)] public Color cyanAccent = new Color(0.04f, 0.95f, 1.00f, 1f);
    public Color hardBlack = new Color(0.008f, 0.010f, 0.016f, 1f);
    public Color hardWhite = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int HitAmountId = Shader.PropertyToID("_HitAmount");
    private static readonly int AdsAmountId = Shader.PropertyToID("_AdsAmount");
    private static readonly int KillAmountId = Shader.PropertyToID("_KillAmount");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");

    private MaterialPropertyBlock _bodyBlock;
    private MaterialPropertyBlock _ghostBlock;
    private MaterialPropertyBlock _fxBlock;
    private PreviewMode _mode;
    private bool _magentaState;
    private bool _playing;
    private float _elapsed;
    private float _duration;
    private float _seed;
    private Color _baseColor;
    private Color _accentColor;
    private Vector3 _bodyBasePosition;
    private Quaternion _bodyBaseRotation;
    private Vector3 _bodyBaseScale;

    public bool IsPlaying => _playing;

    private void Awake()
    {
        EnsureBlocks();
        CacheBodyTransform();
        ConfigureIdle(false);
        HideGraphics();
        HideGhosts();
    }

    public void ConfigureIdle(bool magentaState)
    {
        EnsureBlocks();
        _magentaState = magentaState;
        _baseColor = magentaState ? magentaStateColor : blueState;
        _accentColor = magentaState ? cyanAccent : yellowAccent;
        ApplyBody(0f, 0f, 0f, 1f, 1.0f);
        SetBodyVisible(true);
    }


    public void Play(PreviewMode mode, bool magentaState)
    {
        EnsureBlocks();
        CacheBodyTransform();

        _mode = mode;
        _magentaState = magentaState;
        _baseColor = magentaState ? magentaStateColor : blueState;
        _accentColor = magentaState ? cyanAccent : yellowAccent;
        _duration = mode == PreviewMode.Hipfire ? hipfireDuration : mode == PreviewMode.Ads ? adsDuration : killDuration;
        _elapsed = 0f;
        _seed = UnityEngine.Random.Range(0.15f, 98.0f);
        _playing = true;

        RestoreBodyTransform();
        SetBodyVisible(true);
        HideGraphics();
        HideGhosts();
        ConfigureBlocks(mode);
        Evaluate(0f);
    }

    public void StopPreview()
    {
        _playing = false;
        RestoreBodyTransform();
        HideGraphics();
        HideGhosts();
        ApplyBody(0f, 0f, 0f, 1f, 1.0f);
        SetBodyVisible(true);
    }

    private void Update()
    {
        if (!_playing)
        {
            return;
        }

        _elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
        float normalizedTime = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration));
        Evaluate(normalizedTime);

        if (_elapsed >= _duration)
        {
            StopPreview();
        }
    }

    private void Evaluate(float t)
    {
        switch (_mode)
        {
            case PreviewMode.Hipfire:
                EvaluateHipfire(t);
                break;
            case PreviewMode.Ads:
                EvaluateAds(t);
                break;
            case PreviewMode.Kill:
                EvaluateKill(t);
                break;
        }
    }

    private void EvaluateHipfire(float t)
    {
        float hitAmount = 1f - SmoothRange(0.055f, 0.31f, t);
        float briefGhost = Mathf.Sin(Mathf.Clamp01(t / 0.30f) * Mathf.PI) * (1f - SmoothRange(0.26f, 0.43f, t));
        float bodyKick = Mathf.Sin(Mathf.Clamp01(t / 0.31f) * Mathf.PI) * 0.012f;

        ApplyBody(hitAmount, 0f, 0f, 1f, 1.02f);
        ApplyBodyTransform(new Vector3(bodyKick, -bodyKick * 0.35f, 0f), Quaternion.Euler(0f, 0f, -bodyKick * 130f), Vector3.one);

        ApplyGhosts(
            whiteAlpha: briefGhost * 0.36f,
            darkAlpha: briefGhost * 0.48f,
            glitch: briefGhost * 0.42f,
            whiteOffset: new Vector3(0.036f, 0.012f, 0f),
            darkOffset: new Vector3(-0.030f, -0.010f, 0f)
        );

        EvaluateImpactGraphics(t, 0.78f, 0.38f, -18f, false);
        EvaluateBlocks(t, 5, 0.16f, 0.97f);
    }

    private void EvaluateAds(float t)
    {
        float hitAmount = 1f - SmoothRange(0.08f, 0.27f, t);
        float adsEnvelope = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.035f, 0.76f, t)) * Mathf.PI);
        float recoil = Mathf.Sin(Mathf.Clamp01(t / 0.28f) * Mathf.PI) * 0.022f;

        ApplyBody(hitAmount, adsEnvelope, 0f, 1f, 1.05f);
        ApplyBodyTransform(new Vector3(recoil, -recoil * 0.22f, 0f), Quaternion.Euler(0f, 0f, recoil * -165f), Vector3.one);

        float whiteFlicker = adsEnvelope * (0.68f + 0.18f * Mathf.Sin(t * 76f));
        float darkFlicker = adsEnvelope * (0.58f + 0.16f * Mathf.Cos(t * 63f));
        ApplyGhosts(
            whiteAlpha: Mathf.Clamp01(whiteFlicker),
            darkAlpha: Mathf.Clamp01(darkFlicker),
            glitch: adsEnvelope,
            whiteOffset: new Vector3(0.084f + Mathf.Sin(t * 37f) * 0.018f, 0.018f, 0f),
            darkOffset: new Vector3(-0.072f + Mathf.Cos(t * 31f) * 0.014f, -0.020f, 0f)
        );

        EvaluateImpactGraphics(t, 1.16f, 0.62f, -14f, true);
        EvaluateBlocks(t, 8, 0.10f, 1.18f);
    }

    private void EvaluateKill(float t)
    {
        float hitAmount = 1f - SmoothRange(0.055f, 0.18f, t);
        float killAmount = SmoothRange(0.045f, 0.38f, t) * (1f - SmoothRange(0.70f, 0.96f, t));
        float visibility = t < 0.33f ? 1f : 0f;
        float scaleCollapse = 1f - SmoothRange(0.17f, 0.36f, t) * 0.10f;
        float pull = SmoothRange(0.06f, 0.33f, t);

        ApplyBody(hitAmount, killAmount * 0.55f, killAmount, visibility, 1.08f);
        ApplyBodyTransform(
            new Vector3(Mathf.Sin(t * 88f) * 0.016f * pull, Mathf.Cos(t * 71f) * 0.012f * pull, 0f),
            Quaternion.Euler(0f, 0f, Mathf.Sin(t * 55f) * 2.4f * pull),
            Vector3.one * scaleCollapse
        );

        float ghostEnvelope = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.02f, 0.68f, t)) * Mathf.PI);
        ApplyGhosts(
            whiteAlpha: ghostEnvelope * 0.84f,
            darkAlpha: ghostEnvelope * 0.76f,
            glitch: Mathf.Clamp01(killAmount * 1.25f),
            whiteOffset: new Vector3(Mathf.Lerp(0.035f, 0.23f, pull), 0.026f, 0f),
            darkOffset: new Vector3(Mathf.Lerp(-0.030f, -0.19f, pull), -0.034f, 0f)
        );

        EvaluateImpactGraphics(t, 1.42f, 0.78f, -12f, true);
        EvaluateBlocks(t, blocks == null ? 0 : blocks.Length, 0.055f, 1.48f);
        EvaluateHalftone(t);
    }

    private void EvaluateImpactGraphics(float t, float width, float height, float angle, bool heavy)
    {
        float attack = SmoothRange(0f, heavy ? 0.10f : 0.075f, t);
        float release = 1f - SmoothRange(heavy ? 0.33f : 0.27f, heavy ? 0.70f : 0.62f, t);
        float envelope = Mathf.Clamp01(attack * release);

        if (impactBlack != null)
        {
            impactBlack.enabled = envelope > 0.001f;
            impactBlack.transform.localPosition = new Vector3(0.045f, -0.030f, 0.004f);
            impactBlack.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 3f);
            impactBlack.transform.localScale = new Vector3(width * 1.12f, height * 1.12f, 1f);
            SetFx(impactBlack, hardBlack, envelope * 0.94f, 1.0f);
        }

        if (impactWhite != null)
        {
            impactWhite.enabled = envelope > 0.001f;
            float snap = EaseOutBack(Mathf.Clamp01(t / (heavy ? 0.16f : 0.12f)));
            impactWhite.transform.localPosition = Vector3.zero;
            impactWhite.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            impactWhite.transform.localScale = new Vector3(width * Mathf.Lerp(0.28f, 1f, snap), height * Mathf.Lerp(0.55f, 1f, snap), 1f);
            SetFx(impactWhite, hardWhite, envelope, heavy ? 1.45f : 1.25f);
        }

        if (impactAccent != null)
        {
            float accentDelay = heavy ? 0.07f : 0.10f;
            float accentT = Mathf.Clamp01((t - accentDelay) / (heavy ? 0.50f : 0.42f));
            float accentEnvelope = Mathf.Sin(accentT * Mathf.PI) * (heavy ? 0.82f : 0.54f);
            impactAccent.enabled = t >= accentDelay && accentEnvelope > 0.001f;
            impactAccent.transform.localPosition = new Vector3(-0.055f, 0.038f, -0.002f);
            impactAccent.transform.localRotation = Quaternion.Euler(0f, 0f, angle + 17f);
            impactAccent.transform.localScale = new Vector3(width * (heavy ? 0.72f : 0.54f), height * (heavy ? 0.66f : 0.50f), 1f);
            SetFx(impactAccent, _accentColor, accentEnvelope, 1.15f);
        }
    }

    private void EvaluateBlocks(float t, int visibleCount, float globalDelay, float distanceMultiplier)
    {
        if (blocks == null)
        {
            return;
        }

        visibleCount = Mathf.Clamp(visibleCount, 0, blocks.Length);
        for (int index = 0; index < blocks.Length; index++)
        {
            BlockSlot slot = blocks[index];
            if (slot == null || slot.renderer == null)
            {
                continue;
            }

            float delay = globalDelay + slot.startDelay;
            bool visible = index < visibleCount && t >= delay;
            slot.renderer.enabled = visible;
            if (!visible)
            {
                continue;
            }

            float localTime = Mathf.Clamp01((t - delay) / Mathf.Max(0.08f, 0.91f - delay));
            float envelope = Mathf.Sin(localTime * Mathf.PI) * (1f - SmoothRange(0.72f, 1f, localTime));
            float travel = slot.distance * distanceMultiplier * EaseOutCubic(localTime);
            slot.renderer.transform.localPosition = slot.direction * travel + new Vector3(0f, 0f, -0.006f - index * 0.0004f);
            slot.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, slot.spin * localTime);
            float sizePulse = Mathf.Lerp(0.62f, 1.0f, Mathf.Sin(localTime * Mathf.PI));
            slot.renderer.transform.localScale = new Vector3(slot.size.x * sizePulse, slot.size.y * sizePulse, 1f);

            Color color = slot.colorRole == 0 ? hardWhite : slot.colorRole == 1 ? hardBlack : _accentColor;
            float intensity = slot.colorRole == 0 ? 1.24f : 1.0f;
            SetFx(slot.renderer, color, envelope, intensity);
        }
    }

    private void EvaluateHalftone(float t)
    {
        if (halftoneResidue == null)
        {
            return;
        }

        float localTime = Mathf.Clamp01(Mathf.InverseLerp(0.20f, 0.88f, t));
        float envelope = Mathf.Sin(localTime * Mathf.PI) * (1f - SmoothRange(0.72f, 1f, localTime));
        halftoneResidue.enabled = t >= 0.20f && envelope > 0.001f;
        halftoneResidue.transform.localPosition = new Vector3(0.05f, -0.02f, 0.012f);
        halftoneResidue.transform.localRotation = Quaternion.Euler(0f, 0f, -8f + localTime * 7f);
        halftoneResidue.transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 2.55f, EaseOutCubic(localTime));
        SetFx(halftoneResidue, hardBlack, envelope * 0.52f, 1f);
    }

    private void ConfigureBlocks(PreviewMode mode)
    {
        if (blocks == null)
        {
            return;
        }

        float[] hipfireAngles = { 16f, 154f, 211f, 333f, 89f, 278f, 42f, 187f, 305f, 121f, 245f, 350f };
        float[] adsAngles = { 8f, 38f, 92f, 148f, 194f, 224f, 278f, 326f, 62f, 173f, 252f, 344f };
        float[] killAngles = { 3f, 31f, 66f, 101f, 137f, 169f, 202f, 232f, 263f, 296f, 327f, 349f };
        float[] angleSet = mode == PreviewMode.Hipfire ? hipfireAngles : mode == PreviewMode.Ads ? adsAngles : killAngles;

        UnityEngine.Random.State oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(Mathf.RoundToInt(_seed * 100f) + (int)mode * 307);

        for (int index = 0; index < blocks.Length; index++)
        {
            float angle = angleSet[index % angleSet.Length] + UnityEngine.Random.Range(-5.5f, 5.5f);
            float radians = angle * Mathf.Deg2Rad;
            BlockSlot slot = blocks[index];
            slot.direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;
            slot.distance = mode == PreviewMode.Hipfire ? UnityEngine.Random.Range(0.30f, 0.62f) : mode == PreviewMode.Ads ? UnityEngine.Random.Range(0.48f, 0.94f) : UnityEngine.Random.Range(0.72f, 1.48f);
            slot.startDelay = mode == PreviewMode.Hipfire ? UnityEngine.Random.Range(0.02f, 0.12f) : mode == PreviewMode.Ads ? UnityEngine.Random.Range(0.02f, 0.15f) : UnityEngine.Random.Range(0.00f, 0.14f);
            slot.spin = UnityEngine.Random.Range(-150f, 150f);

            float longSize = mode == PreviewMode.Hipfire ? UnityEngine.Random.Range(0.22f, 0.40f) : mode == PreviewMode.Ads ? UnityEngine.Random.Range(0.28f, 0.52f) : UnityEngine.Random.Range(0.38f, 0.78f);
            float shortSize = longSize * UnityEngine.Random.Range(0.28f, 0.58f);
            if (index % 3 == 0)
            {
                slot.size = new Vector2(shortSize, longSize);
            }
            else
            {
                slot.size = new Vector2(longSize, shortSize);
            }

            slot.colorRole = index % 4 == 0 ? 2 : index % 3 == 0 ? 1 : 0;
        }

        UnityEngine.Random.state = oldState;
    }

    private void ApplyBody(float hitAmount, float adsAmount, float killAmount, float visibility, float intensity)
    {
        EnsureBlocks();
        if (bodyRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in bodyRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            _bodyBlock.Clear();
            _bodyBlock.SetColor(BaseColorId, _baseColor);
            _bodyBlock.SetColor(AccentColorId, _accentColor);
            _bodyBlock.SetFloat(IntensityId, intensity);
            _bodyBlock.SetFloat(HitAmountId, Mathf.Clamp01(hitAmount));
            _bodyBlock.SetFloat(AdsAmountId, Mathf.Clamp01(adsAmount));
            _bodyBlock.SetFloat(KillAmountId, Mathf.Clamp01(killAmount));
            _bodyBlock.SetFloat(SeedId, _seed);
            _bodyBlock.SetFloat(VisibilityId, visibility);
            renderer.SetPropertyBlock(_bodyBlock);
        }
    }

    private void ApplyGhosts(float whiteAlpha, float darkAlpha, float glitch, Vector3 whiteOffset, Vector3 darkOffset)
    {
        if (whiteGhostRoot != null)
        {
            whiteGhostRoot.localPosition = whiteOffset;
        }
        if (darkGhostRoot != null)
        {
            darkGhostRoot.localPosition = darkOffset;
        }

        ApplyGhostRenderers(whiteGhostRenderers, hardWhite, whiteAlpha, glitch, _seed + 0.73f);
        ApplyGhostRenderers(darkGhostRenderers, _accentColor, darkAlpha, glitch * 0.86f, _seed + 4.11f);
    }

    private void ApplyGhostRenderers(Renderer[] renderers, Color color, float alpha, float glitch, float seed)
    {
        EnsureBlocks();
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = alpha > 0.002f;
            _ghostBlock.Clear();
            _ghostBlock.SetColor(BaseColorId, color);
            _ghostBlock.SetFloat(AlphaId, Mathf.Clamp01(alpha));
            _ghostBlock.SetFloat(GlitchAmountId, Mathf.Clamp01(glitch));
            _ghostBlock.SetFloat(SeedId, seed);
            renderer.SetPropertyBlock(_ghostBlock);
        }
    }

    private void SetFx(MeshRenderer renderer, Color color, float alpha, float intensity)
    {
        if (renderer == null)
        {
            return;
        }

        EnsureBlocks();
        Color displayColor = color;
        displayColor.a = Mathf.Clamp01(alpha);
        _fxBlock.Clear();
        _fxBlock.SetColor(BaseColorId, displayColor);
        _fxBlock.SetFloat(IntensityId, Mathf.Max(0f, intensity));
        renderer.SetPropertyBlock(_fxBlock);
    }

    private void ApplyBodyTransform(Vector3 positionOffset, Quaternion rotationOffset, Vector3 scaleMultiplier)
    {
        if (bodyRoot == null)
        {
            return;
        }

        bodyRoot.localPosition = _bodyBasePosition + positionOffset;
        bodyRoot.localRotation = _bodyBaseRotation * rotationOffset;
        bodyRoot.localScale = Vector3.Scale(_bodyBaseScale, scaleMultiplier);
    }

    private void CacheBodyTransform()
    {
        if (bodyRoot == null)
        {
            return;
        }

        _bodyBasePosition = bodyRoot.localPosition;
        _bodyBaseRotation = bodyRoot.localRotation;
        _bodyBaseScale = bodyRoot.localScale;
    }

    private void RestoreBodyTransform()
    {
        if (bodyRoot == null)
        {
            return;
        }

        bodyRoot.localPosition = _bodyBasePosition;
        bodyRoot.localRotation = _bodyBaseRotation;
        bodyRoot.localScale = _bodyBaseScale;
    }

    private void HideGraphics()
    {
        SetRendererEnabled(impactWhite, false);
        SetRendererEnabled(impactBlack, false);
        SetRendererEnabled(impactAccent, false);
        SetRendererEnabled(halftoneResidue, false);
        if (blocks == null)
        {
            return;
        }
        foreach (BlockSlot slot in blocks)
        {
            if (slot != null && slot.renderer != null)
            {
                slot.renderer.enabled = false;
            }
        }
    }

    private void HideGhosts()
    {
        SetRenderersEnabled(whiteGhostRenderers, false);
        SetRenderersEnabled(darkGhostRenderers, false);
        if (whiteGhostRoot != null)
        {
            whiteGhostRoot.localPosition = Vector3.zero;
        }
        if (darkGhostRoot != null)
        {
            darkGhostRoot.localPosition = Vector3.zero;
        }
    }

    private void SetBodyVisible(bool visible)
    {
        SetRenderersEnabled(bodyRenderers, visible);
    }

    private static void SetRendererEnabled(Renderer renderer, bool enabledValue)
    {
        if (renderer != null)
        {
            renderer.enabled = enabledValue;
        }
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabledValue)
    {
        if (renderers == null)
        {
            return;
        }
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = enabledValue;
            }
        }
    }

    private void EnsureBlocks()
    {
        if (_bodyBlock == null)
        {
            _bodyBlock = new MaterialPropertyBlock();
        }
        if (_ghostBlock == null)
        {
            _ghostBlock = new MaterialPropertyBlock();
        }
        if (_fxBlock == null)
        {
            _fxBlock = new MaterialPropertyBlock();
        }
    }

    private static float SmoothRange(float start, float end, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseOutBack(float value)
    {
        value = Mathf.Clamp01(value);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float shifted = value - 1f;
        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hipfireDuration = Mathf.Max(0.08f, hipfireDuration);
        adsDuration = Mathf.Max(0.12f, adsDuration);
        killDuration = Mathf.Max(0.25f, killDuration);
    }
#endif
}
