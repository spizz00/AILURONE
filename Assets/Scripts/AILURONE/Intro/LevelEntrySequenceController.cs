using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-31000)]
[DisallowMultipleComponent]
public sealed class LevelEntrySequenceController : MonoBehaviour
{
    private const string HudCanvasName = "HUD_Canvas_AILURONE";
    private const string CrosshairRootName = "CrosshairSystem_Runtime";

    [Header("Scene References")]
    [SerializeField] private Transform player;
    [SerializeField] private AlwaysEquippedWeaponController weaponController;
    [SerializeField] private EscapePodFailureController escapePod;

    [Header("Deployment Timing")]
    [SerializeField] private float hudBootDuration = 1.85f;
    [SerializeField] private float shootingUnlockNormalizedTime = 0.88f;

    [Header("Weapon Stow")]
    [SerializeField] private Vector3 stowedWeaponPosition =
        new Vector3(0f, -2.2f, -0.7f);
    [SerializeField] private Vector3 stowedWeaponEuler =
        new Vector3(45f, 0f, 0f);

    private CanvasGroup _hudGroup;
    private CanvasGroup _bootOverlayGroup;
    private RectTransform _scanBand;
    private Image[] _bootBlocks = System.Array.Empty<Image>();
    private Image[] _reticleSegments = System.Array.Empty<Image>();
    private TextMeshProUGUI[] _statusLabels =
        System.Array.Empty<TextMeshProUGUI>();
    private TextMeshProUGUI _bootLabel;
    private Texture2D _scanTexture;
    private Sprite _scanSprite;
    private bool _deploymentStarted;
    private bool _weaponRaiseStarted;
    private bool _shootingUnlocked;
    private readonly List<HudBootModule> _hudModules =
        new List<HudBootModule>();

    private sealed class HudBootModule
    {
        public Transform Root;
        public CanvasGroup Group;
        public CrosshairController Crosshair;
        public float OriginalAlpha;
        public float DesiredAlpha;
        public bool OriginalInteractable;
        public bool OriginalBlocksRaycasts;
        public bool OwnsGroup;
        public float Start;
        public float End;
    }

    public bool DeploymentStarted => _deploymentStarted;
    public Transform Player => player;

    public void Configure(
        Transform playerTransform,
        AlwaysEquippedWeaponController equippedWeaponController,
        EscapePodFailureController podController)
    {
        player = playerTransform;
        weaponController = equippedWeaponController;
        escapePod = podController;
    }

    public void ConfigureBootPresentation(
        float duration,
        float shootingUnlockTime)
    {
        hudBootDuration = Mathf.Max(0.5f, duration);
        shootingUnlockNormalizedTime = Mathf.Clamp01(
            shootingUnlockTime);
    }

    private void Awake()
    {
        AILURONEGameplayActionGate.SetDeploymentLocked(true);
        ResolveReferences();
        StowWeapon();
    }

    private IEnumerator Start()
    {
        // GameManager starts its timer in Start. Waiting one frame lets this
        // sequence establish the deployment boundary without changing it.
        yield return null;

        if (!_deploymentStarted && GameManager.Instance != null)
        {
            GameManager.Instance.StopTimer();
        }

        while (!_deploymentStarted)
        {
            ResolveHud();
            SetHudAlpha(0f, false);
            yield return null;
        }
    }

    public void BeginDeployment()
    {
        if (_deploymentStarted)
        {
            return;
        }

        _deploymentStarted = true;

        ResolveReferences();
        ResolveHud();

        PlayerHealth playerHealth =
            player != null
                ? player.GetComponent<PlayerHealth>()
                : null;

        if (playerHealth != null)
        {
            playerHealth.ResetRewindHistory();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartTimer();
        }

        if (escapePod != null)
        {
            escapePod.NotifyDeploymentStarted();
        }

        StartCoroutine(PlayHudBootSequence());
    }

    private IEnumerator PlayHudBootSequence()
    {
        CreateBootOverlay();

        while (_hudGroup == null)
        {
            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            ResolveHud();
            UpdateBootOverlay(0.02f);
            yield return null;
        }

        CreateHudBootModules();
        SetHudAlpha(1f, false);

        float duration = Mathf.Max(0.25f, hudBootDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (AILURONEGameplayActionGate.IsPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float progress = Mathf.Clamp01(elapsed / duration);

            if (!_weaponRaiseStarted && progress >= 0.54f)
            {
                _weaponRaiseStarted = true;

                if (weaponController != null)
                {
                    weaponController.ClearExternalVisualOffset(5.5f);
                }
            }

            if (!_shootingUnlocked &&
                progress >= shootingUnlockNormalizedTime)
            {
                UnlockShooting();
                AILURONEGameplayActionGate.SetDeploymentLocked(false);
            }

            ResolveHud();
            SetHudAlpha(1f, false);
            UpdateHudBootModules(progress);
            UpdateBootOverlay(progress);
            yield return null;
        }

        SetHudAlpha(1f, true);
        UnlockShooting();
        AILURONEGameplayActionGate.SetDeploymentLocked(false);
        DestroyHudBootModules();
        DestroyBootOverlay();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (weaponController == null && player != null)
        {
            weaponController =
                player.GetComponent<AlwaysEquippedWeaponController>();
        }
    }

    private void StowWeapon()
    {
        if (weaponController == null)
        {
            return;
        }

        weaponController.allowShooting = false;
        weaponController.SnapExternalVisualOffset(
            stowedWeaponPosition,
            stowedWeaponEuler);
    }

    private void UnlockShooting()
    {
        if (_shootingUnlocked)
        {
            return;
        }

        _shootingUnlocked = true;

        if (weaponController != null)
        {
            weaponController.allowShooting = true;
        }
    }

    private void ResolveHud()
    {
        if (_hudGroup != null)
        {
            return;
        }

        GameObject canonical = GameObject.Find(HudCanvasName);

        if (canonical != null)
        {
            Canvas canonicalCanvas = canonical.GetComponent<Canvas>();

            if (canonicalCanvas != null)
            {
                _hudGroup = canonical.GetComponent<CanvasGroup>();

                if (_hudGroup == null)
                {
                    _hudGroup = canonical.AddComponent<CanvasGroup>();
                }

                return;
            }
        }

        Canvas[] canvases =
            Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        for (int index = 0; index < canvases.Length; index++)
        {
            Canvas canvas = canvases[index];

            if (canvas == null ||
                canvas.gameObject.name != HudCanvasName)
            {
                continue;
            }

            _hudGroup = canvas.GetComponent<CanvasGroup>();

            if (_hudGroup == null)
            {
                _hudGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }

            break;
        }
    }

    private void SetHudAlpha(float alpha, bool interactive)
    {
        if (_hudGroup == null)
        {
            return;
        }

        _hudGroup.alpha = Mathf.Clamp01(alpha);
        _hudGroup.interactable = interactive;
        _hudGroup.blocksRaycasts = interactive;
    }

    private void CreateHudBootModules()
    {
        DestroyHudBootModules();

        if (_hudGroup == null)
        {
            return;
        }

        AddCrosshairBootModule(0.05f, 0.28f);
        AddHudBootModule("BottomLeft_FinalIntegrity", 0.20f, 0.48f);
        AddHudBootModule("AbilityHUD_AuxiliaryRail", 0.34f, 0.61f);
        AddHudBootModule("BottomCenter_Score", 0.48f, 0.72f);
        AddHudBootModule("TopCenter_Timer", 0.61f, 0.84f);
        AddHudBootModule("TopRight_CoreObjective", 0.74f, 0.97f);

        if (_hudModules.Count < 4)
        {
            Transform safeArea = FindDescendant(
                _hudGroup.transform,
                "HUD_SafeArea");

            if (safeArea != null)
            {
                int fallbackIndex = 0;

                for (int index = 0; index < safeArea.childCount; index++)
                {
                    Transform child = safeArea.GetChild(index);

                    if (child.name == "VisorLensOverlay")
                    {
                        continue;
                    }

                    float start = Mathf.Clamp01(
                        0.18f + fallbackIndex * 0.12f);
                    AddHudBootModule(
                        child,
                        start,
                        Mathf.Min(0.98f, start + 0.24f));
                    fallbackIndex++;
                }
            }
        }
    }

    private void AddHudBootModule(
        string objectName,
        float start,
        float end)
    {
        Transform root = FindDescendant(
            _hudGroup.transform,
            objectName);

        AddHudBootModule(root, start, end);
    }

    private void AddHudBootModule(
        Transform root,
        float start,
        float end)
    {

        if (root == null)
        {
            return;
        }

        if (root.name == CrosshairRootName &&
            CrosshairController.Instance != null)
        {
            AddCrosshairBootModule(start, end);
            return;
        }

        for (int index = 0; index < _hudModules.Count; index++)
        {
            if (_hudModules[index].Root == root)
            {
                return;
            }
        }

        CanvasGroup bootGroup = root.GetComponent<CanvasGroup>();
        bool ownsGroup = bootGroup == null;

        if (ownsGroup)
        {
            bootGroup = root.gameObject.AddComponent<CanvasGroup>();
        }

        float originalAlpha = bootGroup.alpha;
        bool originalInteractable = bootGroup.interactable;
        bool originalBlocksRaycasts = bootGroup.blocksRaycasts;
        bootGroup.alpha = 0f;
        bootGroup.interactable = false;
        bootGroup.blocksRaycasts = false;

        _hudModules.Add(new HudBootModule
        {
            Root = root,
            Group = bootGroup,
            OriginalAlpha = originalAlpha,
            DesiredAlpha = 0f,
            OriginalInteractable = originalInteractable,
            OriginalBlocksRaycasts = originalBlocksRaycasts,
            OwnsGroup = ownsGroup,
            Start = start,
            End = Mathf.Max(start + 0.02f, end)
        });
    }

    private void AddCrosshairBootModule(float start, float end)
    {
        CrosshairController crosshair = CrosshairController.Instance;

        if (crosshair == null)
        {
            return;
        }

        for (int index = 0; index < _hudModules.Count; index++)
        {
            if (_hudModules[index].Crosshair == crosshair)
            {
                return;
            }
        }

        crosshair.SetDeploymentAlpha(0f);

        _hudModules.Add(new HudBootModule
        {
            Crosshair = crosshair,
            DesiredAlpha = 0f,
            Start = start,
            End = Mathf.Max(start + 0.02f, end)
        });
    }

    private void UpdateHudBootModules(float progress)
    {
        for (int index = 0; index < _hudModules.Count; index++)
        {
            HudBootModule module = _hudModules[index];

            if (module.Group == null && module.Crosshair == null)
            {
                continue;
            }

            float local = Mathf.InverseLerp(
                module.Start,
                module.End,
                progress);
            float reveal = Mathf.SmoothStep(0f, 1f, local);
            float flicker = local < 0.34f
                ? (Mathf.FloorToInt(Time.unscaledTime * 38f + index) % 3 == 0
                    ? 0.28f
                    : 1f)
                : 1f;

            module.DesiredAlpha = reveal * flicker;

            if (module.Crosshair != null)
            {
                module.Crosshair.SetDeploymentAlpha(module.DesiredAlpha);
            }
            else
            {
                module.Group.alpha = module.DesiredAlpha;
            }
        }
    }

    private void LateUpdate()
    {
        for (int index = 0; index < _hudModules.Count; index++)
        {
            HudBootModule module = _hudModules[index];

            if (module.Crosshair != null)
            {
                module.Crosshair.SetDeploymentAlpha(module.DesiredAlpha);
            }
            else if (module.Group != null)
            {
                module.Group.alpha = module.DesiredAlpha;
            }
        }
    }

    private void DestroyHudBootModules()
    {
        for (int index = 0; index < _hudModules.Count; index++)
        {
            HudBootModule module = _hudModules[index];
            CanvasGroup group = module.Group;

            if (module.Crosshair != null)
            {
                module.Crosshair.SetDeploymentAlpha(1f);
            }

            if (group != null)
            {
                if (module.OwnsGroup)
                {
                    Destroy(group);
                }
                else
                {
                    group.alpha = module.OriginalAlpha;
                    group.interactable = module.OriginalInteractable;
                    group.blocksRaycasts = module.OriginalBlocksRaycasts;
                }
            }
        }

        _hudModules.Clear();
    }

    private static Transform FindDescendant(
        Transform root,
        string objectName)
    {
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            if (transforms[index].name == objectName)
            {
                return transforms[index];
            }
        }

        return null;
    }

    private void CreateBootOverlay()
    {
        if (_bootOverlayGroup != null)
        {
            return;
        }

        GameObject overlayObject = new GameObject(
            "HUD_DeploymentBootOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));

        Canvas canvas = overlayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = overlayObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _bootOverlayGroup = overlayObject.GetComponent<CanvasGroup>();
        _bootOverlayGroup.interactable = false;
        _bootOverlayGroup.blocksRaycasts = false;

        _scanSprite = CreateScanSprite();

        Image scanImage = CreateImage(
            overlayObject.transform,
            "DeploymentScanBand");

        _scanBand = scanImage.rectTransform;
        _scanBand.anchorMin = new Vector2(0f, 0.5f);
        _scanBand.anchorMax = new Vector2(1f, 0.5f);
        _scanBand.pivot = new Vector2(0.5f, 0.5f);
        _scanBand.sizeDelta = new Vector2(120f, 190f);
        scanImage.sprite = _scanSprite;
        scanImage.type = Image.Type.Simple;
        scanImage.color = new Color(0.72f, 0.86f, 0.88f, 0.22f);

        _bootBlocks = new[]
        {
            CreateBootBlock(overlayObject.transform, "BootBlock_TopLeft", new Vector2(0.08f, 0.88f), new Vector2(250f, 16f)),
            CreateBootBlock(overlayObject.transform, "BootBlock_TopRight", new Vector2(0.83f, 0.88f), new Vector2(170f, 16f)),
            CreateBootBlock(overlayObject.transform, "BootBlock_Left", new Vector2(0.06f, 0.48f), new Vector2(12f, 150f)),
            CreateBootBlock(overlayObject.transform, "BootBlock_Right", new Vector2(0.93f, 0.52f), new Vector2(12f, 180f)),
            CreateBootBlock(overlayObject.transform, "BootBlock_BottomLeft", new Vector2(0.10f, 0.13f), new Vector2(210f, 12f)),
            CreateBootBlock(overlayObject.transform, "BootBlock_BottomRight", new Vector2(0.80f, 0.13f), new Vector2(260f, 12f))
        };

        _reticleSegments = new[]
        {
            CreateReticleSegment(overlayObject.transform, "Optic_Left", true),
            CreateReticleSegment(overlayObject.transform, "Optic_Right", true),
            CreateReticleSegment(overlayObject.transform, "Optic_Top", false),
            CreateReticleSegment(overlayObject.transform, "Optic_Bottom", false)
        };

        _statusLabels = new[]
        {
            CreateStatusLabel(
                overlayObject.transform,
                "OpticStatus",
                "OPTIC LINK  //  CALIBRATING",
                new Vector2(0.5f, 0.57f)),
            CreateStatusLabel(
                overlayObject.transform,
                "VitalsStatus",
                "VITALS  //  ONLINE",
                new Vector2(0.12f, 0.22f)),
            CreateStatusLabel(
                overlayObject.transform,
                "MobilityStatus",
                "MOTION  //  SYNCHRONIZED",
                new Vector2(0.83f, 0.18f)),
            CreateStatusLabel(
                overlayObject.transform,
                "MissionStatus",
                "MISSION LINK  //  VERIFIED",
                new Vector2(0.80f, 0.84f))
        };

        GameObject labelObject = new GameObject(
            "DeploymentBootLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        labelObject.transform.SetParent(overlayObject.transform, false);
        _bootLabel = labelObject.GetComponent<TextMeshProUGUI>();
        _bootLabel.font = TMP_Settings.defaultFontAsset;
        _bootLabel.text = "AILURONE // DEPLOYMENT LINK";
        _bootLabel.fontSize = 18f;
        _bootLabel.alignment = TextAlignmentOptions.Center;
        _bootLabel.characterSpacing = 5f;
        _bootLabel.color = new Color(0.53f, 1f, 0.97f, 0f);
        _bootLabel.raycastTarget = false;

        RectTransform labelRect = _bootLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.78f);
        labelRect.anchorMax = new Vector2(0.5f, 0.78f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(620f, 46f);
    }

    private void UpdateBootOverlay(float progress)
    {
        if (_bootOverlayGroup == null)
        {
            return;
        }

        float scanProgress = Mathf.Clamp01(progress / 0.68f);
        float easedScan = Mathf.SmoothStep(0f, 1f, scanProgress);
        _scanBand.anchoredPosition = new Vector2(
            0f,
            Mathf.Lerp(-650f, 650f, easedScan));

        float overlayFade =
            1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 1f, progress));

        _bootOverlayGroup.alpha = overlayFade;

        for (int index = 0; index < _bootBlocks.Length; index++)
        {
            float appear = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0.08f + index * 0.035f,
                    0.16f + index * 0.035f,
                    progress));

            Color color = _bootBlocks[index].color;
            color.a = 0.34f * appear;
            _bootBlocks[index].color = color;
        }

        float opticIn = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(0.03f, 0.20f, progress));
        float opticOut = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(0.29f, 0.43f, progress));
        float opticAlpha = opticIn * opticOut;
        float opticOffset = Mathf.Lerp(170f, 28f, opticIn);

        for (int index = 0; index < _reticleSegments.Length; index++)
        {
            Image segment = _reticleSegments[index];

            if (segment == null)
            {
                continue;
            }

            Vector2 direction = index switch
            {
                0 => Vector2.left,
                1 => Vector2.right,
                2 => Vector2.up,
                _ => Vector2.down
            };
            segment.rectTransform.anchoredPosition =
                direction * opticOffset;
            Color color = segment.color;
            color.a = 0.92f * opticAlpha;
            segment.color = color;
        }

        float[] statusStarts = { 0.05f, 0.22f, 0.43f, 0.70f };

        for (int index = 0; index < _statusLabels.Length; index++)
        {
            TextMeshProUGUI status = _statusLabels[index];

            if (status == null)
            {
                continue;
            }

            float appear = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    statusStarts[index],
                    statusStarts[index] + 0.10f,
                    progress));
            float fade = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.86f, 1f, progress));
            Color color = status.color;
            color.a = 0.72f * appear * fade;
            status.color = color;
        }

        if (_bootLabel != null)
        {
            _bootLabel.text = progress < 0.24f
                ? "AILURONE // OPTIC LINK"
                : progress < 0.52f
                    ? "AILURONE // BIOMETRICS ONLINE"
                    : progress < 0.76f
                        ? "AILURONE // ORDNANCE SYNC"
                        : "AILURONE // DEPLOYMENT VERIFIED";
            Color color = _bootLabel.color;
            color.a =
                0.82f * Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.20f, 0.34f, progress));
            _bootLabel.color = color;
        }
    }

    private Sprite CreateScanSprite()
    {
        const int height = 64;

        _scanTexture = new Texture2D(
            1,
            height,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "AILURONE_DeploymentScan",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[height];

        for (int index = 0; index < height; index++)
        {
            float t = index / (float)(height - 1);
            float alpha = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2.5f);
            pixels[index] = new Color(1f, 1f, 1f, alpha);
        }

        _scanTexture.SetPixels(pixels);
        _scanTexture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            _scanTexture,
            new Rect(0f, 0f, 1f, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        sprite.name = "AILURONE_DeploymentScanSprite";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static Image CreateBootBlock(
        Transform parent,
        string objectName,
        Vector2 anchor,
        Vector2 size)
    {
        Image image = CreateImage(parent, objectName);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        image.color = new Color(0.64f, 0.78f, 0.80f, 0f);
        return image;
    }

    private static Image CreateReticleSegment(
        Transform parent,
        string objectName,
        bool horizontal)
    {
        Image image = CreateImage(parent, objectName);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = horizontal
            ? new Vector2(46f, 2f)
            : new Vector2(2f, 46f);
        image.color = new Color(0.53f, 1f, 0.97f, 0f);
        return image;
    }

    private static TextMeshProUGUI CreateStatusLabel(
        Transform parent,
        string objectName,
        string text,
        Vector2 anchor)
    {
        GameObject labelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label =
            labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = 13f;
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = 4f;
        label.color = new Color(0.53f, 1f, 0.97f, 0f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(390f, 30f);
        return label;
    }

    private static Image CreateImage(Transform parent, string objectName)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void DestroyBootOverlay()
    {
        if (_bootOverlayGroup != null)
        {
            Destroy(_bootOverlayGroup.gameObject);
            _bootOverlayGroup = null;
            _bootBlocks = System.Array.Empty<Image>();
            _reticleSegments = System.Array.Empty<Image>();
            _statusLabels = System.Array.Empty<TextMeshProUGUI>();
        }
    }

    private void FinishSequenceSafely()
    {
        StopAllCoroutines();
        _deploymentStarted = true;

        if (escapePod != null)
        {
            escapePod.NotifyDeploymentStarted();
        }

        if (weaponController != null)
        {
            weaponController.allowShooting = true;
            weaponController.ClearExternalVisualOffset(5.5f);
        }

        _shootingUnlocked = true;
        SetHudAlpha(1f, true);
        DestroyHudBootModules();
        DestroyBootOverlay();
        AILURONEGameplayActionGate.SetDeploymentLocked(false);
    }

    private void OnDisable()
    {
        FinishSequenceSafely();
    }

    private void OnDestroy()
    {
        FinishSequenceSafely();

        if (_scanSprite != null)
        {
            Destroy(_scanSprite);
        }

        if (_scanTexture != null)
        {
            Destroy(_scanTexture);
        }
    }
}
