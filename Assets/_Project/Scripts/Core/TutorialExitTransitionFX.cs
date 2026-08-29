using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialExitTransitionFX : MonoBehaviour
{
    private const int StreakCount = 20;
    private const int ScanlineCount = 9;

    private readonly RectTransform[] _streaks =
        new RectTransform[StreakCount];
    private readonly Image[] _streakImages =
        new Image[StreakCount];
    private readonly RectTransform[] _scanlines =
        new RectTransform[ScanlineCount];
    private readonly Image[] _scanlineImages =
        new Image[ScanlineCount];

    private Camera _camera;
    private float _baseFieldOfView;
    private StarterAssets.FirstPersonController _playerController;
    private CanvasGroup _hudGroup;
    private float _hudBaseAlpha = 1f;
    private Image _flashImage;
    private AudioSource _portalAudio;
    private float _basePortalPitch;
    private float _basePortalVolume;

    public IEnumerator PlayRoutine(float duration)
    {
        BuildOverlay();
        ResolveSceneReferences();

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.1f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / safeDuration
            );
            ApplyFrame(progress);
            yield return null;
        }

        ApplyFrame(1f);
    }

    private void ResolveSceneReferences()
    {
        _camera = Camera.main;
        if (_camera != null)
        {
            _baseFieldOfView = _camera.fieldOfView;
        }

        _playerController =
            Object.FindAnyObjectByType<StarterAssets.FirstPersonController>();
        if (_playerController != null &&
            _playerController.virtualCamera != null)
        {
            _baseFieldOfView =
                _playerController.virtualCamera.Lens.FieldOfView;
        }

        GameObject hud = GameObject.Find("HUD_Canvas_AILURONE");
        if (hud != null)
        {
            _hudGroup = hud.GetComponent<CanvasGroup>();
            if (_hudGroup == null)
            {
                _hudGroup = hud.AddComponent<CanvasGroup>();
            }
            _hudBaseAlpha = _hudGroup.alpha;
        }

        GlitchPortal portal =
            Object.FindFirstObjectByType<GlitchPortal>();
        if (portal != null && portal.portalAudioSource != null)
        {
            _portalAudio = portal.portalAudioSource;
            _basePortalPitch = _portalAudio.pitch;
            _basePortalVolume = _portalAudio.volume;
        }
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject(
            "TutorialExitTransition_Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32761;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _flashImage = CreateImage(
            "NeuralFlash",
            canvasObject.transform,
            new Color(0.72f, 1f, 1f, 0f)
        );
        StretchToScreen(_flashImage.rectTransform);

        for (int index = 0; index < StreakCount; index++)
        {
            Image streak = CreateImage(
                $"RadialDataStreak_{index + 1}",
                canvasObject.transform,
                new Color(0.3f, 1f, 1f, 0f)
            );
            RectTransform rect = streak.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                620f + index % 5 * 115f,
                2f + index % 3 * 1.5f
            );
            rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                index * (360f / StreakCount) +
                Mathf.Sin(index * 2.17f) * 7f
            );
            rect.localScale = new Vector3(0.1f, 1f, 1f);

            _streaks[index] = rect;
            _streakImages[index] = streak;
        }

        for (int index = 0; index < ScanlineCount; index++)
        {
            Image scanline = CreateImage(
                $"NeuralScanline_{index + 1}",
                canvasObject.transform,
                new Color(0.5f, 1f, 1f, 0f)
            );
            RectTransform rect = scanline.rectTransform;
            float normalizedY =
                (index + 1f) / (ScanlineCount + 1f);
            rect.anchorMin = new Vector2(0.5f, normalizedY);
            rect.anchorMax = new Vector2(0.5f, normalizedY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(
                1920f,
                index % 3 == 0 ? 3f : 1.5f
            );

            _scanlines[index] = rect;
            _scanlineImages[index] = scanline;
        }
    }

    private void ApplyFrame(float progress)
    {
        float acceleration =
            progress * progress * progress;

        float targetFieldOfView =
            _baseFieldOfView + 26f * acceleration;

        if (_playerController != null &&
            _playerController.virtualCamera != null)
        {
            _playerController.virtualCamera.Lens.FieldOfView =
                targetFieldOfView;
        }

        if (_camera != null)
        {
            _camera.fieldOfView = targetFieldOfView;
        }

        if (_hudGroup != null)
        {
            _hudGroup.alpha =
                _hudBaseAlpha *
                (1f - Mathf.SmoothStep(0f, 1f, progress / 0.5f));
            _hudGroup.interactable = false;
            _hudGroup.blocksRaycasts = false;
        }

        float streakFade =
            Mathf.SmoothStep(0f, 1f, progress * 2.2f) *
            (1f - Mathf.SmoothStep(0.72f, 1f, progress));

        for (int index = 0; index < StreakCount; index++)
        {
            RectTransform rect = _streaks[index];
            rect.localScale = new Vector3(
                Mathf.Lerp(0.08f, 1.55f, acceleration),
                1f,
                1f
            );

            Color color = _streakImages[index].color;
            color.a =
                streakFade * (0.22f + index % 4 * 0.07f);
            _streakImages[index].color = color;
        }

        float scanFade =
            Mathf.Sin(progress * Mathf.PI) * 0.38f;
        for (int index = 0; index < ScanlineCount; index++)
        {
            RectTransform rect = _scanlines[index];
            rect.anchoredPosition = new Vector2(
                Mathf.Sin(
                    progress * 38f + index * 1.73f
                ) * (35f + progress * 120f),
                0f
            );

            Color color = _scanlineImages[index].color;
            float flicker =
                0.55f +
                0.45f *
                Mathf.Sin(progress * 75f + index * 2.4f);
            color.a = scanFade * Mathf.Max(0.1f, flicker);
            _scanlineImages[index].color = color;
        }

        float flashAlpha =
            progress < 0.68f
                ? progress * 0.08f
                : Mathf.SmoothStep(
                    0f,
                    1f,
                    (progress - 0.68f) / 0.32f
                );
        Color flash = _flashImage.color;
        flash.a = flashAlpha;
        _flashImage.color = flash;

        if (_portalAudio != null)
        {
            _portalAudio.pitch =
                Mathf.Lerp(
                    _basePortalPitch,
                    Mathf.Max(1.35f, _basePortalPitch + 0.35f),
                    acceleration
                );
            _portalAudio.volume =
                Mathf.Lerp(
                    _basePortalVolume,
                    Mathf.Max(0.5f, _basePortalVolume),
                    progress
                );
        }
    }

    private static Image CreateImage(
        string objectName,
        Transform parent,
        Color color
    )
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchToScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
