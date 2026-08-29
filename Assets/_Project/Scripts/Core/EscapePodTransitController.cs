using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EscapePodTransitController : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;
    [SerializeField, Min(0f)] private double timelineStartSeconds = 2.8d;
    [SerializeField, Min(0.01f)] private double timelineEndSeconds = 10.2d;
    [SerializeField, Min(0.1f)] private float transitDuration = 4.8f;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.55f;
    [SerializeField] private string nextSceneName = "Level";

    private CanvasGroup _fadeGroup;
    private Image _fadeImage;
    private bool _loadingNextScene;

    public bool IsTransitRunning { get; private set; }
    public double TimelineStartSeconds => timelineStartSeconds;
    public double TimelineEndSeconds => timelineEndSeconds;
    public float TransitDuration => transitDuration;

    private void Awake()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        AudioListener.pause = false;

        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }

        if (director != null)
        {
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.Stop();
        }

        BuildFadeOverlay();
    }

    private void Start()
    {
        StartCoroutine(PlayTransitRoutine());
    }

    private IEnumerator PlayTransitRoutine()
    {
        IsTransitRunning = true;

        if (director == null || director.playableAsset == null)
        {
            Debug.LogError(
                "[EscapePodTransit] Missing director or timeline. Loading Level behind the transition frame.",
                this
            );
            yield return null;
            LoadNextScene();
            yield break;
        }

        double safeStart = System.Math.Max(0d, timelineStartSeconds);
        double safeEnd = System.Math.Min(
            director.playableAsset.duration,
            System.Math.Max(safeStart + 0.01d, timelineEndSeconds)
        );

        director.time = safeStart;
        director.Evaluate();
        yield return FadeRoutine(1f, 0f, fadeInDuration);

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.1f, transitDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / safeDuration);
            director.time = safeStart + (safeEnd - safeStart) * normalized;
            director.Evaluate();
            yield return null;
        }

        director.time = safeEnd;
        director.Evaluate();

        if (_fadeImage != null)
        {
            _fadeImage.color = Color.black;
        }

        yield return FadeRoutine(0f, 1f, fadeOutDuration);
        LoadNextScene();
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (_fadeGroup == null)
        {
            yield break;
        }

        if (duration <= 0.001f)
        {
            _fadeGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        _fadeGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            _fadeGroup.alpha = Mathf.LerpUnclamped(from, to, eased);
            yield return null;
        }

        _fadeGroup.alpha = to;
    }

    private void LoadNextScene()
    {
        if (_loadingNextScene)
        {
            return;
        }

        _loadingNextScene = true;
        IsTransitRunning = false;

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError(
                $"[EscapePodTransit] Scene '{nextSceneName}' is unavailable in Build Settings.",
                this
            );
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void BuildFadeOverlay()
    {
        GameObject canvasObject = new GameObject(
            "EscapePodTransit_FadeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup)
        );

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _fadeGroup = canvasObject.GetComponent<CanvasGroup>();
        _fadeGroup.alpha = 1f;
        _fadeGroup.interactable = false;
        _fadeGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject(
            "TransitEntryFrame",
            typeof(RectTransform),
            typeof(Image)
        );
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _fadeImage = imageObject.GetComponent<Image>();
        _fadeImage.color = new Color(0.72f, 1f, 1f, 1f);
        _fadeImage.raycastTarget = false;
    }

    private void OnValidate()
    {
        timelineStartSeconds = System.Math.Max(0d, timelineStartSeconds);
        timelineEndSeconds = System.Math.Max(
            timelineStartSeconds + 0.01d,
            timelineEndSeconds
        );
        transitDuration = Mathf.Max(0.1f, transitDuration);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            nextSceneName = "Level";
        }
    }
}
