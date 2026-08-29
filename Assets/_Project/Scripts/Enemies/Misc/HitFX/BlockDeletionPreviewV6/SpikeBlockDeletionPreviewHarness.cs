#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class SpikeBlockDeletionPreviewHarnessV6 : MonoBehaviour
{
    public SpikeBlockDeletionPreviewControllerV6 controller;

    [Header("Auto Preview")]
    public bool autoCycle;
    [Min(0.5f)] public float autoInterval = 1.05f;

    private float _autoTimer;
    private int _autoIndex;

    private void Start()
    {
        controller?.ConfigureIdle(false);
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
        _autoIndex = (_autoIndex + 1) % 8;
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
            if (keyboard.digit7Key.wasPressedThisFrame) PlayIndex(6);
            if (keyboard.digit8Key.wasPressedThisFrame) PlayIndex(7);
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
        if (Input.GetKeyDown(KeyCode.Alpha7)) PlayIndex(6);
        if (Input.GetKeyDown(KeyCode.Alpha8)) PlayIndex(7);
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
        SpikeBlockDeletionPreviewControllerV6.PreviewMode mode;
        if (index < 2)
        {
            mode = SpikeBlockDeletionPreviewControllerV6.PreviewMode.Hipfire;
        }
        else if (index < 4)
        {
            mode = SpikeBlockDeletionPreviewControllerV6.PreviewMode.AdsNonLethal;
        }
        else if (index < 6)
        {
            mode = SpikeBlockDeletionPreviewControllerV6.PreviewMode.AdsLethal;
        }
        else
        {
            mode = SpikeBlockDeletionPreviewControllerV6.PreviewMode.Kill;
        }

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
        const int width = 470;
        const int height = 224;
        GUI.Box(new Rect(16, 16, width, height), string.Empty);
        GUI.Label(new Rect(30, 26, width - 28, 24), "AILURONE — BLOCK DELETION HIT FX V6");
        GUI.Label(new Rect(30, 52, width - 28, 22), "1  Blue / Hipfire       2  Magenta / Hipfire");
        GUI.Label(new Rect(30, 74, width - 28, 22), "3  Blue / ADS Non-Lethal  4  Magenta / ADS Non-Lethal");
        GUI.Label(new Rect(30, 96, width - 28, 22), "5  Blue / ADS Lethal     6  Magenta / ADS Lethal");
        GUI.Label(new Rect(30, 118, width - 28, 22), "7  Blue / Kill           8  Magenta / Kill");
        GUI.Label(new Rect(30, 144, width - 28, 22), "R  Auto Cycle            C  Clear");
        GUI.Label(new Rect(30, 170, width - 28, 22), "Current: " + (autoCycle ? "AUTO" : "MANUAL"));
        GUI.Label(new Rect(30, 192, width - 28, 20), "V6 visual study only — no weapon / damage / AI changes.");
    }
}
