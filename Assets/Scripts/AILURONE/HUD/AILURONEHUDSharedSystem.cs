#pragma warning disable 0618
#pragma warning disable 0414
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    internal static class AILURONEHUDSharedSystem
    {
        private const string BootstrapName =
            "AILURONE_HUD_SharedSystem";

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBootstrap()
        {
            AILURONEHUDSharedBootstrap[] existing =
                Object.FindObjectsByType<AILURONEHUDSharedBootstrap>(
                    FindObjectsInactive.Include);

            if (existing.Length > 0)
            {
                return;
            }

            GameObject gameObject = new GameObject(BootstrapName);
            Object.DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<AILURONEHUDSharedBootstrap>();
        }
    }

    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    internal sealed class AILURONEHUDSharedBootstrap : MonoBehaviour
    {
        private const string CanvasName =
            "HUD_Canvas_AILURONE";

        private const string SafeAreaName =
            "HUD_SafeArea";

        private const string HudResourcePath =
            "AILURONE/HUD/AILURONE_GameModeHUD";

        private const float RetryInterval = 0.5f;

        private float _nextRetryAt;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _nextRetryAt = 0f;
        }

        private void Start()
        {
            PrepareScene(SceneManager.GetActiveScene());
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRetryAt)
            {
                return;
            }

            _nextRetryAt =
                Time.unscaledTime + RetryInterval;

            PrepareScene(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadMode)
        {
            _nextRetryAt = 0f;
            PrepareScene(scene);
        }

        private static void PrepareScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameManager gameManager =
                FindSceneComponent<GameManager>(scene);

            // Menus and non-gameplay scenes deliberately receive no HUD.
            if (gameManager == null)
            {
                return;
            }

            Canvas canvas = FindCanonicalCanvas(scene);

            if (canvas == null)
            {
                canvas = InstantiateSharedHUD(scene);
            }

            if (canvas == null)
            {
                // The Resources prefab may not have been generated yet.
                // The bootstrap retries quietly without replacing legacy UI.
                return;
            }

            ApplyLevelEntryVisibility(scene, canvas);
            BindGameManager(gameManager, canvas);
            EnsureLensOverlay(canvas);
            EnsurePostProcessing(scene);
        }

        private static void ApplyLevelEntryVisibility(
            Scene scene,
            Canvas canvas)
        {
            LevelEntrySequenceController entrySequence =
                FindSceneComponent<LevelEntrySequenceController>(scene);

            if (entrySequence == null || entrySequence.DeploymentStarted)
            {
                return;
            }

            CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static Canvas InstantiateSharedHUD(Scene scene)
        {
            GameObject prefab =
                Resources.Load<GameObject>(HudResourcePath);

            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab);

            if (instance.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(instance, scene);
            }

            Canvas canvas = FindCanvasInHierarchy(instance.transform);

            if (canvas != null)
            {
                canvas.gameObject.name = CanvasName;
            }
            else
            {
                // Do not accumulate invalid instances while quietly retrying.
                Object.Destroy(instance);
            }

            return canvas;
        }

        private static void BindGameManager(
            GameManager gameManager,
            Canvas canvas)
        {
            TextMeshProUGUI timer =
                FindText(canvas.transform, "TimerValue");

            TextMeshProUGUI score =
                FindText(canvas.transform, "ScoreValue");

            TextMeshProUGUI adjustment =
                FindText(canvas.transform, "TimerAdjustment");

            Text winText =
                FindLegacyText(canvas.transform, "WinTextUI");

            if (timer != null)
            {
                gameManager.timerTextUI = timer;
            }

            if (score != null)
            {
                gameManager.scoreTextUI = score;
            }

            if (adjustment != null)
            {
                gameManager.stackBufferTextUI = adjustment;
            }

            if (winText != null)
            {
                gameManager.winTextUI = winText;
            }
        }

        private static void EnsurePostProcessing(Scene scene)
        {
            Camera[] cameras =
                Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include);

            Camera mainCamera = null;

            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];

                if (candidate == null ||
                    candidate.gameObject.scene != scene)
                {
                    continue;
                }

                if (mainCamera == null)
                {
                    mainCamera = candidate;
                }

                if (candidate.CompareTag("MainCamera"))
                {
                    mainCamera = candidate;
                    break;
                }
            }

            if (mainCamera == null)
            {
                return;
            }

            UniversalAdditionalCameraData cameraData =
                mainCamera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData == null)
            {
                cameraData =
                    mainCamera.gameObject
                        .AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
        }

        private static void EnsureLensOverlay(Canvas canvas)
        {
            RectTransform safeArea =
                FindRect(canvas.transform, SafeAreaName);

            if (safeArea == null)
            {
                GameObject safeAreaObject = new GameObject(
                    SafeAreaName,
                    typeof(RectTransform));

                safeAreaObject.layer = canvas.gameObject.layer;
                safeAreaObject.transform.SetParent(
                    canvas.transform,
                    false);

                safeArea =
                    safeAreaObject.GetComponent<RectTransform>();

                StretchToParent(safeArea);
            }

            AILURONELensOverlay overlay =
                safeArea.GetComponentInChildren<AILURONELensOverlay>(
                    true);

            if (overlay == null)
            {
                GameObject overlayObject = new GameObject(
                    "VisorLensOverlay",
                    typeof(RectTransform),
                    typeof(CanvasGroup));

                overlayObject.layer = canvas.gameObject.layer;
                overlayObject.transform.SetParent(safeArea, false);

                RectTransform overlayRect =
                    overlayObject.GetComponent<RectTransform>();

                StretchToParent(overlayRect);

                CanvasGroup canvasGroup =
                    overlayObject.GetComponent<CanvasGroup>();

                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                overlay =
                    overlayObject.AddComponent<AILURONELensOverlay>();
            }

            overlay.transform.SetAsFirstSibling();
        }

        private static Canvas FindCanonicalCanvas(Scene scene)
        {
            Canvas[] canvases =
                Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include);

            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];

                if (canvas != null &&
                    canvas.gameObject.scene == scene &&
                    canvas.gameObject.name == CanvasName)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static Canvas FindCanvasInHierarchy(Transform root)
        {
            Canvas[] canvases =
                root.GetComponentsInChildren<Canvas>(true);

            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];

                if (canvas.gameObject.name == CanvasName)
                {
                    return canvas;
                }
            }

            return canvases.Length > 0
                ? canvases[0]
                : null;
        }

        private static TextMeshProUGUI FindText(
            Transform root,
            string objectName)
        {
            TextMeshProUGUI[] texts =
                root.GetComponentsInChildren<TextMeshProUGUI>(true);

            for (int index = 0; index < texts.Length; index++)
            {
                TextMeshProUGUI text = texts[index];

                if (text.gameObject.name == objectName)
                {
                    return text;
                }
            }

            return null;
        }

        private static Text FindLegacyText(
            Transform root,
            string objectName)
        {
            Text[] texts =
                root.GetComponentsInChildren<Text>(true);

            for (int index = 0; index < texts.Length; index++)
            {
                Text text = texts[index];

                if (text.gameObject.name == objectName)
                {
                    return text;
                }
            }

            return null;
        }

        private static RectTransform FindRect(
            Transform root,
            string objectName)
        {
            RectTransform[] rects =
                root.GetComponentsInChildren<RectTransform>(true);

            for (int index = 0; index < rects.Length; index++)
            {
                RectTransform rect = rects[index];

                if (rect.gameObject.name == objectName)
                {
                    return rect;
                }
            }

            return null;
        }

        private static T FindSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] candidates =
                Object.FindObjectsByType<T>(
                    FindObjectsInactive.Include);

            for (int index = 0; index < candidates.Length; index++)
            {
                T candidate = candidates[index];

                if (candidate != null &&
                    candidate.gameObject.scene == scene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class AILURONELensOverlay : MonoBehaviour
    {
        private const float ResolveInterval = 0.5f;
        private const float BaseEdgeAlpha = 0.0045f;

        private static AILURONELensOverlay _activeInstance;

        private static readonly Color LensColor =
            new Color(0.64f, 0.92f, 1f, 1f);

        private RectTransform _root;
        private Image _scanBand;
        private Image[] _edgeReflections =
            System.Array.Empty<Image>();

        private Texture2D _scanTexture;
        private Sprite _scanSprite;

        private PlayerHealth _playerHealth;
        private TimeManager _timeManager;
        private DashController _dashController;

        private bool _subscribedToHealth;
        private bool _lastDashActive;
        private bool _lastOverclockActive;

        private float _nextResolveAt;
        private float _nextIdleScanAt;
        private float _scanElapsed;
        private float _scanDuration;
        private float _scanPeakAlpha;
        private bool _scanActive;

        internal static void RequestTeleportArrivalScan()
        {
            if (_activeInstance == null)
            {
                return;
            }

            _activeInstance.RequestEventScan(
                0.05f,
                0.36f
            );
        }

        internal static void RequestTeleportChannelStartScan()
        {
            if (_activeInstance == null)
            {
                return;
            }

            _activeInstance.RequestEventScan(
                0.04f,
                0.18f
            );
        }

        internal void EnsureVisuals()
        {
            _root = transform as RectTransform;

            if (_root == null)
            {
                return;
            }

            _scanBand = EnsureImage(
                _root,
                "LensScanBand");

            ConfigureScanBand(_scanBand.rectTransform);

            if (_scanSprite == null)
            {
                CreateScanSprite();
            }

            _scanBand.sprite = _scanSprite;
            _scanBand.type = Image.Type.Simple;
            _scanBand.preserveAspect = false;

            _edgeReflections = new[]
            {
                EnsureImage(_root, "LensEdgeTop"),
                EnsureImage(_root, "LensEdgeBottom"),
                EnsureImage(_root, "LensEdgeLeft"),
                EnsureImage(_root, "LensEdgeRight")
            };

            ConfigureEdge(
                _edgeReflections[0].rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -1f),
                new Vector2(0f, 2f));

            ConfigureEdge(
                _edgeReflections[1].rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 2f));

            ConfigureEdge(
                _edgeReflections[2].rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0f),
                new Vector2(2f, 0f));

            ConfigureEdge(
                _edgeReflections[3].rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(-1f, 0f),
                new Vector2(2f, 0f));

            ApplyVisuals(0f, 0f);
        }

        private void Awake()
        {
            EnsureVisuals();
        }

        private void OnEnable()
        {
            _activeInstance = this;
            EnsureVisuals();
            _nextResolveAt = 0f;
            ScheduleIdleScan();
            ResolveReferences();
            SubscribeToHealth();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextResolveAt)
            {
                _nextResolveAt =
                    Time.unscaledTime + ResolveInterval;

                ResolveReferences();
            }

            DetectAbilityTransitions();

            bool suppressed = ShouldSuppress();

            if (!suppressed &&
                !_scanActive &&
                Time.unscaledTime >= _nextIdleScanAt)
            {
                BeginScan(
                    Random.Range(0.010f, 0.016f),
                    Random.Range(0.8f, 1.0f));
            }

            UpdateScan(suppressed);
        }

        private void OnDisable()
        {
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }

            UnsubscribeFromHealth();
            CancelScan();
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }

            UnsubscribeFromHealth();

            if (_scanSprite != null)
            {
                Destroy(_scanSprite);
            }

            if (_scanTexture != null)
            {
                Destroy(_scanTexture);
            }
        }

        private void ResolveReferences()
        {
            Scene scene = gameObject.scene;

            PlayerHealth resolvedHealth =
                FindSceneComponent<PlayerHealth>(scene);

            if (resolvedHealth != _playerHealth)
            {
                UnsubscribeFromHealth();
                _playerHealth = resolvedHealth;
                SubscribeToHealth();
            }

            TimeManager resolvedTimeManager =
                FindSceneComponent<TimeManager>(scene);

            if (resolvedTimeManager != _timeManager)
            {
                _timeManager = resolvedTimeManager;
                _lastOverclockActive =
                    _timeManager != null &&
                    _timeManager.IsAbilityActive;
            }

            DashController resolvedDash =
                FindSceneComponent<DashController>(scene);

            if (resolvedDash != _dashController)
            {
                _dashController = resolvedDash;
                _lastDashActive =
                    _dashController != null &&
                    _dashController.isDashing;
            }
        }

        private void SubscribeToHealth()
        {
            if (_subscribedToHealth || _playerHealth == null)
            {
                return;
            }

            _playerHealth.Damaged += HandleDamaged;
            _playerHealth.RewindStarted += HandleRewindStarted;
            _playerHealth.RewindCompleted += HandleRewindCompleted;
            _subscribedToHealth = true;
        }

        private void UnsubscribeFromHealth()
        {
            if (!_subscribedToHealth || _playerHealth == null)
            {
                _subscribedToHealth = false;
                return;
            }

            _playerHealth.Damaged -= HandleDamaged;
            _playerHealth.RewindStarted -= HandleRewindStarted;
            _playerHealth.RewindCompleted -= HandleRewindCompleted;
            _subscribedToHealth = false;
        }

        private void DetectAbilityTransitions()
        {
            bool dashActive =
                _dashController != null &&
                _dashController.isDashing;

            if (dashActive && !_lastDashActive)
            {
                RequestEventScan(0.033f, 0.21f);
            }

            _lastDashActive = dashActive;

            bool overclockActive =
                _timeManager != null &&
                _timeManager.IsAbilityActive;

            if (overclockActive && !_lastOverclockActive)
            {
                RequestEventScan(0.034f, 0.28f);
            }

            _lastOverclockActive = overclockActive;
        }

        private void HandleDamaged(
            float actualDamage,
            float remainingHealth)
        {
            if (_playerHealth != null &&
                _playerHealth.IsRewinding)
            {
                return;
            }

            RequestEventScan(0.027f, 0.18f);
        }

        private void HandleRewindStarted()
        {
            CancelScan();
            ScheduleIdleScan();
        }

        private void HandleRewindCompleted()
        {
            ScheduleIdleScan();
        }

        private void RequestEventScan(
            float peakAlpha,
            float duration)
        {
            if (ShouldSuppress())
            {
                return;
            }

            peakAlpha = Mathf.Clamp(peakAlpha, 0f, 0.05f);
            duration = Mathf.Max(0.05f, duration);

            if (!_scanActive || peakAlpha > _scanPeakAlpha)
            {
                BeginScan(peakAlpha, duration);
                return;
            }

            // Concurrent events extend the stronger pulse but never add alpha.
            _scanDuration = Mathf.Max(
                _scanDuration,
                _scanElapsed + duration);
        }

        private void BeginScan(
            float peakAlpha,
            float duration)
        {
            _scanActive = true;
            _scanElapsed = 0f;
            _scanDuration = Mathf.Max(0.05f, duration);
            _scanPeakAlpha = Mathf.Clamp(peakAlpha, 0f, 0.05f);
        }

        private void UpdateScan(bool suppressed)
        {
            if (!_scanActive)
            {
                ApplyVisuals(0f, 0f);
                return;
            }

            _scanElapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                _scanElapsed /
                Mathf.Max(0.05f, _scanDuration));

            if (progress >= 1f)
            {
                CancelScan();
                ScheduleIdleScan();
                return;
            }

            if (suppressed)
            {
                ApplyVisuals(0f, 0f);
                return;
            }

            float envelope =
                Mathf.Sin(progress * Mathf.PI);

            ApplyVisuals(
                _scanPeakAlpha * envelope,
                progress);
        }

        private void ApplyVisuals(
            float scanAlpha,
            float scanProgress)
        {
            if (_scanBand != null)
            {
                _scanBand.color = new Color(
                    LensColor.r,
                    LensColor.g,
                    LensColor.b,
                    scanAlpha);

                float height =
                    _root != null && _root.rect.height > 1f
                        ? _root.rect.height
                        : 1080f;

                float eased = Mathf.SmoothStep(
                    0f,
                    1f,
                    scanProgress);

                float y = Mathf.Lerp(
                    -height * 0.62f,
                    height * 0.62f,
                    eased);

                _scanBand.rectTransform.anchoredPosition =
                    new Vector2(0f, y);
            }

            float edgeAlpha =
                BaseEdgeAlpha + scanAlpha * 0.24f;

            for (int index = 0;
                index < _edgeReflections.Length;
                index++)
            {
                Image edge = _edgeReflections[index];

                if (edge == null)
                {
                    continue;
                }

                edge.color = new Color(
                    LensColor.r,
                    LensColor.g,
                    LensColor.b,
                    edgeAlpha);
            }
        }

        private bool ShouldSuppress()
        {
            if (_playerHealth != null &&
                _playerHealth.IsRewinding)
            {
                return true;
            }

            VisualFeedbackController feedback =
                VisualFeedbackController.Instance;

            return feedback != null &&
                feedback.SuppressVisorOverlay;
        }

        private void CancelScan()
        {
            _scanActive = false;
            _scanElapsed = 0f;
            _scanDuration = 0f;
            _scanPeakAlpha = 0f;
            ApplyVisuals(0f, 0f);
        }

        private void ScheduleIdleScan()
        {
            _nextIdleScanAt =
                Time.unscaledTime +
                Random.Range(9f, 14f);
        }

        private void CreateScanSprite()
        {
            const int height = 64;

            _scanTexture = new Texture2D(
                1,
                height,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "AILURONE_RuntimeLensScan",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[height];

            for (int index = 0; index < height; index++)
            {
                float t = index / (float)(height - 1);
                float alpha = Mathf.Pow(
                    Mathf.Sin(t * Mathf.PI),
                    3f);

                pixels[index] =
                    new Color(1f, 1f, 1f, alpha);
            }

            _scanTexture.SetPixels(pixels);
            _scanTexture.Apply(false, true);

            _scanSprite = Sprite.Create(
                _scanTexture,
                new Rect(0f, 0f, 1f, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);

            _scanSprite.name = "AILURONE_RuntimeLensScanSprite";
            _scanSprite.hideFlags = HideFlags.DontSave;
        }

        private static Image EnsureImage(
            RectTransform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            Image image;

            if (existing != null)
            {
                image = existing.GetComponent<Image>();

                if (image == null)
                {
                    image = existing.gameObject.AddComponent<Image>();
                }
            }
            else
            {
                GameObject imageObject = new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

                imageObject.layer = parent.gameObject.layer;
                imageObject.transform.SetParent(parent, false);
                image = imageObject.GetComponent<Image>();
            }

            image.raycastTarget = false;
            image.maskable = false;
            return image;
        }

        private static void ConfigureScanBand(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(120f, 150f);
            rect.localScale = Vector3.one;
            rect.localEulerAngles = new Vector3(0f, 0f, -2f);
        }

        private static void ConfigureEdge(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;
        }

        private static T FindSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] candidates =
                Object.FindObjectsByType<T>(
                    FindObjectsInactive.Include);

            for (int index = 0; index < candidates.Length; index++)
            {
                T candidate = candidates[index];

                if (candidate != null &&
                    candidate.gameObject.scene == scene)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
