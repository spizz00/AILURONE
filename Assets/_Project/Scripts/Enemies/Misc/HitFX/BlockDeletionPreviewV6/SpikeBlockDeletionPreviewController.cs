#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;

/// <summary>
/// Isolated AILURONE hit/kill feedback study.
/// V6 splits ADS feedback into a readability-first non-lethal hit and a stronger
/// full-charge lethal hit. Final death remains a separate block-deletion event.
/// It does not deal damage and does not touch the formal Spike prefab or AI.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikeBlockDeletionPreviewControllerV6 : MonoBehaviour
{
    public enum PreviewMode
    {
        Hipfire,
        AdsNonLethal,
        AdsLethal,
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
    public Renderer[] accentGhostRenderers;
    public Transform bodyRoot;
    public Transform whiteGhostRoot;
    public Transform accentGhostRoot;

    [Header("Impact Graphics")]
    public MeshRenderer impactWhite;
    public MeshRenderer impactBlack;
    public MeshRenderer impactAccent;
    public MeshRenderer checkerResidue;
    public BlockSlot[] blocks;

    [Header("Timing — V6 ADS Split")]
    [Min(0.10f)] public float hipfireDuration = 0.165f;
    [Min(0.12f)] public float adsNonLethalDuration = 0.18f;
    [Min(0.14f)] public float adsLethalDuration = 0.24f;
    [Min(0.30f)] public float killDuration = 0.46f;

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
    private static readonly int ErrorAmountId = Shader.PropertyToID("_ErrorAmount");
    private static readonly int SliceAmountId = Shader.PropertyToID("_SliceAmount");
    private static readonly int DeleteAmountId = Shader.PropertyToID("_DeleteAmount");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");

    private MaterialPropertyBlock _bodyBlock;
    private MaterialPropertyBlock _ghostBlock;
    private MaterialPropertyBlock _fxBlock;
    private PreviewMode _mode;
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
        EnsurePropertyBlocks();
        CacheBodyTransform();
        ConfigureIdle(false);
        HideGraphics();
        HideGhosts();
    }

    public void ConfigureIdle(bool magentaState)
    {
        EnsurePropertyBlocks();
        _baseColor = magentaState ? magentaStateColor : blueState;
        _accentColor = magentaState ? cyanAccent : yellowAccent;
        ApplyBody(0f, 0f, 0f, 1f, 1f);
        SetBodyVisible(true);
    }

    public void Play(PreviewMode mode, bool magentaState)
    {
        EnsurePropertyBlocks();
        CacheBodyTransform();

        _mode = mode;
        _baseColor = magentaState ? magentaStateColor : blueState;
        _accentColor = magentaState ? cyanAccent : yellowAccent;
        _duration = mode == PreviewMode.Hipfire
            ? hipfireDuration
            : mode == PreviewMode.AdsNonLethal
                ? adsNonLethalDuration
                : mode == PreviewMode.AdsLethal
                    ? adsLethalDuration
                    : killDuration;

        _elapsed = 0f;
        _seed = UnityEngine.Random.Range(0.25f, 91f);
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
        ApplyBody(0f, 0f, 0f, 1f, 1f);
        SetBodyVisible(true);
    }

    private void Update()
    {
        if (!_playing)
        {
            return;
        }

        _elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
        float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration));
        Evaluate(t);

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
            case PreviewMode.AdsNonLethal:
                EvaluateAdsNonLethal(t);
                break;
            case PreviewMode.AdsLethal:
                EvaluateAdsLethal(t);
                break;
            case PreviewMode.Kill:
                EvaluateKill(t);
                break;
        }
    }

    private void EvaluateHipfire(float t)
    {
        float attack = SmoothRange(0f, 0.055f, t);
        float release = 1f - SmoothRange(0.30f, 0.86f, t);
        float error = Mathf.Clamp01(attack * release);
        float microSlice = Mathf.Sin(Mathf.Clamp01(t / 0.32f) * Mathf.PI) * 0.16f;
        float kick = Mathf.Sin(Mathf.Clamp01(t / 0.36f) * Mathf.PI) * 0.010f;

        ApplyBody(error, microSlice, 0f, 1f, 1.02f);
        ApplyBodyTransform(
            new Vector3(kick, -kick * 0.28f, 0f),
            Quaternion.Euler(0f, 0f, -kick * 100f),
            Vector3.one
        );

        // Hipfire deliberately avoids a large displaced duplicate silhouette.
        float tinyGhost = Mathf.Sin(Mathf.Clamp01(t / 0.30f) * Mathf.PI) * 0.18f;
        ApplyGhosts(
            tinyGhost,
            tinyGhost * 0.72f,
            tinyGhost * 0.35f,
            new Vector3(0.025f, 0.006f, 0f),
            new Vector3(-0.020f, -0.005f, 0f)
        );

        EvaluateImpactGraphics(t, PreviewMode.Hipfire);
        EvaluateBlocks(t, 5, 0.055f, 0.88f);
    }

    private void EvaluateAdsNonLethal(float t)
    {
        // Readability-first ADS hit: the target must remain trackable after the hit.
        float attack = SmoothRange(0f, 0.045f, t);
        float release = 1f - SmoothRange(0.46f, 0.82f, t);
        float error = Mathf.Clamp01(attack * release * 0.62f);
        float sliceEnvelope = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.02f, 0.62f, t)) * Mathf.PI) * 0.52f;
        float recoil = Mathf.Sin(Mathf.Clamp01(t / 0.28f) * Mathf.PI) * 0.010f;

        ApplyBody(error, sliceEnvelope, 0f, 1f, 1.015f);
        ApplyBodyTransform(
            new Vector3(recoil, -recoil * 0.16f, 0f),
            Quaternion.Euler(0f, 0f, recoil * -82f),
            Vector3.one
        );

        ApplyGhosts(
            sliceEnvelope * 0.26f,
            sliceEnvelope * 0.20f,
            sliceEnvelope * 0.34f,
            new Vector3(0.038f, 0.007f, 0f),
            new Vector3(-0.031f, -0.006f, 0f)
        );

        EvaluateImpactGraphics(t, PreviewMode.AdsNonLethal);
        EvaluateBlocks(t, 4, 0.035f, 0.72f);
    }

    private void EvaluateAdsLethal(float t)
    {
        // Full-charge ADS impact: strong enough to sell a one-shot Spike kill,
        // but the body remains visible. Final deletion belongs to Kill mode.
        float attack = SmoothRange(0f, 0.050f, t);
        float release = 1f - SmoothRange(0.52f, 0.88f, t);
        float error = Mathf.Clamp01(attack * release * 0.90f);
        float sliceEnvelope = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.02f, 0.74f, t)) * Mathf.PI) * 0.68f;
        float recoil = Mathf.Sin(Mathf.Clamp01(t / 0.30f) * Mathf.PI) * 0.015f;

        ApplyBody(error, sliceEnvelope, 0.10f * error, 1f, 1.035f);
        ApplyBodyTransform(
            new Vector3(recoil, -recoil * 0.18f, 0f),
            Quaternion.Euler(0f, 0f, recoil * -98f),
            Vector3.one
        );

        ApplyGhosts(
            sliceEnvelope * 0.40f,
            sliceEnvelope * 0.32f,
            sliceEnvelope * 0.50f,
            new Vector3(0.052f, 0.009f, 0f),
            new Vector3(-0.045f, -0.010f, 0f)
        );

        EvaluateImpactGraphics(t, PreviewMode.AdsLethal);
        EvaluateBlocks(t, 7, 0.035f, 0.88f);
    }

    private void EvaluateKill(float t)
    {
        float attack = SmoothRange(0f, 0.045f, t);
        float errorRelease = 1f - SmoothRange(0.66f, 0.95f, t);
        float error = Mathf.Clamp01(attack * errorRelease);
        float slice = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.015f, 0.63f, t)) * Mathf.PI);
        float deletion = SmoothRange(0.19f, 0.73f, t);
        float shakeEnvelope = 1f - SmoothRange(0.36f, 0.78f, t);
        float collapse = 1f - SmoothRange(0.20f, 0.72f, t) * 0.08f;

        ApplyBody(error, slice, deletion, 1f, 1.06f);
        ApplyBodyTransform(
            new Vector3(
                Mathf.Sin(t * 86f) * 0.010f * shakeEnvelope,
                Mathf.Cos(t * 73f) * 0.008f * shakeEnvelope,
                0f
            ),
            Quaternion.Euler(0f, 0f, Mathf.Sin(t * 61f) * 1.4f * shakeEnvelope),
            Vector3.one * collapse
        );

        float ghostEnvelope = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.01f, 0.56f, t)) * Mathf.PI);
        ApplyGhosts(
            ghostEnvelope * 0.70f,
            ghostEnvelope * 0.58f,
            ghostEnvelope * 0.92f,
            new Vector3(Mathf.Lerp(0.035f, 0.13f, slice), 0.018f, 0f),
            new Vector3(Mathf.Lerp(-0.030f, -0.11f, slice), -0.022f, 0f)
        );

        EvaluateImpactGraphics(t, PreviewMode.Kill);
        EvaluateBlocks(t, blocks == null ? 0 : blocks.Length, 0.025f, 1.28f);
        EvaluateResidue(t);
    }

    private void EvaluateImpactGraphics(float t, PreviewMode mode)
    {
        bool heavy = mode == PreviewMode.AdsLethal || mode == PreviewMode.Kill;
        bool kill = mode == PreviewMode.Kill;

        float attackEnd = mode == PreviewMode.Hipfire ? 0.075f : mode == PreviewMode.AdsNonLethal ? 0.055f : 0.060f;
        float releaseStart = mode == PreviewMode.Hipfire ? 0.25f : mode == PreviewMode.AdsNonLethal ? 0.22f : kill ? 0.45f : 0.40f;
        float releaseEnd = mode == PreviewMode.Hipfire ? 0.73f : mode == PreviewMode.AdsNonLethal ? 0.62f : kill ? 0.83f : 0.72f;
        float envelope = SmoothRange(0f, attackEnd, t) * (1f - SmoothRange(releaseStart, releaseEnd, t));

        float width;
        float height;
        if (mode == PreviewMode.Hipfire)
        {
            width = 0.86f;
            height = 0.58f;
        }
        else if (mode == PreviewMode.AdsNonLethal)
        {
            width = 0.78f;
            height = 0.54f;
        }
        else if (mode == PreviewMode.AdsLethal)
        {
            width = 1.00f;
            height = 0.68f;
        }
        else
        {
            width = 1.42f;
            height = 1.02f;
        }

        float snap = EaseOutBack(Mathf.Clamp01(t / (heavy ? 0.12f : 0.09f)));

        if (impactBlack != null)
        {
            impactBlack.enabled = envelope > 0.001f;
            impactBlack.transform.localPosition = new Vector3(0.040f, -0.028f, 0.004f);
            impactBlack.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            impactBlack.transform.localScale = new Vector3(width * 1.08f, height * 1.08f, 1f);
            SetFx(impactBlack, hardBlack, envelope * (mode == PreviewMode.AdsNonLethal ? 0.72f : 0.96f), 1f);
        }

        if (impactWhite != null)
        {
            impactWhite.enabled = envelope > 0.001f;
            impactWhite.transform.localPosition = Vector3.zero;
            impactWhite.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            impactWhite.transform.localScale = new Vector3(
                width * Mathf.Lerp(0.42f, 1f, snap),
                height * Mathf.Lerp(0.62f, 1f, snap),
                1f
            );
            float whiteIntensity = mode == PreviewMode.AdsNonLethal ? 0.86f : heavy ? 1.20f : 1.10f;
            SetFx(impactWhite, hardWhite, envelope * (mode == PreviewMode.AdsNonLethal ? 0.78f : 1f), whiteIntensity);
        }

        if (impactAccent != null)
        {
            float accentDelay = mode == PreviewMode.Hipfire ? 0.095f : mode == PreviewMode.AdsNonLethal ? 0.075f : 0.070f;
            float accentDuration = mode == PreviewMode.AdsNonLethal ? 0.28f : kill ? 0.54f : 0.40f;
            float local = Mathf.Clamp01((t - accentDelay) / Mathf.Max(0.05f, accentDuration));
            float accentPeak = mode == PreviewMode.AdsNonLethal ? 0.30f : mode == PreviewMode.AdsLethal ? 0.50f : mode == PreviewMode.Hipfire ? 0.44f : 0.68f;
            float accentEnvelope = Mathf.Sin(local * Mathf.PI) * accentPeak;
            impactAccent.enabled = t >= accentDelay && accentEnvelope > 0.001f;
            impactAccent.transform.localPosition = new Vector3(-0.030f, 0.025f, -0.002f);
            impactAccent.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);
            impactAccent.transform.localScale = new Vector3(width * 0.70f, height * 0.72f, 1f);
            SetFx(impactAccent, _accentColor, accentEnvelope, 1.02f);
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

            float localTime = Mathf.Clamp01((t - delay) / Mathf.Max(0.08f, 0.93f - delay));
            float envelope = Mathf.Sin(localTime * Mathf.PI) * (1f - SmoothRange(0.76f, 1f, localTime));
            float travel = slot.distance * distanceMultiplier * EaseOutCubic(localTime);
            slot.renderer.transform.localPosition = slot.direction * travel + new Vector3(0f, 0f, -0.006f - index * 0.0004f);
            slot.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, slot.spin * localTime);
            float pulse = Mathf.Lerp(0.74f, 1f, Mathf.Sin(localTime * Mathf.PI));
            slot.renderer.transform.localScale = new Vector3(slot.size.x * pulse, slot.size.y * pulse, 1f);

            Color color = slot.colorRole == 0 ? hardWhite : slot.colorRole == 1 ? hardBlack : _accentColor;
            float intensity = slot.colorRole == 0 ? 1.18f : 1f;
            SetFx(slot.renderer, color, envelope, intensity);
        }
    }

    private void EvaluateResidue(float t)
    {
        if (checkerResidue == null)
        {
            return;
        }

        float local = Mathf.Clamp01(Mathf.InverseLerp(0.32f, 0.96f, t));
        float envelope = Mathf.Sin(local * Mathf.PI) * (1f - SmoothRange(0.72f, 1f, local));
        checkerResidue.enabled = t >= 0.32f && envelope > 0.001f;
        checkerResidue.transform.localPosition = new Vector3(0.02f, -0.01f, 0.012f);
        checkerResidue.transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
        checkerResidue.transform.localScale = Vector3.one * Mathf.Lerp(0.90f, 2.25f, EaseOutCubic(local));
        SetFx(checkerResidue, hardBlack, envelope * 0.48f, 1f);
    }

    private void ConfigureBlocks(PreviewMode mode)
    {
        if (blocks == null)
        {
            return;
        }

        float[] hipfireAngles = { 18f, 148f, 208f, 319f, 82f, 260f, 39f, 183f, 301f, 118f, 238f, 346f, 64f, 165f, 284f, 332f };
        float[] adsAngles = { 8f, 36f, 78f, 124f, 168f, 207f, 246f, 286f, 329f, 55f, 146f, 228f, 310f, 98f, 191f, 351f };
        float[] killAngles = { 4f, 27f, 54f, 80f, 107f, 134f, 161f, 187f, 213f, 239f, 266f, 292f, 318f, 342f, 71f, 199f };
        float[] angleSet = mode == PreviewMode.Hipfire ? hipfireAngles : mode == PreviewMode.Kill ? killAngles : adsAngles;

        UnityEngine.Random.State oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(Mathf.RoundToInt(_seed * 100f) + (int)mode * 409);

        for (int index = 0; index < blocks.Length; index++)
        {
            BlockSlot slot = blocks[index];
            if (slot == null)
            {
                continue;
            }

            float angle = angleSet[index % angleSet.Length] + UnityEngine.Random.Range(-4f, 4f);
            float radians = angle * Mathf.Deg2Rad;
            slot.direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;

            if (mode == PreviewMode.Hipfire)
            {
                slot.distance = UnityEngine.Random.Range(0.26f, 0.50f);
                slot.startDelay = UnityEngine.Random.Range(0.00f, 0.055f);
            }
            else if (mode == PreviewMode.AdsNonLethal)
            {
                slot.distance = UnityEngine.Random.Range(0.30f, 0.52f);
                slot.startDelay = UnityEngine.Random.Range(0.00f, 0.045f);
            }
            else if (mode == PreviewMode.AdsLethal)
            {
                slot.distance = UnityEngine.Random.Range(0.36f, 0.64f);
                slot.startDelay = UnityEngine.Random.Range(0.00f, 0.055f);
            }
            else
            {
                slot.distance = UnityEngine.Random.Range(0.66f, 1.22f);
                slot.startDelay = UnityEngine.Random.Range(0.00f, 0.085f);
            }

            slot.spin = UnityEngine.Random.Range(-92f, 92f);

            float sizeBase;
            if (mode == PreviewMode.Hipfire)
            {
                sizeBase = UnityEngine.Random.Range(0.20f, 0.34f);
            }
            else if (mode == PreviewMode.AdsNonLethal)
            {
                sizeBase = UnityEngine.Random.Range(0.22f, 0.34f);
            }
            else if (mode == PreviewMode.AdsLethal)
            {
                sizeBase = UnityEngine.Random.Range(0.25f, 0.40f);
            }
            else
            {
                // First four kill blocks are deliberately large body-replacement pieces.
                sizeBase = index < 4
                    ? UnityEngine.Random.Range(0.62f, 0.88f)
                    : UnityEngine.Random.Range(0.30f, 0.56f);
            }

            float aspect = UnityEngine.Random.Range(0.55f, 1.35f);
            slot.size = index % 2 == 0
                ? new Vector2(sizeBase * aspect, sizeBase)
                : new Vector2(sizeBase, sizeBase * aspect);

            slot.colorRole = index % 5 == 0 ? 2 : index % 4 == 0 ? 1 : 0;
        }

        UnityEngine.Random.state = oldState;
    }

    private void ApplyBody(float errorAmount, float sliceAmount, float deleteAmount, float visibility, float intensity)
    {
        EnsurePropertyBlocks();
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
            _bodyBlock.SetFloat(ErrorAmountId, Mathf.Clamp01(errorAmount));
            _bodyBlock.SetFloat(SliceAmountId, Mathf.Clamp01(sliceAmount));
            _bodyBlock.SetFloat(DeleteAmountId, Mathf.Clamp01(deleteAmount));
            _bodyBlock.SetFloat(SeedId, _seed);
            _bodyBlock.SetFloat(VisibilityId, Mathf.Clamp01(visibility));
            renderer.SetPropertyBlock(_bodyBlock);
        }
    }

    private void ApplyGhosts(float whiteAlpha, float accentAlpha, float glitch, Vector3 whiteOffset, Vector3 accentOffset)
    {
        if (whiteGhostRoot != null)
        {
            whiteGhostRoot.localPosition = whiteOffset;
        }
        if (accentGhostRoot != null)
        {
            accentGhostRoot.localPosition = accentOffset;
        }

        ApplyGhostRenderers(whiteGhostRenderers, hardWhite, whiteAlpha, glitch, _seed + 0.73f);
        ApplyGhostRenderers(accentGhostRenderers, _accentColor, accentAlpha, glitch * 0.84f, _seed + 4.11f);
    }

    private void ApplyGhostRenderers(Renderer[] renderers, Color color, float alpha, float glitch, float seed)
    {
        EnsurePropertyBlocks();
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

        EnsurePropertyBlocks();
        Color displayed = color;
        displayed.a = Mathf.Clamp01(alpha);
        _fxBlock.Clear();
        _fxBlock.SetColor(BaseColorId, displayed);
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
        SetRendererEnabled(checkerResidue, false);

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
        SetRenderersEnabled(accentGhostRenderers, false);
        if (whiteGhostRoot != null)
        {
            whiteGhostRoot.localPosition = Vector3.zero;
        }
        if (accentGhostRoot != null)
        {
            accentGhostRoot.localPosition = Vector3.zero;
        }
    }

    private void SetBodyVisible(bool visible)
    {
        SetRenderersEnabled(bodyRenderers, visible);
    }

    private static void SetRendererEnabled(Renderer renderer, bool value)
    {
        if (renderer != null)
        {
            renderer.enabled = value;
        }
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool value)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = value;
            }
        }
    }

    private void EnsurePropertyBlocks()
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
        hipfireDuration = Mathf.Max(0.10f, hipfireDuration);
        adsNonLethalDuration = Mathf.Max(0.12f, adsNonLethalDuration);
        adsLethalDuration = Mathf.Max(0.14f, adsLethalDuration);
        killDuration = Mathf.Max(0.30f, killDuration);
    }
#endif
}
