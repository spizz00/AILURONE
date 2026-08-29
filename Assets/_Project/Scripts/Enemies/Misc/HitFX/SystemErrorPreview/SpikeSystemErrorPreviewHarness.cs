#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class SpikeSystemErrorPreviewHarness : MonoBehaviour
{
    public SpikeSystemErrorPreviewController controller;

    [Header("Auto Preview")]
    public bool autoCycle;
    [Min(0.5f)] public float autoInterval = 1.15f;

    private float _autoTimer;
    private int _autoIndex;

    private void Start()
    {
        if (controller != null)
        {
            controller.ConfigureIdle(false);
        }
    }

    private void Update()
    {
        HandleInput();

        if (!autoCycle || controller == null)
        {
            return;
        }

        _autoTimer += Time.unscaledDeltaTime;
        if (_autoTimer < autoInterval)
        {
            return;
        }

        _autoTimer = 0f;
        PlayIndex(_autoIndex);
        _autoIndex = (_autoIndex + 1) % 6;
    }

    private void HandleInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) PlayIndex(0);
            if (keyboard.digit2Key.wasPressedThisFrame) PlayIndex(1);
            if (keyboard.digit3Key.wasPressedThisFrame) PlayIndex(2);
            if (keyboard.digit4Key.wasPressedThisFrame) PlayIndex(3);
            if (keyboard.digit5Key.wasPressedThisFrame) PlayIndex(4);
            if (keyboard.digit6Key.wasPressedThisFrame) PlayIndex(5);
            if (keyboard.rKey.wasPressedThisFrame) ToggleAuto();
            if (keyboard.cKey.wasPressedThisFrame) ClearPreview();
            return;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlayIndex(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayIndex(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) PlayIndex(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) PlayIndex(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) PlayIndex(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) PlayIndex(5);
        if (Input.GetKeyDown(KeyCode.R)) ToggleAuto();
        if (Input.GetKeyDown(KeyCode.C)) ClearPreview();
#endif
    }

    public void PlayIndex(int index)
    {
        if (controller == null)
        {
            return;
        }

        bool magenta = index % 2 == 1;
        SpikeSystemErrorPreviewController.PreviewMode mode =
            index < 2
                ? SpikeSystemErrorPreviewController.PreviewMode.Hipfire
                : index < 4
                    ? SpikeSystemErrorPreviewController.PreviewMode.Ads
                    : SpikeSystemErrorPreviewController.PreviewMode.Kill;

        controller.Play(mode, magenta);
    }

    private void ToggleAuto()
    {
        autoCycle = !autoCycle;
        _autoTimer = 0f;
    }

    private void ClearPreview()
    {
        autoCycle = false;
        _autoTimer = 0f;
        controller?.StopPreview();
    }

    private void OnGUI()
    {
        const int width = 350;
        const int height = 188;
        GUI.Box(new Rect(16, 16, width, height), string.Empty);
        GUI.Label(new Rect(30, 26, width - 28, 24), "AILURONE — SYSTEM ERROR HIT FX V4");
        GUI.Label(new Rect(30, 52, width - 28, 22), "1  Blue / Hipfire     2  Magenta / Hipfire");
        GUI.Label(new Rect(30, 74, width - 28, 22), "3  Blue / ADS         4  Magenta / ADS");
        GUI.Label(new Rect(30, 96, width - 28, 22), "5  Blue / Kill        6  Magenta / Kill");
        GUI.Label(new Rect(30, 122, width - 28, 22), "R  Auto Cycle         C  Clear");
        GUI.Label(new Rect(30, 148, width - 28, 22), "Current: " + (autoCycle ? "AUTO" : "MANUAL"));
        GUI.Label(new Rect(30, 169, width - 28, 20), "Preview only — no weapon, damage, AI or formal prefab changes.");
    }
}
