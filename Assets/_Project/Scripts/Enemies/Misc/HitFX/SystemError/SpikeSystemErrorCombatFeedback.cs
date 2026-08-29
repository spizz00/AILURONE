#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Production version of the V4 System Error preview for non-fatal Spike hits.
/// It changes presentation only and never touches damage, movement, AI or death logic.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikeSystemErrorCombatFeedback : MonoBehaviour
{
    private sealed class GhostPair
    {
        public Transform source;
        public Transform whiteTransform;
        public MeshRenderer whiteRenderer;
        public Transform accentTransform;
        public MeshRenderer accentRenderer;
    }

    private sealed class BlockState
    {
        public MeshRenderer renderer;
        public Vector3 direction;
        public float distance;
        public float startDelay;
        public float spin;
        public Vector2 size;
        public int colorRole;
    }

    [Header("Spike References")]
    public EnemyTarget enemyTarget;
    public SpikeEnemy spikeEnemy;
    public SpikeVisualFeedback visualFeedback;
    public MeshRenderer bodyRenderer;

    [Header("System Error Assets")]
    public Mesh quadMesh;
    public Material targetMaterial;
    public Material ghostMaterial;
    public Material slashMaterial;
    public Material brokenRectMaterial;
    public Material halftoneMaterial;
    public Material[] blockMaterials;

    [Header("Timing")]
    [Min(0.08f)]
    public float hipfireDuration = 0.16f;

    [Min(0.12f)]
    public float adsMinimumDuration = 0.19f;

    [Min(0.12f)]
    public float adsMaximumDuration = 0.29f;

    [Min(0.1f)]
    public float adsChargeExponent = 1.35f;

    [Header("State Colors")]
    [ColorUsage(true, true)]
    public Color blueState =
        new Color(0.02f, 0.42f, 1.00f, 1f);

    [ColorUsage(true, true)]
    public Color magentaState =
        new Color(1.00f, 0.015f, 0.50f, 1f);

    [ColorUsage(true, true)]
    public Color yellowAccent =
        new Color(1.00f, 0.86f, 0.10f, 1f);

    [ColorUsage(true, true)]
    public Color cyanAccent =
        new Color(0.04f, 0.95f, 1.00f, 1f);

    public Color hardBlack =
        new Color(0.008f, 0.010f, 0.016f, 1f);

    public Color hardWhite = Color.white;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int AccentColorId =
        Shader.PropertyToID("_AccentColor");

    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");

    private static readonly int HitAmountId =
        Shader.PropertyToID("_HitAmount");

    private static readonly int AdsAmountId =
        Shader.PropertyToID("_AdsAmount");

    private static readonly int KillAmountId =
        Shader.PropertyToID("_KillAmount");

    private static readonly int SeedId =
        Shader.PropertyToID("_Seed");

    private static readonly int VisibilityId =
        Shader.PropertyToID("_Visibility");

    private static readonly int AlphaId =
        Shader.PropertyToID("_Alpha");

    private static readonly int GlitchAmountId =
        Shader.PropertyToID("_GlitchAmount");

    private Material[] _originalBodyMaterials;
    private MaterialPropertyBlock _bodyBlock;
    private MaterialPropertyBlock _ghostBlock;
    private MaterialPropertyBlock _fxBlock;

    private GhostPair _ghostPair;
    private Transform _graphicsRoot;
    private MeshRenderer _impactWhite;
    private MeshRenderer _impactBlack;
    private MeshRenderer _impactAccent;
    private BlockState[] _blocks;

    private Camera _camera;
    private bool _playing;
    private bool _bodyOverridden;
    private bool _firedAsAds;
    private bool _magentaState;
    private float _elapsed;
    private float _duration;
    private float _chargeResponse;
    private float _shotResponse;
    private float _seed;
    private Vector3 _localHitPoint;
    private Color _baseColor;
    private Color _accentColor;
    private SpikeSystemErrorDeathPresentation
        _activeDeathPresentation;

    public bool IsPlaying => _playing;

    private void Awake()
    {
        ResolveReferences();
        CacheBodyMaterials();

        _bodyBlock = new MaterialPropertyBlock();
        _ghostBlock = new MaterialPropertyBlock();
        _fxBlock = new MaterialPropertyBlock();

        BuildRuntimeVisuals();
        HideRuntimeVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (enemyTarget != null)
        {
            enemyTarget.Died += HandleDeath;
        }
    }

    public void PlayNonFatalHit(
        Vector3 worldHitPoint,
        bool firedAsAds,
        float charge01,
        float shotStrength
    )
    {
        ResolveReferences();

        if (!CanPlay())
        {
            return;
        }

        if (_graphicsRoot == null ||
            _ghostPair == null)
        {
            BuildRuntimeVisuals();
        }

        _firedAsAds = firedAsAds;
        _chargeResponse =
            firedAsAds
                ? Mathf.Pow(
                    Mathf.Clamp01(charge01),
                    Mathf.Max(0.1f, adsChargeExponent)
                )
                : 0f;

        _shotResponse =
            firedAsAds
                ? _chargeResponse
                : Mathf.InverseLerp(
                    0.82f,
                    1.12f,
                    shotStrength
                );

        _duration =
            firedAsAds
                ? Mathf.Lerp(
                    adsMinimumDuration,
                    adsMaximumDuration,
                    _chargeResponse
                )
                : hipfireDuration;

        _magentaState = ResolveMagentaState();
        _baseColor =
            _magentaState
                ? magentaState
                : blueState;

        _accentColor =
            _magentaState
                ? cyanAccent
                : yellowAccent;

        _localHitPoint =
            transform.InverseTransformPoint(
                worldHitPoint
            );

        _camera = Camera.main;
        _seed = Random.Range(0.15f, 98f);
        _elapsed = 0f;
        _playing = true;

        ConfigureBlocks();
        BeginBodyOverride();
        UpdateAttachment();
        Evaluate(0f);
    }

    public void RefineLethalHit(
        Vector3 worldHitPoint,
        bool firedAsAds,
        float charge01,
        float shotStrength
    )
    {
        if (_activeDeathPresentation == null)
        {
            return;
        }

        _activeDeathPresentation.RefineWeaponShot(
            worldHitPoint,
            firedAsAds,
            charge01,
            shotStrength
        );
    }

    private void HandleDeath(
        EnemyDeathInfo deathInfo
    )
    {
        if (!CanPlayDeath())
        {
            return;
        }

        bool environmentalDeath =
            deathInfo.Cause ==
            EnemyDeathCause.Environment;

        bool useMagentaState =
            environmentalDeath ||
            ResolveMagentaState();

        StopFeedback();

        _activeDeathPresentation =
            SpikeSystemErrorDeathPresentation.Spawn(
                this,
                deathInfo.ImpactPoint,
                useMagentaState,
                environmentalDeath
            );
    }

    private void Update()
    {
        if (!_playing)
        {
            return;
        }

        _elapsed +=
            Mathf.Max(
                0f,
                Time.unscaledDeltaTime
            );

        float normalizedTime =
            Mathf.Clamp01(
                _elapsed /
                Mathf.Max(0.01f, _duration)
            );

        Evaluate(normalizedTime);

        if (_elapsed >= _duration)
        {
            StopFeedback();
        }
    }

    private void LateUpdate()
    {
        if (!_playing)
        {
            return;
        }

        UpdateAttachment();
        UpdateGhostTransforms();
    }

    private void Evaluate(
        float normalizedTime
    )
    {
        float visualStrength =
            _firedAsAds
                ? Mathf.Lerp(
                    0.58f,
                    1f,
                    _chargeResponse
                )
                : Mathf.Lerp(
                    0.86f,
                    1f,
                    _shotResponse
                );

        if (_firedAsAds)
        {
            EvaluateAds(
                normalizedTime,
                visualStrength
            );
        }
        else
        {
            EvaluateHipfire(
                normalizedTime,
                visualStrength
            );
        }
    }

    private void EvaluateHipfire(
        float t,
        float visualStrength
    )
    {
        float hitAmount =
            1f - SmoothRange(0.055f, 0.31f, t);

        float ghostEnvelope =
            Mathf.Sin(
                Mathf.Clamp01(t / 0.30f) *
                Mathf.PI
            ) *
            (1f - SmoothRange(0.26f, 0.43f, t));

        ApplyBody(
            hitAmount,
            0f,
            1.02f
        );

        ApplyGhosts(
            ghostEnvelope * 0.36f * visualStrength,
            ghostEnvelope * 0.48f * visualStrength,
            ghostEnvelope * 0.42f,
            new Vector2(0.036f, 0.012f) * visualStrength,
            new Vector2(-0.030f, -0.010f) * visualStrength
        );

        EvaluateImpactGraphics(
            t,
            0.78f * visualStrength,
            0.38f * visualStrength,
            -18f,
            false
        );

        EvaluateBlocks(
            t,
            Mathf.RoundToInt(
                Mathf.Lerp(3f, 5f, _shotResponse)
            ),
            0.16f,
            0.97f * visualStrength
        );
    }

    private void EvaluateAds(
        float t,
        float visualStrength
    )
    {
        float hitAmount =
            1f - SmoothRange(0.08f, 0.27f, t);

        float adsEnvelope =
            Mathf.Sin(
                Mathf.Clamp01(
                    Mathf.InverseLerp(
                        0.035f,
                        0.76f,
                        t
                    )
                ) *
                Mathf.PI
            );

        ApplyBody(
            hitAmount,
            adsEnvelope * visualStrength,
            Mathf.Lerp(1.02f, 1.05f, visualStrength)
        );

        float whiteFlicker =
            adsEnvelope *
            (0.68f + 0.18f * Mathf.Sin(t * 76f));

        float accentFlicker =
            adsEnvelope *
            (0.58f + 0.16f * Mathf.Cos(t * 63f));

        ApplyGhosts(
            Mathf.Clamp01(whiteFlicker) * visualStrength,
            Mathf.Clamp01(accentFlicker) * visualStrength,
            adsEnvelope * visualStrength,
            new Vector2(
                0.084f + Mathf.Sin(t * 37f) * 0.018f,
                0.018f
            ) * visualStrength,
            new Vector2(
                -0.072f + Mathf.Cos(t * 31f) * 0.014f,
                -0.020f
            ) * visualStrength
        );

        EvaluateImpactGraphics(
            t,
            1.16f * visualStrength,
            0.62f * visualStrength,
            -14f,
            true
        );

        EvaluateBlocks(
            t,
            Mathf.RoundToInt(
                Mathf.Lerp(5f, 8f, _chargeResponse)
            ),
            0.10f,
            1.18f * visualStrength
        );
    }

    private void ApplyBody(
        float hitAmount,
        float adsAmount,
        float intensity
    )
    {
        if (bodyRenderer == null)
        {
            return;
        }

        _bodyBlock.Clear();
        _bodyBlock.SetColor(BaseColorId, _baseColor);
        _bodyBlock.SetColor(AccentColorId, _accentColor);
        _bodyBlock.SetFloat(IntensityId, intensity);
        _bodyBlock.SetFloat(HitAmountId, Mathf.Clamp01(hitAmount));
        _bodyBlock.SetFloat(AdsAmountId, Mathf.Clamp01(adsAmount));
        _bodyBlock.SetFloat(KillAmountId, 0f);
        _bodyBlock.SetFloat(SeedId, _seed);
        _bodyBlock.SetFloat(VisibilityId, 1f);
        bodyRenderer.SetPropertyBlock(_bodyBlock);
    }

    private void ApplyGhosts(
        float whiteAlpha,
        float accentAlpha,
        float glitch,
        Vector2 whiteOffset,
        Vector2 accentOffset
    )
    {
        if (_ghostPair == null)
        {
            return;
        }

        ApplyGhostRenderer(
            _ghostPair.whiteRenderer,
            hardWhite,
            whiteAlpha,
            glitch,
            _seed + 0.73f
        );

        ApplyGhostRenderer(
            _ghostPair.accentRenderer,
            _accentColor,
            accentAlpha,
            glitch * 0.86f,
            _seed + 4.11f
        );

        PositionGhost(
            _ghostPair.whiteTransform,
            whiteOffset
        );

        PositionGhost(
            _ghostPair.accentTransform,
            accentOffset
        );
    }

    private void ApplyGhostRenderer(
        MeshRenderer renderer,
        Color color,
        float alpha,
        float glitch,
        float seed
    )
    {
        if (renderer == null)
        {
            return;
        }

        renderer.enabled = alpha > 0.002f;
        _ghostBlock.Clear();
        _ghostBlock.SetColor(BaseColorId, color);
        _ghostBlock.SetFloat(AlphaId, Mathf.Clamp01(alpha));
        _ghostBlock.SetFloat(GlitchAmountId, Mathf.Clamp01(glitch));
        _ghostBlock.SetFloat(SeedId, seed);
        renderer.SetPropertyBlock(_ghostBlock);
    }

    private void EvaluateImpactGraphics(
        float t,
        float width,
        float height,
        float angle,
        bool heavy
    )
    {
        float attack =
            SmoothRange(
                0f,
                heavy ? 0.10f : 0.075f,
                t
            );

        float release =
            1f - SmoothRange(
                heavy ? 0.33f : 0.27f,
                heavy ? 0.70f : 0.62f,
                t
            );

        float envelope =
            Mathf.Clamp01(attack * release);

        if (_impactBlack != null)
        {
            _impactBlack.enabled = envelope > 0.001f;
            _impactBlack.transform.localPosition =
                new Vector3(0.045f, -0.030f, 0.004f);
            _impactBlack.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle - 3f);
            _impactBlack.transform.localScale =
                new Vector3(width * 1.12f, height * 1.12f, 1f);

            SetFx(
                _impactBlack,
                hardBlack,
                envelope * 0.94f,
                1f
            );
        }

        if (_impactWhite != null)
        {
            _impactWhite.enabled = envelope > 0.001f;

            float snap =
                EaseOutBack(
                    Mathf.Clamp01(
                        t / (heavy ? 0.16f : 0.12f)
                    )
                );

            _impactWhite.transform.localPosition = Vector3.zero;
            _impactWhite.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle);
            _impactWhite.transform.localScale =
                new Vector3(
                    width * Mathf.Lerp(0.28f, 1f, snap),
                    height * Mathf.Lerp(0.55f, 1f, snap),
                    1f
                );

            SetFx(
                _impactWhite,
                hardWhite,
                envelope,
                heavy ? 1.45f : 1.25f
            );
        }

        if (_impactAccent != null)
        {
            float accentDelay =
                heavy ? 0.07f : 0.10f;

            float accentTime =
                Mathf.Clamp01(
                    (t - accentDelay) /
                    (heavy ? 0.50f : 0.42f)
                );

            float accentEnvelope =
                Mathf.Sin(accentTime * Mathf.PI) *
                (heavy ? 0.82f : 0.54f);

            _impactAccent.enabled =
                t >= accentDelay &&
                accentEnvelope > 0.001f;

            _impactAccent.transform.localPosition =
                new Vector3(-0.055f, 0.038f, -0.002f);
            _impactAccent.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle + 17f);
            _impactAccent.transform.localScale =
                new Vector3(
                    width * (heavy ? 0.72f : 0.54f),
                    height * (heavy ? 0.66f : 0.50f),
                    1f
                );

            SetFx(
                _impactAccent,
                _accentColor,
                accentEnvelope,
                1.15f
            );
        }
    }

    private void EvaluateBlocks(
        float t,
        int visibleCount,
        float globalDelay,
        float distanceMultiplier
    )
    {
        if (_blocks == null)
        {
            return;
        }

        visibleCount =
            Mathf.Clamp(
                visibleCount,
                0,
                _blocks.Length
            );

        for (int index = 0;
             index < _blocks.Length;
             index++)
        {
            BlockState block = _blocks[index];

            if (block == null ||
                block.renderer == null)
            {
                continue;
            }

            float delay =
                globalDelay + block.startDelay;

            bool visible =
                index < visibleCount &&
                t >= delay;

            block.renderer.enabled = visible;

            if (!visible)
            {
                continue;
            }

            float localTime =
                Mathf.Clamp01(
                    (t - delay) /
                    Mathf.Max(0.08f, 0.91f - delay)
                );

            float envelope =
                Mathf.Sin(localTime * Mathf.PI) *
                (1f - SmoothRange(0.72f, 1f, localTime));

            float travel =
                block.distance *
                distanceMultiplier *
                EaseOutCubic(localTime);

            block.renderer.transform.localPosition =
                block.direction * travel +
                new Vector3(
                    0f,
                    0f,
                    -0.006f - index * 0.0004f
                );

            block.renderer.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    block.spin * localTime
                );

            float sizePulse =
                Mathf.Lerp(
                    0.62f,
                    1f,
                    Mathf.Sin(localTime * Mathf.PI)
                );

            block.renderer.transform.localScale =
                new Vector3(
                    block.size.x * sizePulse,
                    block.size.y * sizePulse,
                    1f
                );

            Color color =
                block.colorRole == 0
                    ? hardWhite
                    : block.colorRole == 1
                        ? hardBlack
                        : _accentColor;

            SetFx(
                block.renderer,
                color,
                envelope,
                block.colorRole == 0 ? 1.24f : 1f
            );
        }
    }

    private void ConfigureBlocks()
    {
        if (_blocks == null)
        {
            return;
        }

        float[] hipfireAngles =
        {
            16f, 154f, 211f, 333f,
            89f, 278f, 42f, 187f,
            305f, 121f, 245f, 350f
        };

        float[] adsAngles =
        {
            8f, 38f, 92f, 148f,
            194f, 224f, 278f, 326f,
            62f, 173f, 252f, 344f
        };

        float[] angles =
            _firedAsAds
                ? adsAngles
                : hipfireAngles;

        Random.State previousState = Random.state;
        Random.InitState(
            Mathf.RoundToInt(_seed * 100f) +
            (_firedAsAds ? 307 : 0)
        );

        for (int index = 0;
             index < _blocks.Length;
             index++)
        {
            BlockState block = _blocks[index];
            float angle =
                angles[index % angles.Length] +
                Random.Range(-5.5f, 5.5f);

            float radians = angle * Mathf.Deg2Rad;
            block.direction =
                new Vector3(
                    Mathf.Cos(radians),
                    Mathf.Sin(radians),
                    0f
                ).normalized;

            block.distance =
                _firedAsAds
                    ? Random.Range(0.48f, 0.94f)
                    : Random.Range(0.30f, 0.62f);

            block.startDelay =
                _firedAsAds
                    ? Random.Range(0.02f, 0.15f)
                    : Random.Range(0.02f, 0.12f);

            block.spin =
                Random.Range(-150f, 150f);

            float longSize =
                _firedAsAds
                    ? Random.Range(0.28f, 0.52f)
                    : Random.Range(0.22f, 0.40f);

            float shortSize =
                longSize * Random.Range(0.28f, 0.58f);

            block.size =
                index % 3 == 0
                    ? new Vector2(shortSize, longSize)
                    : new Vector2(longSize, shortSize);

            block.colorRole =
                index % 4 == 0
                    ? 2
                    : index % 3 == 0
                        ? 1
                        : 0;
        }

        Random.state = previousState;
    }

    private void SetFx(
        MeshRenderer renderer,
        Color color,
        float alpha,
        float intensity
    )
    {
        if (renderer == null)
        {
            return;
        }

        Color displayColor = color;
        displayColor.a = Mathf.Clamp01(alpha);

        _fxBlock.Clear();
        _fxBlock.SetColor(BaseColorId, displayColor);
        _fxBlock.SetFloat(IntensityId, Mathf.Max(0f, intensity));
        renderer.SetPropertyBlock(_fxBlock);
    }

    private void BeginBodyOverride()
    {
        if (_bodyOverridden)
        {
            return;
        }

        if (visualFeedback != null)
        {
            visualFeedback.SetExternalVisualOverride(true);
        }

        bodyRenderer.sharedMaterials =
            RepeatMaterial(
                targetMaterial,
                Mathf.Max(
                    1,
                    _originalBodyMaterials == null
                        ? 1
                        : _originalBodyMaterials.Length
                )
            );

        _bodyOverridden = true;
    }

    private void StopFeedback()
    {
        _playing = false;
        HideRuntimeVisuals();

        if (!_bodyOverridden)
        {
            return;
        }

        if (bodyRenderer != null &&
            _originalBodyMaterials != null)
        {
            bodyRenderer.sharedMaterials =
                _originalBodyMaterials;
            bodyRenderer.SetPropertyBlock(null);
        }

        _bodyOverridden = false;

        if (visualFeedback != null)
        {
            visualFeedback.SetExternalVisualOverride(false);
        }
    }

    private void ResolveReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        if (spikeEnemy == null)
        {
            spikeEnemy = GetComponent<SpikeEnemy>();
        }

        if (visualFeedback == null)
        {
            visualFeedback = GetComponent<SpikeVisualFeedback>();
        }

        if (bodyRenderer == null &&
            visualFeedback != null &&
            visualFeedback.targetRenderers != null)
        {
            foreach (Renderer renderer
                     in visualFeedback.targetRenderers)
            {
                MeshRenderer meshRenderer =
                    renderer as MeshRenderer;

                if (meshRenderer == null)
                {
                    continue;
                }

                bodyRenderer = meshRenderer;
                break;
            }
        }
    }

    private bool CanPlay()
    {
        return
            bodyRenderer != null &&
            bodyRenderer.GetComponent<MeshFilter>() != null &&
            quadMesh != null &&
            targetMaterial != null &&
            ghostMaterial != null &&
            slashMaterial != null &&
            brokenRectMaterial != null &&
            blockMaterials != null &&
            blockMaterials.Length > 0;
    }

    private bool CanPlayDeath()
    {
        return
            CanPlay() &&
            halftoneMaterial != null;
    }

    private void CacheBodyMaterials()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        _originalBodyMaterials =
            bodyRenderer.sharedMaterials;
    }

    private void BuildRuntimeVisuals()
    {
        if (!CanPlay() ||
            _graphicsRoot != null)
        {
            return;
        }

        _ghostPair = BuildGhostPair();

        GameObject graphicsObject =
            new GameObject("SystemError_HitGraphics_Runtime");

        graphicsObject.transform.SetParent(
            transform,
            false
        );

        _graphicsRoot = graphicsObject.transform;

        _impactBlack =
            CreateGraphic(
                "Impact_BlackCut",
                slashMaterial,
                104
            );

        _impactWhite =
            CreateGraphic(
                "Impact_WhiteSlash",
                slashMaterial,
                105
            );

        _impactAccent =
            CreateGraphic(
                "Impact_AccentBlock",
                brokenRectMaterial,
                106
            );

        _blocks = new BlockState[12];

        for (int index = 0;
             index < _blocks.Length;
             index++)
        {
            Material material =
                blockMaterials[
                    index % blockMaterials.Length
                ];

            _blocks[index] =
                new BlockState
                {
                    renderer =
                        CreateGraphic(
                            $"SystemError_Block_{index + 1:00}",
                            material,
                            110 + index
                        )
                };
        }
    }

    private GhostPair BuildGhostPair()
    {
        GhostPair result = new GhostPair();
        result.source = bodyRenderer.transform;

        result.whiteTransform =
            CreateGhost(
                "SystemError_Ghost_White",
                out result.whiteRenderer
            );

        result.accentTransform =
            CreateGhost(
                "SystemError_Ghost_Accent",
                out result.accentRenderer
            );

        return result;
    }

    private Transform CreateGhost(
        string objectName,
        out MeshRenderer renderer
    )
    {
        GameObject ghostObject =
            new GameObject(objectName);

        ghostObject.layer =
            bodyRenderer.gameObject.layer;

        Transform ghostTransform =
            ghostObject.transform;

        Transform sourceTransform =
            bodyRenderer.transform;

        ghostTransform.SetParent(
            sourceTransform.parent,
            false
        );

        ghostTransform.localPosition =
            sourceTransform.localPosition;
        ghostTransform.localRotation =
            sourceTransform.localRotation;
        ghostTransform.localScale =
            sourceTransform.localScale;

        MeshFilter sourceFilter =
            bodyRenderer.GetComponent<MeshFilter>();

        MeshFilter ghostFilter =
            ghostObject.AddComponent<MeshFilter>();

        ghostFilter.sharedMesh =
            sourceFilter.sharedMesh;

        renderer =
            ghostObject.AddComponent<MeshRenderer>();

        renderer.sharedMaterials =
            RepeatMaterial(
                ghostMaterial,
                Mathf.Max(
                    1,
                    bodyRenderer.sharedMaterials.Length
                )
            );

        renderer.shadowCastingMode =
            ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;

        return ghostTransform;
    }

    private MeshRenderer CreateGraphic(
        string objectName,
        Material material,
        int sortingOrder
    )
    {
        GameObject graphicObject =
            new GameObject(objectName);

        graphicObject.layer = gameObject.layer;
        graphicObject.transform.SetParent(
            _graphicsRoot,
            false
        );

        MeshFilter filter =
            graphicObject.AddComponent<MeshFilter>();

        filter.sharedMesh = quadMesh;

        MeshRenderer renderer =
            graphicObject.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;

        return renderer;
    }

    private void UpdateAttachment()
    {
        if (_graphicsRoot == null)
        {
            return;
        }

        if (_camera == null)
        {
            _camera = Camera.main;
        }

        Vector3 hitPoint =
            transform.TransformPoint(
                _localHitPoint
            );

        if (_camera == null)
        {
            _graphicsRoot.position = hitPoint;
            return;
        }

        Vector3 towardCamera =
            _camera.transform.position -
            hitPoint;

        if (towardCamera.sqrMagnitude <= 0.0001f)
        {
            towardCamera =
                -_camera.transform.forward;
        }

        _graphicsRoot.position =
            hitPoint +
            towardCamera.normalized * 0.035f;

        _graphicsRoot.rotation =
            Quaternion.LookRotation(
                towardCamera.normalized,
                _camera.transform.up
            );
    }

    private void UpdateGhostTransforms()
    {
        if (_ghostPair == null ||
            _ghostPair.source == null)
        {
            return;
        }

        CopySourceTransform(
            _ghostPair.whiteTransform
        );

        CopySourceTransform(
            _ghostPair.accentTransform
        );
    }

    private void CopySourceTransform(
        Transform destination
    )
    {
        if (destination == null ||
            _ghostPair.source == null)
        {
            return;
        }

        destination.localRotation =
            _ghostPair.source.localRotation;
        destination.localScale =
            _ghostPair.source.localScale;
    }

    private void PositionGhost(
        Transform ghostTransform,
        Vector2 cameraOffset
    )
    {
        if (ghostTransform == null ||
            _ghostPair == null ||
            _ghostPair.source == null)
        {
            return;
        }

        Transform parent = ghostTransform.parent;
        Vector3 worldOffset;

        if (_camera != null)
        {
            worldOffset =
                _camera.transform.right * cameraOffset.x +
                _camera.transform.up * cameraOffset.y;
        }
        else
        {
            worldOffset =
                new Vector3(
                    cameraOffset.x,
                    cameraOffset.y,
                    0f
                );
        }

        Vector3 localOffset =
            parent != null
                ? parent.InverseTransformVector(worldOffset)
                : worldOffset;

        ghostTransform.localPosition =
            _ghostPair.source.localPosition +
            localOffset;
    }

    private void HideRuntimeVisuals()
    {
        SetRendererEnabled(_impactWhite, false);
        SetRendererEnabled(_impactBlack, false);
        SetRendererEnabled(_impactAccent, false);

        if (_blocks != null)
        {
            foreach (BlockState block in _blocks)
            {
                if (block != null)
                {
                    SetRendererEnabled(
                        block.renderer,
                        false
                    );
                }
            }
        }

        if (_ghostPair != null)
        {
            SetRendererEnabled(
                _ghostPair.whiteRenderer,
                false
            );

            SetRendererEnabled(
                _ghostPair.accentRenderer,
                false
            );
        }
    }

    private bool ResolveMagentaState()
    {
        if (spikeEnemy == null)
        {
            return false;
        }

        switch (spikeEnemy.CurrentState)
        {
            case SpikeEnemy.SpikeState.Windup:
            case SpikeEnemy.SpikeState.Charging:
            case SpikeEnemy.SpikeState.Stunned:
            case SpikeEnemy.SpikeState.Falling:
                return true;

            default:
                return false;
        }
    }

    private static Material[] RepeatMaterial(
        Material material,
        int count
    )
    {
        Material[] materials =
            new Material[Mathf.Max(1, count)];

        for (int index = 0;
             index < materials.Length;
             index++)
        {
            materials[index] = material;
        }

        return materials;
    }

    private static void SetRendererEnabled(
        Renderer renderer,
        bool enabledValue
    )
    {
        if (renderer != null)
        {
            renderer.enabled = enabledValue;
        }
    }

    private static float SmoothRange(
        float start,
        float end,
        float value
    )
    {
        return Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(start, end, value)
        );
    }

    private static float EaseOutCubic(
        float value
    )
    {
        float inverse =
            1f - Mathf.Clamp01(value);

        return
            1f - inverse * inverse * inverse;
    }

    private static float EaseOutBack(
        float value
    )
    {
        value = Mathf.Clamp01(value);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float shifted = value - 1f;

        return
            1f +
            c3 * shifted * shifted * shifted +
            c1 * shifted * shifted;
    }

    private void OnDisable()
    {
        if (enemyTarget != null)
        {
            enemyTarget.Died -= HandleDeath;
        }

        StopFeedback();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hipfireDuration =
            Mathf.Max(0.08f, hipfireDuration);

        adsMinimumDuration =
            Mathf.Max(0.12f, adsMinimumDuration);

        adsMaximumDuration =
            Mathf.Max(
                adsMinimumDuration,
                adsMaximumDuration
            );

        adsChargeExponent =
            Mathf.Max(0.1f, adsChargeExponent);
    }
#endif
}
