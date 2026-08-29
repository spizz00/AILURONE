#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Detached V4 System Error death presentation. The gameplay Spike can be
/// destroyed immediately while this visual-only copy finishes its animation.
/// </summary>
public sealed class SpikeSystemErrorDeathPresentation : MonoBehaviour
{
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

    private MeshRenderer _bodyRenderer;
    private Transform _bodyTransform;
    private MeshRenderer _whiteGhost;
    private Transform _whiteGhostTransform;
    private MeshRenderer _accentGhost;
    private Transform _accentGhostTransform;
    private Transform _graphicsRoot;
    private MeshRenderer _impactWhite;
    private MeshRenderer _impactBlack;
    private MeshRenderer _impactAccent;
    private MeshRenderer _halftone;
    private BlockState[] _blocks;

    private MaterialPropertyBlock _bodyBlock;
    private MaterialPropertyBlock _ghostBlock;
    private MaterialPropertyBlock _fxBlock;

    private Camera _camera;
    private Color _baseColor;
    private Color _accentColor;
    private Color _hardBlack;
    private Color _hardWhite;
    private Vector3 _impactPoint;
    private Vector3 _bodyBasePosition;
    private Quaternion _bodyBaseRotation;
    private Vector3 _bodyBaseScale;
    private float _elapsed;
    private float _duration;
    private float _seed;
    private float _presentationStrength;
    private bool _environmentalDeath;
    private bool _firedAsAds;

    public static SpikeSystemErrorDeathPresentation Spawn(
        SpikeSystemErrorCombatFeedback source,
        Vector3 impactPoint,
        bool magentaState,
        bool environmentalDeath
    )
    {
        if (source == null ||
            source.bodyRenderer == null ||
            source.quadMesh == null ||
            source.targetMaterial == null ||
            source.ghostMaterial == null ||
            source.slashMaterial == null ||
            source.brokenRectMaterial == null ||
            source.halftoneMaterial == null ||
            source.blockMaterials == null ||
            source.blockMaterials.Length == 0)
        {
            return null;
        }

        MeshFilter sourceFilter =
            source.bodyRenderer.GetComponent<MeshFilter>();

        if (sourceFilter == null ||
            sourceFilter.sharedMesh == null)
        {
            return null;
        }

        GameObject root =
            new GameObject(
                environmentalDeath
                    ? "Spike_SystemErrorDeath_Environment"
                    : "Spike_SystemErrorDeath_Weapon"
            );

        SpikeSystemErrorDeathPresentation presentation =
            root.AddComponent<
                SpikeSystemErrorDeathPresentation
            >();

        presentation.Initialize(
            source,
            sourceFilter.sharedMesh,
            impactPoint,
            magentaState,
            environmentalDeath
        );

        return presentation;
    }

    public void RefineWeaponShot(
        Vector3 worldHitPoint,
        bool firedAsAds,
        float charge01,
        float shotStrength
    )
    {
        if (_environmentalDeath)
        {
            return;
        }

        _impactPoint = worldHitPoint;
        _firedAsAds = firedAsAds;

        float chargeResponse =
            firedAsAds
                ? Mathf.Pow(
                    Mathf.Clamp01(charge01),
                    1.35f
                )
                : 0f;

        float strengthResponse =
            Mathf.InverseLerp(
                0.82f,
                1.45f,
                shotStrength
            );

        _presentationStrength =
            firedAsAds
                ? Mathf.Lerp(
                    0.92f,
                    1.18f,
                    chargeResponse
                )
                : Mathf.Lerp(
                    0.88f,
                    1.02f,
                    strengthResponse
                );

        _duration =
            firedAsAds
                ? Mathf.Lerp(
                    0.56f,
                    0.64f,
                    chargeResponse
                )
                : 0.54f;
    }

    private void Initialize(
        SpikeSystemErrorCombatFeedback source,
        Mesh bodyMesh,
        Vector3 impactPoint,
        bool magentaState,
        bool environmentalDeath
    )
    {
        _environmentalDeath = environmentalDeath;
        _impactPoint = impactPoint;
        _duration = environmentalDeath ? 0.62f : 0.56f;
        _presentationStrength =
            environmentalDeath ? 0.92f : 0.96f;
        _seed = Random.Range(0.15f, 98f);
        _camera = Camera.main;

        _baseColor =
            magentaState
                ? source.magentaState
                : source.blueState;

        _accentColor =
            magentaState
                ? source.cyanAccent
                : source.yellowAccent;

        _hardBlack = source.hardBlack;
        _hardWhite = source.hardWhite;

        _bodyBlock = new MaterialPropertyBlock();
        _ghostBlock = new MaterialPropertyBlock();
        _fxBlock = new MaterialPropertyBlock();

        CreateBodyCopies(
            source.bodyRenderer,
            bodyMesh,
            source.targetMaterial,
            source.ghostMaterial
        );

        CreateGraphics(
            source.quadMesh,
            source.slashMaterial,
            source.brokenRectMaterial,
            source.halftoneMaterial,
            source.blockMaterials
        );

        ConfigureBlocks();
        UpdateAttachment();
        Evaluate(0f);
    }

    private void Update()
    {
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
        UpdateAttachment();

        if (_elapsed >= _duration)
        {
            Destroy(gameObject);
        }
    }

    private void Evaluate(
        float normalizedTime
    )
    {
        float hitAmount =
            1f -
            SmoothRange(
                0.055f,
                0.18f,
                normalizedTime
            );

        float killAmount =
            SmoothRange(
                0.045f,
                0.38f,
                normalizedTime
            ) *
            (1f -
             SmoothRange(
                 0.70f,
                 0.96f,
                 normalizedTime
             ));

        float visibility =
            normalizedTime < 0.33f
                ? 1f
                : 0f;

        float pull =
            SmoothRange(
                0.06f,
                0.33f,
                normalizedTime
            );

        float scaleCollapse =
            1f -
            SmoothRange(
                0.17f,
                0.36f,
                normalizedTime
            ) * 0.10f;

        ApplyBody(
            hitAmount,
            killAmount *
            (_firedAsAds ? 0.72f : 0.55f),
            killAmount,
            visibility,
            1.08f * _presentationStrength
        );

        ApplyBodyTransform(
            new Vector3(
                Mathf.Sin(normalizedTime * 88f) *
                0.016f * pull,
                Mathf.Cos(normalizedTime * 71f) *
                0.012f * pull,
                0f
            ),
            Quaternion.Euler(
                0f,
                0f,
                Mathf.Sin(normalizedTime * 55f) *
                2.4f * pull
            ),
            Vector3.one * scaleCollapse
        );

        float ghostEnvelope =
            Mathf.Sin(
                Mathf.Clamp01(
                    Mathf.InverseLerp(
                        0.02f,
                        0.68f,
                        normalizedTime
                    )
                ) * Mathf.PI
            );

        ApplyGhosts(
            ghostEnvelope *
            (_firedAsAds ? 0.94f : 0.84f),
            ghostEnvelope * 0.76f,
            Mathf.Clamp01(
                killAmount * 1.25f
            ),
            Mathf.Lerp(0.035f, 0.23f, pull),
            Mathf.Lerp(-0.030f, -0.19f, pull)
        );

        EvaluateImpactGraphics(normalizedTime);
        EvaluateBlocks(normalizedTime);
        EvaluateHalftone(normalizedTime);
    }

    private void EvaluateImpactGraphics(
        float normalizedTime
    )
    {
        float attack =
            SmoothRange(
                0f,
                0.10f,
                normalizedTime
            );

        float release =
            1f -
            SmoothRange(
                0.33f,
                0.70f,
                normalizedTime
            );

        float envelope =
            Mathf.Clamp01(attack * release);

        float width =
            1.42f * _presentationStrength;

        float height =
            0.78f * _presentationStrength;

        float angle =
            _environmentalDeath ? 12f : -12f;

        if (_impactBlack != null)
        {
            _impactBlack.enabled =
                envelope > 0.001f;
            _impactBlack.transform.localPosition =
                new Vector3(0.045f, -0.030f, 0.004f);
            _impactBlack.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle - 3f);
            _impactBlack.transform.localScale =
                new Vector3(
                    width * 1.12f,
                    height * 1.12f,
                    1f
                );

            SetFx(
                _impactBlack,
                _hardBlack,
                envelope * 0.94f,
                1f
            );
        }

        if (_impactWhite != null)
        {
            float snap =
                EaseOutBack(
                    Mathf.Clamp01(
                        normalizedTime / 0.16f
                    )
                );

            _impactWhite.enabled =
                envelope > 0.001f;
            _impactWhite.transform.localPosition =
                Vector3.zero;
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
                _hardWhite,
                envelope,
                1.45f
            );
        }

        if (_impactAccent != null)
        {
            float accentTime =
                Mathf.Clamp01(
                    (normalizedTime - 0.07f) /
                    0.50f
                );

            float accentEnvelope =
                Mathf.Sin(accentTime * Mathf.PI) *
                0.82f;

            _impactAccent.enabled =
                normalizedTime >= 0.07f &&
                accentEnvelope > 0.001f;
            _impactAccent.transform.localPosition =
                new Vector3(-0.055f, 0.038f, -0.002f);
            _impactAccent.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle + 17f);
            _impactAccent.transform.localScale =
                new Vector3(
                    width * 0.72f,
                    height * 0.66f,
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
        float normalizedTime
    )
    {
        if (_blocks == null)
        {
            return;
        }

        for (int index = 0;
             index < _blocks.Length;
             index++)
        {
            BlockState block = _blocks[index];
            float delay =
                0.055f + block.startDelay;

            bool visible =
                normalizedTime >= delay;

            block.renderer.enabled = visible;

            if (!visible)
            {
                continue;
            }

            float localTime =
                Mathf.Clamp01(
                    (normalizedTime - delay) /
                    Mathf.Max(0.08f, 0.91f - delay)
                );

            float envelope =
                Mathf.Sin(localTime * Mathf.PI) *
                (1f -
                 SmoothRange(
                     0.72f,
                     1f,
                     localTime
                 ));

            float travel =
                block.distance *
                Mathf.Lerp(
                    1.30f,
                    1.58f,
                    Mathf.Clamp01(
                        _presentationStrength - 0.88f
                    ) / 0.30f
                ) *
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
                    ? _hardWhite
                    : block.colorRole == 1
                        ? _hardBlack
                        : _accentColor;

            SetFx(
                block.renderer,
                color,
                envelope,
                block.colorRole == 0
                    ? 1.24f
                    : 1f
            );
        }
    }

    private void EvaluateHalftone(
        float normalizedTime
    )
    {
        if (_halftone == null)
        {
            return;
        }

        float localTime =
            Mathf.Clamp01(
                Mathf.InverseLerp(
                    0.20f,
                    0.88f,
                    normalizedTime
                )
            );

        float envelope =
            Mathf.Sin(localTime * Mathf.PI) *
            (1f -
             SmoothRange(
                 0.72f,
                 1f,
                 localTime
             ));

        _halftone.enabled =
            normalizedTime >= 0.20f &&
            envelope > 0.001f;
        _halftone.transform.localPosition =
            new Vector3(0.05f, -0.02f, 0.012f);
        _halftone.transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -8f + localTime * 7f
            );
        _halftone.transform.localScale =
            Vector3.one *
            Mathf.Lerp(
                0.75f,
                2.55f,
                EaseOutCubic(localTime)
            );

        SetFx(
            _halftone,
            _hardBlack,
            envelope * 0.52f,
            1f
        );
    }

    private void ApplyBody(
        float hitAmount,
        float adsAmount,
        float killAmount,
        float visibility,
        float intensity
    )
    {
        if (_bodyRenderer == null)
        {
            return;
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
        _bodyRenderer.SetPropertyBlock(_bodyBlock);
    }

    private void ApplyGhosts(
        float whiteAlpha,
        float accentAlpha,
        float glitch,
        float whiteOffset,
        float accentOffset
    )
    {
        PositionGhost(
            _whiteGhostTransform,
            whiteOffset,
            0.026f
        );

        PositionGhost(
            _accentGhostTransform,
            accentOffset,
            -0.034f
        );

        ApplyGhostRenderer(
            _whiteGhost,
            _hardWhite,
            whiteAlpha,
            glitch,
            _seed + 0.73f
        );

        ApplyGhostRenderer(
            _accentGhost,
            _accentColor,
            accentAlpha,
            glitch * 0.86f,
            _seed + 4.11f
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

    private void ApplyBodyTransform(
        Vector3 positionOffset,
        Quaternion rotationOffset,
        Vector3 scaleMultiplier
    )
    {
        if (_bodyTransform == null)
        {
            return;
        }

        _bodyTransform.localPosition =
            _bodyBasePosition + positionOffset;
        _bodyTransform.localRotation =
            _bodyBaseRotation * rotationOffset;
        _bodyTransform.localScale =
            Vector3.Scale(
                _bodyBaseScale,
                scaleMultiplier
            );
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

    private void CreateBodyCopies(
        MeshRenderer sourceRenderer,
        Mesh bodyMesh,
        Material targetMaterial,
        Material ghostMaterial
    )
    {
        _bodyRenderer =
            CreateBodyRenderer(
                "Body",
                bodyMesh,
                targetMaterial,
                sourceRenderer,
                0
            );

        _bodyTransform = _bodyRenderer.transform;
        _bodyBasePosition = _bodyTransform.localPosition;
        _bodyBaseRotation = _bodyTransform.localRotation;
        _bodyBaseScale = _bodyTransform.localScale;

        _whiteGhost =
            CreateBodyRenderer(
                "Ghost_White",
                bodyMesh,
                ghostMaterial,
                sourceRenderer,
                1
            );

        _whiteGhostTransform =
            _whiteGhost.transform;

        _accentGhost =
            CreateBodyRenderer(
                "Ghost_Accent",
                bodyMesh,
                ghostMaterial,
                sourceRenderer,
                2
            );

        _accentGhostTransform =
            _accentGhost.transform;

        _whiteGhost.enabled = false;
        _accentGhost.enabled = false;
    }

    private MeshRenderer CreateBodyRenderer(
        string objectName,
        Mesh mesh,
        Material material,
        MeshRenderer sourceRenderer,
        int sortingOffset
    )
    {
        GameObject bodyObject =
            new GameObject(objectName);

        bodyObject.layer =
            sourceRenderer.gameObject.layer;
        bodyObject.transform.SetParent(
            transform,
            false
        );
        bodyObject.transform.position =
            sourceRenderer.transform.position;
        bodyObject.transform.rotation =
            sourceRenderer.transform.rotation;
        bodyObject.transform.localScale =
            sourceRenderer.transform.lossyScale;

        MeshFilter filter =
            bodyObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer =
            bodyObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterials =
            RepeatMaterial(
                material,
                Mathf.Max(
                    1,
                    sourceRenderer.sharedMaterials.Length
                )
            );
        renderer.sortingLayerID =
            sourceRenderer.sortingLayerID;
        renderer.sortingOrder =
            sourceRenderer.sortingOrder + sortingOffset;
        renderer.shadowCastingMode =
            ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return renderer;
    }

    private void CreateGraphics(
        Mesh quadMesh,
        Material slashMaterial,
        Material brokenRectMaterial,
        Material halftoneMaterial,
        Material[] blockMaterials
    )
    {
        GameObject graphicsObject =
            new GameObject("Impact_Graphics");

        _graphicsRoot = graphicsObject.transform;
        _graphicsRoot.SetParent(transform, false);

        _impactBlack =
            CreateGraphic(
                "Impact_Black",
                quadMesh,
                slashMaterial,
                110
            );

        _impactWhite =
            CreateGraphic(
                "Impact_White",
                quadMesh,
                slashMaterial,
                111
            );

        _impactAccent =
            CreateGraphic(
                "Impact_Accent",
                quadMesh,
                brokenRectMaterial,
                112
            );

        _halftone =
            CreateGraphic(
                "Halftone_Residue",
                quadMesh,
                halftoneMaterial,
                109
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
                            $"Block_{index + 1:00}",
                            quadMesh,
                            material,
                            120 + index
                        )
                };
        }
    }

    private MeshRenderer CreateGraphic(
        string objectName,
        Mesh mesh,
        Material material,
        int sortingOrder
    )
    {
        GameObject graphicObject =
            new GameObject(objectName);

        graphicObject.transform.SetParent(
            _graphicsRoot,
            false
        );

        MeshFilter filter =
            graphicObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

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

    private void ConfigureBlocks()
    {
        float[] angles =
        {
            3f, 31f, 66f, 101f,
            137f, 169f, 202f, 232f,
            263f, 296f, 327f, 349f
        };

        Random.State previousState = Random.state;
        Random.InitState(
            Mathf.RoundToInt(_seed * 100f) + 921
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
                Random.Range(0.72f, 1.48f);
            block.startDelay =
                Random.Range(0f, 0.14f);
            block.spin =
                Random.Range(-150f, 150f);

            float longSize =
                Random.Range(0.38f, 0.78f);
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

        if (_camera == null)
        {
            _graphicsRoot.position = _impactPoint;
            return;
        }

        Vector3 towardCamera =
            _camera.transform.position -
            _impactPoint;

        if (towardCamera.sqrMagnitude <= 0.0001f)
        {
            towardCamera =
                -_camera.transform.forward;
        }

        _graphicsRoot.position =
            _impactPoint +
            towardCamera.normalized * 0.035f;
        _graphicsRoot.rotation =
            Quaternion.LookRotation(
                towardCamera.normalized,
                _camera.transform.up
            );
    }

    private void PositionGhost(
        Transform ghostTransform,
        float horizontalOffset,
        float verticalOffset
    )
    {
        if (ghostTransform == null ||
            _bodyTransform == null)
        {
            return;
        }

        Vector3 worldOffset =
            _camera != null
                ? _camera.transform.right * horizontalOffset +
                  _camera.transform.up * verticalOffset
                : new Vector3(
                    horizontalOffset,
                    verticalOffset,
                    0f
                );

        ghostTransform.position =
            _bodyTransform.position + worldOffset;
        ghostTransform.rotation =
            _bodyTransform.rotation;
        ghostTransform.localScale =
            _bodyTransform.localScale;
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
            1f -
            inverse * inverse * inverse;
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
}
