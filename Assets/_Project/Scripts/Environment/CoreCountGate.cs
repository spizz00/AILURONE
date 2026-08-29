#pragma warning disable 0618
#pragma warning disable 0414
using System.Collections;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class CoreCountGate : MonoBehaviour
{
    [Header("门引用")]
    [Tooltip("需要被打开的门。把挂有 GlitchDoor 的门拖到这里。")]
    public GlitchDoor targetDoor;

    [Header("解锁条件")]
    [Min(1)]
    [Tooltip("玩家当前至少需要持有多少个 Core。")]
    public int requiredCoreCount = 2;

    [Tooltip("多久检查一次 Core 数量。0.1 秒已经足够及时。")]
    [Min(0.02f)]
    public float checkInterval = 0.1f;

    [Header("可选视觉反馈")]
    [Tooltip("可选。收集满 2 个 Core 解锁时才显示的提示文本。")]
    public TMP_Text statusText;

    public Color unlockedTextColor = new Color(0.1f, 1f, 0.9f, 1f);

    [Tooltip("解锁提示文本显示的时长（秒），之后会自动隐藏。")]
    [Min(0.5f)]
    public float textDisplayDuration = 5.0f;

    [Tooltip("门未解锁时显示的物体，例如红色指示灯。")]
    public GameObject[] lockedIndicators;

    [Tooltip("门解锁后显示的物体，例如青色指示灯。")]
    public GameObject[] unlockedIndicators;

    [Header("可选解锁反馈")]
    public GameObject unlockEffectPrefab;
    public Transform unlockEffectPoint;
    public AudioClip unlockSound;

    [Range(0f, 1f)]
    public float unlockSoundVolume = 1f;

    [Header("终端与全局提示")]
    [Tooltip("解锁时是否在现有 Terminal Log 中显示信息。")]
    public bool sendTerminalLog = true;

    [Tooltip("显示在日志与 3D 悬浮提示里的区域名称。")]
    public string gateDisplayName = "HUB DOOR UNLOCKED";

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool _hasUnlocked = false;
    private float _checkTimer = 0f;

    private void Awake()
    {
        if (targetDoor == null)
        {
            targetDoor = GetComponent<GlitchDoor>();
        }

        if (targetDoor == null)
        {
            targetDoor = GetComponentInChildren<GlitchDoor>();
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }

        SetUnlockedVisualState(false);
    }

    private void Start()
    {
        EvaluateGate();
    }

    private void Update()
    {
        if (_hasUnlocked)
        {
            return;
        }

        _checkTimer -= Time.unscaledDeltaTime;

        if (_checkTimer > 0f)
        {
            return;
        }

        _checkTimer = checkInterval;
        EvaluateGate();
    }

    private void EvaluateGate()
    {
        if (_hasUnlocked)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            return;
        }

        int currentCoreCount = GameManager.Instance.GetCurrentBallsCount();

        if (currentCoreCount >= requiredCoreCount)
        {
            UnlockGate(currentCoreCount);
        }
    }

    private void UnlockGate(int currentCoreCount)
    {
        if (_hasUnlocked)
        {
            return;
        }

        _hasUnlocked = true;

        SetUnlockedVisualState(true);

        // Explicitly formats the 3D text to show gateDisplayName and ACCESS GRANTED
        if (statusText != null)
        {
            statusText.text =
                $"{gateDisplayName.ToUpper()}\n" +
                $"REWRITE NODES: {requiredCoreCount:00} / {requiredCoreCount:00}\n" +
                $"ACCESS GRANTED";

            statusText.color = unlockedTextColor;
            StartCoroutine(ShowTextForDurationRoutine(textDisplayDuration));
        }

        // 1. Spawn optional VFX
        if (unlockEffectPrefab != null)
        {
            Vector3 spawnPosition = unlockEffectPoint != null
                ? unlockEffectPoint.position
                : transform.position;

            Quaternion spawnRotation = unlockEffectPoint != null
                ? unlockEffectPoint.rotation
                : transform.rotation;

            Instantiate(unlockEffectPrefab, spawnPosition, spawnRotation);
        }

        // 2. Play unlock SFX
        if (unlockSound != null)
        {
            Vector3 soundPosition = unlockEffectPoint != null
                ? unlockEffectPoint.position
                : transform.position;

            AudioSource.PlayClipAtPoint(
                unlockSound,
                soundPosition,
                unlockSoundVolume
            );
        }

        // 3. Output to terminal feed
        if (sendTerminalLog && TerminalLogManager.Instance != null)
        {
            TerminalLogManager.Instance.AddLog(
                $"{requiredCoreCount:00}/{requiredCoreCount:00}",
                "ACCESS GRANTED",
                $"■ {gateDisplayName}"
            );
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"🔓 [CoreCountGate] {gateDisplayName} 已解锁。" +
                $"当前 Core：{currentCoreCount}，要求：{requiredCoreCount}。"
            );
        }

        // 4. Open the door
        if (targetDoor != null)
        {
            targetDoor.OpenDoor();
        }
        else
        {
            Debug.LogWarning(
                $"⚠️ [CoreCountGate] {gameObject.name} 没有设置 Target Door，" +
                "条件已经满足，但无法打开门。"
            );
        }
    }

    private IEnumerator ShowTextForDurationRoutine(float duration)
    {
        statusText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(duration);
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
    }

    private void SetUnlockedVisualState(bool unlocked)
    {
        SetObjectsActive(lockedIndicators, !unlocked);
        SetObjectsActive(unlockedIndicators, unlocked);
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }
    }

    public bool IsUnlocked()
    {
        return _hasUnlocked;
    }

    public void ForceUnlockForTesting()
    {
        if (!_hasUnlocked)
        {
            int currentCount = GameManager.Instance != null
                ? GameManager.Instance.GetCurrentBallsCount()
                : requiredCoreCount;

            UnlockGate(currentCount);
        }
    }
}