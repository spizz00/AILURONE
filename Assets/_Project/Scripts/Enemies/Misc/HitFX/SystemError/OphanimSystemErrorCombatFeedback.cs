#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adapts the shared V4 System Error language to Ophanim's core and four rings.
/// Presentation only: health, AI, movement, collision and rewards are untouched.
/// </summary>
[DisallowMultipleComponent]
public sealed class OphanimSystemErrorCombatFeedback : MonoBehaviour
{
    private sealed class PartState
    {
        public MeshRenderer source;
        public Material[] originalMaterials;
        public Transform whiteTransform;
        public MeshRenderer whiteRenderer;
        public Transform accentTransform;
        public MeshRenderer accentRenderer;
    }

    private sealed class BlockState
    {
        public Transform transform;
        public MeshRenderer renderer;
        public Vector2 direction;
        public float distance;
        public float delay;
        public float spin;
        public Vector2 size;
    }

    [Header("Ophanim References")]
    public EnemyTarget enemyTarget;
    public OphanimEnemy ophanimEnemy;
    public OphanimVisualFeedback visualFeedback;
    public Transform visualRoot;
    public MeshRenderer[] partRenderers;

    [Header("System Error Assets")]
    public Mesh quadMesh;
    public Material targetMaterial;
    public Material ghostMaterial;
    public Material slashMaterial;
    public Material brokenRectMaterial;
    public Material halftoneMaterial;
    public Material[] blockMaterials;

    [Header("Timing")]
    [Min(0.08f)] public float hipfireDuration = 0.18f;
    [Min(0.12f)] public float adsMinimumDuration = 0.21f;
    [Min(0.12f)] public float adsMaximumDuration = 0.32f;
    [Min(0.1f)] public float adsChargeExponent = 1.35f;

    [Header("Colors")]
    [ColorUsage(true, true)]
    public Color blueState = new Color(0.02f, 0.42f, 1f, 1f);
    [ColorUsage(true, true)]
    public Color magentaState = new Color(1f, 0.015f, 0.5f, 1f);
    [ColorUsage(true, true)]
    public Color yellowAccent = new Color(1f, 0.86f, 0.1f, 1f);
    [ColorUsage(true, true)]
    public Color cyanAccent = new Color(0.04f, 0.95f, 1f, 1f);
    public Color hardBlack = new Color(0.008f, 0.01f, 0.016f, 1f);
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

    private readonly List<PartState> _parts = new List<PartState>();
    private readonly List<BlockState> _blocks = new List<BlockState>();
    private MaterialPropertyBlock _propertyBlock;
    private Transform _runtimeRoot;
    private MeshRenderer _slashWhite;
    private MeshRenderer _slashBlack;
    private MeshRenderer _slashAccent;
    private Transform _halftoneTransform;
    private MeshRenderer _halftoneRenderer;
    private Camera _camera;
    private Vector3 _hitPoint;
    private float _elapsed;
    private float _duration;
    private float _chargeResponse;
    private float _strength;
    private float _seed;
    private bool _playing;
    private bool _firedAsAds;
    private bool _magenta;
    private bool _subscribed;
    private OphanimSystemErrorDeathPresentation _deathPresentation;

    private void Awake()
    {
        ResolveReferences();
        _propertyBlock = new MaterialPropertyBlock();
        CacheParts();
        BuildRuntimeVisuals();
        HideRuntimeVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
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

        if (_runtimeRoot == null)
        {
            CacheParts();
            BuildRuntimeVisuals();
        }

        _firedAsAds = firedAsAds;
        _chargeResponse = firedAsAds
            ? Mathf.Pow(Mathf.Clamp01(charge01), Mathf.Max(0.1f, adsChargeExponent))
            : 0f;
        _strength = Mathf.Clamp01(Mathf.InverseLerp(0.35f, 1.5f, shotStrength));
        _duration = firedAsAds
            ? Mathf.Lerp(adsMinimumDuration, adsMaximumDuration, _chargeResponse)
            : hipfireDuration;
        _hitPoint = worldHitPoint;
        _magenta = ResolveMagentaState();
        _seed = Random.Range(0.15f, 98f);
        _elapsed = 0f;
        _camera = Camera.main;
        _playing = true;

        OverrideSourceMaterials();
        ShowRuntimeVisuals();
        Evaluate(0f);
    }

    public void RefineLethalHit(
        Vector3 worldHitPoint,
        bool firedAsAds,
        float charge01,
        float shotStrength
    )
    {
        if (_deathPresentation != null)
        {
            _deathPresentation.RefineWeaponShot(
                worldHitPoint,
                firedAsAds,
                charge01,
                shotStrength
            );
        }
    }

    private void LateUpdate()
    {
        if (!_playing)
        {
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        Evaluate(Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration)));

        if (_elapsed >= _duration)
        {
            StopFeedback();
        }
    }

    private void Evaluate(float progress)
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }

        float attack = Mathf.Clamp01(progress / 0.16f);
        float release = 1f - Mathf.Clamp01((progress - 0.38f) / 0.62f);
        float envelope = Mathf.Min(attack, release);
        float glitch = envelope * Mathf.Lerp(0.62f, 1f, _chargeResponse);
        Color baseColor = _magenta ? magentaState : blueState;
        Color accent = _magenta ? cyanAccent : yellowAccent;

        for (int i = 0; i < _parts.Count; i++)
        {
            PartState part = _parts[i];

            if (part.source == null)
            {
                continue;
            }

            SetBodyProperties(part.source, baseColor, accent, glitch);
            UpdateGhost(part, i, baseColor, accent, glitch);
        }

        UpdateImpactGraphics(baseColor, accent, glitch, progress);
        UpdateBlocks(baseColor, accent, glitch, progress);
        UpdateHalftone(accent, glitch, progress);
    }

    private void UpdateGhost(
        PartState part,
        int index,
        Color baseColor,
        Color accent,
        float amount
    )
    {
        if (part.whiteTransform == null || part.accentTransform == null)
        {
            return;
        }

        Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;
        Vector3 up = _camera != null ? _camera.transform.up : Vector3.up;
        float wave = Mathf.Sin((_elapsed * 48f) + index * 1.73f + _seed);
        float ringBias = index == 0 ? 0.65f : 1f + index * 0.08f;
        Vector3 offset = (right * wave + up * Mathf.Cos(_elapsed * 37f + index))
            * 0.055f * amount * ringBias;

        FollowSource(part.whiteTransform, part.source.transform, offset);
        FollowSource(part.accentTransform, part.source.transform, -offset * 1.35f);
        SetGhostProperties(part.whiteRenderer, hardWhite, amount * 0.72f);
        SetGhostProperties(part.accentRenderer, accent, amount * 0.9f);
    }

    private void UpdateImpactGraphics(
        Color baseColor,
        Color accent,
        float amount,
        float progress
    )
    {
        if (_slashWhite == null)
        {
            return;
        }

        Quaternion rotation = _camera != null
            ? Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up)
            : Quaternion.identity;
        float scale = Mathf.Lerp(0.35f, 1.25f + _chargeResponse * 0.5f, amount);
        Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;

        PositionQuad(_slashBlack.transform, _hitPoint, rotation, new Vector3(scale * 1.45f, scale * 0.16f, 1f));
        PositionQuad(_slashWhite.transform, _hitPoint + right * 0.015f, rotation, new Vector3(scale, scale * 0.075f, 1f));
        PositionQuad(_slashAccent.transform, _hitPoint - right * 0.02f, rotation, new Vector3(scale * 0.72f, scale * 0.055f, 1f));

        SetFx(_slashBlack, hardBlack, hardWhite, amount, amount, progress);
        SetFx(_slashWhite, hardWhite, accent, amount, amount, progress);
        SetFx(_slashAccent, baseColor, accent, amount, amount, progress);
    }

    private void UpdateBlocks(
        Color baseColor,
        Color accent,
        float amount,
        float progress
    )
    {
        Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;
        Vector3 up = _camera != null ? _camera.transform.up : Vector3.up;
        Quaternion facing = _camera != null
            ? Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up)
            : Quaternion.identity;

        for (int i = 0; i < _blocks.Count; i++)
        {
            BlockState block = _blocks[i];
            float local = Mathf.Clamp01((progress - block.delay) / Mathf.Max(0.01f, 1f - block.delay));
            float scatter = 1f - Mathf.Pow(1f - local, 3f);
            float alpha = amount * (1f - Mathf.Clamp01((local - 0.55f) / 0.45f));
            block.transform.position = _hitPoint +
                right * block.direction.x * block.distance * scatter +
                up * block.direction.y * block.distance * scatter;
            block.transform.rotation = facing * Quaternion.Euler(0f, 0f, block.spin * scatter);
            block.transform.localScale = new Vector3(
                block.size.x * Mathf.Lerp(0.45f, 1f, scatter),
                block.size.y * Mathf.Lerp(0.45f, 1f, scatter),
                1f
            );
            Color color = i % 3 == 0 ? hardWhite : (i % 2 == 0 ? accent : baseColor);
            SetFx(block.renderer, color, accent, alpha, amount, _seed + i);
        }
    }

    private void UpdateHalftone(Color accent, float amount, float progress)
    {
        if (_halftoneTransform == null)
        {
            return;
        }

        Quaternion facing = _camera != null
            ? Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up)
            : Quaternion.identity;
        _halftoneTransform.position = _hitPoint;
        _halftoneTransform.rotation = facing;
        float scale = Mathf.Lerp(0.25f, 1.9f, progress);
        _halftoneTransform.localScale = new Vector3(scale, scale, 1f);
        SetFx(_halftoneRenderer, accent, hardWhite, amount * 0.34f, amount, _seed);
    }

    private void HandleDeath(EnemyDeathInfo info)
    {
        if (!CanPlayDeath())
        {
            return;
        }

        bool environment = info.Cause == EnemyDeathCause.Environment;
        StopFeedback();
        _deathPresentation = OphanimSystemErrorDeathPresentation.Spawn(
            this,
            info.ImpactPoint,
            environment || ResolveMagentaState(),
            environment
        );
    }

    private void ResolveReferences()
    {
        if (enemyTarget == null) enemyTarget = GetComponent<EnemyTarget>();
        if (ophanimEnemy == null) ophanimEnemy = GetComponent<OphanimEnemy>();
        if (visualFeedback == null) visualFeedback = GetComponent<OphanimVisualFeedback>();
        if (visualRoot == null && visualFeedback != null) visualRoot = visualFeedback.visualRoot;

        if ((partRenderers == null || partRenderers.Length == 0) && visualFeedback != null)
        {
            List<MeshRenderer> resolved = new List<MeshRenderer>();
            AddRenderer(resolved, visualFeedback.coreRenderer);

            if (visualFeedback.ringRenderers != null)
            {
                foreach (Renderer renderer in visualFeedback.ringRenderers)
                {
                    AddRenderer(resolved, renderer);
                }
            }

            partRenderers = resolved.ToArray();
        }
    }

    private static void AddRenderer(List<MeshRenderer> list, Renderer renderer)
    {
        MeshRenderer meshRenderer = renderer as MeshRenderer;

        if (meshRenderer != null &&
            meshRenderer.GetComponent<MeshFilter>() != null &&
            !list.Contains(meshRenderer))
        {
            list.Add(meshRenderer);
        }
    }

    private void CacheParts()
    {
        _parts.Clear();

        if (partRenderers == null)
        {
            return;
        }

        foreach (MeshRenderer source in partRenderers)
        {
            if (source == null || source.GetComponent<MeshFilter>() == null)
            {
                continue;
            }

            _parts.Add(new PartState
            {
                source = source,
                originalMaterials = source.sharedMaterials
            });
        }
    }

    private void BuildRuntimeVisuals()
    {
        if (!Application.isPlaying || !CanPlay())
        {
            return;
        }

        GameObject root = new GameObject("Ophanim_SystemError_Runtime");
        root.hideFlags = HideFlags.DontSave;
        _runtimeRoot = root.transform;

        for (int i = 0; i < _parts.Count; i++)
        {
            PartState part = _parts[i];
            part.whiteRenderer = CreateMeshCopy(part.source, $"GhostWhite_{i}", ghostMaterial, out part.whiteTransform);
            part.accentRenderer = CreateMeshCopy(part.source, $"GhostAccent_{i}", ghostMaterial, out part.accentTransform);
        }

        _slashBlack = CreateQuad("ImpactBlack", brokenRectMaterial);
        _slashWhite = CreateQuad("ImpactWhite", slashMaterial);
        _slashAccent = CreateQuad("ImpactAccent", slashMaterial);
        _halftoneRenderer = CreateQuad("Halftone", halftoneMaterial);
        _halftoneTransform = _halftoneRenderer != null ? _halftoneRenderer.transform : null;
        CreateBlocks(10);
    }

    private MeshRenderer CreateMeshCopy(
        MeshRenderer source,
        string objectName,
        Material material,
        out Transform createdTransform
    )
    {
        GameObject created = new GameObject(objectName);
        created.transform.SetParent(_runtimeRoot, true);
        MeshFilter filter = created.AddComponent<MeshFilter>();
        filter.sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer renderer = created.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        createdTransform = created.transform;
        return renderer;
    }

    private MeshRenderer CreateQuad(string objectName, Material material)
    {
        if (quadMesh == null || material == null || _runtimeRoot == null)
        {
            return null;
        }

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(_runtimeRoot, true);
        created.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        MeshRenderer renderer = created.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void CreateBlocks(int count)
    {
        if (blockMaterials == null || blockMaterials.Length == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            MeshRenderer renderer = CreateQuad($"Block_{i:00}", blockMaterials[i % blockMaterials.Length]);

            if (renderer == null)
            {
                continue;
            }

            float angle = (i / (float)count) * Mathf.PI * 2f + 0.31f;
            _blocks.Add(new BlockState
            {
                transform = renderer.transform,
                renderer = renderer,
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                distance = 0.45f + (i % 4) * 0.18f,
                delay = (i % 5) * 0.035f,
                spin = i % 2 == 0 ? 75f + i * 8f : -95f - i * 6f,
                size = new Vector2(0.08f + (i % 3) * 0.045f, 0.025f + (i % 2) * 0.02f)
            });
        }
    }

    private void OverrideSourceMaterials()
    {
        foreach (PartState part in _parts)
        {
            if (part.source != null)
            {
                part.source.sharedMaterial = targetMaterial;
            }
        }
    }

    private void RestoreSourceMaterials()
    {
        foreach (PartState part in _parts)
        {
            if (part.source != null && part.originalMaterials != null)
            {
                part.source.sharedMaterials = part.originalMaterials;
                part.source.SetPropertyBlock(null);
            }
        }
    }

    private void ShowRuntimeVisuals()
    {
        if (_runtimeRoot != null) _runtimeRoot.gameObject.SetActive(true);
    }

    private void HideRuntimeVisuals()
    {
        if (_runtimeRoot != null) _runtimeRoot.gameObject.SetActive(false);
    }

    private void StopFeedback()
    {
        _playing = false;
        RestoreSourceMaterials();
        HideRuntimeVisuals();
    }

    private bool CanPlay()
    {
        return targetMaterial != null && ghostMaterial != null && quadMesh != null &&
            slashMaterial != null && brokenRectMaterial != null &&
            partRenderers != null && partRenderers.Length > 0;
    }

    private bool CanPlayDeath()
    {
        return CanPlay() && halftoneMaterial != null &&
            blockMaterials != null && blockMaterials.Length > 0;
    }

    private bool ResolveMagentaState()
    {
        if (ophanimEnemy == null)
        {
            return false;
        }

        OphanimEnemy.OphanimState state = ophanimEnemy.CurrentState;
        return state == OphanimEnemy.OphanimState.Orbiting ||
            state == OphanimEnemy.OphanimState.Recovering;
    }

    private void Subscribe()
    {
        if (!_subscribed && enemyTarget != null)
        {
            enemyTarget.Died += HandleDeath;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed && enemyTarget != null)
        {
            enemyTarget.Died -= HandleDeath;
        }

        _subscribed = false;
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopFeedback();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (_runtimeRoot != null)
        {
            Destroy(_runtimeRoot.gameObject);
        }
    }

    private void SetBodyProperties(Renderer renderer, Color baseColor, Color accent, float amount)
    {
        if (renderer == null) return;
        _propertyBlock.Clear();
        _propertyBlock.SetColor(BaseColorId, baseColor);
        _propertyBlock.SetColor(AccentColorId, accent);
        _propertyBlock.SetFloat(IntensityId, 1f + amount * 2.8f);
        _propertyBlock.SetFloat(HitAmountId, amount);
        _propertyBlock.SetFloat(AdsAmountId, _firedAsAds ? _chargeResponse : 0f);
        _propertyBlock.SetFloat(KillAmountId, 0f);
        _propertyBlock.SetFloat(SeedId, _seed);
        _propertyBlock.SetFloat(VisibilityId, 1f);
        renderer.SetPropertyBlock(_propertyBlock);
    }

    private void SetGhostProperties(Renderer renderer, Color color, float amount)
    {
        SetFx(renderer, color, color, amount, amount, _seed);
    }

    private void SetFx(Renderer renderer, Color baseColor, Color accent, float alpha, float glitch, float seed)
    {
        if (renderer == null) return;
        _propertyBlock.Clear();
        Color color = baseColor;
        color.a = Mathf.Clamp01(alpha);
        _propertyBlock.SetColor(BaseColorId, color);
        _propertyBlock.SetColor(AccentColorId, accent);
        _propertyBlock.SetFloat(AlphaId, Mathf.Clamp01(alpha));
        _propertyBlock.SetFloat(VisibilityId, Mathf.Clamp01(alpha));
        _propertyBlock.SetFloat(GlitchAmountId, glitch);
        _propertyBlock.SetFloat(HitAmountId, glitch);
        _propertyBlock.SetFloat(SeedId, seed);
        renderer.SetPropertyBlock(_propertyBlock);
        renderer.enabled = alpha > 0.01f;
    }

    private static void FollowSource(Transform target, Transform source, Vector3 offset)
    {
        target.position = source.position + offset;
        target.rotation = source.rotation;
        target.localScale = source.lossyScale;
    }

    private static void PositionQuad(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        target.position = position;
        target.rotation = rotation;
        target.localScale = scale;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hipfireDuration = Mathf.Max(0.08f, hipfireDuration);
        adsMinimumDuration = Mathf.Max(0.12f, adsMinimumDuration);
        adsMaximumDuration = Mathf.Max(adsMinimumDuration, adsMaximumDuration);
        adsChargeExponent = Mathf.Max(0.1f, adsChargeExponent);
    }
#endif
}

/// <summary>
/// Detached visual-only Ophanim death copy. It survives the gameplay object's
/// same-frame destruction and breaks the rings apart before V4 deletion.
/// </summary>
public sealed class OphanimSystemErrorDeathPresentation : MonoBehaviour
{
    private sealed class CopyState
    {
        public Transform transform;
        public MeshRenderer renderer;
        public Transform whiteGhost;
        public MeshRenderer whiteRenderer;
        public Transform accentGhost;
        public MeshRenderer accentRenderer;
        public Vector3 basePosition;
        public Quaternion baseRotation;
        public Vector3 baseScale;
        public Vector3 direction;
        public Vector3 spinAxis;
        public float spin;
        public bool core;
    }

    private sealed class BlockState
    {
        public Transform transform;
        public MeshRenderer renderer;
        public Vector3 direction;
        public float distance;
        public float delay;
        public float spin;
        public Vector2 size;
    }

    private readonly List<CopyState> _copies = new List<CopyState>();
    private readonly List<BlockState> _blocks = new List<BlockState>();
    private OphanimSystemErrorCombatFeedback _source;
    private MaterialPropertyBlock _block;
    private Camera _camera;
    private MeshRenderer _impactBlack;
    private MeshRenderer _impactWhite;
    private MeshRenderer _impactAccent;
    private Transform _halftoneTransform;
    private MeshRenderer _halftoneRenderer;
    private Vector3 _center;
    private Vector3 _impactPoint;
    private Color _baseColor;
    private Color _accentColor;
    private Color _hardBlack;
    private Color _hardWhite;
    private float _age;
    private float _duration;
    private float _strength;
    private float _charge;
    private float _seed;
    private bool _environment;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int HitAmountId = Shader.PropertyToID("_HitAmount");
    private static readonly int AdsAmountId = Shader.PropertyToID("_AdsAmount");
    private static readonly int KillAmountId = Shader.PropertyToID("_KillAmount");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");

    public static OphanimSystemErrorDeathPresentation Spawn(
        OphanimSystemErrorCombatFeedback source,
        Vector3 impactPoint,
        bool magenta,
        bool environment
    )
    {
        if (source == null || source.partRenderers == null)
        {
            return null;
        }

        GameObject root = new GameObject("Ophanim_SystemError_Death");
        OphanimSystemErrorDeathPresentation presentation =
            root.AddComponent<OphanimSystemErrorDeathPresentation>();
        presentation.Initialize(source, impactPoint, magenta, environment);
        return presentation;
    }

    public void RefineWeaponShot(
        Vector3 hitPoint,
        bool firedAsAds,
        float charge01,
        float shotStrength
    )
    {
        _impactPoint = hitPoint;
        _charge = firedAsAds ? Mathf.Clamp01(charge01) : 0f;
        _strength = Mathf.Clamp01(Mathf.InverseLerp(0.35f, 1.5f, shotStrength));
        _duration = Mathf.Lerp(0.56f, 0.72f, Mathf.Max(_charge, _strength));
    }

    private void Initialize(
        OphanimSystemErrorCombatFeedback source,
        Vector3 impactPoint,
        bool magenta,
        bool environment
    )
    {
        _source = source;
        _impactPoint = impactPoint;
        _center = source.visualRoot != null ? source.visualRoot.position : source.transform.position;
        _baseColor = magenta ? source.magentaState : source.blueState;
        _accentColor = magenta ? source.cyanAccent : source.yellowAccent;
        _hardBlack = source.hardBlack;
        _hardWhite = source.hardWhite;
        _environment = environment;
        _duration = environment ? 0.68f : 0.6f;
        _strength = environment ? 0.82f : 0.72f;
        _seed = Random.Range(0.15f, 98f);
        _block = new MaterialPropertyBlock();
        _camera = Camera.main;

        CreateCopies();
        CreateGraphics();
        Evaluate(0f);
    }

    private void Update()
    {
        _age += Time.unscaledDeltaTime;
        Evaluate(Mathf.Clamp01(_age / Mathf.Max(0.01f, _duration)));

        if (_age >= _duration)
        {
            Destroy(gameObject);
        }
    }

    private void Evaluate(float progress)
    {
        if (_camera == null) _camera = Camera.main;
        float collapse = Mathf.Clamp01(progress / 0.24f);
        float deletion = Mathf.Clamp01((progress - 0.10f) / 0.72f);
        float fade = 1f - Mathf.Clamp01((progress - 0.54f) / 0.46f);
        float ghostAmount = Mathf.Sin(Mathf.Clamp01(progress / 0.72f) * Mathf.PI) * fade;

        for (int i = 0; i < _copies.Count; i++)
        {
            CopyState copy = _copies[i];
            float local = Mathf.Clamp01((deletion - i * 0.025f) / Mathf.Max(0.01f, 1f - i * 0.025f));
            float eased = 1f - Mathf.Pow(1f - local, 3f);
            float distance = copy.core ? 0.08f : Mathf.Lerp(0.1f, 1.15f + _strength * 0.45f, eased);
            copy.transform.position = copy.basePosition + copy.direction * distance * eased;
            copy.transform.rotation = copy.baseRotation * Quaternion.AngleAxis(copy.spin * eased, copy.spinAxis);
            float scale = copy.core
                ? Mathf.Lerp(1f, 0.04f, collapse * collapse)
                : Mathf.Lerp(1f, 0.12f, eased);
            copy.transform.localScale = copy.baseScale * scale;
            SetBody(copy.renderer, fade, deletion);

            Vector3 cameraRight = _camera != null ? _camera.transform.right : Vector3.right;
            Vector3 cameraUp = _camera != null ? _camera.transform.up : Vector3.up;
            float wave = Mathf.Sin(_age * 52f + i * 1.7f + _seed);
            Vector3 ghostOffset = (cameraRight * wave + cameraUp * Mathf.Cos(_age * 39f + i))
                * 0.075f * ghostAmount * (copy.core ? 0.7f : 1.1f);
            Follow(copy.whiteGhost, copy.transform, ghostOffset);
            Follow(copy.accentGhost, copy.transform, -ghostOffset * 1.45f);
            SetFx(copy.whiteRenderer, _hardWhite, ghostAmount * 0.68f, ghostAmount);
            SetFx(copy.accentRenderer, _accentColor, ghostAmount * 0.86f, ghostAmount);
        }

        UpdateImpact(progress, ghostAmount);
        UpdateBlocks(progress, ghostAmount);
        UpdateHalftone(progress, ghostAmount);
    }

    private void CreateCopies()
    {
        for (int i = 0; i < _source.partRenderers.Length; i++)
        {
            MeshRenderer sourceRenderer = _source.partRenderers[i];

            if (sourceRenderer == null)
            {
                continue;
            }

            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();

            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                continue;
            }

            Transform bodyTransform;
            MeshRenderer bodyRenderer = CreateMeshCopy(sourceFilter.sharedMesh, _source.targetMaterial, $"Body_{i}", out bodyTransform);
            Transform whiteTransform;
            MeshRenderer whiteRenderer = CreateMeshCopy(sourceFilter.sharedMesh, _source.ghostMaterial, $"WhiteGhost_{i}", out whiteTransform);
            Transform accentTransform;
            MeshRenderer accentRenderer = CreateMeshCopy(sourceFilter.sharedMesh, _source.ghostMaterial, $"AccentGhost_{i}", out accentTransform);

            Vector3 radial = sourceRenderer.bounds.center - _center;
            radial.y *= 0.55f;

            if (radial.sqrMagnitude < 0.001f)
            {
                radial = i == 0 ? Vector3.up : Quaternion.Euler(0f, i * 72f, 0f) * Vector3.right;
            }

            CopyState copy = new CopyState
            {
                transform = bodyTransform,
                renderer = bodyRenderer,
                whiteGhost = whiteTransform,
                whiteRenderer = whiteRenderer,
                accentGhost = accentTransform,
                accentRenderer = accentRenderer,
                basePosition = sourceRenderer.transform.position,
                baseRotation = sourceRenderer.transform.rotation,
                baseScale = sourceRenderer.transform.lossyScale,
                direction = radial.normalized,
                spinAxis = (Vector3.up + radial.normalized * 0.7f).normalized,
                spin = (i % 2 == 0 ? 1f : -1f) * (150f + i * 70f),
                core = sourceRenderer == _source.visualFeedback.coreRenderer
            };

            bodyTransform.position = copy.basePosition;
            bodyTransform.rotation = copy.baseRotation;
            bodyTransform.localScale = copy.baseScale;
            _copies.Add(copy);
        }
    }

    private MeshRenderer CreateMeshCopy(Mesh mesh, Material material, string objectName, out Transform resultTransform)
    {
        GameObject created = new GameObject(objectName);
        created.transform.SetParent(transform, true);
        created.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = created.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        resultTransform = created.transform;
        return renderer;
    }

    private void CreateGraphics()
    {
        _impactBlack = CreateQuad("ImpactBlack", _source.brokenRectMaterial);
        _impactWhite = CreateQuad("ImpactWhite", _source.slashMaterial);
        _impactAccent = CreateQuad("ImpactAccent", _source.slashMaterial);
        _halftoneRenderer = CreateQuad("Halftone", _source.halftoneMaterial);
        _halftoneTransform = _halftoneRenderer != null ? _halftoneRenderer.transform : null;

        if (_source.blockMaterials == null || _source.blockMaterials.Length == 0)
        {
            return;
        }

        const int count = 14;
        for (int i = 0; i < count; i++)
        {
            MeshRenderer renderer = CreateQuad($"DeletionBlock_{i:00}", _source.blockMaterials[i % _source.blockMaterials.Length]);
            float angle = i / (float)count * Mathf.PI * 2f + 0.23f;
            _blocks.Add(new BlockState
            {
                transform = renderer.transform,
                renderer = renderer,
                direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.72f, ((i % 3) - 1) * 0.18f).normalized,
                distance = 0.8f + (i % 5) * 0.38f,
                delay = (i % 6) * 0.025f,
                spin = (i % 2 == 0 ? 1f : -1f) * (100f + i * 13f),
                size = new Vector2(0.14f + (i % 4) * 0.07f, 0.035f + (i % 3) * 0.025f)
            });
        }
    }

    private MeshRenderer CreateQuad(string objectName, Material material)
    {
        if (_source.quadMesh == null || material == null)
        {
            return null;
        }

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(transform, true);
        created.AddComponent<MeshFilter>().sharedMesh = _source.quadMesh;
        MeshRenderer renderer = created.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void UpdateImpact(float progress, float amount)
    {
        if (_impactWhite == null) return;
        Quaternion facing = _camera != null
            ? Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up)
            : Quaternion.identity;
        Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;
        float burst = Mathf.Sin(Mathf.Clamp01(progress / 0.55f) * Mathf.PI);
        float scale = Mathf.Lerp(0.55f, 2.2f + _charge * 0.8f, progress);
        Place(_impactBlack.transform, _impactPoint, facing, new Vector3(scale * 1.45f, scale * 0.18f, 1f));
        Place(_impactWhite.transform, _impactPoint + right * 0.02f, facing, new Vector3(scale, scale * 0.075f, 1f));
        Place(_impactAccent.transform, _impactPoint - right * 0.025f, facing, new Vector3(scale * 0.72f, scale * 0.05f, 1f));
        SetFx(_impactBlack, _hardBlack, burst, amount);
        SetFx(_impactWhite, _hardWhite, burst, amount);
        SetFx(_impactAccent, _accentColor, burst, amount);
    }

    private void UpdateBlocks(float progress, float amount)
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            BlockState state = _blocks[i];
            float local = Mathf.Clamp01((progress - 0.12f - state.delay) / 0.64f);
            float eased = 1f - Mathf.Pow(1f - local, 3f);
            state.transform.position = _center + state.direction * state.distance * eased;
            Quaternion facing = _camera != null
                ? Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up)
                : Quaternion.identity;
            state.transform.rotation = facing * Quaternion.Euler(0f, 0f, state.spin * eased);
            state.transform.localScale = new Vector3(state.size.x, state.size.y, 1f) * Mathf.Lerp(0.35f, 1f, eased);
            float alpha = Mathf.Sin(local * Mathf.PI) * Mathf.Lerp(0.8f, 1f, _strength);
            Color color = i % 3 == 0 ? _hardWhite : (i % 2 == 0 ? _accentColor : _baseColor);
            SetFx(state.renderer, color, alpha, amount);
        }
    }

    private void UpdateHalftone(float progress, float amount)
    {
        if (_halftoneTransform == null) return;
        Quaternion facing = _camera != null
            ? Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up)
            : Quaternion.identity;
        _halftoneTransform.position = _center;
        _halftoneTransform.rotation = facing;
        float scale = Mathf.Lerp(0.4f, 4.4f, 1f - Mathf.Pow(1f - progress, 3f));
        _halftoneTransform.localScale = new Vector3(scale, scale, 1f);
        SetFx(_halftoneRenderer, _accentColor, amount * 0.34f, amount);
    }

    private void SetBody(Renderer renderer, float visibility, float deletion)
    {
        if (renderer == null) return;
        _block.Clear();
        _block.SetColor(BaseColorId, _baseColor);
        _block.SetColor(AccentColorId, _accentColor);
        _block.SetFloat(HitAmountId, 1f);
        _block.SetFloat(AdsAmountId, _charge);
        _block.SetFloat(KillAmountId, deletion);
        _block.SetFloat(SeedId, _seed);
        _block.SetFloat(VisibilityId, visibility);
        renderer.SetPropertyBlock(_block);
    }

    private void SetFx(Renderer renderer, Color color, float alpha, float glitch)
    {
        if (renderer == null) return;
        _block.Clear();
        Color tinted = color;
        tinted.a = Mathf.Clamp01(alpha);
        _block.SetColor(BaseColorId, tinted);
        _block.SetColor(AccentColorId, _accentColor);
        _block.SetFloat(AlphaId, Mathf.Clamp01(alpha));
        _block.SetFloat(VisibilityId, Mathf.Clamp01(alpha));
        _block.SetFloat(GlitchAmountId, glitch);
        _block.SetFloat(HitAmountId, glitch);
        _block.SetFloat(SeedId, _seed);
        renderer.SetPropertyBlock(_block);
        renderer.enabled = alpha > 0.01f;
    }

    private static void Follow(Transform target, Transform source, Vector3 offset)
    {
        target.position = source.position + offset;
        target.rotation = source.rotation;
        target.localScale = source.localScale;
    }

    private static void Place(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        target.position = position;
        target.rotation = rotation;
        target.localScale = scale;
    }
}
