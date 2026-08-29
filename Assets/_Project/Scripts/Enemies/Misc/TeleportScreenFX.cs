#pragma warning disable 0618
#pragma warning disable 0414
using AILURONE.HUD;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Lightweight presentation bridge for teleport commit feedback.
/// The world-rewrite shader remains owned by TeleportWorldRewriteController;
/// this component only coordinates HUD, crosshair and FOV response.
/// </summary>
[DisallowMultipleComponent]
public sealed class TeleportScreenFX : MonoBehaviour
{
    [Header("Channel Fade")]
    [Range(0.02f, 0.15f)]
    public float hudHiddenDuration = 0.06f;

    [Min(1f)]
    public float hudRestoreSpeed = 12f;

    [Header("FOV Transition")]
    public float departureFOV = 112f;
    public float departureFOVSpeed = 8f;
    public float arrivalFOV = 106f;
    public float arrivalFOVSpeed = 18f;
    public float arrivalFOVDuration = 0.24f;

    private FirstPersonController _firstPersonController;
    private TeleportController _teleportController;
    private CanvasGroup _hudGroup;
    private float _hudOriginalAlpha = 1f;
    private float _hudVisibility = 1f;
    private float _hudHiddenRemaining;
    private float _channelProgress;
    private bool _channelActive;
    private bool _ownsExternalFOV;
    private bool _hudResolved;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveHUD();
    }

    private void Update()
    {
        if (_hudGroup == null)
        {
            ResolveHUD();
        }

        float delta =
            Time.unscaledDeltaTime;

        if (_hudHiddenRemaining > 0f)
        {
            _hudHiddenRemaining -= delta;
            _hudVisibility = 0f;
        }
        else if (_channelActive)
        {
            _hudHiddenRemaining = 0f;
            _hudVisibility =
                1f -
                Mathf.Pow(
                    _channelProgress,
                    2.15f
                );
        }
        else
        {
            _hudHiddenRemaining = 0f;
            _hudVisibility =
                Mathf.MoveTowards(
                    _hudVisibility,
                    1f,
                    hudRestoreSpeed * delta
                );
        }

        ApplyHUDVisibility();
    }

    public void BeginChannel()
    {
        ResolveReferences();
        ResolveHUD();

        _channelActive = true;
        _channelProgress = 0f;
        _hudHiddenRemaining = 0f;
        _hudVisibility = 1f;
        ApplyHUDVisibility();

        AILURONELensOverlay
            .RequestTeleportChannelStartScan();

        if (CrosshairController.Instance != null)
        {
            CrosshairController.Instance
                .SetTeleportChannelProgress(0f);
        }

        // TeleportController normally owns the channel FOV. This fallback
        // keeps the same gradual presentation when that option is disabled.
        if (ShouldControlFOV())
        {
            _firstPersonController
                .RequestExternalFOV(
                    departureFOV,
                    departureFOVSpeed,
                    0f
                );
            _ownsExternalFOV = true;
        }
    }

    public void SetChannelProgress(
        float progress
    )
    {
        _channelProgress =
            Mathf.Clamp01(progress);

        if (CrosshairController.Instance != null)
        {
            CrosshairController.Instance
                .SetTeleportChannelProgress(
                    _channelProgress
                );
        }
    }

    public void BeginDepartureFlash(
        float duration
    )
    {
        ResolveReferences();
        ResolveHUD();

        _channelProgress = 1f;
        _channelActive = false;

        float hiddenDuration =
            Mathf.Max(
                duration,
                hudHiddenDuration
            );

        _hudHiddenRemaining =
            Mathf.Max(
                _hudHiddenRemaining,
                hiddenDuration
            );
        _hudVisibility = 0f;
        ApplyHUDVisibility();

        if (CrosshairController.Instance != null)
        {
            CrosshairController.Instance
                .BeginTeleportCommitPulse(
                    hiddenDuration
                );
        }

    }

    public void CancelChannel()
    {
        _channelActive = false;
        _channelProgress = 0f;
        _hudHiddenRemaining = 0f;
        ApplyHUDVisibility();

        if (CrosshairController.Instance != null)
        {
            CrosshairController.Instance
                .CancelTeleportPresentation();
        }

        ReleaseOwnedFOV();
    }

    public void PlayArrivalBurst()
    {
        ResolveReferences();

        _channelActive = false;
        _channelProgress = 0f;

        if (CrosshairController.Instance != null)
        {
            CrosshairController.Instance
                .BeginTeleportArrivalRecovery();
        }

        AILURONELensOverlay
            .RequestTeleportArrivalScan();

        if (ShouldControlFOV())
        {
            _firstPersonController
                .RequestExternalFOV(
                    arrivalFOV,
                    arrivalFOVSpeed,
                    arrivalFOVDuration
                );
        }

        _ownsExternalFOV = false;
    }

    private void ResolveReferences()
    {
        if (_firstPersonController == null)
        {
            _firstPersonController =
                GetComponent<FirstPersonController>();
        }

        if (_teleportController == null)
        {
            _teleportController =
                GetComponent<TeleportController>();
        }
    }

    private void ResolveHUD()
    {
        if (_hudResolved)
        {
            return;
        }

        GameObject hudCanvas =
            GameObject.Find("HUD_Canvas_AILURONE");

        if (hudCanvas == null)
        {
            return;
        }

        Transform safeArea =
            hudCanvas.transform.Find("HUD_SafeArea");

        if (safeArea == null)
        {
            return;
        }

        _hudGroup =
            safeArea.GetComponent<CanvasGroup>();

        if (_hudGroup == null)
        {
            _hudGroup =
                safeArea.gameObject
                    .AddComponent<CanvasGroup>();
        }

        _hudOriginalAlpha =
            _hudGroup.alpha;
        _hudResolved = true;
    }

    private bool ShouldControlFOV()
    {
        return
            _firstPersonController != null &&
            (_teleportController == null ||
             !_teleportController.useChannelFOV);
    }

    private void ApplyHUDVisibility()
    {
        if (_hudGroup == null)
        {
            return;
        }

        _hudGroup.alpha =
            _hudOriginalAlpha *
            _hudVisibility;
    }

    private void ReleaseOwnedFOV()
    {
        if (!_ownsExternalFOV ||
            _firstPersonController == null)
        {
            return;
        }

        _firstPersonController
            .ReleaseExternalFOV(18f);
        _ownsExternalFOV = false;
    }

    private void RestoreImmediately()
    {
        _channelActive = false;
        _channelProgress = 0f;
        _hudHiddenRemaining = 0f;
        _hudVisibility = 1f;
        ApplyHUDVisibility();
        ReleaseOwnedFOV();

        if (CrosshairController.Instance != null)
        {
            CrosshairController.Instance
                .CancelTeleportPresentation();
        }
    }

    private void OnDisable()
    {
        RestoreImmediately();
    }

    private void OnDestroy()
    {
        RestoreImmediately();
    }

    private static float Smooth01(
        float value
    )
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hudHiddenDuration =
            Mathf.Clamp(
                hudHiddenDuration,
                0.02f,
                0.15f
            );
        hudRestoreSpeed =
            Mathf.Max(
                1f,
                hudRestoreSpeed
            );
        departureFOVSpeed =
            Mathf.Max(
                1f,
                departureFOVSpeed
            );
        arrivalFOVSpeed =
            Mathf.Max(
                1f,
                arrivalFOVSpeed
            );
        arrivalFOVDuration =
            Mathf.Max(
                0.01f,
                arrivalFOVDuration
            );
    }
#endif
}
