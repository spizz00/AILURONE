#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;

/// <summary>
/// AILURONE Spike 受击隔离预览 V3。
/// 腰射：斜向压入核 -> 不对称装甲裂片 -> 单段局部余波。
/// ADS：保持 V1/V2 已认可的重击结构，不在本轮改动。
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikeHitFXPrototypeController : MonoBehaviour
{
    [Serializable]
    public sealed class ShardSlot
    {
        public MeshRenderer renderer;
        [HideInInspector] public Vector3 direction;
        [HideInInspector] public float speed;
        [HideInInspector] public float spin;
        [HideInInspector] public float startDelay;
        [HideInInspector] public float size;
    }

    [Header("腰射 V3：斜向切入结构")]
    public MeshRenderer hipfireImpact;
    public MeshRenderer[] hipfireSplitCuts;
    public MeshRenderer[] hipfireArcSegments;
    public ShardSlot[] hipfireShards;

    [Header("ADS：冻结已认可结构")]
    public MeshRenderer impactCore;
    public MeshRenderer entryCut;
    public MeshRenderer[] adsArcSegments;
    public ShardSlot[] shards;

    [Header("腰射 V3 节奏")]
    [Min(0.10f)] public float hipfireDuration = 0.245f;
    public Vector2 hipfireImpactPeak = new Vector2(0.72f, 0.28f);
    [Range(1, 6)] public int hipfireVisibleShards = 4;

    [Header("ADS 节奏（冻结）")]
    [Min(0.10f)] public float adsDuration = 0.46f;
    public Vector2 adsCorePeak = new Vector2(0.86f, 0.46f);
    [Range(1, 12)] public int adsVisibleShards = 11;

    [Header("视觉颜色")]
    [ColorUsage(true, true)] public Color warmCore = new Color(1.00f, 0.76f, 0.33f, 1f);
    [ColorUsage(true, true)] public Color cyanAccent = new Color(0.08f, 0.96f, 1.00f, 1f);
    [ColorUsage(true, true)] public Color warmAccent = new Color(1.00f, 0.68f, 0.22f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private MaterialPropertyBlock _block;
    private bool _playing;
    private bool _ads;
    private float _elapsed;
    private float _duration;
    private Color _accent;
    private int _hipfireVariation;

    public bool IsPlaying => _playing;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        HideAll();
    }

    public void Play(bool ads, bool magentaState)
    {
        EnsureBlock();
        _ads = ads;
        _accent = magentaState ? cyanAccent : warmAccent;
        _elapsed = 0f;
        _duration = ads ? adsDuration : hipfireDuration;
        _playing = true;

        if (_ads)
        {
            ConfigureAdsShards();
        }
        else
        {
            _hipfireVariation = (_hipfireVariation + 1) % 3;
            ConfigureHipfireShards(_hipfireVariation);
        }

        HideAll();
        Evaluate(0f);
    }

    public void StopPreview()
    {
        _playing = false;
        HideAll();
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
        if (_ads)
        {
            EvaluateAds(t);
        }
        else
        {
            EvaluateHipfire(t);
        }
    }

    // =========================================================
    // 腰射 V3：斜向压入 -> 不对称裂开 -> 单段局部余波
    // =========================================================

    private void EvaluateHipfire(float t)
    {
        EvaluateHipfireImpact(t);
        EvaluateHipfireSplitCuts(t);
        EvaluateHipfireArc(t);
        EvaluateHipfireShards(t);
    }

    private void EvaluateHipfireImpact(float t)
    {
        if (hipfireImpact == null)
        {
            return;
        }

        hipfireImpact.enabled = true;

        float attack = Mathf.Clamp01(t / 0.16f);
        float compression = EaseOutBack(attack);
        float collapse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.78f, t));
        float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, t));
        float alpha = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, attack) * (1f - release));

        float width = Mathf.Lerp(0.16f, hipfireImpactPeak.x, compression);
        float height = Mathf.Lerp(0.075f, hipfireImpactPeak.y, compression);
        width = Mathf.Lerp(width, 0.30f, collapse);
        height = Mathf.Lerp(height, 0.085f, collapse);

        hipfireImpact.transform.localPosition = Vector3.Lerp(
            new Vector3(-0.15f, 0.055f, 0.014f),
            new Vector3(0.025f, -0.008f, 0.014f),
            EaseOutCubic(attack)
        );
        hipfireImpact.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-22f, -13f, attack));
        hipfireImpact.transform.localScale = new Vector3(width, height, 1f);

        SetVisual(hipfireImpact, warmCore, alpha, 2.25f);
    }

    private void EvaluateHipfireSplitCuts(float t)
    {
        if (hipfireSplitCuts == null)
        {
            return;
        }

        Vector2[][] directionSets =
        {
            new[] { new Vector2(-0.88f, 0.48f), new Vector2(0.83f, -0.39f), new Vector2(0.58f, 0.68f) },
            new[] { new Vector2(-0.92f, 0.34f), new Vector2(0.76f, -0.52f), new Vector2(0.70f, 0.52f) },
            new[] { new Vector2(-0.80f, 0.58f), new Vector2(0.89f, -0.30f), new Vector2(0.48f, 0.76f) }
        };

        float[] lengths = { 0.53f, 0.40f, 0.27f };
        float[] thickness = { 0.088f, 0.070f, 0.052f };
        float[] delays = { 0.17f, 0.205f, 0.245f };
        Vector2[] chosen = directionSets[Mathf.Clamp(_hipfireVariation, 0, directionSets.Length - 1)];

        for (int i = 0; i < hipfireSplitCuts.Length; i++)
        {
            MeshRenderer cut = hipfireSplitCuts[i];
            if (cut == null)
            {
                continue;
            }

            float delay = delays[Mathf.Min(i, delays.Length - 1)];
            float localT = Mathf.Clamp01((t - delay) / 0.60f);
            bool visible = t >= delay && localT < 1f;
            cut.enabled = visible;
            if (!visible)
            {
                continue;
            }

            Vector2 direction = chosen[Mathf.Min(i, chosen.Length - 1)].normalized;
            float motion = EaseOutCubic(localT);
            float distance = Mathf.Lerp(0.025f, i == 0 ? 0.27f : i == 1 ? 0.22f : 0.17f, motion);
            float alpha = Mathf.Sin(localT * Mathf.PI) * (1f - Mathf.SmoothStep(0.70f, 1f, localT));
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float length = Mathf.Lerp(0.10f, lengths[Mathf.Min(i, lengths.Length - 1)], EaseOutBack(Mathf.Clamp01(localT * 1.35f)));

            cut.transform.localPosition = new Vector3(direction.x * distance, direction.y * distance, 0.017f + i * 0.001f);
            cut.transform.localRotation = Quaternion.Euler(0f, 0f, angle + (i == 0 ? -5f : i == 1 ? 7f : -9f));
            cut.transform.localScale = new Vector3(length, thickness[Mathf.Min(i, thickness.Length - 1)], 1f);

            Color color = i == 0 ? warmCore : _accent;
            float intensity = i == 0 ? 1.78f : 1.48f;
            SetVisual(cut, color, alpha, intensity);
        }
    }

    private void EvaluateHipfireArc(float t)
    {
        if (hipfireArcSegments == null || hipfireArcSegments.Length == 0)
        {
            return;
        }

        for (int i = 0; i < hipfireArcSegments.Length; i++)
        {
            MeshRenderer arc = hipfireArcSegments[i];
            if (arc == null)
            {
                continue;
            }

            const float delay = 0.27f;
            float localT = Mathf.Clamp01((t - delay) / 0.52f);
            bool visible = t >= delay && localT < 1f;
            arc.enabled = visible;
            if (!visible)
            {
                continue;
            }

            float alpha = Mathf.Sin(localT * Mathf.PI) * 0.42f;
            float size = Mathf.Lerp(0.22f, 0.67f, EaseOutCubic(localT));
            arc.transform.localPosition = new Vector3(-0.035f, -0.045f, 0.012f);
            arc.transform.localScale = Vector3.one * size;
            arc.transform.localRotation = Quaternion.Euler(0f, 0f, 197f + localT * 11f);
            SetVisual(arc, _accent, alpha, 1.32f);
        }
    }

    private void EvaluateHipfireShards(float t)
    {
        if (hipfireShards == null)
        {
            return;
        }

        int visibleCount = Mathf.Clamp(hipfireVisibleShards, 0, hipfireShards.Length);
        for (int i = 0; i < hipfireShards.Length; i++)
        {
            ShardSlot slot = hipfireShards[i];
            if (slot == null || slot.renderer == null)
            {
                continue;
            }

            bool visible = i < visibleCount && t >= slot.startDelay;
            slot.renderer.enabled = visible;
            if (!visible)
            {
                continue;
            }

            float localT = Mathf.Clamp01((t - slot.startDelay) / Mathf.Max(0.05f, 0.82f - slot.startDelay));
            float alpha = Mathf.Sin(localT * Mathf.PI) * (1f - Mathf.SmoothStep(0.66f, 1f, localT));
            float distance = slot.speed * EaseOutCubic(localT);
            slot.renderer.transform.localPosition = slot.direction * distance + new Vector3(0f, 0f, 0.021f + i * 0.0006f);
            slot.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, slot.spin * localT);
            float longAxis = slot.size * Mathf.Lerp(0.58f, 1.04f, Mathf.Sin(localT * Mathf.PI));
            slot.renderer.transform.localScale = new Vector3(longAxis, longAxis * (0.22f + (i % 2) * 0.06f), 1f);
            SetVisual(slot.renderer, i == 0 ? warmCore : _accent, alpha, i == 0 ? 1.58f : 1.36f);
        }
    }

    private void ConfigureHipfireShards(int variation)
    {
        if (hipfireShards == null)
        {
            return;
        }

        float[][] angleSets =
        {
            new[] { 151f, 203f, 25f, 336f },
            new[] { 163f, 216f, 37f, 347f },
            new[] { 140f, 194f, 17f, 323f }
        };

        float[] chosen = angleSets[Mathf.Clamp(variation, 0, angleSets.Length - 1)];
        UnityEngine.Random.State previous = UnityEngine.Random.state;
        UnityEngine.Random.InitState(8317 + variation * 139);

        for (int i = 0; i < hipfireShards.Length; i++)
        {
            float angle = chosen[i % chosen.Length] + UnityEngine.Random.Range(-4f, 4f);
            float radians = angle * Mathf.Deg2Rad;
            hipfireShards[i].direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;
            hipfireShards[i].speed = UnityEngine.Random.Range(i == 0 ? 0.34f : 0.28f, i == 0 ? 0.52f : 0.44f);
            hipfireShards[i].spin = UnityEngine.Random.Range(-70f, 70f);
            hipfireShards[i].startDelay = UnityEngine.Random.Range(0.20f, 0.34f);
            hipfireShards[i].size = UnityEngine.Random.Range(0.25f, 0.37f);
        }

        UnityEngine.Random.state = previous;
    }

    // =========================================================
    // ADS：完整保留 V1/V2 已认可的视觉行为
    // =========================================================

    private void EvaluateAds(float t)
    {
        float coreAlpha = ImpactEnvelope(t);
        float expansion = EaseOutCubic(Mathf.Clamp01(t / 0.44f));

        if (impactCore != null)
        {
            impactCore.enabled = true;
            float squash = Mathf.Lerp(0.34f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f)));
            impactCore.transform.localScale = new Vector3(
                Mathf.Lerp(0.10f, adsCorePeak.x, expansion),
                Mathf.Lerp(adsCorePeak.y * 0.25f, adsCorePeak.y, expansion) * squash,
                1f
            );
            impactCore.transform.localPosition = new Vector3(0.055f, 0f, 0.010f);
            SetVisual(impactCore, warmCore, coreAlpha, 2.25f);
        }

        if (entryCut != null)
        {
            entryCut.enabled = true;
            float cutLife = Mathf.Clamp01(1f - t / 0.52f);
            float cutStretch = Mathf.Lerp(0.16f, 0.76f, EaseOutCubic(Mathf.Clamp01(t / 0.30f)));
            entryCut.transform.localScale = new Vector3(cutStretch, 0.075f, 1f);
            entryCut.transform.localPosition = new Vector3(-cutStretch * 0.42f, 0f, 0.006f);
            SetVisual(entryCut, _accent, cutLife * coreAlpha, 1.70f);
        }

        EvaluateAdsShards(t);
        EvaluateAdsArcs(t);
    }

    private void EvaluateAdsShards(float t)
    {
        if (shards == null)
        {
            return;
        }

        int visibleCount = Mathf.Clamp(adsVisibleShards, 0, shards.Length);
        for (int i = 0; i < shards.Length; i++)
        {
            ShardSlot slot = shards[i];
            if (slot == null || slot.renderer == null)
            {
                continue;
            }

            bool visible = i < visibleCount;
            slot.renderer.enabled = visible;
            if (!visible)
            {
                continue;
            }

            float localT = Mathf.Clamp01((t - slot.startDelay) / Mathf.Max(0.05f, 1f - slot.startDelay));
            float alpha = Mathf.Sin(localT * Mathf.PI) * (1f - Mathf.SmoothStep(0.55f, 1f, localT));
            float distance = slot.speed * EaseOutCubic(localT);
            slot.renderer.transform.localPosition = slot.direction * distance + new Vector3(0f, 0f, 0.014f + i * 0.0004f);
            slot.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, slot.spin * localT);
            float longAxis = slot.size * Mathf.Lerp(0.55f, 1.12f, Mathf.Sin(localT * Mathf.PI));
            slot.renderer.transform.localScale = new Vector3(longAxis, longAxis * (0.20f + (i % 3) * 0.045f), 1f);
            SetVisual(slot.renderer, i % 4 == 0 ? warmCore : _accent, alpha, i % 4 == 0 ? 1.45f : 1.28f);
        }
    }

    private void EvaluateAdsArcs(float t)
    {
        if (adsArcSegments == null)
        {
            return;
        }

        for (int i = 0; i < adsArcSegments.Length; i++)
        {
            MeshRenderer arc = adsArcSegments[i];
            if (arc == null)
            {
                continue;
            }

            arc.enabled = true;
            float delay = 0.08f + i * 0.035f;
            float localT = Mathf.Clamp01((t - delay) / 0.66f);
            float alpha = Mathf.Sin(localT * Mathf.PI) * 0.72f;
            float size = Mathf.Lerp(0.18f, 1.10f + i * 0.09f, EaseOutCubic(localT));
            arc.transform.localScale = Vector3.one * size;
            arc.transform.localRotation = Quaternion.Euler(0f, 0f, i * 67f + localT * (i % 2 == 0 ? 14f : -18f));
            SetVisual(arc, _accent, alpha, 1.22f);
        }
    }

    private void ConfigureAdsShards()
    {
        if (shards == null)
        {
            return;
        }

        UnityEngine.Random.State previous = UnityEngine.Random.state;
        UnityEngine.Random.InitState(9417);

        for (int i = 0; i < shards.Length; i++)
        {
            float baseAngle = (360f / Mathf.Max(1, shards.Length)) * i;
            float angle = baseAngle + UnityEngine.Random.Range(-16f, 16f);
            float radians = angle * Mathf.Deg2Rad;
            float tangentBias = Mathf.Lerp(0.70f, 1.20f, Mathf.Abs(Mathf.Sin(radians)));
            shards[i].direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians) * tangentBias, 0f).normalized;
            shards[i].speed = UnityEngine.Random.Range(0.62f, 1.08f);
            shards[i].spin = UnityEngine.Random.Range(-150f, 150f);
            shards[i].startDelay = UnityEngine.Random.Range(0f, 0.10f);
            shards[i].size = UnityEngine.Random.Range(0.34f, 0.56f);
        }

        UnityEngine.Random.state = previous;
    }

    // =========================================================
    // 通用
    // =========================================================

    private void HideAll()
    {
        SetRendererArrayVisible(hipfireSplitCuts, false);
        SetRendererArrayVisible(hipfireArcSegments, false);
        SetShardArrayVisible(hipfireShards, false);
        SetRendererArrayVisible(adsArcSegments, false);
        SetShardArrayVisible(shards, false);

        if (hipfireImpact != null) hipfireImpact.enabled = false;
        if (impactCore != null) impactCore.enabled = false;
        if (entryCut != null) entryCut.enabled = false;
    }

    private static void SetRendererArrayVisible(MeshRenderer[] renderers, bool visible)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private static void SetShardArrayVisible(ShardSlot[] slots, bool visible)
    {
        if (slots == null)
        {
            return;
        }

        foreach (ShardSlot slot in slots)
        {
            if (slot != null && slot.renderer != null)
            {
                slot.renderer.enabled = visible;
            }
        }
    }

    private void SetVisual(MeshRenderer renderer, Color color, float alpha, float intensity)
    {
        if (renderer == null)
        {
            return;
        }

        EnsureBlock();
        _block.Clear();
        Color value = color;
        value.a = Mathf.Clamp01(alpha);
        _block.SetColor(BaseColorId, value);
        _block.SetFloat(IntensityId, Mathf.Max(0f, intensity));
        renderer.SetPropertyBlock(_block);
    }

    private void EnsureBlock()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }
    }

    private static float ImpactEnvelope(float t)
    {
        if (t < 0.12f) return Mathf.SmoothStep(0f, 1f, t / 0.12f);
        if (t < 0.38f) return 1f;
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 1f, t));
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float shifted = value - 1f;
        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hipfireDuration = Mathf.Max(0.10f, hipfireDuration);
        adsDuration = Mathf.Max(0.10f, adsDuration);
        hipfireVisibleShards = Mathf.Clamp(hipfireVisibleShards, 1, 6);
        adsVisibleShards = Mathf.Clamp(adsVisibleShards, 1, 12);
    }
#endif
}
