#pragma warning disable 0618
#pragma warning disable 0414
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Global runtime switch for developer-only enemy health displays.
/// F8 toggles all debug health displays without changing enemy gameplay.
/// </summary>
[DefaultExecutionOrder(-9000)]
public sealed class EnemyDebugHealthDisplayManager : MonoBehaviour
{
    private const string RuntimeObjectName =
        "[AILURONE] Enemy Debug Health Display Manager";

    private static EnemyDebugHealthDisplayManager instance;
    private static bool displaysVisible = true;

    public static bool DisplaysVisible => displaysVisible;

    public static event Action<bool> VisibilityChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeManager()
    {
        if (instance != null)
        {
            return;
        }

        EnemyDebugHealthDisplayManager existing =
            FindAnyObjectByType<EnemyDebugHealthDisplayManager>();

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject managerObject =
            new GameObject(RuntimeObjectName);

        instance =
            managerObject.AddComponent<EnemyDebugHealthDisplayManager>();

        DontDestroyOnLoad(managerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!WasTogglePressedThisFrame())
        {
            return;
        }

        displaysVisible = !displaysVisible;
        VisibilityChanged?.Invoke(displaysVisible);

        Debug.Log(
            displaysVisible
                ? "[AILURONE] Enemy debug health displays: ON"
                : "[AILURONE] Enemy debug health displays: OFF"
        );
    }

    private static bool WasTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F8);
#else
        return false;
#endif
    }
}
