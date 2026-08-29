#pragma warning disable 0618
#pragma warning disable 0414
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ground Bot 状态切换时短暂出现的工业化头顶提示：
/// - 橙色问号：CombatArea 激活并进入扫描；
/// - 红色感叹号：首次确认发现玩家或重新锁定玩家。
///
/// 该提示只负责状态切换事件，不会在整个警戒或战斗阶段常驻。
/// 所有 UI 都在运行时生成，不需要场景对象或额外 Prefab。
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundBotAlertIndicatorFX : MonoBehaviour
{
    public enum IndicatorKind
    {
        Suspicion,
        Detected
    }

    private static Sprite _sharedPanelSprite;

    private Transform _owner;
    private EnemyTarget _enemyTarget;
    private RectTransform _displayRoot;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Image _borderImage;
    private Image _panelImage;
    private Image _topAccent;
    private Image _bottomAccent;
    private TMP_Text _symbolText;
    private Camera _cachedCamera;
    private Vector3 _stableLocalAnchor;
    private IndicatorKind _kind;
    private Color _accentColor;
    private float _duration;
    private float _visualScale;
    private float _elapsed;
    private bool _isCancelled;

    private const float ReferenceDistance = 10f;
    private const float BaseWorldScale = 0.0052f;
    private const float MaximumVisibleDistance = 45f;

    public static GroundBotAlertIndicatorFX Spawn(
        Transform owner,
        EnemyTarget enemyTarget,
        IndicatorKind kind,
        float duration,
        float heightPadding,
        float visualScale
    )
    {
        if (owner == null)
        {
            return null;
        }

        GameObject runtimeObject = new GameObject(
            kind == IndicatorKind.Suspicion
                ? "GroundBot_SuspicionIndicator_Runtime"
                : "GroundBot_DetectedIndicator_Runtime",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(GroundBotAlertIndicatorFX)
        );

        runtimeObject.layer = owner.gameObject.layer;

        GroundBotAlertIndicatorFX controller =
            runtimeObject.GetComponent<GroundBotAlertIndicatorFX>();

        controller.Initialize(
            owner,
            enemyTarget,
            kind,
            duration,
            heightPadding,
            visualScale
        );

        return controller;
    }

    public void CancelImmediate()
    {
        if (_isCancelled)
        {
            return;
        }

        _isCancelled = true;
        Destroy(gameObject);
    }

    private void Initialize(
        Transform owner,
        EnemyTarget enemyTarget,
        IndicatorKind kind,
        float duration,
        float heightPadding,
        float visualScale
    )
    {
        _owner = owner;
        _enemyTarget = enemyTarget;
        _kind = kind;
        _duration = Mathf.Clamp(duration, 0.20f, 1.20f);
        _visualScale = Mathf.Clamp(visualScale, 0.55f, 1.75f);
        _accentColor = kind == IndicatorKind.Suspicion
            ? new Color(1.00f, 0.48f, 0.08f, 1f)
            : new Color(1.00f, 0.075f, 0.045f, 1f);

        CacheStableAnchor(Mathf.Max(0.05f, heightPadding));
        BuildRuntimeUI();
        ApplyStaticStyle();
        UpdateVisuals(0f);
    }

    private void LateUpdate()
    {
        if (_isCancelled ||
            _owner == null ||
            (_enemyTarget != null && _enemyTarget.IsDead))
        {
            CancelImmediate();
            return;
        }

        _elapsed += Time.deltaTime;

        if (_elapsed >= _duration)
        {
            CancelImmediate();
            return;
        }

        if (_cachedCamera == null || !_cachedCamera.isActiveAndEnabled)
        {
            _cachedCamera = Camera.main;
        }

        if (_cachedCamera == null)
        {
            if (_canvas != null)
            {
                _canvas.enabled = false;
            }

            return;
        }

        Vector3 worldAnchor =
            _owner.TransformPoint(_stableLocalAnchor);

        Vector3 cameraToAnchor =
            worldAnchor - _cachedCamera.transform.position;

        float distance = cameraToAnchor.magnitude;

        bool visible =
            Vector3.Dot(
                _cachedCamera.transform.forward,
                cameraToAnchor
            ) > 0.05f &&
            distance <= MaximumVisibleDistance;

        _canvas.enabled = visible;

        if (!visible)
        {
            return;
        }

        float normalizedTime =
            Mathf.Clamp01(_elapsed / _duration);

        float rise =
            Mathf.Lerp(
                _kind == IndicatorKind.Detected ? -0.06f : -0.035f,
                0.10f,
                SmoothStep01(normalizedTime)
            );

        if (_kind == IndicatorKind.Suspicion)
        {
            float wobbleFade = 1f - normalizedTime;
            rise += Mathf.Sin(_elapsed * 34f) * 0.012f * wobbleFade;
        }

        _displayRoot.position =
            worldAnchor + Vector3.up * rise;

        _displayRoot.rotation = Quaternion.LookRotation(
            _displayRoot.position - _cachedCamera.transform.position,
            _cachedCamera.transform.up
        );

        float distanceMultiplier = Mathf.Clamp(
            distance / ReferenceDistance,
            0.86f,
            1.72f
        );

        float popScale = ResolvePopScale(normalizedTime);
        float worldScale =
            BaseWorldScale *
            distanceMultiplier *
            _visualScale *
            popScale;

        _displayRoot.localScale = Vector3.one * worldScale;
        UpdateVisuals(normalizedTime);
    }

    private void CacheStableAnchor(float heightPadding)
    {
        Renderer[] allRenderers =
            _owner.GetComponentsInChildren<Renderer>(true);

        bool foundRenderer = false;
        Bounds combinedBounds = default;

        foreach (Renderer candidate in allRenderers)
        {
            if (!IsStableBodyRenderer(candidate))
            {
                continue;
            }

            if (!foundRenderer)
            {
                combinedBounds = candidate.bounds;
                foundRenderer = true;
            }
            else
            {
                combinedBounds.Encapsulate(candidate.bounds);
            }
        }

        Vector3 worldAnchor = foundRenderer
            ? new Vector3(
                combinedBounds.center.x,
                combinedBounds.max.y + heightPadding,
                combinedBounds.center.z
            )
            : _owner.position + Vector3.up * 2f;

        _stableLocalAnchor =
            _owner.InverseTransformPoint(worldAnchor);
    }

    private static bool IsStableBodyRenderer(Renderer candidate)
    {
        if (candidate == null ||
            !candidate.enabled ||
            !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!(candidate is MeshRenderer) &&
            !(candidate is SkinnedMeshRenderer))
        {
            return false;
        }

        if (candidate.GetComponentInParent<GroundBotProjectile>() != null)
        {
            return false;
        }

        return true;
    }

    private void BuildRuntimeUI()
    {
        _displayRoot = GetComponent<RectTransform>();
        _displayRoot.sizeDelta = new Vector2(96f, 104f);

        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 190;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 24f;
        scaler.referencePixelsPerUnit = 100f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _borderImage = CreateImage(
            "IndicatorBorder",
            _displayRoot,
            new Vector2(60f, 76f),
            Vector2.zero
        );

        _panelImage = CreateImage(
            "IndicatorPanel",
            _displayRoot,
            new Vector2(52f, 68f),
            Vector2.zero
        );

        _topAccent = CreateImage(
            "TopSignalBar",
            _displayRoot,
            new Vector2(32f, 4f),
            new Vector2(6f, 42f)
        );

        _bottomAccent = CreateImage(
            "BottomSignalBar",
            _displayRoot,
            new Vector2(19f, 3f),
            new Vector2(-10f, -43f)
        );

        GameObject symbolObject = new GameObject(
            "StateSymbol",
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        symbolObject.transform.SetParent(_displayRoot, false);
        symbolObject.layer = gameObject.layer;

        RectTransform symbolRect =
            symbolObject.GetComponent<RectTransform>();

        symbolRect.anchorMin = new Vector2(0.5f, 0.5f);
        symbolRect.anchorMax = new Vector2(0.5f, 0.5f);
        symbolRect.pivot = new Vector2(0.5f, 0.5f);
        symbolRect.sizeDelta = new Vector2(56f, 72f);
        symbolRect.anchoredPosition =
            _kind == IndicatorKind.Suspicion
                ? new Vector2(0f, 1f)
                : new Vector2(0f, 2f);

        _symbolText =
            symbolObject.GetComponent<TextMeshProUGUI>();

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;

        if (defaultFont == null)
        {
            defaultFont = Resources.Load<TMP_FontAsset>(
                "Fonts & Materials/LiberationSans SDF"
            );
        }

        _symbolText.font = defaultFont;
        _symbolText.text =
            _kind == IndicatorKind.Suspicion ? "?" : "!";
        _symbolText.fontSize =
            _kind == IndicatorKind.Suspicion ? 54f : 60f;
        _symbolText.fontStyle = FontStyles.Bold;
        _symbolText.alignment = TextAlignmentOptions.Center;
        _symbolText.textWrappingMode = TextWrappingModes.NoWrap;
        _symbolText.raycastTarget = false;
        _symbolText.enableAutoSizing = false;
        _symbolText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        _symbolText.outlineWidth = 0.16f;
    }

    private void ApplyStaticStyle()
    {
        Sprite panelSprite = GetOrCreatePanelSprite();

        _borderImage.sprite = panelSprite;
        _panelImage.sprite = panelSprite;
        _borderImage.type = Image.Type.Simple;
        _panelImage.type = Image.Type.Simple;

        _borderImage.color = _accentColor;
        _panelImage.color = new Color(0.014f, 0.018f, 0.022f, 0.92f);
        _topAccent.color = _accentColor;
        _bottomAccent.color = _accentColor;
        _symbolText.color = _accentColor;

        float baseTilt =
            _kind == IndicatorKind.Suspicion ? -4f : 2f;

        _borderImage.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, baseTilt);
        _panelImage.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, baseTilt);
    }

    private void UpdateVisuals(float normalizedTime)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        float fadeStart =
            _kind == IndicatorKind.Suspicion ? 0.68f : 0.72f;

        float alpha = normalizedTime <= fadeStart
            ? 1f
            : 1f - SmoothStep01(
                Mathf.InverseLerp(
                    fadeStart,
                    1f,
                    normalizedTime
                )
            );

        _canvasGroup.alpha = alpha;

        float pulse = _kind == IndicatorKind.Suspicion
            ? 0.90f + Mathf.Sin(_elapsed * 23f) * 0.10f
            : 0.94f + Mathf.Sin(_elapsed * 31f) * 0.06f;

        Color accent = _accentColor * pulse;
        accent.a = 1f;

        _borderImage.color = accent;
        _topAccent.color = accent;
        _bottomAccent.color = accent;
        _symbolText.color = accent;

        if (_kind == IndicatorKind.Detected && normalizedTime < 0.26f)
        {
            float shakeFade = 1f - normalizedTime / 0.26f;
            float shake = Mathf.Sin(_elapsed * 72f) * 2.2f * shakeFade;

            _panelImage.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 2f + shake);
            _borderImage.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 2f + shake);
        }
    }

    private float ResolvePopScale(float normalizedTime)
    {
        float peakScale =
            _kind == IndicatorKind.Suspicion ? 1.12f : 1.26f;

        float firstPhase =
            _kind == IndicatorKind.Suspicion ? 0.22f : 0.15f;

        float settlePhase =
            _kind == IndicatorKind.Suspicion ? 0.42f : 0.34f;

        if (normalizedTime <= firstPhase)
        {
            float phaseTime = SmoothStep01(
                normalizedTime / firstPhase
            );

            return Mathf.Lerp(
                _kind == IndicatorKind.Suspicion ? 0.48f : 0.34f,
                peakScale,
                phaseTime
            );
        }

        if (normalizedTime <= settlePhase)
        {
            float phaseTime = SmoothStep01(
                Mathf.InverseLerp(
                    firstPhase,
                    settlePhase,
                    normalizedTime
                )
            );

            return Mathf.Lerp(peakScale, 1f, phaseTime);
        }

        return 1f;
    }

    private Image CreateImage(
        string objectName,
        RectTransform parent,
        Vector2 size,
        Vector2 position
    )
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );

        imageObject.transform.SetParent(parent, false);
        imageObject.layer = gameObject.layer;

        RectTransform rect =
            imageObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = GetOrCreatePanelSprite();

        return image;
    }

    private static Sprite GetOrCreatePanelSprite()
    {
        if (_sharedPanelSprite != null)
        {
            return _sharedPanelSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true
        )
        {
            name = "Runtime_GroundBotIndicatorPanel",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color transparent = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool cutTopLeft = x + y < 13;
                bool cutBottomRight = x + y > 112;
                bool cutTopRight = x - y > 53;
                bool cutBottomLeft = y - x > 57;

                texture.SetPixel(
                    x,
                    y,
                    cutTopLeft ||
                    cutBottomRight ||
                    cutTopRight ||
                    cutBottomLeft
                        ? transparent
                        : solid
                );
            }
        }

        texture.Apply(false, true);

        _sharedPanelSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        _sharedPanelSprite.name =
            "Runtime_GroundBotIndicatorPanelSprite";
        _sharedPanelSprite.hideFlags =
            HideFlags.HideAndDontSave;

        return _sharedPanelSprite;
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
