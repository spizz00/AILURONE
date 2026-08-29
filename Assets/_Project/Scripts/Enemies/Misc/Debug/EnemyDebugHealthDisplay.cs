#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime world-space enemy health bar for Ground Bot, Spike, Ophanim and Flying Bot.
///
/// Visual direction:
/// - very thin white segmented bar;
/// - five equal segments;
/// - no enemy name and no numeric health text;
/// - health is consumed from right to left;
/// - the active segment can be partially filled;
/// - hidden by default and revealed only after this enemy takes real damage;
/// - player weapon damage and Ground Bot projectile damage use the same
///   EnemyTarget.Damaged event, so both reveal and refresh the bar.
///
/// The display is created completely at runtime and does not alter enemy
/// damage, AI or reward logic.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDebugHealthDisplay : MonoBehaviour
{
    private const int SegmentCount = 5;

    [Header("References")]
    [SerializeField]
    private EnemyTarget enemyTarget;

    [Header("Placement")]
    [Min(0f)]
    [SerializeField]
    private float verticalPadding = 0.30f;

    [Min(1f)]
    [SerializeField]
    private float maximumVisibleDistance = 55f;

    [Header("Screen Readability")]
    [Min(0.001f)]
    [SerializeField]
    private float referenceWorldScale = 0.0045f;

    [Min(1f)]
    [SerializeField]
    private float referenceDistance = 10f;

    [SerializeField]
    private Vector2 scaleMultiplierRange = new Vector2(0.8f, 2.45f);

    [Header("Damage Reveal")]
    [Min(0.1f)]
    [SerializeField]
    private float visibleDurationAfterDamage = 2.5f;

    [Range(0f, 1f)]
    [SerializeField]
    private float fadeOutDuration = 0.28f;

    private RectTransform displayRoot;
    private Canvas displayCanvas;
    private CanvasGroup displayCanvasGroup;
    private readonly Image[] segmentFills = new Image[SegmentCount];
    private readonly RectTransform[] segmentFillRects =
        new RectTransform[SegmentCount];
    private Camera cachedCamera;
    private Renderer[] targetRenderers;
    private Vector3 stableLocalAnchor;
    private bool hasStableAnchor;
    private bool hasSubscribed;
    private bool targetDied;
    private float revealUntilTime = float.NegativeInfinity;

    private static readonly Color FrameColor =
        new Color(1f, 1f, 1f, 0.96f);

    private static readonly Color PanelColor =
        new Color(0.012f, 0.015f, 0.018f, 0.88f);

    private static readonly Color EmptySegmentColor =
        new Color(0.035f, 0.043f, 0.05f, 0.94f);

    private static readonly Color FillColor =
        new Color(1f, 1f, 1f, 1f);

    private void Awake()
    {
        ResolveReferences();

        if (enemyTarget == null)
        {
            enabled = false;
            return;
        }

        CacheTargetRenderers();
        CacheStableAnchor();
        BuildRuntimeDisplay();
        RefreshDisplay();
        SetDisplayImmediatelyHidden();
    }

    private void OnEnable()
    {
        Subscribe();
        EnemyDebugHealthDisplayManager.VisibilityChanged +=
            HandleGlobalVisibilityChanged;
    }

    private void OnDisable()
    {
        Unsubscribe();
        EnemyDebugHealthDisplayManager.VisibilityChanged -=
            HandleGlobalVisibilityChanged;
    }

    private void LateUpdate()
    {
        if (displayCanvas == null || targetDied)
        {
            return;
        }

        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
        }

        float remainingRevealTime =
            revealUntilTime - Time.time;

        bool revealIsActive =
            remainingRevealTime > 0f;

        bool shouldShow =
            EnemyDebugHealthDisplayManager.DisplaysVisible &&
            revealIsActive &&
            cachedCamera != null &&
            enemyTarget != null &&
            !enemyTarget.IsDead;

        if (!shouldShow)
        {
            displayCanvas.enabled = false;
            return;
        }

        if (displayCanvasGroup != null)
        {
            float safeFadeDuration =
                Mathf.Clamp(
                    fadeOutDuration,
                    0f,
                    visibleDurationAfterDamage
                );

            displayCanvasGroup.alpha =
                safeFadeDuration <= 0.001f ||
                remainingRevealTime >= safeFadeDuration
                    ? 1f
                    : Mathf.Clamp01(
                        remainingRevealTime /
                        safeFadeDuration
                    );
        }

        Vector3 worldAnchor = ResolveWorldAnchor();
        Vector3 cameraToAnchor = worldAnchor - cachedCamera.transform.position;
        float distance = cameraToAnchor.magnitude;

        bool isInFront =
            Vector3.Dot(
                cachedCamera.transform.forward,
                cameraToAnchor
            ) > 0.05f;

        if (!isInFront || distance > maximumVisibleDistance)
        {
            displayCanvas.enabled = false;
            return;
        }

        displayCanvas.enabled = true;
        displayRoot.position = worldAnchor;
        displayRoot.rotation = Quaternion.LookRotation(
            worldAnchor - cachedCamera.transform.position,
            cachedCamera.transform.up
        );

        float distanceMultiplier = Mathf.Clamp(
            distance / Mathf.Max(1f, referenceDistance),
            Mathf.Max(0.1f, scaleMultiplierRange.x),
            Mathf.Max(scaleMultiplierRange.x, scaleMultiplierRange.y)
        );

        float worldScale = referenceWorldScale * distanceMultiplier;
        displayRoot.localScale = Vector3.one * worldScale;
    }

    private void OnValidate()
    {
        maximumVisibleDistance =
            Mathf.Max(1f, maximumVisibleDistance);
        referenceWorldScale =
            Mathf.Max(0.001f, referenceWorldScale);
        referenceDistance =
            Mathf.Max(1f, referenceDistance);
        visibleDurationAfterDamage =
            Mathf.Max(0.1f, visibleDurationAfterDamage);
        fadeOutDuration =
            Mathf.Clamp(
                fadeOutDuration,
                0f,
                visibleDurationAfterDamage
            );
    }

    private void ResolveReferences()
    {
        if (enemyTarget == null)
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }
    }

    private void CacheTargetRenderers()
    {
        Renderer[] allRenderers =
            GetComponentsInChildren<Renderer>(true);

        int validCount = 0;

        foreach (Renderer candidate in allRenderers)
        {
            if (IsStableBodyRenderer(candidate))
            {
                validCount++;
            }
        }

        targetRenderers = new Renderer[validCount];
        int writeIndex = 0;

        foreach (Renderer candidate in allRenderers)
        {
            if (!IsStableBodyRenderer(candidate))
            {
                continue;
            }

            targetRenderers[writeIndex] = candidate;
            writeIndex++;
        }
    }

    private bool IsStableBodyRenderer(Renderer candidate)
    {
        if (candidate == null ||
            (!(candidate is MeshRenderer) &&
             !(candidate is SkinnedMeshRenderer)))
        {
            return false;
        }

        // Runtime projectile pools are children of Ground Bot while idle.
        // They must never contribute to the health-bar anchor.
        if (candidate.GetComponentInParent<GroundBotProjectile>() != null)
        {
            return false;
        }

        // Ignore inactive runtime helpers and pooled visuals.
        if (!candidate.gameObject.activeInHierarchy || !candidate.enabled)
        {
            return false;
        }

        return true;
    }

    private void CacheStableAnchor()
    {
        bool foundRenderer = false;
        Bounds combinedBounds = default;

        if (targetRenderers != null)
        {
            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                if (!foundRenderer)
                {
                    combinedBounds = targetRenderer.bounds;
                    foundRenderer = true;
                }
                else
                {
                    combinedBounds.Encapsulate(targetRenderer.bounds);
                }
            }
        }

        Vector3 worldAnchor;

        if (foundRenderer)
        {
            worldAnchor = new Vector3(
                combinedBounds.center.x,
                combinedBounds.max.y + verticalPadding,
                combinedBounds.center.z
            );
        }
        else
        {
            worldAnchor = transform.position + Vector3.up * 2f;
        }

        stableLocalAnchor = transform.InverseTransformPoint(worldAnchor);
        hasStableAnchor = true;
    }

    private void Subscribe()
    {
        if (hasSubscribed || enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged += HandleDamaged;
        enemyTarget.Died += HandleDied;
        hasSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!hasSubscribed || enemyTarget == null)
        {
            return;
        }

        enemyTarget.Damaged -= HandleDamaged;
        enemyTarget.Died -= HandleDied;
        hasSubscribed = false;
    }

    private void HandleDamaged(
        float actualDamage,
        float remainingHealth,
        Vector3 hitPoint
    )
    {
        if (actualDamage <= 0f || targetDied)
        {
            return;
        }

        RefreshDisplay();

        revealUntilTime =
            Time.time +
            Mathf.Max(0.1f, visibleDurationAfterDamage);

        if (displayCanvasGroup != null)
        {
            displayCanvasGroup.alpha = 1f;
        }
    }

    private void HandleDied(EnemyDeathInfo deathInfo)
    {
        targetDied = true;

        if (displayCanvas != null)
        {
            displayCanvas.enabled = false;
        }
    }

    private void HandleGlobalVisibilityChanged(bool visible)
    {
        if (displayCanvas != null && !visible)
        {
            displayCanvas.enabled = false;
        }

        if (displayCanvasGroup != null && !visible)
        {
            displayCanvasGroup.alpha = 0f;
        }
    }

    private Vector3 ResolveWorldAnchor()
    {
        if (!hasStableAnchor)
        {
            CacheStableAnchor();
        }

        return transform.TransformPoint(stableLocalAnchor);
    }

    private void BuildRuntimeDisplay()
    {
        GameObject rootObject =
            new GameObject(
                "EnemyHealthBar",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup)
            );

        displayRoot = rootObject.GetComponent<RectTransform>();
        displayRoot.SetParent(transform, false);
        displayRoot.sizeDelta = new Vector2(300f, 22f);
        displayRoot.localScale = Vector3.one * referenceWorldScale;

        displayCanvas = rootObject.GetComponent<Canvas>();
        displayCanvas.renderMode = RenderMode.WorldSpace;
        displayCanvas.overrideSorting = true;
        displayCanvas.sortingOrder = 5000;

        displayCanvasGroup = rootObject.GetComponent<CanvasGroup>();
        displayCanvasGroup.interactable = false;
        displayCanvasGroup.blocksRaycasts = false;
        displayCanvasGroup.ignoreParentGroups = true;

        // Dark backing keeps the white line readable against bright scenery.
        Image backing = CreateImage(
            "Backing",
            displayRoot,
            PanelColor,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        backing.raycastTarget = false;

        // Thin four-sided white frame.
        CreateBorderStrip(
            "TopFrame",
            displayRoot,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -2f),
            new Vector2(0f, 0f)
        );

        CreateBorderStrip(
            "BottomFrame",
            displayRoot,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 2f)
        );

        CreateBorderStrip(
            "LeftFrame",
            displayRoot,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(2f, 0f)
        );

        CreateBorderStrip(
            "RightFrame",
            displayRoot,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-2f, 0f),
            new Vector2(0f, 0f)
        );

        RectTransform segmentArea = CreateRectTransform(
            "SegmentArea",
            displayRoot,
            new Vector2(5f, 5f),
            new Vector2(-5f, -5f)
        );

        const float gap = 4f;
        float totalWidth = 290f;
        float availableWidth = totalWidth - gap * (SegmentCount - 1);
        float segmentWidth = availableWidth / SegmentCount;

        for (int i = 0; i < SegmentCount; i++)
        {
            float left = i * (segmentWidth + gap);

            RectTransform segmentRoot = CreateAbsoluteRectTransform(
                $"Segment_{i + 1}",
                segmentArea,
                new Vector2(left, 0f),
                new Vector2(segmentWidth, 12f)
            );

            Image segmentBackground = segmentRoot.gameObject.AddComponent<Image>();
            segmentBackground.color = EmptySegmentColor;
            segmentBackground.raycastTarget = false;

            RectTransform fillRect = CreateRectTransform(
                "Fill",
                segmentRoot,
                Vector2.zero,
                Vector2.zero
            );

            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.color = FillColor;
            fillImage.type = Image.Type.Simple;
            fillImage.raycastTarget = false;

            segmentFills[i] = fillImage;
            segmentFillRects[i] = fillRect;
        }
    }

    private void SetDisplayImmediatelyHidden()
    {
        revealUntilTime = float.NegativeInfinity;

        if (displayCanvasGroup != null)
        {
            displayCanvasGroup.alpha = 0f;
        }

        if (displayCanvas != null)
        {
            displayCanvas.enabled = false;
        }
    }

    private void RefreshDisplay()
    {
        if (enemyTarget == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(enemyTarget.HealthNormalized);
        float scaledHealth = normalized * SegmentCount;

        for (int i = 0; i < segmentFills.Length; i++)
        {
            if (segmentFills[i] == null)
            {
                continue;
            }

            // Left segments remain full first; health disappears from right to left.
            // RectTransform anchors are used instead of Image.fillAmount because
            // runtime Images without a source sprite do not reliably apply the
            // Filled rendering mode in every Unity/URP configuration.
            float segmentAmount =
                Mathf.Clamp01(scaledHealth - i);

            RectTransform fillRect =
                segmentFillRects[i];

            if (fillRect != null)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax =
                    new Vector2(segmentAmount, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                fillRect.gameObject.SetActive(
                    segmentAmount > 0.0001f
                );
            }
        }
    }

    private static void CreateBorderStrip(
        string objectName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        Image image = CreateImage(
            objectName,
            parent,
            FrameColor,
            anchorMin,
            anchorMax,
            offsetMin,
            offsetMax
        );

        image.raycastTarget = false;
    }

    private static Image CreateImage(
        string objectName,
        RectTransform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        GameObject imageObject =
            new GameObject(objectName, typeof(RectTransform), typeof(Image));

        RectTransform rectTransform =
            imageObject.GetComponent<RectTransform>();

        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRectTransform(
        string objectName,
        RectTransform parent,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        GameObject rectObject =
            new GameObject(objectName, typeof(RectTransform));

        RectTransform rectTransform =
            rectObject.GetComponent<RectTransform>();

        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        return rectTransform;
    }

    private static RectTransform CreateAbsoluteRectTransform(
        string objectName,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        GameObject rectObject =
            new GameObject(objectName, typeof(RectTransform));

        RectTransform rectTransform =
            rectObject.GetComponent<RectTransform>();

        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        return rectTransform;
    }
}
